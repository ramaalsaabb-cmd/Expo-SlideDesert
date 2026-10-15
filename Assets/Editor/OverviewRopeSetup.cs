using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OverviewRopeSetup
{
    [InitializeOnLoadMethod]
    static void Initialize()
    {
        EditorApplication.delayCall += AddRope;
        EditorSceneManager.sceneOpened += (scene, mode) => AddRope();
    }
    [MenuItem("Tools/Desert VR/Add Rope Only")]
    static void AddRope()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || scene.name != "DesertOverview") continue;
            Transform car = null, sled = null, carVisual = null, sledVisual = null;
            GameObject rope = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var item in root.GetComponentsInChildren<Transform>(true))
                {
                    if (item.name.Trim().ToLowerInvariant() == "hilux") carVisual = item;
                    if (item.name == "Sled_ROOT") sledVisual = item;
                    if (item.name == "CarTowHitch") car = item;
                    if (item.name == "SledTowHitch") sled = item;
                    if (item.name == "Car To Sled Rope") rope = item.gameObject;
                }
            if (!car || !sled) continue;
            if (!rope)
            {
                rope = new GameObject("Car To Sled Rope");
                SceneManager.MoveGameObjectToScene(rope, scene);
                Undo.RegisterCreatedObjectUndo(rope, "Add car to sled rope");
            }
            var line = rope.GetComponent<LineRenderer>();
            if (!line) line = Undo.AddComponent<LineRenderer>(rope);
            rope.SetActive(true);
            line.enabled = true;
            line.forceRenderingOff = false;
            line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/OverviewTowRope.mat");
            line.useWorldSpace = true;
            line.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            line.widthMultiplier = 0.018f;
            line.startColor = line.endColor = Color.white;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            var visual = rope.GetComponent<OverviewTowRope>();
            if (!visual) visual = Undo.AddComponent<OverviewTowRope>(rope);
            visual.carHitch = car;
            visual.sledHitch = sled;
            visual.carVisual = carVisual;
            visual.sledVisual = sledVisual;
            visual.ropeMaterial = line.sharedMaterial;
            visual.thickness = 0.018f;
            visual.sag = 0.08f;
            visual.enabled = true;
            // The overview uses one static visual rope; suppress the old simulation mesh here only.
            foreach(var root in scene.GetRootGameObjects())foreach(var item in root.GetComponentsInChildren<Transform>(true))
            {
                if(item.name!="Flexible Tow Rope"||item.gameObject==rope)continue;
                foreach(var behaviour in item.GetComponents<MonoBehaviour>())behaviour.enabled=false;
                foreach(var renderer in item.GetComponents<Renderer>())renderer.enabled=false;
            }
            visual.RebuildAttachments();
            EditorUtility.SetDirty(visual);
            EditorUtility.SetDirty(line);
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log("Added visible Car To Sled Rope: " + Vector3.Distance(car.position, sled.position).ToString("F2") + " m; 33 points; existing layout unchanged.", rope);
        }
    }
}
