using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class SledVisibleFeetSetup {
 const string Folder="Assets/Characters/SledFeet";
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Run;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Run;};}
 public static void Attach(GameObject root,bool woman){
 if(ActualRiderFeetSetup.Attach(root,woman))return;
 var placed=root.transform.Find("Visible sandal feet");
 if(!placed){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/VisibleFeet.prefab");if(!prefab)return;var feet=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root.scene);feet.name="Visible sandal feet";placed=feet.transform;placed.SetParent(root.transform,false);}
 // Soles sit at the original sled deck height, inside its front rim.
 placed.localPosition=woman?new Vector3(0,.007f,.025f):new Vector3(0,0,-.015f);
 placed.localRotation=Quaternion.identity;
 placed.localScale=woman?new Vector3(.80f,.88f,.88f):new Vector3(1.04f,1, .98f);
 foreach(var renderer in placed.GetComponentsInChildren<MeshRenderer>())if(renderer.name.Contains("leather")){
 string materialPath=Folder+(woman?"/SaqeeraSandals.mat":"/MuflehSandals.mat");var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(!mat){mat=new Material(renderer.sharedMaterial);mat.name=woman?"Saqeera dark sandals":"Mufleh brown sandals";var tint=woman?new Color(.42f,.24f,.23f):new Color(.80f,.64f,.46f);if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",tint);AssetDatabase.CreateAsset(mat,materialPath);}renderer.sharedMaterial=mat;
 }
 // Lift and draw back only the low front hem so it reveals the feet on the deck.
 var body=root.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f=>f.name.EndsWith("_Seated_Static"));if(body){string path=Folder+(woman?"/SaqeeraSeatedHemV2.asset":"/MuflehSeatedHemV2.asset");var adjusted=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(!adjusted){var originalBody=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/"+(woman?"Traditional_Woman_Sled":"Desert_Robe_Character_Sled")+".glb").GetComponentsInChildren<MeshFilter>(true).First(f=>f.name.EndsWith("_Seated_Static"));adjusted=UnityEngine.Object.Instantiate(originalBody.sharedMesh);var vertices=adjusted.vertices;for(int i=0;i<vertices.Length;i++){var v=root.transform.InverseTransformPoint(body.transform.TransformPoint(vertices[i]));float weight=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.20f,.70f,v.y)))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.24f,.46f,v.z));v.z=Mathf.Lerp(v.z,Mathf.Min(v.z,.26f),weight);v.y+=weight*.045f;vertices[i]=body.transform.InverseTransformPoint(root.transform.TransformPoint(v));}adjusted.vertices=vertices;adjusted.RecalculateBounds();adjusted.RecalculateNormals();AssetDatabase.CreateAsset(adjusted,path);}body.sharedMesh=adjusted;}
 }
 static void Run(){if(EditorApplication.isPlayingOrWillChangePlaymode||File.Exists("work/sled-deck-feet-distinct-v6.txt"))return;try{
 Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sled_Sandal_Rider.glb"));var container=new GameObject("Visible sandal feet");
 foreach(var filter in source.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="Rider_Brown embroidered sandal leather"||f.name=="Rider_Warm medium skin")){
 var original=filter.sharedMesh;var vertices=original.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=source.transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
 var mesh=UnityEngine.Object.Instantiate(original);mesh.name=filter.name+" Feet";mesh.vertices=vertices;
 for(int sub=0;sub<mesh.subMeshCount;sub++){var all=original.GetTriangles(sub);var kept=new List<int>();for(int i=0;i<all.Length;i+=3)if(vertices[all[i]].y<.215f&&vertices[all[i+1]].y<.215f&&vertices[all[i+2]].y<.215f&&vertices[all[i]].z>.28f&&vertices[all[i+1]].z>.28f&&vertices[all[i+2]].z>.28f&&(!filter.name.Contains("skin")||(vertices[all[i]].z>.40f&&vertices[all[i+1]].z>.40f&&vertices[all[i+2]].z>.40f&&Mathf.Abs(vertices[all[i]].x)<.17f&&Mathf.Abs(vertices[all[i+1]].x)<.17f&&Mathf.Abs(vertices[all[i+2]].x)<.17f&&vertices[all[i]].y<.19f&&vertices[all[i+1]].y<.19f&&vertices[all[i+2]].y<.19f))){kept.Add(all[i]);kept.Add(all[i+1]);kept.Add(all[i+2]);}mesh.SetTriangles(kept,sub);}
 mesh.RecalculateBounds();mesh.RecalculateNormals();string path=Folder+"/"+(filter.name.Contains("skin")?"FeetSkin":"Sandals")+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;}else AssetDatabase.CreateAsset(mesh,path);var child=new GameObject(filter.name);child.transform.SetParent(container.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterials=filter.GetComponent<MeshRenderer>().sharedMaterials;
 }
 PrefabUtility.SaveAsPrefabAsset(container,Folder+"/VisibleFeet.prefab");UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(container);
 var previous=SceneManager.GetActiveScene();var report=new System.Text.StringBuilder();foreach(var label in new[]{"Demo","DesertOverview"}){var scene=SceneManager.GetSceneByPath("Assets/Scenes/"+label+".unity");bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/"+label+".unity",OpenSceneMode.Additive);foreach(var t in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray())if(t.name=="Desert_Robe_Character_ROOT"||t.name=="Traditional_Woman_ROOT"){Attach(t.gameObject,t.name=="Traditional_Woman_ROOT");report.AppendLine(label+": "+t.name+" sandal feet visible, existing body pose retained");}EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);if(opened)EditorSceneManager.CloseScene(scene,true);}if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);AssetDatabase.SaveAssets();File.WriteAllText("work/sled-deck-feet-distinct-v6.txt",report.ToString());
 }catch(Exception e){Debug.LogException(e);File.WriteAllText("work/sled-visible-feet-error.txt",e.ToString());}}
}






