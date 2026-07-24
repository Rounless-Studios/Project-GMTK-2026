using UnityEngine;
using SpinMotion;
using Gmtk2026.GameBalance;

namespace GMTK
{
    public enum RacePhase { Boot, PreRace, Countdown, Racing, FinalDuel, Finished }

    /// <summary>
    /// Central authority for the race lifecycle. Drives phase transitions from the kit's
    /// event bus and the elimination manager, and owns the GameBalance per-race snapshot
    /// (BeginRace at start, EndRace at finish). Other systems read <see cref="CurrentPhase"/>
    /// or subscribe to <see cref="PhaseChanged"/> instead of deciding race-level state.
    /// Self-attaches to the GMTK game-mode host.
    /// </summary>
    public class GMTKRaceState : MonoBehaviour
    {
        public static GMTKRaceState Instance { get; private set; }

        public RacePhase CurrentPhase { get; private set; } = RacePhase.Boot;
        public RaceFinishType LastResult { get; private set; }

        public static event System.Action<RacePhase> PhaseChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            var host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<GMTKRaceState>() == null)
                host.AddComponent<GMTKRaceState>();
        }

        private void Awake() => Instance = this;

        private void Start()
        {
            var e = Race.Events;
            if (e == null) return;
            e.OnClickPlayRaceEvent.AddListener(OnPlayRace);
            e.PlayPreRaceCountdownEvent.AddListener(OnCountdown);
            e.RaceStartedEvent.AddListener(OnRaceStarted);
            e.RaceFinishedEvent.AddListener(OnRaceFinished);
            e.RestartRaceEvent.AddListener(OnRestart);
            EliminationManager.FinalDuelStarted += OnFinalDuel;
        }

        private void OnDestroy()
        {
            EliminationManager.FinalDuelStarted -= OnFinalDuel;
            if (Instance == this) Instance = null;
        }

        private void OnPlayRace() => SetPhase(RacePhase.PreRace);
        private void OnCountdown() => SetPhase(RacePhase.Countdown);
        private void OnRestart() => SetPhase(RacePhase.PreRace);
        private void OnFinalDuel() => SetPhase(RacePhase.FinalDuel);

        private void OnRaceStarted()
        {
            // freeze the immutable balance snapshot that every system reads this race
            GameBalance.BeginRace();
            SetPhase(RacePhase.Racing);
        }

        private void OnRaceFinished(RaceFinishType type)
        {
            LastResult = type;
            SetPhase(RacePhase.Finished);
            GameBalance.EndRace();
        }

        private void SetPhase(RacePhase phase)
        {
            if (CurrentPhase == phase) return;
            CurrentPhase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }
}
