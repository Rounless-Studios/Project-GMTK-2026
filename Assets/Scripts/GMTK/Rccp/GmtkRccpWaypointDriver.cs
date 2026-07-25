using UnityEngine;

namespace GMTK.Rccp
{
    /// <summary>
    /// Drives an RCCP vehicle over the existing track waypoints without depending on the
    /// retired RSK car controller or a separately baked NavMesh.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RCCP_CarController))]
    public sealed class GmtkRccpWaypointDriver : MonoBehaviour
    {
        [SerializeField] private float waypointReachDistance = 9f;
        [Tooltip("Aim distance at a standstill; speed adds to it so corners are seen early.")]
        [SerializeField] private float minLookAhead = 14f;
        [SerializeField] private float lookAheadPerKph = 0.32f;
        [Tooltip("Path length whose heading change decides the corner speed.")]
        [SerializeField] private float cornerScanDistance = 50f;
        [SerializeField] private float straightSpeedKph = 135f;
        [SerializeField] private float tightCornerSpeedKph = 42f;
        [Tooltip("Heading change over the scan window that counts as a full-tightness corner.")]
        [SerializeField] private float tightCornerDegrees = 75f;
        [Tooltip("Metres the aim point moves toward the inside of a corner at the apex.")]
        [SerializeField] private float apexOffset = 4.5f;
        [Tooltip("Metres the aim point moves to the outside while a sharp corner is still ahead.")]
        [SerializeField] private float entryOffset = 3.5f;
        [Tooltip("Turn angle treated as a full-tightness corner when placing the racing line.")]
        [SerializeField] private float racingLineReferenceDegrees = 30f;
        [Tooltip("Metres driven while merging from the grid lane onto the racing line. Distance, not " +
                 "time, because the countdown would otherwise burn the merge while the car is frozen.")]
        [SerializeField] private float laneMergeDistance = 140f;
        [Tooltip("Largest grid lane offset kept from the spawn position.")]
        [SerializeField] private float maxLaneOffset = 6f;
        [Tooltip("Small per-car offset kept for the whole race so the pack does not share one line.")]
        [SerializeField] private float laneJitter = 1.2f;
        [Tooltip("Widest half-width probed when turning a waypoint into a track cross-section line.")]
        [SerializeField] private float maxRoadHalfWidth = 13f;
        [Tooltip("Metres kept clear of the measured road edge.")]
        [SerializeField] private float roadEdgeMargin = 2.2f;
        [SerializeField] private float steerGainLowSpeed = 1.5f;
        [SerializeField] private float steerGainHighSpeed = 0.55f;
        [Tooltip("Damps the steering rate so the car stops sawing at the wheel.")]
        [SerializeField] private float steerDamping = 0.06f;
        [SerializeField] private float stuckSpeed = 1.5f;
        [SerializeField] private float stuckDelay = 2.5f;
        [SerializeField] private float reverseDuration = 1.25f;

        private RCCP_CarController carController;
        private Rigidbody carRigidbody;
        private RCCP_Input inputReceiver;
        private RCCP_Inputs inputs;
        private GmtkRccpWaypointPath path;
        private int waypointIndex;
        private float throttleScale = 0.9f;
        private float previousSteerAngle;
        private float gridLaneOffset;
        private float mergeTravelled;
        private float personalLaneOffset;
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

