using NUnit.Framework;
using UnityEngine;

namespace Gmtk2026.GameBalance.Tests
{
    public class AiDrivingTests
    {
        private AiDrivingSettings Settings() => new AiDrivingSettings();

        [Test]
        public void LookAheadGrowsWithSpeed()
        {
            var s = Settings();
            float standstill = AiDriving.LookAheadMetres(0f, s);
            float fast = AiDriving.LookAheadMetres(100f, s);

            Assert.AreEqual(s.minLookAheadMetres, standstill);
            Assert.Greater(fast, standstill);
            Assert.AreEqual(s.minLookAheadMetres + 100f * s.lookAheadMetresPerKph, fast, 0.001f);
        }

        [Test]
        public void StraightKeepsFullSpeed()
        {
            var s = Settings();
            Assert.AreEqual(s.straightSpeedKph, AiDriving.CornerSpeedKph(0f, ScanAt(s, 100f), 1f, s), 0.001f);
        }

        [Test]
        public void WideSweeperStaysFastAndHairpinDropsToTheFloor()
        {
            var s = Settings();

            // 20 degrees over the scan window is a ~140 m radius: an arcade car takes that flat out.
            // Asserting the exact ceiling would only re-state the grip and scan constants, so this
            // checks the intent instead — no meaningful braking.
            Assert.GreaterOrEqual(
                AiDriving.CornerSpeedKph(20f, ScanAt(s, 100f), 1f, s),
                s.straightSpeedKph * 0.85f,
                "a wide sweeper must not be braked for");

            // 180 degrees over 50 m is a ~16 m radius hairpin
            Assert.AreEqual(
                s.minCornerSpeedKph,
                AiDriving.CornerSpeedKph(180f, ScanAt(s, 100f), 1f, s),
                0.001f);
        }

        [Test]
        public void CornerSpeedFollowsTheRadiusAndIgnoresTurnDirection()
        {
            var s = Settings();
            float wide = AiDriving.CornerSpeedKph(45f, ScanAt(s, 100f), 1f, s);
            float tight = AiDriving.CornerSpeedKph(90f, ScanAt(s, 100f), 1f, s);

            Assert.Less(tight, wide, "halving the radius must lower the speed");
            Assert.AreEqual(tight, AiDriving.CornerSpeedKph(-90f, ScanAt(s, 100f), 1f, s), 0.001f);

            // sqrt(grip x radius): the same angle over twice the distance is twice the radius
            Assert.Greater(
                AiDriving.CornerSpeedKph(90f, ScanAt(s, 100f) * 2f, 1f, s),
                tight);
        }

        [Test]
        public void MoreGripTakesCornersFaster()
        {
            var s = Settings();
            float normal = AiDriving.CornerSpeedKph(90f, ScanAt(s, 100f), 1f, s);

            s.cornerGrip *= 2f;
            Assert.Greater(AiDriving.CornerSpeedKph(90f, ScanAt(s, 100f), 1f, s), normal);
        }

        [Test]
        public void PersonalityPaceScalesCornerSpeed()
        {
            var s = Settings();
            Assert.AreEqual(
                AiDriving.CornerSpeedKph(90f, ScanAt(s, 100f), 1f, s) * 0.5f,
                AiDriving.CornerSpeedKph(90f, ScanAt(s, 100f), 0.5f, s),
                0.001f);
        }


        [Test]
        public void CornerScanCoversTheBrakingDistance()
        {
            var s = Settings();

            Assert.AreEqual(s.minCornerScanMetres, AiDriving.CornerScanMetres(0f, s), 0.001f);
            Assert.AreEqual(s.minCornerScanMetres, AiDriving.CornerScanMetres(30f, s), 0.001f,
                "a slow car must not be braked for a corner it cannot reach yet");

            float fast = AiDriving.CornerScanMetres(135f, s);
            float expected = (135f / 3.6f) * (135f / 3.6f) / (2f * s.cornerBrakingDecel);
            Assert.AreEqual(expected, fast, 0.5f);
            Assert.Greater(fast, AiDriving.CornerScanMetres(80f, s));
        }

