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

            // 20 degrees over 50 m is a ~143 m radius: an arcade car takes that flat out
            Assert.AreEqual(
                s.straightSpeedKph,
                AiDriving.CornerSpeedKph(20f, ScanAt(s, 100f), 1f, s),
                0.001f,
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
