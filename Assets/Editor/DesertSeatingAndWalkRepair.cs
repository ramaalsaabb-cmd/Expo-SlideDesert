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
 const string Marker="work/white-all-characters-v6.txt";
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
 string backup="work/WhiteAllCharactersBackup";Directory.CreateDirectory(backup);
 foreach(var file in Directory.GetFiles("Assets/Cinematics").Where(p=>p.EndsWith(".anim")||p.EndsWith(".playable")))if(!File.Exists(backup+"/"+Path.GetFileName(file)))File.Copy(file,backup+"/"+Path.GetFileName(file));
 if(!File.Exists(backup+"/DesertOverview.unity"))File.Copy(scene.path,backup+"/DesertOverview.unity");
 Vector3 sharedSeat=Vector3.zero;Quaternion sharedRotation=Quaternion.identity;bool seatCaptured=false;
 foreach(var director in new[]{seq.muflehTimeline,seq.saqeeraTimeline}) {
 var timeline=(TimelineAsset)director.playableAsset;
 director.time=timeline.duration-.1;director.RebuildGraph();director.Evaluate();director.Evaluate();
 var blockers=new List<Vector3>();
 foreach(var group in timeline.GetRootTracks().OfType<GroupTrack>()) {
 var body=group.GetChildTracks().OfType<AnimationTrack>().FirstOrDefault(t=>t.name.StartsWith("Body"));if(!body||group.name.StartsWith("White Thobe"))continue;
 var model=(Animator)director.GetGenericBinding(body);blockers.Add(model.GetBoneTransform(HumanBodyBones.Hips).position);
 }
 var seatedManGroup=timeline.GetRootTracks().OfType<GroupTrack>().Single(g=>g.name=="Thobe");
var seatedManTrack=seatedManGroup.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Body"));var seatedMan=(Animator)director.GetGenericBinding(seatedManTrack);var seatedHip=seatedMan.GetBoneTransform(HumanBodyBones.Hips).position;var shoulderRight=seatedMan.GetBoneTransform(HumanBodyBones.RightUpperArm).position-seatedMan.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;var facing=Vector3.Cross(Vector3.ProjectOnPlane(shoulderRight,Vector3.up),Vector3.up).normalized;
foreach(var group in timeline.GetRootTracks().OfType<GroupTrack>().Where(g=>g.name.StartsWith("White Thobe"))) {
 var body=group.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Body"));var walk=body.GetClips().FirstOrDefault(c=>c.displayName.StartsWith("Walk"));if(walk==null)continue;
 var track=group.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Position"));var asset=(AnimationPlayableAsset)track.GetClips().Single().asset;var clip=asset.clip;
 var bindings=AnimationUtility.GetCurveBindings(clip);var driver=(Animator)director.GetGenericBinding(track);
 string[] props={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
 var curves=props.Select(p=>AnimationUtility.GetEditorCurve(clip,bindings.Single(b=>b.propertyName==p))).ToArray();
 Func<float,Vector3> world=t=>driver.transform.TransformPoint(new Vector3(curves[0].Evaluate(t),curves[1].Evaluate(t),curves[2].Evaluate(t)));
 float finish=(float)body.GetClips().First(c=>c.displayName.StartsWith("Sit")).start;
 float oldWalkingStart=(float)walk.start;var selectedGroup=timeline.GetRootTracks().OfType<GroupTrack>().First(g=>g.name.Contains("selected focus"));var selectedBody=selectedGroup.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Body"));float walkingStart=(float)selectedBody.GetClips().First(c=>c.displayName.StartsWith("Sit")).start+1f;var start=world(oldWalkingStart);var end=world(finish);end.y=start.y;
 using(var router=new DesertEntranceRouting(tent,car,sled,scene)) {
 router.SetWalkerRadius(.22f);router.SetSeatedBlockers(blockers);
 var approvedTimeline=(TimelineAsset)seq.muflehTimeline.playableAsset;
 var approvedGroup=approvedTimeline.GetRootTracks().OfType<GroupTrack>().Single(g=>g.name.StartsWith("White Thobe"));
 var approvedTrack=approvedGroup.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Position"));
 var approvedDriver=(Animator)seq.muflehTimeline.GetGenericBinding(approvedTrack);
 var approvedClip=((AnimationPlayableAsset)approvedTrack.GetClips().Single().asset).clip;
 var approvedBindings=AnimationUtility.GetCurveBindings(approvedClip);
 Func<string,float> approved=p=>AnimationUtility.GetEditorCurve(approvedClip,approvedBindings.Single(b=>b.propertyName==p)).Evaluate((float)approvedTimeline.duration);
 var approvedSeat=approvedDriver.transform.TransformPoint(new Vector3(approved("m_LocalPosition.x"),approved("m_LocalPosition.y"),approved("m_LocalPosition.z")));
 var muflehRotation=approvedDriver.transform.rotation*new Quaternion(approved("m_LocalRotation.x"),approved("m_LocalRotation.y"),approved("m_LocalRotation.z"),approved("m_LocalRotation.w"));
 if(!seatCaptured){approvedSeat=all.Single(t=>t.name=="White Thobe Gathering Seat").position;approvedSeat.y=.16688856f;sharedSeat=approvedSeat;sharedRotation=muflehRotation;seatCaptured=true;}else{approvedSeat=sharedSeat;muflehRotation=sharedRotation;}
 end=new Vector3(approvedSeat.x,start.y,approvedSeat.z);
 report.AppendLine("Shared White Thobe seat from Mufleh timeline="+approvedSeat+"; fixed Saqeera destination="+end);
 report.AppendLine("Route start="+start+" end="+end+" blockers="+string.Join(";",blockers)+" startClear="+router.CheckRouteSegment(start,start)+" endClear="+router.CheckRouteSegment(end,end));File.WriteAllText("work/walk-diagnostic.txt",report.ToString());List<Vector3> route=null;Vector3 rear=Vector3.zero;foreach(var offset in new[]{new Vector2(.95f,0),new Vector2(1.15f,0),new Vector2(1.4f,0),new Vector2(1.15f,.45f),new Vector2(1.15f,-.45f),new Vector2(.95f,.8f),new Vector2(.95f,-.8f),new Vector2(1.65f,1.1f),new Vector2(1.65f,-1.1f)}){var candidate=seatedHip-facing*offset.x+Vector3.Cross(Vector3.up,facing)*offset.y;candidate.y=start.y;report.AppendLine("rear candidate="+candidate+" facing="+facing+" clear="+router.CheckRouteSegment(candidate,candidate));File.WriteAllText("work/rear-diagnostic.txt",report.ToString());if(!router.CheckRouteSegment(candidate,candidate))continue;try{var approach=router.FindClearRoute(start,candidate);var departure=router.FindClearRoute(candidate,end);route=approach;route.AddRange(departure.Skip(1));rear=candidate;break;}catch(Exception ex){if(ex.Message.Contains("20 seconds"))throw;}}if(route==null)throw new Exception("No clear rear aisle behind seated Thobe.");report.AppendLine("Mandatory rear waypoint="+rear+"; facing="+facing+"; rear dot="+Vector3.Dot(rear-seatedHip,facing));float length=0;for(int i=1;i<route.Count;i++)length+=Vector3.Distance(route[i-1],route[i]);
 var output=Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray();
 float oldFinish=finish;float speed=((AnimationPlayableAsset)walk.asset).clip.averageSpeed.magnitude;if(speed<.3f||speed>2f)speed=1.15f;finish=walkingStart+length/speed;double oldDuration=timeline.duration;double newDuration=Math.Max(oldDuration,finish+3);foreach(var t in timeline.GetOutputTracks()){foreach(var c in t.GetClips()){if(Math.Abs(c.end-oldDuration)<.05)c.duration=Math.Max(.1,newDuration-c.start);}}var standing=body.GetClips().First(c=>c.displayName.StartsWith("Standing"));standing.duration=walkingStart;walk.start=walkingStart;var sit=body.GetClips().First(c=>c.displayName.StartsWith("Sit"));walk.timeScale=1;walk.duration=finish-walkingStart+.35f;walk.blendOutDuration=.35; sit.start=finish;sit.duration=Math.Max(.1,newDuration-finish);sit.blendInDuration=.35;EditorUtility.SetDirty(body);
var originalEnd=Quaternion.Inverse(driver.transform.rotation)*muflehRotation;
 int count=Mathf.CeilToInt((float)timeline.duration*30);
 Vector3 last=start;float clearance=float.PositiveInfinity;
 for(int k=0;k<=count;k++) {
 float time=(float)timeline.duration*k/count;float remaining=Mathf.Clamp01((time-walkingStart)/Mathf.Max(.1f,finish-walkingStart))*length;int seg=1;
 while(seg<route.Count-1&&remaining>Vector3.Distance(route[seg-1],route[seg])){remaining-=Vector3.Distance(route[seg-1],route[seg]);seg++;}
 var direction=route[seg]-route[seg-1];var pos=Vector3.Lerp(route[seg-1],route[seg],remaining/Mathf.Max(.001f,direction.magnitude));
 if(k>0&&!router.CheckRouteSegment(last,pos))throw new Exception("Blocked baked route: "+group.name+" at "+time);last=pos;
 foreach(var b in blockers)clearance=Mathf.Min(clearance,Vector3.ProjectOnPlane(pos-b,Vector3.up).magnitude);
 var local=driver.transform.InverseTransformPoint(pos);float heightTime=time<walkingStart?0:time<finish?oldWalkingStart+(time-walkingStart)*(oldFinish-oldWalkingStart)/(finish-walkingStart):oldFinish+(time-finish);local.y=curves[1].Evaluate(heightTime);if(time>=finish)local.y=Mathf.Lerp(curves[1].Evaluate(oldWalkingStart),driver.transform.InverseTransformPoint(approvedSeat).y,Mathf.SmoothStep(0,1,(time-finish)/.35f));
 var rot=Quaternion.Inverse(driver.transform.rotation)*Quaternion.LookRotation(Vector3.ProjectOnPlane(direction,Vector3.up));
 if(time<walkingStart)rot=new Quaternion(curves[3].Evaluate(0),curves[4].Evaluate(0),curves[5].Evaluate(0),curves[6].Evaluate(0));
 rot=Quaternion.Slerp(rot,originalEnd,Mathf.SmoothStep(0,1,(time-finish)/.35f));
 for(int axis=0;axis<3;axis++)output[axis].AddKey(time,local[axis]);for(int axis=0;axis<4;axis++)output[axis+3].AddKey(time,rot[axis]);
 }
 for(int axis=0;axis<7;axis++){for(int k=0;k<output[axis].length;k++){AnimationUtility.SetKeyLeftTangentMode(output[axis],k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(output[axis],k,AnimationUtility.TangentMode.Linear);}AnimationUtility.SetEditorCurve(clip,bindings.Single(b=>b.propertyName==props[axis]),output[axis]);}
 clip.EnsureQuaternionContinuity();EditorUtility.SetDirty(clip);router.Commit();
 report.AppendLine(director.name+" / "+group.name+": "+route.Count+" waypoints; walking begins at "+walkingStart.ToString("F2")+"s after selected character sits; "+length.ToString("F2")+"m; minimum seated-person clearance "+clearance.ToString("F2")+"m; walking speed "+speed.ToString("F2")+"m/s; all segments checked against furniture.");
 }
 }
 var motionSamples=timeline.GetRootTracks().OfType<GroupTrack>().Select(g=>{
 var t=g.GetChildTracks().OfType<AnimationTrack>().First(x=>x.name.StartsWith("Position"));var c=((AnimationPlayableAsset)t.GetClips().Single().asset).clip;var bs=AnimationUtility.GetCurveBindings(c);var a=(Animator)director.GetGenericBinding(t);var xyz=new[]{"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z"}.Select(p=>AnimationUtility.GetEditorCurve(c,bs.First(b=>b.propertyName==p))).ToArray();return(name:g.name,parent:a.transform,curves:xyz);
 }).ToArray();
 var whiteMotion=motionSamples.Single(x=>x.name.StartsWith("White Thobe"));float allClearance=float.PositiveInfinity;
 Func<Transform,AnimationCurve[],float,Vector3> at=(p,c,t)=>p.TransformPoint(new Vector3(c[0].Evaluate(t),c[1].Evaluate(t),c[2].Evaluate(t)));
 for(float t=0;t<timeline.duration;t+=.05f){var wp=at(whiteMotion.parent,whiteMotion.curves,t);foreach(var other in motionSamples.Where(x=>!x.name.StartsWith("White Thobe"))){float gap=Vector3.ProjectOnPlane(wp-at(other.parent,other.curves,t),Vector3.up).magnitude;allClearance=Mathf.Min(allClearance,gap);if(gap<.70f)throw new Exception("Character overlap with "+other.name+" at "+t+" gap="+gap);}}
 report.AppendLine(director.name+": complete movement checked every 0.05s against all five characters; minimum root clearance="+allClearance.ToString("F2")+"m.");
 director.Stop();EditorUtility.SetDirty(timeline);
 }
 AssetDatabase.SaveAssets();var selected=DesertCharacterSelection.MuflehSelected?seq.muflehTimeline:seq.saqeeraTimeline;selected.time=0;selected.RebuildGraph();selected.Evaluate();selected.Evaluate();selected.Pause();
 EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText(Marker,report.ToString());Debug.Log(report.ToString());
 }catch(Exception ex){File.WriteAllText("work/seating-walk-error.txt",ex.ToString());Debug.LogException(ex);}
 finally{SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}
 }
}














