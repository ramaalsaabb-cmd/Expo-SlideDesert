using UnityEditor;
using UnityEngine;

public class KatheebAssetImport : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if(!assetPath.Contains("/KatheebCharacters/")) return;
        var importer=(ModelImporter)assetImporter;
        importer.isReadable=true;
        importer.importAnimation=false;
        importer.importBlendShapes=false;
        importer.animationType=ModelImporterAnimationType.Generic;
        importer.materialImportMode=ModelImporterMaterialImportMode.None;
    }
    void OnPreprocessTexture()
    {
        if(!assetPath.Contains("/KatheebUI/") && !assetPath.Contains("/KatheebCharacters/")) return;
        var importer=(TextureImporter)assetImporter;
        importer.textureShape=TextureImporterShape.Texture2D;
        importer.textureType=assetPath.EndsWith("/Normal.png")?TextureImporterType.NormalMap:TextureImporterType.Default;
        importer.alphaSource=TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency=assetPath.Contains("/KatheebUI/");
        importer.mipmapEnabled=!assetPath.Contains("/KatheebUI/");
        importer.npotScale=TextureImporterNPOTScale.None;
        importer.maxTextureSize=assetPath.Contains("/KatheebUI/")?4096:2048;
        if(assetPath.Contains("/KatheebUI/"))
        {
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.filterMode=FilterMode.Bilinear;
            importer.wrapMode=TextureWrapMode.Clamp;
        }
    }
    [InitializeOnLoadMethod]
    static void ScheduleRigImportCheck() { EditorApplication.delayCall += ConfigureRigs; }
    static void ConfigureRigs()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach(string art in new[]{"Menu","Cards","Countdown"})
        {
            var importer=AssetImporter.GetAtPath("Assets/Resources/KatheebUI/"+art+".png") as TextureImporter;
            if(!importer) continue;
            if(importer.maxTextureSize==4096 && importer.textureCompression==TextureImporterCompression.Uncompressed && importer.wrapMode==TextureWrapMode.Clamp) continue;
            importer.maxTextureSize=4096;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.wrapMode=TextureWrapMode.Clamp;
            foreach(string platform in new[]{"Android","Standalone"})
                importer.ClearPlatformTextureSettings(platform);
            importer.SaveAndReimport();
        }
    }
    [MenuItem("Tools/Desert VR/Validate Katheeb Menu Assets")]
    static void Validate()
    {
        foreach(string path in new[]{"Assets/Resources/KatheebUI/Menu.png","Assets/Resources/KatheebUI/Cards.png","Assets/Resources/KatheebUI/Countdown.png"})
            if(!AssetDatabase.LoadMainAssetAtPath(path)) Debug.LogError("Katheeb asset missing: "+path);
        var shader=Resources.Load<Shader>("KatheebUI/Artwork");
        if(!shader || ShaderUtil.ShaderHasError(shader)) Debug.LogError("Katheeb artwork shader failed to import.");
        else Debug.Log("Katheeb menu assets and shader validated.");
    }
}
