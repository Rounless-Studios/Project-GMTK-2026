using Gmtk2026.GameBalance;
using UnityEngine;

namespace GMTK.Rccp
{
    /// <summary>
    /// RCCP implementation of the vehicle-package boundary used by GMTK gameplay systems.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RCCP_CarController))]
    public sealed class GmtkRccpVehicle : GmtkVehicleAdapter
    {
        private RCCP_CarController carController;
        private GmtkRccpWaypointDriver aiDriver;
        private GmtkRccpFallRespawner fallRespawner;
        private Rigidbody carRigidbody;
        private bool isPlayer;
        private bool controlsEnabled = true;
        private bool subscribedToFreeze;
        private bool frozen;
        private RigidbodyConstraints constraintsBeforeFreeze;

        public override bool IsPlayer => isPlayer;

        private void Awake()
        {
            carController = GetComponent<RCCP_CarController>();
            carRigidbody = GetComponent<Rigidbody>();
            constraintsBeforeFreeze = carRigidbody != null
                ? carRigidbody.constraints
                : RigidbodyConstraints.None;
        }

        private void OnDestroy()
        {
            if (subscribedToFreeze && Race.Events != null)
            {
                Race.Events.ToggleCarFreezeEvent.RemoveListener(OnToggleFreeze);
                Race.Events.RaceStartedEvent.RemoveListener(OnRaceStarted);
                Race.Events.RestartRaceEvent.RemoveListener(OnRestartRace);
            }
        }

        public void Initialize(bool player, GmtkRccpWaypointPath waypointPath, int raceIndex)
        {
            isPlayer = player;

            if (player)
            {
                carController.externalControl = false;
                carController.SetCanControl(true);
                carController.StartEngine();

                BindAsPlayer();
            }
            else
            {
                aiDriver = GetComponent<GmtkRccpWaypointDriver>();

                if (aiDriver == null)
                    aiDriver = gameObject.AddComponent<GmtkRccpWaypointDriver>();

                aiDriver.Initialize(waypointPath, raceIndex);
                carController.externalControl = true;
                carController.SetCanControl(true);
                carController.StartEngine();
            }

            fallRespawner = GetComponent<GmtkRccpFallRespawner>();

            if (fallRespawner == null)
                fallRespawner = gameObject.AddComponent<GmtkRccpFallRespawner>();

            fallRespawner.Initialize(waypointPath);

            if (!subscribedToFreeze && Race.Events != null)
            {
                Race.Events.ToggleCarFreezeEvent.AddListener(OnToggleFreeze);
                Race.Events.RaceStartedEvent.AddListener(OnRaceStarted);
                Race.Events.RestartRaceEvent.AddListener(OnRestartRace);
                subscribedToFreeze = true;
            }

            // Cars are spawned when Play is clicked, but the flow still has a prologue and a countdown
            // to show, and the freeze event that covers those was already broadcast before this vehicle
            // existed to hear it. A car that arrives before the lights go out starts frozen instead.
            if (!Race.IsRaceInProgress) OnToggleFreeze(true);
        }

        /// <summary>
        /// Claims the player slot: RCCP's scene manager hands the chase camera to whichever vehicle
        /// registered last, so the grid spawner calls this again once every AI car exists.
        /// </summary>
        public void BindAsPlayer()
        {
            // The facade also hooks up the chase camera and UI, unlike calling the scene manager
            // directly, and it creates the manager when the scene has none.
            RCCP.RegisterPlayerVehicle(carController, true, true);
            RCCP.SetControl(carController, true);

            AttachChaseCamera();
        }

        /// <summary>
        /// RegisterPlayer only reaches the camera when an RCCP_SceneManager exists in the scene,
        /// so the chase camera is targeted directly too. Without a target it stays at its authored
        /// position, which reads as a camera buried in the track.
        /// </summary>
        private void AttachChaseCamera()
        {
            RCCP_Camera chaseCamera = FindAnyObjectByType<RCCP_Camera>(FindObjectsInactive.Include);

            if (chaseCamera == null)
            {
                Debug.LogError("RCCP: no RCCP_Camera in the scene, the player has no chase camera.", this);
                return;
            }

            chaseCamera.gameObject.SetActive(true);
            chaseCamera.SetTarget(carController);
            chaseCamera.ToggleCamera(true);

            if (chaseCamera.actualCamera != null)
                chaseCamera.actualCamera.enabled = true;

            RCCP_SceneManager sceneManager = RCCP_SceneManager.Instance;

            if (sceneManager != null)
            {
                sceneManager.activePlayerVehicle = carController;
                sceneManager.activePlayerCamera = chaseCamera;

                if (chaseCamera.actualCamera != null)
                    sceneManager.activeMainCamera = chaseCamera.actualCamera;
            }

            Debug.Log("RCCP camera attached to " + carController.name
                + " (target=" + (chaseCamera.cameraTarget != null && chaseCamera.cameraTarget.playerVehicle != null
                    ? chaseCamera.cameraTarget.playerVehicle.name : "NULL")
                + ", rendering=" + chaseCamera.IsCameraActive() + ")");
        }

