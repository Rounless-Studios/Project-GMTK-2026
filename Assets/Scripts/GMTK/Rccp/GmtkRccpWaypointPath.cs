using System.Collections.Generic;
using UnityEngine;
using SpinMotion;

namespace GMTK.Rccp
{
    /// <summary>
    /// Read-only RCCP path built from the track's existing Racing Starter Kit waypoints.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GmtkRccpWaypointPath : MonoBehaviour
    {
        private readonly List<Transform> waypoints = new();

        public int Count => waypoints.Count;

        public Transform this[int index] => waypoints[index];

        public static GmtkRccpWaypointPath GetOrCreate()
        {
            GmtkRccpWaypointPath existing =
                Object.FindAnyObjectByType<GmtkRccpWaypointPath>();

            if (existing != null)
                return existing;

            GameObject pathObject = new("GMTK_RCCP_WaypointPath");
            GmtkRccpWaypointPath path = pathObject.AddComponent<GmtkRccpWaypointPath>();
            path.CollectWaypoints();
            return path;
        }

        /// <summary>
        /// Point <paramref name="distance"/> metres further along the path, measured from
        /// <paramref name="origin"/> through the waypoint at <paramref name="startIndex"/>. Lets the
        /// AI aim where the track goes instead of at the next marker.
        /// </summary>
        public Vector3 SamplePointAhead(int startIndex, Vector3 origin, float distance)
        {
            if (waypoints.Count == 0)
                return origin;

            Vector3 from = origin;
            int index = startIndex;

            for (int step = 0; step < waypoints.Count; step++)
            {
                Vector3 to = waypoints[index].position;
                float segment = Vector3.Distance(from, to);

                if (segment >= distance)
                    return segment <= 0.001f ? to : Vector3.Lerp(from, to, distance / segment);

                distance -= segment;
                from = to;
                index = (index + 1) % waypoints.Count;
            }

            return from;
        }

        /// <summary>
        /// Total heading change in degrees over the next <paramref name="distance"/> metres, used to
        /// pick a corner speed before the corner instead of reacting inside it.
        /// </summary>
        public float HeadingChangeAhead(int startIndex, float distance)
        {
            if (waypoints.Count < 3)
                return 0f;

            float travelled = 0f;
            float change = 0f;
            int index = startIndex;

            for (int step = 0; step < waypoints.Count && travelled < distance; step++)
            {
                Vector3 current = waypoints[index].position;
                Vector3 next = waypoints[(index + 1) % waypoints.Count].position;
                Vector3 after = waypoints[(index + 2) % waypoints.Count].position;

                Vector3 incoming = next - current;
                Vector3 outgoing = after - next;
                incoming.y = 0f;
                outgoing.y = 0f;

                if (incoming.sqrMagnitude > 0.01f && outgoing.sqrMagnitude > 0.01f)
                    change += Vector3.Angle(incoming, outgoing);

                travelled += incoming.magnitude;
                index = (index + 1) % waypoints.Count;
            }

            return change;
        }

        public int FindClosestIndex(Vector3 position)
        {
            int closestIndex = 0;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < waypoints.Count; i++)
            {
                float distance = (waypoints[i].position - position).sqrMagnitude;

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        private void CollectWaypoints()
        {
            AIWaypoints source = Object.FindAnyObjectByType<AIWaypoints>();

            if (source == null)
            {
                Debug.LogError("RCCP migration: no Racing Starter Kit AI waypoint source found.");
                return;
            }

            Transform[] candidates = source.GetComponentsInChildren<Transform>(true);

            for (int i = 1; i < candidates.Length; i++)
            {
                if (candidates[i].TryGetComponent(out MeshRenderer _))
                    waypoints.Add(candidates[i]);
            }

            if (waypoints.Count == 0)
                Debug.LogError("RCCP migration: the track waypoint source is empty.");
        }
    }
}
