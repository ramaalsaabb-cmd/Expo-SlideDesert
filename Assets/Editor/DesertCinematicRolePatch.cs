using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

/// <summary>Patches the already authored Timelines without rebuilding or moving the environment.</summary>
public static class DesertCinematicRolePatch
{
    const string Version = "mufleh-saqeera-white-thobe-camera-v4-behind";
    static bool patching;
    [InitializeOnLoadMethod]
    static void Initialize() { EditorApplication.delayCall += Patch; }
    [MenuItem("Tools/Desert VR/Update Selected Camera and White Thobe Role")]
    public static void Patch()
    {
        if(patching || EditorApplication.isPlayingOrWillChangePlaymode) return;
        for(int i=0;i<SceneManager.sceneCount;i++)
        {
            var scene=SceneManager.GetSceneAt(i);
            if(!scene.isLoaded || scene.name!="DesertOverview") continue;
            var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
            var chooser=all.Select(x=>x.GetComponent<DesertCinematicSequence>()).FirstOrDefault(x=>x);
            if(!chooser || !chooser.muflehTimeline || !chooser.saqeeraTimeline) return;
            EnsureCamera(scene, chooser);
            all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
            if(chooser.configurationVersion==Version || chooser.configurationVersion.EndsWith("-local-motion-v5")) return;
            patching=true;
            try
            {
                Transform white=all.First(x=>x.name=="White Thobe"),thobe=all.First(x=>x.name=="Thobe");
                if(white.parent!=thobe.parent) throw new Exception("White Thobe and Thobe have different parents; route conversion is required before swapping.");
                var protectedObjects=all.Where(x=>x.name=="CarRoot"||x.name=="SledRoot"||(x.name=="Tent"&&!x.parent)).ToArray();
                var positions=protectedObjects.Select(x=>x.localPosition).ToArray();var rotations=protectedObjects.Select(x=>x.localRotation).ToArray();var scales=protectedObjects.Select(x=>x.localScale).ToArray();
                PatchDirector(chooser.muflehTimeline,"Desert Robe",all,chooser.cinematicCamera);
                PatchDirector(chooser.saqeeraTimeline,"Traditional Dress",all,chooser.cinematicCamera);
                // Save a useful initial Game view, instead of leaving the rig at its old overview pose.
                var previewDirector=DesertCharacterSelection.MuflehSelected?chooser.muflehTimeline:chooser.saqeeraTimeline;
                var previewTimeline=(TimelineAsset)previewDirector.playableAsset;
                var previewTrack=previewTimeline.GetRootTracks().OfType<AnimationTrack>().First(x=>x.name.StartsWith("CAMERA"));
                var previewClip=((AnimationPlayableAsset)previewTrack.GetClips().Single().asset).clip;
                var previewRig=(Animator)previewDirector.GetGenericBinding(previewTrack);
                previewClip.SampleAnimation(previewRig.gameObject,0f);
                chooser.cinematicCamera.transform.localRotation=Quaternion.identity;
                EditorUtility.SetDirty(previewRig.transform);
                for(int j=0;j<protectedObjects.Length;j++)
                    if(protectedObjects[j].localPosition!=positions[j]||protectedObjects[j].localRotation!=rotations[j]||protectedObjects[j].localScale!=scales[j]) throw new Exception("Protected layout changed; scene not saved.");
                chooser.configurationVersion=Version;EditorUtility.SetDirty(chooser);
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                File.AppendAllText("Assets/Cinematics/BuildReport.txt", "\nUPDATED v2: White Thobe is the car-door walker. Camera tracks follow the selected character's actual route from a smooth three-quarter trailing view. Protected layout verified unchanged.\n");
                Debug.Log("Updated both Timelines: White Thobe walks from the car door; camera follows only selected Mufleh/Saqeera. Car, sled and tent layout unchanged.",chooser);
            }
            catch(Exception ex){Debug.LogError("Cinematic role/camera update incomplete: "+ex);}
            finally{patching=false;}
            return;
        }
    }
    static void EnsureCamera(Scene scene, DesertCinematicSequence chooser)
    {
        var rig = scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).FirstOrDefault(x=>x.name=="XR Origin - Desert Overview");
        if(!rig)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cinematics/OverviewXRCamera.prefab");
            if(!prefab) throw new Exception("Overview XR camera prefab is missing.");
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
            Undo.RegisterCreatedObjectUndo(instance,"Restore cinematic XR camera");
            rig=instance.transform;
        }
        var camera=rig.GetComponentInChildren<Camera>(true);
        if(!camera) throw new Exception("Restored XR rig has no Camera component.");
        rig.gameObject.SetActive(true);
        camera.gameObject.SetActive(true);
        camera.enabled=true;
        camera.targetDisplay=0;
        camera.targetTexture=null;
        camera.tag="MainCamera";
        var animator=rig.parent && rig.parent.name=="Overview Camera Timeline Motion" ? rig.parent.GetComponent<Animator>() : rig.GetComponent<Animator>();
        if(!animator) animator=Undo.AddComponent<Animator>(rig.gameObject);
        animator.enabled=true;
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        chooser.cinematicCamera=camera;
        chooser.characterAnimators=(chooser.characterAnimators??new Animator[0]).Where(x=>x).Append(animator).Distinct().ToArray();
        foreach(var director in new[]{chooser.muflehTimeline,chooser.saqeeraTimeline})
        {
            if(!(director.playableAsset is TimelineAsset timeline)) continue;
            foreach(var track in timeline.GetRootTracks().OfType<AnimationTrack>().Where(x=>x.name.StartsWith("CAMERA")))
            {
                director.SetGenericBinding(track,animator);
                if(director.GetGenericBinding(track)!=animator) throw new Exception("Camera binding repair failed.");
            }
            EditorUtility.SetDirty(director);
        }
        EditorUtility.SetDirty(chooser);
        EditorUtility.SetDirty(camera);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
    static AnimationTrack Body(GroupTrack group)=>group.GetChildTracks().OfType<AnimationTrack>().First(x=>x.name.StartsWith("Body"));
    static AnimationTrack Route(GroupTrack group)=>group.GetChildTracks().OfType<AnimationTrack>().First(x=>x.name.StartsWith("Position"));
    static void Swap(PlayableDirector director,TrackAsset a,TrackAsset b)
    {var value=director.GetGenericBinding(a);director.SetGenericBinding(a,director.GetGenericBinding(b));director.SetGenericBinding(b,value);}
    static void PatchDirector(PlayableDirector director,string selectedName,Transform[] all,Camera camera)
    {
        var timeline=director.playableAsset as TimelineAsset;if(!timeline)throw new Exception("Timeline asset is missing.");
        var groups=timeline.GetRootTracks().OfType<GroupTrack>().ToArray();
        var white=groups.First(x=>x.name=="White Thobe");var thobe=groups.First(x=>x.name=="Thobe");
        if(Body(thobe).GetClips().Count()>1 && Body(white).GetClips().Count()==1)
        {
            Swap(director,Body(white),Body(thobe));Swap(director,Route(white),Route(thobe));
            white.name="Thobe";thobe.name="White Thobe";EditorUtility.SetDirty(white);EditorUtility.SetDirty(thobe);
        }
        var movingWhite=groups.First(x=>x.name=="White Thobe");
        var actor=director.GetGenericBinding(Body(movingWhite)) as Animator;
        if(!actor || !actor.transform.IsChildOf(all.First(x=>x.name=="White Thobe"))) throw new Exception("White Thobe pose binding is incorrect.");
        if(Body(movingWhite).GetClips().Count()<2)throw new Exception("White Thobe has no walking track.");
        var selectedGroup=groups.First(x=>x.name.StartsWith(selectedName));
        var selectedRoute=Route(selectedGroup);var routeItem=selectedRoute.GetClips().Single();
        var motion=((AnimationPlayableAsset)routeItem.asset).clip;
        var selectedAnimator=director.GetGenericBinding(selectedRoute) as Animator;
        if(!selectedAnimator)throw new Exception("Selected character route binding is missing.");
        var cameraTrack=timeline.GetRootTracks().OfType<AnimationTrack>().First(x=>x.name.StartsWith("CAMERA"));
        var rig=all.First(x=>x.name=="XR Origin - Desert Overview");
        var rigAnimator=rig.parent && rig.parent.name=="Overview Camera Timeline Motion" ? rig.parent.GetComponent<Animator>() : rig.GetComponent<Animator>();if(!rigAnimator)throw new Exception("Camera rig Animator is missing.");
        director.SetGenericBinding(cameraTrack,rigAnimator);
        var cameraItem=cameraTrack.GetClips().Single();var cameraClip=((AnimationPlayableAsset)cameraItem.asset).clip;
        var routePath=AnimationUtility.GetCurveBindings(motion).First(x=>x.propertyName=="m_LocalPosition.x").path;
        var selectedTransform=routePath.Length>0?selectedAnimator.transform.Find(routePath):selectedAnimator.transform;
        BakeCamera(cameraClip,motion,routeItem,selectedTransform,rig,cameraItem.duration,camera);
        cameraTrack.name=selectedName=="Desert Robe"?"CAMERA - Follow Mufleh":"CAMERA - Follow Saqeera";
        EditorUtility.SetDirty(cameraClip);EditorUtility.SetDirty(cameraTrack);EditorUtility.SetDirty(timeline);EditorUtility.SetDirty(director);
    }
    static AnimationCurve Curve(AnimationClip clip,string property)
    {
        var binding=AnimationUtility.GetCurveBindings(clip).First(x=>x.propertyName==property);
        return AnimationUtility.GetEditorCurve(clip,binding);
    }
    static void BakeCamera(AnimationClip cameraClip,AnimationClip route,TimelineClip routeItem,Transform selected,Transform rig,double duration,Camera camera)
    {
        var x=Curve(route,"m_LocalPosition.x");var y=Curve(route,"m_LocalPosition.y");var z=Curve(route,"m_LocalPosition.z");
        Func<double,Vector3> at=time=>
        {
            float t=(float)(Math.Max(0,time-routeItem.start)*routeItem.timeScale+routeItem.clipIn);
            var local=new Vector3(x.Evaluate(t),y.Evaluate(t),z.Evaluate(t));
            return selected.parent?selected.parent.TransformPoint(local):local;
        };
        var qx=Curve(route,"m_LocalRotation.x");var qy=Curve(route,"m_LocalRotation.y");
        var qz=Curve(route,"m_LocalRotation.z");var qw=Curve(route,"m_LocalRotation.w");
        Func<double,Vector3> facing=time=>
        {
            float t=(float)(Math.Max(0,time-routeItem.start)*routeItem.timeScale+routeItem.clipIn);
            var rotation=new Quaternion(qx.Evaluate(t),qy.Evaluate(t),qz.Evaluate(t),qw.Evaluate(t));
            if(selected.parent)rotation=selected.parent.rotation*rotation;
            var direction=Vector3.ProjectOnPlane(rotation*Vector3.forward,Vector3.up).normalized;
            return direction.sqrMagnitude>0.1f?direction:Vector3.forward;
        };
        var curves=Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray();
        Vector3 forward=facing(0);
        Vector3 current=at(0)-forward*2.6f;
        Quaternion currentRotation=Quaternion.LookRotation(forward,Vector3.up);
        float eyeHeight=camera?Mathf.Abs(camera.transform.position.y-rig.position.y):1.65f;
        eyeHeight=Mathf.Clamp(eyeHeight,1.2f,1.9f);
        int frames=Mathf.CeilToInt((float)duration*30);
        for(int i=0;i<=frames;i++)
        {
            float time=Mathf.Min((float)duration,i/30f);var target=at(time);
            forward=facing(time);
            var desired=target-forward*2.6f;
            current=Vector3.Lerp(current,desired,i==0?1f:1-Mathf.Exp(-5f/30f));
            Vector3 aim=target+Vector3.up*1.05f-(current+Vector3.up*eyeHeight);
            var rotation=Quaternion.LookRotation(aim.normalized,Vector3.up);
            currentRotation=i==0?rotation:Quaternion.Slerp(currentRotation,rotation,1-Mathf.Exp(-5f/30f));
            Vector3 local=rig.parent?rig.parent.InverseTransformPoint(current):current;
            Quaternion localRotation=rig.parent?Quaternion.Inverse(rig.parent.rotation)*currentRotation:currentRotation;
            float[] values={local.x,local.y,local.z,localRotation.x,localRotation.y,localRotation.z,localRotation.w};
            for(int j=0;j<7;j++)curves[j].AddKey(time,values[j]);
        }
        var cameraPath=AnimationUtility.GetCurveBindings(cameraClip).First(x=>x.propertyName=="m_LocalPosition.x").path;
        foreach(var binding in AnimationUtility.GetCurveBindings(cameraClip)) AnimationUtility.SetEditorCurve(cameraClip,binding,null);
        string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
        for(int j=0;j<7;j++)
        {
            for(int i=0;i<curves[j].length;i++){AnimationUtility.SetKeyLeftTangentMode(curves[j],i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curves[j],i,AnimationUtility.TangentMode.Linear);}
            AnimationUtility.SetEditorCurve(cameraClip,EditorCurveBinding.FloatCurve(cameraPath,typeof(Transform),properties[j]),curves[j]);
        }
        cameraClip.EnsureQuaternionContinuity();
    }
}
