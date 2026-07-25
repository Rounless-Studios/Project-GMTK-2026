using UnityEngine;

namespace Gmtk2026.GameBalance
{
    /// <summary>
    /// Pure driving maths for the waypoint AI: how far ahead to aim, how fast a corner can be taken,
    /// how much steering authority to use, and where the car sits across the road. Kept engine-free
    /// (only Mathf) so it is unit-testable and so the vehicle component stays a thin shell.
    /// </summary>
    public static class AiDriving
    {
        /// <summary>Aim distance in metres: further ahead the faster the car goes.</summary>
        public static float LookAheadMetres(float speedKph, AiDrivingSettings s)
        {
            return s.minLookAheadMetres + Mathf.Max(0f, speedKph) * s.lookAheadMetresPerKph;
        }

        /// <summary>
        /// How far ahead to look for a corner: the distance needed to brake from the current speed, so
        /// a fast car starts slowing early while a slow car is not braked for a corner it cannot reach
        /// yet. A fixed window either brakes too late on the straights or too early in the slow bits.
        /// </summary>
        public static float CornerScanMetres(float speedKph, AiDrivingSettings s)
        {
            float speedMs = Mathf.Max(0f, speedKph) / 3.6f;
            float brakingMetres = speedMs * speedMs / (2f * Mathf.Max(1f, s.cornerBrakingDecel));

            return Mathf.Max(s.minCornerScanMetres, brakingMetres);
        }

        /// <summary>
        /// Lets the corner-speed target drop instantly but rise only at a limited rate. The scan window
        /// grows and shrinks with speed, so a corner can leave the window right after the car slowed
        /// for it; without this the AI would release the brakes and hunt.
        /// </summary>
        public static float SmoothTargetSpeedKph(
            float previousTargetKph,
            float targetKph,
            float deltaSeconds,
            AiDrivingSettings s)
        {
            if (targetKph <= previousTargetKph)
                return targetKph;

            return Mathf.Min(targetKph, previousTargetKph + s.targetSpeedRiseKphPerSecond * deltaSeconds);
        }

        /// <summary>
        /// Corner speed from the radius the path describes ahead, so the car slows before the corner
        /// instead of reacting inside it. The heading change accumulated over
        /// <paramref name="scanMetres"/> is read as an arc (radius = arc / angle) and the speed comes
        /// from the sideways grip limit, which keeps a wide sweeper fast while a hairpin still slows.
        /// Scaling the angle down directly would over-brake every long corner.
        /// <paramref name="throttleScale"/> is the personality's pace multiplier.
        /// </summary>
        public static float CornerSpeedKph(
            float headingChangeDegrees,
            float scanMetres,
            float throttleScale,
            AiDrivingSettings s)
        {
            float angle = Mathf.Abs(headingChangeDegrees) * Mathf.Deg2Rad;

            if (angle < 0.001f)
                return s.straightSpeedKph * throttleScale;

            float radius = Mathf.Max(1f, Mathf.Max(1f, scanMetres) / angle);
            float speedKph = Mathf.Sqrt(s.cornerGrip * radius) * 3.6f;

            return Mathf.Clamp(speedKph, s.minCornerSpeedKph, s.straightSpeedKph) * throttleScale;
        }

        /// <summary>Steering authority falls off with speed.</summary>
        public static float SteerGain(float speedKph, AiDrivingSettings s)
        {
            float t = Mathf.Clamp01(Mathf.Max(0f, speedKph) / Mathf.Max(1f, s.straightSpeedKph));
            return Mathf.Lerp(s.steerGainLowSpeed, s.steerGainHighSpeed, t);
        }

        /// <summary>
        /// 1 right off the grid, 0 once the car has driven the merge distance. Distance, not time,
        /// because the pre-race countdown would otherwise consume the merge while the car is frozen.
        /// </summary>
        public static float GridLaneBlend(float travelledMetres, AiDrivingSettings s)
        {
            if (s.laneMergeMetres <= 0f) return 0f;
            return 1f - Mathf.Clamp01(Mathf.Max(0f, travelledMetres) / s.laneMergeMetres);
        }

        /// <summary>
        /// Per-car offset kept after merging, leaning to the side the car started on. A shared bias
        /// would cancel the grid lane of every car starting on the other side and send them to the
        /// centre of the track; deriving it from the race index keeps a race reproducible.
        /// </summary>
        public static float LaneSpreadMetres(int raceIndex, float startSide, AiDrivingSettings s)
        {
            float side = startSide >= 0f ? 1f : -1f;
            int step = Mathf.Abs(raceIndex) % 3;
            return side * s.laneSpreadMetres * (0.5f + step * 0.25f);
        }

        /// <summary>Keeps the chosen position inside the measured road, edges already inset by the margin.</summary>
        public static float ClampLateral(float desiredMetres, float edgeLeftMetres, float edgeRightMetres)
        {
            return Mathf.Clamp(desiredMetres, -Mathf.Max(0f, edgeLeftMetres), Mathf.Max(0f, edgeRightMetres));
        }

        /// <summary>Throttle and brake from the speed error against the corner speed.</summary>
        public static void SpeedInputs(float targetSpeedKph, float speedKph, out float throttle, out float brake)
        {
            float error = targetSpeedKph - speedKph;
            throttle = Mathf.Clamp01(error / 12f);
            brake = error < -4f ? Mathf.Clamp01(-error / 22f) : 0f;
        }
    }
}
