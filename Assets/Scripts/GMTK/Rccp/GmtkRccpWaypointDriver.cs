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
        [SerializeField] private float steeringSensitivity = 1.4f;
        [SerializeField] private float cornerBrakeAngle = 55f;
        [SerializeField] private float stuckSpeed = 1.5f;
        [SerializeField] private float stuckDelay = 2.5f;
        [SerializeField] private float reverseDuration = 1.25f;

        private RCCP_CarController carController;
        private RCCP_Input inputReceiver;
        private RCCP_Inputs inputs;
        private GmtkRccpWaypointPath path;
        private int waypointIndex;
        private float throttleScale = 0.9f;
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

            if (path != null && path.Count > 0)
                waypointIndex = path.FindClosestIndex(transform.position);
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

            Vector3 waypointPosition = path[waypointIndex].position;
            Vector3 toWaypoint = waypointPosition - transform.position;

            if (toWaypoint.sqrMagnitude <= waypointReachDistance * waypointReachDistance)
            {
                waypointIndex = (waypointIndex + 1) % path.Count;
                waypointPosition = path[waypointIndex].position;
            }

            waypointPosition += GetPersonalityOffset(waypointPosition);
            Vector3 localTarget = transform.InverseTransformPoint(waypointPosition);
            float targetAngle = Mathf.Atan2(localTarget.x, Mathf.Max(1f, localTarget.z)) * Mathf.Rad2Deg;
            float absoluteAngle = Mathf.Abs(targetAngle);

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
                float cornerFactor = Mathf.InverseLerp(90f, 0f, absoluteAngle);
                inputs.throttleInput = Mathf.Clamp01(cornerFactor * throttleScale);
                inputs.brakeInput = absoluteAngle >= cornerBrakeAngle
                    ? Mathf.InverseLerp(cornerBrakeAngle, 100f, absoluteAngle)
                    : 0f;
                inputs.steerInput = Mathf.Clamp(
                    targetAngle / 45f * steeringSensitivity,
                    -1f,
                    1f);
            }

            inputs.handbrakeInput = 0f;
            inputs.clutchInput = 0f;
            inputs.nosInput = personalityType == AIPersonalityType.Reckless ? 0.35f : 0f;
            inputReceiver.OverrideInputs(inputs);
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
