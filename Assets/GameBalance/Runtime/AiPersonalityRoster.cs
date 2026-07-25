using System;
using System.Collections.Generic;

namespace Gmtk2026.GameBalance
{
    /// <summary>
    /// Decides which personality each AI car gets. The mix is shuffled per race so players cannot
    /// learn "the car in slot 1 always rams", while the composition stays even: every personality is
    /// handed out once before any is repeated. A non-zero seed reproduces one exact grid.
    /// Personalities are returned as indices so this stays independent of the gameplay enum.
    /// </summary>
    public static class AiPersonalityRoster
    {
        /// <summary>
        /// Turns the configured seed into the one actually used: 0 means "fresh mix this race". Call
        /// this first so the seed can be logged with the resulting grid.
        /// </summary>
        public static int ResolveSeed(int configuredSeed)
        {
            return configuredSeed != 0 ? configuredSeed : NewSeed();
        }

        /// <summary>
        /// Personality index per AI car, in race order, for an already resolved seed.
        /// </summary>
        public static List<int> BuildOrder(int aiCount, int personalityCount, int seed)
        {
            var order = new List<int>();

            if (aiCount <= 0 || personalityCount <= 0)
                return order;

            // even composition first: round-robin so 4 cars over 4 personalities is one of each
            for (int i = 0; i < aiCount; i++)
                order.Add(i % personalityCount);

            Shuffle(order, seed);
            return order;
        }

        private static int NewSeed()
        {
            int seed = Environment.TickCount & int.MaxValue;
            return seed == 0 ? 1 : seed;
        }

        private static void Shuffle(List<int> order, int seed)
        {
            var random = new Random(seed);

            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
        }
    }
}
