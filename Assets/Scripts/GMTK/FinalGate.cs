using System.Collections;
using System.Collections.Generic;
using Gmtk2026.GameBalance;
using UnityEngine;
using GMTK.Kit;

namespace GMTK
{
    /// <summary>
    /// Final-gate win rule (checklist stage 6). Once the final duel begins, the first of the
    /// surviving racers to cross the gate wins, the gate slams shut behind them and the racers
    /// locked outside are executed. The player winning is a Win; an AI winning is a Lose.
    /// This component owns the rule and the whole gate sequence timing (all read from
    /// <see cref="PresentationSettings"/>); <see cref="FinalGateDoors"/> only listens to the
    /// events below and animates. Crossings arrive via <see cref="ReportGateCrossing"/>
    /// (from the gate trigger or a test).
    /// Self-attaches to the GMTK game-mode host.
    /// </summary>
    public class FinalGate : MonoBehaviour
    {
        public static FinalGate Instance { get; private set; }

        public bool IsOpen { get; private set; }      // duel active, awaiting a crossing
        public int WinnerIndex { get; private set; } = -1;
        public float TimeRemaining { get; private set; }

        // ---- events for the gate presentation ----
        public static event System.Action GateOpened;          // duel started, gate awaits a crossing
        public static event System.Action<int> GateSlamming;   // (winner raceIndex) close behind them
        public static event System.Action GateReset;           // restart: remove the gate again

        private readonly List<int> duelCars = new();
        private bool resolved;
        private Coroutine sequence;

        private PresentationSettings Presentation => GameBalance.Current.presentation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            var host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<FinalGate>() == null) host.AddComponent<FinalGate>();
        }

        private void Awake() => Instance = this;

        private void Start()
        {
            EliminationManager.FinalDuelStarted += OnFinalDuel;
            var e = Race.Events;
            if (e != null) e.RestartRaceEvent.AddListener(ResetGate);
        }

        private void OnDestroy()
        {
            EliminationManager.FinalDuelStarted -= OnFinalDuel;
            GameEvents e = Race.Events;
            if (e != null) e.RestartRaceEvent.RemoveListener(ResetGate);
            if (Instance == this) Instance = null;
        }

        private void OnFinalDuel()
        {
            duelCars.Clear();
            var elim = Object.FindAnyObjectByType<EliminationManager>();
            int count = Race.CarCount;
            for (int i = 0; i < count; i++)
                if (elim == null || !elim.IsEliminated(i)) duelCars.Add(i);

            resolved = false;
            WinnerIndex = -1;
            IsOpen = true;
            TimeRemaining = Presentation.gateOpenDurationSeconds;
            GateOpened?.Invoke();
        }

        private void Update()
        {
            if (!IsOpen || resolved) return;
            TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);
            if (TimeRemaining <= 0f && duelCars.Count > 0)
                ResolveWinner(LeadingDuelCar());
        }

        /// <summary>Duel car furthest along the track; race scores grow with progress.</summary>
        private int LeadingDuelCar()
        {
            int leader = duelCars[0];
            double best = double.MinValue;
            foreach (int idx in duelCars)
            {
                double score = Race.ScoreOf(idx);
                if (score > best) { best = score; leader = idx; }
            }
            return leader;
        }

        /// <summary>Report that a surviving car crossed the gate (gate trigger or test).</summary>
        public void ReportGateCrossing(int raceIndex)
        {
            if (!IsOpen || resolved) return;
            if (!duelCars.Contains(raceIndex)) return;
            ResolveWinner(raceIndex);
        }

        private void ResolveWinner(int winner)
        {
            resolved = true;
            IsOpen = false;
            WinnerIndex = winner;
            TimeRemaining = 0f;

            GateSlamming?.Invoke(winner);
            sequence = StartCoroutine(CloseGateThenExecute(winner));
        }

        /// <summary>
        /// GDD climax: the gate slams shut behind the winner, the racers locked outside are
        /// executed against it, and only then is the race result reported. Every wait comes
        /// from the same settings fields the doors animate with.
        /// </summary>
        private IEnumerator CloseGateThenExecute(int winner)
        {
            var p = Presentation;
            float untilShut = p.gateCloseDelaySeconds + p.gateCloseDurationSeconds;
            if (untilShut > 0f) yield return new WaitForSeconds(untilShut);
            if (p.gateExecutionDelaySeconds > 0f) yield return new WaitForSeconds(p.gateExecutionDelaySeconds);

            // the racers left outside are executed against the shut gate
            foreach (var idx in duelCars)
            {
                if (idx == winner) continue;
                var car = Race.CarByIndex(idx);
                if (car == null) continue;
                var ex = car.GetComponent<CarExplosion>();
                if (ex == null) ex = car.AddComponent<CarExplosion>();
                ex.wreckLingerSeconds = p.wreckLingerSeconds;
                ex.Explode();
            }

            // When the player is locked outside, keep the chase view alive long enough for
            // their own breakable wreck and explosion particle to be visible before results.
            if (winner != 0 && p.wreckLingerSeconds > 0f)
                yield return new WaitForSeconds(p.wreckLingerSeconds);

            RaceResultAuthority authority = RaceResultAuthority.Instance;
            if (authority != null)
                authority.TryFinish(winner == 0 ? RaceFinishType.Win : RaceFinishType.Lose);

            sequence = null;
        }

        private void ResetGate()
        {
            if (sequence != null) { StopCoroutine(sequence); sequence = null; }
            IsOpen = false;
            resolved = false;
            WinnerIndex = -1;
            TimeRemaining = 0f;
            duelCars.Clear();
            GateReset?.Invoke();
        }
    }
}
