using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
public static class DesertSeatedHeadSetup {
 const string Marker="work/seated-head-raised-v1.txt";
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Once;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Once;};}
 static void Once(){if(!EditorApplication.isPlayingOrWillChangePlaymode&&!File.Exists(Marker))Apply();}
 static AnimationClip Raised(string name){
 var source=AssetDatabase.LoadAllAssetsAtPath("Assets/Cinematics/Animations/"+name+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
 string path="Assets/Cinematics/Animations/"+name+" Head Raised.anim";
 var result=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(result)return result;
 result=UnityEngine.Object.Instantiate(source);result.name=name+" Head Raised";
 foreach(var muscle in new[]{("Head Nod Down-Up",.16f),("Neck Nod Down-Up",.06f)}){
 var binding=EditorCurveBinding.FloatCurve("",typeof(Animator),muscle.Item1);
 var original=AnimationUtility.GetEditorCurve(result,binding);
 if(original==null)original=AnimationCurve.Constant(0,source.length,0);
 float mean=Enumerable.Range(0,31).Average(i=>original.Evaluate(source.length*i/30));
 var curve=new AnimationCurve();int samples=Mathf.Max(2,Mathf.CeilToInt(source.length*30));
 for(int i=0;i<=samples;i++){float time=source.length*i/samples;curve.AddKey(time,Mathf.Clamp(muscle.Item2+original.Evaluate(time)-mean,-.04f,.32f));}
 for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}
 AnimationUtility.SetEditorCurve(result,binding,curve);
 }
 AssetDatabase.CreateAsset(result,path);return result;
 }
 [MenuItem("Tools/Desert VR/Raise Seated Heads")]
 static void Apply(){var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath("Assets/Scenes/DesertOverview.unity");bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/DesertOverview.unity",OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
 try{
 var report=new System.Text.StringBuilder();var seq=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DesertCinematicSequence>(true)).Single();Directory.CreateDirectory("work/HeadPoseBackup");
 foreach(var d in new[]{seq.muflehTimeline,seq.saqeeraTimeline}){
 var timeline=(TimelineAsset)d.playableAsset;string file=AssetDatabase.GetAssetPath(timeline);string copy="work/HeadPoseBackup/"+Path.GetFileName(file);if(!File.Exists(copy))File.Copy(file,copy);
 foreach(var group in timeline.GetRootTracks().OfType<GroupTrack>()){
 var body=group.GetChildTracks().OfType<AnimationTrack>().FirstOrDefault(t=>t.name.StartsWith("Body"));if(!body)continue;
 foreach(var item in body.GetClips().Where(c=>c.displayName.StartsWith("Sit")||c.displayName.StartsWith("Seated"))){
 var asset=(AnimationPlayableAsset)item.asset;string sourcePath=AssetDatabase.GetAssetPath(asset.clip);string name=sourcePath.Contains("Male Sitting Pose")?"Male Sitting Pose":"Sitting Idle";asset.clip=Raised(name);EditorUtility.SetDirty(asset);report.AppendLine(d.name+" / "+group.name+": "+asset.clip.name);
 }
 }
 d.RebuildGraph();d.time=timeline.duration-.5;d.Evaluate();d.Evaluate();d.Stop();
 }
 AssetDatabase.SaveAssets();var selected=DesertCharacterSelection.MuflehSelected?seq.muflehTimeline:seq.saqeeraTimeline;selected.time=0;selected.RebuildGraph();selected.Evaluate();selected.Evaluate();selected.Pause();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText(Marker,report.ToString());
 }catch(Exception ex){File.WriteAllText("work/seated-head-error.txt",ex.ToString());Debug.LogException(ex);}finally{SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}}
}
