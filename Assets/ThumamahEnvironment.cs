using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Previewable, lightweight surroundings. The original TerrainData is never edited.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class ThumamahEnvironment : MonoBehaviour
{
    public Terrain sourceTerrain;
    public Material landscapeMaterial;
    public Material mountainMaterial;
    [Range(200f, 600f)] public float surroundingExtent = 360f;
    [Range(40f, 180f)] public float mountainHeight = 115f;
    [Range(6f, 20f)] public float meshSpacing = 10f;
    public int seed = 314;

    GameObject generatedRoot;
    readonly List<Mesh> generatedMeshes = new List<Mesh>();
    bool rebuildRequested;
    Color sandColor;
    const HideFlags GeneratedFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

    void OnEnable() { Rebuild(); }
    void OnValidate() { rebuildRequested = true; }
    void Update()
    {
        if (rebuildRequested) { rebuildRequested = false; Rebuild(); }
    }
    void OnDisable() { Clear(); }

    [ContextMenu("Rebuild Thumamah surroundings")]
    public void Rebuild()
    {
        rebuildRequested = false;
        Clear();
        if (!sourceTerrain || !sourceTerrain.terrainData || !landscapeMaterial) return;
        Vector3 size = sourceTerrain.terrainData.size;
        sandColor = new Color(0.72f, 0.57f, 0.39f);
        float extent = Mathf.Clamp(surroundingExtent, 200f, 600f);
        generatedRoot = new GameObject("Thumamah - generated mountains and dunes");
        generatedRoot.hideFlags = GeneratedFlags;
        generatedRoot.transform.SetParent(transform, false);
        // Match the terrain's frame, even when its parent has moved.
        generatedRoot.transform.SetPositionAndRotation(sourceTerrain.transform.position, sourceTerrain.transform.rotation);
        generatedRoot.transform.localScale = Vector3.one;
        float[] xs = { -extent, 0f, size.x, size.x + extent };
        float[] zs = { -extent, 0f, size.z, size.z + extent };
        for (int z = 0; z < 3; z++)
            for (int x = 0; x < 3; x++)
            {
                if (x == 1 && z == 1) continue;
                // Small chunks allow the headset to cull the landscape behind the viewer.
                int nx = Mathf.CeilToInt((xs[x + 1] - xs[x]) / 180f);
                int nz = Mathf.CeilToInt((zs[z + 1] - zs[z]) / 180f);
                for (int iz = 0; iz < nz; iz++)
                    for (int ix = 0; ix < nx; ix++)
                        BuildGround(Mathf.Lerp(xs[x], xs[x + 1], ix / (float)nx),
                            Mathf.Lerp(xs[x], xs[x + 1], (ix + 1f) / nx),
                            Mathf.Lerp(zs[z], zs[z + 1], iz / (float)nz),
                            Mathf.Lerp(zs[z], zs[z + 1], (iz + 1f) / nz), size);
            }
    }

    float Height(float x, float z, Vector3 size, out float rock)
    {
        float distance = ThumamahLandscapeProfile.DistanceOutside(x, z, size.x, size.z);
        float edge = sourceTerrain.terrainData.GetInterpolatedHeight(
            Mathf.Clamp01(x / size.x), Mathf.Clamp01(z / size.z));
        float datum = sourceTerrain.terrainData.GetHeight(0, 0);
        float baseHeight = Mathf.Lerp(edge, datum, ThumamahLandscapeProfile.Smooth(0f, 100f, distance));
        return baseHeight + ThumamahLandscapeProfile.Relief(x, z, size.x, size.z,
            Mathf.Clamp(surroundingExtent, 200f, 600f), Mathf.Clamp(mountainHeight, 40f, 180f), seed, out rock);
    }

    Color GroundColor(float x, float z, float rock)
    {
        Color limestone = new Color(0.65f, 0.57f, 0.45f);
        Color color = Color.Lerp(sandColor, limestone, rock);
        color *= Mathf.Lerp(0.91f, 1.06f, ThumamahLandscapeProfile.Noise(x / 18f, z / 18f, seed + 25));
        color.a = rock;
        return color;
    }

    void BuildGround(float minX, float maxX, float minZ, float maxZ, Vector3 size)
    {
        float spacing = Mathf.Clamp(meshSpacing, 6f, 20f);
        int nx = Mathf.CeilToInt((maxX - minX) / spacing), nz = Mathf.CeilToInt((maxZ - minZ) / spacing);
        Vector3 origin = new Vector3(minX, 0f, minZ);
        var vertices = new Vector3[(nx + 1) * (nz + 1)];
        var normals = new Vector3[vertices.Length];
        var colors = new Color[vertices.Length];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[nx * nz * 6];
        for (int z = 0; z <= nz; z++)
            for (int x = 0; x <= nx; x++)
            {
                float px = Mathf.Lerp(minX, maxX, x / (float)nx), pz = Mathf.Lerp(minZ, maxZ, z / (float)nz);
                int index = z * (nx + 1) + x;
                float height = Height(px, pz, size, out float rock);
                vertices[index] = new Vector3(px, height, pz) - origin;
                // Evaluate the same global height field on tile borders to avoid lighting seams.
                float dx = Height(px + 1f, pz, size, out _) - Height(px - 1f, pz, size, out _);
                float dz = Height(px, pz + 1f, size, out _) - Height(px, pz - 1f, size, out _);
                normals[index] = new Vector3(-dx, 2f, -dz).normalized;
                colors[index] = GroundColor(px, pz, rock);
                // Use the same normalized UV scale as the original terrain material.
                uv[index] = new Vector2(px / size.x, pz / size.z);
            }
        int t = 0;
        for (int z = 0; z < nz; z++)
            for (int x = 0; x < nx; x++)
            {
                int a = z * (nx + 1) + x, b = a + nx + 1;
                triangles[t++] = a; triangles[t++] = b; triangles[t++] = a + 1;
                triangles[t++] = a + 1; triangles[t++] = b; triangles[t++] = b + 1;
            }
        var mesh = new Mesh { name = "Thumamah landscape tile", hideFlags = GeneratedFlags };
        mesh.vertices = vertices; mesh.normals = normals; mesh.colors = colors; mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        AddMesh(mesh, origin, "Desert ridge and dune tile",
            mountainMaterial ? new[] { mountainMaterial } : null);
    }

    void AddMesh(Mesh mesh, Vector3 origin, string label, Material[] materials = null)
    {
        generatedMeshes.Add(mesh);
        var obj = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
        obj.hideFlags = GeneratedFlags;
        obj.layer = sourceTerrain.gameObject.layer;
        obj.transform.SetParent(generatedRoot.transform, false);
        obj.transform.localPosition = origin;
        obj.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = obj.GetComponent<MeshRenderer>();
        if (materials != null) renderer.sharedMaterials = materials;
        else renderer.sharedMaterial = landscapeMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
    }

    void Clear()
    {
        // Only objects made by this component are discarded.
        if (generatedRoot) { generatedRoot.SetActive(false); Dispose(generatedRoot); }
        generatedRoot = null;
        foreach (var mesh in generatedMeshes) if (mesh) Dispose(mesh);
        generatedMeshes.Clear();
    }
    static void Dispose(UnityEngine.Object obj)
    {
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}
