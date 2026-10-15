using UnityEngine;

/// <summary>Flat overview ground, preserving the terrain asset used by the driving scene.</summary>
[ExecuteAlways, DefaultExecutionOrder(-20000)]
public class DesertOverviewFlatGround : MonoBehaviour
{
    public Terrain target;
    TerrainData original, flat;
    void OnEnable()
    {
        if (!target || !target.terrainData) return;
        original = target.terrainData;
        flat = Instantiate(original);
        flat.name = "DesertOverview - flat sand";
        flat.hideFlags = HideFlags.DontSave;
        int resolution = flat.heightmapResolution;
        flat.SetHeights(0, 0, new float[resolution, resolution]);
        target.terrainData = flat;
        var collider = target.GetComponent<TerrainCollider>();
        if (collider) collider.terrainData = flat;
        target.Flush();
    }
    void OnDisable()
    {
        if (target && target.terrainData == flat)
        {
            target.terrainData = original;
            var collider = target.GetComponent<TerrainCollider>();
            if (collider) collider.terrainData = original;
        }
        if (flat)
        {
            if (Application.isPlaying) Destroy(flat); else DestroyImmediate(flat);
        }
        flat = null;
    }
}
