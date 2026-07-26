using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Gmtk2026.GameBalance
{
    // ---- policy enums (choosable in a preset without changing code branches) ----

    public enum CurseSelectionMode { OrderedRotation, Random, PlayerChoice }
    public enum CurseTargetingMode { NearestAheadThenNearestActive, NearestActive, CurrentLeader }
    public enum OvertakeCooldownRewardMode { Reset, Reduce }
    // AI personalities live in AIPersonalityType.cs; the old AIArchetype axis was dropped
    // on 2026-07-26 in favour of the four types the runtime actually drives.

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
        [Min(1)] public int minimumAnswerCount = 4;
        [Min(1)] public int maximumAnswerCount = 4;
    }

    [System.Serializable]
    public class CurseSettings
    {
        [Min(0)] public float sharedCooldownSeconds = 12f;
        public CurseSelectionMode selectionMode = CurseSelectionMode.OrderedRotation;
        public CurseTargetingMode targetingMode = CurseTargetingMode.NearestAheadThenNearestActive;

        [Header("Rupture")]
        [Min(0)] public float ruptureDurabilityDamage = 60f;  // 30% of the 200 durability range
        [Min(0)] public float ruptureKnockbackForce = 8f; // TBD by playtest

        [Header("Engine Seal")]
        [Min(0)] public float engineSealDurationSeconds = 5f;

        [Header("Soul Swap")]
        [Min(0)] public float soulSwapMinimumDistanceMeters = 15f;
        [Min(0)] public float soulSwapMaximumDistanceMeters = 40f;
        [Min(0)] public float soulSwapCollisionIgnoreSeconds = 0.3f;

        // Per-personality curse defence now lives in AISettings.personalityProfiles
        // (quizAvoidChance), so one personality means one row of numbers.

        [Header("AI Casting")]
        [Min(0f)] public float aiInitialCastDelayMinimumSeconds = 2f;
        [Min(0f)] public float aiInitialCastDelayMaximumSeconds = 5f;
        [Min(0.1f)] public float aiFailedCastRetrySeconds = 1f;
        [Tooltip("Simulated time an AI target takes to answer its invisible defence quiz.")]
        [Min(0f)] public float aiQuizResolutionDelayMinimumSeconds = 1.25f;
        [Min(0f)] public float aiQuizResolutionDelayMaximumSeconds = 3f;
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
        // the visible range is 0-200 (doubled on 2026-07-26 so a car survives longer before it is
        // thrown out of control); the stages sit at 60% and 30% of it
        [Min(0)] public float maximumDurability = 200f;
        [Min(0)] public float damagedThreshold = 120f;
        [Min(0)] public float criticalThreshold = 60f;
        [Min(0)] public float wreckedThreshold = 0f;
        [Min(0)] public float wreckDurationSeconds = 2f;
        [Min(0)] public float recoveryDurability = 100f;
        [Min(0)] public float recoveryProtectionSeconds = 2f; // TBD by playtest
        // a collision only hurts above this relative impulse; light bumps are free
        [Min(0)] public float strongCollisionImpulse = 8f;    // TBD by playtest
        [Min(0)] public float strongCollisionDamage = 25f;    // ~8 strong hits from full

        [Header("Wreck Launch")]
        [Tooltip("A wreck takes the controls away instead of freezing the car: the impact that " +
                 "emptied the durability is handed back scaled by this, so the car is thrown out " +
                 "of control.")]
        [Min(0)] public float wreckImpactSpeedMultiplier = 1.8f;
        [Tooltip("Slowest launch (m/s), so a gentle hit that still empties the durability throws " +
                 "the car too. Also the launch for a wreck with no collision behind it.")]
        [Min(0)] public float wreckMinimumLaunchSpeed = 7f;
        [Tooltip("Fastest launch (m/s); keeps a huge impulse from firing the car off the map.")]
        [Min(0)] public float wreckMaximumLaunchSpeed = 18f;
        [Tooltip("How much of the launch goes upward (1 = 45 degrees). This is what makes the car " +
                 "tumble instead of sliding flat.")]
        [Range(0f, 1f)] public float wreckUpwardLaunchRatio = 0.4f;
        [Tooltip("Spin (rad/s) added on the launch so the wreck rolls out of control.")]
        [Min(0)] public float wreckSpinRadiansPerSecond = 4f;
        [Tooltip("How upright the car must still be when the wreck ends. Below this the recovery " +
                 "rolls it back over, otherwise a car that landed on its roof is stranded.")]
        [Range(-1f, 1f)] public float wreckUprightMinimumUpDot = 0.4f;
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
        [Tooltip("Largest height difference from the nearest waypoint that still counts as being on the track. Without it a hillside below the road counts as support, and the safe position slides downhill with the falling car so the fall is never measured.")]
        [Min(0.5f)] public float maximumTrackHeightDifference = 4f;
        [Tooltip("Seconds with nothing under the car before it is respawned anyway. Catches a car launched off the map, which can stay above its last safe height for a long time.")]
        [Min(0.1f)] public float maximumAirborneSeconds = 3f;
        [Tooltip("Downward ray length used to confirm static track geometry below the car.")]
        [Min(0.1f)] public float groundProbeDistance = 5f;
        [Tooltip("How often each car refreshes its last supported track position.")]
        [Min(0.02f)] public float trackSampleIntervalSeconds = 0.1f;
    }

    [System.Serializable]
    public class VehicleSettings
    {
        [Min(0)] public float boostAcceleration = 18f;
        [Min(0)] public float lateralGripRecovery = 2.2f;
        [Min(0)] public float yawDamping = 1.8f;
        [Min(0)] public float maximumYawRadiansPerSecond = 2.8f;
        [Min(0)] public float bindingDeceleration = 14f;
        [Min(0)] public float stabilizationMinimumSpeedKph = 25f;
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
        public Rect executionCctvViewportRect = new Rect(0.01f, 0.35f, 0.32f, 0.30f);
        [Min(0)] public float executionTransitionSeconds = 0.2f;
        [Min(0)] public float executionReturnSeconds = 0.25f;
    }

    /// <summary>
    /// Everything one AI personality is worth, in one row. Replaces the old AIArchetypeProfile:
    /// the pace scale, the sideways pull and the curse defence chance used to live in three
    /// different places (the driver's switch, AiPersonalityAssignmentSettings, CurseSettings).
    /// </summary>
    [System.Serializable]
    public class AiPersonalityProfile
    {
        public AIPersonalityType personality = AIPersonalityType.CleanRacer;

        [Tooltip("Scales the AI's throttle and corner-speed target. This is the personality's pace.")]
        [Range(0.5f, 1.5f)] public float paceScale = 0.9f;

        [Tooltip("Metres the racing line may be pulled toward the player. Ram pulls straight at " +
                 "them, block pushes sideways into their lane. 0 for personalities that never " +
                 "leave the racing line.")]
        [Min(0)] public float lateralStrengthMetres;

        [Tooltip("Chance the AI spends a charge each decision tick once it is on a straight. 1 means " +
                 "it boosts every straight it has a charge for, which is the current design; lower it " +
                 "to make a personality hoard.")]
        [Range(0f, 1f)] public float boostTendency = 1f;

        [Tooltip("Chance this personality answers the defence quiz correctly and shrugs the curse " +
                 "off. Fast and aggressive personalities defend worse.")]
        [Range(0f, 1f)] public float quizAvoidChance = 0.5f;

        [Tooltip("Extra acceleration for a personality that has fallen far behind. Not consumed " +
                 "yet — catch-up is a separate task.")]
        [Min(0)] public float catchupAcceleration = 0.1f;

        [Header("Racecraft")]
        [Tooltip("Scales the sideways grip this personality believes it has, which is its braking " +
                 "point: above 1 it brakes later and carries more speed through corners, below 1 it " +
                 "plays safe. This is the axis you see from the outside.")]
        [Range(0.7f, 1.35f)] public float brakingConfidence = 1f;

        [Tooltip("How far off the racing line this personality will move to pass the car ahead, as a " +
                 "multiple of Overtake Offset Metres. 0 never attempts a pass.")]
        [Range(0f, 2f)] public float overtakeAggression = 0.6f;

        [Tooltip("How hard this personality covers the line of the car behind it. 0 never defends.")]
        [Range(0f, 1f)] public float blockStrength;

        [Tooltip("Metres this personality keeps from the car ahead before lifting off. 0 means it " +
                 "keeps its foot in and uses the other car as a brake - that is the rammer.")]
        [Min(0)] public float contactToleranceMetres = 6f;
    }

    [System.Serializable]
    public class AISettings
    {
        /// <summary>
        /// One entry per AI car, so the grid composition is data instead of enum order. The
        /// confirmed default is 폭주광 2 + 난폭자 1 + 봉쇄자 1 + 생존자 1 for five cars.
        /// <see cref="AiPersonalityRoster"/> shuffles which grid slot gets which entry, so the
        /// composition stays fixed while the slot mapping varies per race.
        /// </summary>
        public List<AIPersonalityType> personalityAssignments = new()
        {
            AIPersonalityType.Reckless,
            AIPersonalityType.Reckless,
            AIPersonalityType.Rammer,
            AIPersonalityType.Blocker,
            AIPersonalityType.CleanRacer,
        };

        public AiDrivingSettings driving = new();
        public AiPersonalityAssignmentSettings personalityAssignment = new();

        /// <summary>Used when a targeted car has no personality (the player, or an unassigned car).</summary>
        [Range(0f, 1f)] public float defaultQuizAvoidChance = 0.5f;

        // Pace scales match what GmtkRccpWaypointDriver used to hard-code; defence chances match
        // what CurseSettings used to hold. No number changes in this refactor.
        public List<AiPersonalityProfile> personalityProfiles = new()
        {
            new AiPersonalityProfile
            {
                // fastest and latest on the brakes, throws it up the inside, never defends
                personality = AIPersonalityType.Reckless,
                paceScale = 1f, lateralStrengthMetres = 0f,
                boostTendency = 1f, quizAvoidChance = 0.3f,
                brakingConfidence = 1.2f, overtakeAggression = 1.4f,
                blockStrength = 0f, contactToleranceMetres = 2.5f,
            },
            new AiPersonalityProfile
            {
                // aims at whatever is in front and keeps its foot in: tolerance 0 is the ram
                personality = AIPersonalityType.Rammer,
                paceScale = 0.96f, lateralStrengthMetres = 7f,
                boostTendency = 1f, quizAvoidChance = 0.35f,
                brakingConfidence = 1.05f, overtakeAggression = 1f,
                blockStrength = 0.2f, contactToleranceMetres = 0f,
            },
            new AiPersonalityProfile
            {
                // slowest, but sits in the way: covers the line of whoever is behind
                personality = AIPersonalityType.Blocker,
                paceScale = 0.86f, lateralStrengthMetres = 5f,
                boostTendency = 1f, quizAvoidChance = 0.65f,
                brakingConfidence = 0.95f, overtakeAggression = 0.3f,
                blockStrength = 1f, contactToleranceMetres = 5f,
            },
            new AiPersonalityProfile
            {
                // drives the line, brakes early, passes only when the room is there
                personality = AIPersonalityType.CleanRacer,
                paceScale = 0.9f, lateralStrengthMetres = 0f,
                boostTendency = 1f, quizAvoidChance = 0.8f,
                brakingConfidence = 1f, overtakeAggression = 0.6f,
                blockStrength = 0.15f, contactToleranceMetres = 8f,
            },
        };

        /// <summary>The profile for a personality, or null when the preset has no row for it.</summary>
        public AiPersonalityProfile GetProfile(AIPersonalityType personality)
        {
            foreach (var p in personalityProfiles)
                if (p != null && p.personality == personality) return p;
            return null;
        }

        /// <summary>Curse defence chance for a personality, falling back to the default.</summary>
        public float QuizAvoidChanceOf(AIPersonalityType personality)
        {
            var profile = GetProfile(personality);
            return profile != null ? profile.quizAvoidChance : defaultQuizAvoidChance;
        }
    }

    [System.Serializable]
    public class AiDrivingSettings
    {
        [Header("Look ahead")]
        [Tooltip("Aim distance at a standstill; speed adds to it so corners are seen early.")]
        [Min(1)] public float minLookAheadMetres = 14f;
        [Min(0)] public float lookAheadMetresPerKph = 0.32f;
        [Tooltip("Longest the aim point may ever be. At 200 kph the speed term alone reaches 78 m, " +
                 "which aims across a corner instead of through it and the car drives straight on.")]
        [Min(5)] public float maximumLookAheadMetres = 42f;
        [Tooltip("Heading change over the corner scan that pulls the aim point all the way back to the " +
                 "minimum. Looking far ahead is only useful while the road is straight.")]
        [Min(1)] public float lookAheadTightenDegrees = 22f;

        [Header("Corner speed")]
        [Tooltip("Shortest path length scanned for a corner; slow cars do not look further than this.")]
        [Min(1)] public float minCornerScanMetres = 45f;
        [Tooltip("Braking the AI assumes (m/s^2) when deciding how far ahead to scan for corners. The " +
                 "scan covers the braking distance, so fast cars start slowing early enough.")]
        [Min(1)] public float cornerBrakingDecel = 8f;
        [Tooltip("How fast the corner speed target may rise again (kph/s). Prevents brake-release " +
                 "hunting as a corner enters and leaves the speed-dependent scan window.")]
        [Min(1)] public float targetSpeedRiseKphPerSecond = 50f;
        [Tooltip("Sideways speed the AI treats as normal cornering. Above this the car is sliding, whatever grip the settings assume it has.")]
        [Min(0)] public float slipToleranceKph = 12f;
        [Tooltip("Kph cut from the speed target per kph of slip beyond the tolerance. This is what stops the AI running off the outside of a corner it entered too fast.")]
        [Min(0)] public float slipSpeedPenalty = 2.5f;
        [Min(1)] public float straightSpeedKph = 200f;
        [Tooltip("Slowest an AI will take any corner.")]
        [Min(1)] public float minCornerSpeedKph = 60f;
        [Tooltip("Sideways grip the AI assumes (m/s^2). Higher takes corners faster: corner speed is " +
                 "sqrt(grip x radius), so a wide sweeper stays fast while a hairpin still slows.")]
        [Min(1)] public float cornerGrip = 18f;

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

        [Header("Rivals")]
        [Tooltip("How far ahead and behind another car is close enough to race. Beyond this the AI " +
                 "drives the track as if it were alone.")]
        [Min(1)] public float rivalScanMetres = 40f;
        [Tooltip("Metres off the racing line a pass may take, before the personality's aggression " +
                 "scales it. The measured road edges still clamp the result.")]
        [Min(0)] public float overtakeOffsetMetres = 3.2f;
        [Tooltip("The AI only commits to a pass when it is closing this fast, so it does not weave " +
                 "behind a car it cannot catch.")]
        [Min(0)] public float overtakeMinClosingKph = 5f;
        [Tooltip("Metres the AI keeps from the car ahead when it cannot pass. The personality's " +
                 "contact tolerance shifts this: a rammer keeps nothing.")]
        [Min(0)] public float followGapMetres = 7f;
        [Tooltip("Kph the AI gives up below the car ahead when it has closed right up. Small on " +
                 "purpose: a queue of cars must settle in behind each other, not brake each other to " +
                 "walking pace.")]
        [Min(0)] public float followLiftKph = 10f;
        [Tooltip("Metres a defending car may move toward the line of the car behind it.")]
        [Min(0)] public float blockOffsetMetres = 2.4f;
        [Tooltip("Seconds between rival scans. The AI re-reads who is around it - and its own " +
                 "personality numbers - on this tick instead of every frame.")]
        [Min(0.02f)] public float rivalScanIntervalSeconds = 0.2f;

        [Header("Road probing")]
        [Min(1)] public float maxRoadHalfWidthMetres = 13f;
        [Min(0)] public float roadEdgeMarginMetres = 2.2f;
        [Tooltip("Height of the sideways barrier probe. A guardrail is about a metre tall and starts at " +
                 "the road edge, so a probe at its top slides over it and reports open road.")]
        [Min(0.05f)] public float barrierProbeHeightMetres = 0.5f;
        [Tooltip("Thickness of the barrier probe. A sphere cannot graze past a thin rail the way a ray " +
                 "can, which is what let cars aim into the guardrail.")]
        [Min(0.05f)] public float barrierProbeRadiusMetres = 0.35f;

        [Header("Diagnostics")]
        [Tooltip("Logs one AI car's speed target versus what the car actually does, once a second.")]
        public bool logDriveTelemetry = false;
        [Tooltip("Logs the measured road edges (what the barrier probe hit, how wide the road was read as) and every collision with scenery. For finding out why cars hit the guardrail.")]
        public bool logTrackContact = false;

        [Header("Waypoints and recovery")]
        [Min(1)] public float waypointReachMetres = 9f;
        [Tooltip("The reach also covers this many seconds of travel, so a fast car consumes waypoints as it passes them. A fixed 9 m sphere is passed in a tenth of a second at 200 kph and is easy to miss entirely when the car is running an offset racing line.")]
        [Min(0)] public float waypointReachSecondsAhead = 0.25f;
        [Tooltip("Distance from its own waypoint at which a car re-anchors to the nearest one. Without it a cursor left behind never catches up, and the corner scan keeps reading the piece of track the car has already driven.")]
        [Min(5)] public float waypointResyncMetres = 45f;
        [Tooltip("Largest sideways nudge the ram/block personality may add to the racing line.")]
        [Min(0)] public float maxPersonalityLateralMetres = 3f;
        [Min(0)] public float stuckSpeedKph = 1.5f;
        [Min(0)] public float stuckDelaySeconds = 2.5f;
        [Min(0)] public float reverseDurationSeconds = 1.25f;

        [Header("Tactics")]
        [Tooltip("How often the AI reconsiders spending a charge. Short so a charge is spent as soon as it recharges.")]
        [Min(0.1f)] public float boostDecisionIntervalSeconds = 0.25f;
        [Tooltip("Heading change over the corner scan that still counts as a straight, both for " +
                 "spending a boost and for letting the boosted speed target stand.")]
        [Min(0)] public float boostStraightMaximumDegrees = 8f;
        [Tooltip("Speed below which the AI keeps its charge. 0 lets it boost off the line as well, which is the current design; raise it to stop cars burning a charge while crawling out of a spin.")]
        [Min(0)] public float boostMinimumSpeedKph = 0f;
        [Min(1)] public float catchupGapMetres = 35f;
        [Min(0)] public float eliminationUrgencyScale = 1.12f;
        [Min(1)] public float obstacleProbeMetres = 12f;
        [Range(0f, 1f)] public float obstacleAvoidanceStrength = 0.65f;
    }

    [System.Serializable]
    public class AiPersonalityAssignmentSettings
    {
        [Tooltip("0 shuffles the personalities freshly every race; any other value reproduces one mix.")]
        public int shuffleSeed = 0;
        [Tooltip("Logs the seed and the resulting grid so an odd race can be reproduced.")]
        public bool logAssignment = true;

        [Header("Aggression")]
        // Ram/block strength moved to AiPersonalityProfile.lateralStrengthMetres so each
        // personality owns its own number. Range stays shared: it is how far any AI can
        // notice the player, not a personality trait.
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
        [Tooltip("Duel time limit; when it runs out the racer furthest along the track wins.")]
        [Min(0)] public float gateOpenDurationSeconds = 20f;
        [Tooltip("Drivable width the two closing leaves span together.")]
        [Min(1)] public float gateWidthMeters = 16f;
        [Min(1)] public float gateHeightMeters = 7f;
        [Tooltip("Pause after the winner crosses before the leaves start moving.")]
        [Min(0)] public float gateCloseDelaySeconds = 0.2f;
        [Tooltip("Time the leaves take to slam shut behind the winner.")]
        [Min(0)] public float gateCloseDurationSeconds = 0.75f;
        [Tooltip("Pause after the gate is shut before the racers locked outside are executed.")]
        [Min(0)] public float gateExecutionDelaySeconds = 0.25f;
        [Header("Explosion / Wreck")]
        [Tooltip("Breakable vehicle spawned in place of an executed racer.")]
        public GameObject executionBreakableVehiclePrefab;
        [Min(0)] public float explosionVfxDurationSeconds = 0.45f;
        [Min(0)] public float wreckLingerSeconds = 2.5f;
        [Header("Curse")]
        [Tooltip("One-shot effect spawned on a racer when a curse penalty is applied.")]
        public GameObject curseAppliedEffectPrefab;
        [Min(0.01f)] public float curseAppliedEffectScale = 1.5f;
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

    [System.Serializable]
    public class SpecialEventSettings
    {
        public bool enabled = true;
        [Min(0)] public float initialDelaySeconds = 15f;
        [Min(0)] public float minimumIntervalSeconds = 25f;
        [Min(0)] public float maximumIntervalSeconds = 40f;
        [Min(0)] public float warningLeadSeconds = 2f;
        [Min(1)] public int maximumSimultaneousEvents = 1;
        [Min(1)] public int maximumEventsPerRace = 4;
        public bool blockDuringExecutionWarning = true;
        public bool blockDuringQuiz = true;
        public bool blockDuringOvertakeChallenge = true;
        public bool blockDuringFinalDuel = true;
        public bool preventImmediateRepeat = true;

        [Header("Placement")]
        [Tooltip("Build hazards on the waypoint loop ahead of the player instead of anywhere on " +
                 "the track. The track is long enough that a random waypoint is never seen.")]
        public bool spawnAheadOfPlayer = true;
        [Min(0)] public float spawnAheadMinimumMetres = 60f;
        [Min(0)] public float spawnAheadMaximumMetres = 140f;

        [Header("Dump Truck")]
        [Min(1)] public float dumpTruckSpeed = 22f;
        [Min(0)] public float dumpTruckDamage = 35f;
        [Min(0)] public float dumpTruckKnockback = 6f;
        [Min(0.1f)] public float dumpTruckLifetimeSeconds = 12f;

        [Header("Earthquake")]
        [Min(0.1f)] public float earthquakeDurationSeconds = 3f;
        [Min(0)] public float earthquakeLateralVelocityChange = 2f;

        [Header("Meteor")]
        [Min(0)] public float meteorWarningSeconds = 2f;
        [Min(0.1f)] public float meteorImpactRadius = 5f;
        [Min(0)] public float meteorDamage = 45f;
        [Min(0)] public float meteorKnockback = 5f;
        [Min(0.1f)] public float meteorDebrisLifetimeSeconds = 3f;

        [Header("Construction Zone")]
        [Tooltip("Barriers across the track; one of them is always left out as a gap to aim for.")]
        [Min(2)] public int constructionBarrierCount = 5;
        [Min(0.5f)] public float constructionSpacingMetres = 2.4f;
        [Min(0)] public float constructionDamage = 17f;
        [Min(0)] public float constructionKnockback = 2f;
        [Min(0.1f)] public float constructionLifetimeSeconds = 14f;

        [Header("Livestock Crossing")]
        [Min(1)] public int cowCount = 3;
        [Min(0)] public float cowSpeed = 7f;
        [Tooltip("How far off to the side the herd starts, so it walks in rather than popping in.")]
        [Min(0)] public float cowStartSideOffsetMetres = 16f;
        [Min(0)] public float cowSpacingMetres = 4f;
        [Min(0)] public float cowDamage = 20f;
        [Min(0)] public float cowKnockback = 4f;
        [Min(0.1f)] public float cowLifetimeSeconds = 12f;

        [Header("Beach Ball")]
        [Min(0.1f)] public float ballDiameterMetres = 6f;
        [Min(0.1f)] public float ballMass = 3f;
        [Range(0f, 1f)] public float ballBounciness = 0.85f;
        [Min(0)] public float ballDropHeightMetres = 14f;
        [Min(0)] public float ballDamage = 14f;
        [Min(0)] public float ballKnockback = 5f;
        [Min(0.1f)] public float ballLifetimeSeconds = 14f;

        [Header("Crate Shower")]
        [Min(1)] public int crateMinimumCount = 5;
        [Min(1)] public int crateMaximumCount = 9;
        [Min(0)] public float crateScatterRadiusMetres = 7f;
        [Min(0)] public float crateDropHeightMetres = 18f;
        [Min(0.1f)] public float crateMass = 15f;
        [Min(0)] public float crateDamage = 10f;
        [Min(0)] public float crateKnockback = 2f;
        [Min(0.1f)] public float crateLifetimeSeconds = 10f;

        [Header("Boost Pad")]
        [Tooltip("The one hazard that helps: a pad that shoves whoever drives over it forward.")]
        [Min(0)] public float boostPadForce = 25f;
        [Min(0.1f)] public float boostPadLengthMetres = 8f;
        [Min(0.1f)] public float boostPadWidthMetres = 4f;
        [Min(0.1f)] public float boostPadLifetimeSeconds = 16f;
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
        public VehicleSettings vehicle = new();
        public OvertakeSettings overtake = new();
        public CameraSettings camera = new();
        public AISettings ai = new();
        public TrackValidationSettings trackValidation = new();
        public PresentationSettings presentation = new();
        public PlaytestSettings playtest = new();
        public SpecialEventSettings specialEvents = new();

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
            if (specialEvents.minimumIntervalSeconds > specialEvents.maximumIntervalSeconds)
                errors.Add("specialEvents.minimumIntervalSeconds must be <= maximumIntervalSeconds");
            if (specialEvents.maximumSimultaneousEvents < 1)
                errors.Add("specialEvents.maximumSimultaneousEvents must be >= 1");
            if (specialEvents.maximumEventsPerRace < 1)
                errors.Add("specialEvents.maximumEventsPerRace must be >= 1");
            if (specialEvents.spawnAheadMinimumMetres > specialEvents.spawnAheadMaximumMetres)
                errors.Add("specialEvents.spawnAheadMinimumMetres must be <= spawnAheadMaximumMetres");
            if (specialEvents.crateMinimumCount > specialEvents.crateMaximumCount)
                errors.Add("specialEvents.crateMinimumCount must be <= crateMaximumCount");
            if (specialEvents.constructionBarrierCount < 2)
                errors.Add("specialEvents.constructionBarrierCount must be >= 2 (one is the gap)");

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
            if (damage.wreckMinimumLaunchSpeed > damage.wreckMaximumLaunchSpeed)
                errors.Add("damage.wreckMinimumLaunchSpeed must be <= wreckMaximumLaunchSpeed");

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
            if (curse.aiQuizResolutionDelayMinimumSeconds >
                curse.aiQuizResolutionDelayMaximumSeconds)
            {
                errors.Add(
                    "curse.aiQuizResolutionDelayMinimumSeconds must be <= " +
                    "aiQuizResolutionDelayMaximumSeconds");
            }

            if (boost.overtakeRewardCharges > boost.maximumCharges)
                errors.Add("boost.overtakeRewardCharges must be <= boost.maximumCharges");

            // AI personalities: the grid list must cover every AI car, and every personality
            // handed out must have a profile row, otherwise a car silently falls back to defaults.
            if (ai.personalityAssignments == null || ai.personalityAssignments.Count == 0)
            {
                errors.Add("ai.personalityAssignments must list one personality per AI car");
            }
            else if (ai.personalityAssignments.Count != race.aiCount)
            {
                errors.Add($"ai.personalityAssignments has {ai.personalityAssignments.Count} " +
                           $"entries but race.aiCount is {race.aiCount}");
            }

            if (ai.personalityProfiles == null || ai.personalityProfiles.Count == 0)
            {
                errors.Add("ai.personalityProfiles must contain a row per personality");
            }
            else
            {
                foreach (var profile in ai.personalityProfiles)
                {
                    if (profile == null)
                    {
                        errors.Add("ai.personalityProfiles contains an empty row");
                        continue;
                    }
                    if (profile.paceScale <= 0f)
                        errors.Add($"ai.personalityProfiles[{profile.personality}].paceScale must be > 0");
                    if (profile.lateralStrengthMetres < 0f)
                        errors.Add($"ai.personalityProfiles[{profile.personality}].lateralStrengthMetres must be >= 0");
                }

                if (ai.personalityAssignments != null)
                {
                    foreach (var personality in ai.personalityAssignments)
                    {
                        if (ai.GetProfile(personality) == null)
                            errors.Add($"ai.personalityAssignments uses {personality} but " +
                                       "ai.personalityProfiles has no row for it");
                    }
                }
            }

            var r = camera.executionCctvViewportRect;
            if (r.width <= 0f || r.height <= 0f ||
                r.xMin < 0f || r.yMin < 0f || r.xMax > 1f || r.yMax > 1f)
            {
                errors.Add("camera.executionCctvViewportRect must have positive size and remain within the 0..1 viewport");
            }

            // the final gate must have a real opening and a real duel window to be crossable
            if (presentation.gateOpenDurationSeconds <= 0f)
                errors.Add("presentation.gateOpenDurationSeconds must be > 0");
            if (presentation.gateWidthMeters <= 0f)
                errors.Add("presentation.gateWidthMeters must be > 0");
            if (presentation.gateHeightMeters <= 0f)
                errors.Add("presentation.gateHeightMeters must be > 0");
            if (presentation.gateCloseDurationSeconds < 0f)
                errors.Add("presentation.gateCloseDurationSeconds must be >= 0");
            if (presentation.gateCloseDelaySeconds < 0f)
                errors.Add("presentation.gateCloseDelaySeconds must be >= 0");
            if (presentation.gateExecutionDelaySeconds < 0f)
                errors.Add("presentation.gateExecutionDelaySeconds must be >= 0");

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
