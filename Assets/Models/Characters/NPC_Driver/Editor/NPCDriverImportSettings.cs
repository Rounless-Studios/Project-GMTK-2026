using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for the retargeted driver character under this folder.
///
/// Generic, not Humanoid, on purpose: the pose is authored against this exact
/// skeleton (hand wrapped on the wheel rim, phone held at a measured angle), and
/// humanoid retargeting would re-solve it through the avatar's muscle limits and
/// shift those hands. Switch to Humanoid only if this rig has to share clips with
/// other characters.
///
/// Everything here is written into the .meta files on import, so this script can be
/// deleted afterwards without losing the settings.
/// </summary>
public class NPCDriverImportSettings : AssetPostprocessor
{
    const string Root = "Assets/Models/Characters/NPC_Driver/";
    const string ClipName = "Drive_Phone_Idle";

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Root)) return;

        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;
        importer.importNormals = ModelImporterNormals.Import;   // normals come from the FBX
        // Materials are left on Unity's defaults -- the character's materials are new
        // to this project, so there is nothing to remap onto.
    }

    void OnPostprocessModel(GameObject root)
    {
        if (!assetPath.StartsWith(Root)) return;

        var importer = (ModelImporter)assetImporter;
        // defaultClipAnimations is empty on the very first pass, before the FBX is
        // parsed, so name the clip on the pass where it exists. The length check is
        // what stops the reimport below from recursing.
        if (importer.clipAnimations.Length == 0 && importer.defaultClipAnimations.Length > 0)
        {
            var clips = importer.defaultClipAnimations;
            clips[0].name = ClipName;
            clips[0].loopTime = true;      // the clip was authored to loop seamlessly
            importer.clipAnimations = clips;
            EditorApplication.delayCall += () => importer.SaveAndReimport();
        }
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root)) return;

        var importer = (TextureImporter)assetImporter;
        var name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        if (name.EndsWith("_n"))
        {
            importer.textureType = TextureImporterType.NormalMap;
        }
    }

    // The model binds its textures by filename at import time, so a texture that lands
    // after the model does not get picked up on its own. Reimport the model once when
    // that happens. Reimporting a model does not import textures, so this cannot loop.
    static bool _modelRefreshed;

    static void OnPostprocessAllAssets(string[] imported, string[] deleted,
                                       string[] moved, string[] movedFrom)
    {
        if (_modelRefreshed) return;
        foreach (var path in imported)
        {
            if (!path.StartsWith(Root + "Textures/")) continue;
            _modelRefreshed = true;
            EditorApplication.delayCall += () =>
            {
                AssetDatabase.ImportAsset(Root + "NPC_driver_phone.fbx",
                                          ImportAssetOptions.ForceUpdate);
            };
            return;
        }
    }
}
