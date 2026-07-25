using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Gmtk2026.GameBalance;

namespace Gmtk2026.GameBalance.Tests
{
    public class GameBalanceSettingsTests
    {
        private GameBalanceSettings NewDefault() => ScriptableObject.CreateInstance<GameBalanceSettings>();

        [Test]
        public void DefaultSettings_AreValid()
        {
            var s = NewDefault();
            List<string> errors = s.Validate();
            Assert.IsEmpty(errors, "Default settings should be valid but got:\n" + string.Join("\n", errors));
            Object.DestroyImmediate(s);
        }

        [Test]
        public void GddDefaults_MatchCatalog()
        {
            var s = NewDefault();
            Assert.AreEqual(5, s.race.aiCount);
            Assert.AreEqual(1, s.race.lapCount);
            Assert.AreEqual(6, s.race.TotalRacerCount, "total = 1 + aiCount");
            Assert.AreEqual(2, s.race.finalDuelRacerCount);
            Assert.AreEqual(30f, s.elimination.intervalSeconds);
            Assert.AreEqual(10f, s.elimination.warningSeconds);
            Assert.AreEqual(5f, s.elimination.intenseWarningSeconds);
            Assert.AreEqual(12f, s.curse.sharedCooldownSeconds);
            Assert.AreEqual(2, s.boost.maximumCharges);
            Assert.AreEqual(1.25f, s.boost.durationSeconds);
            Assert.AreEqual(6f, s.boost.rechargeSecondsPerCharge);
            Assert.AreEqual(100f, s.damage.maximumDurability);
            Assert.AreEqual(20f, s.overtake.checkIntervalSeconds - 0f); // 20
            Assert.AreEqual(20f, s.overtake.checkIntervalSeconds);
            Object.DestroyImmediate(s);
        }

        [Test]
        public void FinalDuelCount_NotLessThanTotal_IsInvalid()
        {
            var s = NewDefault();
            s.race.finalDuelRacerCount = s.race.TotalRacerCount; // == total, must be <
            CollectionAssert.IsNotEmpty(s.Validate());
            Object.DestroyImmediate(s);
        }

        [Test]
        public void DamageThresholds_OutOfOrder_IsInvalid()
        {
            var s = NewDefault();
            s.damage.criticalThreshold = 80f; // now critical > damaged(60): wrong order
            CollectionAssert.IsNotEmpty(s.Validate());
            Object.DestroyImmediate(s);
        }

        [Test]
        public void VehicleRecovery_NonPositiveFallDistance_IsInvalid()
        {
            var s = NewDefault();
            s.vehicleRecovery.fallDistanceBelowTrack = 0f;
            CollectionAssert.IsNotEmpty(s.Validate());
            Object.DestroyImmediate(s);
        }

        [Test]
        public void AnswerCount_MinGreaterThanMax_IsInvalid()
        {
            var s = NewDefault();
            s.quiz.minimumAnswerCount = 5;
            s.quiz.maximumAnswerCount = 2;
            CollectionAssert.IsNotEmpty(s.Validate());
            Object.DestroyImmediate(s);
        }

        [Test]
        public void EliminationWarning_NotLessThanInterval_IsInvalid()
        {
            var s = NewDefault();
            s.elimination.warningSeconds = s.elimination.intervalSeconds; // must be <
            CollectionAssert.IsNotEmpty(s.Validate());
            Object.DestroyImmediate(s);
        }

        [Test]
        public void SoulSwapDistance_MinGreaterThanMax_IsInvalid()
        {
            var s = NewDefault();
            s.curse.soulSwapMinimumDistanceMeters = 50f;
            s.curse.soulSwapMaximumDistanceMeters = 40f;
            CollectionAssert.IsNotEmpty(s.Validate());
            Object.DestroyImmediate(s);
        }

        [Test]
        public void BeginRace_Snapshot_IsIndependentOfActive()
        {
            var src = NewDefault();
            src.elimination.intervalSeconds = 30f;
            GameBalance.SetActive(src);

            var snap = GameBalance.BeginRace();
            Assert.IsNotNull(snap);
            Assert.AreNotSame(src, snap, "snapshot must be a distinct clone");

            // mutating the source after the snapshot must not affect the snapshot
            src.elimination.intervalSeconds = 5f;
            Assert.AreEqual(30f, snap.elimination.intervalSeconds, "snapshot must be immutable to source edits");

            GameBalance.EndRace();
            GameBalance.ResetForTests();
            Object.DestroyImmediate(src);
        }

        [Test]
        public void GameJamDefaultPreset_LoadsAndValidates()
        {
            GameBalance.ResetForTests();
            var preset = GameBalance.Load("GameJamDefault");
            Assert.IsNotNull(preset, "GameJamDefault preset should exist in Resources/GameBalance");
            Assert.IsEmpty(preset.Validate());
            GameBalance.ResetForTests();
        }
    }
}
