using UnityEngine;

/// <summary>Displays an imported character in edit mode and in the static overview scene.</summary>
[ExecuteAlways]
public class OverviewImportedCharacter : MonoBehaviour
{
    public string modelResource;
    public Material clothingMaterial;
    public float displayHeight = 1.75f;
    void OnEnable() { Populate(); }
    void Update() { if (transform.childCount == 0) Populate(); }
    void Populate()
    {
        if (transform.childCount != 0 || string.IsNullOrEmpty(modelResource)) return;
        var source = Resources.Load<GameObject>(modelResource);
        if (!source || !clothingMaterial) return; // Unity may still be importing the FBX.
        var actor = Instantiate(source, transform, false);
        actor.name = "Character Model";
        foreach (var animator in actor.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        bool hasBounds = false;
        Bounds bounds = default;
        foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true))
        {
            var slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++) slots[i] = clothingMaterial;
            renderer.sharedMaterials = slots;
            renderer.enabled = true;
            if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (!hasBounds || bounds.size.y < 0.0001f) return;
        float scale = displayHeight / bounds.size.y;
        Vector3 bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        Vector3 localBottom = actor.transform.InverseTransformPoint(bottom);
        actor.transform.localScale *= scale;
        actor.transform.position += transform.position - actor.transform.TransformPoint(localBottom);
        foreach (var terrain in Terrain.activeTerrains)
        {
            if (!terrain.terrainData || terrain.gameObject.scene != gameObject.scene) continue;
            var at = transform.position - terrain.transform.position;
            var size = terrain.terrainData.size;
            if (at.x < 0 || at.z < 0 || at.x > size.x || at.z > size.z) continue;
            var position = actor.transform.position;
            position.y += terrain.SampleHeight(transform.position) + terrain.transform.position.y - transform.position.y;
            actor.transform.position = position;
            break;
        }
    }
}
