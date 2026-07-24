using UnityEngine;
using SpinMotion;
using Gmtk2026.GameBalance;

namespace GMTK
{
    /// <summary>
    /// Per-car curse actuation (checklist stages 3.7-3.10). Owns the shared cooldown/rotation
    /// (<see cref="CurseCooldownState"/>), auto-selects a target from the live race order, and
    /// applies the chosen curse: Rupture -> target durability, Engine Seal -> target boost,
    /// Soul Swap -> a guarded position swap. Casting is caster-agnostic: the player triggers a
    /// successful cast through the quiz, AI (stage 9) calls <see cref="CastAtAutoTarget"/>
    /// directly. The E-input / quiz / HUD layer is wired on top of these hooks.
    /// </summary>
    [DisallowMultipleComponent]
    public class CurseController : MonoBehaviour
    {
        public CurseCooldownState State { get; private set; }
        public bool CanCast => State != null && State.IsReady;
        public float CooldownRemaining => State != null ? State.CooldownRemaining : 0f;
        public bool IsPlayer { get; private set; }
        public int RaceIndex { get; set; } = -1;

        public static event System.Action<CurseController> Changed;
        public static event System.Action<CurseController, CurseType, int> CurseCast; // caster, type, target

        private CurseSettings C => GameBalance.Current.curse;

        private void Awake()
        {
            IsPlayer = GetComponentInChildren<CarUserControl>(true) != null;
            Build();
        }

        private void Build() => State = new CurseCooldownState(C);

        public void ResetForRace()
        {
            Build();
            Changed?.Invoke(this);
        }

        private void Update() => State?.Tick(Time.deltaTime);

        /// <summary>
        /// Cast the next curse at the auto-selected target. Returns false if on cooldown or no
        /// valid target exists. Call on a successful player quiz answer or an AI decision.
        /// </summary>
        public bool CastAtAutoTarget()
        {
            if (!CanCast) return false;
            int target = SelectTarget();
            if (target < 0) return false;
            var type = SelectCurse();
            if (!ApplyCurse(type, target)) return false;
            State.OnCastSucceeded();
            Changed?.Invoke(this);
            CurseCast?.Invoke(this, type, target);
            return true;
        }

        /// <summary>Player attempted a cast but failed the quiz (wrong answer / timeout).</summary>
        public void NotifyFailedAttempt()
        {
            if (State == null) return;
            State.OnCastFailed();
            Changed?.Invoke(this);
        }

        /// <summary>Overtake reward: clear the curse cooldown.</summary>
        public void ResetCooldown()
        {
            if (State == null) return;
            State.ResetCooldown();
            Changed?.Invoke(this);
        }

        private CurseType SelectCurse()
        {
            switch (C.selectionMode)
            {
                case CurseSelectionMode.Random:
                    return CurseCatalog.All[Random.Range(0, CurseCatalog.All.Length)];
                default: // OrderedRotation, and PlayerChoice until a chooser UI exists
                    return State.NextOrdered();
            }
        }

        // NearestAheadThenNearestActive / NearestActive / CurrentLeader over the live race order
        private int SelectTarget()
        {
            int self = RaceIndex >= 0 ? RaceIndex : ResolveOwnIndex();
            if (self < 0) return -1;

            int count = Race.CarCount;
            var elim = Object.FindAnyObjectByType<EliminationManager>();
            double selfScore = Race.ScoreOf(self);
            Vector3 selfPos = PosOf(self);

            int nearestAhead = -1; double aheadGap = double.MaxValue;
            int leader = -1; double leaderScore = double.MinValue;
            int nearestActive = -1; float nearestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (i == self) continue;
                if (elim != null && elim.IsEliminated(i)) continue;

                double sc = Race.ScoreOf(i);
                if (sc > leaderScore) { leaderScore = sc; leader = i; }

                double gap = sc - selfScore;
                if (gap > 0 && gap < aheadGap) { aheadGap = gap; nearestAhead = i; }

                float d = (PosOf(i) - selfPos).sqrMagnitude;
                if (d < nearestDist) { nearestDist = d; nearestActive = i; }
            }

            switch (C.targetingMode)
            {
                case CurseTargetingMode.CurrentLeader: return leader;
                case CurseTargetingMode.NearestActive: return nearestActive;
                default: return nearestAhead >= 0 ? nearestAhead : nearestActive;
            }
        }

        private int ResolveOwnIndex()
        {
            foreach (int i in Race.AllCarIndices())
                if (Race.CarByIndex(i) == gameObject) return i;
            return -1;
        }

        private static Vector3 PosOf(int idx)
        {
            var c = Race.CarByIndex(idx);
            return c != null ? c.transform.position : Vector3.zero;
        }

        private bool ApplyCurse(CurseType type, int targetIndex)
        {
            var target = Race.CarByIndex(targetIndex);
            if (target == null) return false;

            switch (type)
            {
                case CurseType.Rupture:
                    var dur = target.GetComponent<DurabilityController>();
                    if (dur != null) dur.ApplyDamage(C.ruptureDurabilityDamage);
                    return true;

                case CurseType.EngineSeal:
                    var boost = target.GetComponent<BoostController>();
                    if (boost != null) boost.ApplySeal(C.engineSealDurationSeconds);
                    return true;

                case CurseType.SoulSwap:
                    return TrySoulSwap(targetIndex);

                default:
                    return false;
            }
        }

        // Provisional soul swap: validate distance + phase safety, then swap world poses.
        // Track-relative swapping (progress-preserving, lateral-safe) is refined once the MVC
        // vehicle + track data land; the distance/phase safety gate below is the durable part.
        private bool TrySoulSwap(int targetIndex)
        {
            int self = RaceIndex >= 0 ? RaceIndex : ResolveOwnIndex();
            var selfCar = Race.CarByIndex(self);
            var targetCar = Race.CarByIndex(targetIndex);
            if (selfCar == null || targetCar == null) return false;

            // never swap during the final duel / gate phase
            if (GMTKRaceState.Instance != null && GMTKRaceState.Instance.CurrentPhase == RacePhase.FinalDuel)
                return false;

            float dist = Vector3.Distance(selfCar.transform.position, targetCar.transform.position);
            if (dist < C.soulSwapMinimumDistanceMeters || dist > C.soulSwapMaximumDistanceMeters)
                return false;

            Vector3 selfPos = selfCar.transform.position;
            Quaternion selfRot = selfCar.transform.rotation;
            selfCar.transform.SetPositionAndRotation(targetCar.transform.position, targetCar.transform.rotation);
            targetCar.transform.SetPositionAndRotation(selfPos, selfRot);
            return true;
        }
    }
}
