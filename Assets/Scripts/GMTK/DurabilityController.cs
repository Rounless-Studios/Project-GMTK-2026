using UnityEngine;
using SpinMotion;
using Gmtk2026.GameBalance;

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

        private void Awake() => Build();

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
            foreach (var ai in GetComponentsInChildren<CarAIControl>(true)) ai.enabled = false;
            foreach (var user in GetComponentsInChildren<CarUserControl>(true)) user.enabled = false;
        }

        private void RestoreControls()
        {
            if (!controlsSuspended) return;
            controlsSuspended = false;
            foreach (var ai in GetComponentsInChildren<CarAIControl>(true)) ai.enabled = true;
            foreach (var user in GetComponentsInChildren<CarUserControl>(true)) user.enabled = true;
        }
    }
}
