using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SledDustEditorSetup
{
    static SledDustEditorSetup()
    {
        EditorApplication.delayCall+=Apply;
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state==PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall+=Apply;
        };
    }
    [MenuItem("Tools/Desert VR/Add Sled Dust")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach(var follow in Object.FindObjectsByType<SledFollowCar>())
        {
            if(follow.gameObject.scene.path!="Assets/Scenes/Demo.unity" || follow.GetComponent<SledDustController>()) continue;
            var dust=Undo.AddComponent<SledDustController>(follow.gameObject);
            dust.dustMaterial=Resources.Load<Material>("SledDust");
            EditorUtility.SetDirty(dust);
            EditorSceneManager.MarkSceneDirty(follow.gameObject.scene);
        }
    }
}
