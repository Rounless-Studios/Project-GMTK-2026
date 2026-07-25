using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Gmtk2026.GameBalance
{
    // ---- policy enums (choosable in a preset without changing code branches) ----

    public enum CurseSelectionMode { OrderedRotation, Random, PlayerChoice }
    public enum CurseTargetingMode { NearestAheadThenNearestActive, NearestActive, CurrentLeader }
    public enum OvertakeCooldownRewardMode { Reset, Reduce }
    public enum AIArchetype { Rammer, Speedster, Schemer, Survivor }

    // ---- settings groups (defaults = the GameJamDefault preset values) ----

    [System.Serializable]
    public class RaceSettings
    {
        [Min(0)] public int aiCount = 5;
        [Min(1)] public int lapCount = 1;
        [Min(1)] public int finalDuelRacerCount = 2;
        [Min(0)] public float targetRaceDurationMinSeconds = 180f;
        [Min(0)] public float targetRaceDurationMaxSeconds = 300f;

        // Player count is a structural invariant of single-player, not a tunable.
        public int TotalRacerCount => 1 + aiCount;
    }

    [System.Serializable]
    public class EliminationSettings
    {
        [Min(0)] public float intervalSeconds = 30f;
        [Min(0)] public float warningSeconds = 10f;
        [Min(0)] public float intenseWarningSeconds = 5f;
        [Min(0)] public float executionCameraLeadSeconds = 3f;
        [Min(0)] public float executeAtRemainingSeconds = 0f;
    }

    [System.Serializable]
    public class ResultSettings
    {
        [Min(0)] public float playerDeathCinematicMaxSeconds = 3f;
        [Min(0)] public float restartToControlMaxSeconds = 5f;
    }

    [System.Serializable]
    public class QuizSettings
    {
        // GDD is inconsistent (3 vs 4); 4 chosen as the default, confirm before playtest.
        [Min(0)] public float answerTimeSeconds = 4f;
        [Min(1)] public int minimumAnswerCount = 2;
        [Min(1)] public int maximumAnswerCount = 3;
    }

    [System.Serializable]
    public class CurseSettings
    {
        [Min(0)] public float sharedCooldownSeconds = 12f;
        public CurseSelectionMode selectionMode = CurseSelectionMode.OrderedRotation;
        public CurseTargetingMode targetingMode = CurseTargetingMode.NearestAheadThenNearestActive;

        [Header("Rupture")]
        [Min(0)] public float ruptureDurabilityDamage = 35f;
        [Min(0)] public float ruptureKnockbackForce = 8f; // TBD by playtest

        [Header("Engine Seal")]
        [Min(0)] public float engineSealDurationSeconds = 5f;

        [Header("Soul Swap")]
        [Min(0)] public float soulSwapMinimumDistanceMeters = 15f;
        [Min(0)] public float soulSwapMaximumDistanceMeters = 40f;
        [Min(0)] public float soulSwapCollisionIgnoreSeconds = 0.3f;

        [Header("AI Quiz Ability")]
        [Range(0f, 1f)] public float defaultAiQuizSuccessChance = 0.5f;
        [Range(0f, 1f)] public float cleanRacerQuizSuccessChance = 0.8f;
        [Range(0f, 1f)] public float rammerQuizSuccessChance = 0.35f;
        [Range(0f, 1f)] public float blockerQuizSuccessChance = 0.65f;
        [Range(0f, 1f)] public float recklessQuizSuccessChance = 0.3f;

        [Header("AI Casting")]
        [Min(0f)] public float aiInitialCastDelayMinimumSeconds = 2f;
        [Min(0f)] public float aiInitialCastDelayMaximumSeconds = 5f;
        [Min(0.1f)] public float aiFailedCastRetrySeconds = 1f;
    }

    [System.Serializable]
    public class BoostSettings
    {
        [Min(0)] public int maximumCharges = 2;
        [Min(0)] public float durationSeconds = 1.25f;
        [Min(0)] public float rechargeSecondsPerCharge = 6f;
        [Min(0)] public int overtakeRewardCharges = 1;
        [Min(1)] public float boostSpeedMultiplier = 1.5f;
    }

    [System.Serializable]
    public class DamageSettings
    {
        [Min(0)] public float maximumDurability = 100f;
        [Min(0)] public float damagedThreshold = 60f;
        [Min(0)] public float criticalThreshold = 30f;
        [Min(0)] public float wreckedThreshold = 0f;
        [Min(0)] public float wreckDurationSeconds = 2f;
        [Min(0)] public float recoveryDurability = 50f;
        [Min(0)] public float recoveryProtectionSeconds = 2f; // TBD by playtest
        // a collision only hurts above this relative impulse; light bumps are free
        [Min(0)] public float strongCollisionImpulse = 8f;    // TBD by playtest
        [Min(0)] public float strongCollisionDamage = 15f;    // TBD by playtest
    }

    [System.Serializable]
    public class VehicleRecoverySettings
    {
        [Tooltip("Vertical distance below the last supported track position that triggers a respawn.")]
        [Min(0.1f)] public float fallDistanceBelowTrack = 8f;
        [Tooltip("Vertical clearance above the waypoint when placing the car back on track.")]
        [Min(0f)] public float respawnHeightAboveWaypoint = 2f;
        [Tooltip("Largest horizontal waypoint distance still considered part of the track.")]
        [Min(1f)] public float maximumTrackDistanceFromWaypoint = 20f;
        [Tooltip("Downward ray length used to confirm static track geometry below the car.")]
        [Min(0.1f)] public float groundProbeDistance = 5f;
        [Tooltip("How often each car refreshes its last supported track position.")]
        [Min(0.02f)] public float trackSampleIntervalSeconds = 0.1f;
    }

    [System.Serializable]
    public class OvertakeSettings
    {
        [Min(0)] public float checkIntervalSeconds = 20f;
        [Min(0)] public float challengeDurationSeconds = 8f;
        [Min(0)] public float requiredLeadHoldSeconds = 0.5f;
        public OvertakeCooldownRewardMode cooldownRewardMode = OvertakeCooldownRewardMode.Reset;
        [Min(0)] public float cooldownReductionSeconds = 6f; // only used in Reduce mode
        // failure penalty: "referee's chains" — a hard slowdown for a short time
        [Min(0)] public float bindDurationSeconds = 5f;       // TBD by playtest
        [Range(0f, 1f)] public float bindSpeedMultiplier = 0.5f; // TBD by playtest
    }

    [System.Serializable]
    public class CameraSettings
    {
        [Min(0)] public float sideBySideActivationSeconds = 0.6f;
        // Execution CCTV inset; the player's camera remains in its original main viewport.
        [FormerlySerializedAs("executionPlayerViewportRect")]
        public Rect executionCctvViewportRect = new Rect(0.66f, 0f, 0.34f, 0.34f);
        [Min(0)] public float executionTransitionSeconds = 0.2f;
        [Min(0)] public float executionReturnSeconds = 0.25f;
    }

    [System.Serializable]
    public class AIArchetypeProfile
    {
        public AIArchetype archetype = AIArchetype.Speedster;
        [Range(0.5f, 1.5f)] public float speedMultiplier = 1f;
        [Range(0f, 1f)] public float aggression = 0.3f;
        [Range(0f, 1f)] public float avoidance = 0.7f;
        [Range(0f, 1f)] public float boostTendency = 0.5f;
        [Range(0f, 1f)] public float curseTendency = 0.3f;
        [Min(0)] public float curseCooldownSeconds = 12f;
        [Min(0)] public float curseWarningSeconds = 1.5f;
        [Min(0)] public float catchupAcceleration = 0.1f;
    }

    [System.Serializable]
    public class AISettings
    {
        // one archetype per AI car (length should match RaceSettings.aiCount)
        public List<AIArchetype> archetypeAssignments = new()
        {
            AIArchetype.Rammer, AIArchetype.Speedster, AIArchetype.Schemer,
            AIArchetype.Survivor, AIArchetype.Speedster
        };
        public AiDrivingSettings driving = new();
        public AiPersonalityAssignmentSettings personalityAssignment = new();
        public List<AIArchetypeProfile> profiles = new()
        {
            new AIArchetypeProfile { archetype = AIArchetype.Rammer, aggression = 0.9f, avoidance = 0.3f },
            new AIArchetypeProfile { archetype = AIArchetype.Speedster, speedMultiplier = 1.1f, boostTendency = 0.8f },
            new AIArchetypeProfile { archetype = AIArchetype.Schemer, curseTendency = 0.9f },
            new AIArchetypeProfile { archetype = AIArchetype.Survivor, avoidance = 0.95f, aggression = 0.1f },
        };

        public AIArchetypeProfile GetProfile(AIArchetype archetype)
        {
            foreach (var p in profiles)
                if (p != null && p.archetype == archetype) return p;
            return null;
        }
    }

    [System.Serializable]
    public class AiDrivingSettings
    {
        [Header("Look ahead")]
        [Tooltip("Aim distance at a standstill; speed adds to it so corners are seen early.")]
        [Min(1)] public float minLookAheadMetres = 14f;
        [Min(0)] public float lookAheadMetresPerKph = 0.32f;

        [Header("Corner speed")]
        [Tooltip("Shortest path length scanned for a corner; slow cars do not look further than this.")]
        [Min(1)] public float minCornerScanMetres = 45f;
        [Tooltip("Braking the AI assumes (m/s^2) when deciding how far ahead to scan for corners. The " +
                 "scan covers the braking distance, so fast cars start slowing early enough.")]
        [Min(1)] public float cornerBrakingDecel = 8f;
        [Tooltip("How fast the corner speed target may rise again (kph/s). Prevents brake-release " +
                 "hunting as a corner enters and leaves the speed-dependent scan window.")]
        [Min(1)] public float targetSpeedRiseKphPerSecond = 50f;
        [Min(1)] public float straightSpeedKph = 160f;
        [Tooltip("Slowest an AI will take any corner.")]
        [Min(1)] public float minCornerSpeedKph = 55f;
        [Tooltip("Sideways grip the AI assumes (m/s^2). Higher takes corners faster: corner speed is " +
                 "sqrt(grip x radius), so a wide sweeper stays fast while a hairpin still slows.")]
        [Min(1)] public float cornerGrip = 14f;

        [Header("Steering")]
        [Min(0)] public float steerGainLowSpeed = 1.5f;
        [Min(0)] public float steerGainHighSpeed = 0.55f;
        [Tooltip("Damps the steering rate so the car stops sawing at the wheel.")]
        [Min(0)] public float steerDamping = 0.06f;

        [Header("Racing line")]
        [Tooltip("Metres the aim point moves toward the inside of a corner at the apex.")]
        [Min(0)] public float apexOffsetMetres = 4.5f;
        [Tooltip("Metres the aim point moves to the outside while a sharp corner is still ahead.")]
        [Min(0)] public float entryOffsetMetres = 3.5f;
        [Min(1)] public float racingLineReferenceDegrees = 30f;

        [Header("Grid lane")]
        [Tooltip("Metres driven while merging from the grid lane onto the racing line.")]
        [Min(0)] public float laneMergeMetres = 140f;
        [Min(0)] public float maxLaneOffsetMetres = 6f;
        [Tooltip("Per-car offset kept after merging so the pack does not share one line.")]
        [Min(0)] public float laneSpreadMetres = 1.2f;

        [Header("Road probing")]
        [Min(1)] public float maxRoadHalfWidthMetres = 13f;
        [Min(0)] public float roadEdgeMarginMetres = 2.2f;

        [Header("Diagnostics")]
        [Tooltip("Logs one AI car's speed target versus what the car actually does, once a second.")]
        public bool logDriveTelemetry = false;

        [Header("Waypoints and recovery")]
        [Min(1)] public float waypointReachMetres = 9f;
        [Tooltip("Largest sideways nudge the ram/block personality may add to the racing line.")]
        [Min(0)] public float maxPersonalityLateralMetres = 3f;
        [Min(0)] public float stuckSpeedKph = 1.5f;
        [Min(0)] public float stuckDelaySeconds = 2.5f;
        [Min(0)] public float reverseDurationSeconds = 1.25f;
    }

    [System.Serializable]
    public class AiPersonalityAssignmentSettings
    {
        [Tooltip("0 shuffles the personalities freshly every race; any other value reproduces one mix.")]
        public int shuffleSeed = 0;
        [Tooltip("Logs the seed and the resulting grid so an odd race can be reproduced.")]
        public bool logAssignment = true;

        [Header("Aggression")]
        [Min(0)] public float ramStrengthMetres = 7f;
        [Min(0)] public float blockStrengthMetres = 5f;
        [Min(0)] public float aggroRangeMetres = 45f;
    }

    [System.Serializable]
    public class TrackValidationSettings
    {
        [Min(0)] public int minimumBoostStraights = 2;
        [Min(0)] public int minimumParallelSections = 1;
    }

    [System.Serializable]
    public class PresentationSettings
    {
        [Header("Final Gate")]
        [Min(0)] public float gateCloseSpeed = 2f;
        [Min(0)] public float gateCloseDelaySeconds = 0.2f;
        [Min(0)] public float gateExecutionDelaySeconds = 0.5f;
        [Header("Explosion / Wreck")]
        [Min(0)] public float explosionVfxDurationSeconds = 0.45f;
        [Min(0)] public float wreckLingerSeconds = 2.5f;
        [Header("Audio")]
        [Range(0f, 1f)] public float masterSfxVolume = 1f;
        [Min(0)] public float warningAudioFadeSeconds = 0.25f;
    }

    [System.Serializable]
    public class PlaytestSettings
    {
        [Min(0)] public float ruleUnderstandingTargetSeconds = 15f;
        [Min(0)] public int minimumExternalTesterCount = 3;
    }

    /// <summary>
    /// Single root of every runtime/balance/presentation number in the game. Documented
    /// GDD numbers live here as preset defaults, never as code literals. Concrete presets
    /// (GameJamDefault, FastTest) are .asset instances; systems read an immutable snapshot
    /// supplied by <see cref="GameBalance"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "GameBalanceSettings", menuName = "GMTK/Game Balance Settings")]
    public class GameBalanceSettings : ScriptableObject
    {
        public RaceSettings race = new();
        public EliminationSettings elimination = new();
        public ResultSettings result = new();
        public QuizSettings quiz = new();
        public CurseSettings curse = new();
        public BoostSettings boost = new();
        public DamageSettings damage = new();
        public VehicleRecoverySettings vehicleRecovery = new();
        public OvertakeSettings overtake = new();
        public CameraSettings camera = new();
        public AISettings ai = new();
        public TrackValidationSettings trackValidation = new();
        public PresentationSettings presentation = new();
        public PlaytestSettings playtest = new();

        /// <summary>Returns a list of specific validation errors; empty when valid.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (race.aiCount < 0) errors.Add("race.aiCount must be >= 0");
            if (race.lapCount < 1) errors.Add("race.lapCount must be >= 1");
            if (race.finalDuelRacerCount < 1) errors.Add("race.finalDuelRacerCount must be >= 1");
            if (race.finalDuelRacerCount >= race.TotalRacerCount)
                errors.Add($"race.finalDuelRacerCount ({race.finalDuelRacerCount}) must be < total racers ({race.TotalRacerCount})");
            if (race.targetRaceDurationMinSeconds > race.targetRaceDurationMaxSeconds)
                errors.Add("race.targetRaceDurationMinSeconds must be <= targetRaceDurationMaxSeconds");

            // all elimination warnings must fit inside the interval
            if (elimination.warningSeconds >= elimination.intervalSeconds)
                errors.Add("elimination.warningSeconds must be < elimination.intervalSeconds");
            if (elimination.intenseWarningSeconds >= elimination.intervalSeconds)
                errors.Add("elimination.intenseWarningSeconds must be < elimination.intervalSeconds");
            if (elimination.executionCameraLeadSeconds >= elimination.intervalSeconds)
                errors.Add("elimination.executionCameraLeadSeconds must be < elimination.intervalSeconds");
            if (elimination.intenseWarningSeconds > elimination.warningSeconds)
                errors.Add("elimination.intenseWarningSeconds should be <= elimination.warningSeconds");

            if (quiz.minimumAnswerCount > quiz.maximumAnswerCount)
                errors.Add("quiz.minimumAnswerCount must be <= quiz.maximumAnswerCount");

            // damage stages must strictly descend
            if (!(damage.maximumDurability > damage.damagedThreshold &&
                  damage.damagedThreshold > damage.criticalThreshold &&
                  damage.criticalThreshold > damage.wreckedThreshold))
                errors.Add("damage thresholds must satisfy maximumDurability > damagedThreshold > criticalThreshold > wreckedThreshold");
            if (damage.recoveryDurability > damage.maximumDurability)
                errors.Add("damage.recoveryDurability must be <= maximumDurability");

            if (vehicleRecovery.fallDistanceBelowTrack <= 0f)
                errors.Add("vehicleRecovery.fallDistanceBelowTrack must be > 0");
            if (vehicleRecovery.maximumTrackDistanceFromWaypoint <= 0f)
                errors.Add("vehicleRecovery.maximumTrackDistanceFromWaypoint must be > 0");
            if (vehicleRecovery.groundProbeDistance <= 0f)
                errors.Add("vehicleRecovery.groundProbeDistance must be > 0");
            if (vehicleRecovery.trackSampleIntervalSeconds <= 0f)
                errors.Add("vehicleRecovery.trackSampleIntervalSeconds must be > 0");

            if (curse.soulSwapMinimumDistanceMeters > curse.soulSwapMaximumDistanceMeters)
                errors.Add("curse.soulSwapMinimumDistanceMeters must be <= soulSwapMaximumDistanceMeters");
            if (curse.aiInitialCastDelayMinimumSeconds > curse.aiInitialCastDelayMaximumSeconds)
                errors.Add("curse.aiInitialCastDelayMinimumSeconds must be <= aiInitialCastDelayMaximumSeconds");
            if (curse.aiFailedCastRetrySeconds < 0.1f)
                errors.Add("curse.aiFailedCastRetrySeconds must be >= 0.1");

            if (boost.overtakeRewardCharges > boost.maximumCharges)
                errors.Add("boost.overtakeRewardCharges must be <= boost.maximumCharges");

            var r = camera.executionCctvViewportRect;
            if (r.width <= 0f || r.height <= 0f ||
                r.xMin < 0f || r.yMin < 0f || r.xMax > 1f || r.yMax > 1f)
            {
                errors.Add("camera.executionCctvViewportRect must have positive size and remain within the 0..1 viewport");
            }

            // non-negative sanity for the common time/charge fields
            if (boost.durationSeconds < 0f) errors.Add("boost.durationSeconds must be >= 0");
            if (boost.rechargeSecondsPerCharge < 0f) errors.Add("boost.rechargeSecondsPerCharge must be >= 0");
            if (curse.sharedCooldownSeconds < 0f) errors.Add("curse.sharedCooldownSeconds must be >= 0");
            if (overtake.checkIntervalSeconds < 0f) errors.Add("overtake.checkIntervalSeconds must be >= 0");

            return errors;
        }

        private void OnValidate()
        {
            var errors = Validate();
            if (errors.Count > 0)
                Debug.LogWarning($"[{name}] GameBalanceSettings has {errors.Count} issue(s):\n - {string.Join("\n - ", errors)}", this);
        }
    }
}
