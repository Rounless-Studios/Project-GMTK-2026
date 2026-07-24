using System.Collections;
using System.Text;
using UnityEngine;
using SpinMotion;
using Gmtk2026.GameBalance;

namespace GMTK
{
    /// <summary>
    /// Automated smoke test for the GMTK race features, driven from the Unity CLI.
    /// Loads the FastTest balance preset, starts a race, pins the player's score to the top
    /// so the elimination cascade runs through the AI, and asserts: race starts, personalities
    /// assigned, cars are eliminated down to finalDuelRacerCount, the final-duel phase begins
    /// (no auto-win at one car), the player survives to the duel, and random events spawn.
    ///
    /// Run it by entering play mode and creating the harness via `unity command eval`:
    ///     new UnityEngine.GameObject().AddComponent&lt;GMTK.GMTKAutoPlaytest&gt;();
    /// then read GMTK.GMTKAutoPlaytest.LastResult (console-noise proof).
    /// A standalone NUnit asmdef is avoided because CarAIControl (kit) depends on GMTK,
    /// which would make a test assembly reference circular.
    /// </summary>
    public class GMTKAutoPlaytest : MonoBehaviour
    {
        public int aiCount = 5;
        public int laps = 20;
        public float startTimeout = 15f;
        public float duelTimeout = 60f;

        public const string Tag = "GMTK-PLAYTEST:";
        public static string LastResult = "(not run yet)";

        private bool raceFinished;
        private RaceFinishType finishType;
        private bool finalDuelStarted;

        private void Start() => StartCoroutine(Run());

        private void OnDuel() => finalDuelStarted = true;

        private IEnumerator Run()
        {
            var sb = new StringBuilder();
            bool pass = true;
            void Check(string name, bool ok, string detail = "")
            {
                if (!ok) pass = false;
                sb.Append(ok ? "[OK] " : "[FAIL] ").Append(name);
                if (detail.Length > 0) sb.Append(" (").Append(detail).Append(')');
                sb.Append("; ");
            }

            var events = Race.Events;
            if (events == null) { Debug.LogError(Tag + " FAIL | no GameEvents"); LastResult = "FAIL | no GameEvents"; yield break; }

            // deterministic, fast run: FastTest preset (short elimination interval) + no random events
            var preset = GameBalance.Load("FastTest");
            Check("FastTest preset loaded", preset != null);
            var eventMgr = Object.FindAnyObjectByType<RandomEventManager>();
            if (eventMgr != null) eventMgr.enableEvents = false;

            events.RaceFinishedEvent.AddListener(OnFinished);
            EliminationManager.FinalDuelStarted += OnDuel;

            RaceData.AiBotsSelected = aiCount;
            RaceData.LapsSelected = laps;
            events.OnClickPlayRaceEvent.Invoke();

            float t0 = Time.time;
            while (!Race.IsRaceInProgress && Time.time - t0 < startTimeout) yield return null;
            Check("race started", Race.IsRaceInProgress);
            Check("phase == Racing", GMTKRaceState.Instance != null && GMTKRaceState.Instance.CurrentPhase == RacePhase.Racing,
                GMTKRaceState.Instance != null ? GMTKRaceState.Instance.CurrentPhase.ToString() : "no state");

            // pin the player to the top so it survives the cascade into the final duel
            if (Race.Positions != null && Race.Positions.LapScores.Count > 0)
                Race.Positions.LapScores[0] = 999999;

            var elim = Object.FindAnyObjectByType<EliminationManager>();
            int cars = Race.CarCount;
            int personalities = Object.FindObjectsByType<AIPersonality>(FindObjectsSortMode.None).Length;
            Check("elimination manager present", elim != null);
            Check("car count == ai+1", cars == aiCount + 1, "cars=" + cars);
            Check("personalities == ai", personalities == aiCount, "n=" + personalities);

            // wait until the final duel begins (elimination stopped) or the race is lost
            float t1 = Time.time;
            while (!finalDuelStarted && !raceFinished && Time.time - t1 < duelTimeout) yield return null;

            int finalDuelCount = GameBalance.Current.race.finalDuelRacerCount;
            Check("final duel started (not auto-win)", finalDuelStarted, raceFinished ? "raceFinished=" + finishType : "timeout");
            Check("phase == FinalDuel", GMTKRaceState.Instance != null && GMTKRaceState.Instance.CurrentPhase == RacePhase.FinalDuel,
                GMTKRaceState.Instance != null ? GMTKRaceState.Instance.CurrentPhase.ToString() : "no state");
            Check("no premature race finish", !raceFinished, raceFinished ? finishType.ToString() : "");
            if (elim != null)
            {
                Check("survivors == finalDuelRacerCount", elim.ActiveCarCount == finalDuelCount, "active=" + elim.ActiveCarCount);
                Check("player survived to duel", !elim.IsEliminated(0));
            }

            int explosions = Object.FindObjectsByType<CarExplosion>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int expectedElims = (aiCount + 1) - finalDuelCount;
            Check("eliminated down to duel", explosions >= expectedElims, "explosions=" + explosions + " expected>=" + expectedElims);

            if (eventMgr != null)
            {
                int before = Object.FindObjectsByType<TimedDestroy>(FindObjectsSortMode.None).Length;
                for (int i = 0; i < 8; i++) eventMgr.TriggerRandomEvent();
                int after = Object.FindObjectsByType<TimedDestroy>(FindObjectsSortMode.None).Length;
                Check("random events spawn hazards", after > before, "delta=" + (after - before));
            }

            EliminationManager.FinalDuelStarted -= OnDuel;
            LastResult = (pass ? "PASS" : "FAIL") + " | " + sb;
            Debug.Log(Tag + " " + LastResult);
        }

        private void OnFinished(RaceFinishType type)
        {
            raceFinished = true;
            finishType = type;
        }
    }
}
