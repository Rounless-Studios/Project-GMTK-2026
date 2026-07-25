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
        private float ramStrength;
        private float blockStrength;
        private float aggroRange;

        private void Awake()
        {
            carController = GetComponent<RCCP_CarController>();
            carRigidbody = GetComponent<Rigidbody>();
            inputReceiver = GetComponentInChildren<RCCP_Input>(true);
            inputs = new RCCP_Inputs();
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
            throttleScale = type switch
            {
                AIPersonalityType.Reckless => 1f,
                AIPersonalityType.Rammer => 0.96f,
                AIPersonalityType.Blocker => 0.86f,
                _ => 0.9f,
            };
        }

        public void SetPersonalityTarget(
            AIPersonalityType type,
            Transform target,
            float ramAmount,
            float blockAmount,
            float range)
        {
            personalityType = type;
            personalityTarget = target;
            ramStrength = ramAmount;
            blockStrength = blockAmount;
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
            mergeTravelled += speedKph / 3.6f * Time.fixedDeltaTime;

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
                    AiDriving.CornerSpeedKph(headingChange, arc, throttleScale, S),
                    Time.fixedDeltaTime,
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
            }

            previousSteerAngle = targetAngle;

            inputs.handbrakeInput = 0f;
            inputs.clutchInput = 0f;
            inputs.nosInput = personalityType == AIPersonalityType.Reckless ? 0.35f : 0f;
            inputReceiver.OverrideInputs(inputs);
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

            lateral += gridLaneOffset * mergeBlend + laneSpread + personalityLateral;

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

            return personalityType switch
            {
                AIPersonalityType.Rammer => toTarget.normalized * ramStrength,
                AIPersonalityType.Blocker =>
                    Vector3.Project(toTarget, transform.right).normalized * blockStrength,
                _ => Vector3.zero,
            };
        }
    }
}
