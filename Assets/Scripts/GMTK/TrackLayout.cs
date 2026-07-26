using System.Collections.Generic;
using UnityEngine;
using GMTK.Kit;

namespace GMTK
{
    /// <summary>
    /// Scene-authored layout for the Map track. It keeps the starting grid and a readable
    /// centre-line waypoint chain in the scene so the BxB AI team can tune the path visually.
    /// </summary>
    [ExecuteAlways]
    public sealed class TrackLayout : MonoBehaviour
    {
        private static readonly Vector3[] CentreLine =
        {
            new Vector3(20f, 0.8f, 70f), new Vector3(20f, 0.8f, 90f),
            new Vector3(20f, 0.8f, 112f), new Vector3(28f, 0.8f, 128f),
            new Vector3(45f, 0.8f, 125f), new Vector3(62f, 0.8f, 112f),
            new Vector3(70f, 0.8f, 94f), new Vector3(70f, 0.8f, 76f),
            new Vector3(60f, 0.8f, 61f), new Vector3(60f, 0.8f, 42f),
            new Vector3(60f, 0.8f, 22f), new Vector3(60f, 0.8f, 4f),
            new Vector3(52f, 0.8f, -18f), new Vector3(40f, 0.8f, -30f),
            new Vector3(25f, 0.8f, -30f), new Vector3(20f, 0.8f, -12f),
            new Vector3(20f, 0.8f, 12f), new Vector3(20f, 0.8f, 35f),
            new Vector3(20f, 0.8f, 55f), new Vector3(20f, 0.8f, 70f)
        };

        [SerializeField] private bool applyOnEnable = true;
        [Tooltip("Starting grid transforms, in player/AI spawn order. Assign empty GameObjects from the scene.")]
        [SerializeField] private List<Transform> startingGridPoints = new();

        private void OnEnable()
        {
            if (applyOnEnable) ApplyLayout();
        }

        [ContextMenu("Apply grid and waypoints")]
        public void ApplyLayout()
        {
            ApplyRuntimeSpawner();
            ApplyLiveRaceCars();
            BuildWaypoints();
        }

        private void ApplyRuntimeSpawner()
        {
            if (startingGridPoints.Count == 0) return;

            var spawner = FindFirstObjectByType<PlayersSpawner>(FindObjectsInactive.Include);
            if (spawner == null) return;

            spawner.spawnPoints.Clear();
            foreach (var point in startingGridPoints)
                if (point != null) spawner.spawnPoints.Add(point);
        }

        private void ApplyLiveRaceCars()
        {
            for (int i = 0; i < startingGridPoints.Count; i++)
            {
                var point = startingGridPoints[i];
                var car = Race.CarByIndex(i);
                if (point == null || car == null) continue;

                car.transform.SetPositionAndRotation(point.position, point.rotation);
                foreach (var rb in car.GetComponentsInChildren<Rigidbody>(true))
                {
                    rb.position = point.position;
                    rb.rotation = point.rotation;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }

        private void BuildWaypoints()
        {
            var pathTransform = GameObject.Find("AIPath")?.transform;
            var root = pathTransform != null
                ? pathTransform.Find("Waypoints")
                : transform.Find("Waypoints");
            if (root == null)
            {
                root = new GameObject("Waypoints").transform;
                root.SetParent(pathTransform != null ? pathTransform : transform, false);
            }

            while (root.childCount > CentreLine.Length)
                DestroyImmediate(root.GetChild(root.childCount - 1).gameObject);

            for (int i = 0; i < CentreLine.Length; i++)
            {
                Transform point;
                if (i < root.childCount)
                    point = root.GetChild(i);
                else
                    point = new GameObject($"WP_{i:00}").transform;

                point.SetParent(root, false);
                point.position = CentreLine[i];
                point.rotation = i + 1 < CentreLine.Length
                    ? Quaternion.LookRotation(CentreLine[i + 1] - CentreLine[i], Vector3.up)
                    : point.rotation;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            foreach (var spawnPoint in startingGridPoints)
            {
                if (spawnPoint == null) continue;
                Gizmos.DrawSphere(spawnPoint.position, 0.45f);
                Gizmos.DrawRay(spawnPoint.position, spawnPoint.forward * 1.5f);
            }

            var pathTransform = GameObject.Find("AIPath")?.transform;
            var root = pathTransform != null
                ? pathTransform.Find("Waypoints")
                : transform.Find("Waypoints");
            if (root == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < root.childCount; i++)
            {
                var a = root.GetChild(i).position;
                var b = root.GetChild((i + 1) % root.childCount).position;
                Gizmos.DrawSphere(a, 0.7f);
                Gizmos.DrawLine(a, b);
            }
        }
    }
}
