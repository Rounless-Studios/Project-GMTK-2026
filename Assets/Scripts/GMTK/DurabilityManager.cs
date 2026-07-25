using UnityEngine;
using System.Collections.Generic;

namespace GMTK
{
    /// <summary>
    /// Gives every car a <see cref="DurabilityController"/> for the race and resets them on
    /// start / restart (checklist stage 7). Self-attaches to the GMTK game-mode host.
    /// </summary>
    public class DurabilityManager : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            var host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<DurabilityManager>() == null)
                host.AddComponent<DurabilityManager>();
        }

        private void Start()
        {
            var events = Race.Events;
            if (events != null)
            {
                events.RaceStartedEvent.AddListener(OnRaceStarted);
                events.RestartRaceEvent.AddListener(OnRestartRace);
            }

            // BxB MVC scenes (including bora) do not have the Racing Starter Kit event
            // bus. Discover their live vehicle component directly instead. Repeating the
            // scan also covers vehicles spawned shortly after scene initialization.
            EnsureControllers(false);
            InvokeRepeating(nameof(EnsureLateSpawnedControllers), 1f, 1f);
        }

        private void OnDestroy()
        {
            CancelInvoke(nameof(EnsureLateSpawnedControllers));
            var events = Race.Events;
            if (events == null) return;
            events.RaceStartedEvent.RemoveListener(OnRaceStarted);
            events.RestartRaceEvent.RemoveListener(OnRestartRace);
        }

        private void OnRaceStarted() => EnsureControllers(true);
        private void OnRestartRace() => EnsureControllers(true);
        private void EnsureLateSpawnedControllers() => EnsureControllers(false);

        private void EnsureControllers(bool resetExisting)
        {
            var cars = new HashSet<GameObject>();

            foreach (var idx in Race.AllCarIndices())
            {
                var car = Race.CarByIndex(idx);
                if (car != null) cars.Add(car);
            }

            // Avoid a compile-time dependency on the optional MVC package. Its main
            // vehicle component is stable and uniquely identifies the car root.
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (behaviour != null &&
                    behaviour.GetType().FullName == "MVC.Core.Vehicle")
                {
                    cars.Add(behaviour.gameObject);
                }
            }

            foreach (var car in cars)
            {
                var controller = car.GetComponent<DurabilityController>();
                if (controller == null)
                    car.AddComponent<DurabilityController>();
                else if (resetExisting)
                    controller.ResetForRace();
            }
        }
    }
}
