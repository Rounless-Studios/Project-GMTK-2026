using System.Collections;
using UnityEngine;
using SpinMotion;
using Gmtk2026.GameBalance;
using GMTK.Rccp;

namespace GMTK
{
    /// <summary>
    /// Per-car curse actuation (checklist stages 3.7-3.10). Owns the shared cooldown/rotation
    /// (<see cref="CurseCooldownState"/>), auto-selects a target from the live race order, and
    /// activates the chosen curse challenge on the target. The target avoids the effect by
    /// succeeding at a quiz; a failed human quiz or AI ability roll applies Rupture, Engine Seal
    /// or Soul Swap. The caster spends its cooldown when the challenge begins.
    /// </summary>
    [DisallowMultipleComponent]
    public class CurseController : MonoBehaviour
    {
        public CurseCooldownState State { get; private set; }
        public bool CanCast => State != null && State.IsReady;
        public float CooldownRemaining => State != null ? State.CooldownRemaining : 0f;
        public float CooldownProgress => State != null ? State.CooldownProgress : 1f;
        public bool IsPlayer { get; private set; }
        public int RaceIndex { get; set; } = -1;

        public static event System.Action<CurseController> Changed;
        public static event System.Action<CurseController, CurseType, int> CurseCast; // caster, type, target

        private CurseSettings C => GameBalance.Current.curse;
        private GmtkVehicleAdapter vehicleAdapter;

        private void Awake()
        {
            vehicleAdapter = GetComponent<GmtkVehicleAdapter>();
            IsPlayer = vehicleAdapter != null
                ? vehicleAdapter.IsPlayer
                : GetComponentInChildren<CarUserControl>(true) != null;
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
        /// Activate the next curse on an auto-selected target. Returns false if the skill is on
        /// cooldown, no valid target exists, or the target cannot begin a curse challenge.
        /// </summary>
        public bool CastAtAutoTarget()
        {
            if (!CanCast) return false;
            var type = SelectCurse();
            int target = SelectTarget(type);
            if (target < 0 || CurseManager.Instance == null) return false;
            if (!CurseManager.Instance.TryActivateCurse(this, type, target)) return false;
            State.OnCastSucceeded();
            Changed?.Invoke(this);
            CurseCast?.Invoke(this, type, target);
            return true;
        }

        /// <summary>Apply the stored curse after the target fails its quiz.</summary>
        public bool ApplyPenalty(CurseType type, int targetIndex) => ApplyCurse(type, targetIndex);

        /// <summary>Overtake reward (Reset mode): clear the curse cooldown.</summary>
        public void ResetCooldown()
        {
            if (State == null) return;
            State.ResetCooldown();
            Changed?.Invoke(this);
        }

        /// <summary>Overtake reward (Reduce mode): shorten the curse cooldown.</summary>
        public void ReduceCooldown(float seconds)
        {
            if (State == null) return;
            State.ReduceCooldown(seconds);
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

        // Every targeting mode is restricted to valid racers strictly ahead in the live race order.
        private int SelectTarget(CurseType type)
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
                if (!CanApplyPenaltyTo(type, i)) continue;

                double sc = Race.ScoreOf(i);
                if (!CurseCooldownState.IsValidTargetByScore(selfScore, sc)) continue;

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
                    if (dur == null) return false;
                    dur.ApplyDamage(C.ruptureDurabilityDamage);
                    GmtkVehicleAdapter targetAdapter = target.GetComponent<GmtkVehicleAdapter>();
                    if (targetAdapter != null)
                    {
                        Vector3 away = target.transform.position - transform.position;
                        away.y = 0f;
                        if (away.sqrMagnitude < 0.01f) away = target.transform.right;
                        targetAdapter.ApplyWorldImpulse(
                            away.normalized * C.ruptureKnockbackForce);
                    }
                    return true;

                case CurseType.EngineSeal:
                    var boost = target.GetComponent<BoostController>();
                    if (boost == null) return false;
                    boost.ApplySeal(C.engineSealDurationSeconds);
                    return true;

                case CurseType.SoulSwap:
                    return TrySoulSwap(targetIndex);

                default:
                    return false;
            }
        }

        private bool CanApplyPenaltyTo(CurseType type, int targetIndex)
        {
            var target = Race.CarByIndex(targetIndex);
            if (target == null) return false;

            switch (type)
            {
                case CurseType.Rupture:
                    return target.GetComponent<DurabilityController>() != null;
                case CurseType.EngineSeal:
                    return target.GetComponent<BoostController>() != null;
                case CurseType.SoulSwap:
                    return CanSoulSwap(targetIndex);
                default:
                    return false;
            }
        }

