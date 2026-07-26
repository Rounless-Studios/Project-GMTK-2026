using UnityEngine;

namespace GMTK.Rccp
{
    /// <summary>
    /// Prevents RCCP's terrain-friction discovery coroutine from dereferencing Terrain components
    /// whose native TerrainData failed to deserialize. Broken tiles are already non-renderable and
    /// non-collidable, so excluding only those GameObjects preserves every valid terrain.
    /// </summary>
    public static class RccpTerrainDataGuard
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ExcludeInvalidTerrains()
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            int excluded = 0;

            foreach (Terrain terrain in terrains)
            {
                if (terrain == null || terrain.terrainData != null)
                    continue;

                terrain.gameObject.SetActive(false);
                excluded++;
            }

            if (excluded > 0)
            {
                Debug.LogWarning(
                    $"RCCP terrain guard excluded {excluded} Terrain object(s) with missing " +
                    "TerrainData before vehicle surface discovery. Reimport Assets/Terrain if " +
                    "those background tiles should be visible.");
            }
        }
    }
}
