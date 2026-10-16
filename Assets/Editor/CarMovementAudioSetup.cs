using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
public static class CarMovementAudioSetup {
 const string Marker="work/car-movement-audio-v2.txt";
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Once;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Once;};}
 static void Once(){if(!EditorApplication.isPlayingOrWillChangePlaymode&&!File.Exists(Marker))Apply();}
 static void Apply(){var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath("Assets/Scenes/Demo.unity");bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/Demo.unity",OpenSceneMode.Additive);try{
 var car=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CarPathFollower>(true)).Single();var clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Driving/CarDrifting.mp3");var video=AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Audio/Driving/DrivingAmbience.mp4");if(!clip||clip.length<=0||!video||video.audioTrackCount<1)throw new Exception("One of the supplied sound tracks failed to import");
 Directory.CreateDirectory("work/CarMovementAudioBackup");if(!File.Exists("work/CarMovementAudioBackup/Demo.unity"))EditorSceneManager.SaveScene(scene,"work/CarMovementAudioBackup/Demo.unity",true);
 var component=car.GetComponent<CarMovementAudio>();if(!component)component=Undo.AddComponent<CarMovementAudio>(car.gameObject);Undo.RecordObject(component,"Two quiet movement audio layers");component.movement=car;component.driftingSound=clip;component.ambienceSound=video;component.driftingVolume=.30f;component.ambienceVolume=.10f;EditorUtility.SetDirty(component);
 EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();File.WriteAllText(Marker,"Bound both supplied tracks to "+car.name+" in Demo. Drifting="+clip.length.ToString("F2")+"s; video sound="+video.length.ToString("F2")+"s, audio tracks="+video.audioTrackCount+". Volumes=20% drifting, 10% ambience; both loop, start together only at actual speed >0.08m/s, stop at rest. Video pixels are not displayed. Existing transition sound unchanged.");
 }catch(Exception ex){File.WriteAllText("work/car-movement-audio-error.txt",ex.ToString());Debug.LogException(ex);}finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}}
}


