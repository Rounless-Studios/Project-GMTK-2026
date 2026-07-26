using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GMTK
{
    public enum DifficultyPreset
    {
        Story,
        Normal,
        Brutal
    }

    /// <summary>
    /// One persisted difficulty authority. Gameplay systems consume the multipliers instead of
    /// embedding their own interpretation of Easy/Normal/Hard.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DifficultyManager : MonoBehaviour
    {
        private const string PreferenceKey = "GMTK.Difficulty";
        public static DifficultyManager Instance { get; private set; }
        public DifficultyPreset Current { get; private set; }

        public float AiPaceMultiplier => Current switch
        {
            DifficultyPreset.Story => 0.88f,
            DifficultyPreset.Brutal => 1.08f,
            _ => 1f
        };

        public float AiAggressionMultiplier => Current switch
        {
            DifficultyPreset.Story => 0.65f,
            DifficultyPreset.Brutal => 1.25f,
            _ => 1f
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            GameObject host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<DifficultyManager>() == null)
                host.AddComponent<DifficultyManager>();
        }

        private void Awake()
        {
            Instance = this;
            Current = (DifficultyPreset)Mathf.Clamp(
                PlayerPrefs.GetInt(PreferenceKey, (int)DifficultyPreset.Normal),
                0,
                System.Enum.GetValues(typeof(DifficultyPreset)).Length - 1);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Set(DifficultyPreset preset)
        {
            Current = preset;
            PlayerPrefs.SetInt(PreferenceKey, (int)preset);
            PlayerPrefs.Save();
        }

        public void Cycle()
        {
            int count = System.Enum.GetValues(typeof(DifficultyPreset)).Length;
            Set((DifficultyPreset)(((int)Current + 1) % count));
            Debug.Log($"Difficulty changed to {Current}. It applies immediately to AI pace and aggression.");
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            // Development-friendly until the settings menu is open: the choice is persisted.
            if ((Debug.isDebugBuild || Application.isEditor)
                && Keyboard.current != null
                && Keyboard.current.f6Key.wasPressedThisFrame)
                Cycle();
#endif
        }
    }
}
