using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

public static class DesertCharacterStartingPositions
{
    const string Version="white-approved-seat-v14";
    [InitializeOnLoadMethod]
    static void Initialize(){EditorApplication.delayCall+=Apply;EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Apply;};}
    public static void BatchRepair()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/DesertOverview.unity");
        Apply();
        var sequence=UnityEngine.Object.FindAnyObjectByType<DesertCinematicSequence>();
        if(!sequence||sequence.characterPlacementVersion!=Version)throw new Exception("Route repair did not complete. See earlier diagnostic.");
    }
    [MenuItem("Tools/Desert VR/Place White Thobe at Door and Saqeera at Sled")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        for(int sceneIndex=0;sceneIndex<SceneManager.sceneCount;sceneIndex++)
        {
            var scene=SceneManager.GetSceneAt(sceneIndex);if(!scene.isLoaded||scene.name!="DesertOverview")continue;
            var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
            var chooser=all.Select(x=>x.GetComponent<DesertCinematicSequence>()).FirstOrDefault(x=>x);
            if(!chooser||!chooser.muflehTimeline||!chooser.saqeeraTimeline||chooser.characterPlacementVersion==Version)return;
            try
            {
                var car=all.First(x=>x.name=="CarRoot");var sled=all.First(x=>x.name=="Sled_ROOT");
                var tent=all.First(x=>x.name=="Tent"&&!x.parent);
                var protectedObjects=all.Where(x=>x.name=="CarRoot"||x.name=="SledRoot"||(x.name=="Tent"&&!x.parent)).ToArray();
                var positions=protectedObjects.Select(x=>x.localPosition).ToArray();var rotations=protectedObjects.Select(x=>x.localRotation).ToArray();var scales=protectedObjects.Select(x=>x.localScale).ToArray();
                // Read the user's actual scene placements before evaluating any Timeline.
                var white=all.Single(x=>x.name=="White Thobe");
                var woman=all.Single(x=>x.name=="Traditional Dress");
                var man=all.Single(x=>x.name=="Desert Robe");
                var whiteStart=white.position;var womanStart=woman.position;
                // The inactive selection is seated in the preview. Both selectable protagonists share the user-placed departure spot.
                var manStart=DesertCharacterSelection.MuflehSelected?man.position:womanStart;
                RepairMovementBindings(chooser.muflehTimeline);
                RepairMovementBindings(chooser.saqeeraTimeline);
                chooser.characterAnimators=new[]{chooser.muflehTimeline,chooser.saqeeraTimeline}
                    .SelectMany(d=>((TimelineAsset)d.playableAsset).GetOutputTracks().OfType<AnimationTrack>().Select(t=>d.GetGenericBinding(t) as Animator))
                    .Where(x=>x).Distinct().ToArray();
                using(var routing=new DesertEntranceRouting(tent,car,sled,scene))
                {
                    routing.Bake(chooser.muflehTimeline,true);
                    routing.Bake(chooser.saqeeraTimeline,true);
                    routing.Commit();
                }
                DesertCharacterSelection.Save("Woman");
                var selected=chooser.saqeeraTimeline;
                selected.time=0;selected.RebuildGraph();selected.Evaluate();selected.Evaluate();selected.Pause();
                var timeline=(TimelineAsset)selected.playableAsset;
                var follow=timeline.GetRootTracks().OfType<DesertSelectedCameraTrack>().FirstOrDefault();
                if(follow)
                {
                    var asset=(DesertSelectedCameraClip)follow.GetClips().First().asset;
                    var saqeera=all.First(x=>x.name=="Traditional Dress");
                    selected.SetReferenceValue(asset.selectedCharacter.exposedName,saqeera);
                    selected.SetGenericBinding(follow,chooser.cinematicCamera);
                    selected.RebuildGraph();selected.time=0;selected.Evaluate();selected.Evaluate();selected.Pause();
                }
                else throw new Exception("Selected-character camera follow track has not imported yet. Run Follow Selected Character Camera first.");
                for(int i=0;i<protectedObjects.Length;i++)if(protectedObjects[i].localPosition!=positions[i]||protectedObjects[i].localRotation!=rotations[i]||protectedObjects[i].localScale!=scales[i])throw new Exception("Protected layout changed; scene not saved.");
                chooser.characterPlacementVersion=Version;EditorUtility.SetDirty(chooser);EditorUtility.SetDirty(selected);
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                Debug.Log("User character starting positions retained exactly; front entrance approach retained; final seats placed beside the existing seated group and facing the gathering. Camera follows selected character with collision clearance. Vehicle and tent layout unchanged.",chooser);
            }
            catch(Exception ex){Debug.LogError("Character start placement incomplete: "+ex);}
            return;
        }
    }
    // Role changes can leave the other character's driver bound to this track.
    // Resolve the real scene slot and bind its immediate parent, preserving its world transform.
    static void RepairMovementBindings(UnityEngine.Playables.PlayableDirector director)
    {
        var timeline=(TimelineAsset)director.playableAsset;
        var slots=director.gameObject.scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
        string[] names={"White Thobe","Thobe","Green Embroidered Dress","Desert Robe","Ornate Blue Kaftan","Traditional Dress"};
        foreach(var group in timeline.GetRootTracks().OfType<GroupTrack>())
        {
            var name=names.FirstOrDefault(n=>group.name==n||group.name.StartsWith(n+" "));
            if(name==null)continue;
            var target=slots.Single(x=>x.name==name);
            var track=group.GetChildTracks().OfType<AnimationTrack>().First(x=>x.name.StartsWith("Position"));
            var driver=target.parent;
            if(!driver)throw new Exception("Character has no motion parent: "+name);
            var animator=driver.GetComponent<Animator>();
            if(!animator)animator=Undo.AddComponent<Animator>(driver.gameObject);
            animator.enabled=true;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var clip=((AnimationPlayableAsset)track.GetClips().Single().asset).clip;
            var pairs=AnimationUtility.GetCurveBindings(clip).Select(b=>(binding:b,curve:AnimationUtility.GetEditorCurve(clip,b))).ToArray();
            foreach(var pair in pairs)AnimationUtility.SetEditorCurve(clip,pair.binding,null);
            foreach(var pair in pairs)
            {
                var binding=pair.binding;binding.path=target.name;
                AnimationUtility.SetEditorCurve(clip,binding,pair.curve);
            }
            director.SetGenericBinding(track,animator);
            var body=group.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Body"));
            var model=target.GetComponentsInChildren<Animator>(true).FirstOrDefault(a=>a.avatar&&a.avatar.isHuman);
            if(!model)throw new Exception("Humanoid body Animator missing for "+name);
            model.enabled=true;model.applyRootMotion=false;model.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            director.SetGenericBinding(body,model);
            if(animator.transform.Find(target.name)!=target)throw new Exception("Cannot resolve motion target: "+name);
            EditorUtility.SetDirty(clip);EditorUtility.SetDirty(director);
        }
    }
    static void ShiftStart(UnityEngine.Playables.PlayableDirector director,string character,Vector3 worldStart)
    {
        var timeline=(TimelineAsset)director.playableAsset;
        var group=timeline.GetRootTracks().OfType<GroupTrack>().First(x=>x.name.StartsWith(character));
        var track=group.GetChildTracks().OfType<AnimationTrack>().First(x=>x.name.StartsWith("Position"));
        var pose=group.GetChildTracks().OfType<AnimationTrack>().First(x=>x.name.StartsWith("Body"));
        var walk=pose.GetClips().First(x=>Math.Abs(x.start-1)<0.001);
        float arrival=(float)(walk.start+walk.duration);
        var animator=(Animator)director.GetGenericBinding(track);
        var clip=((AnimationPlayableAsset)track.GetClips().Single().asset).clip;
        var bindings=AnimationUtility.GetCurveBindings(clip);
        var binding=bindings.First(x=>x.propertyName=="m_LocalPosition.x");
        var target=binding.path.Length>0?animator.transform.Find(binding.path):animator.transform;
        if(!target)throw new Exception("Movement target missing after binding repair: "+character+" / "+binding.path);
        var localStart=target.parent?target.parent.InverseTransformPoint(worldStart):worldStart;
        string[] axes={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z"};
        var curves=axes.Select(name=>AnimationUtility.GetEditorCurve(clip,bindings.First(x=>x.propertyName==name))).ToArray();
        var oldStart=new Vector3(curves[0].Evaluate(0),curves[1].Evaluate(0),curves[2].Evaluate(0));
        Vector3 difference=localStart-oldStart;
        for(int axis=0;axis<3;axis++)
        {
            var keys=curves[axis].keys;
            for(int k=0;k<keys.Length;k++)keys[k].value+=difference[axis]*(1-Mathf.Clamp01((keys[k].time-1)/Mathf.Max(.1f,arrival-1)));
            curves[axis].keys=keys;
            for(int k=0;k<keys.Length;k++){AnimationUtility.SetKeyLeftTangentMode(curves[axis],k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curves[axis],k,AnimationUtility.TangentMode.Linear);}
            AnimationUtility.SetEditorCurve(clip,bindings.First(x=>x.propertyName==axes[axis]),curves[axis]);
        }
        // Keep the authored destination and seat pose; face the beginning of the updated walk.
        Vector3 tangent=new Vector3(curves[0].Evaluate(1.3f)-curves[0].Evaluate(1f),0,curves[2].Evaluate(1.3f)-curves[2].Evaluate(1f));
        if(tangent.sqrMagnitude>0.00001f)
        {
            Quaternion startRotation=Quaternion.LookRotation(tangent.normalized,Vector3.up);
            string[] axesQ={"m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
            var q=axesQ.Select(name=>AnimationUtility.GetEditorCurve(clip,bindings.First(x=>x.propertyName==name))).ToArray();
            for(int axis=0;axis<4;axis++)
            {
                var keys=q[axis].keys;
                for(int k=0;k<keys.Length;k++)if(keys[k].time<=1.0f)keys[k].value=startRotation[axis];
                q[axis].keys=keys;AnimationUtility.SetEditorCurve(clip,bindings.First(x=>x.propertyName==axesQ[axis]),q[axis]);
            }
        }
        clip.EnsureQuaternionContinuity();EditorUtility.SetDirty(clip);EditorUtility.SetDirty(timeline);
    }
    static Bounds BoundsOf(Transform root)
    {
        bool found=false;Bounds bounds=default;
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(false))
        {
            string name=renderer.name.ToLowerInvariant();if(!renderer.enabled||name.Contains("rope")||name.Contains("hitch")||name.Contains("handle"))continue;
            if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);
        }
        if(!found)throw new Exception("Visible model geometry not found: "+root.name);return bounds;
    }
    static Vector3 Ground(Scene scene,Vector3 p)
    {
        foreach(var root in scene.GetRootGameObjects())foreach(var terrain in root.GetComponentsInChildren<Terrain>(false))
        {
            if(!terrain.terrainData)continue;var local=p-terrain.transform.position;var size=terrain.terrainData.size;
            if(local.x>=0&&local.z>=0&&local.x<=size.x&&local.z<=size.z){p.y=terrain.SampleHeight(p)+terrain.transform.position.y;return p;}
        }
        p.y=0;return p;
    }
}
