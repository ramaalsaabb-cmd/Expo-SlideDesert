using System.Collections.Generic;
using UnityEngine;

/// <summary>Visible terrain bumps along the driving route; source TerrainData stays intact.</summary>
[DefaultExecutionOrder(-5000)]
public class RouteTerrainBumps : MonoBehaviour
{
    public CarPathFollower route;
    public Terrain[] terrains;
    public float spacing = 23f;
    public float halfWidth = 18f;
    public float halfLength = 6f;
    public float minimumHeight = 0.35f;
    public float maximumHeight = 0.65f;
    public float endClearance = 60f;
    readonly Dictionary<Terrain, TerrainData> originals = new Dictionary<Terrain, TerrainData>();
    readonly List<TerrainData> generated = new List<TerrainData>();
    struct Bump { public Vector3 center, forward; public float height; }

    List<Bump> BuildBumps()
    {
        var bumps = new List<Bump>();
        if (!route || route.points == null || route.points.Length < 2) return bumps;
        float total = 0f;
        for (int i = 1; i < route.points.Length; i++)
            if (route.points[i - 1] && route.points[i])
                total += Vector3.ProjectOnPlane(route.points[i].position - route.points[i - 1].position, Vector3.up).magnitude;
        // Leave enough path after the final bump for the trailing sled to pass it.
        for (float distance = 35f; distance < total - endClearance; distance += Mathf.Max(12f, spacing))
        {
            float remaining = distance;
            for (int i = 1; i < route.points.Length; i++)
            {
                if (!route.points[i - 1] || !route.points[i]) continue;
                Vector3 direction = Vector3.ProjectOnPlane(route.points[i].position - route.points[i - 1].position, Vector3.up);
                float length = direction.magnitude;
                if (length < 0.001f) continue;
                if (remaining > length) { remaining -= length; continue; }
                bumps.Add(new Bump {
                    center = route.points[i - 1].position + direction * (remaining / length),
                    forward = direction / length,
                    height = Mathf.Lerp(minimumHeight, maximumHeight, (bumps.Count % 4) / 3f)
                });
                break;
            }
        }
        return bumps;
    }

    void Awake()
    {
        var bumps = BuildBumps();
        if (terrains == null) return;
        foreach (var terrain in terrains)
        {
            if (!terrain || !terrain.terrainData) continue;
            var original = terrain.terrainData;
            var data = Instantiate(original);
            data.name = original.name + " (route bumps runtime)";
            originals.Add(terrain, original);
            generated.Add(data);
            int resolution = data.heightmapResolution;
            var heights = data.GetHeights(0, 0, resolution, resolution);
            Vector3 size = data.size, origin = terrain.transform.position;
            float radius = Mathf.Sqrt(halfWidth * halfWidth + halfLength * halfLength);
            foreach (var bump in bumps)
            {
                int minX = Mathf.Clamp(Mathf.FloorToInt((bump.center.x - radius - origin.x) / size.x * (resolution - 1)), 0, resolution - 1);
                int maxX = Mathf.Clamp(Mathf.CeilToInt((bump.center.x + radius - origin.x) / size.x * (resolution - 1)), 0, resolution - 1);
                int minZ = Mathf.Clamp(Mathf.FloorToInt((bump.center.z - radius - origin.z) / size.z * (resolution - 1)), 0, resolution - 1);
                int maxZ = Mathf.Clamp(Mathf.CeilToInt((bump.center.z + radius - origin.z) / size.z * (resolution - 1)), 0, resolution - 1);
                Vector3 right = Vector3.Cross(Vector3.up, bump.forward);
                for (int z = minZ; z <= maxZ; z++)
                    for (int x = minX; x <= maxX; x++)
                    {
                        var delta = new Vector3(origin.x + x * size.x / (resolution - 1), 0f,
                            origin.z + z * size.z / (resolution - 1)) - new Vector3(bump.center.x, 0f, bump.center.z);
                        float along = Mathf.Abs(Vector3.Dot(delta, bump.forward)) / Mathf.Max(1f, halfLength);
                        float across = Mathf.Abs(Vector3.Dot(delta, right)) / Mathf.Max(1f, halfWidth);
                        if (along >= 1f || across >= 1f) continue;
                        float longitudinal = 0.5f + 0.5f * Mathf.Cos(along * Mathf.PI);
                        float lateral = 0.5f + 0.5f * Mathf.Cos(across * Mathf.PI);
                        heights[z, x] = Mathf.Clamp01(heights[z, x] + bump.height * longitudinal * lateral / size.y);
                    }
            }
            data.SetHeights(0, 0, heights);
            terrain.terrainData = data;
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider) collider.terrainData = data;
        }
        Physics.SyncTransforms();
        Debug.Log("[Route] Added " + bumps.Count + " terrain bumps across " + route.points.Length + " path points.", this);
    }

    void OnDestroy()
    {
        foreach (var pair in originals)
        {
            if (!pair.Key) continue;
            pair.Key.terrainData = pair.Value;
            var collider = pair.Key.GetComponent<TerrainCollider>();
            if (collider) collider.terrainData = pair.Value;
        }
        foreach (var data in generated) if (data) Destroy(data);
    }

    void OnDrawGizmosSelected()
    {
        if (!route || route.points == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 1; i < route.points.Length; i++)
            if (route.points[i - 1] && route.points[i]) Gizmos.DrawLine(route.points[i - 1].position, route.points[i].position);
        Gizmos.color = Color.yellow;
        foreach (var bump in BuildBumps())
            Gizmos.DrawWireSphere(bump.center, halfLength);
    }
}
