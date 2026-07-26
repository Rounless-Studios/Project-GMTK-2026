using UnityEngine;
using Gmtk2026.GameBalance;
using System.Collections.Generic;

namespace GMTK
{
    /// <summary>
    /// Per-car durability actuation (checklist stage 7). Wraps the settings-driven
    /// <see cref="DurabilityState"/>: takes damage from strong collisions, rupture curses and
    /// hazards, loses control while wrecked, and recovers to a partial durability after
    /// <c>wreckDurationSeconds</c> with a short protection window. A wreck never eliminates the
    /// car — elimination stays the EliminationManager / FinalGate's job, so a wrecked last-place
    /// car is still executed on schedule.
    /// A wreck cuts the driver's input but leaves the car in the world: the impact that emptied
    /// the durability is handed back scaled up (<see cref="WreckLaunch"/>), so the car is thrown
    /// out of control and tumbles on instead of stopping dead where it was hit.
    /// </summary>
    [DisallowMultipleComponent]
    public class DurabilityController : MonoBehaviour
    {
        public DurabilityState State { get; private set; }
        public DamageStage Stage => State != null ? State.Stage : DamageStage.Pristine;
        public bool IsWrecked => State != null && State.IsWrecked;
        public bool IsProtected => State != null && State.IsProtected;
        public float Durability => State != null ? State.Durability : 0f;

        // static so a single HUD / VFX driver can listen for every car
        public static event System.Action<DurabilityController, DamageStage> StageChanged;
        public static event System.Action<DurabilityController> Wrecked;
        public static event System.Action<DurabilityController> Recovered;

        private DamageSettings D => GameBalance.Current.damage;
        private bool controlsSuspended;
        private GmtkVehicleAdapter vehicleAdapter;

        // the collision being processed right now, so a wreck it causes can throw the car with it
        private bool hasPendingImpact;
        private Vector3 pendingImpactPush;
        private float pendingImpactSpeed;

        [Header("Wreck Recovery")]
        [Tooltip("Height the car is lifted when a wreck that landed it on its roof is rolled back " +
                 "over, so it does not recover inside the road surface.")]
        [SerializeField, Min(0f)] private float uprightLiftMetres = 0.6f;

        [Header("Collision Sparks")]
        [SerializeField, Min(0f)] private float minimumSparkCollisionSpeed = 3f;
        [SerializeField, Min(0f)] private float sparkCooldownSeconds = 0.15f;

        [Header("Drift Sparks")]
        [SerializeField, Min(0f)] private float minimumDriftSpeed = 8f;
        [SerializeField, Min(0f)] private float minimumDriftSidewaysSlip = 0.2f;
        [SerializeField, Min(0f)] private float driftSparkCooldownSeconds = 0.18f;
        [SerializeField, Range(0.1f, 1f)] private float driftSparkScale = 0.45f;

        private const string CollisionSparkResourcePath = "VFX/Spark";
        private static GameObject collisionSparkPrefab;
        private static bool collisionSparkLoadFailed;
        private float nextSparkTime;
        private float nextDriftSparkTime;
        private Rigidbody vehicleBody;
        private WheelCollider[] rearWheelColliders;

        private void Awake()
        {
            vehicleAdapter = GetComponent<GmtkVehicleAdapter>();
            vehicleBody = GetComponent<Rigidbody>();
            CacheRearWheelColliders();
            Build();
        }

        private void Build()
        {
            State = new DurabilityState(D);
            State.StageChanged += s => StageChanged?.Invoke(this, s);
            State.Wrecked += OnWrecked;
            State.Recovered += OnRecovered;
        }

        /// <summary>Restore full durability and control for a fresh race.</summary>
        public void ResetForRace()
        {
            RestoreControls();
            Build();
        }

        /// <summary>Damage entry point for strong collisions, rupture curses and hazards.</summary>
        public void ApplyDamage(float amount) => State?.ApplyDamage(amount);

