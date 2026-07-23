using System.Collections;
using System.Text;
using UnityEngine;
using SpinMotion;

namespace GMTK
{
    /// <summary>
    /// Automated smoke test for the GMTK race features, driven from the Unity CLI.
    /// It starts a race, pins the player's score to the top so the elimination cascade
    /// runs through every AI, and asserts: race starts, personalities are assigned, all
    /// AI are eliminated, the player wins, and random events spawn hazards.
    ///
    /// Run it by entering play mode and creating the harness, e.g. via `unity command eval`:
    ///     new UnityEngine.GameObject().AddComponent&lt;GMTK.GMTKAutoPlaytest&gt;();
    /// then read the console for a line beginning with "GMTK-PLAYTEST:".
    /// A standalone assembly / NUnit wrapper is intentionally avoided because CarAIControl
    /// (kit assembly) depends on GMTK, which would make a test asmdef circular.
    /// </summary>
    public class GMTKAutoPlaytest : MonoBehaviour
    {
        public int aiCount = 5;
        public int laps = 20;
        public float fastFirstElimination = 3f;
        public float fastInterval = 2f;
        public float startTimeout = 15f;
        public float finishTimeout = 45f;

        public const string Tag = "GMTK-PLAYTEST:";

        private bool raceFinished;
        private RaceFinishType finishType;

        private void Start() => StartCoroutine(Run());

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
            if (events == null)
            {
                Debug.LogError(Tag + " FAIL | no GameEvents found");
                yield break;
            }
            events.RaceFinishedEvent.AddListener(OnFinished);

            // speed up elimination and silence random events for a deterministic run
            var elim = Object.FindAnyObjectByType<EliminationManager>();
            if (elim != null)
            {
                elim.firstEliminationDelaySeconds = fastFirstElimination;
                elim.eliminationIntervalSeconds = fastInterval;
            }
            var eventMgr = Object.FindAnyObjectByType<RandomEventManager>();
            if (eventMgr != null) eventMgr.enableEvents = false;

            // start the race
            RaceData.AiBotsSelected = aiCount;
            RaceData.LapsSelected = laps;
            events.OnClickPlayRaceEvent.Invoke();

            float t0 = Time.time;
            while (!Race.IsRaceInProgress && Time.time - t0 < startTimeout) yield return null;
            Check("race started", Race.IsRaceInProgress);

            // pin the player to the top so the cascade eliminates the AI, not the idle player
            if (Race.Positions != null && Race.Positions.LapScores.Count > 0)
                Race.Positions.LapScores[0] = 999999;

            int cars = Race.CarCount;
            int personalities = Object.FindObjectsByType<AIPersonality>(FindObjectsSortMode.None).Length;
            Check("car count == ai+1", cars == aiCount + 1, "cars=" + cars);
            Check("personalities == ai", personalities == aiCount, "n=" + personalities);

            // wait for the cascade to finish
            float t1 = Time.time;
            while (!raceFinished && Time.time - t1 < finishTimeout) yield return null;
            Check("race finished", raceFinished, raceFinished ? finishType.ToString() : "timeout");
            Check("player won via elimination", raceFinished && finishType == RaceFinishType.Win, finishType.ToString());

            int explosions = Object.FindObjectsByType<CarExplosion>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Check("all AI eliminated", explosions >= aiCount, "explosions=" + explosions);

            // random events smoke test
            if (eventMgr != null)
            {
                int before = Object.FindObjectsByType<TimedDestroy>(FindObjectsSortMode.None).Length;
                for (int i = 0; i < 8; i++) eventMgr.TriggerRandomEvent();
                int after = Object.FindObjectsByType<TimedDestroy>(FindObjectsSortMode.None).Length;
                Check("random events spawn hazards", after > before, "delta=" + (after - before));
            }

            Debug.Log(Tag + " " + (pass ? "PASS" : "FAIL") + " | " + sb);
        }

        private void OnFinished(RaceFinishType type)
        {
            raceFinished = true;
            finishType = type;
        }
    }
}
