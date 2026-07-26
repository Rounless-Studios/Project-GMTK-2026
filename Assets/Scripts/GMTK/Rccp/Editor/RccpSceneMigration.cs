#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GMTK.Kit;

namespace GMTK.Rccp.Editor
{
    /// <summary>
    /// One-time scene migration: replaces the customized RSK spawner with the project-owned
    /// RCCP spawner and removes the obsolete MVC path object.
    /// </summary>
    public static class RccpSceneMigration
    {
        private const string ScenePath =
            "Assets/Racing Starter Kit/RSK Assets/Scenes/SampleScene.unity";

        // The migration already ran and the race now lives in Assets/Scenes/GMTK_Race.unity, so
        // the automatic hook is gone: it re-opened the retired SampleScene on every script reload
        // and logged its missing MVC prefab. Run it from the menu if a legacy scene turns up.

        [MenuItem("GMTK/Vehicles/Migrate Scene To RCCP")]
        public static void MigrateMenu()
        {
            Debug.Log(Migrate());
        }

        public static string Migrate()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;

            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            PlayersSpawner legacy = FindInScene<PlayersSpawner>(scene);

            if (legacy == null)
            {
                if (!wasLoaded)
                    EditorSceneManager.CloseScene(scene, true);

                return "RCCP migration: scene already migrated or legacy spawner not found.";
            }

            GmtkRccpPlayersSpawner replacement =
                legacy.GetComponent<GmtkRccpPlayersSpawner>();

            if (replacement == null)
                replacement = legacy.gameObject.AddComponent<GmtkRccpPlayersSpawner>();

            replacement.CopyFromLegacy(legacy);
            Object.DestroyImmediate(legacy);

            DestroySceneObject(scene, "GMTK_AIPath");
            DestroySceneObject(scene, "_GameController");
            RemoveMvcScriptingDefine();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);

            return "RCCP migration: SampleScene migrated successfully.";
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);

                if (component != null)
                    return component;
            }

            return null;
        }

        private static void DestroySceneObject(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

                foreach (Transform transform in transforms)
                {
                    if (transform.name != objectName)
                        continue;

                    Object.DestroyImmediate(transform.gameObject);
                    return;
                }
            }
        }

        private static void RemoveMvcScriptingDefine()
        {
            NamedBuildTarget target = NamedBuildTarget.Standalone;
            string defines = PlayerSettings.GetScriptingDefineSymbols(target);
            string[] entries = defines.Split(';');
            System.Collections.Generic.List<string> kept = new();

            foreach (string entry in entries)
            {
                if (!string.IsNullOrWhiteSpace(entry) && entry != "MVC_COMMUNITY")
                    kept.Add(entry);
            }

            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", kept));
        }
    }
}
#endif
