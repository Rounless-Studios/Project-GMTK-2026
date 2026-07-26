using Gmtk2026.GameBalance;
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

        public virtual void ApplyWorldImpulse(Vector3 impulse)
        {
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null) body.AddForce(impulse, ForceMode.VelocityChange);
        }

        public virtual void ConfigureAiPersonality(AIPersonalityType type)
        {
        }

        /// <summary>
        /// <paramref name="lateralStrength"/> is the personality's own sideways pull
        /// (<c>AiPersonalityProfile.lateralStrengthMetres</c>); how it is applied depends on
        /// <paramref name="type"/>.
        /// </summary>
        public virtual void ApplyAiTargeting(
            AIPersonalityType type,
            Transform player,
            float lateralStrength,
            float aggroRange)
        {
        }
    }
}
