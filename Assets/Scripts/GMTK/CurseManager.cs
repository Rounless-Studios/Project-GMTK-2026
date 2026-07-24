using UnityEngine;

namespace GMTK
{
    /// <summary>
    /// Gives every car a <see cref="CurseController"/> for the race, assigns its race index and
    /// resets it on start / restart (checklist stage 3.7). Self-attaches to the GMTK game-mode
    /// host. The player's E-input / quiz trigger and AI casting are layered on top of the
    /// controllers this creates.
    /// </summary>
    public class CurseManager : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            var host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<CurseManager>() == null)
                host.AddComponent<CurseManager>();
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
                var cc = car.GetComponent<CurseController>();
                if (cc == null) cc = car.AddComponent<CurseController>();
                else cc.ResetForRace();
                cc.RaceIndex = idx;
            }
        }
    }
}
