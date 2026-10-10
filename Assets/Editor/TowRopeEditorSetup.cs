using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class TowRopeEditorSetup
{
    static TowRopeEditorSetup()
    {
        EditorApplication.delayCall+=Apply;
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state==PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall+=Apply;
        };
    }
    [MenuItem("Tools/Desert VR/Connect Flexible Tow Rope")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach(var follow in Object.FindObjectsByType<SledFollowCar>())
        {
            if(follow.gameObject.scene.path!="Assets/Scenes/Demo.unity") continue;
            var existing = follow.GetComponentInChildren<FlexibleTowRope>();
            if (existing)
            {
                if (existing.sizingVersion < 1)
                {
                    Undo.RecordObjects(new Object[] { existing, follow }, "Resize tow rope");
                    TowRopeSetup.ApplySize(existing, follow);
                    EditorUtility.SetDirty(existing);
                    EditorUtility.SetDirty(follow);
                    EditorSceneManager.MarkSceneDirty(follow.gameObject.scene);
                }
                continue;
            }
            var rope=TowRopeSetup.Configure(follow);
            if(!rope) continue;
            Undo.RegisterCreatedObjectUndo(rope.gameObject,"Connect flexible tow rope");
            EditorSceneManager.MarkSceneDirty(follow.gameObject.scene);
            Debug.Log("[Tow Rope] Connected car bumper to molded sled front.",rope);
        }
    }
}
