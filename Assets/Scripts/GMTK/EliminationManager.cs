using System.Collections.Generic;
using UnityEngine;
using SpinMotion;

namespace GMTK
{
    /// <summary>
    /// Battle-royale race rule: every few seconds the car in last place explodes.
    /// Repeats until a single car remains — that survivor wins. If the player is the
    /// one eliminated, the race ends in a loss immediately.
    /// Self-attaches to the GMTK game-mode host; no scene wiring required.
    /// </summary>
    public class EliminationManager : MonoBehaviour
    {
        [Header("Elimination Timing")]
        [Tooltip("Grace period after the race starts before the first elimination.")]
        public float firstEliminationDelaySeconds = 20f;
        [Tooltip("Seconds between eliminations.")]
        public float eliminationIntervalSeconds = 12f;
        public bool enableElimination = true;

        [Header("Explosion (applied to each eliminated car)")]
        public float explosionForce = 1600f;
        public float upwardForce = 9f;
        public float explosionRadius = 6f;
        public float wreckLingerSeconds = 2.5f;

        private readonly HashSet<int> eliminated = new();
        private float nextEliminationTime;
        private bool armed;
        private bool finished;

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
            armed = enableElimination;
            nextEliminationTime = Time.time + firstEliminationDelaySeconds;
        }

        private void OnRestartRace()
        {
            armed = false;
            eliminated.Clear();
            finished = false;
        }

        private void OnRaceFinished(RaceFinishType type)
        {
            armed = false;
        }

        private void Update()
        {
            if (!armed || finished || !Race.IsRaceInProgress) return;
            if (Time.time < nextEliminationTime) return;

            nextEliminationTime = Time.time + eliminationIntervalSeconds;
            EliminateLastPlace();
        }

        private int ActiveCount()
        {
            return Mathf.Max(0, Race.CarCount - eliminated.Count);
        }

        private void EliminateLastPlace()
        {
            int carCount = Race.CarCount;
            if (carCount <= 1) { armed = false; return; }
            if (ActiveCount() <= 1) { armed = false; return; }

            // find the active car with the lowest race score
            int lastIndex = -1;
            double lowest = double.MaxValue;
            for (int i = 0; i < carCount; i++)
            {
                if (eliminated.Contains(i)) continue;
                double score = Race.ScoreOf(i);
                if (score < lowest)
                {
                    lowest = score;
                    lastIndex = i;
                }
            }

            if (lastIndex < 0) return;

            eliminated.Add(lastIndex);
            ExplodeCar(lastIndex);

            // resolve end conditions
            if (lastIndex == 0)
            {
                // the player was eliminated → immediate loss
                FinishRace(RaceFinishType.Lose);
            }
            else if (ActiveCount() <= 1)
            {
                // only the player remains → win
                FinishRace(RaceFinishType.Win);
            }
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
            explosion.wreckLingerSeconds = wreckLingerSeconds;
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
