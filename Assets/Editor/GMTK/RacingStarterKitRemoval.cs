using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GMTK.EditorTools
{
    /// <summary>
    /// Steps that prepare <c>Assets/Racing Starter Kit/</c> for deletion. Everything here goes
    /// through the AssetDatabase so GUIDs survive and no scene or prefab reference breaks.
    /// Kit components are matched by type name rather than by type reference, so this tool keeps
    /// compiling once the kit is gone.
    /// </summary>
    public static class RacingStarterKitRemoval
    {
        private const string KitRoot = "Assets/Racing Starter Kit";
        private const string TrackAuthoringPrefab = "Assets/Prefab/Race Track Authoring.prefab";

        /// <summary>Source -> destination, in the order they must run.</summary>
        private static readonly (string From, string To)[] Moves =
        {
            // The project's only TMP Settings lives in the kit and TMP resolves it through
            // Resources.Load, so deleting the kit without this breaks every text in the game.
            // LiberationSans SDF, EmojiOne and the style sheet travel with it.
            (KitRoot + "/TextMesh Pro", "Assets/TextMesh Pro"),

            // Autobus-Bold SDF is used by Main UI.prefab and RaceUI.prefab; the licence goes too
            (KitRoot + "/RSK Assets/Fonts/Autobus-Bold SDF.asset", "Assets/Fonts/Autobus-Bold SDF.asset"),
            (KitRoot + "/RSK Assets/Fonts/Autobus-Bold.ttf", "Assets/Fonts/Autobus-Bold.ttf"),
            (KitRoot + "/RSK Assets/Fonts/License.txt", "Assets/Fonts/License.txt"),
        };

        [MenuItem("GMTK/Racing Starter Kit/1. Extract Assets We Keep")]
        public static void ExtractAssetsWeKeep()
        {
            var problems = new List<string>();

            if (!AssetDatabase.IsValidFolder("Assets/Fonts"))
                AssetDatabase.CreateFolder("Assets", "Fonts");

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach ((string from, string to) in Moves)
                {
                    if (AssetDatabase.LoadAssetAtPath<Object>(to) != null || AssetDatabase.IsValidFolder(to))
                    {
                        Debug.Log($"[KitRemoval] already moved: {to}");
                        continue;
                    }

                    if (AssetDatabase.LoadAssetAtPath<Object>(from) == null && !AssetDatabase.IsValidFolder(from))
                    {
                        problems.Add($"source missing: {from}");
                        continue;
                    }

                    string error = AssetDatabase.MoveAsset(from, to);
                    if (string.IsNullOrEmpty(error))
                        Debug.Log($"[KitRemoval] moved: {from} -> {to}");
                    else
                        problems.Add($"{from} -> {to}: {error}");
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Report("ExtractAssetsWeKeep", problems);
        }

        /// <summary>
        /// Removes the kit's <c>RaceFinish</c> from our track prefab. It ends the race on lap
        /// completion behind the central race state's back, and its <c>finishTrigger</c> field is
        /// never assigned, so <c>OnRestartRace()</c> throws a NullReferenceException. Victory is
        /// adjudicated by <see cref="GMTK.FinalGate"/> instead.
        /// </summary>
        [MenuItem("GMTK/Racing Starter Kit/2. Strip RaceFinish From Track Prefab")]
        public static void StripRaceFinish()
        {
            var problems = new List<string>();
            StripComponent(TrackAuthoringPrefab, "RaceFinish", problems);
            Report("StripRaceFinish", problems);
        }

        /// <summary>
        /// Removes the object <see cref="StripRaceFinish"/> emptied out. It only ever carried the
        /// kit script plus a disabled MeshRenderer marker, so nothing is lost with it.
        /// </summary>
        [MenuItem("GMTK/Racing Starter Kit/3. Remove Emptied Race Finish Trigger")]
        public static void RemoveRaceFinishTrigger()
        {
            var problems = new List<string>();
            StripGameObject(TrackAuthoringPrefab, "Race Finish Trigger", problems);
            Report("RemoveRaceFinishTrigger", problems);
        }

        /// <summary>Runs every prepared step in order; used by the batch-mode entry point.</summary>
        public static void RunAll()
        {
            ExtractAssetsWeKeep();
            StripRaceFinish();
            RemoveRaceFinishTrigger();
        }

        private static void StripGameObject(string prefabPath, string objectName, List<string> problems)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                problems.Add($"could not open {prefabPath}");
                return;
            }

            try
            {
                var doomed = new List<GameObject>();
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t != null && t.name == objectName)
                        doomed.Add(t.gameObject);
                }

                if (doomed.Count == 0)
                {
                    Debug.Log($"[KitRemoval] no '{objectName}' left in {prefabPath}");
                    return;
                }

                foreach (GameObject go in doomed)
                {
                    // refuse to take anything unexpected with it
                    if (go.transform.childCount > 0)
                    {
                        problems.Add($"'{objectName}' has {go.transform.childCount} children, left alone");
                        continue;
                    }

                    Debug.Log($"[KitRemoval] removing GameObject '{objectName}'");
                    Object.DestroyImmediate(go, true);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[KitRemoval] saved {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void StripComponent(string prefabPath, string typeName, List<string> problems)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                problems.Add($"could not open {prefabPath}");
                return;
            }

            try
            {
                var doomed = new List<Component>();
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component != null && component.GetType().Name == typeName)
                        doomed.Add(component);
                }

                if (doomed.Count == 0)
                {
                    Debug.Log($"[KitRemoval] no {typeName} left in {prefabPath}");
                    return;
                }

                foreach (Component component in doomed)
                {
                    Debug.Log($"[KitRemoval] removing {typeName} from '{component.gameObject.name}'");
                    Object.DestroyImmediate(component, true);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[KitRemoval] removed {doomed.Count} x {typeName}, saved {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Report(string step, List<string> problems)
        {
            if (problems.Count == 0)
                Debug.Log($"[KitRemoval] {step}: done, no problems.");
            else
                Debug.LogError($"[KitRemoval] {step} problems:\n  " + string.Join("\n  ", problems));
        }
    }
}
