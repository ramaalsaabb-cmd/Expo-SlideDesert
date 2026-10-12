using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class RestoreRequestedRider
{
    static RestoreRequestedRider()
    {
        EditorApplication.delayCall += ApplyToOpenScene;
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += ApplyToOpenScene;
    }

    static void ApplyToOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/Demo.unity") return;
        if (GameObject.Find("Sled_Sandal_Rider")) Apply();
    }

    [MenuItem("Tools/Desert VR/Restore Woman Refined Sled")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Demo.unity")
            scene = EditorSceneManager.OpenScene("Assets/Scenes/Demo.unity");
        var old = GameObject.Find("Sled_Sandal_Rider");
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Woman_Refined_Sled.glb");
        if (!old || !asset) throw new InvalidOperationException("Missing existing rider or requested GLB.");
        var parent = old.transform.parent;
        var position = old.transform.localPosition;
        var rotation = old.transform.localRotation;
        var scale = old.transform.localScale;
        var replacement = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
        replacement.name = "Woman_Refined_Sled";
        replacement.transform.SetParent(parent, false);
        replacement.transform.localPosition = position;
        replacement.transform.localRotation = rotation;
        replacement.transform.localScale = scale;
        // Keep helper attachments (including the tow hitch) at their existing local transforms.
        for (int i = old.transform.childCount - 1; i >= 0; --i)
        {
            var child = old.transform.GetChild(i);
            if (!PrefabUtility.IsAddedGameObjectOverride(child.gameObject)) continue;
            child.SetParent(replacement.transform, false);
        }
        UnityEngine.Object.DestroyImmediate(old);
        foreach (var body in replacement.GetComponentsInChildren<Rigidbody>(true))
        {
            body.useGravity = false;
            body.isKinematic = true;
        }
        foreach (var animator in replacement.GetComponentsInChildren<Animator>(true))
            animator.applyRootMotion = false;
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
        Debug.Log("RESTORED_REQUESTED_RIDER: Assets/Woman_Refined_Sled.glb");
    }
}
