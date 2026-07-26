using UnityEngine;
using UnityEngine.InputSystem;
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
        private GmtkVehicleAdapter vehicleAdapter;

        private void Awake()
        {
            vehicleAdapter = GetComponent<GmtkVehicleAdapter>();
            IsPlayer = vehicleAdapter != null && vehicleAdapter.IsPlayer;
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

            Keyboard keyboard = Keyboard.current;
            if (IsPlayer && keyboard != null &&
                (keyboard.spaceKey.wasPressedThisFrame ||
                 keyboard.leftShiftKey.wasPressedThisFrame ||
                 keyboard.rightShiftKey.wasPressedThisFrame))
                State.TryActivate();

        }

        private void FixedUpdate()
        {
            if (vehicleAdapter != null && State != null)
                vehicleAdapter.ApplyBoost(State.CurrentSpeedMultiplier);
        }
    }
}
