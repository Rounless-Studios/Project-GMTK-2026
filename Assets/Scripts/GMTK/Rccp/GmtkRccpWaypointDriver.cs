using Gmtk2026.GameBalance;
using UnityEngine;

namespace GMTK.Rccp
{
    /// <summary>
    /// Drives an RCCP vehicle over the track waypoints. The component only reads the world (waypoint
    /// path, road edges) and writes RCCP inputs; every number and every decision curve lives in
    /// <see cref="AiDrivingSettings"/> / <see cref="AiDriving"/> so the balance asset stays the single
    /// place to tune the AI and the maths stays unit-tested.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RCCP_CarController))]
    public sealed class GmtkRccpWaypointDriver : MonoBehaviour
    {
        private static AiDrivingSettings S => GameBalance.Current.ai.driving;

        private RCCP_CarController carController;
        private Rigidbody carRigidbody;
        private RCCP_Input inputReceiver;
        private RCCP_Inputs inputs;
        private GmtkRccpWaypointPath path;
        private int waypointIndex;
        private int raceIndex;
        private float throttleScale = 0.9f;
        private float previousSteerAngle;
        private float lastTargetSpeed;
        private float lastTelemetryTime;
        private float gridLaneOffset;
        private float mergeTravelled;
        private float laneSpread;
        private int measuredEdgeIndex = -1;
        private float edgeLimitLeft;
        private float edgeLimitRight;
        private float stuckTimer;
        private float reverseTimer;
        private AIPersonalityType personalityType;
        private Transform personalityTarget;
        private float lateralStrength;
        private float aggroRange;
        private float nextBoostDecisionAt;
        private BoostController boost;

        // racecraft: the rivals are re-read on a slow tick, never per frame
        private Transform[] rivalTransforms = System.Array.Empty<Transform>();
        private Rigidbody[] rivalBodies = System.Array.Empty<Rigidbody>();
        private int rosterCount = -1;
        private float nextRivalScanAt;
        private AiPersonalityProfile profile;
        private float gapAheadMetres;
        private float aheadLateralMetres;
        private float aheadSpeedKph;
        private float gapBehindMetres;
        private float behindLateralMetres;
        private float currentSpeedKph;

        /// <summary>Metres to the car ahead inside the scan window, 0 when there is none. Diagnostics.</summary>
        public float GapAheadMetres => gapAheadMetres;

        /// <summary>Metres to the car behind inside the scan window, 0 when there is none. Diagnostics.</summary>
        public float GapBehindMetres => gapBehindMetres;

        /// <summary>The speed the driver is currently asking for. Diagnostics.</summary>
        public float TargetSpeedKph => lastTargetSpeed;

        /// <summary>The personality driving this car. Diagnostics.</summary>
        public AIPersonalityType Personality => personalityType;

        /// <summary>True once the boost system has handed this car a controller.</summary>
        public bool HasBoost => boost != null;

        /// <summary>
        /// The boost system attaches its controllers when the race starts, which is after this driver
        /// cached its components: without this the AI would hold a null controller for the whole race
        /// and never spend a charge, while the player's own boost worked fine.
        /// </summary>
        public void AttachBoost(BoostController controller)
        {
            boost = controller;
        }

        private void Awake()
        {
            carController = GetComponent<RCCP_CarController>();
            carRigidbody = GetComponent<Rigidbody>();
            inputReceiver = GetComponentInChildren<RCCP_Input>(true);
            inputs = new RCCP_Inputs();
            boost = GetComponent<BoostController>();
        }

        private void OnDisable()
        {
            inputs?.Clear();

            if (inputReceiver != null)
                inputReceiver.DisableOverrideInputs();
        }

        /// <summary>
        /// <paramref name="carRaceIndex"/> is the grid position, which keeps the per-car lane spread
        /// reproducible across runs.
        /// </summary>
        public void Initialize(GmtkRccpWaypointPath waypointPath, int carRaceIndex)
        {
            path = waypointPath;
            raceIndex = carRaceIndex;

            if (path == null || path.Count == 0)
                return;

            waypointIndex = path.FindClosestIndex(transform.position);

            // Every car shares one waypoint ring, so without a lane of its own each car aims at the
            // same centre line point and the field dives into the middle of the track at the start.
            gridLaneOffset = Mathf.Clamp(
                path.SignedLateralOffset(waypointIndex, transform.position),
                -S.maxLaneOffsetMetres,
                S.maxLaneOffsetMetres);
            mergeTravelled = 0f;
            laneSpread = AiDriving.LaneSpreadMetres(raceIndex, gridLaneOffset, S);
        }

