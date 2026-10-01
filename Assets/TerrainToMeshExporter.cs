using UnityEngine;
using UnityEditor;

public class TerrainToMeshExporter : MonoBehaviour
{
    [MenuItem("Tools/Export Terrain Mesh")]
    static void Export()
    {
        var t = Selection.activeGameObject.GetComponent<Terrain>();
        if (!t) return;

        var d = t.terrainData;
        int r = d.heightmapResolution, n = r;
        var h = d.GetHeights(0, 0, r, r);
        var v = new Vector3[n * n];
        var uv = new Vector2[v.Length];
        var tris = new int[(n - 1) * (n - 1) * 6];

        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
        {
            int i = z * n + x;
            v[i] = new Vector3(
                x * d.size.x / (n - 1),
                h[z, x] * d.size.y,
                z * d.size.z / (n - 1));
            uv[i] = new Vector2((float)x / (n - 1),
                                (float)z / (n - 1));
        }

        int k = 0;
        for (int z = 0; z < n - 1; z++)
        for (int x = 0; x < n - 1; x++)
        {
            int a = z * n + x, b = a + n;
            tris[k++] = a; tris[k++] = b; tris[k++] = a + 1;
            tris[k++] = a + 1; tris[k++] = b; tris[k++] = b + 1;
        }

        var m = new Mesh { indexFormat =
            UnityEngine.Rendering.IndexFormat.UInt32 };
        m.vertices = v;
        m.uv = uv;
        m.triangles = tris;
        m.RecalculateNormals();

        var go = new GameObject("Terrain");
        go.transform.position = t.transform.position;
        go.AddComponent<MeshFilter>().sharedMesh = m;
        go.AddComponent<MeshRenderer>();
    }
}