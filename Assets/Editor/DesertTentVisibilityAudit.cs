using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class DesertTentVisibilityAudit {
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Run;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Run;};}
 static void Run(){if(EditorApplication.isPlayingOrWillChangePlaymode||File.Exists("work/tent-visibility-audit.txt"))return;var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath("Assets/Scenes/DesertOverview.unity");bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/DesertOverview.unity",OpenSceneMode.Additive);try{var tent=scene.GetRootGameObjects().Single(g=>g.name=="Tent");var report=new System.Text.StringBuilder();foreach(var lod in tent.GetComponentsInChildren<LODGroup>(true)){var levels=lod.GetLODs();report.AppendLine(AnimationUtility.CalculateTransformPath(lod.transform,tent.transform)+" active="+lod.gameObject.activeSelf+" enabled="+lod.enabled);for(int i=0;i<levels.Length;i++)report.AppendLine(" LOD"+i+" "+string.Join(", ",levels[i].renderers.Select(r=>!r?"MISSING":r.name+" active="+r.gameObject.activeSelf+" enabled="+r.enabled)));}File.WriteAllText("work/tent-visibility-audit.txt",report.ToString());}finally{SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}}
}
