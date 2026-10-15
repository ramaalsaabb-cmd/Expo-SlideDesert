using UnityEditor;
using UnityEngine;

/// <summary>Repairs the overview's own terrain asset once; never edits the driving terrain.</summary>
public static class DesertOverviewGroundSetup
{
    [InitializeOnLoadMethod]
    static void Schedule() { EditorApplication.delayCall += Prepare; }
    [MenuItem("Tools/Desert VR/Repair Overview Ground and Enable Audio")]
    public static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Scenes/DesertOverviewTerrain.asset");
        if (data)
        {
            int resolution = data.heightmapResolution;
            var heights = data.GetHeights(0, 0, resolution, resolution);
            bool needsFlattening = false;
            foreach (float height in heights) if (height != 0f) { needsFlattening = true; break; }
            if (needsFlattening)
            {
                data.SetHeights(0, 0, new float[resolution, resolution]);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
                Debug.Log("DesertOverview: saved permanent flat ground. Driving terrain unchanged.");
            }
        }
        EditorUtility.audioMasterMute = false;
    }
}
