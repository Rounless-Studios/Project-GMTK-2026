using UnityEngine;

namespace Gmtk2026.GameBalance
{
    /// <summary>
    /// Supplies the active <see cref="GameBalanceSettings"/> to every system. Loads a preset
    /// from Resources (so it works in builds) and, at race start, freezes an immutable
    /// snapshot (a runtime clone) so inspector edits mid-race can't change live behaviour.
    /// Never invents silent defaults: a missing/invalid preset logs a specific error.
    /// </summary>
    public static class GameBalance
    {
        public const string DefaultPresetName = "GameJamDefault";
        public const string ResourceFolder = "GameBalance/";

        private static GameBalanceSettings _active;
        private static GameBalanceSettings _snapshot;

        /// <summary>The selected preset asset (shared, editable). Auto-loads the default if unset.</summary>
        public static GameBalanceSettings Active
        {
            get
            {
                if (_active == null) Load(DefaultPresetName);
                return _active;
            }
        }

        /// <summary>The immutable snapshot used during a race. Falls back to Active before a race starts.</summary>
        public static GameBalanceSettings Current => _snapshot != null ? _snapshot : Active;

        /// <summary>Select a preset by name from Resources. Returns null and logs on failure.</summary>
        public static GameBalanceSettings Load(string presetName)
        {
            var settings = Resources.Load<GameBalanceSettings>(ResourceFolder + presetName);
            if (settings == null)
            {
                Debug.LogError($"[GameBalance] Preset '{presetName}' not found at Resources/{ResourceFolder}{presetName}. " +
                               "No silent defaults are created — add the preset asset.");
                return null;
            }

            var errors = settings.Validate();
            if (errors.Count > 0)
                Debug.LogError($"[GameBalance] Preset '{presetName}' failed validation:\n - {string.Join("\n - ", errors)}");

            _active = settings;
            return settings;
        }

        /// <summary>Explicitly set the active preset (e.g. from a menu selection).</summary>
        public static void SetActive(GameBalanceSettings settings)
        {
            _active = settings;
        }

        /// <summary>Freeze an immutable snapshot of the active preset for the upcoming race.</summary>
        public static GameBalanceSettings BeginRace()
        {
            var source = Active;
            if (source == null)
            {
                Debug.LogError("[GameBalance] Cannot begin race: no active preset.");
                return null;
            }
            _snapshot = Object.Instantiate(source);
            _snapshot.name = source.name + " (Race Snapshot)";
            return _snapshot;
        }

        /// <summary>Drop the current race snapshot (systems fall back to the live preset).</summary>
        public static void EndRace()
        {
            if (_snapshot != null)
            {
                // Destroy is play-mode only; edit-mode/test callers need DestroyImmediate.
                if (Application.isPlaying) Object.Destroy(_snapshot);
                else Object.DestroyImmediate(_snapshot);
                _snapshot = null;
            }
        }

        /// <summary>Test/reset hook.</summary>
        public static void ResetForTests()
        {
            _active = null;
            _snapshot = null;
        }
    }
}
