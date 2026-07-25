using System.Collections.Generic;
using System.Text;
using Gmtk2026.GameBalance;
using UnityEngine;
using SpinMotion;

namespace GMTK
{
    /// <summary>
    /// Assigns the authored mix of AIPersonality types to the AI cars right after they spawn.
    /// The composition comes from <c>AISettings.personalityAssignments</c> (confirmed default:
    /// 폭주광 2 + 난폭자 1 + 봉쇄자 1 + 생존자 1) and only the grid slot mapping is shuffled, so
    /// the slot no longer predicts the personality while the mix stays what the preset says.
    /// The player (race index 0) is left untouched. Self-attaches to the game-mode host.
    /// </summary>
    public class AIPersonalityAssigner : MonoBehaviour
    {
        // Index table for the roster, which works in ints to stay engine-agnostic. Order here is
        // only the int mapping; it does not decide the composition any more.
        private static readonly AIPersonalityType[] Personalities =
        {
            AIPersonalityType.CleanRacer,
            AIPersonalityType.Rammer,
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

            var ai = GameBalance.Current.ai;
            var settings = ai.personalityAssignment;
            int aiCount = 0;
            foreach (var tracker in trackers)
                if (tracker != null && tracker.GetCarRacePositionIndex() != 0) aiCount++;

            int seed = AiPersonalityRoster.ResolveSeed(settings.shuffleSeed);
            List<int> order = AiPersonalityRoster.BuildOrder(
                ToIndices(ai.personalityAssignments),
                aiCount,
                Personalities.Length,
                seed);
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

        // The roster works in personality indices so it stays free of the gameplay enum.
        private static List<int> ToIndices(List<AIPersonalityType> assignments)
        {
            if (assignments == null) return null;

            var indices = new List<int>(assignments.Count);
            foreach (var personality in assignments)
                indices.Add(System.Array.IndexOf(Personalities, personality));
            return indices;
        }
    }
}
