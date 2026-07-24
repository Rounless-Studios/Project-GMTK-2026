using System.Collections.Generic;
using UnityEngine;
using SpinMotion;
using Gmtk2026.GameBalance;

namespace GMTK
{
    public enum EliminationWarningLevel { None, Warning, Intense, Execution }

    /// <summary>
    /// "Hell countdown" rule: every EliminationSettings.intervalSeconds the current
    /// last-place car is executed. Warnings escalate at the warning / intense / execution
    /// thresholds. Elimination stops once RaceSettings.finalDuelRacerCount cars remain
    /// (the final gate decides the winner — there is no auto-win at one car). If the player
    /// is executed, the race is lost immediately.
    /// All timing/counts come from GameBalance; explosion force stays a presentation tuning.
    /// Self-attaches to the GMTK game-mode host.
    /// </summary>
    public class EliminationManager : MonoBehaviour
    {
        [Header("Explosion (presentation tuning)")]
        public float explosionForce = 1600f;
        public float upwardForce = 9f;
        public float explosionRadius = 6f;

        // ---- state exposed for HUD / execution camera ----
        public int CurrentLastPlaceIndex { get; private set; } = -1;
        public float SecondsToElimination { get; private set; }
        public EliminationWarningLevel Level { get; private set; }
        public bool InFinalDuel { get; private set; }
        public int ActiveCarCount => Mathf.Max(0, Race.CarCount - eliminated.Count);
        public bool IsEliminated(int raceIndex) => eliminated.Contains(raceIndex);

        // ---- events for HUD / camera / other systems ----
        public static event System.Action<int, EliminationWarningLevel> WarningChanged; // (lastPlaceIndex, level)
        public static event System.Action<int> CarEliminated;                            // (raceIndex)
        public static event System.Action FinalDuelStarted;

        private readonly HashSet<int> eliminated = new();
        private float nextEliminationTime;
        private bool armed;
        private bool finished;
        private EliminationWarningLevel lastFiredLevel = EliminationWarningLevel.None;

        private EliminationSettings E => GameBalance.Current.elimination;
        private int FinalDuelCount => GameBalance.Current.race.finalDuelRacerCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            var host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<EliminationManager>() == null)
                host.AddComponent<EliminationManager>();
        }

        private void Start()
        {
            var events = Race.Events;
            if (events == null) return;
            events.RaceStartedEvent.AddListener(OnRaceStarted);
            events.RestartRaceEvent.AddListener(OnRestartRace);
            events.RaceFinishedEvent.AddListener(OnRaceFinished);
        }

        private void OnRaceStarted()
        {
            eliminated.Clear();
            finished = false;
            InFinalDuel = false;
            Level = EliminationWarningLevel.None;
            lastFiredLevel = EliminationWarningLevel.None;
            CurrentLastPlaceIndex = -1;
            armed = true;
            // first elimination also fires after a full interval (GDD: 30s consistently)
            nextEliminationTime = Time.time + E.intervalSeconds;
        }

        private void OnRestartRace()
        {
            ReviveAllCars();

            armed = false;
            finished = false;
            InFinalDuel = false;
            eliminated.Clear();
            Level = EliminationWarningLevel.None;
            lastFiredLevel = EliminationWarningLevel.None;
        }

        /// <summary>
        /// Re-activates EVERY car and clears its explosion so the next race starts with a full
        /// grid. Iterates all indices (not just our own <c>eliminated</c> set) because the final
        /// gate executes the duel loser without routing through this manager. Public so the
        /// automated playtest can verify the revive rule without firing the kit's restart flow.
        /// </summary>
        public void ReviveAllCars()
        {
            foreach (var idx in Race.AllCarIndices())
            {
                var car = Race.CarByIndex(idx);
                if (car == null) continue;
                car.SetActive(true);
                foreach (var ai in car.GetComponentsInChildren<CarAIControl>(true)) ai.enabled = true;
                foreach (var user in car.GetComponentsInChildren<CarUserControl>(true)) user.enabled = true;
                var ex = car.GetComponent<CarExplosion>();
                if (ex != null) Destroy(ex);
            }
        }

        private void OnRaceFinished(RaceFinishType type) => armed = false;

        private void Update()
        {
            if (!armed || finished || !Race.IsRaceInProgress) return;

            UpdateWarnings();

            if (Time.time >= nextEliminationTime)
            {
                nextEliminationTime = Time.time + E.intervalSeconds;
                EliminateLastPlace();
            }
        }

        private void UpdateWarnings()
        {
            SecondsToElimination = Mathf.Max(0f, nextEliminationTime - Time.time);
            CurrentLastPlaceIndex = FindLastPlace();

            var level = EliminationWarningLevel.None;
            if (SecondsToElimination <= E.executionCameraLeadSeconds) level = EliminationWarningLevel.Execution;
            else if (SecondsToElimination <= E.intenseWarningSeconds) level = EliminationWarningLevel.Intense;
            else if (SecondsToElimination <= E.warningSeconds) level = EliminationWarningLevel.Warning;
            Level = level;

            if (level != lastFiredLevel)
            {
                lastFiredLevel = level;
                WarningChanged?.Invoke(CurrentLastPlaceIndex, level);
            }
        }

        // lowest race score among active cars (re-evaluated live, so a late overtake counts)
        private int FindLastPlace()
        {
            int carCount = Race.CarCount;
            int lastIndex = -1;
            double lowest = double.MaxValue;
            for (int i = 0; i < carCount; i++)
            {
                if (eliminated.Contains(i)) continue;
                double score = Race.ScoreOf(i);
                if (score < lowest) { lowest = score; lastIndex = i; }
            }
            return lastIndex;
        }

        private void EliminateLastPlace()
        {
            if (Race.CarCount <= 1) { armed = false; return; }
            if (ActiveCarCount <= FinalDuelCount) { EnterFinalDuel(); return; }

            int last = FindLastPlace();
            if (last < 0) return;

            eliminated.Add(last);
            CarEliminated?.Invoke(last);
            ExplodeCar(last);

            // reset the warning cycle for the next countdown
            Level = EliminationWarningLevel.None;
            lastFiredLevel = EliminationWarningLevel.None;

            if (last == 0)
            {
                // the player was executed → immediate loss (no auto-win path)
                FinishRace(RaceFinishType.Lose);
                return;
            }

            if (ActiveCarCount <= FinalDuelCount)
                EnterFinalDuel();
        }

        // Elimination stops here; the final gate (stage 6) decides the winner.
        private void EnterFinalDuel()
        {
            if (InFinalDuel) return;
            InFinalDuel = true;
            armed = false;
            FinalDuelStarted?.Invoke();
        }

        private void ExplodeCar(int raceIndex)
        {
            var car = Race.CarByIndex(raceIndex);
            if (car == null) return;
            var explosion = car.GetComponent<CarExplosion>();
            if (explosion == null) explosion = car.AddComponent<CarExplosion>();
            explosion.explosionForce = explosionForce;
            explosion.upwardForce = upwardForce;
            explosion.explosionRadius = explosionRadius;
            explosion.wreckLingerSeconds = GameBalance.Current.presentation.wreckLingerSeconds;
            explosion.Explode();
        }

        private void FinishRace(RaceFinishType type)
        {
            if (finished) return;
            finished = true;
            armed = false;
            var events = Race.Events;
            if (events != null) events.RaceFinishedEvent.Invoke(type);
        }
    }
}
