using Gmtk2026.GameBalance;
using NUnit.Framework;
using UnityEngine;

namespace Gmtk2026.GameBalance.Tests
{
    public class WreckLaunchTests
    {
        private static DamageSettings Settings() => new()
        {
            wreckImpactSpeedMultiplier = 2f,
            wreckMinimumLaunchSpeed = 7f,
            wreckMaximumLaunchSpeed = 18f,
            wreckUpwardLaunchRatio = 0.4f,
        };

        [Test]
        public void SlowImpact_StillReachesTheMinimumLaunch()
        {
            var s = Settings();

            Vector3 launch = WreckLaunch.Compute(s, Vector3.forward, 1f); // 1 * 2 < 7

            Assert.AreEqual(s.wreckMinimumLaunchSpeed, launch.magnitude, 0.001f);
        }

        [Test]
        public void ImpactSpeed_IsScaledUpBetweenTheLimits()
        {
            var s = Settings();

            Vector3 launch = WreckLaunch.Compute(s, Vector3.forward, 6f); // 6 * 2 = 12

            Assert.AreEqual(12f, launch.magnitude, 0.001f);
        }

        [Test]
        public void HugeImpact_IsCappedSoTheCarIsNotFiredOffTheMap()
        {
            var s = Settings();

            Vector3 launch = WreckLaunch.Compute(s, Vector3.forward, 40f); // 40 * 2 = 80

            Assert.AreEqual(s.wreckMaximumLaunchSpeed, launch.magnitude, 0.001f);
        }

        [Test]
        public void Launch_KeepsThePushDirectionAndTiltsItUp()
        {
            var s = Settings();

            Vector3 launch = WreckLaunch.Compute(s, Vector3.forward, 6f);

            Assert.Greater(launch.z, 0f, "the car is pushed away from what it hit");
            Assert.Greater(launch.y, 0f, "and lifted so it tumbles");
            Assert.Greater(launch.z, launch.y, "a 0.4 ratio stays well below 45 degrees");
        }

        [Test]
        public void NoCollisionBehindTheWreck_LaunchesStraightUp()
        {
            var s = Settings();

            Vector3 launch = WreckLaunch.Compute(s, Vector3.zero, 0f);

            Assert.AreEqual(s.wreckMinimumLaunchSpeed, launch.magnitude, 0.001f);
            Assert.AreEqual(s.wreckMinimumLaunchSpeed, launch.y, 0.001f);
        }

        [Test]
        public void PushStraightDown_FallsBackToUpInsteadOfCancellingOut()
        {
            var s = Settings();
            s.wreckUpwardLaunchRatio = 1f; // exactly cancels a downward push

            Vector3 direction = WreckLaunch.Direction(Vector3.down, s.wreckUpwardLaunchRatio);

            Assert.AreEqual(Vector3.up, direction);
        }

        [Test]
        public void MinimumAboveMaximum_ClampsToTheMinimumInsteadOfThrowingNothing()
        {
            var s = Settings();
            s.wreckMinimumLaunchSpeed = 20f;
            s.wreckMaximumLaunchSpeed = 5f; // misconfigured preset

            Vector3 launch = WreckLaunch.Compute(s, Vector3.forward, 6f);

            Assert.AreEqual(20f, launch.magnitude, 0.001f);
        }
    }
}
