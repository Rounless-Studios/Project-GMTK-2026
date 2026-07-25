using System.Collections.Generic;
using UnityEngine;

namespace Gmtk2026.GameBalance
{
    /// <summary>
    /// Race progress measured as distance driven along the track's closed centre path. Ranking and
    /// elimination only need a monotonic "how far is this car" value, and projecting onto the path
    /// gives that continuously in metres instead of in gate-sized steps — no trigger colliders, no
    /// per-gate components, and no way to lose a car's position by missing one gate.
    /// Engine-free apart from Vector3/Mathf so it stays unit-testable.
    /// </summary>
    public sealed class TrackProgress
    {
        /// <summary>
        /// Per-car cursor. Keeping the last segment makes each update a local search, which is both
        /// cheap and the only way a self-crossing track cannot snap a car onto the wrong straight.
        /// </summary>
        public struct CarCursor
        {
            public int segment;
            public float progressMetres;
            public int lap;
            public bool placed;
        }

        private readonly Vector3[] points;
        private readonly float[] segmentLength;
        private readonly float[] entryDistance;
        private readonly int searchWindow;
        private readonly float relocateSqrDistance;

        /// <summary>Total path length in metres (the closing segment included).</summary>
        public float Length { get; }

        public int SegmentCount => points.Length;

        public bool IsUsable => points.Length >= 2 && Length > 0.001f;

        /// <param name="pathPoints">Centre-path points in driving order; treated as a closed loop.</param>
        /// <param name="searchWindowSegments">Segments scanned either side of a car's last segment.</param>
        /// <param name="relocateDistanceMetres">
        /// If the nearest point inside the window is further away than this, the car was moved rather
        /// than driven (fall respawn, editor drag) and the whole path is searched again.
        /// </param>
        public TrackProgress(IList<Vector3> pathPoints, int searchWindowSegments = 12,
            float relocateDistanceMetres = 30f)
        {
            int count = pathPoints != null ? pathPoints.Count : 0;
            points = new Vector3[count];
            segmentLength = new float[count];
            entryDistance = new float[count];
            searchWindow = Mathf.Max(1, searchWindowSegments);
            relocateSqrDistance = Mathf.Max(0.01f, relocateDistanceMetres) * Mathf.Max(0.01f, relocateDistanceMetres);

            for (int i = 0; i < count; i++)
                points[i] = pathPoints[i];

            if (count < 2)
            {
                Length = 0f;
                return;
            }

            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                entryDistance[i] = total;
                segmentLength[i] = Vector3.Distance(points[i], points[(i + 1) % count]);
                total += segmentLength[i];
            }

            Length = total;
        }

        /// <summary>
        /// Advance a car's cursor to <paramref name="position"/>, counting a lap when it passes the
        /// path's start point going forwards.
        /// </summary>
        public void Advance(ref CarCursor cursor, Vector3 position)
        {
            if (!IsUsable) return;

            int segment;
            float t;
            bool relocated = false;

            if (cursor.placed)
            {
                FindNearby(position, cursor.segment, out segment, out t, out float sqrDistance);

                // moved, not driven: the window no longer contains the car at all
                if (sqrDistance > relocateSqrDistance)
                {
                    FindAnywhere(position, out segment, out t);
                    relocated = true;
                }
            }
            else
            {
                FindAnywhere(position, out segment, out t);
            }

            float progress = entryDistance[segment] + t * segmentLength[segment];

            if (!cursor.placed)
            {
                cursor.placed = true;
                cursor.lap = 0;
            }
            else if (!relocated)
            {
                // no car drives half a lap between two frames, so a jump that large is the start line.
                // A relocated car did not drive at all, so its lap count must be left alone — otherwise
                // a fall respawn past the line would award or steal a lap.
                float delta = progress - cursor.progressMetres;
                if (delta < -Length * 0.5f) cursor.lap++;
                else if (delta > Length * 0.5f) cursor.lap = Mathf.Max(0, cursor.lap - 1);
            }

            cursor.segment = segment;
            cursor.progressMetres = progress;
        }

        /// <summary>Distance driven since the race start: the value races are ranked by.</summary>
        public double TotalDistance(in CarCursor cursor)
        {
            return cursor.lap * (double)Length + cursor.progressMetres;
        }

        /// <summary>Forget where a car was, so the next <see cref="Advance"/> searches the whole path.</summary>
        public static void Unplace(ref CarCursor cursor)
        {
            cursor.placed = false;
            cursor.segment = 0;
            cursor.progressMetres = 0f;
        }

        private void FindNearby(Vector3 position, int centre, out int bestSegment, out float bestT,
            out float bestSqrDistance)
        {
            int count = points.Length;
            bestSegment = ((centre % count) + count) % count;
            bestT = 0f;
            bestSqrDistance = float.MaxValue;

            for (int offset = -searchWindow; offset <= searchWindow; offset++)
            {
                int segment = ((centre + offset) % count + count) % count;
                float t = Project(segment, position, out float sqrDistance);

                if (sqrDistance >= bestSqrDistance) continue;
                bestSqrDistance = sqrDistance;
                bestSegment = segment;
                bestT = t;
            }
        }

        private void FindAnywhere(Vector3 position, out int bestSegment, out float bestT)
        {
            bestSegment = 0;
            bestT = 0f;
            float bestSqrDistance = float.MaxValue;

            for (int segment = 0; segment < points.Length; segment++)
            {
                float t = Project(segment, position, out float sqrDistance);

                if (sqrDistance >= bestSqrDistance) continue;
                bestSqrDistance = sqrDistance;
                bestSegment = segment;
                bestT = t;
            }
        }

        /// <summary>Normalised position of the closest point on a segment, plus its squared distance.</summary>
        private float Project(int segment, Vector3 position, out float sqrDistance)
        {
            Vector3 from = points[segment];
            Vector3 to = points[(segment + 1) % points.Length];
            float length = segmentLength[segment];

            if (length <= 0.0001f)
            {
                sqrDistance = (position - from).sqrMagnitude;
                return 0f;
            }

            Vector3 direction = (to - from) / length;
            float along = Mathf.Clamp(Vector3.Dot(position - from, direction), 0f, length);
            sqrDistance = (position - (from + direction * along)).sqrMagnitude;
            return along / length;
        }
    }
}
