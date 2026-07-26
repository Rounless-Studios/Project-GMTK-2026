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
            // bora uses BxB MVC rather than the optional RSK event bus. Mirror its
            // playable flow into the same central phase authority when present.
            RaceFlow.PhaseChanged += OnRacePhaseChanged;
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
            RaceFlow.PhaseChanged -= OnRacePhaseChanged;
            EliminationManager.FinalDuelStarted -= OnFinalDuel;
            GameEvents e = Race.Events;
            if (e != null)
            {
                e.OnClickPlayRaceEvent.RemoveListener(OnPlayRace);
                e.PlayPreRaceCountdownEvent.RemoveListener(OnCountdown);
                e.RaceStartedEvent.RemoveListener(OnRaceStarted);
                e.RaceFinishedEvent.RemoveListener(OnRaceFinished);
                e.RestartRaceEvent.RemoveListener(OnRestart);
            }
            if (Instance == this) Instance = null;
        }

        private void OnPlayRace() => SetPhase(RacePhase.PreRace);
        private void OnCountdown() => SetPhase(RacePhase.Countdown);
        private void OnRestart() => SetPhase(RacePhase.PreRace);
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
                    GameBalance.BeginRace();
                    SetPhase(RacePhase.Racing);
                    break;
                case RaceFlow.Phase.Finished:
                    SetPhase(RacePhase.Finished);
                    break;
            }
        }

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

    /// <summary>
    /// Publishes exactly one terminal result per race and disables the starter kit's independent
    /// lap-completion finish rule. Elimination and the final gate are the only GMTK result sources.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RaceResultAuthority : MonoBehaviour
    {
        public static RaceResultAuthority Instance { get; private set; }
        public bool HasResult { get; private set; }
        public RaceFinishType Result { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            RaceFinish[] legacyFinishers =
                Object.FindObjectsByType<RaceFinish>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            foreach (RaceFinish legacy in legacyFinishers)
            {
                legacy.enabled = false;
                Object.Destroy(legacy);
            }

            GameObject host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<RaceResultAuthority>() == null)
                host.AddComponent<RaceResultAuthority>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            GameEvents events = Race.Events;
            if (events == null) return;
            events.RaceStartedEvent.AddListener(ResetForRace);
            events.RestartRaceEvent.AddListener(ResetForRace);
        }

        private void OnDestroy()
        {
            GameEvents events = Race.Events;
            if (events != null)
            {
                events.RaceStartedEvent.RemoveListener(ResetForRace);
                events.RestartRaceEvent.RemoveListener(ResetForRace);
            }

            if (Instance == this) Instance = null;
        }

        public bool TryFinish(RaceFinishType result)
        {
            if (HasResult) return false;

            HasResult = true;
            Result = result;
            GameEvents events = Race.Events;
            if (events != null) events.RaceFinishedEvent.Invoke(result);
            return true;
        }

        private void ResetForRace()
        {
            HasResult = false;
            Result = default;
        }
    }
}
