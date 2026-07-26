using System.Collections;
using System.Text;
using UnityEngine;
using GMTK.Kit;
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

        private GameBalanceSettings previousPreset;
        private int previousAiBots;
        private int previousLaps;
        private bool editorStateCaptured;

        private void Start() => StartCoroutine(Run());

        /// <summary>
        /// Play-mode domain reloads are off in this project, so the balance preset and RaceData are
        /// statics that outlive the run. Restoring at the end of the coroutine is not enough — leaving
        /// play mode, a recompile or any abort would skip it and leave FastTest active, which executes
        /// a car every three seconds in the next ordinary playtest. Restore on teardown as well.
        /// </summary>
        private void OnDestroy() => RestoreEditorState();

        private void CaptureEditorState()
        {
            if (editorStateCaptured) return;

            previousPreset = GameBalance.Active;
            previousAiBots = RaceData.AiBotsSelected;
            previousLaps = RaceData.LapsSelected;
            editorStateCaptured = true;
        }

        private void RestoreEditorState()
        {
            if (!editorStateCaptured) return;
            editorStateCaptured = false;

            GameBalance.EndRace();
            if (previousPreset != null) GameBalance.SetActive(previousPreset);
            else GameBalance.Load(GameBalance.DefaultPresetName);
            RaceData.AiBotsSelected = previousAiBots;
            RaceData.LapsSelected = previousLaps;
        }

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

            // deterministic, fast run: FastTest preset (short elimination interval) + no random events.
            // See OnDestroy for why the previous state is captured before anything is overwritten.
            CaptureEditorState();
            var preset = GameBalance.Load("FastTest");
            Check("FastTest preset loaded", preset != null);
            var eventMgr = Object.FindAnyObjectByType<RandomEventManager>();
            if (eventMgr != null) eventMgr.enableEvents = false;

            events.RaceFinishedEvent.AddListener(OnFinished);
            EliminationManager.FinalDuelStarted += OnDuel;

            // same static-leak problem as the preset: the menu only writes these when someone opens it
            RaceData.AiBotsSelected = aiCount;
            RaceData.LapsSelected = laps;

            // RaceFlow and the game-mode host subscribe from Start/AfterSceneLoad callbacks, so a click
            // in the first frame reaches the spawner — already listening — but not the race flow: cars
            // appear on the grid and the countdown never runs. Wait until the flow is up.
            float ready = Time.realtimeSinceStartup + 5f;
            while (GMTKRaceState.Instance == null && Time.realtimeSinceStartup < ready) yield return null;
            yield return null;
            Check("race flow ready", GMTKRaceState.Instance != null);

            events.OnClickPlayRaceEvent.Invoke();

            // the flow runs the prologue and countdown on unscaled time while the race is frozen
            float t0 = Time.realtimeSinceStartup;
            while (!Race.IsRaceInProgress && Time.realtimeSinceStartup - t0 < startTimeout)
                yield return null;
            Check("race started", Race.IsRaceInProgress);
            Check("phase == Racing", GMTKRaceState.Instance != null && GMTKRaceState.Instance.CurrentPhase == RacePhase.Racing,
                GMTKRaceState.Instance != null ? GMTKRaceState.Instance.CurrentPhase.ToString() : "no state");

            // pin the player to the top so it survives the cascade into the final duel. The score now
            // comes from waypoint progress, so raising the kit's lap count would be overwritten.
            var progress = Object.FindAnyObjectByType<GmtkRaceProgress>();
            if (progress != null && progress.SuppliesStandings) progress.PinToLead(0);
            else if (Race.Positions != null && Race.Positions.LapScores.Count > 0)
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

            // final gate: player crosses first -> player wins, remaining AI executed
            var gate = FinalGate.Instance;
            Check("final gate present", gate != null);
            if (gate != null && finalDuelStarted && !raceFinished)
            {
                gate.ReportGateCrossing(0);
                float t3 = Time.time;
                while (!raceFinished && Time.time - t3 < 5f) yield return null;
                Check("gate crossing finishes race", raceFinished, raceFinished ? finishType.ToString() : "timeout");
                Check("player wins via gate", finishType == RaceFinishType.Win, finishType.ToString());
                Check("gate winner == player", gate.WinnerIndex == 0, "winner=" + gate.WinnerIndex);
            }

            // restart revive rule: after the duel, several cars are inactive (elimination +
            // the gate-executed loser). ReviveAllCars() must bring every car back so the grid
            // is full for the next race. We call it directly instead of firing the kit's
            // RestartRaceEvent, whose kit-side handler is fragile under headless CLI play; the
            // full restart flow is validated interactively.
            if (raceFinished && elim != null)
            {
                int deadBefore = 0;
                foreach (int i in Race.AllCarIndices())
                {
                    var c = Race.CarByIndex(i);
                    if (c != null && !c.activeSelf) deadBefore++;
                }
                elim.ReviveAllCars();
                yield return null;
                int active2 = 0;
                foreach (int i in Race.AllCarIndices())
                {
                    var c = Race.CarByIndex(i);
                    if (c != null && c.activeSelf) active2++;
                }
                Check("cars were eliminated before restart", deadBefore > 0, "dead=" + deadBefore);
                Check("restart revives all cars", active2 == aiCount + 1, "active=" + active2);
            }

            EliminationManager.FinalDuelStarted -= OnDuel;
            events.RaceFinishedEvent.RemoveListener(OnFinished);

            // hand the editor back the balance it had; OnDestroy repeats this if a run never gets here
            RestoreEditorState();

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
