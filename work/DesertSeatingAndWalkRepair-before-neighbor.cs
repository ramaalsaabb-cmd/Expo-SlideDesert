using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

public static class DesertSeatingAndWalkRepair
{
 const string Marker="work/seating-and-walk-behind-v3.txt";
 [InitializeOnLoadMethod] static void Init(){EditorApplication.delayCall+=Once;EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Once;};}
 static void Once(){if(!File.Exists(Marker)&&!EditorApplication.isPlayingOrWillChangePlaymode)Apply();}
 static AnimationClip Imported(string name)=>AssetDatabase.LoadAllAssetsAtPath("Assets/Cinematics/Animations/"+name+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
 [MenuItem("Tools/Desert VR/Swap Seating and Avoid Seated People")]
 public static void Apply()
 {
 var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath("Assets/Scenes/DesertOverview.unity");bool opened=!scene.isLoaded;
 if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/DesertOverview.unity",OpenSceneMode.Additive);
 SceneManager.SetActiveScene(scene);
 var report=new System.Text.StringBuilder();
 try {
 var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
 var seq=all.Select(t=>t.GetComponent<DesertCinematicSequence>()).First(x=>x);
 var tent=all.First(t=>t.name=="Tent"&&!t.parent);var car=all.First(t=>t.name=="CarRoot");var sled=all.First(t=>t.name=="Sled_ROOT");
 string backup="work/BehindSeatedManBackup";Directory.CreateDirectory(backup);
 foreach(var file in Directory.GetFiles("Assets/Cinematics").Where(p=>p.EndsWith(".anim")||p.EndsWith(".playable")))if(!File.Exists(backup+"/"+Path.GetFileName(file)))File.Copy(file,backup+"/"+Path.GetFileName(file));
 if(!File.Exists(backup+"/DesertOverview.unity"))File.Copy(scene.path,backup+"/DesertOverview.unity");
 foreach(var director in new[]{seq.muflehTimeline,seq.saqeeraTimeline}) {
 var timeline=(TimelineAsset)director.playableAsset;
 foreach(var group in timeline.GetRootTracks().OfType<GroupTrack>()) {
 var body=group.GetChildTracks().OfType<AnimationTrack>().FirstOrDefault(t=>t.name.StartsWith("Body"));if(!body)continue;
 bool male=group.name.StartsWith("White Thobe")||group.name.StartsWith("Thobe")||group.name.StartsWith("Desert Robe");
 foreach(var item in body.GetClips().Where(c=>c.displayName.StartsWith("Sit")||c.displayName.StartsWith("Seated"))) {
 var asset=(AnimationPlayableAsset)item.asset;asset.clip=Imported(male?"Sitting Idle":"Male Sitting Pose");EditorUtility.SetDirty(asset);
 report.AppendLine(director.name+" / "+group.name+": "+(male?"Sitting Idle":"Male Sitting Pose"));
 }
 }
 director.time=0;director.RebuildGraph();director.Evaluate();director.Evaluate();
 var blockers=new List<Vector3>();
 foreach(var group in timeline.GetRootTracks().OfType<GroupTrack>()) {
 var body=group.GetChildTracks().OfType<AnimationTrack>().FirstOrDefault(t=>t.name.StartsWith("Body"));if(!body||body.GetClips().Any(c=>c.displayName.StartsWith("Walk")))continue;
 var model=(Animator)director.GetGenericBinding(body);blockers.Add(model.GetBoneTransform(HumanBodyBones.Hips).position);
 }
 var seatedManGroup=timeline.GetRootTracks().OfType<GroupTrack>().Single(g=>g.name=="Thobe");
var seatedManTrack=seatedManGroup.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Body"));var seatedMan=(Animator)director.GetGenericBinding(seatedManTrack);var seatedHip=seatedMan.GetBoneTransform(HumanBodyBones.Hips).position;var shoulderRight=seatedMan.GetBoneTransform(HumanBodyBones.RightUpperArm).position-seatedMan.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;var facing=Vector3.Cross(Vector3.ProjectOnPlane(shoulderRight,Vector3.up),Vector3.up).normalized;
foreach(var group in timeline.GetRootTracks().OfType<GroupTrack>().Where(g=>g.name.StartsWith("White Thobe")||g.name.StartsWith("Desert Robe"))) {
 var body=group.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Body"));var walk=body.GetClips().FirstOrDefault(c=>c.displayName.StartsWith("Walk"));if(walk==null)continue;
 var track=group.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Position"));var asset=(AnimationPlayableAsset)track.GetClips().Single().asset;var clip=asset.clip;
 var bindings=AnimationUtility.GetCurveBindings(clip);var driver=(Animator)director.GetGenericBinding(track);
 string[] props={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
 var curves=props.Select(p=>AnimationUtility.GetEditorCurve(clip,bindings.Single(b=>b.propertyName==p))).ToArray();
 Func<float,Vector3> world=t=>driver.transform.TransformPoint(new Vector3(curves[0].Evaluate(t),curves[1].Evaluate(t),curves[2].Evaluate(t)));
 float finish=(float)body.GetClips().First(c=>c.displayName.StartsWith("Sit")).start;
 var start=world(1);var end=world(finish);end.y=start.y;
 using(var router=new DesertEntranceRouting(tent,car,sled,scene)) {
 router.SetWalkerRadius(.22f);router.SetSeatedBlockers(blockers);
 report.AppendLine("Route start="+start+" end="+end+" blockers="+string.Join(";",blockers)+" startClear="+router.CheckRouteSegment(start,start)+" endClear="+router.CheckRouteSegment(end,end));File.WriteAllText("work/walk-diagnostic.txt",report.ToString());List<Vector3> route=null;Vector3 rear=Vector3.zero;foreach(var offset in new[]{new Vector2(.95f,0),new Vector2(1.15f,0),new Vector2(1.4f,0),new Vector2(1.15f,.45f),new Vector2(1.15f,-.45f),new Vector2(.95f,.8f),new Vector2(.95f,-.8f),new Vector2(1.65f,1.1f),new Vector2(1.65f,-1.1f)}){var candidate=seatedHip-facing*offset.x+Vector3.Cross(Vector3.up,facing)*offset.y;candidate.y=start.y;report.AppendLine("rear candidate="+candidate+" facing="+facing+" clear="+router.CheckRouteSegment(candidate,candidate));File.WriteAllText("work/rear-diagnostic.txt",report.ToString());if(!router.CheckRouteSegment(candidate,candidate))continue;try{var approach=router.FindClearRoute(start,candidate);var departure=router.FindClearRoute(candidate,end);route=approach;route.AddRange(departure.Skip(1));rear=candidate;break;}catch(Exception ex){if(ex.Message.Contains("20 seconds"))throw;}}if(route==null)throw new Exception("No clear rear aisle behind seated Thobe.");report.AppendLine("Mandatory rear waypoint="+rear+"; facing="+facing+"; rear dot="+Vector3.Dot(rear-seatedHip,facing));float length=0;for(int i=1;i<route.Count;i++)length+=Vector3.Distance(route[i-1],route[i]);
 var output=Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray();
 float oldFinish=finish;float speed=((AnimationPlayableAsset)walk.asset).clip.averageSpeed.magnitude;if(speed<.3f||speed>2f)speed=1.15f;finish=1+length/speed;var sit=body.GetClips().First(c=>c.displayName.StartsWith("Sit"));walk.timeScale=1;walk.duration=finish-1+.35f;walk.blendOutDuration=.35; sit.start=finish;sit.duration=Math.Max(.1,timeline.duration-finish);sit.blendInDuration=.35;EditorUtility.SetDirty(body);
var originalEnd= new Quaternion(curves[3].Evaluate(oldFinish+.4f),curves[4].Evaluate(oldFinish+.4f),curves[5].Evaluate(oldFinish+.4f),curves[6].Evaluate(oldFinish+.4f));
 int count=Mathf.CeilToInt((float)timeline.duration*30);
 Vector3 last=start;float clearance=float.PositiveInfinity;
 for(int k=0;k<=count;k++) {
 float time=(float)timeline.duration*k/count;float remaining=Mathf.Clamp01((time-1)/Mathf.Max(.1f,finish-1))*length;int seg=1;
 while(seg<route.Count-1&&remaining>Vector3.Distance(route[seg-1],route[seg])){remaining-=Vector3.Distance(route[seg-1],route[seg]);seg++;}
 var direction=route[seg]-route[seg-1];var pos=Vector3.Lerp(route[seg-1],route[seg],remaining/Mathf.Max(.001f,direction.magnitude));
 if(k>0&&!router.CheckRouteSegment(last,pos))throw new Exception("Blocked baked route: "+group.name+" at "+time);last=pos;
 foreach(var b in blockers)clearance=Mathf.Min(clearance,Vector3.ProjectOnPlane(pos-b,Vector3.up).magnitude);
 var local=driver.transform.InverseTransformPoint(pos);float heightTime=time<1?time:time<finish?1+(time-1)*(oldFinish-1)/(finish-1):oldFinish+(time-finish);local.y=curves[1].Evaluate(heightTime);
 var rot=Quaternion.Inverse(driver.transform.rotation)*Quaternion.LookRotation(Vector3.ProjectOnPlane(direction,Vector3.up));
 if(time<1)rot=new Quaternion(curves[3].Evaluate(time),curves[4].Evaluate(time),curves[5].Evaluate(time),curves[6].Evaluate(time));
 rot=Quaternion.Slerp(rot,originalEnd,Mathf.SmoothStep(0,1,(time-finish)/.35f));
 for(int axis=0;axis<3;axis++)output[axis].AddKey(time,local[axis]);for(int axis=0;axis<4;axis++)output[axis+3].AddKey(time,rot[axis]);
 }
 for(int axis=0;axis<7;axis++){for(int k=0;k<output[axis].length;k++){AnimationUtility.SetKeyLeftTangentMode(output[axis],k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(output[axis],k,AnimationUtility.TangentMode.Linear);}AnimationUtility.SetEditorCurve(clip,bindings.Single(b=>b.propertyName==props[axis]),output[axis]);}
 clip.EnsureQuaternionContinuity();EditorUtility.SetDirty(clip);router.Commit();
 report.AppendLine(director.name+" / "+group.name+": "+route.Count+" waypoints; "+length.ToString("F2")+"m; minimum seated-person clearance "+clearance.ToString("F2")+"m; walking speed "+speed.ToString("F2")+"m/s; all segments checked against furniture.");
 }
 }
 director.Stop();EditorUtility.SetDirty(timeline);
 }
 AssetDatabase.SaveAssets();var selected=DesertCharacterSelection.MuflehSelected?seq.muflehTimeline:seq.saqeeraTimeline;selected.time=0;selected.RebuildGraph();selected.Evaluate();selected.Evaluate();selected.Pause();
 EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText(Marker,report.ToString());Debug.Log(report.ToString());
 }catch(Exception ex){File.WriteAllText("work/seating-walk-error.txt",ex.ToString());Debug.LogException(ex);}
 finally{SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}
 }
}