        public void ConfigurePersonality(AIPersonalityType type)
        {
            personalityType = type;

            // pace is the personality's own number in the balance asset, not a switch here. The rest of
            // the profile is re-read on every rival scan, so tuning it mid-race takes effect.
            profile = GameBalance.Current.ai.GetProfile(type);
            throttleScale = profile != null ? profile.paceScale : 0.9f;
        }

        public void SetPersonalityTarget(
            AIPersonalityType type,
            Transform target,
            float lateralAmount,
            float range)
        {
            personalityType = type;
            personalityTarget = target;
            lateralStrength = lateralAmount;
            aggroRange = range;
        }

        public void ResetAfterRespawn(int closestWaypointIndex)
        {
            if (path == null || path.Count == 0)
                return;

            waypointIndex = ((closestWaypointIndex % path.Count) + path.Count) % path.Count;
            stuckTimer = 0f;
            reverseTimer = 0f;
            previousSteerAngle = 0f;
            lastTargetSpeed = 0f;
            measuredEdgeIndex = -1;
            inputs?.Clear();
        }

        private void FixedUpdate()
        {
            if (path == null || path.Count == 0 || inputReceiver == null)
                return;

            float speedKph = carRigidbody != null ? carRigidbody.linearVelocity.magnitude * 3.6f : 0f;
            currentSpeedKph = speedKph;
            mergeTravelled += speedKph / 3.6f * Time.fixedDeltaTime;

            if (Time.time >= nextRivalScanAt)
            {
                nextRivalScanAt = Time.time + S.rivalScanIntervalSeconds;
                ScanRivals();
            }

            AdvanceWaypoint();

            Vector3 aimPoint = GetRacingLinePoint(AiDriving.LookAheadMetres(speedKph, S));
            Vector3 localTarget = transform.InverseTransformPoint(aimPoint);
            float targetAngle = Mathf.Atan2(localTarget.x, Mathf.Max(1f, localTarget.z)) * Mathf.Rad2Deg;

            UpdateRecovery(speedKph);

            if (reverseTimer > 0f)
            {
                reverseTimer -= Time.fixedDeltaTime;
                inputs.throttleInput = 0f;
                inputs.brakeInput = 1f;
                inputs.steerInput = -Mathf.Sign(targetAngle);
            }
            else
            {
                // corner speed comes from the path shape over the braking distance, so the car is
                // already slowing when it reaches the corner
                float scan = AiDriving.CornerScanMetres(speedKph, S);
                float headingChange = path.HeadingChangeAhead(waypointIndex, scan, out float arc);
                float targetSpeed = AiDriving.SmoothTargetSpeedKph(
                    lastTargetSpeed,
                    AiDriving.CornerSpeedKph(
                        headingChange,
                        arc,
                        throttleScale,
                        S,
                        profile != null ? profile.brakingConfidence : 1f),
                    Time.fixedDeltaTime,
                    S);
                targetSpeed *= TacticalPaceScale();

                // settle behind the car ahead instead of driving through it - unless this personality
                // keeps no gap at all, which is what a rammer does
                targetSpeed = AiDriving.FollowSpeedKph(
                    targetSpeed,
                    gapAheadMetres,
                    aheadSpeedKph,
                    profile != null ? profile.contactToleranceMetres : S.followGapMetres,
                    S);

                // a burning boost raises the target on a straight, so the car stops braking against
                // its own boost; at a corner the target is left alone and the boost simply runs out
                if (boost != null && boost.State != null && boost.State.IsBoosting)
                    targetSpeed = AiDriving.BoostedTargetKph(
                        targetSpeed,
                        boost.State.CurrentSpeedMultiplier,
                        headingChange,
                        S);

                lastTargetSpeed = targetSpeed;

                AiDriving.SpeedInputs(targetSpeed, speedKph, out float throttle, out float brake);
                inputs.throttleInput = throttle;
                inputs.brakeInput = brake;

                LogTelemetry(speedKph, targetSpeed, arc, headingChange);

                float angleRate = (targetAngle - previousSteerAngle) / Time.fixedDeltaTime;
                inputs.steerInput = Mathf.Clamp(
                    (targetAngle * AiDriving.SteerGain(speedKph, S) - angleRate * S.steerDamping) / 35f,
                    -1f,
                    1f);

                ApplyObstacleAvoidance(ref inputs.steerInput);
                TryUseBoost(headingChange);
            }

            previousSteerAngle = targetAngle;

            inputs.handbrakeInput = 0f;
            inputs.clutchInput = 0f;
            inputs.nosInput = personalityType == AIPersonalityType.Reckless ? 0.35f : 0f;
            inputReceiver.OverrideInputs(inputs);
        }

