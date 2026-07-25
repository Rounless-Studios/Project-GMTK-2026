using System.Collections.Generic;
using NUnit.Framework;

namespace Gmtk2026.GameBalance.Tests
{
    public class AiPersonalityRosterTests
    {
        private const int PersonalityCount = 4;

        [Test]
        public void OneEntryPerAiCar()
        {
            Assert.AreEqual(5, AiPersonalityRoster.BuildOrder(5, PersonalityCount, 1234).Count);
            Assert.AreEqual(0, AiPersonalityRoster.BuildOrder(0, PersonalityCount, 1234).Count);
            Assert.AreEqual(0, AiPersonalityRoster.BuildOrder(3, 0, 1234).Count);
        }

        [Test]
        public void EveryPersonalityAppearsBeforeAnyRepeats()
        {
            var order = AiPersonalityRoster.BuildOrder(PersonalityCount, PersonalityCount, 99);
            order.Sort();

            for (int i = 0; i < PersonalityCount; i++)
                Assert.AreEqual(i, order[i], "4 cars over 4 personalities must be one of each");
        }

        [Test]
        public void CompositionStaysEvenWhenCarsOutnumberPersonalities()
        {
            var counts = new Dictionary<int, int>();

            foreach (int personality in AiPersonalityRoster.BuildOrder(6, PersonalityCount, 7))
            {
                counts.TryGetValue(personality, out int seen);
                counts[personality] = seen + 1;
            }

            Assert.AreEqual(PersonalityCount, counts.Count, "every personality must be used");

            foreach (KeyValuePair<int, int> entry in counts)
                Assert.LessOrEqual(entry.Value, 2, "no personality may be handed out three times for 6 cars");
        }

        [Test]
        public void SameSeedReproducesTheSameGrid()
        {
            var first = AiPersonalityRoster.BuildOrder(5, PersonalityCount, 4321);
            var second = AiPersonalityRoster.BuildOrder(5, PersonalityCount, 4321);

            Assert.AreEqual(first, second);
        }

        [Test]
        public void DifferentSeedsGiveDifferentGrids()
        {
            var a = AiPersonalityRoster.BuildOrder(8, PersonalityCount, 1);
            var b = AiPersonalityRoster.BuildOrder(8, PersonalityCount, 2);

            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void ResolveSeedKeepsAConfiguredSeedAndReplacesZero()
        {
            Assert.AreEqual(555, AiPersonalityRoster.ResolveSeed(555));
            Assert.AreNotEqual(0, AiPersonalityRoster.ResolveSeed(0), "0 must resolve to a usable seed");
        }
    }
}
