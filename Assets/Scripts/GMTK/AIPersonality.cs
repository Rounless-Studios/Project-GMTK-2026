using UnityEngine;
using SpinMotion;

namespace GMTK
{
    public enum AIPersonalityType
    {
        CleanRacer, // fast, tidy lines, focused on lap times
        Rammer,     // actively veers into the player
        Blocker,    // slides across to cut the player off
        Reckless    // flat out, little regard for corners
    }

    /// <summary>
    /// Gives an AI car a distinct driving personality through the active vehicle adapter.
    /// The IAIDriverModifier implementation remains as a fallback for legacy kit cars.
    /// </summary>
    public class AIPersonality : MonoBehaviour, IAIDriverModifier
    {
        public AIPersonalityType type = AIPersonalityType.CleanRacer;

        [Header("Aggression")]
        [Tooltip("How hard rammers pull toward the player.")]
        public float ramStrength = 7f;
        [Tooltip("How hard blockers slide sideways to cut the player off.")]
        public float blockStrength = 5f;
        [Tooltip("Only chase/block the player within this distance.")]
        public float aggroRange = 45f;

        [Header("Speed Multipliers (x normal desired speed)")]
        public float recklessSpeedMultiplier = 1.15f;
        public float rammerSpeedMultiplier = 1.05f;
        public float blockerSpeedMultiplier = 0.97f;
        public float cleanRacerSpeedMultiplier = 1.0f;

        private Transform player;
        private GmtkVehicleAdapter vehicleAdapter;
        private CarAIControl legacyAi;

        private void Awake()
        {
            vehicleAdapter = GetComponent<GmtkVehicleAdapter>();
            legacyAi = GetComponent<CarAIControl>();

            if (legacyAi != null)
                legacyAi.RefreshModifier();

            ApplyToDriver();
        }

        private void FixedUpdate()
        {
            if (vehicleAdapter == null)
                return;

            vehicleAdapter.ApplyAiTargeting(
                type,
                Player(),
                ramStrength,
                blockStrength,
                aggroRange);
        }

        public void ApplyToDriver()
        {
            if (vehicleAdapter == null)
                vehicleAdapter = GetComponent<GmtkVehicleAdapter>();

            vehicleAdapter?.ConfigureAiPersonality(type);
        }

        public float SpeedMultiplier
        {
            get
            {
                return type switch
                {
                    AIPersonalityType.Reckless => recklessSpeedMultiplier,
                    AIPersonalityType.Rammer => rammerSpeedMultiplier,
                    AIPersonalityType.Blocker => blockerSpeedMultiplier,
                    _ => cleanRacerSpeedMultiplier,
                };
            }
        }

        public Vector3 GetTargetOffset(Transform self, Vector3 baseTargetPosition)
        {
            switch (type)
            {
                case AIPersonalityType.Rammer:
                {
                    var p = Player();
                    if (p == null) return Vector3.zero;
                    Vector3 toPlayer = p.position - self.position;
                    if (toPlayer.magnitude > aggroRange) return Vector3.zero;
                    return toPlayer.normalized * ramStrength;
                }
                case AIPersonalityType.Blocker:
                {
                    var p = Player();
                    if (p == null) return Vector3.zero;
                    Vector3 toPlayer = p.position - self.position;
                    if (toPlayer.magnitude > aggroRange) return Vector3.zero;
                    // slide sideways toward the player's lane to block their path
                    Vector3 lateral = Vector3.Project(toPlayer, self.right);
                    return lateral.normalized * blockStrength;
                }
                default:
                    return Vector3.zero;
            }
        }

        private Transform Player()
        {
            if (player == null)
            {
                var car = Race.CarByIndex(0);
                if (car != null) player = car.transform;
            }
            return player;
        }
    }
}
