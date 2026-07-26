using System.Collections.Generic;
using UnityEngine;

namespace Gmtk2026.GameBalance
{
    /// <summary>
    /// Maths behind the track minimap: thinning the dense AI waypoint path into a drawable outline,
    /// cutting out the stretch of it that fits in the panel, and turning world positions into panel
    /// offsets for a top-down view. Engine-free apart from Vector2/Vector3/Mathf so it stays
    /// unit-testable.
    /// </summary>
    public static class MinimapView
    {
        /// <summary>
        /// Copies <paramref name="source"/> into <paramref name="destination"/>, dropping points that
        /// sit closer than <paramref name="minimumSpacingMetres"/> to the last kept one. Measured on
        /// the XZ plane, because that is the only thing a top-down panel shows. The path is a closed
        /// loop, so a last point that has crowded up against the first is dropped as well.
        /// </summary>
        public static void Thin(IList<Vector3> source, float minimumSpacingMetres,
            List<Vector3> destination)
        {
            if (destination == null) return;
            destination.Clear();

            int count = source != null ? source.Count : 0;
            if (count == 0) return;

            float spacingSqr = Mathf.Max(0.01f, minimumSpacingMetres) * Mathf.Max(0.01f, minimumSpacingMetres);
            destination.Add(source[0]);

            for (int i = 1; i < count; i++)
                if (FlatSqrDistance(destination[destination.Count - 1], source[i]) >= spacingSqr)
                    destination.Add(source[i]);

            if (destination.Count > 2 &&
                FlatSqrDistance(destination[destination.Count - 1], destination[0]) < spacingSqr)
                destination.RemoveAt(destination.Count - 1);
        }

        /// <summary>Length of a closed path on the XZ plane, the closing segment included.</summary>
        public static float FlatLength(IList<Vector3> path)
        {
            int count = path != null ? path.Count : 0;
            if (count < 2) return 0f;

            float total = 0f;
            for (int i = 0; i < count; i++)
                total += FlatDistance(path[i], path[(i + 1) % count]);

            return total;
        }

        /// <summary>XZ centre and extent of a path. False when there is nothing to measure.</summary>
        public static bool Bounds(IList<Vector3> path, out Vector2 centre, out Vector2 sizeMetres)
        {
            centre = Vector2.zero;
            sizeMetres = Vector2.zero;
            int count = path != null ? path.Count : 0;
            if (count == 0) return false;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                Vector3 point = path[i];
                if (point.x < minX) minX = point.x;
                if (point.x > maxX) maxX = point.x;
                if (point.z < minZ) minZ = point.z;
                if (point.z > maxZ) maxZ = point.z;
            }

            centre = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            sizeMetres = new Vector2(maxX - minX, maxZ - minZ);
            return true;
        }

        /// <summary>
        /// Scale that fits <paramref name="sizeMetres"/> inside the panel, keeping the aspect ratio so
        /// corners stay corners. A path with no extent gets 1, which draws it as a dot in the middle.
        /// </summary>
        public static float FitPixelsPerMetre(Vector2 sizeMetres, Vector2 panelPixels,
            float paddingPixels)
        {
            float usableX = Mathf.Max(1f, panelPixels.x - paddingPixels * 2f);
            float usableY = Mathf.Max(1f, panelPixels.y - paddingPixels * 2f);
            bool hasWidth = sizeMetres.x > 0.001f;
            bool hasHeight = sizeMetres.y > 0.001f;
            if (!hasWidth && !hasHeight) return 1f;

            float scaleX = hasWidth ? usableX / sizeMetres.x : float.MaxValue;
            float scaleY = hasHeight ? usableY / sizeMetres.y : float.MaxValue;
            return Mathf.Min(scaleX, scaleY);
        }

        /// <summary>
        /// World position to a panel offset in pixels, measured from the panel centre.
        /// <paramref name="upHeadingDegrees"/> is the world heading that ends up pointing up the
        /// panel: pass the player's heading for a view that turns with the car, or 0 to leave the map
        /// north-up.
        /// </summary>
        public static Vector2 ToPanel(Vector3 world, Vector2 centre, float pixelsPerMetre,
            float upHeadingDegrees)
        {
            float x = world.x - centre.x;
            float z = world.z - centre.y;
            float radians = upHeadingDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(
                (x * cos - z * sin) * pixelsPerMetre,
                (x * sin + z * cos) * pixelsPerMetre);
        }

        /// <summary>
        /// Compass heading of a direction on the XZ plane, in degrees: the same value as the Y
        /// rotation of a car with that forward vector (0 = +Z, 90 = +X).
        /// </summary>
        public static float Heading(Vector3 forward)
        {
            if (Mathf.Abs(forward.x) < 0.0001f && Mathf.Abs(forward.z) < 0.0001f) return 0f;
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Fills <paramref name="destination"/> with the stretch of a closed path that lies within
        /// <paramref name="radiusMetres"/> of point <paramref name="centreIndex"/>, wrapping over the
        /// start of the loop and staying in driving order. Returns where the centre point ended up in
        /// the result, or -1 for an empty path. Never returns more points than the path holds, so a
        /// radius longer than the track produces the whole loop instead of spinning.
        /// </summary>
        public static int Window(IList<Vector3> path, int centreIndex, float radiusMetres,
            List<Vector3> destination)
        {
            if (destination == null) return -1;
            destination.Clear();

            int count = path != null ? path.Count : 0;
            if (count == 0) return -1;

            centreIndex = Wrap(centreIndex, count);
            if (count == 1)
            {
                destination.Add(path[0]);
                return 0;
            }

            int back = 0;
            float travelled = 0f;
            while (back < count - 1 && travelled < radiusMetres)
            {
                travelled += FlatDistance(path[Wrap(centreIndex - back - 1, count)],
                    path[Wrap(centreIndex - back, count)]);
                back++;
            }

            int ahead = 0;
            travelled = 0f;
            while (back + ahead < count - 1 && travelled < radiusMetres)
            {
                travelled += FlatDistance(path[Wrap(centreIndex + ahead, count)],
                    path[Wrap(centreIndex + ahead + 1, count)]);
                ahead++;
            }

            for (int offset = -back; offset <= ahead; offset++)
                destination.Add(path[Wrap(centreIndex + offset, count)]);

            return back;
        }

        private static int Wrap(int index, int count) => ((index % count) + count) % count;

        private static float FlatDistance(Vector3 a, Vector3 b) =>
            Mathf.Sqrt(FlatSqrDistance(a, b));

        private static float FlatSqrDistance(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return x * x + z * z;
        }
    }
}
