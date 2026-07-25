using UnityEngine;

namespace GMTK
{
    /// <summary>
    /// Vehicle-package boundary used by gameplay systems. Package-specific code lives in a
    /// derived adapter, while boost, durability, curses, and race logic remain package agnostic.
    /// </summary>
    public abstract class GmtkVehicleAdapter : MonoBehaviour
    {
        public abstract bool IsPlayer { get; }

        public abstract void SetControlsEnabled(bool enabled);

        public abstract void ApplyBoost(float speedMultiplier);

        public abstract void ApplyForwardImpulse(float force);

        public virtual void ConfigureAiPersonality(AIPersonalityType type)
        {
        }

        public virtual void ApplyAiTargeting(
            AIPersonalityType type,
            Transform player,
            float ramStrength,
            float blockStrength,
            float aggroRange)
        {
        }
    }
}
