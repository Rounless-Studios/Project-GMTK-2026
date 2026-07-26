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
        /// Aim distance that also accounts for the corner ahead: far down a straight, pulled back into
        /// a bend. A distance that only grows with speed aims past the corner - the car reads a small
        /// angle to a point beyond the bend, steers barely at all, and drives straight off the outside.
        /// </summary>
        public static float LookAheadMetres(
            float speedKph,
            float headingChangeDegrees,
            AiDrivingSettings s)
        {
            float onAStraight = Mathf.Min(LookAheadMetres(speedKph, s), s.maximumLookAheadMetres);
            float bend = Mathf.Clamp01(
                Mathf.Abs(headingChangeDegrees) / Mathf.Max(1f, s.lookAheadTightenDegrees));

            return Mathf.Max(s.minLookAheadMetres, Mathf.Lerp(onAStraight, s.minLookAheadMetres, bend));
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
        /// <paramref name="throttleScale"/> is the personality's pace multiplier, and
        /// <paramref name="gripScale"/> is how much grip it believes it has - its braking point.
        /// </summary>
        public static float CornerSpeedKph(
            float headingChangeDegrees,
            float scanMetres,
            float throttleScale,
            AiDrivingSettings s,
            float gripScale = 1f)
        {
            float angle = Mathf.Abs(headingChangeDegrees) * Mathf.Deg2Rad;

            if (angle < 0.001f)
                return s.straightSpeedKph * throttleScale;

            float radius = Mathf.Max(1f, Mathf.Max(1f, scanMetres) / angle);
            float speedKph = Mathf.Sqrt(s.cornerGrip * Mathf.Max(0.01f, gripScale) * radius) * 3.6f;

            return Mathf.Clamp(speedKph, s.minCornerSpeedKph, s.straightSpeedKph) * throttleScale;
        }

        /// <summary>
        /// Backs the speed target off while the car is actually sliding sideways.
        /// <para>
        /// Corner speed is computed from an assumed grip figure, and if that figure is optimistic for
        /// the car the AI keeps entering corners too fast and understeers off the outside - it never
        /// finds out, because nothing in the model measures the result. Sideways speed is that
        /// measurement: it needs no tyre model and it is right whatever the vehicle package does.
        /// </para>
        /// </summary>
        public static float SlipCorrectedTargetKph(
            float targetKph,
            float lateralSlipKph,
            AiDrivingSettings s)
        {
            float slip = Mathf.Abs(lateralSlipKph);
            if (slip <= s.slipToleranceKph) return targetKph;

            float excess = slip - s.slipToleranceKph;
            return Mathf.Max(s.minCornerSpeedKph, targetKph - excess * s.slipSpeedPenalty);
        }

        /// <summary>
        /// How close a waypoint has to be before it counts as reached. The fixed radius is a distance,
        /// but passing one is an event in time: at 200 kph a 9 m sphere is crossed in a sixth of a
        /// second, and a car on an offset racing line can pass outside it altogether. Adding a slice of
        /// travel time keeps the cursor moving with the car.
        /// </summary>
        public static float WaypointReachMetres(float speedKph, AiDrivingSettings s)
        {
            float travel = Mathf.Max(0f, speedKph) / 3.6f * s.waypointReachSecondsAhead;
            return Mathf.Max(s.waypointReachMetres, travel);
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

        /// <summary>
        /// Which way to go around the car ahead: the side with more room, and away from where the
        /// rival actually sits when both sides are equally open. Returns +1 for right, -1 for left.
        /// </summary>
        public static float OvertakeSideBias(
            float rivalLateralMetres,
            float spaceLeftMetres,
            float spaceRightMetres)
        {
            float room = Mathf.Max(0f, spaceRightMetres) - Mathf.Max(0f, spaceLeftMetres);
            if (Mathf.Abs(room) > 0.5f) return room > 0f ? 1f : -1f;

            // equally boxed in: go the opposite way to the rival, and pick a side when it is dead ahead
            return rivalLateralMetres > 0f ? -1f : 1f;
        }

        /// <summary>
        /// Sideways offset that takes the car out of the tow of the one ahead and onto a passing line.
        /// Nothing happens until the rival is inside the scan distance and actually being caught, so
        /// the AI does not weave behind a car it cannot pass. Grows as the gap closes and is scaled by
        /// the personality's aggression; the caller still clamps it to the measured road.
        /// </summary>
        public static float OvertakeOffsetMetres(
            float gapAheadMetres,
            float closingKph,
            float rivalLateralMetres,
            float spaceLeftMetres,
            float spaceRightMetres,
            float aggression,
            AiDrivingSettings s)
        {
            if (gapAheadMetres <= 0f || gapAheadMetres > s.rivalScanMetres) return 0f;
            if (closingKph < s.overtakeMinClosingKph) return 0f;
            if (aggression <= 0f) return 0f;

            float urgency = 1f - Mathf.Clamp01(gapAheadMetres / Mathf.Max(1f, s.rivalScanMetres));
            float side = OvertakeSideBias(rivalLateralMetres, spaceLeftMetres, spaceRightMetres);

            return side * s.overtakeOffsetMetres * aggression * urgency;
        }

        /// <summary>
        /// Caps the speed target so the car settles behind the one ahead instead of driving through it.
        /// The gap it keeps is the personality's tolerance: at zero the car never lifts, which is what
        /// makes a rammer a rammer. Only the closing car lifts - a car being caught keeps its pace.
        /// </summary>
        public static float FollowSpeedKph(
            float targetSpeedKph,
            float gapAheadMetres,
            float rivalSpeedKph,
            float contactToleranceMetres,
            AiDrivingSettings s)
        {
            if (contactToleranceMetres <= 0f) return targetSpeedKph;
            if (gapAheadMetres <= 0f || gapAheadMetres > s.rivalScanMetres) return targetSpeedKph;

            float keep = Mathf.Min(contactToleranceMetres, s.followGapMetres + contactToleranceMetres);
            if (gapAheadMetres > keep) return targetSpeedKph;

            // Inside the gap the car matches the one ahead and only lifts a little more as it closes
            // right up. Scaling down by the remaining gap instead would drop each car to a fraction of
            // the one in front, and a queue of cars would brake each other to walking pace.
            float squeeze = Mathf.Clamp01(gapAheadMetres / Mathf.Max(0.1f, keep));
            float cap = Mathf.Max(0f, rivalSpeedKph) - (1f - squeeze) * s.followLiftKph;

            return Mathf.Min(targetSpeedKph, Mathf.Max(0f, cap));
        }

        /// <summary>
        /// Sideways offset that puts a defending car in front of the one behind it, so a faster car has
        /// to work for the pass. Strength is the personality's; a car with none stays on its line.
        /// </summary>
        public static float BlockOffsetMetres(
            float gapBehindMetres,
            float rivalLateralMetres,
            float blockStrength,
            AiDrivingSettings s)
        {
            if (blockStrength <= 0f) return 0f;
            if (gapBehindMetres <= 0f || gapBehindMetres > s.rivalScanMetres) return 0f;

            float urgency = 1f - Mathf.Clamp01(gapBehindMetres / Mathf.Max(1f, s.rivalScanMetres));
            float wanted = Mathf.Clamp(rivalLateralMetres, -s.blockOffsetMetres, s.blockOffsetMetres);

            return wanted * blockStrength * urgency;
        }

        /// <summary>
        /// Whether to spend a boost charge now: as soon as one is in hand, unless the preset asks the AI
        /// to wait for a straight. Waiting is off by default - a charge sitting unused is a charge the
        /// player never has to race against - and the speed target is raised only on a straight anyway,
        /// so a boost burning through a corner cannot carry the car off the road.
        /// <paramref name="roll"/> is a 0..1 random draw compared against the personality's eagerness,
        /// passed in so the decision stays deterministic under test.
        /// </summary>
        public static bool ShouldBoost(
            float headingChangeDegrees,
            float speedKph,
            bool hasCharge,
            float boostTendency,
            float roll,
            AiDrivingSettings s)
        {
            if (!hasCharge) return false;
            if (speedKph < s.boostMinimumSpeedKph) return false;

            if (s.boostOnlyOnStraights &&
                Mathf.Abs(headingChangeDegrees) > s.boostStraightMaximumDegrees)
                return false;

            return roll <= boostTendency;
        }

        /// <summary>
        /// Raises the speed target while a boost burns, but only while the path ahead is straight.
        /// Without this the AI brakes against its own boost - the extra speed pushes it past a target it
        /// then tries to hold - and a boost that is still burning at a corner would carry it off the
        /// road instead of being ignored.
        /// </summary>
        public static float BoostedTargetKph(
            float targetKph,
            float speedMultiplier,
            float headingChangeDegrees,
            AiDrivingSettings s)
        {
            if (speedMultiplier <= 1f) return targetKph;
            if (Mathf.Abs(headingChangeDegrees) > s.boostStraightMaximumDegrees) return targetKph;

            return targetKph * speedMultiplier;
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
