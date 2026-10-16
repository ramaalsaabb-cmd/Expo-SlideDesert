using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
public static class DesertSeatedCameraSetup {
 const string Marker="work/seated-camera-v1.txt";
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Once;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Once;};}
 static void Once(){if(!EditorApplication.isPlayingOrWillChangePlaymode&&!File.Exists(Marker))Apply();}
 [MenuItem("Tools/Desert VR/Camera Behind Head When Seated")]
 static void Apply(){var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath("Assets/Scenes/DesertOverview.unity");bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/DesertOverview.unity",OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
 try{var seq=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DesertCinematicSequence>(true)).Single();var report=new System.Text.StringBuilder();
 foreach(var d in new[]{seq.muflehTimeline,seq.saqeeraTimeline}){
 var timeline=(TimelineAsset)d.playableAsset;
 var follow=timeline.GetRootTracks().OfType<DesertSelectedCameraTrack>().Single(t=>!t.muted);
 var cameraAsset=(DesertSelectedCameraClip)follow.GetClips().Single().asset;
 var target=d.GetReferenceValue(cameraAsset.selectedCharacter.exposedName,out var valid) as Transform;if(!valid||!target)throw new Exception("Missing selected character");
 var group=timeline.GetRootTracks().OfType<GroupTrack>().Single(g=>g.name==target.name||g.name.StartsWith(target.name+" "));
 var body=group.GetChildTracks().OfType<AnimationTrack>().Single(t=>t.name.StartsWith("Body"));
 cameraAsset.seatedStartTime=(float)body.GetClips().First(c=>c.displayName.StartsWith("Sit")).start;cameraAsset.seatedTurnDuration=1.4f;EditorUtility.SetDirty(cameraAsset);
 d.time=cameraAsset.seatedStartTime+2;d.RebuildGraph();for(int i=0;i<4;i++)d.Evaluate();
 var animator=(Animator)d.GetGenericBinding(body);var head=animator.GetBoneTransform(HumanBodyBones.Head);var forward=Vector3.ProjectOnPlane(target.forward,Vector3.up).normalized;
 float rear=Vector3.Dot(seq.cinematicCamera.transform.position-head.position,forward);if(rear>=-.1f)throw new Exception("Camera is not behind selected head: "+target.name+" dot="+rear);
 report.AppendLine(target.name+": orbit begins at "+cameraAsset.seatedStartTime.ToString("F2")+"s; duration 1.4s; rear offset "+rear.ToString("F2")+"m.");d.Stop();
 }
 AssetDatabase.SaveAssets();var selected=DesertCharacterSelection.MuflehSelected?seq.muflehTimeline:seq.saqeeraTimeline;selected.time=0;selected.RebuildGraph();selected.Evaluate();selected.Evaluate();selected.Pause();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText(Marker,report.ToString());
 }catch(Exception ex){File.WriteAllText("work/seated-camera-error.txt",ex.ToString());Debug.LogException(ex);}finally{SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}}
}