        private void Update()
        {
            State?.Tick(Time.deltaTime);
            TryPlayDriftSpark();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (State == null) return;

            TryPlayCollisionSpark(collision);

            // light bumps are free; only a strong impulse hurts
            if (collision.impulse.magnitude < D.strongCollisionImpulse)
                return;

            RememberImpact(collision);
            State.ApplyDamage(D.strongCollisionDamage);
            hasPendingImpact = false;
        }

        /// <summary>
        /// Records where this hit shoves the car and how hard, for the wreck launch. The contact
        /// normals point away from whatever was hit, so their sum is the push direction.
        /// </summary>
        private void RememberImpact(Collision collision)
        {
            pendingImpactPush = Vector3.zero;

            for (int i = 0; i < collision.contactCount; i++)
                pendingImpactPush += collision.GetContact(i).normal;

            pendingImpactSpeed = collision.relativeVelocity.magnitude;
            hasPendingImpact = true;
        }

        private void TryPlayCollisionSpark(Collision collision)
        {
            if (collision == null ||
                collision.contactCount == 0 ||
                collision.relativeVelocity.magnitude < minimumSparkCollisionSpeed ||
                Time.time < nextSparkTime)
            {
                return;
            }

            // A car-to-car impact invokes OnCollisionEnter on both cars. Let only one side
            // spawn the shared contact effect so the same hit does not create two bursts.
            DurabilityController otherCar =
                collision.collider.GetComponentInParent<DurabilityController>();

            if (otherCar != null && GetInstanceID() > otherCar.GetInstanceID())
                return;

            if (!EnsureCollisionSparkPrefab())
                return;

            ContactPoint contact = collision.GetContact(0);
            SpawnSpark(contact.point, contact.normal, 1f);
            nextSparkTime = Time.time + sparkCooldownSeconds;
        }

        private void TryPlayDriftSpark()
        {
            if (vehicleAdapter == null ||
                !vehicleAdapter.IsPlayer ||
                vehicleBody == null ||
                rearWheelColliders == null ||
                rearWheelColliders.Length == 0 ||
                !Input.GetKey(KeyCode.Space) ||
                vehicleBody.linearVelocity.magnitude < minimumDriftSpeed ||
                Time.time < nextDriftSparkTime)
            {
                return;
            }

            bool foundSlidingRearWheel = false;
            WheelHit strongestHit = default;
            float strongestSlip = minimumDriftSidewaysSlip;

            foreach (WheelCollider wheel in rearWheelColliders)
            {
                if (wheel == null)
                    continue;

                if (!wheel.GetGroundHit(out WheelHit hit))
                    continue;

                float slip = Mathf.Abs(hit.sidewaysSlip);

                if (slip < strongestSlip)
                    continue;

                strongestSlip = slip;
                strongestHit = hit;
                foundSlidingRearWheel = true;
            }

            if (!foundSlidingRearWheel || !EnsureCollisionSparkPrefab())
                return;

            SpawnSpark(
                strongestHit.point,
                strongestHit.normal,
                driftSparkScale);
            nextDriftSparkTime = Time.time + driftSparkCooldownSeconds;
        }

        private void CacheRearWheelColliders()
        {
            WheelCollider[] wheels = GetComponentsInChildren<WheelCollider>(true);

            if (wheels.Length <= 2)
            {
                rearWheelColliders = wheels;
                return;
            }

            float minimumZ = float.PositiveInfinity;
            float maximumZ = float.NegativeInfinity;

            foreach (WheelCollider wheel in wheels)
            {
                float localZ =
                    transform.InverseTransformPoint(wheel.transform.position).z;
                minimumZ = Mathf.Min(minimumZ, localZ);
                maximumZ = Mathf.Max(maximumZ, localZ);
            }

            float axleMidpoint = (minimumZ + maximumZ) * 0.5f;
            var rearWheels = new List<WheelCollider>();

            foreach (WheelCollider wheel in wheels)
            {
                float localZ =
                    transform.InverseTransformPoint(wheel.transform.position).z;

                if (localZ <= axleMidpoint)
                    rearWheels.Add(wheel);
            }

            rearWheelColliders = rearWheels.ToArray();
        }

