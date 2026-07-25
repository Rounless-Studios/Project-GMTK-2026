using Gmtk2026.GameBalance;
using NUnit.Framework;
using UnityEngine;

namespace Gmtk2026.GameBalance.Tests
{
    public class FallRespawnStateTests
    {
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
