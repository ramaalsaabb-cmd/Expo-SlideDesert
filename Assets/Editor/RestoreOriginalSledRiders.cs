using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class RestoreOriginalSledRiders {
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Run;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Run;};}
 static void Run(){if(EditorApplication.isPlayingOrWillChangePlaymode||File.Exists("work/original-sled-riders-restored-v1.txt"))return;try{var scene=SceneManager.GetSceneByPath("Assets/Scenes/Demo.unity");bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/Demo.unity",OpenSceneMode.Additive);var report=new System.Text.StringBuilder();foreach(var rider in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray()){bool woman=rider.name=="Traditional_Woman_ROOT";if(!woman&&rider.name!="Desert_Robe_Character_ROOT")continue;foreach(string name in new[]{"Visible sandal feet","Original character feet"}){var added=rider.Find(name);if(added)added.gameObject.SetActive(false);}var body=rider.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name.EndsWith("_Seated_Static"));var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/"+(woman?"Traditional_Woman_Sled":"Desert_Robe_Character_Sled")+".glb").GetComponentsInChildren<MeshFilter>(true).First(f=>f.name.EndsWith("_Seated_Static"));body.sharedMesh=original.sharedMesh;EditorUtility.SetDirty(body);report.AppendLine(rider.name+": original complete seated mesh restored; added feet disabled");}EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed");if(opened)EditorSceneManager.CloseScene(scene,true);File.WriteAllText("work/original-sled-riders-restored-v1.txt",report.ToString());}catch(Exception e){Debug.LogException(e);File.WriteAllText("work/original-sled-riders-restore-error.txt",e.ToString());}}
}
