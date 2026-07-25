using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SpinMotion;

namespace GMTK.Mvc.Editor
{
    /// <summary>
    /// One-shot editor setup for the player-only MVC hybrid in the active kit race scene:
    /// drops in the MVC _GameController (VehicleManager) and points the kit PlayersSpawner at
    /// an MVC player car + the kit CheckpointTracker prefab. Invoke from the Unity CLI:
    ///   return GMTK.Mvc.Editor.MvcHybridSetup.SetupActiveScene();
    /// </summary>
    public static class MvcHybridSetup
    {
        public const string MvcCarPath = "Assets/BxB Studio/MVC Getting Started/Prefabs/Vehicles/Cars/2005 BMW M3 GTR E46.prefab";
        public const string GameControllerPath = "Assets/BxB Studio/MVC/Prefabs/_GameController.prefab";
        public const string CheckpointTrackerGuid = "6f08b7ced6b1347eaafaaf2b4a20ee0c";

        public static string SetupActiveScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            string log = "scene=" + scene.name + " ";

            var gcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameControllerPath);
            if (gcPrefab == null) return log + "ERR: no _GameController at " + GameControllerPath;
            if (GameObject.Find("_GameController") == null)
            {
                var gc = (GameObject)PrefabUtility.InstantiatePrefab(gcPrefab);
                gc.name = "_GameController";
                log += "addedGameController ";
            }
            else log += "gcExists ";

            var spawner = Object.FindAnyObjectByType<PlayersSpawner>();
            if (spawner == null) return log + "ERR: no PlayersSpawner";

            var mvcCar = AssetDatabase.LoadAssetAtPath<GameObject>(MvcCarPath);
            if (mvcCar == null) return log + "ERR: no MVC car at " + MvcCarPath;
            var trackerPath = AssetDatabase.GUIDToAssetPath(CheckpointTrackerGuid);
            var trackerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(trackerPath);
            if (trackerPrefab == null) return log + "ERR: no CheckpointTracker prefab (guid " + CheckpointTrackerGuid + ")";

            spawner.useMvcPlayer = true;
            spawner.mvcPlayerPrefab = mvcCar;
            spawner.checkpointTrackerPrefab = trackerPrefab;
            EditorUtility.SetDirty(spawner);
            log += "spawnerSet ";

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return log + "SAVED";
        }
    }
}
