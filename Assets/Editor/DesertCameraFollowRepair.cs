using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
public static class DesertCameraFollowRepair {
 const string Marker="work/camera-follow-repaired-v1.txt";
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Once;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Once;};}
 static void Once(){if(!EditorApplication.isPlayingOrWillChangePlaymode&&!File.Exists(Marker))Apply();}
 static void Apply(){var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath("Assets/Scenes/DesertOverview.unity");bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/DesertOverview.unity",OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
 try{var seq=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DesertCinematicSequence>(true)).Single();DesertSelectedCameraSetup.Apply();var report=new System.Text.StringBuilder();
 foreach(var d in new[]{seq.muflehTimeline,seq.saqeeraTimeline}){
 var timeline=(TimelineAsset)d.playableAsset;var track=timeline.GetRootTracks().OfType<DesertSelectedCameraTrack>().First();track.muted=false;var item=track.GetClips().Single();item.duration=timeline.duration;var asset=(DesertSelectedCameraClip)item.asset;asset.usePlacedAngle=false;
 var target=d.GetReferenceValue(asset.selectedCharacter.exposedName,out var valid) as Transform;if(!valid||!target)throw new Exception("Missing follow target");
 var group=timeline.GetRootTracks().OfType<GroupTrack>().Single(g=>g.name==target.name||g.name.StartsWith(target.name+" "));var body=group.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Body"));asset.seatedStartTime=(float)body.GetClips().First(c=>c.displayName.StartsWith("Sit")).start;EditorUtility.SetDirty(asset);
 d.RebuildGraph();Vector3 first=Vector3.zero;foreach(float time in new[]{0f,3f,6f,asset.seatedStartTime+2}){d.time=time;for(int i=0;i<4;i++)d.Evaluate();var camera=seq.cinematicCamera;float distance=Vector3.Distance(camera.transform.position,target.position);if(distance>4.5f)throw new Exception("Camera too far: "+target.name+" at "+time+" / "+distance);if(time==0)first=camera.transform.position;report.AppendLine(target.name+" time="+time.ToString("F2")+" camera distance="+distance.ToString("F2")+"m; camera="+camera.transform.position);if(time==6&&Vector3.Distance(first,camera.transform.position)<.5f)throw new Exception("Camera did not move with selected character");}d.Stop();
 }
 AssetDatabase.SaveAssets();var selected=DesertCharacterSelection.MuflehSelected?seq.muflehTimeline:seq.saqeeraTimeline;selected.time=0;selected.RebuildGraph();selected.Evaluate();selected.Evaluate();selected.Pause();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText(Marker,report.ToString());
 }catch(Exception ex){File.WriteAllText("work/camera-follow-error.txt",ex.ToString());Debug.LogException(ex);}finally{SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}}
}

