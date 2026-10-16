using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

public static class DesertSelectedCameraSetup
{
    static bool captureRequested;
    [InitializeOnLoadMethod]
    static void Initialize()
    {
        EditorApplication.delayCall+=Apply;
        EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Apply;};
    }
    [MenuItem("Tools/Desert VR/Use Current Camera Angle")]
    public static void CaptureCurrentAngle()
    {
        foreach(var chooser in Object.FindObjectsByType<DesertCinematicSequence>())
            foreach(var director in new[]{chooser.muflehTimeline,chooser.saqeeraTimeline})
                if(director&&director.playableAsset is TimelineAsset timeline)
                    foreach(var track in timeline.GetRootTracks().OfType<DesertSelectedCameraTrack>())
                        foreach(var clip in track.GetClips())
                            if(clip.asset is DesertSelectedCameraClip asset){asset.usePlacedAngle=false;EditorUtility.SetDirty(asset);}
        captureRequested=true;try{Apply();}finally{captureRequested=false;}
    }
    [MenuItem("Tools/Desert VR/Follow Selected Character Camera")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        for(int i=0;i<SceneManager.sceneCount;i++)
        {
            var scene=SceneManager.GetSceneAt(i);if(!scene.isLoaded||scene.name!="DesertOverview")continue;
            var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
            var chooser=all.Select(x=>x.GetComponent<DesertCinematicSequence>()).FirstOrDefault(x=>x);
            if(!chooser||!chooser.cinematicCamera)return;
            var rig=all.FirstOrDefault(x=>x.name=="XR Origin - Desert Overview");if(!rig)return;
            var directors=new[]{chooser.muflehTimeline,chooser.saqeeraTimeline};
            for(int branch=0;branch<2;branch++)
            {
                var director=directors[branch];if(!director||!(director.playableAsset is TimelineAsset timeline))continue;
                var target=all.FirstOrDefault(x=>x.name==(branch==0?"Desert Robe":"Traditional Dress"));if(!target)continue;
                foreach(var old in timeline.GetRootTracks().OfType<AnimationTrack>().Where(x=>x.name.StartsWith("CAMERA"))){old.muted=true;EditorUtility.SetDirty(old);}
                var track=timeline.GetRootTracks().OfType<DesertSelectedCameraTrack>().FirstOrDefault();
                if(!track)track=timeline.CreateTrack<DesertSelectedCameraTrack>(null,branch==0?"CAMERA - Actual Mufleh Follow":"CAMERA - Actual Saqeera Follow");
                var clip=track.GetClips().FirstOrDefault()??track.CreateClip<DesertSelectedCameraClip>();
                clip.start=0;clip.duration=timeline.duration;
                var asset=(DesertSelectedCameraClip)clip.asset;
                asset.selectedCharacter.exposedName=branch==0?"MuflehActualFollow":"SaqeeraActualFollow";
                asset.cameraRig.exposedName="ActualFollowCameraRig";
                asset.distance=2.6f;
                if(captureRequested)
                {
                    Vector3 start=target.position;
                    var group=timeline.GetRootTracks().OfType<GroupTrack>().FirstOrDefault(g=>g.name.StartsWith(target.name));
                    var motion=group?group.GetChildTracks().OfType<AnimationTrack>().FirstOrDefault(t=>t.name.StartsWith("Position")):null;
                    if(motion)
                    {
                        var animation=((AnimationPlayableAsset)motion.GetClips().First().asset).clip;
                        var bindings=AnimationUtility.GetCurveBindings(animation);
                        var animator=director.GetGenericBinding(motion) as Animator;
                        var binding=bindings.First(b=>b.propertyName=="m_LocalPosition.x");
                        var child=animator?animator.transform.Find(binding.path):null;
                        if(child)
                        {
                            System.Func<string,float> at=n=>AnimationUtility.GetEditorCurve(animation,bindings.First(b=>b.propertyName==n)).Evaluate(0);
                            start=child.parent.TransformPoint(new Vector3(at("m_LocalPosition.x"),at("m_LocalPosition.y"),at("m_LocalPosition.z")));
                        }
                    }
                    asset.placedRigOffset=rig.position-start;
                    asset.placedRigRotation=rig.rotation;
                    asset.placedEyeOffset=chooser.cinematicCamera.transform.position-start;
                    asset.hasPlacedEyeOffset=true;
                    asset.usePlacedAngle=true;
                    Debug.Log("Saved user-placed camera angle for "+target.name+": rig rotation "+rig.eulerAngles+"; follow keeps this angle.",chooser.cinematicCamera);
                }
                director.SetReferenceValue(asset.selectedCharacter.exposedName,target);
                director.SetReferenceValue(asset.cameraRig.exposedName,rig);
                director.SetGenericBinding(track,chooser.cinematicCamera);
                EditorUtility.SetDirty(asset);EditorUtility.SetDirty(track);EditorUtility.SetDirty(timeline);EditorUtility.SetDirty(director);
            }
            var selected=DesertCharacterSelection.MuflehSelected?chooser.muflehTimeline:chooser.saqeeraTimeline;
            // Route authoring owns initial evaluation; do not overwrite manually placed character starts here.
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("Camera follows the actual selected character through a Timeline follow track. Old baked camera tracks muted. Environment layout unchanged.",chooser);
        }
    }
}

