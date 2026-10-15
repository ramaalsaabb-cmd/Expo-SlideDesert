using UnityEngine;

/// <summary>Visual rope only: follows the two existing hitches without moving scene objects.</summary>
[ExecuteAlways]
public class OverviewTowRope : MonoBehaviour
{
    public Transform carHitch, sledHitch;
    public Transform carVisual, sledVisual;
    public Material ropeMaterial;
    public float thickness = 0.05f;
    public float sag = 0.12f;
    LineRenderer line;
    void OnEnable() { Draw(); }
    void LateUpdate() { Draw(); }
    public void Draw()
    {
        Vector3 start, end;
        if (VisualBounds(carVisual, out var car) && VisualBounds(sledVisual, out var sled))
        {
            // Use the visible meshes; imported hitch transforms can be offset from them.
            start = car.ClosestPoint(sled.center);
            end = sled.ClosestPoint(car.center);
            start.y = car.min.y + Mathf.Min(0.4f, car.size.y * 0.25f);
            end.y = sled.min.y + Mathf.Min(0.15f, sled.size.y * 0.6f);
        }
        else if (carHitch && sledHitch) { start = carHitch.position; end = sledHitch.position; }
        else return;
        if (!line) line = GetComponent<LineRenderer>();
        if (!line) line = gameObject.AddComponent<LineRenderer>();
        var previous = GetComponent<MeshRenderer>();
        if (previous)
        {
            if (!line.sharedMaterial) line.sharedMaterial = previous.sharedMaterial;
            previous.enabled = false;
        }
        if (ropeMaterial) line.sharedMaterial = ropeMaterial;
        if (!line.sharedMaterial) line.sharedMaterial = Resources.Load<Material>("TowRope");
        line.forceRenderingOff = false;
        line.enabled = true;
        line.useWorldSpace = true;
        line.startWidth = line.endWidth = thickness;
        line.startColor = line.endColor = Color.white;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.textureMode = LineTextureMode.Tile;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        line.receiveShadows = true;
        const int segments = 32;
        line.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 point = Vector3.Lerp(start, end, t);
            point.y -= Mathf.Sin(t * Mathf.PI) * Mathf.Min(sag, Mathf.Max(0f, Mathf.Min(start.y, end.y) - 0.04f));
            line.SetPosition(i, point);
        }
    }
    static bool VisualBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        if (!root) return false;
        bool found = false;
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(false))
        {
            string name = renderer.name.ToLowerInvariant();
            if (!renderer.enabled || name.Contains("rope") || name.Contains("hitch") || name.Contains("handle")) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found;
    }

}