        [Test]
        public void TargetSpeedDropsAtOnceButRisesGradually()
        {
            var s = Settings();

            Assert.AreEqual(60f, AiDriving.SmoothTargetSpeedKph(120f, 60f, 0.02f, s), 0.001f,
                "slowing down must not be delayed");

            float risen = AiDriving.SmoothTargetSpeedKph(60f, 120f, 0.02f, s);
            Assert.Greater(risen, 60f);
            Assert.Less(risen, 120f);
            Assert.AreEqual(60f + s.targetSpeedRiseKphPerSecond * 0.02f, risen, 0.001f);

            Assert.AreEqual(65f, AiDriving.SmoothTargetSpeedKph(60f, 65f, 1f, s), 0.001f,
                "a small rise must not overshoot the target");
        }

        private static float ScanAt(AiDrivingSettings s, float speedKph) => AiDriving.CornerScanMetres(speedKph, s);

        [Test]
        public void NormalCorneringSlipIsLeftAlone()
        {
            var s = Settings();

            Assert.AreEqual(180f, AiDriving.SlipCorrectedTargetKph(180f, 0f, s), 0.001f);
            Assert.AreEqual(180f, AiDriving.SlipCorrectedTargetKph(180f, s.slipToleranceKph, s), 0.001f,
                "a car settled in a corner slides a little and must not be slowed for it");
        }

        [Test]
        public void SlidingSidewaysCutsTheSpeedTargetWhicheverWayItSlides()
        {
            var s = Settings();
            float slipping = s.slipToleranceKph + 20f;

            float corrected = AiDriving.SlipCorrectedTargetKph(180f, slipping, s);
            Assert.Less(corrected, 180f, "the assumed grip was optimistic, so the target has to come down");
            Assert.AreEqual(corrected, AiDriving.SlipCorrectedTargetKph(180f, -slipping, s), 0.001f,
                "sliding left is the same problem as sliding right");
            Assert.LessOrEqual(
                AiDriving.SlipCorrectedTargetKph(180f, 500f, s),
                s.minCornerSpeedKph + 0.001f,
                "but it never demands less than the slowest corner speed");
        }

        [Test]
        public void ADistantOrUncatchableRivalIsIgnored()
        {
            var s = Settings();

            Assert.AreEqual(0f, AiDriving.OvertakeOffsetMetres(0f, 20f, 0f, 5f, 5f, 1f, s),
                "no car ahead");
            Assert.AreEqual(0f, AiDriving.OvertakeOffsetMetres(s.rivalScanMetres + 1f, 20f, 0f, 5f, 5f, 1f, s),
                "too far ahead to race");
            Assert.AreEqual(0f, AiDriving.OvertakeOffsetMetres(20f, s.overtakeMinClosingKph - 1f, 0f, 5f, 5f, 1f, s),
                "not closing: weaving behind it would only lose time");
            Assert.AreEqual(0f, AiDriving.OvertakeOffsetMetres(20f, 20f, 0f, 5f, 5f, 0f, s),
                "a personality with no aggression never attempts a pass");
        }

        [Test]
        public void OvertakeTakesTheOpenSideAndBuildsAsTheGapCloses()
        {
            var s = Settings();

            float toTheRight = AiDriving.OvertakeOffsetMetres(20f, 20f, 0f, 1f, 6f, 1f, s);
            float toTheLeft = AiDriving.OvertakeOffsetMetres(20f, 20f, 0f, 6f, 1f, 1f, s);
            Assert.Greater(toTheRight, 0f, "more room on the right");
            Assert.Less(toTheLeft, 0f, "more room on the left");

            float near = AiDriving.OvertakeOffsetMetres(5f, 20f, 0f, 1f, 6f, 1f, s);
            Assert.Greater(near, toTheRight, "the closer the car ahead, the further off line the pass goes");
            Assert.LessOrEqual(Mathf.Abs(near), s.overtakeOffsetMetres + 0.0001f);
        }

        [Test]
        public void EquallyBoxedInTheAiGoesRoundTheOtherSideOfTheRival()
        {
            var s = Settings();

            Assert.Less(AiDriving.OvertakeOffsetMetres(10f, 20f, 2f, 5f, 5f, 1f, s), 0f,
                "rival sits to the right, so pass on the left");
            Assert.Greater(AiDriving.OvertakeOffsetMetres(10f, 20f, -2f, 5f, 5f, 1f, s), 0f);
        }

