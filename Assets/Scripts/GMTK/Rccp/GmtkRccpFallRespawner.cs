using Gmtk2026.GameBalance;
using UnityEngine;

namespace GMTK.Rccp
{
    /// <summary>
    /// Tracks the last supported position of one RCCP car. When the car falls below that position,
    /// it is placed on the closest AI waypoint and aligned with the path.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GmtkRccpFallRespawner : MonoBehaviour
    {
        private readonly FallRespawnState state = new();
        private readonly RaycastHit[] groundHits = new RaycastHit[8];

        private Rigidbody carRigidbody;
        private GmtkRccpWaypointDriver aiDriver;
        private GmtkRccpWaypointPath path;
        private float sampleTimer;
        private bool initialized;

        public Vector3 LastTrackPosition => state.LastTrackPosition;
        public int RespawnCount { get; private set; }

        private VehicleRecoverySettings Settings => GameBalance.Current.vehicleRecovery;

        private void Awake()
        {
            carRigidbody = GetComponent<Rigidbody>();
            aiDriver = GetComponent<GmtkRccpWaypointDriver>();
        }

        public void Initialize(GmtkRccpWaypointPath waypointPath)
        {
            path = waypointPath;
            aiDriver = GetComponent<GmtkRccpWaypointDriver>();
            initialized = path != null && path.Count > 0 && carRigidbody != null;

            if (initialized)
                ResetTracking();
        }

        /// <summary>
        /// The grid spawner calls this after a race restart so an old lap's safe position cannot
        /// pull the freshly reset car back across the map.
        /// </summary>
        public void ResetTracking()
        {
            state.RecordTrackPosition(transform.position);
            sampleTimer = 0f;
        }

        private void FixedUpdate()
        {
            if (!initialized)
                return;

            VehicleRecoverySettings settings = Settings;
            sampleTimer -= Time.fixedDeltaTime;

            if (sampleTimer <= 0f)
            {
                sampleTimer = settings.trackSampleIntervalSeconds;

                if (IsSupportedByTrack(transform.position, settings))
                    state.RecordTrackPosition(transform.position);
            }

            if (state.ShouldRespawn(transform.position, settings.fallDistanceBelowTrack))
                RespawnAtLastTrackWaypoint(settings);
        }

        private bool IsSupportedByTrack(Vector3 position, VehicleRecoverySettings settings)
        {
            int waypointIndex = path.FindClosestIndex(position);
            Vector3 toWaypoint = path[waypointIndex].position - position;
            toWaypoint.y = 0f;

            if (toWaypoint.sqrMagnitude >
                settings.maximumTrackDistanceFromWaypoint
                * settings.maximumTrackDistanceFromWaypoint)
            {
                return false;
            }

            Vector3 rayOrigin = position + Vector3.up * 0.5f;
            int hitCount = Physics.RaycastNonAlloc(
                rayOrigin,
                Vector3.down,
                groundHits,
                settings.groundProbeDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = groundHits[i].collider;

                if (hitCollider == null || hitCollider.transform.root == transform.root)
                    continue;

                // Track, kerbs and barriers are static. Dynamic rigidbodies are other cars or
                // hazards and must not turn an airborne car into a new safe position.
                if (hitCollider.attachedRigidbody == null)
                    return true;
            }

            return false;
        }

        private void RespawnAtLastTrackWaypoint(VehicleRecoverySettings settings)
        {
            if (!path.TryGetRespawnPose(
                    state.LastTrackPosition,
                    settings.respawnHeightAboveWaypoint,
                    out int waypointIndex,
                    out Vector3 position,
                    out Quaternion rotation))
            {
                return;
            }

            carRigidbody.linearVelocity = Vector3.zero;
            carRigidbody.angularVelocity = Vector3.zero;
            carRigidbody.position = position;
            carRigidbody.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
            carRigidbody.WakeUp();

            aiDriver?.ResetAfterRespawn(waypointIndex);
            state.RecordTrackPosition(position);
            sampleTimer = settings.trackSampleIntervalSeconds;
            RespawnCount++;

            Debug.Log(
                $"RCCP fall respawn: {name} -> waypoint {waypointIndex} at {position:F1}",
                this);
        }
    }
}
