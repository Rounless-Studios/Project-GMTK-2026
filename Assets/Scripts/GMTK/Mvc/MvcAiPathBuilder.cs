using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using MVC.AI;

namespace GMTK.Mvc
{
    /// <summary>
    /// Builds an MVC <see cref="VehicleAIPath"/> spline at runtime from ordered world-space
    /// points, so the AI can drive a track without hand-authoring a path in the editor. The
    /// points come from the Racing Starter Kit's existing AI waypoints (see
    /// <see cref="HarvestKitWaypoints"/>), letting MVC's built-in path follower reuse the track
    /// the kit already ships. (MVC vehicle integration, checklist stage 2-3.)
    /// </summary>
    public static class MvcAiPathBuilder
    {
        /// <summary>Create a VehicleAIPath GameObject from ordered world points.</summary>
        public static VehicleAIPath BuildPath(string name, IList<Vector3> points, bool looped, float width = 8f)
        {
            var go = new GameObject(name);
            var path = go.AddComponent<VehicleAIPath>();
            path.NewPath();
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                path.AddNextCurve(new float3(p.x, p.y, p.z));
            }
            path.LoopedPath = looped;
            if (width > 0f) path.SetCurvesWidth(width);
            path.GenerateSpacedPoints();
            path.AlignSpacedPointsToGround();
            return path;
        }

        /// <summary>
        /// Ordered world positions of the kit's AI waypoints (direct children of the
        /// SpinMotion.AIWaypoints object that carry a MeshRenderer, matching the kit's own
        /// harvesting). Empty if the waypoints object is absent.
        /// </summary>
        public static List<Vector3> HarvestKitWaypoints()
        {
            var points = new List<Vector3>();
            var waypoints = Object.FindAnyObjectByType<SpinMotion.AIWaypoints>();
            if (waypoints == null) return points;
            foreach (Transform child in waypoints.transform)
                if (child.TryGetComponent<MeshRenderer>(out _))
                    points.Add(child.position);
            return points;
        }
    }
}