        /// <summary>
        /// Finds the closest car ahead and behind, in this car's own frame: how far, how far to the
        /// side, and how fast the one ahead is going. Runs on the rival tick rather than per frame, and
        /// re-reads the personality profile at the same time so the balance asset can be tuned while a
        /// race is running. Cars outside the scan window, eliminated cars and the car itself are skipped.
        /// </summary>
        private void ScanRivals()
        {
            gapAheadMetres = 0f;
            gapBehindMetres = 0f;
            aheadSpeedKph = 0f;
            aheadLateralMetres = 0f;
            behindLateralMetres = 0f;
            profile = GameBalance.Current.ai.GetProfile(personalityType);

            BuildRoster();

            float bestAhead = float.MaxValue;
            float bestBehind = float.MaxValue;

            for (int i = 0; i < rivalTransforms.Length; i++)
            {
                Transform rival = rivalTransforms[i];
                if (rival == null || rival == transform || !rival.gameObject.activeInHierarchy) continue;

                Vector3 local = transform.InverseTransformPoint(rival.position);
                float along = local.z;
                float side = local.x;

                // a car on the other side of the barrier is not racing this one
                if (Mathf.Abs(side) > S.maxRoadHalfWidthMetres) continue;
                if (Mathf.Abs(along) > S.rivalScanMetres) continue;

                if (along > 0f && along < bestAhead)
                {
                    bestAhead = along;
                    gapAheadMetres = along;
                    aheadLateralMetres = side;
                    aheadSpeedKph = rivalBodies[i] != null
                        ? rivalBodies[i].linearVelocity.magnitude * 3.6f
                        : 0f;
                }
                else if (along <= 0f && -along < bestBehind)
                {
                    bestBehind = -along;
                    gapBehindMetres = -along;
                    behindLateralMetres = side;
                }
            }
        }

        /// <summary>
        /// Caches the other cars' transforms and bodies, rebuilt only when the field size changes, so
        /// the scan never walks the race roster or calls GetComponent on the driving path.
        /// </summary>
        private void BuildRoster()
        {
            int count = Race.CarCount;
            if (count == rosterCount) return;

            rosterCount = count;
            rivalTransforms = new Transform[count];
            rivalBodies = new Rigidbody[count];

            for (int i = 0; i < count; i++)
            {
                GameObject car = Race.CarByIndex(i);
                if (car == null) continue;
                rivalTransforms[i] = car.transform;
                rivalBodies[i] = car.GetComponent<Rigidbody>();
            }
        }

        /// <summary>
        /// Sideways metres this car wants for racecraft: off the line to pass the car ahead, or across
        /// the line of the car behind to defend. Both are personality numbers, so a clean racer barely
        /// moves and a blocker parks itself in the way.
        /// </summary>
        private float RacecraftLateralMetres(float speedKph)
        {
            float overtake = AiDriving.OvertakeOffsetMetres(
                gapAheadMetres,
                speedKph - aheadSpeedKph,
                aheadLateralMetres,
                edgeLimitLeft,
                edgeLimitRight,
                profile != null ? profile.overtakeAggression : 0.6f,
                S);

            float block = AiDriving.BlockOffsetMetres(
                gapBehindMetres,
                behindLateralMetres,
                profile != null ? profile.blockStrength : 0f,
                S);

            return overtake + block;
        }

