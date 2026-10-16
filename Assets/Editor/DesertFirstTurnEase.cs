using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class DesertFirstTurnEase {
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Apply;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Apply;};}
 static void Apply(){if(EditorApplication.isPlayingOrWillChangePlaymode||File.Exists("work/first-turn-eased-50.txt"))return;var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath("Assets/Scenes/Demo.unity");bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/Demo.unity",OpenSceneMode.Additive);try{
 var car=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CarPathFollower>(true)).Single();var first=car.points[0];var second=car.points[1];var incoming=Vector3.ProjectOnPlane(first.position-car.transform.position,Vector3.up).normalized;var outgoing=Vector3.ProjectOnPlane(second.position-first.position,Vector3.up);float before=Vector3.SignedAngle(incoming,outgoing,Vector3.up);var adjusted=Quaternion.AngleAxis(Mathf.Sign(before)*50f,Vector3.up)*incoming*outgoing.magnitude;
 Undo.RecordObject(second,"Ease first driving turn");second.position=new Vector3(first.position.x+adjusted.x,second.position.y,first.position.z+adjusted.z);EditorUtility.SetDirty(second);float after=Vector3.Angle(incoming,Vector3.ProjectOnPlane(second.position-first.position,Vector3.up));EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText("work/first-turn-eased-50.txt","First turn eased from "+Mathf.Abs(before).ToString("F1")+" to "+after.ToString("F1")+" degrees; only RoutePoint 02 moved; original first segment length retained.");
 }finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}}
}


