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

        private void Awake()
        {
            vehicleAdapter = GetComponent<GmtkVehicleAdapter>();
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

        private void Update() => State?.Tick(Time.deltaTime);

        private void OnCollisionEnter(Collision collision)
        {
            if (State == null) return;
            // light bumps are free; only a strong impulse hurts
            if (collision.impulse.magnitude >= D.strongCollisionImpulse)
                State.ApplyDamage(D.strongCollisionDamage);
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