        // Distance is checked when the curse target is selected. Do not check it again after
        // the quiz because both cars keep moving while the question is on screen.
        private bool TrySoulSwap(int targetIndex)
        {
            int self = RaceIndex >= 0 ? RaceIndex : ResolveOwnIndex();
            var selfCar = Race.CarByIndex(self);
            var targetCar = Race.CarByIndex(targetIndex);
            if (selfCar == null || targetCar == null) return false;

            if (GMTKRaceState.Instance != null &&
                GMTKRaceState.Instance.CurrentPhase == RacePhase.FinalDuel)
                return false;

            Rigidbody selfBody = FindVehicleBody(selfCar);
            Rigidbody targetBody = FindVehicleBody(targetCar);

            Vector3 selfPos = selfBody != null ? selfBody.position : selfCar.transform.position;
            Quaternion selfRot = selfBody != null ? selfBody.rotation : selfCar.transform.rotation;
            Vector3 selfVelocity = selfBody != null ? selfBody.linearVelocity : Vector3.zero;
            Vector3 selfAngularVelocity = selfBody != null ? selfBody.angularVelocity : Vector3.zero;

            Vector3 targetPos = targetBody != null ? targetBody.position : targetCar.transform.position;
            Quaternion targetRot = targetBody != null ? targetBody.rotation : targetCar.transform.rotation;
            Vector3 targetVelocity = targetBody != null ? targetBody.linearVelocity : Vector3.zero;
            Vector3 targetAngularVelocity = targetBody != null ? targetBody.angularVelocity : Vector3.zero;

            // Resolve both destinations onto the authored driving path. This preserves the swap's
            // race-progress meaning while preventing a raw pose from landing inside a barrier,
            // off-track, or facing backward after the quiz delay.
            GmtkRccpWaypointPath path = GmtkRccpWaypointPath.GetOrCreate();
            if (path != null)
            {
                float height = GameBalance.Current.vehicleRecovery.respawnHeightAboveWaypoint;
                if (path.TryGetRespawnPose(
                        targetPos,
                        height,
                        out _,
                        out Vector3 safeTargetPos,
                        out Quaternion safeTargetRot))
                {
                    targetPos = safeTargetPos;
                    targetRot = safeTargetRot;
                }
                if (path.TryGetRespawnPose(
                        selfPos,
                        height,
                        out _,
                        out Vector3 safeSelfPos,
                        out Quaternion safeSelfRot))
                {
                    selfPos = safeSelfPos;
                    selfRot = safeSelfRot;
                }
            }

            TeleportVehicle(
                selfCar,
                selfBody,
                targetPos,
                targetRot,
                targetVelocity,
                targetAngularVelocity);
            TeleportVehicle(
                targetCar,
                targetBody,
                selfPos,
                selfRot,
                selfVelocity,
                selfAngularVelocity);
            StartCoroutine(IgnoreMutualCollision(
                selfCar,
                targetCar,
                C.soulSwapCollisionIgnoreSeconds));
            Physics.SyncTransforms();
            return true;
        }

        private static IEnumerator IgnoreMutualCollision(
            GameObject first,
            GameObject second,
            float duration)
        {
            if (duration <= 0f) yield break;

            Collider[] firstColliders = first.GetComponentsInChildren<Collider>(true);
            Collider[] secondColliders = second.GetComponentsInChildren<Collider>(true);
            foreach (Collider a in firstColliders)
                foreach (Collider b in secondColliders)
                    if (a != null && b != null) Physics.IgnoreCollision(a, b, true);

            yield return new WaitForSeconds(duration);

            foreach (Collider a in firstColliders)
                foreach (Collider b in secondColliders)
                    if (a != null && b != null) Physics.IgnoreCollision(a, b, false);
        }

        private static Rigidbody FindVehicleBody(GameObject car)
        {
            Rigidbody body = car.GetComponent<Rigidbody>();
            return body != null ? body : car.GetComponentInChildren<Rigidbody>(true);
        }

        private static void TeleportVehicle(
            GameObject car,
            Rigidbody body,
            Vector3 position,
            Quaternion rotation,
            Vector3 linearVelocity,
            Vector3 angularVelocity)
        {
            if (body == null)
            {
                car.transform.SetPositionAndRotation(position, rotation);
                return;
            }

            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = linearVelocity;
            body.angularVelocity = angularVelocity;
            body.WakeUp();
        }

        private bool CanSoulSwap(int targetIndex)
        {
            int self = RaceIndex >= 0 ? RaceIndex : ResolveOwnIndex();
            var selfCar = Race.CarByIndex(self);
            var targetCar = Race.CarByIndex(targetIndex);
            if (selfCar == null || targetCar == null) return false;

            if (GMTKRaceState.Instance != null &&
                GMTKRaceState.Instance.CurrentPhase == RacePhase.FinalDuel)
                return false;

            float distance = Vector3.Distance(
                selfCar.transform.position,
                targetCar.transform.position);
            return distance >= C.soulSwapMinimumDistanceMeters &&
                   distance <= C.soulSwapMaximumDistanceMeters;
        }
    }
}
