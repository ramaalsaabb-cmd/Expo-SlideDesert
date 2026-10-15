using UnityEngine;

/// <summary>Consistent metre-scale display proportions for the static overview only.</summary>
[ExecuteAlways]
public class DesertOverviewScale : MonoBehaviour
{
    public Transform car, sled, tent, tentFabric;
    public Transform xrOrigin;
    public Transform characters;
    [Header("Overview composition in metres")]
    public Vector3 tentCentre = new Vector3(92f, 0f, 40f);
    public Vector3 carCentre = new Vector3(92f, 0f, 28f);
    public Vector3 characterRowCentre = new Vector3(95f, 0f, 29f);
    public Vector3 viewingPosition = new Vector3(96f, 0f, 17f);
    public Vector3 viewingFocus = new Vector3(97f, 0f, 35f);
    public float characterSpacing = 1.45f;
    public Transform carHitch, sledHitch;
    public MeshFilter rope;
    public float carLength = 4.8f, sledLength = 1.5f, tentFootprint = 14f;
    Mesh ropeMesh;
    bool ready;
    public bool applyLayoutAutomatically = false;
    void OnEnable() { ready = false; }
    void Update()
    {
        if (!applyLayoutAutomatically || ready || !car || !sled || !tent) return;
        // Consistent headings: parked truck at three-quarter view, tent entrance toward the viewer.
        car.rotation = Quaternion.Euler(0f, -35f, 0f);
        sled.rotation = car.rotation;
        if (tentFabric) tent.rotation = Quaternion.Euler(0f, 180f - tentFabric.localEulerAngles.y, 0f);
        if (!Fit(car, carLength) || !Fit(sled, sledLength) || !Fit(tent, tentFootprint, tentFabric)) return;
        PlaceCentre(tent, tentFabric ? tentFabric : tent, tentCentre);
        Fit(tent, tentFootprint, tentFabric);
        // "In front" is the visible side nearest the XR viewer, beyond the tent's actual edge.
        if (BoundsOf(tentFabric ? tentFabric : tent, out var shelter) && BoundsOf(car, out var truck))
        {
            Vector3 towardViewer = Vector3.ProjectOnPlane(viewingPosition - shelter.center, Vector3.up).normalized;
            if (towardViewer.sqrMagnitude < 0.01f) towardViewer = Vector3.back;
            float tentRadius = Mathf.Abs(towardViewer.x) * shelter.extents.x + Mathf.Abs(towardViewer.z) * shelter.extents.z;
            float carRadius = Mathf.Abs(towardViewer.x) * truck.extents.x + Mathf.Abs(towardViewer.z) * truck.extents.z;
            carCentre = shelter.center + towardViewer * (tentRadius + carRadius + 4f);
            PlaceCentre(car, car, carCentre);
        }
        else PlaceCentre(car, car, carCentre);
        Fit(car, carLength);
        if (carHitch && sledHitch && BoundsOf(car, out var carBounds))
        {
            Vector3 behind = Vector3.ProjectOnPlane(carHitch.position - carBounds.center, Vector3.up).normalized;
            if (behind.sqrMagnitude < 0.01f) behind = -car.forward;
            Vector3 desiredHitch = carHitch.position + behind * 2.5f;
            Vector3 offset = desiredHitch - sledHitch.position;
            offset.y = 0;
            sled.position += offset;
            Fit(sled, sledLength);
        }
        ArrangeCharacters();
        ReconnectRope();
        if (xrOrigin && BoundsOf(tentFabric ? tentFabric : tent, out var tentBounds))
        {
            Vector3 position = viewingPosition;
            if (BoundsOf(sled, out var sledBounds))
            {
                float frontEdge = Mathf.Min(carCentre.z - carLength * 0.5f, sledBounds.min.z);
                position.z = Mathf.Min(position.z, frontEdge - 8f);
            }
            foreach (var terrain in Terrain.activeTerrains)
            {
                if (terrain.gameObject.scene != gameObject.scene || !terrain.terrainData) continue;
                Vector3 local = position - terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.z) continue;
                position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
                break;
            }
            xrOrigin.position = position;
            Vector3 facing = Vector3.ProjectOnPlane(viewingFocus - position, Vector3.up);
            if (facing.sqrMagnitude > 0.01f) xrOrigin.rotation = Quaternion.LookRotation(facing, Vector3.up);
        }
        ready = true;
    }
    static void PlaceCentre(Transform root, Transform measure, Vector3 destination)
    {
        if (!BoundsOf(measure, out var bounds)) return;
        root.position += new Vector3(destination.x - bounds.center.x, 0f, destination.z - bounds.center.z);
    }
    void ArrangeCharacters()
    {
        if (!characters) return;
        characters.SetPositionAndRotation(characterRowCentre, Quaternion.identity);
        characters.localScale = Vector3.one;
        var riders = characters.GetComponentsInChildren<OverviewImportedCharacter>(true);
        for (int i = 0; i < riders.Length; i++)
        {
            var slot = riders[i].transform;
            slot.localPosition = new Vector3((i - (riders.Length - 1) * 0.5f) * characterSpacing, 0f, 0f);
            slot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            slot.localScale = Vector3.one;
            riders[i].RefreshPlacement();
        }
    }
    [ContextMenu("Apply realistic overview sizes")]
    public void Reapply() { ready = false; applyLayoutAutomatically = true; }
    static bool BoundsOf(Transform root, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(false))
        {
            if (!renderer.enabled || renderer.name.ToLowerInvariant().Contains("rope")) continue;
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found && bounds.size.sqrMagnitude > 0.00001f;
    }
    static bool Fit(Transform root, float metres, Transform measure = null)
    {
        if (!measure) measure = root;
        if (!BoundsOf(measure, out var bounds)) return false;
        Vector3 centre = bounds.center;
        float length = Mathf.Max(bounds.size.x, bounds.size.z);
        if (length < 0.001f || float.IsNaN(length) || float.IsInfinity(length)) return false;
        float factor = metres / length;
        if (Mathf.Abs(factor - 1f) > 0.0001f) root.localScale *= factor;
        if (!BoundsOf(measure, out var resized)) return false;
        // Keep the visible object centred when its imported pivot is offset.
        root.position += new Vector3(centre.x - resized.center.x, 0, centre.z - resized.center.z);
        BoundsOf(measure, out resized);
        float ground = 0f;
        foreach (var terrain in Terrain.activeTerrains)
        {
            if (terrain.gameObject.scene != root.gameObject.scene || !terrain.terrainData) continue;
            var local = resized.center - terrain.transform.position;
            var size = terrain.terrainData.size;
            if (local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.z) continue;
            ground = terrain.SampleHeight(resized.center) + terrain.transform.position.y;
            break;
        }
        root.position += Vector3.up * (ground + 0.015f - resized.min.y);
        return true;
    }
    void ReconnectRope()
    {
        if (!rope || !carHitch || !sledHitch) return;
        const int segments = 32, sides = 8;
        var vertices = new Vector3[(segments + 1) * sides];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[segments * sides * 6];
        Vector3 a = carHitch.position, b = sledHitch.position;
        Vector3 direction = (b - a).normalized;
        Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.1f) right = Vector3.right;
        Vector3 up = Vector3.Cross(right, direction).normalized;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 centre = Vector3.Lerp(a, b, t) - Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.18f);
            for (int j = 0; j < sides; j++)
            {
                float angle = j * Mathf.PI * 2f / sides;
                vertices[i * sides + j] = rope.transform.InverseTransformPoint(centre + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * 0.012f);
                uv[i * sides + j] = new Vector2(j / (float)sides, t * Vector3.Distance(a, b));
                if (i == segments) continue;
                int at = (i * sides + j) * 6, v = i * sides + j, next = i * sides + (j + 1) % sides;
                triangles[at] = v; triangles[at + 1] = next; triangles[at + 2] = v + sides;
                triangles[at + 3] = next; triangles[at + 4] = next + sides; triangles[at + 5] = v + sides;
            }
        }
        if (ropeMesh) { if (Application.isPlaying) Destroy(ropeMesh); else DestroyImmediate(ropeMesh); }
        ropeMesh = new Mesh { name = "Overview rope at realistic scale", hideFlags = HideFlags.DontSave };
        ropeMesh.vertices = vertices; ropeMesh.uv = uv; ropeMesh.triangles = triangles;
        ropeMesh.RecalculateNormals(); ropeMesh.RecalculateBounds(); rope.sharedMesh = ropeMesh;
    }
    void OnDestroy()
    {
        if (!ropeMesh) return;
        if (Application.isPlaying) Destroy(ropeMesh); else DestroyImmediate(ropeMesh);
    }
}
