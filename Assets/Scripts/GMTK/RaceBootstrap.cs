using UnityEngine;

namespace GMTK
{
    /// <summary>
    /// Ensures the GMTK game-mode host exists after each scene load. Feature managers
    /// attach themselves to it via their own AutoAttach hooks; this just guarantees the
    /// host is present and clears stale cached references on scene reload.
    /// </summary>
    public static class RaceBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            GMTKGameMode.GetOrCreate();
        }
    }
}
