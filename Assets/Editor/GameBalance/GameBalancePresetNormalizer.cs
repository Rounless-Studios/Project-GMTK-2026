using System;
using System.Collections.Generic;
using Gmtk2026.GameBalance;
using UnityEditor;
using UnityEngine;

namespace GMTK.EditorTools
{
    /// <summary>
    /// Puts the shipped balance presets back on the values documented in
    /// docs/GAMEPLAY_IMPLEMENTATION_CHECKLIST.md ("설정 카탈로그와 GDD 기본값") after temporary
    /// playtest overrides, and re-serialises them so renamed/added settings fields are written.
    /// This is the sanctioned way to restore a preset — the .asset YAML is never hand-edited.
    /// GameBalanceSettingsTests pins the same values, so drift fails a test rather than a race.
    /// </summary>
    public static class GameBalancePresetNormalizer
    {
        private const string GameJamDefaultPath =
            "Assets/GameBalance/Resources/GameBalance/GameJamDefault.asset";
        private const string FastTestPath =
            "Assets/GameBalance/Resources/GameBalance/FastTest.asset";

        [MenuItem("GMTK/Game Balance/Normalize Presets To GDD")]
        public static void Normalize()
        {
            var problems = new List<string>();

            Apply(GameJamDefaultPath, problems, settings =>
            {
                // GDD: eliminations are a steady 30s beat (was 10s for quick manual testing)
                settings.elimination.intervalSeconds = 30f;
                RestoreDurabilityOverride(settings);
                NormalizeFinalGate(settings);
            });

            Apply(FastTestPath, problems, settings =>
            {
                // the fast preset keeps its deliberately short timers
                RestoreDurabilityOverride(settings);
                NormalizeFinalGate(settings);
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (problems.Count == 0)
                Debug.Log("[PresetNormalizer] OK: both presets normalised and validated.");
            else
                Debug.LogError("[PresetNormalizer] FAILED:\n - " + string.Join("\n - ", problems));
        }

        /// <summary>
        /// Durability stays on the temporary wreck-deferring override; the final balance value
        /// is the team's call, so this tool must not decide it. Remove this once it is set.
        /// </summary>
        private static void RestoreDurabilityOverride(GameBalanceSettings settings)
        {
            settings.damage.maximumDurability = 1000000f;
        }

        /// <summary>
        /// Gate values from the checklist catalog. The old `gateExecutionDelaySeconds: 0.5` key
        /// survives re-serialisation, so it has to be written explicitly.
        /// </summary>
        private static void NormalizeFinalGate(GameBalanceSettings settings)
        {
            settings.presentation.gateCloseDurationSeconds = 0.75f;
            settings.presentation.gateExecutionDelaySeconds = 0.25f;
        }

        private static void Apply(string path, List<string> problems, Action<GameBalanceSettings> edit)
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameBalanceSettings>(path);
            if (settings == null)
            {
                problems.Add($"{path}: asset not found");
                return;
            }

            edit(settings);
            EditorUtility.SetDirty(settings);

            var errors = settings.Validate();
            if (errors.Count > 0)
                problems.Add($"{path}: {string.Join("; ", errors)}");
            else
                Debug.Log($"[PresetNormalizer] {settings.name}: elimination.intervalSeconds=" +
                          $"{settings.elimination.intervalSeconds}, damage.maximumDurability=" +
                          $"{settings.damage.maximumDurability} — validation clean.");
        }
    }
}
