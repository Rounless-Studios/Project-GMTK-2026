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
                NormalizeAiRacecraft(settings);
            });

            Apply(FastTestPath, problems, settings =>
            {
                // the fast preset keeps its deliberately short timers
                RestoreDurabilityOverride(settings);
                NormalizeFinalGate(settings);
                NormalizeAiRacecraft(settings);
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (problems.Count == 0)
                Debug.Log("[PresetNormalizer] OK: both presets normalised and validated.");
            else
                Debug.LogError("[PresetNormalizer] FAILED:\n - " + string.Join("\n - ", problems));
        }

        /// <summary>
        /// The racecraft axes (braking point, overtaking, defending, the gap kept behind another car)
        /// were added after these presets were authored, so their personality rows carry the C# field
        /// defaults - identical for all four, which is exactly the "personalities do nothing" symptom.
        /// Writes the intended spread instead: reckless brakes latest and attacks, the blocker defends,
        /// the rammer keeps no gap at all, the clean racer leaves the most room.
        /// </summary>
        private static void NormalizeAiRacecraft(GameBalanceSettings settings)
        {
            SetRacecraft(settings, AIPersonalityType.Reckless, 1.2f, 1.4f, 0f, 2.5f);
            SetRacecraft(settings, AIPersonalityType.Rammer, 1.05f, 1f, 0.2f, 0f);
            SetRacecraft(settings, AIPersonalityType.Blocker, 0.95f, 0.3f, 1f, 5f);
            SetRacecraft(settings, AIPersonalityType.CleanRacer, 1f, 0.6f, 0.15f, 8f);
        }

        private static void SetRacecraft(
            GameBalanceSettings settings,
            AIPersonalityType personality,
            float brakingConfidence,
            float overtakeAggression,
            float blockStrength,
            float contactToleranceMetres)
        {
            AiPersonalityProfile profile = settings.ai.GetProfile(personality);
            if (profile == null) return;

            profile.brakingConfidence = brakingConfidence;
            profile.overtakeAggression = overtakeAggression;
            profile.blockStrength = blockStrength;
            profile.contactToleranceMetres = contactToleranceMetres;

            // every personality boosts on every straight it has a charge for; hoarding is a per-car
            // tuning decision, not the default
            profile.boostTendency = 1f;
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
