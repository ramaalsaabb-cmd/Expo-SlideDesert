using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

/// <summary>Uses ordinary child-transform Timeline curves, avoiding Animator root-motion conversion.</summary>
public static class DesertCinematicLocalMotionFix
{
    const string Version="mufleh-saqeera-white-thobe-camera-v4-behind-local-motion-v5";
    static bool busy;
    [InitializeOnLoadMethod]
    static void Initialize()
    {
        EditorApplication.delayCall+=Repair;
        EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Repair;};
    }
    [MenuItem("Tools/Desert VR/Fix Cinematic Positions and Starting Preview")]
    public static void Repair()
    {
        if(busy||EditorApplication.isPlayingOrWillChangePlaymode)return;
        var chooser=FindChooser();if(!chooser||!chooser.muflehTimeline||!chooser.saqeeraTimeline)return;
        if(chooser.configurationVersion==Version)return;
        busy=true;
        try
        {
            if(chooser.configurationVersion!=Version)
            {
                foreach(var director in new[]{chooser.muflehTimeline,chooser.saqeeraTimeline})
                {
                    var timeline=(TimelineAsset)director.playableAsset;
                    foreach(var track in timeline.GetOutputTracks().OfType<AnimationTrack>())
                    {
                        if(track.muted || !(track.name.StartsWith("Position")||track.name.StartsWith("CAMERA")))continue;
                        var old=director.GetGenericBinding(track) as Animator;if(!old)throw new Exception("Missing movement binding: "+track.name);
                        var clip=((AnimationPlayableAsset)track.GetClips().Single().asset).clip;
                        var sampleBindings=AnimationUtility.GetCurveBindings(clip);
                        if(sampleBindings.Length==0)throw new Exception("Movement clip has no curves: "+clip.name);
                        Transform target;
                        if(sampleBindings[0].path.Length>0)
                        {
                            target=old.transform.Find(sampleBindings[0].path);
                            if(!target)throw new Exception("Animated child was not found: "+sampleBindings[0].path);
                        }
                        else
                        {
                            target=old.transform;
                            var parent=target.parent;
                            string name=track.name.StartsWith("CAMERA")?"Overview Camera Timeline Motion":target.name+" Timeline Motion";
                            var driver=parent?parent.Find(name):target.gameObject.scene.GetRootGameObjects().Select(x=>x.transform).FirstOrDefault(x=>x.name==name);
                            if(!driver)
                            {
                                var obj=new GameObject(name);Undo.RegisterCreatedObjectUndo(obj,"Create Timeline transform driver");
                                SceneManager.MoveGameObjectToScene(obj,target.gameObject.scene);
                                driver=obj.transform;driver.SetParent(parent,false);
                            }
                            Undo.SetTransformParent(target,driver,"Bind ordinary child-transform animation");
                            var bound=driver.GetComponent<Animator>();if(!bound)bound=Undo.AddComponent<Animator>(driver.gameObject);
                            bound.enabled=true;bound.applyRootMotion=false;bound.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                            old.enabled=false;
                            var pairs=sampleBindings.Select(b=>(binding:b,curve:AnimationUtility.GetEditorCurve(clip,b))).ToArray();
                            foreach(var pair in pairs)AnimationUtility.SetEditorCurve(clip,pair.binding,null);
                            foreach(var pair in pairs)
                            {
                                var binding=pair.binding;binding.path=target.name;
                                AnimationUtility.SetEditorCurve(clip,binding,pair.curve);
                            }
                            clip.EnsureQuaternionContinuity();EditorUtility.SetDirty(clip);
                            director.SetGenericBinding(track,bound);
                        }
                        track.trackOffset=TrackOffset.ApplyTransformOffsets;
                        foreach(var item in track.GetClips())
                        {
                            var playable=(AnimationPlayableAsset)item.asset;playable.removeStartOffset=false;playable.applyFootIK=false;
                            EditorUtility.SetDirty(playable);
                        }
                        EditorUtility.SetDirty(track);EditorUtility.SetDirty(director);
                    }
                    EditorUtility.SetDirty(timeline);
                }
                chooser.characterAnimators=new[]{chooser.muflehTimeline,chooser.saqeeraTimeline}
                    .SelectMany(d=>((TimelineAsset)d.playableAsset).GetOutputTracks().OfType<AnimationTrack>().Select(t=>d.GetGenericBinding(t) as Animator))
                    .Where(x=>x).Distinct().ToArray();
                foreach(var animator in chooser.characterAnimators){animator.enabled=true;animator.applyRootMotion=false;}
                chooser.configurationVersion=Version;EditorUtility.SetDirty(chooser);AssetDatabase.SaveAssets();
            }
            ApplyPreview(chooser);
            EditorSceneManager.MarkSceneDirty(chooser.gameObject.scene);EditorSceneManager.SaveScene(chooser.gameObject.scene);
            Debug.Log("Cinematic movement repaired: ordinary child-transform curves; starting character and camera arrangement saved. Car, sled and tent unchanged.",chooser);
        }
        catch(Exception ex){Debug.LogError("Cinematic position repair incomplete: "+ex);}
        finally{busy=false;}
    }
    [MenuItem("Tools/Desert VR/Preview Cinematic Starting Positions")]
    public static void PreviewStart()
    {
        if(busy||EditorApplication.isPlayingOrWillChangePlaymode)return;
        var chooser=FindChooser();if(!chooser)return;
        ApplyPreview(chooser);EditorSceneManager.MarkSceneDirty(chooser.gameObject.scene);SceneView.RepaintAll();
    }
    static DesertCinematicSequence FindChooser()
    {
        for(int i=0;i<SceneManager.sceneCount;i++)
        {
            var scene=SceneManager.GetSceneAt(i);if(!scene.isLoaded||scene.name!="DesertOverview")continue;
            return scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<DesertCinematicSequence>(true)).FirstOrDefault();
        }
        return null;
    }
    static void ApplyPreview(DesertCinematicSequence chooser)
    {
        var director=DesertCharacterSelection.MuflehSelected?chooser.muflehTimeline:chooser.saqeeraTimeline;
        if(!director||!director.playableAsset)return;
        foreach(var animator in chooser.characterAnimators)if(animator)animator.enabled=true;
        director.time=0;director.RebuildGraph();director.Evaluate();director.Pause();
        // Assert Timeline evaluation matches the authored local position curves for every mover/camera.
        foreach(var track in ((TimelineAsset)director.playableAsset).GetOutputTracks().OfType<AnimationTrack>())
        {
            if(track.muted || !(track.name.StartsWith("Position")||track.name.StartsWith("CAMERA")))continue;
            var animator=director.GetGenericBinding(track) as Animator;
            var item=track.GetClips().Single();var clip=((AnimationPlayableAsset)item.asset).clip;
            var bindings=AnimationUtility.GetCurveBindings(clip);var binding=bindings.First(x=>x.propertyName=="m_LocalPosition.x");
            if(binding.path.Length==0)throw new Exception("Movement is still bound to an Animator root: "+track.name);
            var child=animator.transform.Find(binding.path);if(!child)throw new Exception("Missing animated child: "+binding.path);
            float t=(float)item.clipIn;
            Func<string,float> value=property=>AnimationUtility.GetEditorCurve(clip,bindings.First(x=>x.propertyName==property)).Evaluate(t);
            var expected=new Vector3(value("m_LocalPosition.x"),value("m_LocalPosition.y"),value("m_LocalPosition.z"));
            if(Vector3.Distance(child.localPosition,expected)>0.03f)throw new Exception("Timeline preview position differs from authored curves: "+track.name);
            EditorUtility.SetDirty(child);
        }
        if(chooser.cinematicCamera){chooser.cinematicCamera.enabled=true;chooser.cinematicCamera.gameObject.SetActive(true);}
        SceneView.RepaintAll();
    }
}
