using System;
using System.Collections.Generic;
using SpinMotion;
using UnityEngine;
using UnityEngine.Rendering;

namespace GMTK.TrackAuthoring
{
    [Serializable]
    public struct TrackControlPoint
    {
        public Vector3 position;
        [Min(2f)] public float width;
        [Range(-35f, 35f)] public float bank;

        public TrackControlPoint(Vector3 position, float width = 10f, float bank = 0f)
        {
            this.position = position;
            this.width = width;
            this.bank = bank;
        }
    }

    /// <summary>
    /// Designer-authored closed racing curve. Generated children are disposable outputs:
    /// road geometry, collision, AI waypoints, checkpoints, and starting grid.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class RaceTrackAuthoring : MonoBehaviour
    {
        private const string GeneratedRootName = "__GeneratedTrack";

        [Header("Shape")]
        [SerializeField] private bool closed = true;
        [SerializeField, Min(0.5f)] private float sampleSpacing = 2f;
        [SerializeField] private List<TrackControlPoint> controlPoints = new();

        [Header("Road")]
        [SerializeField] private Material roadMaterial;
        [SerializeField, Min(0.1f)] private float textureMetersPerTile = 5f;
        [SerializeField] private bool generateCollider = true;

        [Header("Gameplay")]
        [SerializeField, Min(1f)] private float aiWaypointSpacing = 8f;
        [SerializeField, Min(5f)] private float checkpointSpacing = 45f;
        [SerializeField, Min(1)] private int startingGridCount = 8;
        [SerializeField, Min(2f)] private float gridRowSpacing = 5f;
        [SerializeField, Range(0.1f, 0.45f)] private float gridLateralOffset = 0.25f;
        [SerializeField, Min(1f)] private float checkpointHeight = 4f;
        [SerializeField, Min(0f)] private float startDistance;

        [Header("Menu Presentation")]
        [Tooltip("Position relative to the start-line frame: X=sideways, Y=up, Z=along the track.")]
        [SerializeField] private Vector3 menuCameraOffset = new(7f, 8f, -15f);
        [SerializeField, Min(1f)] private float menuCameraLookAhead = 12f;
        [SerializeField] private Camera startMenuCamera;
        [SerializeField] private Canvas mainMenuCanvas;

        [Header("Project Integration")]
        [SerializeField] private GameObject waypointPrefab;
        [SerializeField] private GameObject checkpointPrefab;
        [SerializeField] private GameObject finishCheckpointPrefab;
        [SerializeField] private AIWaypoints aiWaypoints;
        [SerializeField] private Checkpoints checkpoints;
        [SerializeField] private PlayersSpawner playersSpawner;

        public IReadOnlyList<TrackControlPoint> ControlPoints => controlPoints;
        public bool Closed => closed;
        public float ApproximateLength => BuildSamples().length;

        public void ResetToStarterLoop()
        {
            controlPoints = new List<TrackControlPoint>
            {
                new(new Vector3(-35f, 0f, -25f), 11f),
                new(new Vector3(0f, 0f, -42f), 11f),
                new(new Vector3(38f, 2f, -20f), 10f, 4f),
                new(new Vector3(42f, 5f, 22f), 10f, -5f),
                new(new Vector3(5f, 1f, 42f), 12f),
                new(new Vector3(-42f, 0f, 22f), 10f, 5f)
            };
        }

        public void AddControlPointAfter(int index)
        {
            if (controlPoints.Count < 2)
            {
                ResetToStarterLoop();
                return;
            }

            int next = (index + 1) % controlPoints.Count;
            TrackControlPoint a = controlPoints[Mathf.Clamp(index, 0, controlPoints.Count - 1)];
            TrackControlPoint b = controlPoints[next];
            controlPoints.Insert(index + 1, new TrackControlPoint(
                Vector3.Lerp(a.position, b.position, 0.5f),
                Mathf.Lerp(a.width, b.width, 0.5f),
                Mathf.Lerp(a.bank, b.bank, 0.5f)));
        }

        public void RemoveControlPoint(int index)
        {
            if (controlPoints.Count <= (closed ? 4 : 2)) return;
            controlPoints.RemoveAt(index);
        }

        public void SetControlPoint(int index, TrackControlPoint point) => controlPoints[index] = point;

        public void AutoFindSceneSystems()
        {
            if (aiWaypoints == null)
                aiWaypoints = FindFirstObjectByType<AIWaypoints>(FindObjectsInactive.Include);
            if (checkpoints == null)
                checkpoints = FindFirstObjectByType<Checkpoints>(FindObjectsInactive.Include);
            if (playersSpawner == null)
                playersSpawner = FindFirstObjectByType<PlayersSpawner>(FindObjectsInactive.Include);
            if (startMenuCamera == null)
            {
                StartMenuCamera controller =
                    FindFirstObjectByType<StartMenuCamera>(FindObjectsInactive.Include);
                if (controller != null) startMenuCamera = controller.GetComponent<Camera>();
            }
            if (mainMenuCanvas == null)
            {
                StartMenuCanvas menu =
                    FindFirstObjectByType<StartMenuCanvas>(FindObjectsInactive.Include);
                if (menu != null) mainMenuCanvas = menu.GetComponent<Canvas>();
            }
        }

        [ContextMenu("Bake Track")]
        public void Bake()
        {
            if (!TryValidate(out string problem))
            {
                Debug.LogError($"Cannot bake track: {problem}", this);
                return;
            }

            AutoFindSceneSystems();
            ClearGenerated();
            SampleCollection samples = BuildSamples();
            Transform generated = NewChild(transform, GeneratedRootName);

            BuildRoad(generated, samples);
            BuildWaypoints(generated, samples);
            BuildCheckpoints(generated, samples);
            BuildStartingGrid(generated, samples);
            PlaceMenuPresentation(samples);

            Debug.Log($"Baked '{name}': {samples.length:0} m, {samples.items.Count} road samples.", this);
        }

        [ContextMenu("Clear Generated Track")]
        public void ClearGenerated()
        {
            Transform old = transform.Find(GeneratedRootName);
            if (old != null) DestroyObject(old.gameObject);

            if (aiWaypoints != null)
                ClearChildren(aiWaypoints.transform);
            if (checkpoints != null)
                ClearChildren(checkpoints.transform);
        }

        public bool TryValidate(out string problem)
        {
            int minimum = closed ? 4 : 2;
            if (controlPoints.Count < minimum)
            {
                problem = $"Add at least {minimum} control points.";
                return false;
            }

            for (int i = 0; i < controlPoints.Count; i++)
            {
                int next = (i + 1) % controlPoints.Count;
                if (!closed && i == controlPoints.Count - 1) break;
                if ((controlPoints[i].position - controlPoints[next].position).sqrMagnitude < 1f)
                {
                    problem = $"Control points {i} and {next} overlap.";
                    return false;
                }
            }

            problem = string.Empty;
            return true;
        }

        public void EvaluateDistance(float distance, out Vector3 localPosition, out Quaternion localRotation, out float width)
        {
            SampleCollection samples = BuildSamples();
            EvaluateSamples(samples, distance, out localPosition, out localRotation, out width);
        }

        private void BuildRoad(Transform generated, SampleCollection samples)
        {
            GameObject road = new("Road");
            road.transform.SetParent(generated, false);
            MeshFilter filter = road.AddComponent<MeshFilter>();
            MeshRenderer renderer = road.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = roadMaterial;

            int count = samples.items.Count;
            var vertices = new Vector3[count * 2];
            var normals = new Vector3[count * 2];
            var uvs = new Vector2[count * 2];
            int segmentCount = closed ? count : count - 1;
            var triangles = new int[segmentCount * 6];

            for (int i = 0; i < count; i++)
            {
                TrackSample sample = samples.items[i];
                Vector3 right = sample.rotation * Vector3.right;
                vertices[i * 2] = sample.position - right * (sample.width * 0.5f);
                vertices[i * 2 + 1] = sample.position + right * (sample.width * 0.5f);
                Vector3 normal = sample.rotation * Vector3.up;
                normals[i * 2] = normals[i * 2 + 1] = normal;
                float v = sample.distance / textureMetersPerTile;
                uvs[i * 2] = new Vector2(0f, v);
                uvs[i * 2 + 1] = new Vector2(1f, v);
            }

            for (int i = 0; i < segmentCount; i++)
            {
                int next = (i + 1) % count;
                int t = i * 6;
                int left = i * 2;
                int right = left + 1;
                int nextLeft = next * 2;
                int nextRight = nextLeft + 1;
                triangles[t] = left;
                triangles[t + 1] = nextLeft;
                triangles[t + 2] = right;
                triangles[t + 3] = right;
                triangles[t + 4] = nextLeft;
                triangles[t + 5] = nextRight;
            }

            Mesh mesh = new() { name = $"{name}_Road" };
            if (vertices.Length > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;

            if (generateCollider)
            {
                MeshCollider collider = road.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }
        }

        private void BuildWaypoints(Transform generated, SampleCollection samples)
        {
            Transform root = aiWaypoints != null
                ? aiWaypoints.transform
                : NewChild(generated, "AI Waypoints");
            if (aiWaypoints != null) ClearChildren(root);

            int count = Mathf.Max(2, Mathf.CeilToInt(samples.length / aiWaypointSpacing));
            for (int i = 0; i < count; i++)
            {
                float distance = samples.length * i / count;
                EvaluateSamples(samples, distance, out Vector3 position, out Quaternion rotation, out _);
                GameObject point = waypointPrefab != null
                    ? Instantiate(waypointPrefab)
                    : GameObject.CreatePrimitive(PrimitiveType.Cube);
                point.name = $"Waypoint_{i:000}";
                point.transform.SetParent(root, false);
                point.transform.SetPositionAndRotation(
                    transform.TransformPoint(position + rotation * Vector3.up * 0.5f),
                    transform.rotation * rotation);
                point.transform.localScale = Vector3.one;
                if (point.TryGetComponent(out Collider collider)) DestroyObject(collider);
            }
        }

        private void BuildCheckpoints(Transform generated, SampleCollection samples)
        {
            Transform root = checkpoints != null
                ? checkpoints.transform
                : NewChild(generated, "Checkpoints");
            if (checkpoints != null) ClearChildren(root);

            int count = Mathf.Max(2, Mathf.CeilToInt(samples.length / checkpointSpacing));
            for (int i = 0; i < count; i++)
            {
                float distance = RepeatDistance(startDistance + samples.length * i / count, samples.length);
                EvaluateSamples(samples, distance, out Vector3 position, out Quaternion rotation, out float width);
                GameObject prefab = i == 0 && finishCheckpointPrefab != null
                    ? finishCheckpointPrefab
                    : checkpointPrefab;
                GameObject checkpoint = prefab != null ? Instantiate(prefab) : new GameObject();
                checkpoint.name = i == 0 ? "Checkpoint_001_Finish" : $"Checkpoint_{i + 1:000}";
                checkpoint.transform.SetParent(root, false);
                checkpoint.transform.SetPositionAndRotation(
                    transform.TransformPoint(position + rotation * Vector3.up * (checkpointHeight * 0.5f)),
                    transform.rotation * rotation);

                BoxCollider box = checkpoint.GetComponent<BoxCollider>();
                if (box == null) box = checkpoint.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = Vector3.zero;
                box.size = new Vector3(width, checkpointHeight, 1f);
                if (checkpoint.GetComponent<Checkpoint>() == null)
                    checkpoint.AddComponent<Checkpoint>();
            }
        }

        private void BuildStartingGrid(Transform generated, SampleCollection samples)
        {
            Transform root = NewChild(generated, "Starting Grid");
            var points = new List<Transform>(startingGridCount);

            for (int i = 0; i < startingGridCount; i++)
            {
                int row = i / 2;
                float distance = RepeatDistance(startDistance - 6f - row * gridRowSpacing, samples.length);
                EvaluateSamples(samples, distance, out Vector3 position, out Quaternion rotation, out float width);
                float side = i % 2 == 0 ? -1f : 1f;
                Transform point = NewChild(root, $"Grid_{i + 1:00}");
                point.localPosition = position + rotation * Vector3.right * (side * width * gridLateralOffset)
                    + rotation * Vector3.up * 0.35f;
                point.localRotation = rotation;
                points.Add(point);
            }

            if (playersSpawner != null)
            {
                playersSpawner.spawnPoints.Clear();
                playersSpawner.spawnPoints.AddRange(points);
            }
        }

        private void PlaceMenuPresentation(SampleCollection samples)
        {
            EvaluateSamples(samples, startDistance,
                out Vector3 startPosition, out Quaternion startRotation, out _);
            EvaluateSamples(samples, startDistance + menuCameraLookAhead,
                out Vector3 lookPosition, out _, out _);

            Vector3 cameraLocalPosition = startPosition + startRotation * menuCameraOffset;
            Vector3 cameraWorldPosition = transform.TransformPoint(cameraLocalPosition);
            Vector3 lookWorldPosition = transform.TransformPoint(lookPosition);
            Quaternion cameraWorldRotation = Quaternion.LookRotation(
                lookWorldPosition - cameraWorldPosition, transform.up);

            if (startMenuCamera != null)
                startMenuCamera.transform.SetPositionAndRotation(
                    cameraWorldPosition, cameraWorldRotation);

            if (mainMenuCanvas == null) return;

            if (mainMenuCanvas.renderMode == RenderMode.ScreenSpaceCamera)
                mainMenuCanvas.worldCamera = startMenuCamera;
            else if (mainMenuCanvas.renderMode == RenderMode.WorldSpace)
            {
                Transform canvasTransform = mainMenuCanvas.transform;
                canvasTransform.position = cameraWorldPosition
                    + cameraWorldRotation * Vector3.forward * 3f;
                canvasTransform.rotation = cameraWorldRotation;
            }
        }

        private SampleCollection BuildSamples()
        {
            var result = new SampleCollection();
            if (controlPoints.Count < 2) return result;

            int segmentCount = closed ? controlPoints.Count : controlPoints.Count - 1;
            Vector3 previous = EvaluateSegment(0, 0f, out _, out _);
            float distance = 0f;

            for (int segment = 0; segment < segmentCount; segment++)
            {
                float chord = Vector3.Distance(controlPoints[segment].position,
                    controlPoints[(segment + 1) % controlPoints.Count].position);
                int steps = Mathf.Max(4, Mathf.CeilToInt(chord / sampleSpacing));
                int start = segment == 0 ? 0 : 1;
                for (int step = start; step <= steps; step++)
                {
                    if (closed && segment == segmentCount - 1 && step == steps) break;
                    float t = step / (float)steps;
                    Vector3 position = EvaluateSegment(segment, t, out float width, out float bank);
                    Vector3 tangent = EvaluateTangent(segment, t);
                    if (result.items.Count > 0) distance += Vector3.Distance(previous, position);
                    Quaternion rotation = MakeRotation(tangent, bank);
                    result.items.Add(new TrackSample(position, rotation, width, distance));
                    previous = position;
                }
            }

            if (closed && result.items.Count > 1)
                distance += Vector3.Distance(result.items[^1].position, result.items[0].position);
            result.length = distance;
            return result;
        }

        private Vector3 EvaluateSegment(int segment, float t, out float width, out float bank)
        {
            int count = controlPoints.Count;
            int i1 = Mathf.Clamp(segment, 0, count - 1);
            int i2 = closed ? (i1 + 1) % count : Mathf.Min(i1 + 1, count - 1);
            int i0 = closed ? (i1 - 1 + count) % count : Mathf.Max(i1 - 1, 0);
            int i3 = closed ? (i2 + 1) % count : Mathf.Min(i2 + 1, count - 1);
            width = Mathf.Lerp(controlPoints[i1].width, controlPoints[i2].width, t);
            bank = Mathf.Lerp(controlPoints[i1].bank, controlPoints[i2].bank, t);
            return CatmullRom(controlPoints[i0].position, controlPoints[i1].position,
                controlPoints[i2].position, controlPoints[i3].position, t);
        }

        private Vector3 EvaluateTangent(int segment, float t)
        {
            const float epsilon = 0.005f;
            Vector3 before = EvaluateSegment(segment, Mathf.Max(0f, t - epsilon), out _, out _);
            Vector3 after = EvaluateSegment(segment, Mathf.Min(1f, t + epsilon), out _, out _);
            Vector3 tangent = after - before;
            return tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.forward;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static Quaternion MakeRotation(Vector3 tangent, float bank)
        {
            Quaternion forward = Quaternion.LookRotation(tangent, Vector3.up);
            return forward * Quaternion.AngleAxis(bank, Vector3.forward);
        }

        private static void EvaluateSamples(SampleCollection samples, float distance,
            out Vector3 position, out Quaternion rotation, out float width)
        {
            if (samples.items.Count == 0)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                width = 10f;
                return;
            }

            distance = RepeatDistance(distance, samples.length);
            for (int i = 1; i < samples.items.Count; i++)
            {
                if (samples.items[i].distance < distance) continue;
                TrackSample a = samples.items[i - 1];
                TrackSample b = samples.items[i];
                float t = Mathf.InverseLerp(a.distance, b.distance, distance);
                position = Vector3.Lerp(a.position, b.position, t);
                rotation = Quaternion.Slerp(a.rotation, b.rotation, t);
                width = Mathf.Lerp(a.width, b.width, t);
                return;
            }

            TrackSample last = samples.items[^1];
            TrackSample first = samples.items[0];
            float closingDistance = samples.length - last.distance;
            float closingT = closingDistance > 0.001f ? (distance - last.distance) / closingDistance : 0f;
            position = Vector3.Lerp(last.position, first.position, closingT);
            rotation = Quaternion.Slerp(last.rotation, first.rotation, closingT);
            width = Mathf.Lerp(last.width, first.width, closingT);
        }

        private static float RepeatDistance(float distance, float length) =>
            length > 0f ? Mathf.Repeat(distance, length) : 0f;

        private static Transform NewChild(Transform parent, string objectName)
        {
            GameObject child = new(objectName);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                DestroyObject(root.GetChild(i).gameObject);
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private void Reset()
        {
            ResetToStarterLoop();
            AutoFindSceneSystems();
        }

        private void OnDrawGizmos()
        {
            SampleCollection samples = BuildSamples();
            if (samples.items.Count < 2) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.1f, 0.85f, 1f, 1f);
            for (int i = 0; i < samples.items.Count - 1; i++)
                Gizmos.DrawLine(samples.items[i].position, samples.items[i + 1].position);
            if (closed)
                Gizmos.DrawLine(samples.items[^1].position, samples.items[0].position);
        }

        private readonly struct TrackSample
        {
            public readonly Vector3 position;
            public readonly Quaternion rotation;
            public readonly float width;
            public readonly float distance;

            public TrackSample(Vector3 position, Quaternion rotation, float width, float distance)
            {
                this.position = position;
                this.rotation = rotation;
                this.width = width;
                this.distance = distance;
            }
        }

        private sealed class SampleCollection
        {
            public readonly List<TrackSample> items = new();
            public float length;
        }
    }
}
