using UnityEngine;
using SpinMotion;
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
        private readonly List<Behaviour> suspendedControls = new();
        private Rigidbody[] suspendedBodies;
        private bool[] originalKinematicStates;

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
            if (collision.impulse.magnitude >= D.strongCollisionImpulse)
                State.ApplyDamage(D.strongCollisionDamage);
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
            ParticleSystem particles =
                collisionSparkPrefab.GetComponentInChildren<ParticleSystem>();
            float destroyDelay = 2f;

            if (particles != null)
            {
                ParticleSystem.MainModule main = particles.main;
                destroyDelay = Mathf.Max(
                    1f,
                    main.startDelay.constantMax + main.startLifetime.constantMax + 0.5f);
            }

            Vector3 effectScale = collisionSparkPrefab.transform.localScale * scale;
            if (PooledEffectService.Instance != null)
            {
                PooledEffectService.Instance.Play(
                    collisionSparkPrefab,
                    point + normal * 0.02f,
                    rotation,
                    effectScale,
                    destroyDelay);
            }
            else
            {
                GameObject effect = Instantiate(
                    collisionSparkPrefab,
                    point + normal * 0.02f,
                    rotation);
                effect.transform.localScale = effectScale;
                Destroy(effect, destroyDelay);
            }
        }

        private void OnWrecked()
        {
            SuspendControls();
            Wrecked?.Invoke(this);
        }

        private void OnRecovered()
        {
            RestoreControls();
            Recovered?.Invoke(this);
        }

        private void SuspendControls()
        {
            if (controlsSuspended) return;
            controlsSuspended = true;
            suspendedControls.Clear();

            // the vehicle package cuts its own input through the adapter; kit cars fall back to
            // switching their controllers off
            if (vehicleAdapter != null)
            {
                vehicleAdapter.SetControlsEnabled(false);
            }
            else
            {
                SuspendEnabled(GetComponentsInChildren<CarAIControl>(true));
                SuspendEnabled(GetComponentsInChildren<CarUserControl>(true));
            }

            // freezing the bodies as well guarantees that cached throttle cannot keep moving the
            // wrecked car, whichever vehicle package drives it
            suspendedBodies = GetComponentsInChildren<Rigidbody>(true);
            originalKinematicStates = new bool[suspendedBodies.Length];
            for (int i = 0; i < suspendedBodies.Length; i++)
            {
                Rigidbody body = suspendedBodies[i];
                originalKinematicStates[i] = body.isKinematic;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
        }

        private void RestoreControls()
        {
            if (!controlsSuspended) return;
            controlsSuspended = false;

            if (suspendedBodies != null && originalKinematicStates != null)
            {
                int count = Mathf.Min(suspendedBodies.Length, originalKinematicStates.Length);
                for (int i = 0; i < count; i++)
                {
                    if (suspendedBodies[i] != null)
                        suspendedBodies[i].isKinematic = originalKinematicStates[i];
                }
            }

            suspendedBodies = null;
            originalKinematicStates = null;

            if (vehicleAdapter != null)
            {
                vehicleAdapter.SetControlsEnabled(true);
            }
            else
            {
                foreach (Behaviour control in suspendedControls)
                {
                    if (control != null) control.enabled = true;
                }
            }

            suspendedControls.Clear();
        }

        private void SuspendEnabled<T>(T[] controls) where T : Behaviour
        {
            foreach (T control in controls)
            {
                if (control == null || !control.enabled) continue;
                control.enabled = false;
                suspendedControls.Add(control);
            }
        }
    }
}