        private bool EnsureCollisionSparkPrefab()
        {
            if (collisionSparkPrefab == null && !collisionSparkLoadFailed)
            {
                collisionSparkPrefab =
                    Resources.Load<GameObject>(CollisionSparkResourcePath);
                collisionSparkLoadFailed = collisionSparkPrefab == null;

                if (collisionSparkLoadFailed)
                {
                    Debug.LogError(
                        $"Collision spark prefab was not found at Resources/{CollisionSparkResourcePath}.",
                        this);
                }
            }

            return collisionSparkPrefab != null;
        }

        private void SpawnSpark(Vector3 point, Vector3 normal, float scale)
        {
            Quaternion rotation =
                Quaternion.FromToRotation(Vector3.forward, normal);
            GameObject effect = Instantiate(
                collisionSparkPrefab,
                point + normal * 0.02f,
                rotation);
            effect.transform.localScale *= scale;
            ParticleSystem particles = effect.GetComponentInChildren<ParticleSystem>();
            float destroyDelay = 2f;

            if (particles != null)
            {
                ParticleSystem.MainModule main = particles.main;
                destroyDelay = Mathf.Max(
                    1f,
                    main.startDelay.constantMax + main.startLifetime.constantMax + 0.5f);
            }

            Destroy(effect, destroyDelay);
        }

        private void OnWrecked()
        {
            SuspendControls();
            ThrowWreckedCar();
            Wrecked?.Invoke(this);
        }

        /// <summary>
        /// Hands the wrecking impact back, scaled up, so the car is thrown out of control. A wreck
        /// with no collision behind it (rupture curse, hazard damage) gets the minimum launch
        /// straight up instead, which still reads as the car breaking loose.
        /// </summary>
        private void ThrowWreckedCar()
        {
            if (vehicleBody == null || vehicleBody.isKinematic)
                return;

            Vector3 launch = WreckLaunch.Compute(
                D,
                hasPendingImpact ? pendingImpactPush : Vector3.zero,
                hasPendingImpact ? pendingImpactSpeed : 0f);

            vehicleBody.AddForce(launch, ForceMode.VelocityChange);

            if (D.wreckSpinRadiansPerSecond > 0f)
                vehicleBody.angularVelocity += Random.onUnitSphere * D.wreckSpinRadiansPerSecond;
        }

        private void OnRecovered()
        {
            UprightIfFlipped();
            RestoreControls();
            Recovered?.Invoke(this);
        }

        private void SuspendControls()
        {
            if (controlsSuspended) return;
            controlsSuspended = true;

            // input only: the bodies keep simulating, which is what lets the wreck tumble on
            // instead of freezing where it was hit
            if (vehicleAdapter != null)
                vehicleAdapter.SetControlsEnabled(false);
        }

        private void RestoreControls()
        {
            if (!controlsSuspended) return;
            controlsSuspended = false;

            if (vehicleAdapter != null)
                vehicleAdapter.SetControlsEnabled(true);
        }

        /// <summary>
        /// A thrown car can land on its roof and nothing in the vehicle package rolls it back over,
        /// so a recovered car would be stranded for the rest of the race. Levels it in place,
        /// keeping its heading.
        /// </summary>
        private void UprightIfFlipped()
        {
            if (vehicleBody == null ||
                Vector3.Dot(transform.up, Vector3.up) >= D.wreckUprightMinimumUpDot)
            {
                return;
            }

            Vector3 heading = transform.forward;
            heading.y = 0f;

            if (heading.sqrMagnitude < 1e-4f)
                heading = Vector3.forward;

            Quaternion rotation = Quaternion.LookRotation(heading.normalized, Vector3.up);
            Vector3 position = transform.position + Vector3.up * uprightLiftMetres;

            vehicleBody.angularVelocity = Vector3.zero;
            vehicleBody.position = position;
            vehicleBody.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
        }
    }
}
