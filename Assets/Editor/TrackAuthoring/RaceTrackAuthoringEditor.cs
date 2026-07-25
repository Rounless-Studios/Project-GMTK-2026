using System.IO;
using GMTK.TrackAuthoring;
using SpinMotion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GMTK.Editor.TrackAuthoring
{
    [CustomEditor(typeof(RaceTrackControlPointHandle))]
    public sealed class RaceTrackControlPointHandleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            RaceTrackControlPointHandle handle = (RaceTrackControlPointHandle)target;
            RaceTrackAuthoring owner = handle.Owner;
            if (owner == null || handle.PointIndex < 0 ||
                handle.PointIndex >= owner.ControlPoints.Count)
            {
                EditorGUILayout.HelpBox("This visual handle is not linked to track data.",
                    MessageType.Warning);
                return;
            }

            int index = handle.PointIndex;
            TrackControlPoint point = owner.ControlPoints[index];
            EditorGUILayout.LabelField($"Track Control Point {index}", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Outgoing Segment controls the cyan line from this point to the next point.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            TrackSegmentMode mode = (TrackSegmentMode)EditorGUILayout.EnumPopup(
                "Outgoing Segment", point.segmentToNext);
            float width = point.width;
            using (new EditorGUI.DisabledScope(owner.UsesGlobalWidth))
                width = EditorGUILayout.FloatField("Road Width", point.width);
            float bank = EditorGUILayout.Slider("Bank", point.bank, -35f, 35f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(owner, "Edit Track Segment");
                point.segmentToNext = mode;
                point.width = Mathf.Max(2f, width);
                point.bank = bank;
                owner.SetControlPoint(index, point);
                owner.RefreshPreviewLine();
                EditorUtility.SetDirty(owner);
                if (owner.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);
            }

            if (owner.UsesGlobalWidth)
                EditorGUILayout.HelpBox(
                    $"Global width override is active: {owner.GlobalWidth:0.##} m",
                    MessageType.Info);

            if (GUILayout.Button("Select Track Authoring"))
                Selection.activeGameObject = owner.gameObject;
        }
    }

    [CustomEditor(typeof(RaceTrackAuthoring))]
    public sealed class RaceTrackAuthoringEditor : UnityEditor.Editor
    {
        private const string WaypointRootPrefab =
            "Assets/Racing Starter Kit/RSK Assets/Prefabs/Track Based/AI Waypoints.prefab";
        private const string CheckpointRootPrefab =
            "Assets/Racing Starter Kit/RSK Assets/Prefabs/Track Based/Checkpoints.prefab";
        private const string SpawningRootPrefab =
            "Assets/Racing Starter Kit/RSK Assets/Prefabs/Track Based/Spawning.prefab";
        private const string WaypointPrefab =
            "Assets/Racing Starter Kit/RSK Assets/Prefabs/AI Waypoint.prefab";
        private const string CheckpointPrefab =
            "Assets/Racing Starter Kit/RSK Assets/Prefabs/Checkpoint.prefab";
        private const string FinishPrefab =
            "Assets/Racing Starter Kit/RSK Assets/Prefabs/Race Finish Checkpoint Variant.prefab";
        private const string RoadMaterial = "Assets/Track/Materials/Tarmac.mat";

        private int selectedPoint;
        private static readonly Color PointColor = new(0f, 1f, 1f, 1f);
        private static readonly Color SelectedPointColor = new(1f, 0.65f, 0f, 1f);

        private void OnEnable()
        {
            EnsureVisualHandles((RaceTrackAuthoring)target);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Edit the cyan numbered points in the Scene view. Width and bank are editable " +
                "in Control Points. Bake regenerates all disposable road/gameplay outputs.",
                MessageType.Info);

            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            RaceTrackAuthoring track = (RaceTrackAuthoring)target;
            EnsureVisualHandles(track);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Approximate length: {track.ApproximateLength:0} m",
                EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Frame Whole Track"))
                    FrameTrack(track);

                if (GUILayout.Button("Add Point At End"))
                {
                    Undo.RecordObject(track, "Append Track Point");
                    track.AddControlPointAtEnd();
                    selectedPoint = track.ControlPoints.Count - 1;
                    RebuildVisualHandles(track);
                    EditorUtility.SetDirty(track);
                }

                using (new EditorGUI.DisabledScope(
                    track.ControlPoints.Count <= (track.Closed ? 4 : 2)))
                {
                    if (GUILayout.Button("Remove Selected"))
                    {
                        Undo.RecordObject(track, "Remove Track Point");
                        track.RemoveControlPoint(selectedPoint);
                        selectedPoint = Mathf.Clamp(selectedPoint - 1, 0, track.ControlPoints.Count - 1);
                        RebuildVisualHandles(track);
                        EditorUtility.SetDirty(track);
                    }
                }
            }

            if (GUILayout.Button("Auto-setup Racing Systems"))
                SetupProjectIntegration(track, true);

            if (GUILayout.Button("Rebuild Visible Control Points"))
                RebuildVisualHandles(track);

            if (GUILayout.Button("Generate Rolling Heights"))
            {
                Undo.RecordObject(track, "Generate Track Heights");
                track.GenerateRollingHeights();
                SnapVisualHandlesToAuthoring(track);
                EditorUtility.SetDirty(track);
                MarkSceneDirty(track);
            }

            EditorGUILayout.Space();
            GUI.backgroundColor = new Color(0.35f, 0.9f, 0.45f);
            if (GUILayout.Button("Bake Track", GUILayout.Height(34f)))
                BakeAndPersist(track);
            GUI.backgroundColor = Color.white;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Bake & Play"))
                {
                    BakeAndPersist(track);
                    if (!EditorApplication.isPlayingOrWillChangePlaymode)
                        EditorApplication.EnterPlaymode();
                }

                if (GUILayout.Button("Clear Generated"))
                {
                    Undo.RegisterFullObjectHierarchyUndo(track.gameObject, "Clear Generated Track");
                    track.ClearGenerated();
                    MarkSceneDirty(track);
                }
            }

            if (!track.TryValidate(out string problem))
                EditorGUILayout.HelpBox(problem, MessageType.Error);
        }

        private void OnSceneGUI()
        {
            RaceTrackAuthoring track = (RaceTrackAuthoring)target;
            if (track.ControlPoints.Count == 0) return;

            CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            Handles.matrix = track.transform.localToWorldMatrix;

            Handles.color = new Color(0f, 1f, 1f, 0.95f);
            for (int i = 0; i < track.ControlPoints.Count; i++)
            {
                int next = (i + 1) % track.ControlPoints.Count;
                if (!track.Closed && i == track.ControlPoints.Count - 1) break;
                Handles.DrawAAPolyLine(7f,
                    track.ControlPoints[i].position,
                    track.ControlPoints[next].position);
            }

            for (int i = 0; i < track.ControlPoints.Count; i++)
            {
                TrackControlPoint point = track.ControlPoints[i];
                float size = HandleUtility.GetHandleSize(
                    track.transform.TransformPoint(point.position)) * 0.32f;

                Handles.color = i == selectedPoint ? SelectedPointColor : PointColor;
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.FreeMoveHandle(
                    point.position, size, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(track, "Move Track Point");
                    selectedPoint = i;
                    point.position = moved;
                    track.SetControlPoint(i, point);
                    EditorUtility.SetDirty(track);
                    Repaint();
                }

                Handles.color = Color.black;
                SceneView drawingView = SceneView.currentDrawingSceneView;
                Vector3 discNormal = drawingView != null && drawingView.camera != null
                    ? drawingView.camera.transform.forward
                    : Vector3.up;
                Handles.DrawWireDisc(point.position, discNormal, size * 0.62f, 5f);

                GUIStyle label = new(EditorStyles.boldLabel)
                {
                    normal = { textColor = i == selectedPoint ? SelectedPointColor : PointColor },
                    fontSize = 15
                };
                Handles.Label(point.position + Vector3.up * size * 0.8f,
                    $"POINT {i}   W:{point.width:0.0}   B:{point.bank:0}°", label);
            }

            selectedPoint = Mathf.Clamp(selectedPoint, 0, track.ControlPoints.Count - 1);

            Handles.matrix = Matrix4x4.identity;
            Handles.zTest = previousZTest;
            SceneView.RepaintAll();
        }

        [MenuItem("GameObject/GMTK/Race Track Authoring", false, 10)]
        private static void CreateTrack(MenuCommand command)
        {
            GameObject root = new("Race Track Authoring");
            Undo.RegisterCreatedObjectUndo(root, "Create Race Track");
            GameObjectUtility.SetParentAndAlign(root, command.context as GameObject);
            RaceTrackAuthoring track = Undo.AddComponent<RaceTrackAuthoring>(root);
            SetupProjectIntegration(track, true);
            Selection.activeGameObject = root;
            FrameTrack(track);
            MarkSceneDirty(track);
        }

        private static void FrameTrack(RaceTrackAuthoring track)
        {
            EnsureVisualHandles(track);
            RaceTrackControlPointHandle[] handles =
                track.GetComponentsInChildren<RaceTrackControlPointHandle>(true);
            if (handles.Length > 0)
            {
                Object[] previousSelection = Selection.objects;
                Selection.objects = System.Array.ConvertAll(handles, item => (Object)item.gameObject);
                SceneView.lastActiveSceneView?.FrameSelected();
                Selection.objects = previousSelection;
                return;
            }

            if (track.ControlPoints.Count == 0) return;

            Bounds bounds = new(track.transform.TransformPoint(track.ControlPoints[0].position),
                Vector3.one);
            for (int i = 1; i < track.ControlPoints.Count; i++)
                bounds.Encapsulate(track.transform.TransformPoint(track.ControlPoints[i].position));
            bounds.Expand(12f);
            SceneView.lastActiveSceneView?.Frame(bounds, false);
        }

        private static void EnsureVisualHandles(RaceTrackAuthoring track)
        {
            Transform root = track.transform.Find("__TrackControlPoints");
            RaceTrackControlPointHandle[] existing = root != null
                ? root.GetComponentsInChildren<RaceTrackControlPointHandle>(true)
                : System.Array.Empty<RaceTrackControlPointHandle>();

            if (existing.Length != track.ControlPoints.Count)
            {
                RebuildVisualHandles(track);
                return;
            }

            for (int i = 0; i < existing.Length; i++)
            {
                existing[i].Configure(track, i);
                if (existing[i].transform.parent != root)
                    existing[i].transform.SetParent(root, false);
            }
            EnsurePreviewLine(track, root);
            track.RefreshPreviewLine();
        }

        private static void RebuildVisualHandles(RaceTrackAuthoring track)
        {
            Transform oldRoot = track.transform.Find("__TrackControlPoints");
            if (oldRoot != null)
                Undo.DestroyObjectImmediate(oldRoot.gameObject);

            GameObject rootObject = new("__TrackControlPoints") { tag = "EditorOnly" };
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Track Control Points");
            rootObject.transform.SetParent(track.transform, false);

            for (int i = 0; i < track.ControlPoints.Count; i++)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.tag = "EditorOnly";
                sphere.transform.SetParent(rootObject.transform, false);
                sphere.transform.localScale = Vector3.one * 3f;
                RaceTrackControlPointHandle handle =
                    sphere.AddComponent<RaceTrackControlPointHandle>();
                handle.Configure(track, i);
                handle.SnapToAuthoringData();
            }

            EnsurePreviewLine(track, rootObject.transform);
            track.RefreshPreviewLine();
            EditorUtility.SetDirty(track);
            MarkSceneDirty(track);
            SceneView.RepaintAll();
        }

        private static void EnsurePreviewLine(RaceTrackAuthoring track, Transform root)
        {
            Transform lineTransform = root.Find("Track Preview Line");
            GameObject lineObject;
            if (lineTransform == null)
            {
                lineObject = new GameObject("Track Preview Line") { tag = "EditorOnly" };
                lineObject.transform.SetParent(root, false);
                Undo.RegisterCreatedObjectUndo(lineObject, "Create Track Preview Line");
            }
            else
            {
                lineObject = lineTransform.gameObject;
            }

            LineRenderer line = lineObject.GetComponent<LineRenderer>();
            if (line == null) line = lineObject.AddComponent<LineRenderer>();

            line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Line.mat");
            line.startColor = PointColor;
            line.endColor = PointColor;
            line.startWidth = 0.8f;
            line.endWidth = 0.8f;
            line.numCornerVertices = 4;
            line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            line.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        private static void SetupProjectIntegration(RaceTrackAuthoring track, bool createMissing)
        {
            track.AutoFindSceneSystems();
            SerializedObject so = new(track);

            AssignAssetIfEmpty<GameObject>(so, "waypointPrefab", WaypointPrefab);
            AssignAssetIfEmpty<GameObject>(so, "checkpointPrefab", CheckpointPrefab);
            AssignAssetIfEmpty<GameObject>(so, "finishCheckpointPrefab", FinishPrefab);
            AssignAssetIfEmpty<Material>(so, "roadMaterial", RoadMaterial);

            if (createMissing)
            {
                AssignSceneSystemIfMissing<AIWaypoints>(so, "aiWaypoints", WaypointRootPrefab);
                AssignSceneSystemIfMissing<Checkpoints>(so, "checkpoints", CheckpointRootPrefab);
                AssignSceneSystemIfMissing<PlayersSpawner>(so, "playersSpawner", SpawningRootPrefab);
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(track);
            MarkSceneDirty(track);
        }

        private static void AssignAssetIfEmpty<T>(SerializedObject so, string propertyName,
            string path) where T : Object
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property.objectReferenceValue == null)
                property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void AssignSceneSystemIfMissing<T>(SerializedObject so, string propertyName,
            string prefabPath) where T : Component
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property.objectReferenceValue != null) return;

            T existing = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (existing != null)
            {
                property.objectReferenceValue = existing;
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Track authoring could not find required prefab: {prefabPath}");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, $"Create {typeof(T).Name}");
            property.objectReferenceValue = instance.GetComponent<T>();
        }

        private static void BakeAndPersist(RaceTrackAuthoring track)
        {
            SetupProjectIntegration(track, false);
            SyncVisualHandlesToAuthoring(track);
            Undo.RegisterFullObjectHierarchyUndo(track.gameObject, "Bake Race Track");
            track.Bake();
            PersistGeneratedMesh(track);
            MarkSceneDirty(track);
        }

        private static void SyncVisualHandlesToAuthoring(RaceTrackAuthoring track)
        {
            RaceTrackControlPointHandle[] handles =
                track.GetComponentsInChildren<RaceTrackControlPointHandle>(true);
            if (handles.Length == 0) return;

            Undo.RecordObject(track, "Sync Track Control Points");
            foreach (RaceTrackControlPointHandle handle in handles)
            {
                int index = handle.PointIndex;
                if (index < 0 || index >= track.ControlPoints.Count) continue;

                TrackControlPoint point = track.ControlPoints[index];
                point.position = handle.transform.localPosition;
                track.SetControlPoint(index, point);
                handle.transform.hasChanged = false;
            }
            EditorUtility.SetDirty(track);
        }

        private static void SnapVisualHandlesToAuthoring(RaceTrackAuthoring track)
        {
            RaceTrackControlPointHandle[] handles =
                track.GetComponentsInChildren<RaceTrackControlPointHandle>(true);
            foreach (RaceTrackControlPointHandle handle in handles)
                handle.SnapToAuthoringData();
            track.RefreshPreviewLine();
            SceneView.RepaintAll();
        }

        private static void PersistGeneratedMesh(RaceTrackAuthoring track)
        {
            MeshFilter filter = track.transform.Find("__GeneratedTrack/Road")
                ?.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;

            const string folder = "Assets/GeneratedTracks";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "GeneratedTracks");

            string sceneName = string.IsNullOrWhiteSpace(track.gameObject.scene.name)
                ? "UnsavedScene"
                : track.gameObject.scene.name;
            string fileName = MakeSafeFileName($"{sceneName}_{track.name}_Road.asset");
            string path = $"{folder}/{fileName}";
            Mesh source = filter.sharedMesh;
            Mesh replacement = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (replacement == null)
            {
                replacement = Instantiate(source);
                replacement.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(replacement, path);
            }
            else
            {
                // Mesh vertex/index buffers are native data and CopySerialized does
                // not reliably replace them. Copy the buffers explicitly while
                // preserving the asset GUID referenced by scenes and source control.
                replacement.Clear();
                replacement.indexFormat = source.indexFormat;
                replacement.vertices = source.vertices;
                replacement.normals = source.normals;
                replacement.uv = source.uv;
                replacement.triangles = source.triangles;
                replacement.bounds = source.bounds;
                replacement.name = Path.GetFileNameWithoutExtension(path);
                EditorUtility.SetDirty(replacement);
            }
            filter.sharedMesh = replacement;

            MeshCollider collider = filter.GetComponent<MeshCollider>();
            if (collider != null)
            {
                collider.sharedMesh = null;
                collider.sharedMesh = replacement;
            }
            EditorUtility.SetDirty(filter);
            if (collider != null) EditorUtility.SetDirty(collider);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static string MakeSafeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value.Replace(' ', '_');
        }

        private static void MarkSceneDirty(RaceTrackAuthoring track)
        {
            if (track.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(track.gameObject.scene);
        }
    }
}
