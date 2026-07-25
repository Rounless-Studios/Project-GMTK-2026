using UnityEngine;

namespace Gmtk2026.GameBalance
{
    /// <summary>Visible damage stage, derived from the durability thresholds.</summary>
    public enum DamageStage { Pristine, Damaged, Critical, Wrecked }

    /// <summary>
    /// Settings-driven durability rule (checklist stage 7), kept as a plain, engine-light
    /// class so it can be unit-tested without play mode. It owns nothing about a vehicle:
    /// callers feed it damage and a per-frame <see cref="Tick"/>, and read the resulting
    /// <see cref="Stage"/> / <see cref="IsWrecked"/> / <see cref="IsProtected"/> to drive the
    /// car, VFX and HUD. Reaching zero durability wrecks the car for
    /// <c>wreckDurationSeconds</c> (control loss is the caller's job) and then recovers it to
    /// <c>recoveryDurability</c> with a short damage-immune window — a wreck never eliminates
    /// on its own.
    /// </summary>
    public class DurabilityState
    {
        private readonly DamageSettings s;

        public float Durability { get; private set; }
        public DamageStage Stage { get; private set; }
        public bool IsWrecked => Stage == DamageStage.Wrecked;
        public bool IsProtected => protectionTimer > 0f;
        public float WreckTimeRemaining => Mathf.Max(0f, wreckTimer);
        public float ProtectionTimeRemaining => Mathf.Max(0f, protectionTimer);

        private float wreckTimer;
        private float protectionTimer;

        /// <summary>Fired when the damage stage changes (for VFX / HUD).</summary>
        public event System.Action<DamageStage> StageChanged;
        /// <summary>Fired the moment the car is wrecked (durability hit zero).</summary>
        public event System.Action Wrecked;
        /// <summary>Fired when the wreck timer elapses and the car is restored.</summary>
        public event System.Action Recovered;

        public DurabilityState(DamageSettings settings)
        {
            s = settings;
            Durability = s.maximumDurability;
            Stage = DamageStage.Pristine;
        }

        /// <summary>
        /// Apply damage from a strong collision, rupture curse or hazard. Ignored while the
        /// car is already wrecked or inside the post-recovery protection window.
        /// </summary>
        public void ApplyDamage(float amount)
        {
            if (amount <= 0f) return;
            if (IsWrecked || IsProtected) return;
            Durability = Mathf.Max(0f, Durability - amount);
            RecomputeStage();
        }

        /// <summary>Advance timers; call once per frame with the frame delta.</summary>
        public void Tick(float deltaTime)
        {
            if (protectionTimer > 0f) protectionTimer = Mathf.Max(0f, protectionTimer - deltaTime);
            if (Stage == DamageStage.Wrecked)
            {
                wreckTimer -= deltaTime;
                if (wreckTimer <= 0f) Recover();
            }
        }

        private void RecomputeStage()
        {
            DamageStage next;
            if (Durability <= s.wreckedThreshold) next = DamageStage.Wrecked;
            else if (Durability <= s.criticalThreshold) next = DamageStage.Critical;
            else if (Durability <= s.damagedThreshold) next = DamageStage.Damaged;
            else next = DamageStage.Pristine;

            if (next == Stage) return;
            Stage = next;
            StageChanged?.Invoke(next);
            if (next == DamageStage.Wrecked)
            {
                wreckTimer = s.wreckDurationSeconds;
                Wrecked?.Invoke();
            }
        }

        private void Recover()
        {
            wreckTimer = 0f;
            Durability = Mathf.Min(s.recoveryDurability, s.maximumDurability);
            protectionTimer = s.recoveryProtectionSeconds;
            RecomputeStage();          // 50 durability -> Damaged, not Pristine
            Recovered?.Invoke();
        }
    }
}
