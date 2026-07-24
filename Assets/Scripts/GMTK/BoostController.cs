using UnityEngine;
using SpinMotion;
using Gmtk2026.GameBalance;

namespace GMTK
{
    /// <summary>
    /// Per-car boost actuation (checklist stage 3.2). Wraps the settings-driven
    /// <see cref="BoostState"/>: the player triggers it with Space / Shift, AI drives it through
    /// <see cref="TryBoost"/> (stage 9), an overtake win refunds a charge via
    /// <see cref="RewardOvertake"/>, and the engine-seal curse calls <see cref="ApplySeal"/>.
    /// Exposes <see cref="SpeedMultiplier"/> for the vehicle layer to consume.
    /// </summary>
    [DisallowMultipleComponent]
    public class BoostController : MonoBehaviour
    {
        public BoostState State { get; private set; }
        public int Charges => State != null ? State.Charges : 0;
        public bool IsBoosting => State != null && State.IsBoosting;
        public bool IsSealed => State != null && State.IsSealed;
        public float SpeedMultiplier => State != null ? State.CurrentSpeedMultiplier : 1f;

        // static so a single HUD / VFX driver can listen for every car
        public static event System.Action<BoostController> Changed;

        public bool IsPlayer { get; private set; }

        private BoostSettings B => GameBalance.Current.boost;

        private void Awake()
        {
            IsPlayer = GetComponentInChildren<CarUserControl>(true) != null;
            Build();
        }

        private void Build()
        {
            State = new BoostState(B);
            State.ChargesChanged += _ => Changed?.Invoke(this);
            State.BoostStarted += () => Changed?.Invoke(this);
            State.BoostEnded += () => Changed?.Invoke(this);
        }

        /// <summary>Restore full charges and clear timers for a fresh race.</summary>
        public void ResetForRace() => Build();

        /// <summary>Request a boost (AI or scripted). Returns true if one started.</summary>
        public bool TryBoost() => State != null && State.TryActivate();

        /// <summary>Engine-seal curse: block boost and recharge for a time.</summary>
        public void ApplySeal(float seconds) => State?.ApplySeal(seconds);

        /// <summary>Refund a charge for a successful overtake challenge.</summary>
        public void RewardOvertake() => State?.AddCharges(B.overtakeRewardCharges);

        private void Update()
        {
            if (State == null) return;
            State.Tick(Time.deltaTime);

            if (IsPlayer && (Input.GetKeyDown(KeyCode.Space)
                             || Input.GetKeyDown(KeyCode.LeftShift)
                             || Input.GetKeyDown(KeyCode.RightShift)))
                State.TryActivate();

            // TODO(vehicle): feed State.CurrentSpeedMultiplier into the drive once the MVC
            // vehicle replaces the kit car. The rule/charges/seal are complete and tested here.
        }
    }
}
