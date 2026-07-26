using NUnit.Framework;
using Gmtk2026.GameBalance;

namespace Gmtk2026.GameBalance.Tests
{
    public class CurseCooldownStateTests
    {
        // GDD default: shared cooldown 12s, OrderedRotation
        private CurseSettings Settings() => new CurseSettings();

        [Test]
        public void StartsReadyWithRuptureNext()
        {
            var c = new CurseCooldownState(Settings());
            Assert.IsTrue(c.IsReady);
            Assert.AreEqual(0f, c.CooldownRemaining);
            Assert.AreEqual(CurseType.Rupture, c.NextOrdered());
        }

        [Test]
        public void SuccessStartsCooldownAndAdvancesRotation()
        {
            var c = new CurseCooldownState(Settings());
            c.OnCastSucceeded();
            Assert.IsFalse(c.IsReady);
            Assert.AreEqual(12f, c.CooldownRemaining);
            Assert.AreEqual(CurseType.EngineSeal, c.NextOrdered());
        }

        [Test]
        public void RotationCyclesThroughAllThreeThenWraps()
        {
            var c = new CurseCooldownState(Settings());
            Assert.AreEqual(CurseType.Rupture, c.NextOrdered());
            c.OnCastSucceeded();
            Assert.AreEqual(CurseType.EngineSeal, c.NextOrdered());
            c.OnCastSucceeded();
            Assert.AreEqual(CurseType.SoulSwap, c.NextOrdered());
            c.OnCastSucceeded();
            Assert.AreEqual(CurseType.Rupture, c.NextOrdered(), "wraps back to the first curse");
        }

        [Test]
        public void FailedAttemptSpendsCooldownButKeepsNextCurse()
        {
            var c = new CurseCooldownState(Settings());
            c.OnCastFailed();
            Assert.IsFalse(c.IsReady);
            Assert.AreEqual(12f, c.CooldownRemaining);
            Assert.AreEqual(CurseType.Rupture, c.NextOrdered(), "failure does not advance rotation");
        }

        [Test]
        public void CooldownCountsDownToReady()
        {
            var c = new CurseCooldownState(Settings());
            c.OnCastSucceeded();
            Assert.AreEqual(0f, c.CooldownProgress, 0.001f);
            c.Tick(11f);
            Assert.IsFalse(c.IsReady);
            Assert.AreEqual(1f, c.CooldownRemaining, 0.001f);
            Assert.AreEqual(11f / 12f, c.CooldownProgress, 0.001f);
            c.Tick(1f);
            Assert.IsTrue(c.IsReady);
            Assert.AreEqual(0f, c.CooldownRemaining);
            Assert.AreEqual(1f, c.CooldownProgress, 0.001f);
        }

        [Test]
        public void ResetCooldownMakesReadyImmediately()
        {
            var c = new CurseCooldownState(Settings());
            c.OnCastSucceeded();
            Assert.IsFalse(c.IsReady);
            c.ResetCooldown();          // overtake reward
            Assert.IsTrue(c.IsReady);
            Assert.AreEqual(CurseType.EngineSeal, c.NextOrdered(), "reset keeps the rotation");
        }

        [Test]
        public void ResetForRaceRestoresReadyAndRotation()
        {
            var c = new CurseCooldownState(Settings());
            c.OnCastSucceeded();
            c.OnCastSucceeded();
            c.ResetForRace();
            Assert.IsTrue(c.IsReady);
            Assert.AreEqual(CurseType.Rupture, c.NextOrdered());
        }

        [TestCase(0f, 0f, false)]
        [TestCase(1f, 1f, true)]
        [TestCase(0.7f, 0.69f, true)]
        [TestCase(0.7f, 0.7f, false)]
        public void AiQuizUsesAbilityAsSuccessProbability(
            float successChance,
            float roll,
            bool expected)
        {
            Assert.AreEqual(expected, CurseCooldownState.ResolveAiQuiz(successChance, roll));
        }

        [TestCase(100d, 101d, true)]
        [TestCase(100d, 100d, false)]
        [TestCase(100d, 99d, false)]
        public void CurseTargetMustBeStrictlyAhead(
            double casterScore,
            double targetScore,
            bool expected)
        {
            Assert.AreEqual(
                expected,
                CurseCooldownState.IsValidTargetByScore(casterScore, targetScore));
        }
    }
}
