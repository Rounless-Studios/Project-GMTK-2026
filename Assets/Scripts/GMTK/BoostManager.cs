using UnityEngine;

namespace GMTK
{
    /// <summary>
    /// Gives every car a <see cref="BoostController"/> for the race and resets them on
    /// start / restart (checklist stage 3.2). Self-attaches to the GMTK game-mode host.
    /// </summary>
    public class BoostManager : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            var host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<BoostManager>() == null)
                host.AddComponent<BoostManager>();
        }

        private void Start()
        {
            var events = Race.Events;
            if (events == null) return;
            events.RaceStartedEvent.AddListener(EnsureControllers);
            events.RestartRaceEvent.AddListener(EnsureControllers);
        }

        private void EnsureControllers()
        {
            foreach (var idx in Race.AllCarIndices())
            {
                var car = Race.CarByIndex(idx);
                if (car == null) continue;
                var bc = car.GetComponent<BoostController>();
                if (bc == null) car.AddComponent<BoostController>();
                else bc.ResetForRace();
            }
        }
    }
}
