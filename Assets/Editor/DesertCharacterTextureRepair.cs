using System;
using UnityEditor;
using UnityEngine;

public class DesertCharacterTextureRepair : AssetPostprocessor
{
    static bool IsCharacterTexture(string path)
    {
        path = path.Replace('\\', '/');
        return path.Contains("/ArabicVRMenu/") || path.Contains("/CharacterIcons/") || path.Contains("/thobe+3d+model/")
            || path.Contains("/green+embroidered+dress+3d+model/")
            || path.Contains("/tripo_convert_c676e25b-00a7-4839-a02f-3f9056a6bad2.fbm/");
    }
    void OnPreprocessTexture()
    {
        if (!IsCharacterTexture(assetPath)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureShape = TextureImporterShape.Texture2D;
        if (assetPath.Contains("/ArabicVRMenu/"))
        {
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
        }
        importer.textureType = assetPath.IndexOf("normal",StringComparison.OrdinalIgnoreCase)>=0
            ? TextureImporterType.NormalMap : TextureImporterType.Default;
    }
    [InitializeOnLoadMethod]
    static void ScheduleRepair() { EditorApplication.delayCall += Repair; }
    [MenuItem("Tools/Desert VR/Repair Character Textures")]
    public static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach(var guid in AssetDatabase.FindAssets("t:Texture",new[]{"Assets"}))
        {
            var path=AssetDatabase.GUIDToAssetPath(guid);
            if (!IsCharacterTexture(path)) continue;
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer || importer.textureShape==TextureImporterShape.Texture2D) continue;
            importer.textureShape=TextureImporterShape.Texture2D;
            importer.SaveAndReimport();
        }
    }
    [MenuItem("Tools/Desert VR/Test Fade To DesertOverview")]
    static void TestFade()
    {
        if (!EditorApplication.isPlaying) { Debug.Log("Press Play, then run Test Fade To DesertOverview.");return; }
        var transition=UnityEngine.Object.FindAnyObjectByType<DesertSceneTransition>();
        if(!transition) transition=new GameObject("End Of Route Transition").AddComponent<DesertSceneTransition>();
        transition.TryBegin();
    }
}
