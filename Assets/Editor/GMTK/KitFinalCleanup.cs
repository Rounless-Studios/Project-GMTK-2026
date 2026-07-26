using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GMTK.EditorTools
{
    /// <summary>
    /// Last sweep after the kit was relocated into <c>Assets/Kit/</c>: drops the two spawner fields
    /// nothing reads any more, then deletes the assets that became unreachable once the scene let
    /// go of the kit Map.
    /// </summary>
    public static class KitFinalCleanup
    {
        private const string TrackAuthoringPrefab = "Assets/Prefab/Race Track Authoring.prefab";

        /// <summary>
        /// Fields on the legacy <c>PlayersSpawner</c> that <see cref="GMTK.Rccp.GmtkRccpPlayersSpawner"/>
        /// never copies. <c>playerPrefab</c> is deliberately absent: the RCCP spawner still pulls the
        /// CheckpointTracker template out of it, so clearing that one would kill checkpoint tracking.
        /// </summary>
        private static readonly string[] DeadSpawnerFields = { "aiCarPrefab", "aiWaypointTrackerPrefab" };

        /// <summary>Unreachable once the two fields above are empty and the scene dropped the Map.</summary>
        private static readonly string[] DeadAssets =
        {
            "Assets/Kit/Prefabs/Track Based/Map.prefab",
            "Assets/Kit/Prefabs/Player Cars/Player Car 1 (AI Variant).prefab",
            "Assets/Kit/Prefabs/AI Car Waypoint Tracker.prefab",
            "Assets/Kit/Prefabs/Pylon.prefab",
            "Assets/Kit/Materials/AI Car Waypoint Tracker.mat",
            "Assets/Kit/Materials/Grass.mat",
            "Assets/Kit/Materials/Green.mat",
            "Assets/Kit/Materials/Grey Dark.mat",
            "Assets/Kit/Materials/Grey Metallic.mat",
            "Assets/Kit/Materials/Grey Very Dark.mat",
            "Assets/Kit/Materials/Grey.mat",
            "Assets/Kit/Materials/Road.mat",
        };

        /// <summary>
        /// Bulk left over from the kit that nothing reaches: the baked lighting the race scene just
        /// let go of, and the TextMesh Pro sample content that rode along when TMP was pulled out of
        /// the kit folder (only TMP Settings and the fonts were ever needed).
        /// </summary>
        private static readonly string[] DeadBulk =
        {
            "Assets/Kit/Scenes",
            "Assets/TextMesh Pro/Examples & Extras",
        };

        [MenuItem("GMTK/Racing Starter Kit/8. Drop Unreferenced Bulk")]
        public static void DropUnreferencedBulk()
        {
            var problems = new List<string>();
            int deleted = 0;

            foreach (string path in DeadBulk)
            {
                bool present = AssetDatabase.LoadAssetAtPath<Object>(path) != null ||
                               AssetDatabase.IsValidFolder(path) ||
                               System.IO.File.Exists(path);
                if (!present)
                {
                    Debug.Log($"[KitCleanup] already gone: {path}");
                    continue;
                }

                if (AssetDatabase.DeleteAsset(path)) { deleted++; Debug.Log($"[KitCleanup] deleted {path}"); }
                else problems.Add($"could not delete {path}");
            }

            AssetDatabase.Refresh();
            Debug.Log($"[KitCleanup] dropped {deleted} item(s)");
            if (problems.Count == 0) Debug.Log("[KitCleanup] bulk drop done, no problems.");
            else Debug.LogError("[KitCleanup] problems:\n  " + string.Join("\n  ", problems));
        }

        [MenuItem("GMTK/Racing Starter Kit/7. Final Cleanup")]
        public static void Run()
        {
            var problems = new List<string>();

            ClearDeadSpawnerFields(problems);

            int deleted = 0;
            foreach (string path in DeadAssets)
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(path) == null)
                {
                    Debug.Log($"[KitCleanup] already gone: {path}");
                    continue;
                }

                if (AssetDatabase.DeleteAsset(path)) deleted++;
                else problems.Add($"could not delete {path}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[KitCleanup] deleted {deleted} asset(s)");
            if (problems.Count == 0) Debug.Log("[KitCleanup] done, no problems.");
            else Debug.LogError("[KitCleanup] problems:\n  " + string.Join("\n  ", problems));
        }

        private static void ClearDeadSpawnerFields(List<string> problems)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(TrackAuthoringPrefab);
            if (root == null)
            {
                problems.Add($"could not open {TrackAuthoringPrefab}");
                return;
            }

            try
            {
                bool touched = false;
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || component.GetType().Name != "PlayersSpawner") continue;

                    var so = new SerializedObject(component);
                    foreach (string field in DeadSpawnerFields)
                    {
                        SerializedProperty prop = so.FindProperty(field);
                        if (prop == null) { problems.Add($"no field {field} on PlayersSpawner"); continue; }
                        if (prop.objectReferenceValue == null) continue;

                        Debug.Log($"[KitCleanup] clearing PlayersSpawner.{field}");
                        prop.objectReferenceValue = null;
                        touched = true;
                    }

                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                if (touched)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, TrackAuthoringPrefab);
                    Debug.Log($"[KitCleanup] saved {TrackAuthoringPrefab}");
                }
                else
                {
                    Debug.Log("[KitCleanup] spawner fields were already empty");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
