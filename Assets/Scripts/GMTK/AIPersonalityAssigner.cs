using System.Collections.Generic;
using UnityEngine;
using SpinMotion;

namespace GMTK
{
    /// <summary>
    /// Assigns a varied mix of AIPersonality types to the AI cars right after they spawn.
    /// The player (race index 0) is left untouched. Self-attaches to the game-mode host.
    /// </summary>
    public class AIPersonalityAssigner : MonoBehaviour
    {
        // Order tuned so a small field still gets a rammer and a blocker early.
        private static readonly AIPersonalityType[] Rotation =
        {
            AIPersonalityType.Rammer,
            AIPersonalityType.CleanRacer,
            AIPersonalityType.Blocker,
            AIPersonalityType.Reckless,
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            var host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<AIPersonalityAssigner>() == null)
                host.AddComponent<AIPersonalityAssigner>();
        }

        private void Start()
        {
            var events = Race.Events;
            if (events != null)
                events.PlayersCheckpointTrackersAssignedEvent.AddListener(OnPlayersAssigned);
        }

        private void OnPlayersAssigned(List<CheckpointTracker> trackers)
        {
            if (trackers == null) return;

            int aiOrdinal = 0;
            foreach (var tracker in trackers)
            {
                if (tracker == null) continue;
                int raceIndex = tracker.GetCarRacePositionIndex();
                if (raceIndex == 0) continue; // skip the human player

                var car = Race.CarByIndex(raceIndex);
                if (car == null) continue;

                var personality = car.GetComponent<AIPersonality>();
                if (personality == null) personality = car.AddComponent<AIPersonality>();
                personality.type = Rotation[aiOrdinal % Rotation.Length];
                personality.ApplyToDriver();
                aiOrdinal++;
            }
        }
    }
}
