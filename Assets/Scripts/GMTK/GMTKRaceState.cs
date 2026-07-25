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
        public bool HasResult { get; private set; }

        public static event System.Action<RacePhase> PhaseChanged;

        private GameEvents subscribedEvents;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            var host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<GMTKRaceState>() == null)
                host.AddComponent<GMTKRaceState>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // GMTKRaceState shares GMTKGameMode with the other runtime managers.
                // Destroy only the duplicate component, never the shared host object.
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            // bora uses BxB MVC rather than the optional RSK event bus. Mirror its
            // playable flow into the same central phase authority when present.
            RaceFlow.PhaseChanged += OnRacePhaseChanged;
            EliminationManager.FinalDuelStarted += OnFinalDuel;

            subscribedEvents = Race.Events;
            if (subscribedEvents == null) return;

            // GameEvents is a ScriptableObject, so listeners can survive an editor play
            // session when domain reload is disabled. Remove this target before adding it.
            Unsubscribe(subscribedEvents);
            subscribedEvents.OnClickPlayRaceEvent.AddListener(OnPlayRace);
            subscribedEvents.PlayPreRaceCountdownEvent.AddListener(OnCountdown);
            subscribedEvents.RaceStartedEvent.AddListener(OnRaceStarted);
            subscribedEvents.RaceFinishedEvent.AddListener(OnRaceFinished);
            subscribedEvents.RestartRaceEvent.AddListener(OnRestart);
            subscribedEvents.RaceTimeoutEvent.AddListener(OnRaceTimeout);
        }

        private void OnDestroy()
        {
            if (subscribedEvents != null) Unsubscribe(subscribedEvents);
            RaceFlow.PhaseChanged -= OnRacePhaseChanged;
            EliminationManager.FinalDuelStarted -= OnFinalDuel;
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// The only GMTK result entry point. It commits the result before notifying the kit
        /// event bus, so duplicate reports from elimination, gate or timeout cannot overwrite
        /// the first winner. Existing result UI remains compatible through RaceFinishedEvent.
        /// </summary>
        public static bool ReportResult(RaceFinishType type)
        {
            var state = Instance;
            if (state == null)
            {
                var host = GMTKGameMode.GetOrCreate();
                state = host.GetComponent<GMTKRaceState>();
                if (state == null) state = host.AddComponent<GMTKRaceState>();
            }

            return state != null && state.CommitResult(type, true);
        }

        private void OnPlayRace()
        {
            ResetResult();
            SetPhase(RacePhase.PreRace);
        }

        private void OnCountdown() => SetPhase(RacePhase.Countdown);

        private void OnRestart()
        {
            ResetResult();
            GameBalance.EndRace();
            SetPhase(RacePhase.PreRace);
        }

        private void OnFinalDuel() => SetPhase(RacePhase.FinalDuel);

        private void OnRacePhaseChanged(RaceFlow.Phase phase)
        {
            switch (phase)
            {
                case RaceFlow.Phase.StartScreen:
                    SetPhase(RacePhase.Boot);
                    break;
                case RaceFlow.Phase.Prologue:
                    SetPhase(RacePhase.PreRace);
                    break;
                case RaceFlow.Phase.Countdown:
                    SetPhase(RacePhase.Countdown);
                    break;
                case RaceFlow.Phase.Racing:
                    BeginRacing();
                    break;
                case RaceFlow.Phase.Finished:
                    SetPhase(RacePhase.Finished);
                    break;
            }
        }

        private void OnRaceStarted()
        {
            BeginRacing();
        }

        // Compatibility path for a non-GMTK producer on the shared kit event bus. GMTK
        // gameplay systems call ReportResult instead of invoking RaceFinishedEvent directly.
        private void OnRaceFinished(RaceFinishType type) => CommitResult(type, false);
        private void OnRaceTimeout() => CommitResult(RaceFinishType.Timeout, true);

        private bool CommitResult(RaceFinishType type, bool publishToEventBus)
        {
            if (HasResult) return false;

            HasResult = true;
            LastResult = type;
            SetPhase(RacePhase.Finished);
            GameBalance.EndRace();

            if (publishToEventBus)
            {
                var events = subscribedEvents != null ? subscribedEvents : Race.Events;
                if (events != null) events.RaceFinishedEvent.Invoke(type);
            }

            return true;
        }

        private void ResetResult()
        {
            HasResult = false;
            LastResult = default;
        }

        private void BeginRacing()
        {
            ResetResult();

            // RaceFlow.PhaseChanged and the kit's RaceStartedEvent both describe the same
            // transition in GMTK_Race. Whichever arrives first freezes the balance snapshot;
            // the second observes Racing and must not replace that immutable snapshot.
            if (CurrentPhase != RacePhase.Racing) GameBalance.BeginRace();
            SetPhase(RacePhase.Racing);
        }

        private void Unsubscribe(GameEvents events)
        {
            events.OnClickPlayRaceEvent.RemoveListener(OnPlayRace);
            events.PlayPreRaceCountdownEvent.RemoveListener(OnCountdown);
            events.RaceStartedEvent.RemoveListener(OnRaceStarted);
            events.RaceFinishedEvent.RemoveListener(OnRaceFinished);
            events.RestartRaceEvent.RemoveListener(OnRestart);
            events.RaceTimeoutEvent.RemoveListener(OnRaceTimeout);
        }

        private void SetPhase(RacePhase phase)
        {
            if (CurrentPhase == phase) return;
            CurrentPhase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }
}
