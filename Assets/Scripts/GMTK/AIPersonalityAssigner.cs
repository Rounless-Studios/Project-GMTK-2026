using System.Collections.Generic;
using System.Text;
using Gmtk2026.GameBalance;
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
        // The mix is shuffled per race so the grid slot no longer predicts the personality, while
        // AiPersonalityRoster keeps the composition even (every type is used before any repeats).
        private static readonly AIPersonalityType[] Personalities =
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

            var settings = GameBalance.Current.ai.personalityAssignment;
            int aiCount = 0;
            foreach (var tracker in trackers)
                if (tracker != null && tracker.GetCarRacePositionIndex() != 0) aiCount++;

            int seed = AiPersonalityRoster.ResolveSeed(settings.shuffleSeed);
            List<int> order = AiPersonalityRoster.BuildOrder(aiCount, Personalities.Length, seed);
            var assigned = new StringBuilder();

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
                personality.type = Personalities[order[aiOrdinal % order.Count]];
                personality.ApplyToDriver();

                assigned.Append(' ').Append(raceIndex).Append('=').Append(personality.type);
                aiOrdinal++;
            }

            // the seed makes an odd race reproducible: put it back into the balance asset to replay it
            if (settings.logAssignment && aiOrdinal > 0)
                Debug.Log($"AI personalities: seed={seed}{assigned}");
        }
    }
}
