using NUnit.Framework;
using Gmtk2026.GameBalance;

namespace Gmtk2026.GameBalance.Tests
{
    public class OvertakeChallengeStateTests
    {
        // GDD defaults: challenge 8s, hold 0.5s
        private OvertakeSettings Settings() => new OvertakeSettings();

        [Test]
        public void StartsIdle()
        {
            var o = new OvertakeChallengeState(Settings());
            Assert.AreEqual(OvertakeStatus.Idle, o.Status);
        }

        [Test]
        public void BeginActivatesWithFullTime()
        {
            var o = new OvertakeChallengeState(Settings());
            o.Begin();
            Assert.AreEqual(OvertakeStatus.Active, o.Status);
            Assert.AreEqual(8f, o.TimeRemaining);
            Assert.AreEqual(0f, o.LeadHeld);
        }

        [Test]
        public void HoldingLeadForRequiredTimeSucceeds()
        {
            var o = new OvertakeChallengeState(Settings());
            o.Begin();
            o.Tick(0.3f, true);
            Assert.AreEqual(OvertakeStatus.Active, o.Status);
            var st = o.Tick(0.3f, true);   // 0.6 >= 0.5
            Assert.AreEqual(OvertakeStatus.Succeeded, st);
        }

        [Test]
        public void DroppingBehindResetsHeldLead()
        {
            var o = new OvertakeChallengeState(Settings());
            o.Begin();
            o.Tick(0.4f, true);            // held 0.4
            o.Tick(0.1f, false);           // dropped -> reset
            Assert.AreEqual(0f, o.LeadHeld);
            Assert.AreEqual(OvertakeStatus.Active, o.Status);
            o.Tick(0.4f, true);            // held 0.4
            var st = o.Tick(0.2f, true);   // 0.6 >= 0.5
            Assert.AreEqual(OvertakeStatus.Succeeded, st);
        }

        [Test]
        public void RunningOutOfTimeWithoutLeadFails()
        {
            var o = new OvertakeChallengeState(Settings());
            o.Begin();
            var st = o.Tick(8f, false);
            Assert.AreEqual(OvertakeStatus.Failed, st);
            Assert.AreEqual(0f, o.TimeRemaining);
        }

        [Test]
        public void LeadAchievedOnFinalTickStillWins()
        {
            var o = new OvertakeChallengeState(Settings());
            o.Begin();
            o.Tick(7.5f, false);           // 0.5s left, no lead
            var st = o.Tick(0.5f, true);   // gains lead exactly as time runs out
            Assert.AreEqual(OvertakeStatus.Succeeded, st, "success is checked before timeout");
        }

        [Test]
        public void TickAfterResolutionIsNoOp()
        {
            var o = new OvertakeChallengeState(Settings());
            o.Begin();
            o.Tick(8f, false);             // Failed
            var st = o.Tick(1f, true);
            Assert.AreEqual(OvertakeStatus.Failed, st, "resolved challenge does not change");
        }

        [Test]
        public void CancelReturnsToIdle()
        {
            var o = new OvertakeChallengeState(Settings());
            o.Begin();
            o.Tick(0.3f, true);
            o.Cancel();
            Assert.AreEqual(OvertakeStatus.Idle, o.Status);
            Assert.AreEqual(0f, o.LeadHeld);
        }
    }
}
