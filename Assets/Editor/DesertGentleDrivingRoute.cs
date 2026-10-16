using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class DesertGentleDrivingRoute {
 const string Marker="work/varied-wide-driving-route-v2.txt";
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Once;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Once;};}
 static void Once(){if(!EditorApplication.isPlayingOrWillChangePlaymode&&!File.Exists(Marker))Apply();}
 [MenuItem("Tools/Desert VR/20 Gentle Driving Route Points")]
 static void Apply(){var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath("Assets/Scenes/Demo.unity");bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/Demo.unity",OpenSceneMode.Additive);try{
 var car=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CarPathFollower>(true)).Single();var root=scene.GetRootGameObjects().Single(g=>g.name=="CarPath").transform;var originals=car.points.Where(p=>p).ToArray();if(originals.Length<2)throw new Exception("Driving route needs start and finish");
 Directory.CreateDirectory("work/VariedWideRouteBackup");if(!File.Exists("work/VariedWideRouteBackup/Demo.unity"))EditorSceneManager.SaveScene(scene,"work/VariedWideRouteBackup/Demo.unity",true);
 var start=originals.First().position;var oldEnd=originals.Last().position;var forward=Vector3.ProjectOnPlane(oldEnd-start,Vector3.up).normalized;const int count=20;var points=new Transform[count];
 var terrains=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>(true)).Where(t=>t.terrainData).ToArray();
 Func<Vector3,bool> inside=p=>terrains.Length==0||terrains.Any(t=>{var local=p-t.transform.position;var size=t.terrainData.size;return local.x>=4&&local.z>=4&&local.x<=size.x-4&&local.z<=size.z-4;});
 File.WriteAllText("work/driving-terrain-bounds.txt","Start="+start+" oldEnd="+oldEnd+"\n"+string.Join("\n",terrains.Select(t=>t.name+" origin="+t.transform.position+" size="+t.terrainData.size)));Vector3[] layout=null;float spacing=32f;float baseYaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;float[] bends={0,12,0,-28,-16,-4,-16,-28,-16,-4,-16,9,34,59,84,109,134,159,180};
 foreach(float candidateSpacing in new[]{32f,30f}){foreach(float side in new[]{1f,-1f}){var candidate=new Vector3[count];candidate[0]=start;bool valid=inside(start);for(int i=1;i<count;i++){float yaw=baseYaw+side*bends[i-1];candidate[i]=candidate[i-1]+Quaternion.Euler(0,yaw,0)*Vector3.forward*candidateSpacing;candidate[i].y=start.y;valid&=inside(candidate[i]);}if(valid){layout=candidate;spacing=candidateSpacing;break;}}if(layout!=null)break;}
 if(layout==null)throw new Exception("Cannot fit twenty widely spaced route points inside the existing terrain.");
 for(int i=0;i<count;i++){var point=i<originals.Length?originals[i]:new GameObject("RoutePoint "+(i+1).ToString("00")).transform;if(i>=originals.Length)point.SetParent(root,false);Undo.RecordObject(point,"Space and vary driving route turns");point.position=layout[i];point.name="RoutePoint "+(i+1).ToString("00");point.SetSiblingIndex(i);EditorUtility.SetDirty(point);points[i]=point;}
 Undo.RecordObject(car,"Bind twenty varied driving route points");car.points=points;EditorUtility.SetDirty(car);
 float minSpacing=float.PositiveInfinity,maxTurn=0;int straightRun=0,maxStraightRun=0;
 for(int i=1;i<count;i++){minSpacing=Mathf.Min(minSpacing,Vector3.ProjectOnPlane(points[i].position-points[i-1].position,Vector3.up).magnitude);if(i<count-1){float angle=Vector3.Angle(Vector3.ProjectOnPlane(points[i].position-points[i-1].position,Vector3.up),Vector3.ProjectOnPlane(points[i+1].position-points[i].position,Vector3.up));maxTurn=Mathf.Max(maxTurn,angle);straightRun=angle<1f?straightRun+1:0;maxStraightRun=Mathf.Max(maxStraightRun,straightRun);}}
 if(car.points.Length!=20||minSpacing<29||maxTurn>35||maxStraightRun>2)throw new Exception("Route spacing or turn validation failed");if(Vector3.Distance(points[0].position,start)>.001)throw new Exception("Route start moved");
 EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();File.WriteAllText(Marker,"20 connected route points; start preserved; route length increased for wider spacing; minimum spacing="+minSpacing.ToString("F2")+"m; maximum anchor turn="+maxTurn.ToString("F2")+" degrees; longest straight run="+maxStraightRun+" intermediate points; light and stronger turns alternate left and right; all anchors inside terrain; existing smooth spline follower retained.");Selection.activeGameObject=root.gameObject;SceneView.RepaintAll();
 }catch(Exception ex){File.WriteAllText("work/gentle-driving-route-error.txt",ex.ToString());Debug.LogException(ex);}finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}}
}



