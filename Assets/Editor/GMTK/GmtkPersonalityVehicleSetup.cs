using System.Collections.Generic;
using Gmtk2026.GameBalance;
using GMTK.Rccp;
using UnityEditor;
using UnityEngine;

namespace GMTK.EditorTools
{
    /// <summary>
    /// Gives each AI personality its own car body, so the paint tells you who is alongside you.
    /// <para>
    /// The five RCCP prototype cars are colour variants of the same vehicle (identical performance),
    /// and they are third-party assets: this tool only references them. The spawner that holds the
    /// mapping lives inside Race Track Authoring.prefab, so the list is written into that prefab
    /// asset rather than into the shared scene.
    /// </para>
    /// </summary>
    public static class GmtkPersonalityVehicleSetup
    {
        private const string TrackPrefabPath = "Assets/Prefab/Race Track Authoring.prefab";
        private const string VehicleFolder =
            "Assets/Realistic Car Controller Pro/Prefabs/Prototype/Model_Skyline by BUMSTRUM(3DMaesen) (Prototype)";

        /// <summary>
        /// One row per AI car, matching the confirmed grid (폭주광 2 · 난폭자 1 · 봉쇄자 1 · 생존자 1).
        /// The five prototype cars are colour variants of the same vehicle, so the second 폭주광 takes
        /// the base body: there is no sixth variant and the player also drives the base one.
        /// </summary>
        private static readonly (AIPersonalityType personality, string suffix)[] Roster =
        {
            (AIPersonalityType.Reckless, " R"),
            (AIPersonalityType.Rammer, " K"),
            (AIPersonalityType.Blocker, " B"),
            (AIPersonalityType.CleanRacer, " W"),
            (AIPersonalityType.Reckless, ""),
        };

        [MenuItem("GMTK/Vehicles/Assign Personality Cars")]
        public static void Assign()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(TrackPrefabPath);
            if (contents == null)
            {
                Debug.LogError($"[PersonalityCars] {TrackPrefabPath} not found.");
                return;
            }

            try
            {
                var spawner = contents.GetComponentInChildren<GmtkRccpPlayersSpawner>(true);
                if (spawner == null)
                {
                    Debug.LogError($"[PersonalityCars] no GmtkRccpPlayersSpawner inside {TrackPrefabPath}.");
                    return;
                }

                var so = new SerializedObject(spawner);
                SerializedProperty list = so.FindProperty("aiCars");
                if (list == null)
                {
                    Debug.LogError("[PersonalityCars] the spawner has no aiCars field.");
                    return;
                }

                var report = new List<string>();
                list.arraySize = Roster.Length;

                for (int i = 0; i < Roster.Length; i++)
                {
                    (AIPersonalityType personality, string suffix) = Roster[i];
                    string path = $"{VehicleFolder}{suffix}.prefab";
                    var car = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var controller = car != null ? car.GetComponent<RCCP_CarController>() : null;

                    if (controller == null)
                    {
                        Debug.LogError($"[PersonalityCars] {personality}: no RCCP car at {path}.");
                        continue;
                    }

                    SerializedProperty entry = list.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("personality").enumValueIndex = (int)personality;
                    entry.FindPropertyRelative("prefab").objectReferenceValue = controller;
                    report.Add($"AI {i + 1}: {personality} -> {car.name}");
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, TrackPrefabPath);
                AssetDatabase.SaveAssets();

                Debug.Log($"[PersonalityCars] {TrackPrefabPath}: {string.Join(", ", report)}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
