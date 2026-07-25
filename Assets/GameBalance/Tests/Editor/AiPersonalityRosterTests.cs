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

        // ---- authored composition (AISettings.personalityAssignments) ----

        // Reckless is index 3 in the personality table; the confirmed grid doubles it.
        private static readonly List<int> ConfirmedGrid = new() { 3, 3, 1, 2, 0 };

        [Test]
        public void AuthoredCompositionIsPreservedExactly()
        {
            var order = AiPersonalityRoster.BuildOrder(ConfirmedGrid, 5, PersonalityCount, 2468);
            var counts = Tally(order);

            Assert.AreEqual(5, order.Count);
            Assert.AreEqual(2, counts[3], "the preset doubles Reckless, so two cars must get it");
            Assert.AreEqual(1, counts[1]);
            Assert.AreEqual(1, counts[2]);
            Assert.AreEqual(1, counts[0]);
        }

        [Test]
        public void AuthoredCompositionShufflesSlotsButNotTheMix()
        {
            var a = AiPersonalityRoster.BuildOrder(ConfirmedGrid, 5, PersonalityCount, 11);
            var b = AiPersonalityRoster.BuildOrder(ConfirmedGrid, 5, PersonalityCount, 22);

            Assert.AreNotEqual(a, b, "different seeds must move personalities between grid slots");
            CollectionAssert.AreEquivalent(a, b, "but the composition must be identical");
        }

        [Test]
        public void AuthoredCompositionIsReproducibleForOneSeed()
        {
            Assert.AreEqual(
                AiPersonalityRoster.BuildOrder(ConfirmedGrid, 5, PersonalityCount, 777),
                AiPersonalityRoster.BuildOrder(ConfirmedGrid, 5, PersonalityCount, 777));
        }

        [Test]
        public void ShortOrMissingAssignmentsFallBackToRoundRobin()
        {
            var expected = AiPersonalityRoster.BuildOrder(5, PersonalityCount, 99);

            Assert.AreEqual(expected, AiPersonalityRoster.BuildOrder(null, 5, PersonalityCount, 99),
                "a preset with no list must still fill the grid");
            Assert.AreEqual(
                expected,
                AiPersonalityRoster.BuildOrder(new List<int> { 3, 3 }, 5, PersonalityCount, 99),
                "a list that does not cover every car must not leave cars unassigned");
        }

        [Test]
        public void OutOfRangeAssignmentFallsBackInsteadOfIndexingPastTheTable()
        {
            var order = AiPersonalityRoster.BuildOrder(
                new List<int> { 0, 1, 2, 3, PersonalityCount }, 5, PersonalityCount, 99);

            Assert.AreEqual(AiPersonalityRoster.BuildOrder(5, PersonalityCount, 99), order);
            foreach (int personality in order)
                Assert.Less(personality, PersonalityCount);
        }

        [Test]
        public void AuthoredCompositionUsesMoreCarsThanEntriesSafely()
        {
            Assert.IsEmpty(AiPersonalityRoster.BuildOrder(ConfirmedGrid, 0, PersonalityCount, 1));
            Assert.IsEmpty(AiPersonalityRoster.BuildOrder(ConfirmedGrid, 5, 0, 1));
        }

        private static Dictionary<int, int> Tally(List<int> order)
        {
            var counts = new Dictionary<int, int>();
            foreach (int personality in order)
            {
                counts.TryGetValue(personality, out int seen);
                counts[personality] = seen + 1;
            }
            return counts;
        }
    }
}
