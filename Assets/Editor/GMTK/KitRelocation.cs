using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GMTK.EditorTools
{
    /// <summary>
    /// Moves the part of Racing Starter Kit we still depend on into <c>Assets/Kit/</c> and deletes
    /// the rest of the folder. Moves go through <see cref="AssetDatabase.MoveAsset"/>, which keeps
    /// GUIDs, so no scene or prefab reference breaks and no C# has to be rewritten. The kept files
    /// come from <see cref="KitRelocationList"/> — a transitive closure over everything our scenes,
    /// prefabs and scripts reach.
    /// <para>
    /// Run <see cref="Preview"/> first: it reports what would move and what would be deleted
    /// without touching anything.
    /// </para>
    /// </summary>
    public static class KitRelocation
    {
        private const string KitRoot = "Assets/Racing Starter Kit";

        [MenuItem("GMTK/Racing Starter Kit/4. Relocate Kit (Preview)")]
        public static void Preview() => Run(dryRun: true);

        [MenuItem("GMTK/Racing Starter Kit/5. Relocate Kit (Apply)")]
        public static void Apply() => Run(dryRun: false);

        private static void Run(bool dryRun)
        {
            string tag = dryRun ? "[KitRelocate:dry]" : "[KitRelocate]";
            var problems = new List<string>();
            int moved = 0, alreadyThere = 0;

            if (!AssetDatabase.IsValidFolder(KitRoot))
            {
                Debug.Log($"{tag} {KitRoot} is already gone, nothing to do.");
                return;
            }

            // Destination folders are created on disk and imported in one refresh BEFORE the batch
            // below. AssetDatabase.CreateFolder cannot be used inside StartAssetEditing: the
            // database does not see what it just made, so every call would mint "Scripts 1",
            // "Scripts 2", ... instead of reusing one folder.
            if (!dryRun)
            {
                for (int i = 0; i < KitRelocationList.Moves.GetLength(0); i++)
                {
                    string folder = Path.GetDirectoryName(KitRelocationList.Moves[i, 1]);
                    if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                }
                AssetDatabase.Refresh();
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < KitRelocationList.Moves.GetLength(0); i++)
                {
                    string from = KitRelocationList.Moves[i, 0];
                    string to = KitRelocationList.Moves[i, 1];

                    if (Exists(to)) { alreadyThere++; continue; }
                    if (!Exists(from)) { problems.Add($"source missing: {from}"); continue; }

                    if (dryRun) { moved++; continue; }

                    string error = AssetDatabase.MoveAsset(from, to);
                    if (string.IsNullOrEmpty(error)) moved++;
                    else problems.Add($"{from} -> {to}: {error}");
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            if (dryRun)
            {
                ReportLeftovers(tag);
                Debug.Log($"{tag} would move {moved}, already in place {alreadyThere}, problems {problems.Count}");
                Finish(tag, problems);
                return;
            }

            // Anything still under the kit root was reachable from nothing we own.
            if (problems.Count == 0)
            {
                if (AssetDatabase.DeleteAsset(KitRoot))
                    Debug.Log($"{tag} deleted {KitRoot}");
                else
                    problems.Add($"could not delete {KitRoot}");
            }
            else
            {
                Debug.LogWarning($"{tag} moves reported problems, leaving {KitRoot} in place for inspection");
            }

            AssetDatabase.Refresh();
            Debug.Log($"{tag} moved {moved}, already in place {alreadyThere}");
            Finish(tag, problems);
        }

        /// <summary>Lists what the delete would take, so the preview is auditable.</summary>
        private static void ReportLeftovers(string tag)
        {
            var doomed = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { KitRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path)) continue;
                if (!IsKept(path)) doomed.Add(path);
            }

            doomed.Sort();
            Debug.Log($"{tag} would delete {doomed.Count} files, e.g.:\n  " +
                      string.Join("\n  ", doomed.GetRange(0, Mathf.Min(15, doomed.Count))));
        }

        private static bool IsKept(string path)
        {
            for (int i = 0; i < KitRelocationList.Moves.GetLength(0); i++)
                if (KitRelocationList.Moves[i, 0] == path) return true;
            return false;
        }

        /// <summary>
        /// Batch mode cannot load every asset type (a baked LightingData comes back null), so the
        /// file on disk is the authority and the load is only a fallback for folders.
        /// </summary>
        private static bool Exists(string path) =>
            File.Exists(path) || AssetDatabase.IsValidFolder(path) ||
            AssetDatabase.LoadAssetAtPath<Object>(path) != null;

        private static void Finish(string tag, List<string> problems)
        {
            if (problems.Count == 0) Debug.Log($"{tag} done, no problems.");
            else Debug.LogError($"{tag} problems:\n  " + string.Join("\n  ", problems));
        }
    }
}
