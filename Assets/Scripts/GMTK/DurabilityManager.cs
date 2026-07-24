using UnityEngine;

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
            if (events == null) return;
            events.RaceStartedEvent.AddListener(OnRaceStarted);
            events.RestartRaceEvent.AddListener(OnRestartRace);
        }

        private void OnRaceStarted() => EnsureControllers();
        private void OnRestartRace() => EnsureControllers();

        private void EnsureControllers()
        {
            foreach (var idx in Race.AllCarIndices())
            {
                var car = Race.CarByIndex(idx);
                if (car == null) continue;
                var dc = car.GetComponent<DurabilityController>();
                if (dc == null) car.AddComponent<DurabilityController>();
                else dc.ResetForRace();
            }
        }
    }
}
