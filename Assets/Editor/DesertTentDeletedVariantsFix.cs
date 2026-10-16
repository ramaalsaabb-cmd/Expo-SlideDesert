using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class DesertTentDeletedVariantsFix {
 const string Marker="work/tent-deleted-variants-fixed-v1.txt";
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Run;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Run;};}
 static void Run(){if(EditorApplication.isPlayingOrWillChangePlaymode||File.Exists(Marker))return;var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath("Assets/Scenes/DesertOverview.unity");bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/DesertOverview.unity",OpenSceneMode.Additive);try{
 var tent=scene.GetRootGameObjects().Single(g=>g.name=="Tent");var report=new System.Text.StringBuilder();int count=0;Directory.CreateDirectory("work/TentDeletedVariantsBackup");if(!File.Exists("work/TentDeletedVariantsBackup/DesertOverview.unity"))File.Copy(scene.path,"work/TentDeletedVariantsBackup/DesertOverview.unity");
 foreach(var lod in tent.GetComponentsInChildren<LODGroup>(true)){
 var levels=lod.GetLODs();if(levels.Length==0)continue;
 var source=PrefabUtility.GetCorrespondingObjectFromSource(lod);var sourceLevels=source?source.GetLODs():null;
 bool removed=false;for(int i=0;i<levels.Length;i++){int actual=levels[i].renderers.Count(r=>r);int expected=sourceLevels!=null&&i<sourceLevels.Length?sourceLevels[i].renderers.Count(r=>r):levels[i].renderers.Length;if(actual<expected){removed=true;break;}}
 if(!removed)continue;
 Undo.RecordObject(lod.gameObject,"Keep deleted tent prop hidden at every LOD");
 foreach(var renderer in levels.SelectMany(l=>l.renderers).Where(r=>r).Distinct()){
 Undo.RecordObject(renderer,"Hide remaining deleted prop variant");renderer.enabled=false;EditorUtility.SetDirty(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
 }
 lod.gameObject.SetActive(false);EditorUtility.SetDirty(lod.gameObject);PrefabUtility.RecordPrefabInstancePropertyModifications(lod.gameObject);
 for(int i=0;i<levels.Length;i++){lod.ForceLOD(i);if(levels[i].renderers.Any(r=>r&&r.enabled&&r.gameObject.activeInHierarchy))throw new Exception("Deleted prop still visible: "+lod.name+" LOD"+i);}lod.ForceLOD(-1);
 report.AppendLine(AnimationUtility.CalculateTransformPath(lod.transform,tent.transform)+": all "+levels.Length+" display levels hidden; removed meshes retained as removed.");count++;
 }
 if(count==0)throw new Exception("No scene-specific deleted LOD renderers found; no objects changed.");
 EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();report.Insert(0,"Fixed "+count+" deleted props. Verified every LOD remains hidden; intact props retain normal LOD switching.\n");File.WriteAllText(Marker,report.ToString());Debug.Log(report.ToString());
 }catch(Exception ex){File.WriteAllText("work/tent-deleted-variants-error.txt",ex.ToString());Debug.LogException(ex);}finally{SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}}
}
