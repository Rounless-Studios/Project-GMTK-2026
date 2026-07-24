using System.Collections.Generic;
using UnityEngine;
using SpinMotion;

namespace GMTK
{
    /// <summary>
    /// Final-gate win rule (checklist stage 6). Once the final duel begins, the first of the
    /// surviving racers to cross the gate wins and the loser(s) are executed. The player
    /// winning is a Win; an AI winning is a Lose. The physical closing-gate object/animation
    /// is presentation added later — this component owns the rule and receives crossings via
    /// <see cref="ReportGateCrossing"/> (from a gate trigger or a test).
    /// Self-attaches to the GMTK game-mode host.
    /// </summary>
    public class FinalGate : MonoBehaviour
    {
        public static FinalGate Instance { get; private set; }

        public bool IsOpen { get; private set; }      // duel active, awaiting a crossing
        public int WinnerIndex { get; private set; } = -1;

        private readonly List<int> duelCars = new();
        private bool resolved;

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

            // the loser(s) are executed as the gate closes behind the winner
            foreach (var idx in duelCars)
            {
                if (idx == winner) continue;
                var car = Race.CarByIndex(idx);
                if (car == null) continue;
                var ex = car.GetComponent<CarExplosion>();
                if (ex == null) ex = car.AddComponent<CarExplosion>();
                ex.Explode();
            }

            var e = Race.Events;
            if (e != null)
                e.RaceFinishedEvent.Invoke(winner == 0 ? RaceFinishType.Win : RaceFinishType.Lose);
        }

        private void ResetGate()
        {
            IsOpen = false;
            resolved = false;
            WinnerIndex = -1;
            duelCars.Clear();
        }
    }
}
