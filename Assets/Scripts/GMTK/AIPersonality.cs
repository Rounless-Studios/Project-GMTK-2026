using Gmtk2026.GameBalance;
using UnityEngine;

namespace GMTK
{
    /// <summary>
    /// Gives an AI car a distinct driving personality through the active vehicle adapter.
    /// Every number comes from <c>AISettings.personalityProfiles</c>: this component is added at
    /// runtime, so anything serialized here would reset on every spawn and could never be tuned.
    /// The IAIDriverModifier implementation remains as a fallback for legacy kit cars.
    /// </summary>
    public class AIPersonality : MonoBehaviour, IAIDriverModifier
    {
        public AIPersonalityType type = AIPersonalityType.CleanRacer;

        private static AiPersonalityAssignmentSettings Aggression =>
            GameBalance.Current.ai.personalityAssignment;

        private AiPersonalityProfile Profile => GameBalance.Current.ai.GetProfile(type);

        private Transform player;
        private GmtkVehicleAdapter vehicleAdapter;

        private void Awake()
        {
            vehicleAdapter = GetComponent<GmtkVehicleAdapter>();
            ApplyToDriver();
        }

        private void FixedUpdate()
        {
            if (vehicleAdapter == null)
                return;

            vehicleAdapter.ApplyAiTargeting(
                type,
                Player(),
                LateralStrength,
                Aggression.aggroRangeMetres);
        }

        /// <summary>Sideways pull toward the player; 0 for personalities that keep the racing line.</summary>
        public float LateralStrength
        {
            get
            {
                var profile = Profile;
                return profile != null ? profile.lateralStrengthMetres : 0f;
            }
        }

        public void ApplyToDriver()
        {
            if (vehicleAdapter == null)
                vehicleAdapter = GetComponent<GmtkVehicleAdapter>();

            vehicleAdapter?.ConfigureAiPersonality(type);
        }

        /// <summary>
        /// Pace for the legacy kit driver. Reads the same <c>paceScale</c> the RCCP driver uses, so
        /// a personality has one speed number rather than one per vehicle package.
        /// </summary>
        public float SpeedMultiplier
        {
            get
            {
                var profile = Profile;
                return profile != null ? profile.paceScale : 1f;
            }
        }

        public Vector3 GetTargetOffset(Transform self, Vector3 baseTargetPosition)
        {
            float strength = LateralStrength;
            if (strength <= 0f) return Vector3.zero;

            var p = Player();
            if (p == null) return Vector3.zero;

            Vector3 toPlayer = p.position - self.position;
            if (toPlayer.magnitude > Aggression.aggroRangeMetres) return Vector3.zero;

            switch (type)
            {
                case AIPersonalityType.Rammer:
                    return toPlayer.normalized * strength;
                case AIPersonalityType.Blocker:
                    // slide sideways toward the player's lane to block their path
                    return Vector3.Project(toPlayer, self.right).normalized * strength;
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