        [Test]
        public void AggressionScalesHowFarOffLineAPassGoes()
        {
            var s = Settings();
            float careful = AiDriving.OvertakeOffsetMetres(10f, 20f, 0f, 1f, 6f, 0.6f, s);
            float committed = AiDriving.OvertakeOffsetMetres(10f, 20f, 0f, 1f, 6f, 1.4f, s);

            Assert.Greater(committed, careful);
        }

        [Test]
        public void ClosingCarLiftsOffInsideItsGapButNotOutsideIt()
        {
            var s = Settings();

            Assert.AreEqual(150f, AiDriving.FollowSpeedKph(150f, 30f, 90f, 8f, s), 0.001f,
                "still far behind: keep the corner-speed target");

            float lifted = AiDriving.FollowSpeedKph(150f, 3f, 90f, 8f, s);
            Assert.Less(lifted, 150f, "inside the gap the car must not drive through the one ahead");
            Assert.LessOrEqual(lifted, 90f);
        }

        [Test]
        public void FollowingACarDoesNotBrakeTheQueueToAWalk()
        {
            var s = Settings();

            // right on the bumper of a car doing 120: the follower gives up a little, not most of it
            float capped = AiDriving.FollowSpeedKph(200f, 0.5f, 120f, 8f, s);

            Assert.Greater(capped, 120f - s.followLiftKph - 0.001f);
            Assert.Greater(capped, 100f,
                "scaling by the remaining gap would put the whole field at walking pace");
        }

        [Test]
        public void ARammerKeepsItsFootIn()
        {
            var s = Settings();

            Assert.AreEqual(150f, AiDriving.FollowSpeedKph(150f, 1f, 90f, 0f, s), 0.001f,
                "tolerance 0 is what makes a rammer use the other car as a brake");
        }

        [Test]
        public void BoostIsSpentOnStraightsOnly()
        {
            var s = Settings();
            float straight = s.boostStraightMaximumDegrees * 0.5f;
            float corner = s.boostStraightMaximumDegrees + 5f;

            Assert.IsTrue(AiDriving.ShouldBoostOnStraight(straight, 120f, true, 1f, 0.5f, s));
            Assert.IsFalse(AiDriving.ShouldBoostOnStraight(corner, 120f, true, 1f, 0.5f, s),
                "a boost into a corner is thrown away");
            Assert.IsFalse(AiDriving.ShouldBoostOnStraight(straight, 120f, false, 1f, 0.5f, s),
                "no charge in hand");
            Assert.IsFalse(
                AiDriving.ShouldBoostOnStraight(straight, s.boostMinimumSpeedKph - 1f, true, 1f, 0.5f, s),
                "too slow for a boost to be worth a charge");
        }

        [Test]
        public void BoostEagernessIsThePersonalitysOwnNumber()
        {
            var s = Settings();

            Assert.IsTrue(AiDriving.ShouldBoostOnStraight(0f, 120f, true, 0.8f, 0.7f, s),
                "an eager personality takes this draw");
            Assert.IsFalse(AiDriving.ShouldBoostOnStraight(0f, 120f, true, 0.3f, 0.7f, s),
                "a cautious one does not");
        }

        [Test]
        public void ABurningBoostRaisesTheTargetOnAStraightAndNotIntoACorner()
        {
            var s = Settings();

            Assert.AreEqual(150f, AiDriving.BoostedTargetKph(100f, 1.5f, 0f, s), 0.001f,
                "otherwise the car brakes against its own boost");
            Assert.AreEqual(100f,
                AiDriving.BoostedTargetKph(100f, 1.5f, s.boostStraightMaximumDegrees + 5f, s), 0.001f,
                "a boost still burning at a corner must not raise the corner speed");
            Assert.AreEqual(100f, AiDriving.BoostedTargetKph(100f, 1f, 0f, s), 0.001f, "not boosting");
        }