        private float TacticalPaceScale()
        {
            float scale = 1f;
            EliminationManager elimination = EliminationManager.Instance;
            if (elimination != null && elimination.CurrentLastPlaceIndex == raceIndex)
                scale *= S.eliminationUrgencyScale;

            if (Race.CarCount > 0)
            {
                double leader = double.MinValue;
                foreach (int index in Race.AllCarIndices())
                    leader = System.Math.Max(leader, Race.ScoreOf(index));

                if (leader - Race.ScoreOf(raceIndex) >= S.catchupGapMetres)
                {
                    AiPersonalityProfile profile =
                        GameBalance.Current.ai.GetProfile(personalityType);
                    if (profile != null) scale += profile.catchupAcceleration;
                }
            }

            return scale;
        }

        private void TryUseBoost(float headingChange)
        {
            if (boost == null || Time.time < nextBoostDecisionAt) return;
            nextBoostDecisionAt = Time.time + S.boostDecisionIntervalSeconds;

            if (!AiDriving.ShouldBoostOnStraight(
                    headingChange,
                    currentSpeedKph,
                    boost.Charges > 0,
                    profile != null ? profile.boostTendency : 1f,
                    Random.value,
                    S))
                return;

            boost.TryBoost();
        }

        private void ApplyObstacleAvoidance(ref float steer)
        {
            Vector3 origin = transform.position + transform.forward * 1.5f + Vector3.up * 0.6f;
            if (!Physics.Raycast(
                    origin,
                    transform.forward,
                    out RaycastHit hit,
                    S.obstacleProbeMetres,
                    ~0,
                    QueryTriggerInteraction.Ignore))
                return;
            if (hit.transform.root == transform.root) return;

            Vector3 local = transform.InverseTransformPoint(hit.point);
            float direction = local.x >= 0f ? -1f : 1f;
            steer = Mathf.Clamp(
                steer + direction * S.obstacleAvoidanceStrength,
                -1f,
                1f);
        }

        /// <summary>
        /// One AI car's telemetry once a second: what the driver asked for against what the car did,
        /// so a slow lap can be attributed to the target, to the inputs, or to the drivetrain.
        /// </summary>
        private void LogTelemetry(float speedKph, float targetSpeed, float scan, float headingChange)
        {
            if (!S.logDriveTelemetry || raceIndex != 1 || Time.time - lastTelemetryTime < 1f)
                return;

            lastTelemetryTime = Time.time;

            Debug.Log($"AI drive {name}: spd={speedKph:F0} target={targetSpeed:F0} " +
                      $"thr={inputs.throttleInput:F2} brk={inputs.brakeInput:F2} steer={inputs.steerInput:F2} " +
                      $"gear={carController.currentGear} rpm={carController.engineRPM:F0} " +
                      $"engine={carController.engineRunning} scan={scan:F0}m angle={headingChange:F0}deg " +
                      $"applied(thr={carController.throttleInput_V:F2} brk={carController.brakeInput_V:F2} " +
                      $"steer={carController.steerInput_V:F2})");
        }

        /// <summary>
        /// Places the aim point on a racing line rather than on the centre line: pushed toward the
        /// inside of the corner being entered, widened to the outside while a sharper corner is still
        /// ahead, offset by the car's own lane, and finally clamped to the measured road.
        /// </summary>
        private Vector3 GetRacingLinePoint(float lookAhead)
        {
            path.SampleAim(
                waypointIndex,
                transform.position,
                lookAhead,
                out Vector3 aimPoint,
                out Vector3 pathDirection,
                out float turnDegrees);

            path.SampleAim(
                waypointIndex,
                transform.position,
                lookAhead * 2f,
                out Vector3 _,
                out Vector3 _,
                out float farTurnDegrees);

            Vector3 pathRight = Vector3.Cross(Vector3.up, pathDirection);
            float reference = Mathf.Max(1f, S.racingLineReferenceDegrees);

            // inside of the corner being entered
            float lateral = Mathf.Sign(turnDegrees)
                            * Mathf.Clamp01(Mathf.Abs(turnDegrees) / reference)
                            * S.apexOffsetMetres;

            // widen to the outside while the sharp part is still further ahead
            if (Mathf.Abs(farTurnDegrees) > Mathf.Abs(turnDegrees))
            {
                lateral -= Mathf.Sign(farTurnDegrees)
                           * Mathf.Clamp01(Mathf.Abs(farTurnDegrees) / reference)
                           * S.entryOffsetMetres;
            }

            float mergeBlend = AiDriving.GridLaneBlend(mergeTravelled, S);

            // The ram/block pull is a world-space vector toward the player: only its sideways part
            // belongs on the racing line, it is capped, and it stays off while the grid is merging.
            // Otherwise every AI aims at the player's grid slot and the field converges at the start.
            float personalityLateral = Mathf.Clamp(
                Vector3.Dot(GetPersonalityOffset(aimPoint), pathRight),
                -S.maxPersonalityLateralMetres,
                S.maxPersonalityLateralMetres) * (1f - mergeBlend);

            // passing and defending also wait for the merge: at the start every car has someone right
            // in front of it, and reacting to that on the grid throws the whole field off line
            lateral += gridLaneOffset * mergeBlend + laneSpread + personalityLateral
                       + RacecraftLateralMetres(currentSpeedKph) * (1f - mergeBlend);

            // the waypoint is used as a track cross-section line, never as a point to drive to
            MeasureRoadEdges(aimPoint, pathRight);

            return aimPoint + pathRight * AiDriving.ClampLateral(lateral, edgeLimitLeft, edgeLimitRight);
        }

