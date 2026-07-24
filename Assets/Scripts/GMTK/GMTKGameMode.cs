using UnityEngine;

namespace GMTK
{
    /// <summary>
    /// Marker component that hosts the GMTK game-mode managers. Each feature manager
    /// attaches itself to this single GameObject at runtime (see their AutoAttach
    /// methods), so features stay independent and need no manual scene wiring.
    /// </summary>
    public class GMTKGameMode : MonoBehaviour
    {
        private const string HostName = "GMTK_GameMode";

        /// <summary>Find the shared game-mode host, creating it if this is a fresh scene.</summary>
        public static GameObject GetOrCreate()
        {
            var existing = Object.FindAnyObjectByType<GMTKGameMode>();
            if (existing != null) return existing.gameObject;

            // fresh scene → stale cached kit references are no longer valid
            Race.ClearCache();

            var go = new GameObject(HostName);
            go.AddComponent<GMTKGameMode>();
            return go;
        }
    }
}
