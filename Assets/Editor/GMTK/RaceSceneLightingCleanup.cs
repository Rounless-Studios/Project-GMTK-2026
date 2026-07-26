using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GMTK.EditorTools
{
    /// <summary>
    /// Cuts the race scene's last two ties to the old starter-kit demo scene.
    /// <para>
    /// The kit's <c>Map</c> prefab is still instantiated even though every piece of its geometry is
    /// stripped or deactivated — the only live part is its Directional Light, which happens to be
    /// the scene's only light. So the light is rebuilt as a plain scene object first, then the Map
    /// instance goes.
    /// </para>
    /// <para>
    /// The scene also still points at the kit demo's baked <c>LightingData.asset</c> (33 MB) even
    /// though none of the geometry it was baked against exists any more. Clearing it drops the file
    /// and moves the scene to fully realtime lighting.
    /// </para>
    /// </summary>
    public static class RaceSceneLightingCleanup
    {
        private const string ScenePath = "Assets/Scenes/GMTK_Race.unity";
        private const string MapObjectName = "Map";
        private const string LightObjectName = "Directional Light";

        // copied from the light inside Map.prefab so the scene keeps the look it had
        private static readonly Color SunColor = new(1f, 0.95686275f, 0.8392157f);
        private static readonly Vector3 SunEuler = new(39.1f, 90f, 0f);
        private const float SunIntensity = 1.5f;

        [MenuItem("GMTK/Racing Starter Kit/6. Rebuild Scene Light And Drop Map")]
        public static void Run()
        {
            var problems = new List<string>();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[SceneCleanup] could not open {ScenePath}");
                return;
            }

            GameObject map = null;
            bool hasOwnLight = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == MapObjectName) map = root;
                if (root.name == LightObjectName) hasOwnLight = true;
            }

            if (!hasOwnLight)
            {
                var sun = new GameObject(LightObjectName);
                Light light = sun.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = SunColor;
                light.intensity = SunIntensity;
                light.shadows = LightShadows.Soft;
                light.shadowBias = 0.05f;
                light.shadowNormalBias = 0.4f;
                light.shadowNearPlane = 0.2f;
                sun.transform.SetPositionAndRotation(
                    new Vector3(0f, 3f, 0f),
                    Quaternion.Euler(SunEuler));
                Debug.Log("[SceneCleanup] created scene Directional Light");
            }
            else
            {
                Debug.Log("[SceneCleanup] scene already owns a Directional Light");
            }

            if (map != null)
            {
                Object.DestroyImmediate(map);
                Debug.Log("[SceneCleanup] removed the kit Map instance");
            }
            else
            {
                Debug.Log("[SceneCleanup] no Map instance left");
            }

            // The baked data was produced against geometry this scene no longer contains.
            // Neither Lightmapping.lightingDataAsset = null nor ClearLightingDataAsset() reaches
            // the serialized field under -nographics, so the scene's LightmapSettings object is
            // edited directly. Hand-editing the .unity YAML is not an option here.
            Lightmapping.ClearLightingDataAsset();
            if (!DetachLightingDataAsset()) problems.Add("could not detach the LightingDataAsset");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                problems.Add($"could not save {ScenePath}");

            int lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Debug.Log($"[SceneCleanup] scene now has {lights} Light component(s)");

            if (problems.Count == 0) Debug.Log("[SceneCleanup] done, no problems.");
            else Debug.LogError("[SceneCleanup] problems:\n  " + string.Join("\n  ", problems));
        }

        /// <summary>
        /// Nulls <c>m_LightingDataAsset</c> on the scene's LightmapSettings singleton. Returns true
        /// when the field is confirmed empty afterwards (or was never set).
        /// </summary>
        private static bool DetachLightingDataAsset()
        {
            Object[] all = Resources.FindObjectsOfTypeAll(typeof(LightmapSettings));
            if (all.Length == 0)
            {
                Debug.LogWarning("[SceneCleanup] no LightmapSettings object found");
                return false;
            }

            bool cleared = true;
            foreach (Object settings in all)
            {
                var so = new SerializedObject(settings);
                SerializedProperty prop = so.FindProperty("m_LightingDataAsset");
                if (prop == null)
                {
                    Debug.LogWarning("[SceneCleanup] LightmapSettings has no m_LightingDataAsset");
                    cleared = false;
                    continue;
                }

                // objectReferenceValue reads null whenever the target cannot be LOADED, even though
                // the guid/fileID is still serialized — and a LightingDataAsset baked for different
                // geometry is exactly that case. The instance id is what actually tells us whether
                // a reference is stored, so both are cleared unconditionally.
                int storedId = prop.objectReferenceInstanceIDValue;
                prop.objectReferenceValue = null;
                prop.objectReferenceInstanceIDValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();

                so.Update();
                bool ok = so.FindProperty("m_LightingDataAsset").objectReferenceInstanceIDValue == 0;
                Debug.Log($"[SceneCleanup] detached LightingDataAsset (was id {storedId}): {ok}");
                cleared &= ok;
            }

            return cleared;
        }
    }
}
