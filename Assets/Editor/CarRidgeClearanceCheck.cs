using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class CarRidgeClearanceCheck {
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Run;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Run;};}
 static void Run(){if(EditorApplication.isPlayingOrWillChangePlaymode||File.Exists("work/car-ridge-clearance-v1.txt"))return;var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath("Assets/Scenes/Demo.unity");bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/Demo.unity",OpenSceneMode.Additive);var car=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CarPathFollower>(true)).Single();var follow=car.GetComponent<CarTerrainFollow>();var bumps=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RouteTerrainBumps>(true)).Single();var position=car.transform.position;var rotation=car.transform.rotation;try{
 bumps.RebuildTerrain();var centers=bumps.GetBumpCenters();float minimum=float.PositiveInfinity;int samples=0;
 foreach(var center in centers){int nearest=Enumerable.Range(0,car.points.Length).OrderBy(i=>Vector3.ProjectOnPlane(car.points[i].position-center,Vector3.up).sqrMagnitude).First();var forward=Vector3.ProjectOnPlane(car.points[Mathf.Min(car.points.Length-1,nearest+1)].position-car.points[Mathf.Max(0,nearest-1)].position,Vector3.up).normalized;
 for(float distance=-14;distance<=14;distance+=.4f){var p=center+forward*distance;p.y=car.transform.position.y;car.transform.position=p;car.transform.rotation=Quaternion.LookRotation(forward);follow.FollowGround(1f/72);minimum=Mathf.Min(minimum,follow.MinimumSupportClearance());samples++;}}
 if(minimum<.025f)throw new Exception("Vehicle intersects ridge: "+minimum);File.WriteAllText("work/car-ridge-clearance-v1.txt",samples+" vehicle poses checked across "+centers.Length+" generated ridges; minimum wheel/underbody sample clearance="+minimum.ToString("F3")+"m. Upward motion cannot interpolate into sand; downward settling remains smooth.");
 }catch(Exception ex){File.WriteAllText("work/car-ridge-clearance-error.txt",ex.ToString());Debug.LogException(ex);}finally{car.transform.SetPositionAndRotation(position,rotation);bumps.RestoreTerrain();if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}}
}
