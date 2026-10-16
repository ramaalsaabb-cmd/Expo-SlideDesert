using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class DesertGentleDrivingRoute {
 const string Marker="work/gentle-driving-route-v1.txt";
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Once;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Once;};}
 static void Once(){if(!EditorApplication.isPlayingOrWillChangePlaymode&&!File.Exists(Marker))Apply();}
 [MenuItem("Tools/Desert VR/20 Gentle Driving Route Points")]
 static void Apply(){var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath("Assets/Scenes/Demo.unity");bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/Demo.unity",OpenSceneMode.Additive);try{
 var car=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CarPathFollower>(true)).Single();var root=scene.GetRootGameObjects().Single(g=>g.name=="CarPath").transform;var originals=car.points.Where(p=>p).ToArray();if(originals.Length<2)throw new Exception("Driving route needs start and finish");
 Directory.CreateDirectory("work/GentleDrivingRouteBackup");if(!File.Exists("work/GentleDrivingRouteBackup/Demo.unity"))EditorSceneManager.SaveScene(scene,"work/GentleDrivingRouteBackup/Demo.unity",true);
 var start=originals.First().position;var end=originals.Last().position;var forward=Vector3.ProjectOnPlane(end-start,Vector3.up).normalized;var side=Vector3.Cross(Vector3.up,forward).normalized;const int count=20;var points=new Transform[count];
 for(int i=0;i<count;i++){float t=i/(float)(count-1);var point=i<originals.Length?originals[i]:new GameObject("RoutePoint "+(i+1).ToString("00")).transform;if(i>=originals.Length)point.SetParent(root,false);Undo.RecordObject(point,"Distribute gentle driving points");point.position=Vector3.Lerp(start,end,t)+side*(3.5f*Mathf.Sin(i*Mathf.PI/2)*Mathf.Sin(Mathf.PI*t));point.name="RoutePoint "+(i+1).ToString("00");point.SetSiblingIndex(i);EditorUtility.SetDirty(point);points[i]=point;}
 points[0].position=start;points[count-1].position=end;Undo.RecordObject(car,"Bind twenty driving route points");car.points=points;EditorUtility.SetDirty(car);
 float minSpacing=float.PositiveInfinity,maxTurn=0;int straightRun=0,maxStraightRun=0;
 for(int i=1;i<count;i++){minSpacing=Mathf.Min(minSpacing,Vector3.ProjectOnPlane(points[i].position-points[i-1].position,Vector3.up).magnitude);if(i<count-1){float angle=Vector3.Angle(Vector3.ProjectOnPlane(points[i].position-points[i-1].position,Vector3.up),Vector3.ProjectOnPlane(points[i+1].position-points[i].position,Vector3.up));maxTurn=Mathf.Max(maxTurn,angle);straightRun=angle<1f?straightRun+1:0;maxStraightRun=Mathf.Max(maxStraightRun,straightRun);}}
 if(car.points.Length!=20||minSpacing<10||maxTurn>25||maxStraightRun>2)throw new Exception("Route spacing or turn validation failed");if(Vector3.Distance(points[0].position,start)>.001||Vector3.Distance(points[19].position,end)>.001)throw new Exception("Route endpoints moved");
 EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();File.WriteAllText(Marker,"20 connected route points; endpoints preserved; minimum spacing="+minSpacing.ToString("F2")+"m; maximum anchor turn="+maxTurn.ToString("F2")+" degrees; longest straight run="+maxStraightRun+" intermediate points; existing smooth spline follower retained.");Selection.activeGameObject=root.gameObject;SceneView.RepaintAll();
 }catch(Exception ex){File.WriteAllText("work/gentle-driving-route-error.txt",ex.ToString());Debug.LogException(ex);}finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}}
}
