using Gmtk2026.GameBalance;
using NUnit.Framework;
using UnityEngine;

namespace Gmtk2026.GameBalance.Tests
{
    public class FallRespawnStateTests
    {
        [Test]
        public void ACarWithNothingUnderItIsRecoveredEvenWithoutFalling()
        {
            var state = new FallRespawnState();
            state.RecordTrackPosition(new Vector3(0f, 10f, 0f));

            // launched off the map: still above its last safe height, so the drop rule never fires
            var flying = new Vector3(60f, 40f, 0f);
            Assert.IsFalse(state.ShouldRespawn(flying, 8f, 3f));

            state.TickAirborne(1.5f);
            Assert.IsFalse(state.ShouldRespawn(flying, 8f, 3f));

            state.TickAirborne(1.5f);
            Assert.IsTrue(state.ShouldRespawn(flying, 8f, 3f),
                "three seconds with no track under the car has to end the flight");
        }

        [Test]
        public void TouchingTrackAgainClearsTheAirborneTimer()
        {
            var state = new FallRespawnState();
            state.RecordTrackPosition(Vector3.zero);
            state.TickAirborne(2.9f);

            state.RecordTrackPosition(new Vector3(0f, 0.2f, 5f));

            Assert.AreEqual(0f, state.AirborneSeconds);
            Assert.IsFalse(state.ShouldRespawn(new Vector3(0f, 0.2f, 5f), 8f, 3f));
        }

        [Test]
        public void TheAirborneRuleCanBeTurnedOff()
        {
            var state = new FallRespawnState();
            state.RecordTrackPosition(Vector3.zero);
            state.TickAirborne(30f);

            Assert.IsFalse(state.ShouldRespawn(new Vector3(0f, 5f, 0f), 8f, 0f));
        }

        [Test]
        public void DoesNotRespawnBeforeATrackPositionIsRecorded()
        {
            var state = new FallRespawnState();

            Assert.IsFalse(state.ShouldRespawn(new Vector3(0f, -100f, 0f), 8f));
        }

        [Test]
        public void RespawnsOnlyAfterFallingPastTheConfiguredDistance()
        {
            var state = new FallRespawnState();
            state.RecordTrackPosition(new Vector3(10f, 4f, 20f));

            Assert.IsFalse(state.ShouldRespawn(new Vector3(10f, -4f, 20f), 8f));
            Assert.IsTrue(state.ShouldRespawn(new Vector3(10f, -4.01f, 20f), 8f));
        }

        [Test]
        public void NewTrackSamplesReplaceThePreviousSafePosition()
        {
            var state = new FallRespawnState();
            state.RecordTrackPosition(new Vector3(1f, 2f, 3f));
            state.RecordTrackPosition(new Vector3(4f, 5f, 6f));

            Assert.AreEqual(new Vector3(4f, 5f, 6f), state.LastTrackPosition);
        }

        [Test]
        public void NonPositiveFallDistanceDisablesRespawning()
        {
            var state = new FallRespawnState();
            state.RecordTrackPosition(Vector3.zero);

            Assert.IsFalse(state.ShouldRespawn(new Vector3(0f, -100f, 0f), 0f));
        }
    }
}
