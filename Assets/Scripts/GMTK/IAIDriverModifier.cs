using UnityEngine;

namespace GMTK
{
    /// <summary>
    /// Optional behaviour module a car can expose to influence how CarAIControl drives.
    /// Implemented by AIPersonality (see the AI personalities feature). CarAIControl
    /// looks one up via GetComponent and applies it if present, so the hook is inert
    /// when no personality is attached.
    /// </summary>
    public interface IAIDriverModifier
    {
        /// <summary>Multiplier applied to the AI's desired speed (1 = unchanged).</summary>
        float SpeedMultiplier { get; }

        /// <summary>Extra world-space offset added to the steering target (e.g. veer toward the player).</summary>
        Vector3 GetTargetOffset(Transform self, Vector3 baseTargetPosition);
    }
}
