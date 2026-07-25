using NUnit.Framework;
using Gmtk2026.GameBalance;

namespace Gmtk2026.GameBalance.Tests
{
    public class BoostStateTests
    {
        // GDD defaults: max 2, duration 1.25, recharge 6/charge, overtake reward 1, mult 1.5
        private BoostSettings Settings() => new BoostSettings();

        [Test]
        public void StartsAtMaxChargesNotBoosting()
        {
            var b = new BoostState(Settings());
            Assert.AreEqual(2, b.Charges);
            Assert.IsFalse(b.IsBoosting);
            Assert.AreEqual(1f, b.CurrentSpeedMultiplier);
        }

        [Test]
        public void ActivateConsumesChargeAndBoosts()
        {
            var b = new BoostState(Settings());
            Assert.IsTrue(b.TryActivate());
            Assert.AreEqual(1, b.Charges);
            Assert.IsTrue(b.IsBoosting);
            Assert.AreEqual(1.5f, b.CurrentSpeedMultiplier);
        }

        [Test]
        public void CannotActivateWhileBoosting()
        {
            var b = new BoostState(Settings());
            Assert.IsTrue(b.TryActivate());
            Assert.IsFalse(b.TryActivate(), "already boosting");
            Assert.AreEqual(1, b.Charges, "no second charge spent");
        }

        [Test]
        public void CannotActivateWithoutCharges()
        {
            var b = new BoostState(Settings());
            Assert.IsTrue(b.TryActivate());   // 2 -> 1
            b.Tick(1.25f);                    // boost ends
            Assert.IsTrue(b.TryActivate());   // 1 -> 0
            b.Tick(1.25f);
            Assert.IsFalse(b.TryActivate(), "no charges left");
        }

        [Test]
        public void BoostEndsAfterDuration()
        {
            var b = new BoostState(Settings());
            bool ended = false;
            b.BoostEnded += () => ended = true;
            b.TryActivate();
            b.Tick(1f);
            Assert.IsTrue(b.IsBoosting);
            b.Tick(0.3f);                     // total 1.3 > 1.25
            Assert.IsFalse(b.IsBoosting);
            Assert.IsTrue(ended);
            Assert.AreEqual(1f, b.CurrentSpeedMultiplier);
        }

        [Test]
        public void RechargesOneChargeAfterInterval()
        {
            var b = new BoostState(Settings());
            b.TryActivate(); b.Tick(1.25f);   // charges 1
            Assert.AreEqual(1, b.Charges);
            b.Tick(6f);                       // one recharge interval
            Assert.AreEqual(2, b.Charges);
        }

        [Test]
        public void RechargeDoesNotExceedMax()
        {
            var b = new BoostState(Settings());
            b.Tick(100f);                     // already full
            Assert.AreEqual(2, b.Charges);
        }

        [Test]
        public void OvertakeRewardAddsChargeCapped()
        {
            var b = new BoostState(Settings());
            b.TryActivate(); b.Tick(1.25f);   // charges 1
            b.AddCharges(1);
            Assert.AreEqual(2, b.Charges);
            b.AddCharges(1);                  // capped
            Assert.AreEqual(2, b.Charges);
        }

        [Test]
        public void SealBlocksActivation()
        {
            var b = new BoostState(Settings());
            b.ApplySeal(3f);
            Assert.IsTrue(b.IsSealed);
            Assert.IsFalse(b.TryActivate());
            Assert.AreEqual(2, b.Charges, "no charge spent while sealed");
        }

        [Test]
        public void SealPausesRechargeThenResumesAfterExpiry()
        {
            var b = new BoostState(Settings());
            b.TryActivate(); b.Tick(1.25f);   // charges 1
            b.ApplySeal(3f);
            b.Tick(6f);                       // 3s sealed (no recharge) then 3s free
            Assert.AreEqual(1, b.Charges, "recharge paused during seal");
            Assert.IsFalse(b.IsSealed);
            b.Tick(6f);                       // now free to recharge
            Assert.AreEqual(2, b.Charges);
        }

        [Test]
        public void SealExpiresAndFiresSealChanged()
        {
            var b = new BoostState(Settings());
            int sealEvents = 0;
            bool lastSealed = false;
            b.SealChanged += v => { sealEvents++; lastSealed = v; };
            b.ApplySeal(2f);
            Assert.AreEqual(1, sealEvents);
            Assert.IsTrue(lastSealed);
            b.Tick(2f);
            Assert.AreEqual(2, sealEvents);
            Assert.IsFalse(lastSealed);
            Assert.IsFalse(b.IsSealed);
        }

        [Test]
        public void ResetForRaceRestoresFullCharges()
        {
            var b = new BoostState(Settings());
            b.TryActivate();
            b.ApplySeal(5f);
            b.ResetForRace();
            Assert.AreEqual(2, b.Charges);
            Assert.IsFalse(b.IsBoosting);
            Assert.IsFalse(b.IsSealed);
        }
    }
}