        /// <summary>
        /// The kit releases the countdown freeze with ToggleCarFreezeEvent(false). If that event is
        /// missed the car stays constrained and never moves, so the race start clears it as well.
        /// </summary>
        private void OnRaceStarted()
        {
            if (carRigidbody != null)
                carRigidbody.constraints = RigidbodyConstraints.None;

            // released here rather than through the freeze path, so record it: otherwise the next
            // pre-race freeze would be skipped as redundant and the restart grid could drive away
            frozen = false;
            fallRespawner?.ResetTracking();
        }

        private void OnRestartRace() => fallRespawner?.ResetTracking();

        public override void SetControlsEnabled(bool enabled)
        {
            controlsEnabled = enabled;
            carController.SetCanControl(enabled);

            if (aiDriver != null)
                aiDriver.enabled = enabled;
        }

        public override void ApplyBoost(float speedMultiplier)
        {
            if (!controlsEnabled || carRigidbody == null || speedMultiplier <= 1f)
                return;

            float acceleration =
                (speedMultiplier - 1f) * GameBalance.Current.vehicle.boostAcceleration;
            carRigidbody.AddForce(transform.forward * acceleration, ForceMode.Acceleration);
        }

        private void FixedUpdate()
        {
            if (carRigidbody == null || !controlsEnabled || frozen) return;

            VehicleSettings settings = GameBalance.Current.vehicle;
            float speedKph = carRigidbody.linearVelocity.magnitude * 3.6f;
            bool drifting = isPlayer
                && speedKph >= settings.driftMinimumSpeedKph
                && carController.handbrakeInput_V > 0.1f;
            if (speedKph >= settings.stabilizationMinimumSpeedKph)
            {
                Vector3 localVelocity = transform.InverseTransformDirection(
                    carRigidbody.linearVelocity);
                float grip = settings.lateralGripRecovery
                    * (drifting ? settings.driftGripMultiplier : 1f);
                carRigidbody.AddForce(
                    -transform.right * localVelocity.x * grip,
                    ForceMode.Acceleration);

                Vector3 angular = carRigidbody.angularVelocity;
                float maximumYaw = settings.maximumYawRadiansPerSecond
                    * (drifting ? settings.driftMaximumYawMultiplier : 1f);
                angular.y = Mathf.Clamp(
                    angular.y,
                    -maximumYaw,
                    maximumYaw);
                carRigidbody.angularVelocity = angular;
                float yawDamping = settings.yawDamping
                    * (drifting ? settings.driftYawDampingMultiplier : 1f);
                carRigidbody.AddTorque(
                    -Vector3.up * angular.y * yawDamping,
                    ForceMode.Acceleration);
            }

            if (isPlayer && OvertakeManager.Instance != null &&
                OvertakeManager.Instance.IsBound)
            {
                float forwardSpeed = Vector3.Dot(
                    carRigidbody.linearVelocity,
                    transform.forward);
                if (forwardSpeed > 0f)
                    carRigidbody.AddForce(
                        -transform.forward *
                        settings.bindingDeceleration *
                        (1f - OvertakeManager.Instance.BindSpeedMultiplier),
                        ForceMode.Acceleration);
            }
        }

        public override void ApplyForwardImpulse(float force)
        {
            if (carRigidbody != null)
                carRigidbody.AddForce(transform.forward * force, ForceMode.VelocityChange);
        }

        public override void ApplyWorldImpulse(Vector3 impulse)
        {
            if (carRigidbody != null)
                carRigidbody.AddForce(impulse, ForceMode.VelocityChange);
        }

        public override void ConfigureAiPersonality(AIPersonalityType type)
        {
            if (aiDriver != null)
                aiDriver.ConfigurePersonality(type);
        }

        public override void ApplyAiTargeting(
            AIPersonalityType type,
            Transform player,
            float lateralStrength,
            float aggroRange)
        {
            if (aiDriver != null)
                aiDriver.SetPersonalityTarget(type, player, lateralStrength, aggroRange);
        }

        private void OnToggleFreeze(bool freeze)
        {
            if (carRigidbody == null || freeze == frozen)
                return;

            if (freeze)
            {
                // only the first freeze may record the constraints: a second one would record the
                // frozen constraints as the originals and the car would never be released again
                constraintsBeforeFreeze = carRigidbody.constraints;
                carRigidbody.constraints =
                    RigidbodyConstraints.FreezePositionX |
                    RigidbodyConstraints.FreezePositionZ |
                    RigidbodyConstraints.FreezeRotation;
            }
            else
            {
                carRigidbody.constraints = constraintsBeforeFreeze;
            }

            frozen = freeze;
        }
    }
}
