using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
public static class DesertWhiteArrivalRepair {
 const string Marker="work/white-arrival-repaired-v1.txt";
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Once;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Once;};}
 static void Once(){if(!EditorApplication.isPlayingOrWillChangePlaymode&&!File.Exists(Marker))Apply();}
 static void Apply(){var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath("Assets/Scenes/DesertOverview.unity");bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/DesertOverview.unity",OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
 try{var seq=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DesertCinematicSequence>(true)).Single();var report=new System.Text.StringBuilder();Directory.CreateDirectory("work/WhiteArrivalBackup");
 foreach(var d in new[]{seq.muflehTimeline,seq.saqeeraTimeline}){
 var timeline=(TimelineAsset)d.playableAsset;d.time=0;d.RebuildGraph();d.Evaluate();d.Evaluate();
 var groups=timeline.GetRootTracks().OfType<GroupTrack>().ToArray();var white=groups.Single(g=>g.name.StartsWith("White Thobe"));var body=white.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Body"));var walk=body.GetClips().First(c=>c.displayName.StartsWith("Walk"));var sit=body.GetClips().First(c=>c.displayName.StartsWith("Sit"));
 var motions=groups.Select(g=>{var t=g.GetChildTracks().OfType<AnimationTrack>().First(x=>x.name.StartsWith("Position"));var clip=((AnimationPlayableAsset)t.GetClips().Single().asset).clip;var a=(Animator)d.GetGenericBinding(t);var bindings=AnimationUtility.GetCurveBindings(clip);var xyz=new[]{"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z"}.Select(p=>AnimationUtility.GetEditorCurve(clip,bindings.Single(b=>b.propertyName==p))).ToArray();return(name:g.name,matrix:a.transform.localToWorldMatrix,curves:xyz,clip:clip,bindings:bindings);}).ToArray();var wm=motions.Single(m=>m.name.StartsWith("White Thobe"));
 var source=d==seq.muflehTimeline?AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Cinematics/RepairSources/White Thobe Original Full Route.anim"):wm.clip;
 if(!source)throw new Exception("Complete route not imported");
 var sourceBindings=AnimationUtility.GetCurveBindings(source);var sourceCurves=sourceBindings.Select(b=>(binding:b,curve:AnimationUtility.GetEditorCurve(source,b))).ToArray();
 Func<string,float,float> val=(p,t)=>sourceCurves.Single(c=>c.binding.propertyName==p).curve.Evaluate(t);
 Func<float,Vector3> sourceLocal=t=>new Vector3(val("m_LocalPosition.x",t),val("m_LocalPosition.y",t),val("m_LocalPosition.z",t));
 float sourceLength=source.length;
 if(d==seq.muflehTimeline){
 d.time=timeline.duration-.1;d.RebuildGraph();d.Evaluate();d.Evaluate();
 var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
 var blockers=groups.Where(g=>!g.name.StartsWith("White Thobe")).Select(g=>{var bt=g.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Body"));var a=(Animator)d.GetGenericBinding(bt);return a.GetBoneTransform(HumanBodyBones.Hips).position;}).ToArray();
 var start=wm.matrix.MultiplyPoint3x4(sourceLocal(0));var end=all.Single(t=>t.name=="White Thobe Gathering Seat").position;end.y=start.y;
 using(var router=new DesertEntranceRouting(all.First(t=>t.name=="Tent"&&!t.parent),all.First(t=>t.name=="CarRoot"),all.First(t=>t.name=="Sled_ROOT"),scene)){
 router.SetWalkerRadius(.22f);router.SetSeatedBlockers(blockers);var rear=new Vector3(75.43f,start.y,32.72f);var route=router.FindClearRoute(start,rear);route.AddRange(router.FindClearRoute(rear,end).Skip(1));float length=0;for(int i=1;i<route.Count;i++)length+=Vector3.Distance(route[i-1],route[i]);float arrival=1+length/1.15f;sourceLength=arrival+5;
 var synthetic=Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray();for(int k=0;k<=Mathf.CeilToInt(sourceLength*60);k++){float t=Mathf.Min(sourceLength,k/60f);float remaining=Mathf.Clamp01((t-1)/(arrival-1))*length;int seg=1;while(seg<route.Count-1&&remaining>Vector3.Distance(route[seg-1],route[seg])){remaining-=Vector3.Distance(route[seg-1],route[seg]);seg++;}var dir=route[seg]-route[seg-1];var p=Vector3.Lerp(route[seg-1],route[seg],remaining/Mathf.Max(.001f,dir.magnitude));var local=wm.matrix.inverse.MultiplyPoint3x4(p);var routeParentRotation=Quaternion.LookRotation(wm.matrix.MultiplyVector(Vector3.forward),wm.matrix.MultiplyVector(Vector3.up));var rotation=Quaternion.Inverse(routeParentRotation)*Quaternion.LookRotation(Vector3.ProjectOnPlane(dir,Vector3.up));for(int axis=0;axis<3;axis++)synthetic[axis].AddKey(t,local[axis]);for(int axis=0;axis<4;axis++)synthetic[axis+3].AddKey(t,rotation[axis]);}
 string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};for(int axis=0;axis<7;axis++){for(int k=0;k<synthetic[axis].length;k++){AnimationUtility.SetKeyLeftTangentMode(synthetic[axis],k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(synthetic[axis],k,AnimationUtility.TangentMode.Linear);}int index=Array.FindIndex(sourceCurves,c=>c.binding.propertyName==properties[axis]);sourceCurves[index]=(sourceCurves[index].binding,synthetic[axis]);}router.Commit();}
 }
 var destination=sourceLocal(sourceLength);float sourceArrival=1;
 for(float t=sourceLength;t>=1;t-=1f/120){if(Vector3.ProjectOnPlane(sourceLocal(t)-destination,Vector3.up).magnitude>.015f){sourceArrival=t+1f/120;break;}}
 Func<Matrix4x4,AnimationCurve[],float,Vector3> pos=(m,c,t)=>m.MultiplyPoint3x4(new Vector3(c[0].Evaluate(t),c[1].Evaluate(t),c[2].Evaluate(t)));
 float ratio=0,minimum=0;foreach(float speed in new[]{1f,.95f,1.05f,.9f,1.1f,.85f,1.15f,.8f,1.2f,.75f,1.25f,.7f,.65f,.6f,.55f,.5f}){float gap=float.PositiveInfinity;string nearest="";float nearestTime=0;float arrival=1+(sourceArrival-1)/speed;for(float time=0;time<Math.Max(timeline.duration,arrival+5);time+=.025f){var p=wm.matrix.MultiplyPoint3x4(sourceLocal(time<1?0:1+(time-1)*speed));foreach(var other in motions.Where(m=>!m.name.StartsWith("White Thobe"))){float candidateGap=Vector3.ProjectOnPlane(p-pos(other.matrix,other.curves,time),Vector3.up).magnitude;if(candidateGap<gap){gap=candidateGap;nearest=other.name;nearestTime=time;}}}File.AppendAllText("work/arrival-clearance-debug.txt",d.name+" speed="+speed+" gap="+gap+" other="+nearest+" time="+nearestTime+" end="+wm.matrix.MultiplyPoint3x4(destination)+"\n");if(gap>=.80f){ratio=speed;minimum=gap;break;}}
 if(ratio==0)throw new Exception("Simultaneous path clearance failed for "+d.name);
 float arrivalTime=1+(sourceArrival-1)/ratio;double endTime=Math.Max(30,arrivalTime+5);double oldEnd=timeline.duration;
 foreach(var path in new[]{AssetDatabase.GetAssetPath(timeline),AssetDatabase.GetAssetPath(wm.clip)}){string copy="work/WhiteArrivalBackup/"+Path.GetFileName(path);if(!File.Exists(copy))File.Copy(path,copy);}
 timeline.durationMode=TimelineAsset.DurationMode.FixedLength;timeline.fixedDuration=endTime;
 foreach(var track in timeline.GetOutputTracks())foreach(var c in track.GetClips()){if(track is DesertSelectedCameraTrack||track.name.StartsWith("Position")||Math.Abs(c.end-oldEnd)<.1)c.duration=Math.Max(.1,endTime-c.start);}
 var center=motions.Where(m=>!m.name.StartsWith("White Thobe")).Aggregate(Vector3.zero,(sum,m)=>sum+pos(m.matrix,m.curves,(float)oldEnd))/5;
 var seatWorld=wm.matrix.MultiplyPoint3x4(destination);var face=Vector3.ProjectOnPlane(center-seatWorld,Vector3.up).normalized;var parentRotation=Quaternion.LookRotation(wm.matrix.MultiplyVector(Vector3.forward),wm.matrix.MultiplyVector(Vector3.up));var seatRotation=Quaternion.Inverse(parentRotation)*Quaternion.LookRotation(face,Vector3.up);
 int count=Mathf.CeilToInt((float)endTime*60);foreach(var pair in sourceCurves){var output=new AnimationCurve();for(int k=0;k<=count;k++){float t=(float)endTime*k/count;float st=t<1?0:1+(t-1)*ratio;float value=pair.curve.Evaluate(st);
 if(pair.binding.propertyName=="m_LocalPosition.y")value=Mathf.Lerp(val("m_LocalPosition.y",0),.16688856f,Mathf.SmoothStep(0,1,(t-arrivalTime)/.35f));
 if(pair.binding.propertyName.StartsWith("m_LocalRotation.")){var rotation=new Quaternion(val("m_LocalRotation.x",st),val("m_LocalRotation.y",st),val("m_LocalRotation.z",st),val("m_LocalRotation.w",st));rotation=Quaternion.Slerp(rotation,seatRotation,Mathf.SmoothStep(0,1,(t-arrivalTime)/.6f));int axis=pair.binding.propertyName.EndsWith("x")?0:pair.binding.propertyName.EndsWith("y")?1:pair.binding.propertyName.EndsWith("z")?2:3;value=rotation[axis];}
 output.AddKey(t,value);}for(int k=0;k<output.length;k++){AnimationUtility.SetKeyLeftTangentMode(output,k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(output,k,AnimationUtility.TangentMode.Linear);}var binding=wm.bindings.Single(b=>b.propertyName==pair.binding.propertyName);AnimationUtility.SetEditorCurve(wm.clip,binding,output);}
 wm.clip.EnsureQuaternionContinuity();EditorUtility.SetDirty(wm.clip);body.GetClips().First(c=>c.displayName.StartsWith("Standing")).duration=1;walk.start=1;walk.timeScale=ratio;walk.duration=arrivalTime-1+.35;sit.start=arrivalTime;sit.duration=endTime-arrivalTime;sit.blendInDuration=.35;walk.blendOutDuration=.35;EditorUtility.SetDirty(body);EditorUtility.SetDirty(timeline);
 d.RebuildGraph();d.time=arrivalTime+2;for(int i=0;i<4;i++)d.Evaluate();var animator=(Animator)d.GetGenericBinding(body);var hip=animator.GetBoneTransform(HumanBodyBones.Hips);float hipHeight=hip.position.y-animator.transform.position.y;if(hipHeight>.45f)throw new Exception("White Thobe still standing at seated time: "+hipHeight);
 var root=animator.transform.parent;float faceDot=Vector3.Dot(Vector3.ProjectOnPlane(root.forward,Vector3.up).normalized,face);if(faceDot<.98f)throw new Exception("White Thobe facing wrong way "+faceDot);
 report.AppendLine(d.name+": simultaneous start 1s; arrival="+arrivalTime.ToString("F2")+"s; sitting hold="+(endTime-arrivalTime).ToString("F2")+"s; timeline="+timeline.duration+"s; hip height="+hipHeight.ToString("F2")+"m; faces gathering dot="+faceDot.ToString("F3")+"; all-character minimum clearance="+minimum.ToString("F2")+"m; seat="+root.position);d.Stop();
 }
 AssetDatabase.SaveAssets();var active=DesertCharacterSelection.MuflehSelected?seq.muflehTimeline:seq.saqeeraTimeline;active.time=0;active.RebuildGraph();active.Evaluate();active.Evaluate();active.Pause();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText(Marker,report.ToString());
 }catch(Exception ex){File.WriteAllText("work/white-arrival-error.txt",ex.ToString());Debug.LogException(ex);}finally{SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}}
}




