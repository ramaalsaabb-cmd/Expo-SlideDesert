using System.Collections.Generic;
using UnityEngine;

/// <summary>Separates the local rider's head so the seated camera sees the body when looking down.</summary>
public class SeatedRiderView : MonoBehaviour
{
    const int HeadLayer = 31;
    MeshFilter sourceFilter;
    Mesh sourceMesh, bodyMesh, headMesh;
    GameObject headObject;
    Camera playerCamera;
    bool cameraIncludedHeadLayer;

    public void Configure(Camera camera)
    {
        if (bodyMesh || !camera) return;
        sourceFilter = GetComponent<MeshFilter>();
        var sourceRenderer = GetComponent<MeshRenderer>();
        if (!sourceFilter || !sourceRenderer || !sourceFilter.sharedMesh) return;
        sourceMesh = sourceFilter.sharedMesh;
        if (!sourceMesh.isReadable)
        {
            Debug.LogWarning("Seated rider mesh must be readable to hide its head.", this);
            return;
        }
        bodyMesh = Instantiate(sourceMesh);
        headMesh = Instantiate(sourceMesh);
        bodyMesh.name = sourceMesh.name + " (first person body)";
        headMesh.name = sourceMesh.name + " (head)";
        var vertices = sourceMesh.vertices;
        // This model's head/hair lies above 0.70 m in mesh coordinates.
        for (int sub = 0; sub < sourceMesh.subMeshCount; sub++)
        {
            var triangles = sourceMesh.GetTriangles(sub);
            var body = new List<int>();
            var head = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var destination = vertices[triangles[i]].y > 0.70f
                    || vertices[triangles[i + 1]].y > 0.70f
                    || vertices[triangles[i + 2]].y > 0.70f ? head : body;
                destination.Add(triangles[i]);
                destination.Add(triangles[i + 1]);
                destination.Add(triangles[i + 2]);
            }
            bodyMesh.SetTriangles(body, sub);
            headMesh.SetTriangles(head, sub);
        }
        sourceFilter.sharedMesh = bodyMesh;
        headObject = new GameObject("Rider head (hidden from seated camera)");
        headObject.layer = HeadLayer;
        headObject.transform.SetParent(transform, false);
        headObject.AddComponent<MeshFilter>().sharedMesh = headMesh;
        var headRenderer = headObject.AddComponent<MeshRenderer>();
        headRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
        headRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
        headRenderer.receiveShadows = sourceRenderer.receiveShadows;
        playerCamera = camera;
        cameraIncludedHeadLayer = (camera.cullingMask & (1 << HeadLayer)) != 0;
        camera.cullingMask &= ~(1 << HeadLayer);
    }

    void OnDestroy()
    {
        if (sourceFilter && sourceFilter.sharedMesh == bodyMesh) sourceFilter.sharedMesh = sourceMesh;
        if (playerCamera && cameraIncludedHeadLayer) playerCamera.cullingMask |= 1 << HeadLayer;
        if (headObject) Destroy(headObject);
        if (bodyMesh) Destroy(bodyMesh);
        if (headMesh) Destroy(headMesh);
    }
}
