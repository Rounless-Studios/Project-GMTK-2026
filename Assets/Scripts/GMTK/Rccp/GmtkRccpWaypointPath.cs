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
