using System.Collections.Generic;
using System.Text;
using Gmtk2026.GameBalance;
using UnityEngine;
using GMTK.Kit;

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
        private static readonly List<AIPersonalityType> plan = new();
        private static int plannedForCarCount = -1;

        /// <summary>
        /// The personality each AI car will get, in race-index order (index 0 is race index 1).
        /// <para>
        /// The spawner needs this before it instantiates anything, because the car a personality
        /// drives is chosen at spawn time, while the personality itself is assigned once the grid
        /// exists. Both callers share this one plan instead of drawing the shuffle twice and
        /// disagreeing about who is who.
        /// </para>
        /// </summary>
        public static IReadOnlyList<AIPersonalityType> PlanForRace(int aiCount, bool rebuild = false)
        {
            if (!rebuild && plannedForCarCount == aiCount && plan.Count == aiCount)
                return plan;

            var ai = GameBalance.Current.ai;
            int seed = AiPersonalityRoster.ResolveSeed(ai.personalityAssignment.shuffleSeed);
            List<int> order = AiPersonalityRoster.BuildOrder(
                ToIndices(ai.personalityAssignments),
                aiCount,
                Personalities.Length,
                seed);

            plan.Clear();
            for (int i = 0; i < aiCount; i++)
                plan.Add(Personalities[order[i % order.Count]]);

            plannedForCarCount = aiCount;
            plannedSeed = seed;
            return plan;
        }

        private static int plannedSeed;


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

            // the spawner already drew this plan to pick each car's prefab; reuse it so the
            // paint on the car and the personality driving it can never disagree
            IReadOnlyList<AIPersonalityType> order = PlanForRace(aiCount);
            int seed = plannedSeed;
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
                personality.type = order[aiOrdinal % order.Count];
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
