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
        public void AiCastDelay_MinGreaterThanMax_IsInvalid()
        {
            var s = NewDefault();
            s.curse.aiInitialCastDelayMinimumSeconds = 5f;
            s.curse.aiInitialCastDelayMaximumSeconds = 2f;
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

        [Test]
        public void FastTestPreset_LoadsAndValidates()
        {
            GameBalance.ResetForTests();
            var preset = GameBalance.Load("FastTest");
            Assert.IsNotNull(preset, "FastTest preset should exist in Resources/GameBalance");
            Assert.IsEmpty(preset.Validate());
            GameBalance.ResetForTests();
        }

        /// <summary>
        /// The code defaults above can be correct while the shipped asset has drifted, so the
        /// asset itself is pinned to the catalog values that the rules depend on.
        /// The shipped asset is pinned too, because temporary debug overrides must not escape.
        /// </summary>
        [Test]
        public void GameJamDefaultAsset_MatchesGddCatalog()
        {
            GameBalance.ResetForTests();
            var preset = GameBalance.Load("GameJamDefault");
            Assert.IsNotNull(preset);
            Assert.AreEqual(5, preset.race.aiCount);
            Assert.AreEqual(1, preset.race.lapCount);
            Assert.AreEqual(30f, preset.elimination.intervalSeconds,
                "GDD: a steady 30s elimination beat");
            Assert.AreEqual(4f, preset.quiz.answerTimeSeconds);
            Assert.AreEqual(12f, preset.curse.sharedCooldownSeconds);
            Assert.AreEqual(1000000f, preset.damage.maximumDurability,
                "GameJamDefault intentionally suppresses wrecks during active playtesting");
            Assert.AreEqual(0.75f, preset.presentation.gateCloseDurationSeconds);
            Assert.AreEqual(0.25f, preset.presentation.gateExecutionDelaySeconds);
            GameBalance.ResetForTests();
        }

        [Test]
        public void SpecialEventInterval_MinGreaterThanMax_IsInvalid()
        {
            var s = NewDefault();
            s.specialEvents.minimumIntervalSeconds = 41f;
            s.specialEvents.maximumIntervalSeconds = 25f;
            CollectionAssert.IsNotEmpty(s.Validate());
            Object.DestroyImmediate(s);
        }

        [Test]
        public void SpecialEventLimits_MustBePositive()
        {
            var s = NewDefault();
            s.specialEvents.maximumSimultaneousEvents = 0;
            s.specialEvents.maximumEventsPerRace = 0;
            Assert.That(s.Validate(), Has.Some.Contains("maximumSimultaneousEvents"));
            Assert.That(s.Validate(), Has.Some.Contains("maximumEventsPerRace"));
            Object.DestroyImmediate(s);
        }

        [Test]
        public void FastTestAsset_KeepsShortTimers()
        {
            GameBalance.ResetForTests();
            var preset = GameBalance.Load("FastTest");
            Assert.IsNotNull(preset);
            Assert.Less(preset.elimination.intervalSeconds, 30f,
                "the fast preset exists so automated tests need not wait a full beat");
            GameBalance.ResetForTests();
        }

        [Test]
        public void FinalGateDefaults_MatchGddCatalog()
        {
            var s = NewDefault();
            Assert.AreEqual(0.75f, s.presentation.gateCloseDurationSeconds);
            Assert.AreEqual(0.25f, s.presentation.gateExecutionDelaySeconds);
            Assert.Greater(s.presentation.gateOpenDurationSeconds, 0f);
            Assert.Greater(s.presentation.gateWidthMeters, 0f);
            Assert.Greater(s.presentation.gateHeightMeters, 0f);
            Object.DestroyImmediate(s);
        }

        [Test]
        public void GateWidth_NonPositive_IsInvalid()
        {
            var s = NewDefault();
            s.presentation.gateWidthMeters = 0f;
            CollectionAssert.IsNotEmpty(s.Validate());
            Object.DestroyImmediate(s);
        }

        // ---- AI personalities (confirmed axis, 2026-07-26) ----

        [Test]
        public void ConfirmedGridComposition_IsTheDefault()
        {
            var s = NewDefault();
            var counts = new Dictionary<AIPersonalityType, int>();
            foreach (var personality in s.ai.personalityAssignments)
            {
                counts.TryGetValue(personality, out int seen);
                counts[personality] = seen + 1;
            }

            Assert.AreEqual(s.race.aiCount, s.ai.personalityAssignments.Count,
                "one entry per AI car");
            Assert.AreEqual(2, counts[AIPersonalityType.Reckless], "폭주광 2대");
            Assert.AreEqual(1, counts[AIPersonalityType.Rammer], "난폭자 1대");
            Assert.AreEqual(1, counts[AIPersonalityType.Blocker], "봉쇄자 1대");
            Assert.AreEqual(1, counts[AIPersonalityType.CleanRacer], "생존자 1대");
            Object.DestroyImmediate(s);
        }

        [Test]
        public void EveryPersonalityHasAProfile()
        {
            var s = NewDefault();
            foreach (AIPersonalityType personality in
                     System.Enum.GetValues(typeof(AIPersonalityType)))
            {
                Assert.IsNotNull(s.ai.GetProfile(personality), $"no profile row for {personality}");
            }
            Object.DestroyImmediate(s);
        }

        [Test]
        public void CurseDefenceRanking_FollowsTheConfirmedRule()
        {
            var s = NewDefault();

            // "빠르고 공격적인 차는 저주에 약하고, 느리고 신중한 차는 잘 막는다"
            float survivor = s.ai.QuizAvoidChanceOf(AIPersonalityType.CleanRacer);
            float blocker = s.ai.QuizAvoidChanceOf(AIPersonalityType.Blocker);
            float rammer = s.ai.QuizAvoidChanceOf(AIPersonalityType.Rammer);
            float reckless = s.ai.QuizAvoidChanceOf(AIPersonalityType.Reckless);

            Assert.Greater(survivor, blocker);
            Assert.Greater(blocker, rammer);
            Assert.Greater(rammer, reckless);
            Object.DestroyImmediate(s);
        }

        [Test]
        public void PaceRanking_PutsRecklessFastestAndBlockerSlowest()
        {
            var s = NewDefault();
            float reckless = s.ai.GetProfile(AIPersonalityType.Reckless).paceScale;
            float rammer = s.ai.GetProfile(AIPersonalityType.Rammer).paceScale;
            float cleanRacer = s.ai.GetProfile(AIPersonalityType.CleanRacer).paceScale;
            float blocker = s.ai.GetProfile(AIPersonalityType.Blocker).paceScale;

            Assert.Greater(reckless, rammer);
            Assert.Greater(rammer, cleanRacer);
            Assert.Greater(cleanRacer, blocker);
            Object.DestroyImmediate(s);
        }

        [Test]
        public void OnlyRamAndBlockPersonalities_LeaveTheRacingLine()
        {
            var s = NewDefault();
            Assert.Greater(s.ai.GetProfile(AIPersonalityType.Rammer).lateralStrengthMetres, 0f);
            Assert.Greater(s.ai.GetProfile(AIPersonalityType.Blocker).lateralStrengthMetres, 0f);
            Assert.AreEqual(0f, s.ai.GetProfile(AIPersonalityType.Reckless).lateralStrengthMetres);
            Assert.AreEqual(0f, s.ai.GetProfile(AIPersonalityType.CleanRacer).lateralStrengthMetres);
            Object.DestroyImmediate(s);
        }

        [Test]
        public void AssignmentCountMismatchingAiCount_IsInvalid()
        {
            var s = NewDefault();
            s.ai.personalityAssignments.RemoveAt(0);
            CollectionAssert.IsNotEmpty(s.Validate(),
                "a grid list that does not cover every AI car must be reported");
            Object.DestroyImmediate(s);
        }

        [Test]
        public void AssignedPersonalityWithoutAProfile_IsInvalid()
        {
            var s = NewDefault();
            s.ai.personalityProfiles.RemoveAll(p => p.personality == AIPersonalityType.Reckless);
            CollectionAssert.IsNotEmpty(s.Validate(),
                "a personality on the grid with no profile row must be reported");
            Object.DestroyImmediate(s);
        }

        [Test]
        public void UnknownPersonality_FallsBackToTheDefaultDefenceChance()
        {
            var s = NewDefault();
            s.ai.personalityProfiles.Clear();
            Assert.AreEqual(
                s.ai.defaultQuizAvoidChance,
                s.ai.QuizAvoidChanceOf(AIPersonalityType.Reckless));
            Object.DestroyImmediate(s);
        }
    }
}