        [Test]
        public void ADefenderMovesOntoTheLineOfTheCarBehind()
        {
            var s = Settings();

            float covering = AiDriving.BlockOffsetMetres(6f, 2f, 1f, s);
            Assert.Greater(covering, 0f, "follower is to the right, so move right to cover it");
            Assert.LessOrEqual(covering, s.blockOffsetMetres + 0.0001f);

            Assert.Greater(covering, AiDriving.BlockOffsetMetres(30f, 2f, 1f, s),
                "a car right behind is covered harder than one still approaching");
            Assert.AreEqual(0f, AiDriving.BlockOffsetMetres(6f, 2f, 0f, s),
                "a personality that never defends stays on its line");
            Assert.AreEqual(0f, AiDriving.BlockOffsetMetres(s.rivalScanMetres + 1f, 2f, 1f, s));
        }

        [Test]
        public void SteerGainFallsOffWithSpeed()
        {
            var s = Settings();

            Assert.AreEqual(s.steerGainLowSpeed, AiDriving.SteerGain(0f, s), 0.001f);
            Assert.AreEqual(s.steerGainHighSpeed, AiDriving.SteerGain(s.straightSpeedKph, s), 0.001f);
            Assert.AreEqual(s.steerGainHighSpeed, AiDriving.SteerGain(s.straightSpeedKph * 2f, s), 0.001f);
        }

        [Test]
        public void GridLaneBlendFadesOverDistanceNotTime()
        {
            var s = Settings();

            Assert.AreEqual(1f, AiDriving.GridLaneBlend(0f, s), 0.001f);
            Assert.AreEqual(0.5f, AiDriving.GridLaneBlend(s.laneMergeMetres * 0.5f, s), 0.001f);
            Assert.AreEqual(0f, AiDriving.GridLaneBlend(s.laneMergeMetres, s), 0.001f);
            Assert.AreEqual(0f, AiDriving.GridLaneBlend(s.laneMergeMetres * 10f, s), 0.001f);
        }

        [Test]
        public void LaneSpreadStaysOnTheStartingSideAndWithinRange()
        {
            var s = Settings();

            for (int raceIndex = 0; raceIndex < 8; raceIndex++)
            {
                float right = AiDriving.LaneSpreadMetres(raceIndex, 1f, s);
                float left = AiDriving.LaneSpreadMetres(raceIndex, -1f, s);

                Assert.Greater(right, 0f, "a car starting right must stay right of the line");
                Assert.Less(left, 0f, "a car starting left must stay left of the line");
                Assert.LessOrEqual(Mathf.Abs(right), s.laneSpreadMetres + 0.0001f);
                Assert.AreEqual(right, -left, 0.001f);
            }
        }

        [Test]
        public void LaneSpreadIsReproducibleAndSpreadsNeighbours()
        {
            var s = Settings();

            Assert.AreEqual(
                AiDriving.LaneSpreadMetres(3, 1f, s),
                AiDriving.LaneSpreadMetres(3, 1f, s),
                "same race index must always produce the same lane");
            Assert.AreNotEqual(
                AiDriving.LaneSpreadMetres(1, 1f, s),
                AiDriving.LaneSpreadMetres(2, 1f, s),
                "neighbouring cars must not share a lane");
        }

        [Test]
        public void ClampLateralKeepsTheAimInsideTheMeasuredRoad()
        {
            Assert.AreEqual(3f, AiDriving.ClampLateral(3f, 5f, 5f), 0.001f);
            Assert.AreEqual(5f, AiDriving.ClampLateral(9f, 5f, 5f), 0.001f);
            Assert.AreEqual(-2f, AiDriving.ClampLateral(-9f, 2f, 5f), 0.001f);
            Assert.AreEqual(0f, AiDriving.ClampLateral(4f, 0f, 0f), 0.001f);
        }

        [Test]
        public void SpeedInputsBrakeOnlyWhenTooFast()
        {
            AiDriving.SpeedInputs(100f, 60f, out float throttle, out float brake);
            Assert.AreEqual(1f, throttle, 0.001f);
            Assert.AreEqual(0f, brake, 0.001f);

            AiDriving.SpeedInputs(40f, 100f, out throttle, out brake);
            Assert.AreEqual(0f, throttle, 0.001f);
            Assert.Greater(brake, 0f);

            AiDriving.SpeedInputs(60f, 58f, out throttle, out brake);
            Assert.Greater(throttle, 0f);
            Assert.AreEqual(0f, brake, 0.001f, "a small overshoot must not trigger the brakes");
        }
    }
}