        /// <summary>
        /// Turns the aim point into a cross-section line by probing the barriers, and by walking
        /// outward until the ground stops when a side has no barrier. Cached per waypoint so this
        /// costs a couple of casts per second, not per frame.
        /// </summary>
        private void MeasureRoadEdges(Vector3 centre, Vector3 pathRight)
        {
            if (measuredEdgeIndex == waypointIndex)
                return;

            measuredEdgeIndex = waypointIndex;
            edgeLimitRight = Mathf.Max(0f, ProbeEdge(centre, pathRight) - S.roadEdgeMarginMetres);
            edgeLimitLeft = Mathf.Max(0f, ProbeEdge(centre, -pathRight) - S.roadEdgeMarginMetres);
        }

        private float ProbeEdge(Vector3 centre, Vector3 direction)
        {
            float limit = S.maxRoadHalfWidthMetres;

            // a barrier is the hard limit
            if (Physics.Raycast(centre + Vector3.up * 1.2f, direction, out RaycastHit hit, limit))
                return hit.distance;

            // otherwise the surface edge: the track has no terrain beside it, so the last sample with
            // ground under it is the edge
            for (float distance = 2f; distance <= limit; distance += 2f)
            {
                Vector3 probe = centre + direction * distance + Vector3.up * 3f;

                if (!Physics.Raycast(probe, Vector3.down, 8f))
                    return distance - 2f;
            }

            return limit;
        }

        /// <summary>
        /// Consumes waypoints that are reached or already behind the car, so the racing line can cut a
        /// corner instead of driving to every marker, and a missed marker does not turn the car around.
        /// </summary>
        private void AdvanceWaypoint()
        {
            float reach = S.waypointReachMetres;
            float reachSqr = reach * reach;
            float behindLimitSqr = reach * 3f * (reach * 3f);

            for (int step = 0; step < path.Count; step++)
            {
                Vector3 toWaypoint = path[waypointIndex].position - transform.position;
                toWaypoint.y = 0f;

                bool reached = toWaypoint.sqrMagnitude <= reachSqr;
                bool behind = toWaypoint.sqrMagnitude <= behindLimitSqr
                              && Vector3.Dot(toWaypoint, transform.forward) < 0f;

                if (!reached && !behind)
                    return;

                waypointIndex = (waypointIndex + 1) % path.Count;
            }
        }

        private void UpdateRecovery(float speedKph)
        {
            if (speedKph <= S.stuckSpeedKph)
                stuckTimer += Time.fixedDeltaTime;
            else
                stuckTimer = 0f;

            if (stuckTimer < S.stuckDelaySeconds)
                return;

            stuckTimer = 0f;
            reverseTimer = S.reverseDurationSeconds;
        }

        private Vector3 GetPersonalityOffset(Vector3 waypointPosition)
        {
            if (personalityTarget == null)
                return Vector3.zero;

            Vector3 toTarget = personalityTarget.position - transform.position;

            if (toTarget.sqrMagnitude > aggroRange * aggroRange)
                return Vector3.zero;

            if (lateralStrength <= 0f)
                return Vector3.zero;

            return personalityType switch
            {
                AIPersonalityType.Rammer => toTarget.normalized * lateralStrength,
                AIPersonalityType.Blocker =>
                    Vector3.Project(toTarget, transform.right).normalized * lateralStrength,
                _ => Vector3.zero,
            };
        }
    }
}
