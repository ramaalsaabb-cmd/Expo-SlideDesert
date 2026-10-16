using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
public static class DesertSimultaneousWalk {
 const string Marker="work/simultaneous-walk-v1.txt";
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Once;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Once;};}
 static void Once(){if(!EditorApplication.isPlayingOrWillChangePlaymode&&!File.Exists(Marker))Apply();}
 static void Apply(){var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath("Assets/Scenes/DesertOverview.unity");bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/DesertOverview.unity",OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
 try{var seq=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DesertCinematicSequence>(true)).Single();var report=new System.Text.StringBuilder();Directory.CreateDirectory("work/SimultaneousWalkBackup");
 foreach(var d in new[]{seq.muflehTimeline,seq.saqeeraTimeline}){
 var timeline=(TimelineAsset)d.playableAsset;d.time=0;d.RebuildGraph();d.Evaluate();d.Evaluate();
 var groups=timeline.GetRootTracks().OfType<GroupTrack>().ToArray();var white=groups.Single(g=>g.name.StartsWith("White Thobe"));
 var body=white.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Body"));var walk=body.GetClips().First(c=>c.displayName.StartsWith("Walk"));var sit=body.GetClips().First(c=>c.displayName.StartsWith("Sit"));float oldStart=(float)walk.start,oldFinish=(float)sit.start;
 var motions=groups.Select(g=>{var track=g.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Position"));var clip=((AnimationPlayableAsset)track.GetClips().Single().asset).clip;var driver=(Animator)d.GetGenericBinding(track);var bs=AnimationUtility.GetCurveBindings(clip);var xyz=new[]{"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z"}.Select(p=>AnimationUtility.GetEditorCurve(clip,bs.Single(b=>b.propertyName==p))).ToArray();return(name:g.name,matrix:driver.transform.localToWorldMatrix,curves:xyz,clip:clip,bindings:bs);}).ToArray();
 var wm=motions.Single(m=>m.name.StartsWith("White Thobe"));
 Func<Matrix4x4,AnimationCurve[],float,Vector3> pos=(m,c,t)=>m.MultiplyPoint3x4(new Vector3(c[0].Evaluate(t),c[1].Evaluate(t),c[2].Evaluate(t)));
 float chosen=0,minGap=0;
 foreach(float ratio in new[]{1f,.95f,1.05f,.9f,1.1f,.85f,1.15f,.8f,1.2f,.75f,.7f,.65f,.6f}){
 float clearance=float.PositiveInfinity;for(float t=0;t<Math.Max(timeline.duration,1+(oldFinish-oldStart)/ratio+3);t+=.025f){float source=t<1?0:oldStart+(t-1)*ratio;var p=pos(wm.matrix,wm.curves,source);foreach(var other in motions.Where(m=>!m.name.StartsWith("White Thobe"))){float gap=Vector3.ProjectOnPlane(p-pos(other.matrix,other.curves,t),Vector3.up).magnitude;clearance=Mathf.Min(clearance,gap);}}
 if(clearance>=.85f){chosen=ratio;minGap=clearance;break;}
 }
 if(chosen==0)throw new Exception("No collision-free simultaneous timing: "+d.name);
 foreach(string path in new[]{AssetDatabase.GetAssetPath(wm.clip),AssetDatabase.GetAssetPath(timeline)}){string copy="work/SimultaneousWalkBackup/"+Path.GetFileName(path);if(!File.Exists(copy))File.Copy(path,copy);}
 float arrival=1+(oldFinish-oldStart)/chosen;
 if(arrival+3>timeline.duration){double previousEnd=timeline.duration;double newEnd=arrival+3;foreach(var t in timeline.GetOutputTracks())foreach(var c in t.GetClips())if(Math.Abs(c.end-previousEnd)<.05)c.duration=Math.Max(.1,newEnd-c.start);}
 foreach(var binding in wm.bindings){var source=AnimationUtility.GetEditorCurve(wm.clip,binding);if(source==null)continue;var output=new AnimationCurve();int count=Mathf.CeilToInt((float)timeline.duration*60);for(int k=0;k<=count;k++){float t=(float)timeline.duration*k/count;float sourceTime=t<1?0:oldStart+(t-1)*chosen;output.AddKey(t,source.Evaluate(sourceTime));}for(int k=0;k<output.length;k++){AnimationUtility.SetKeyLeftTangentMode(output,k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(output,k,AnimationUtility.TangentMode.Linear);}AnimationUtility.SetEditorCurve(wm.clip,binding,output);}
 wm.clip.EnsureQuaternionContinuity();EditorUtility.SetDirty(wm.clip);body.GetClips().First(c=>c.displayName.StartsWith("Standing")).duration=1;walk.start=1;walk.timeScale*=chosen;walk.duration=arrival-1+.35f;sit.start=arrival;sit.duration=timeline.duration-arrival;EditorUtility.SetDirty(body);
 var selected=groups.First(g=>g.name.Contains("selected focus"));var selectedBody=selected.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Body"));if(Math.Abs(selectedBody.GetClips().First(c=>c.displayName.StartsWith("Walk")).start-walk.start)>.001)throw new Exception("Walk start mismatch");
 report.AppendLine(d.name+": both walks start at 1.000s; White Thobe speed ratio="+chosen.ToString("F2")+"; minimum clearance from all five characters="+minGap.ToString("F2")+"m; sampled every .025s; same route and seat.");d.Stop();
 }
 AssetDatabase.SaveAssets();var active=DesertCharacterSelection.MuflehSelected?seq.muflehTimeline:seq.saqeeraTimeline;active.time=0;active.RebuildGraph();active.Evaluate();active.Evaluate();active.Pause();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText(Marker,report.ToString());
 }catch(Exception ex){File.WriteAllText("work/simultaneous-walk-error.txt",ex.ToString());Debug.LogException(ex);}finally{SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}}
}

