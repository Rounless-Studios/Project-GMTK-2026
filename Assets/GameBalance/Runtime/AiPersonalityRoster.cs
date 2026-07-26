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

        /// <summary>
        /// Grid order for an authored composition. <paramref name="assignments"/> is the preset's
        /// one-entry-per-car list, so which personality is doubled is a design decision stored in
        /// the asset rather than a side effect of round-robin order. Only the slot mapping is
        /// shuffled; the composition is preserved exactly.
        /// <para>
        /// Falls back to <see cref="BuildOrder(int,int,int)"/> when the list is missing or does not
        /// cover every car, so a half-filled preset still produces a full grid.
        /// </para>
        /// </summary>
        public static List<int> BuildOrder(
            IReadOnlyList<int> assignments,
            int aiCount,
            int personalityCount,
            int seed)
        {
            if (aiCount <= 0 || personalityCount <= 0)
                return new List<int>();

            if (assignments == null || assignments.Count < aiCount)
                return BuildOrder(aiCount, personalityCount, seed);

            var order = new List<int>(aiCount);
            for (int i = 0; i < aiCount; i++)
            {
                int personality = assignments[i];

                // an out-of-range entry would index past the personality table at the call site
                if (personality < 0 || personality >= personalityCount)
                    return BuildOrder(aiCount, personalityCount, seed);

                order.Add(personality);
            }

            Shuffle(order, seed);
            return order;
        }

        /// <summary>
        /// Grid order for an explicit per-car roster: the roster entry each AI slot drives, for an
        /// already resolved seed. Every entry is used exactly once when the counts match, so a car's
        /// personality and its body stay paired while the slot it starts from still changes each race.
        /// A field larger than the roster keeps cycling it.
        /// </summary>
        public static List<int> BuildRosterOrder(int entryCount, int aiCount, int seed)
        {
            var order = new List<int>();

            if (entryCount <= 0 || aiCount <= 0)
                return order;

            for (int i = 0; i < entryCount; i++)
                order.Add(i);

            Shuffle(order, seed);

            while (order.Count < aiCount)
                order.Add(order[order.Count - entryCount]);

            if (order.Count > aiCount)
                order.RemoveRange(aiCount, order.Count - aiCount);

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
