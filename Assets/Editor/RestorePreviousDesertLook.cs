using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Restore cached editor objects from the reverted appearance pass once.</summary>
[InitializeOnLoad]
public static class RestorePreviousDesertLook
{
    const string DoneFile = @"C:/Users/ramaa/Documents/Codex/2026-10-10/i-x20-2/work/desert-look-rollback.complete";
    static RestorePreviousDesertLook()
    {
        EditorApplication.delayCall += Restore;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Restore;
        };
    }
    static void Restore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || System.IO.File.Exists(DoneFile)) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Demo.unity") return;
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/8K Skybox Pack Free/Skyboxes/Material/sky-1.mat");
        RenderSettings.fog = false;
        RenderSettings.fogColor = new Color(0.5f,0.5f,0.5f);
        RenderSettings.fogDensity = 0.01f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientSkyColor = new Color(0.212f,0.227f,0.259f);
        RenderSettings.ambientEquatorColor = new Color(0.114f,0.125f,0.133f);
        RenderSettings.ambientGroundColor = new Color(0.047f,0.043f,0.035f);
        RenderSettings.reflectionIntensity = 1f;
        RenderSettings.sun = null;
        foreach (var light in Object.FindObjectsByType<Light>())
        {
            if (light.gameObject.scene != scene || light.type != LightType.Directional) continue;
            light.transform.rotation = new Quaternion(0.49643937f,-0.5411703f,-0.18426016f,0.6532464f);
            light.color = new Color(1f,0.95686275f,0.8392157f);
            light.intensity = 1f;
            light.shadowStrength = 1f;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.4f;
        }
        var originalLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/NewLayer.terrainlayer");
        foreach (var terrain in Terrain.activeTerrains)
        {
            if (terrain.gameObject.scene != scene || !terrain.terrainData || !originalLayer) continue;
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 1000f;
            var layers = terrain.terrainData.terrainLayers;
            if (layers.Length == 1 && (!layers[0] || layers[0].name == "DrySand"))
            {
                terrain.terrainData.terrainLayers = new[] { originalLayer };
                EditorUtility.SetDirty(terrain.terrainData);
            }
        }
        var dustMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/dust.mat");
        foreach (var dust in Object.FindObjectsByType<WheelDustController>())
        {
            if (dust.gameObject.scene != scene) continue;
            dust.dustMaterial = dustMaterial;
            EditorUtility.SetDirty(dust);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        System.IO.File.WriteAllText(DoneFile, "Restored");
        Debug.Log("[Desert Look] Previous appearance restored; seating and subtle blinking retained.");
    }
}
