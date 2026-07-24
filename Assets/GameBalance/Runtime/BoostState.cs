using UnityEngine;

namespace Gmtk2026.GameBalance
{
    /// <summary>
    /// Settings-driven boost rule (checklist stage 3.2), kept as a plain, unit-testable class.
    /// The car starts with <c>maximumCharges</c>; activating consumes one for a
    /// <c>durationSeconds</c> speed burst, and charges refill one at a time every
    /// <c>rechargeSecondsPerCharge</c>. An overtake win refunds a charge. The engine-seal curse
    /// applies a timed seal that blocks activation and pauses recharge. Callers feed input and a
    /// per-frame <see cref="Tick"/>, then read <see cref="CurrentSpeedMultiplier"/> / <see cref="Charges"/>.
    /// </summary>
    public class BoostState
    {
        private readonly BoostSettings s;

        public int Charges { get; private set; }
        public int MaximumCharges => s.maximumCharges;
        public bool IsBoosting => boostTimer > 0f;
        public bool IsSealed => sealTimer > 0f;
        public float BoostTimeRemaining => Mathf.Max(0f, boostTimer);
        public float SealTimeRemaining => Mathf.Max(0f, sealTimer);
        public float CurrentSpeedMultiplier => IsBoosting ? s.boostSpeedMultiplier : 1f;

        private float boostTimer;
        private float rechargeTimer;
        private float sealTimer;

        public event System.Action<int> ChargesChanged;
        public event System.Action BoostStarted;
        public event System.Action BoostEnded;
        public event System.Action<bool> SealChanged;

        public BoostState(BoostSettings settings)
        {
            s = settings;
            Charges = s.maximumCharges;
        }

        /// <summary>Spend a charge to start a boost. Returns false if unavailable.</summary>
        public bool TryActivate()
        {
            if (IsSealed || IsBoosting || Charges <= 0) return false;
            Charges--;
            boostTimer = s.durationSeconds;
            ChargesChanged?.Invoke(Charges);
            BoostStarted?.Invoke();
            return true;
        }

        /// <summary>Refund charges (overtake reward), never above the maximum.</summary>
        public void AddCharges(int amount)
        {
            if (amount <= 0) return;
            int before = Charges;
            Charges = Mathf.Min(s.maximumCharges, Charges + amount);
            if (Charges != before) ChargesChanged?.Invoke(Charges);
        }

        /// <summary>Engine-seal curse: block boost use and recharge for the given time.</summary>
        public void ApplySeal(float seconds)
        {
            if (seconds <= 0f) return;
            bool was = IsSealed;
            sealTimer = Mathf.Max(sealTimer, seconds);
            if (!was && IsSealed) SealChanged?.Invoke(true);
        }

        /// <summary>Advance timers; call once per frame with the frame delta.</summary>
        public void Tick(float deltaTime)
        {
            // gate recharge on the seal state at the start of the frame, so a frame that
            // begins sealed never recharges even if the seal expires partway through it
            bool wasSealed = IsSealed;

            if (sealTimer > 0f)
            {
                sealTimer = Mathf.Max(0f, sealTimer - deltaTime);
                if (sealTimer == 0f) SealChanged?.Invoke(false);
            }

            if (boostTimer > 0f)
            {
                boostTimer = Mathf.Max(0f, boostTimer - deltaTime);
                if (boostTimer == 0f) BoostEnded?.Invoke();
            }

            // refill one slot at a time; paused while sealed or already full
            if (!wasSealed && Charges < s.maximumCharges && s.rechargeSecondsPerCharge > 0f)
            {
                rechargeTimer += deltaTime;
                while (rechargeTimer >= s.rechargeSecondsPerCharge && Charges < s.maximumCharges)
                {
                    rechargeTimer -= s.rechargeSecondsPerCharge;
                    Charges++;
                    ChargesChanged?.Invoke(Charges);
                }
                if (Charges >= s.maximumCharges) rechargeTimer = 0f;
            }
        }

        /// <summary>Restore full charges and clear all timers for a fresh race.</summary>
        public void ResetForRace()
        {
            Charges = s.maximumCharges;
            boostTimer = 0f;
            rechargeTimer = 0f;
            sealTimer = 0f;
        }
    }
}
