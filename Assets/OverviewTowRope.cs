using UnityEngine;

/// <summary>Visual rope only: follows the two existing hitches without moving scene objects.</summary>
[ExecuteAlways]
public class OverviewTowRope : MonoBehaviour
{
    public Transform carHitch, sledHitch;
    public Transform carVisual, sledVisual;
    public Material ropeMaterial;
    public float thickness = 0.018f;
    public float sag = 0.12f;
    LineRenderer line;
    [SerializeField] Vector3 carLocalAttachment,sledLocalAttachment;
    [SerializeField] bool attachmentsReady;
    public void RebuildAttachments(){attachmentsReady=false;Draw();}
    void OnEnable() { Draw(); }
    void LateUpdate() { Draw(); }
    public void Draw()
    {
        Vector3 start, end;
        if (VisualBounds(carVisual, out var car) && VisualBounds(sledVisual, out var sled))
        {
            if(!attachmentsReady)
            {
                var carPoint=car.ClosestPoint(sled.center);
                carPoint.y=car.min.y+Mathf.Min(.36f,car.size.y*.23f);
                var sledPoint=sled.ClosestPoint(car.center);
                sledPoint.y=sled.min.y+Mathf.Min(.12f,sled.size.y*.4f);
                // Snap endpoints to the actual imported mesh, not empty corners of a world-space box.
                carPoint=NearestVisibleVertex(carVisual,carPoint);
                sledPoint=NearestVisibleVertex(sledVisual,sledPoint);
                carLocalAttachment=carVisual.InverseTransformPoint(carPoint);
                sledLocalAttachment=sledVisual.InverseTransformPoint(sledPoint);
                attachmentsReady=true;
            }
            start=carVisual.TransformPoint(carLocalAttachment);
            end=sledVisual.TransformPoint(sledLocalAttachment);
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
        line.widthCurve=AnimationCurve.Linear(0,1,1,1);
        line.widthMultiplier=thickness;
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
    static Vector3 NearestVisibleVertex(Transform root,Vector3 desired)
    {
        float best=float.PositiveInfinity;Vector3 result=desired;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(false))
        {
            var renderer=filter.GetComponent<MeshRenderer>();
            string label=filter.name.ToLowerInvariant();
            if(!renderer||!renderer.enabled||!filter.sharedMesh||label.Contains("rope")||label.Contains("hitch")||label.Contains("handle"))continue;
            if(!filter.sharedMesh.isReadable)
            {
                // Imported FBX meshes may have CPU vertex access disabled; query their actual surface.
                var query=new GameObject("Temporary rope attachment query");query.hideFlags=HideFlags.HideAndDontSave;
                query.transform.SetPositionAndRotation(filter.transform.position,filter.transform.rotation);query.transform.localScale=filter.transform.lossyScale;
                var collider=query.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;
                var inward=renderer.bounds.center-desired;inward.y=0;
                if(inward.sqrMagnitude>.0001f)
                {
                    inward.Normalize();
                    if(collider.Raycast(new Ray(desired-inward*2f,inward),out var hit,renderer.bounds.size.magnitude+4f))
                    {
                        float score=(hit.point-desired).sqrMagnitude;
                        if(score<best){best=score;result=hit.point;}
                    }
                }
                if(Application.isPlaying)Destroy(query);else DestroyImmediate(query);
                continue;
            }
            foreach(var vertex in filter.sharedMesh.vertices)
            {
                var world=filter.transform.TransformPoint(vertex);
                float score=(world-desired).sqrMagnitude;
                if(score<best){best=score;result=world;}
            }
        }
        return result;
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
