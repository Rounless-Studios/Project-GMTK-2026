using UnityEngine;

namespace Gmtk2026.GameBalance
{
    /// <summary>
    /// The shove a wrecked car gets instead of standing still: the impact that emptied its
    /// durability is handed back scaled up and clamped, so hitting zero reads as being thrown
    /// out of control rather than as a car parked in the middle of the track. Pure maths so the
    /// numbers are unit-testable — <see cref="DurabilityState"/> still owns the timers and the
    /// caller owns the rigidbody it is applied to.
    /// </summary>
    public static class WreckLaunch
    {
        /// <summary>
        /// Velocity change to add to the wrecked car.
        /// <paramref name="pushDirection"/> is where the impact shoves the car — a contact normal
        /// points away from whatever was hit — and does not need to be normalised. A zero
        /// direction launches the car straight up, which covers a wreck with no collision behind
        /// it (rupture curse, hazard damage).
        /// </summary>
        public static Vector3 Compute(
            DamageSettings settings,
            Vector3 pushDirection,
            float impactSpeed)
        {
            if (settings == null) return Vector3.zero;

            float maximumSpeed = Mathf.Max(
                settings.wreckMinimumLaunchSpeed,
                settings.wreckMaximumLaunchSpeed);
            float speed = Mathf.Clamp(
                Mathf.Max(0f, impactSpeed) * settings.wreckImpactSpeedMultiplier,
                settings.wreckMinimumLaunchSpeed,
                maximumSpeed);

            return Direction(pushDirection, settings.wreckUpwardLaunchRatio) * speed;
        }

        /// <summary>
        /// Unit launch direction: the push, tilted upward so the car lifts a wheel and tumbles
        /// instead of sliding flat. A ratio of 1 means 45 degrees up.
        /// </summary>
        public static Vector3 Direction(Vector3 pushDirection, float upwardRatio)
        {
            Vector3 direction = pushDirection.sqrMagnitude > 1e-6f
                ? pushDirection.normalized
                : Vector3.up;

            Vector3 tilted = direction + Vector3.up * Mathf.Max(0f, upwardRatio);

            // a push straight down cancels the upward tilt exactly; throw the car up instead of
            // returning nothing at all
            return tilted.sqrMagnitude > 1e-6f ? tilted.normalized : Vector3.up;
        }
    }
}
