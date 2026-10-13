using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class DesertOverviewSetup
{
    const string Destination = "Assets/Scenes/DesertOverview.unity";
    static DesertOverviewSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode
                || System.IO.File.Exists("Library/DesertOverviewSetup.complete")) return;
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) return;
            try { Create(); }
            catch (Exception exception) { Debug.LogException(exception); }
        };
    }
    [MenuItem("Tools/Desert VR/Create Objects Only Overview")]
    public static void Create()
    {
        var source = EditorSceneManager.OpenScene("Assets/Scenes/Demo.unity", OpenSceneMode.Single);
        if (!UnityEngine.Object.FindFirstObjectByType<DesertSceneTransition>())
            new GameObject("End Of Route Transition").AddComponent<DesertSceneTransition>();
        EditorSceneManager.SaveScene(source);
        EditorSceneManager.SaveScene(source, Destination, true);
        var scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var prefab in root.GetComponentsInChildren<Transform>(true).Where(t => PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)).ToArray())
                if (prefab && PrefabUtility.IsOutermostPrefabInstanceRoot(prefab.gameObject)) PrefabUtility.UnpackPrefabInstance(prefab.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        if (!AssetDatabase.IsValidFolder("Assets/Scenes/DesertOverviewMeshes")) AssetDatabase.CreateFolder("Assets/Scenes", "DesertOverviewMeshes");
        var environment = UnityEngine.Object.FindFirstObjectByType<ThumamahEnvironment>();
        if (environment)
        {
            environment.Rebuild();
            var generated = environment.transform.Find("Thumamah - generated mountains and dunes");
            if (generated)
            {
                var baked = UnityEngine.Object.Instantiate(generated.gameObject, environment.transform);
                baked.name = "Static Desert Mountains And Dunes";
                PersistMeshes(baked);
            }
            UnityEngine.Object.DestroyImmediate(environment);
        }
        foreach (var rope in UnityEngine.Object.FindObjectsByType<FlexibleTowRope>(FindObjectsSortMode.None))
        {
            rope.Initialize();
            PersistMeshes(rope.gameObject);
            UnityEngine.Object.DestroyImmediate(rope);
        }
        var camera = Camera.main;
        if (!camera) throw new Exception("Source camera missing");
        var view = new GameObject("Overview Camera");
        view.tag = "MainCamera";
        var newCamera = view.AddComponent<Camera>();
        EditorUtility.CopySerialized(camera, newCamera);
        foreach (var component in camera.GetComponents<MonoBehaviour>())
        {
            if (!component) continue;
            var type = component.GetType();
            if (type.Name == "TrackedPoseDriver" || type.Name == "UniversalAdditionalCameraData")
                EditorUtility.CopySerialized(component, view.AddComponent(type));
        }
        view.AddComponent<AudioListener>();
        var car = UnityEngine.Object.FindFirstObjectByType<CarPathFollower>();
        var sled = UnityEngine.Object.FindFirstObjectByType<SledFollowCar>();
        if (!car || !sled) throw new Exception("Car or sled missing");
        Bounds bounds = new Bounds(car.transform.position, Vector3.zero);
        foreach (var renderer in car.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
        foreach (var renderer in sled.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
        Vector3 center = bounds.center;
        Vector3 side = car.transform.right;
        float distance = Mathf.Max(18f, bounds.size.magnitude * 0.85f);
        view.transform.position = center + side * distance + Vector3.up * 5f;
        view.transform.LookAt(center);
        newCamera.farClipPlane = Mathf.Max(newCamera.farClipPlane, 1500f);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (!t || t == view.transform) continue;
                string n = t.name;
                if (n == "XR Origin (XR Rig)" || n == "HardwareOutput" || n == "CarPath" || n == "SeatPoint"
                    || n.StartsWith("Rider_", StringComparison.Ordinal) || n == "Green_Dress_Woman_Seated" || n == "Supplied_Thobe_Man_Seated" || n == "Woman_ROOT" || n == "Man_ROOT"
                    || n.IndexOf("Character", StringComparison.OrdinalIgnoreCase) >= 0)
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (script && script.gameObject != view) UnityEngine.Object.DestroyImmediate(script);
            foreach (var animator in root.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(animator);
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true)) if (ps) UnityEngine.Object.DestroyImmediate(ps.gameObject);
        }
        foreach (var root in scene.GetRootGameObjects()) PersistMeshes(root);
        EditorSceneManager.SaveScene(scene);
        var builds = EditorBuildSettings.scenes.Where(s => s.path != Destination).ToList();
        builds.Add(new EditorBuildSettingsScene(Destination, true));
        EditorBuildSettings.scenes = builds.ToArray();
        AssetDatabase.SaveAssets();
        System.IO.File.WriteAllText("Library/DesertOverviewSetup.complete", Destination);
        Debug.Log("DESERT_OVERVIEW_CREATED " + Destination);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) Debug.Log("OVERVIEW_RENDERER " + r.name);
        EditorSceneManager.OpenScene("Assets/Scenes/Demo.unity");
    }
    static void PersistMeshes(GameObject root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.None;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!filter.sharedMesh || AssetDatabase.Contains(filter.sharedMesh)) continue;
            var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
            mesh.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/DesertOverviewMeshes/Mesh.asset"));
            filter.sharedMesh = mesh;
        }
    }
}