        public void Initialize(GmtkRccpWaypointPath waypointPath)
        {
            path = waypointPath;

            if (path == null || path.Count == 0)
                return;

            waypointIndex = path.FindClosestIndex(transform.position);

            // Every car shares one waypoint ring, so without this they all aim at the same centre
            // line point and dive into the middle of the track the moment the race starts. Keep the
            // grid lane and merge onto the racing line over the first seconds instead.
            gridLaneOffset = Mathf.Clamp(
                path.SignedLateralOffset(waypointIndex, transform.position),
                -maxLaneOffset,
                maxLaneOffset);
            mergeTravelled = 0f;

            // deterministic per-car spread so the pack does not stack on one line afterwards
            personalLaneOffset = ((GetInstanceID() % 5) - 2) * 0.5f * laneJitter;
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

        private void FixedUpdate()
        {
            if (path == null || path.Count == 0 || inputReceiver == null)
                return;

            float speedKph = carRigidbody != null ? carRigidbody.linearVelocity.magnitude * 3.6f : 0f;
            mergeTravelled += speedKph / 3.6f * Time.fixedDeltaTime;

            AdvanceWaypoint();

            // aim further ahead the faster we go, so corners are entered on a line instead of
            // being noticed once the marker is already alongside the car
            float lookAhead = minLookAhead + speedKph * lookAheadPerKph;
            Vector3 aimPoint = GetRacingLinePoint(lookAhead);
            aimPoint += GetPersonalityOffset(aimPoint);

            Vector3 localTarget = transform.InverseTransformPoint(aimPoint);
            float targetAngle = Mathf.Atan2(localTarget.x, Mathf.Max(1f, localTarget.z)) * Mathf.Rad2Deg;

            UpdateRecovery();

            if (reverseTimer > 0f)
            {
                reverseTimer -= Time.fixedDeltaTime;
                inputs.throttleInput = 0f;
                inputs.brakeInput = 1f;
                inputs.steerInput = -Mathf.Sign(targetAngle);
            }
            else
            {
                // corner speed comes from the path shape ahead, not from the current error, so the
                // car brakes before the corner
                float headingChange = path.HeadingChangeAhead(waypointIndex, cornerScanDistance);
                float tightness = Mathf.Clamp01(headingChange / Mathf.Max(1f, tightCornerDegrees));
                float targetSpeed = Mathf.Lerp(straightSpeedKph, tightCornerSpeedKph, tightness) * throttleScale;
                float speedError = targetSpeed - speedKph;

                inputs.throttleInput = Mathf.Clamp01(speedError / 12f);
                inputs.brakeInput = speedError < -4f ? Mathf.Clamp01(-speedError / 22f) : 0f;

                // steering authority falls off with speed, and the steering rate is damped
                float steerGain = Mathf.Lerp(
                    steerGainLowSpeed,
                    steerGainHighSpeed,
                    Mathf.Clamp01(speedKph / straightSpeedKph));
                float angleRate = (targetAngle - previousSteerAngle) / Time.fixedDeltaTime;
                inputs.steerInput = Mathf.Clamp(
                    (targetAngle * steerGain - angleRate * steerDamping) / 35f,
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
        /// Places the aim point on a racing line rather than on the centre line: pushed toward the
        /// inside of the corner it is entering, and widened to the outside while a sharper corner is
        /// still ahead. The offset is dropped when there is no ground under it, which keeps the line
        /// off the run-off and off the crossover bridge edge.
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
            float reference = Mathf.Max(1f, racingLineReferenceDegrees);

            // inside of the corner being entered
            float apexAmount = Mathf.Clamp01(Mathf.Abs(turnDegrees) / reference) * apexOffset;
            Vector3 offset = pathRight * Mathf.Sign(turnDegrees) * apexAmount;

            // widen to the outside while the sharp part is still further ahead
            if (Mathf.Abs(farTurnDegrees) > Mathf.Abs(turnDegrees))
            {
                float entryAmount = Mathf.Clamp01(Mathf.Abs(farTurnDegrees) / reference) * entryOffset;
                offset -= pathRight * Mathf.Sign(farTurnDegrees) * entryAmount;
            }

            // grid lane fades out over driven distance, the small personal offset stays
            float mergeBlend = laneMergeDistance > 0f
                ? 1f - Mathf.Clamp01(mergeTravelled / laneMergeDistance)
                : 0f;
            float lateral = Vector3.Dot(offset, pathRight)
                            + gridLaneOffset * mergeBlend
                            + personalLaneOffset;

            // the waypoint is used as a track cross-section line: clamp the chosen position to the
            // measured road, never fall back to the centre line
            MeasureRoadEdges(aimPoint, pathRight);
            lateral = Mathf.Clamp(lateral, -edgeLimitLeft, edgeLimitRight);

            return aimPoint + pathRight * lateral;
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
            edgeLimitRight = Mathf.Max(0f, ProbeEdge(centre, pathRight) - roadEdgeMargin);
            edgeLimitLeft = Mathf.Max(0f, ProbeEdge(centre, -pathRight) - roadEdgeMargin);
        }

        private float ProbeEdge(Vector3 centre, Vector3 direction)
        {
            // a barrier is the hard limit
            if (Physics.Raycast(centre + Vector3.up * 1.2f, direction, out RaycastHit hit, maxRoadHalfWidth))
                return hit.distance;

            // otherwise the surface edge: the track has no terrain beside it, so the last sample
            // with ground under it is the edge
            for (float distance = 2f; distance <= maxRoadHalfWidth; distance += 2f)
            {
                Vector3 probe = centre + direction * distance + Vector3.up * 3f;

                if (!Physics.Raycast(probe, Vector3.down, 8f))
                    return distance - 2f;
            }

            return maxRoadHalfWidth;
        }

        /// <summary>
        /// Consumes waypoints that are reached or already behind the car, so the racing line can cut
        /// a corner instead of driving to every marker, and a missed marker does not turn the car
        /// around.
        /// </summary>
        private void AdvanceWaypoint()
        {
            float reachSqr = waypointReachDistance * waypointReachDistance;
            float behindLimitSqr = waypointReachDistance * 3f * (waypointReachDistance * 3f);

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

        private void UpdateRecovery()
        {
            if (carController.absoluteSpeed <= stuckSpeed)
                stuckTimer += Time.fixedDeltaTime;
            else
                stuckTimer = 0f;

            if (stuckTimer < stuckDelay)
                return;

            stuckTimer = 0f;
            reverseTimer = reverseDuration;
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
