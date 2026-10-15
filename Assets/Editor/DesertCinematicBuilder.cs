using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

public class DesertCinematicAnimationImport : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith("Assets/Cinematics/Animations/")) return;
        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.name = Path.GetFileNameWithoutExtension(assetPath);
            clip.loopTime = true;
            clip.lockRootRotation = true;
            clip.lockRootPositionXZ = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionXZ = false;
            clip.keepOriginalPositionY = false;
            clip.heightFromFeet = true;
        }
        importer.clipAnimations = clips;
    }
}

/// <summary>Authors real Timeline/AnimationClip assets and scene bindings. No runtime scripted movement.</summary>
public static class DesertCinematicBuilder
{
    const string Folder = "Assets/Cinematics";
    const string Version = "mufleh-saqeera-v1";
    static bool building;
    static readonly string[] Names = { "White Thobe", "Thobe", "Green Embroidered Dress", "Desert Robe", "Ornate Blue Kaftan", "Traditional Dress" };
    const int Mufleh = 3, Saqeera = 5, Companion = 0;
    // Manual authoring only: never run asset/scene generation during reload or scene opening.
    [MenuItem("Tools/Desert VR/Build Editable Character Cinematics")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Stop Play Mode before authoring Timelines."); return; }
        var scene = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).FirstOrDefault(x => x.name == "DesertOverview" && x.isLoaded);
        if (!scene.IsValid()) scene = EditorSceneManager.OpenScene("Assets/Scenes/DesertOverview.unity", OpenSceneMode.Additive);
        BuildScene(scene);
    }
    static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).ToArray();
    static Transform Find(Scene scene, string name) => All<Transform>(scene).FirstOrDefault(x => x.name == name);
    struct Layout
    {
        public Transform target; public Vector3 position, scale; public Quaternion rotation;
        public Layout(Transform t) { target=t; position=t.localPosition; scale=t.localScale; rotation=t.localRotation; }
        public bool Unchanged => target && target.localPosition == position && target.localScale == scale && target.localRotation == rotation;
    }
    static void BuildScene(Scene scene)
    {
        if (building) return;
        building = true;
        var previousAssets = new HashSet<string>(AssetDatabase.FindAssets("",new[]{Folder}).Select(AssetDatabase.GUIDToAssetPath));
        try
        {
            var transforms = All<Transform>(scene);
            Transform car = Find(scene,"CarRoot"), sled = Find(scene,"SledRoot");
            var tent = transforms.FirstOrDefault(x => x.name == "Tent" && x.parent == null);
            if (!car || !sled || !tent) throw new Exception("Missing car, sled or tent references. Existing layout was not modified.");
            var protectedLayout = new[] {new Layout(car),new Layout(sled),new Layout(tent)};
            var carBounds = BoundsOf(car, false); var sledBounds = BoundsOf(sled, true);
            var cloth = tent.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x != tent && x.name == "Tent");
            var tentBounds = BoundsOf(cloth ? cloth : tent, false);
            // Import/reimport only the six character models and supplied animation files as humanoid.
            foreach (string name in Names)
            {
                string path = Directory.GetFiles("Assets/Resources/CharacterCollection/"+name,"*.fbx").Single();
                ConfigureHumanoid(path, false);
            }
            foreach (var name in new[]{"Walking","Male Sitting Pose","Sitting Idle"}) ConfigureHumanoid(Folder+"/Animations/"+name+".fbx",true);
            var walk = ImportedClip("Walking"); var maleSit = ImportedClip("Male Sitting Pose"); var idle = ImportedClip("Sitting Idle");
            if (!walk.isHumanMotion || !maleSit.isHumanMotion || !idle.isHumanMotion) throw new Exception("Provided animation clips did not import as valid humanoid clips. Inspect Rig / Avatar import errors.");
            var slots = new Transform[6]; var models = new Animator[6]; var motion = new Animator[6];
            for (int i=0;i<6;i++)
            {
                slots[i] = Find(scene,Names[i]);
                if (!slots[i]) throw new Exception("Missing existing character: "+Names[i]);
                var preview = slots[i].GetComponent<OverviewImportedCharacter>();
                if (preview) preview.RefreshPlacement();
                if (slots[i].childCount == 0) throw new Exception("Character model not imported yet: "+Names[i]);
                var actor = slots[i].GetChild(0);
                models[i] = actor.GetComponent<Animator>();
                if (!models[i]) models[i] = Undo.AddComponent<Animator>(actor.gameObject);
                string path=Directory.GetFiles("Assets/Resources/CharacterCollection/"+Names[i],"*.fbx").Single();
                var avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault(x=>x.isHuman && x.isValid);
                if (!avatar) throw new Exception("No valid humanoid avatar for "+Names[i]+". Check character Rig import.");
                models[i].avatar=avatar; models[i].enabled=true; models[i].applyRootMotion=false; models[i].cullingMode=AnimatorCullingMode.AlwaysAnimate;
                motion[i]=slots[i].GetComponent<Animator>();
                if (!motion[i]) motion[i]=Undo.AddComponent<Animator>(slots[i].gameObject);
                motion[i].applyRootMotion=false; motion[i].cullingMode=AnimatorCullingMode.AlwaysAnimate;
                if (preview) preview.enabled=false;
            }
            var rig=Find(scene,"XR Origin - Desert Overview");
            Camera camera=rig ? rig.GetComponentsInChildren<Camera>(true).FirstOrDefault() : All<Camera>(scene).FirstOrDefault(x=>x.isActiveAndEnabled && x.CompareTag("MainCamera"));
            if (!camera) throw new Exception("No existing cinematic camera in DesertOverview.");
            if (!rig) rig=camera.transform.parent ? camera.transform.parent : camera.transform;
            var cameraAnimator=rig.GetComponent<Animator>(); if (!cameraAnimator) cameraAnimator=Undo.AddComponent<Animator>(rig.gameObject);
            cameraAnimator.applyRootMotion=false;
            var root=Find(scene,"Character Cinematics");
            if (!root) { var obj=new GameObject("Character Cinematics"); SceneManager.MoveGameObjectToScene(obj,scene); root=obj.transform; Undo.RegisterCreatedObjectUndo(obj,"Create cinematic bindings"); }
            var chooser=root.GetComponent<DesertCinematicSequence>(); if (!chooser) chooser=Undo.AddComponent<DesertCinematicSequence>(root.gameObject);
            foreach(var existing in All<DesertOverviewScale>(scene)) existing.enabled=false;
            // Unity allows only one PlayableDirector per GameObject.
            // Each sequence owns a distinct child, and there is no retry loop.
            var directors = new[] {
                DirectorFor(root, "Mufleh Timeline"),
                DirectorFor(root, "Saqeera Timeline")
            };
            var previousRootDirector = root.GetComponent<PlayableDirector>();
            if (previousRootDirector) previousRootDirector.playOnAwake = false;
            var seats=new Vector3[6];
            float rowSpacing=Mathf.Min(2.6f,tentBounds.size.z*0.35f), sideSpacing=Mathf.Min(2.2f,tentBounds.size.x*0.22f);
            for(int i=0;i<6;i++) seats[i]=Ground(scene,tentBounds.center+new Vector3((i%3-1)*sideSpacing,0,(i/3==0?-1:1)*rowSpacing*0.5f));
            // Front is defined by the current camera side; the tent itself is never rotated.
            Vector3 towardViewer=Vector3.ProjectOnPlane(camera.transform.position-tentBounds.center,Vector3.up).normalized;
            if(towardViewer.sqrMagnitude<0.1f) towardViewer=Vector3.back;
            float radius=Mathf.Abs(towardViewer.x)*tentBounds.extents.x+Mathf.Abs(towardViewer.z)*tentBounds.extents.z;
            Vector3 entrance=Ground(scene,tentBounds.center+towardViewer*(radius-0.7f));
            Vector3 across=Vector3.Cross(Vector3.up,towardViewer).normalized;
            Vector3 selectedStart=Ground(scene,sledBounds.center+across*(Mathf.Max(sledBounds.extents.x,sledBounds.extents.z)+0.9f));
            Vector3 carStart=Ground(scene,carBounds.center+towardViewer*(Mathf.Abs(towardViewer.x)*carBounds.extents.x+Mathf.Abs(towardViewer.z)*carBounds.extents.z+0.8f));
            for(int variant=0;variant<2;variant++)
            {
                int selected=variant==0?Mufleh:Saqeera;
                string label=variant==0?"Mufleh":"Saqeera";
                var director=directors[variant]; director.playOnAwake=false; director.timeUpdateMode=DirectorUpdateMode.UnscaledGameTime; director.extrapolationMode=DirectorWrapMode.Hold;
                string timelinePath=Folder+"/"+label+" Selected.playable";
                if(AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath)) throw new Exception("Existing authored Timeline found at "+timelinePath+". It was preserved; rename it before rebuilding.");
                var timeline=ScriptableObject.CreateInstance<TimelineAsset>();timeline.name=label+" Selected";AssetDatabase.CreateAsset(timeline,timelinePath);timeline.editorSettings.frameRate=30;
                director.playableAsset=timeline;
                var paths=new Vector3[6][];var durations=new double[6];
                float speed=walk.averageSpeed.magnitude; if(speed<0.3f||speed>2f) speed=1.15f;
                for(int i=0;i<6;i++)
                {
                    if(i==selected||i==Companion)
                    {
                        var start=i==selected?selectedStart:carStart;
                        float lane=i==selected?-0.5f:0.5f;
                        paths[i]=SmoothPath(start,entrance+across*lane,seats[i]);
                        durations[i]=PathLength(paths[i])/speed;
                    }
                    else {paths[i]=new[]{seats[i],seats[i]};durations[i]=0;}
                }
                double duration=1+Math.Max(durations[selected],durations[Companion])+3.5;
                timeline.durationMode=TimelineAsset.DurationMode.FixedLength;timeline.fixedDuration=duration;
                var synchronizedWalkClips = new List<TimelineClip>();
                const double walkingStart = 1.0;
                for(int i=0;i<6;i++)
                {
                    bool moving=i==selected||i==Companion;
                    var group=timeline.CreateTrack<GroupTrack>(null,Names[i]+(i==selected?" — selected focus":""));
                    var pose=timeline.CreateTrack<AnimationTrack>(group,"Body — "+(moving?"Walk then sit":"Already seated"));pose.trackOffset=TrackOffset.ApplySceneOffsets;
                    director.SetGenericBinding(pose,models[i]);
                    AnimationClip sitting=i<2||i==Mufleh?maleSit:idle;
                    if(moving)
                    {
                        AddBody(pose,walk,0,1,"Standing start",false);
                        var walking=AddBody(pose,walk,walkingStart,durations[i],"Walk — synchronized start 1.000 s",true);
                        synchronizedWalkClips.Add(walking);
                        // Freeze the initial walk pose during the one-second establishing shot.
                        var establishing=(AnimationPlayableAsset)pose.GetClips().First().asset;
                        establishing.clip=StandingPose(walk,label+" "+Names[i]+" Standing");
                        double sitStart=1+durations[i]-0.6;
                        var sit=AddBody(pose,sitting,sitStart,duration-sitStart,"Sit / hold seated",true);
                        walking.blendOutDuration=0.6;sit.blendInDuration=0.6;
                    }
                    else AddBody(pose,sitting,0,duration,"Seated throughout",true);
                    var routeClip=MotionClip(slots[i],paths[i],durations[i],duration,seats[i],tentBounds.center,label+" "+Names[i]+" Route",moving);
                    var route=timeline.CreateTrack<AnimationTrack>(group,"Position and facing — editable curves");route.trackOffset=TrackOffset.ApplyTransformOffsets;director.SetGenericBinding(route,motion[i]);
                    var rc=route.CreateClip(routeClip);rc.start=0;rc.duration=duration;ConfigureTransformClip(rc);
                }
                var cameraClip=CameraClip(rig,paths[selected],durations[selected],duration,label+" Follow Camera");
                var cameraTrack=timeline.CreateTrack<AnimationTrack>(null,"CAMERA — follows only "+label);cameraTrack.trackOffset=TrackOffset.ApplyTransformOffsets;director.SetGenericBinding(cameraTrack,cameraAnimator);
                var cameraTimelineClip=cameraTrack.CreateClip(cameraClip);cameraTimelineClip.start=0;cameraTimelineClip.duration=duration;ConfigureTransformClip(cameraTimelineClip);
                foreach (var output in timeline.outputs)
                    if (output.sourceObject is AnimationTrack && !director.GetGenericBinding(output.sourceObject))
                        throw new Exception("Timeline binding missing: "+label+" / "+output.streamName);
                // Validate the actual created clip objects, not mutable editor display labels.
                if (synchronizedWalkClips.Count != 2 || synchronizedWalkClips.Any(x => Math.Abs(x.start - walkingStart) > 0.0001))
                    throw new Exception("Walk start validation failed: " + string.Join(", ", synchronizedWalkClips.Select(x => x.start.ToString("F6"))));
                Debug.Log(label + " Timeline: both walk clips verified at " + walkingStart.ToString("F3") + " seconds.");
                EditorUtility.SetDirty(timeline);EditorUtility.SetDirty(director);
            }
            chooser.muflehTimeline=directors[0];chooser.saqeeraTimeline=directors[1];chooser.characterAnimators=models.Concat(motion).Append(cameraAnimator).ToArray();chooser.cinematicCamera=camera;chooser.configurationVersion=Version;
            EditorUtility.SetDirty(chooser);
            if(protectedLayout.Any(x=>!x.Unchanged)) throw new Exception("Protected vehicle/tent layout changed unexpectedly; scene was NOT saved.");
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Folder+"/BuildReport.txt","COMPLETE: two Timeline assets, six pose tracks and six transform tracks per Timeline, camera Animation Track, two PlayableDirectors and serialized scene bindings.\nWalk tracks begin together at 1.000 s. Only selected Timeline plays.\nCar, sled and tent local positions / rotations / scales verified unchanged.\nAnimations: Walking (6), Male Sitting Pose, Sitting Idle. Humanoid avatars validated.\nVisual headset verification still required for feet, garment clipping and seat placement.\n");
            AssetDatabase.Refresh();Debug.Log("Desert cinematics authored and saved. See Assets/Cinematics/BuildReport.txt. Car, sled and tent layout unchanged.",chooser);
        }
        catch(Exception ex)
        {
            foreach(var path in AssetDatabase.FindAssets("",new[]{Folder}).Select(AssetDatabase.GUIDToAssetPath).ToArray())
                if(!previousAssets.Contains(path) && (path.EndsWith(".anim")||path.EndsWith(".playable"))) AssetDatabase.DeleteAsset(path);
            File.WriteAllText(Folder+"/BuildReport.txt","INCOMPLETE: "+ex.Message+"\n"+ex.StackTrace);
            Debug.LogError("Desert cinematic build incomplete: "+ex.Message+"\n"+ex.StackTrace);
        }
        finally { building=false; }
    }
    static PlayableDirector DirectorFor(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (!child)
        {
            var obj = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(obj, "Create Timeline director");
            obj.transform.SetParent(parent, false);
            child = obj.transform;
        }
        var director = child.GetComponent<PlayableDirector>();
        if (!director) director = Undo.AddComponent<PlayableDirector>(child.gameObject);
        if (!director) throw new Exception("Unable to create director on " + name);
        director.playOnAwake = false;
        return director;
    }
    static void ConfigureHumanoid(string path,bool animation)
    {
        var importer=AssetImporter.GetAtPath(path) as ModelImporter;if(!importer) throw new Exception("Missing imported FBX: "+path);
        if(importer.animationType==ModelImporterAnimationType.Human && importer.importAnimation==animation) return;
        importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=animation;importer.SaveAndReimport();
    }
    static AnimationClip ImportedClip(string name)
    {
        var clip=AssetDatabase.LoadAllAssetsAtPath(Folder+"/Animations/"+name+".fbx").OfType<AnimationClip>().FirstOrDefault(x=>!x.name.StartsWith("__preview__"));
        if(!clip) throw new Exception("Missing animation clip: "+name);return clip;
    }
    static TimelineClip AddBody(AnimationTrack track,AnimationClip clip,double start,double duration,string name,bool loop)
    {
        var item=track.CreateClip(clip);item.start=start;item.duration=Math.Max(0.05,duration);item.displayName=name;
        var asset=(AnimationPlayableAsset)item.asset;asset.applyFootIK=true;asset.removeStartOffset=true;asset.loop=loop?AnimationPlayableAsset.LoopMode.On:AnimationPlayableAsset.LoopMode.Off;
        return item;
    }
    static void ConfigureTransformClip(TimelineClip clip)
    {var asset=(AnimationPlayableAsset)clip.asset;asset.removeStartOffset=false;asset.applyFootIK=false;asset.loop=AnimationPlayableAsset.LoopMode.Off;}
    static AnimationClip StandingPose(AnimationClip walking,string name)
    {
        var clip=UnityEngine.Object.Instantiate(walking);clip.name=name;
        foreach(var binding in AnimationUtility.GetCurveBindings(clip))
        {
            var curve=AnimationUtility.GetEditorCurve(clip,binding);float value=curve.Evaluate(0f);
            AnimationUtility.SetEditorCurve(clip,binding,AnimationCurve.Constant(0f,1f,value));
        }
        AssetDatabase.CreateAsset(clip,Folder+"/"+name+".anim");return clip;
    }
    static Vector3 Ground(Scene scene,Vector3 position)
    {
        foreach(var terrain in All<Terrain>(scene))
        {if(!terrain.terrainData)continue;var local=position-terrain.transform.position;var size=terrain.terrainData.size;if(local.x>=0&&local.z>=0&&local.x<=size.x&&local.z<=size.z){position.y=terrain.SampleHeight(position)+terrain.transform.position.y;return position;}}
        position.y=0;return position;
    }
    static Bounds BoundsOf(Transform root,bool sledOnly)
    {
        if(sledOnly){var child=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x=>x.name=="Sled_ROOT");if(child)root=child;}
        bool found=false;Bounds bounds=default;
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>(false))
        {string n=r.name.ToLowerInvariant();if(!r.enabled||n.Contains("rope")||n.Contains("hitch")||n.Contains("handle"))continue;if(!found){bounds=r.bounds;found=true;}else bounds.Encapsulate(r.bounds);}
        if(!found)throw new Exception("No visible geometry bounds for "+root.name);return bounds;
    }
    static Vector3[] SmoothPath(Vector3 start,Vector3 entrance,Vector3 seat)
    {
        var points=new Vector3[161];
        // A quadratic route through the entrance, resampled by arc length for constant walking speed.
        Vector3 control=2*entrance-(start+seat)*0.5f;
        for(int i=0;i<points.Length;i++){float t=i/(float)(points.Length-1);points[i]=(1-t)*(1-t)*start+2*(1-t)*t*control+t*t*seat;}
        return points;
    }
    static float PathLength(Vector3[] points){float sum=0;for(int i=1;i<points.Length;i++)sum+=Vector3.Distance(points[i-1],points[i]);return sum;}
    static Vector3 Along(Vector3[] points,double ratio)
    {
        float remaining=PathLength(points)*Mathf.Clamp01((float)ratio);
        for(int i=1;i<points.Length;i++){float step=Vector3.Distance(points[i-1],points[i]);if(remaining<=step)return Vector3.Lerp(points[i-1],points[i],step<0.0001f?0:remaining/step);remaining-=step;}return points[points.Length-1];
    }
    static AnimationClip MotionClip(Transform slot,Vector3[] path,double walking,double duration,Vector3 seat,Vector3 centre,string name,bool moving)
    {
        return Curves(slot,name,duration,(time)=>
        {
            double ratio=moving?Math.Max(0,time-1)/Math.Max(0.01,walking):1;
            Vector3 position=Along(path,ratio);Vector3 direction;
            if(moving&&ratio<0.99)direction=Along(path,Math.Min(1,ratio+0.02))-position;else direction=centre-seat;
            direction.y=0;if(direction.sqrMagnitude<0.001f)direction=Vector3.back;
            return (position,Quaternion.LookRotation(direction,Vector3.up));
        });
    }
    static AnimationClip CameraClip(Transform rig,Vector3[] path,double walking,double duration,string name)
    {
        // Bake a smooth chase into the Timeline camera track; HMD-local tracking is preserved.
        Vector3 previous=Along(path,0)+new Vector3(0.7f,0,-3f);Quaternion previousRotation=Quaternion.identity;
        return Curves(rig,name,duration,(time)=>
        {
            double ratio=Math.Max(0,time-1)/Math.Max(0.01,walking);Vector3 target=Along(path,ratio);
            Vector3 desired=target+new Vector3(0.7f,0,-3f);
            previous=Vector3.Lerp(previous,desired,1-Mathf.Exp(-4f/30f));
            Vector3 direction=Vector3.ProjectOnPlane(target-previous,Vector3.up);
            var rotation=Quaternion.LookRotation(direction.normalized,Vector3.up);
            if(time==0)previousRotation=rotation;else previousRotation=Quaternion.Slerp(previousRotation,rotation,1-Mathf.Exp(-5f/30f));
            return (previous,previousRotation);
        });
    }
    static AnimationClip Curves(Transform target,string name,double duration,Func<double,(Vector3 position,Quaternion rotation)> sample)
    {
        var curves=Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray();
        int frames=Mathf.CeilToInt((float)duration*30);
        for(int frame=0;frame<=frames;frame++)
        {
            float time=Mathf.Min((float)duration,frame/30f);var value=sample(time);
            Vector3 local=target.parent?target.parent.InverseTransformPoint(value.position):value.position;
            Quaternion rotation=target.parent?Quaternion.Inverse(target.parent.rotation)*value.rotation:value.rotation;
            float[] values={local.x,local.y,local.z,rotation.x,rotation.y,rotation.z,rotation.w};
            for(int i=0;i<7;i++)curves[i].AddKey(new Keyframe(time,values[i]));
        }
        foreach(var curve in curves)for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}
        var clip=new AnimationClip{name=name,frameRate=30};string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
        for(int i=0;i<7;i++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Transform),properties[i]),curves[i]);
        clip.EnsureQuaternionContinuity();AssetDatabase.CreateAsset(clip,Folder+"/"+name+".anim");return clip;
    }
}
