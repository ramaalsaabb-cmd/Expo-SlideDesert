using UnityEditor;
using UnityEngine;

public class CharacterCollectionImport : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.Contains("/CharacterCollection/")) return;
        var importer = (ModelImporter)assetImporter;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
    }
    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/CharacterCollection/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.textureType = assetPath.ToLowerInvariant().Contains("normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 2048;
        importer.alphaIsTransparency = false;
    }
}
