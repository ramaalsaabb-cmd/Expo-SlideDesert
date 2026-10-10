// Place in Assets/Editor. Select Sled_Unity.fbx, then Tools > Sled > Create Prefab.
using UnityEngine;
using UnityEditor;
public static class SledPrefabBuilder
{
    [MenuItem("Tools/Sled/Create Prefab")]
    public static void Build()
    {
        var model = Selection.activeObject as GameObject;
        if (model == null || !AssetDatabase.GetAssetPath(model).EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
        { Debug.LogError("Select Sled_Unity.fbx in the Project window first."); return; }
        var root = new GameObject("Sled");
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
        visual.transform.SetParent(root.transform, false);
        var rb = root.AddComponent<Rigidbody>(); rb.mass = 2.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        Box(root, "Body collision", new Vector3(0,.048f,0), new Vector3(.405f,.065f,1.15f));
        Box(root, "Front collision", new Vector3(0,.085f,.49f), new Vector3(.36f,.11f,.21f));
        Box(root, "Left rail collision", new Vector3(-.202f,.105f,-.135f), new Vector3(.038f,.12f,.69f));
        Box(root, "Right rail collision", new Vector3(.202f,.105f,-.135f), new Vector3(.038f,.12f,.69f));
        var attach = new GameObject("HandleAttach"); attach.transform.SetParent(root.transform, false);
        attach.transform.localPosition = new Vector3(0,.05f,1.018f);
        string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(model)).Replace('\\','/');
        string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/Sled.prefab");
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root); Selection.activeObject = prefab;
        Debug.Log("Created " + path + ". Add XR Grab Interactable for your project's XR toolkit.");
    }
    static void Box(GameObject root, string name, Vector3 center, Vector3 size)
    {
        var child = new GameObject(name); child.transform.SetParent(root.transform, false);
        var collider = child.AddComponent<BoxCollider>(); collider.center = center; collider.size = size;
    }
}
