using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
public static class DesertNaturalSkySetup {
 const string Marker="work/blue-sunset-sun-v16.txt";
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Once;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Once;};}
 static void Once(){if(!EditorApplication.isPlayingOrWillChangePlaymode&&!File.Exists(Marker))Apply();}
 static void Preview(Scene scene,string label){
 var cameraObject=new GameObject("Temporary natural sky preview");SceneManager.MoveGameObjectToScene(cameraObject,scene);cameraObject.hideFlags=HideFlags.HideAndDontSave;var camera=cameraObject.AddComponent<Camera>();var texture=new RenderTexture(960,600,24);try{
 if(label=="Demo"){var car=scene.GetRootGameObjects().First(g=>g.name=="CarRoot").transform;camera.transform.position=car.position-car.forward*12+Vector3.up*3;camera.transform.LookAt(car.position+car.forward*35+Vector3.up*6);}else{camera.transform.position=new Vector3(69,3,27);camera.transform.LookAt(new Vector3(79,2,35));}
 camera.clearFlags=CameraClearFlags.Skybox;camera.fieldOfView=62;camera.nearClipPlane=.1f;camera.farClipPlane=1800;camera.targetTexture=texture;camera.Render();var active=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(960,600,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,600),0,0);image.Apply();File.WriteAllBytes("work/natural-sky-"+label+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=active;
 }finally{camera.targetTexture=null;texture.Release();UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(cameraObject);}}
 static void Apply(){var previous=SceneManager.GetActiveScene();var report=new System.Text.StringBuilder();try{
 var sky=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DesertOverviewSunset.mat");var shader=Shader.Find("Desert/Soft Cloud Sunset");var texture=AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/8K Skybox Pack Free/Skyboxes/Texture/sky-6.png");if(!sky||!shader||!texture)throw new Exception("Original sunset sky missing");sky.shader=shader;sky.shaderKeywords=new string[0];sky.SetTexture("_Tex",texture);sky.SetFloat("_Exposure",.48f);sky.SetColor("_CloudTint",new Color(1,.78f,.57f,1));sky.SetFloat("_CloudWhitening",.85f);sky.SetFloat("_CloudBrightness",.23f);sky.SetFloat("_Rotation",0);sky.SetColor("_Tint",new Color(.48f,.48f,.48f,.5f));EditorUtility.SetDirty(sky);
 var dust=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/dust.mat");if(dust){if(dust.HasProperty("_BaseColor"))dust.SetColor("_BaseColor",new Color(.64f,.53f,.42f,1));if(dust.HasProperty("_Color"))dust.SetColor("_Color",Color.white);EditorUtility.SetDirty(dust);}var sledDust=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/SledDust.mat");if(sledDust){sledDust.SetColor("_BaseColor",Color.white);EditorUtility.SetDirty(sledDust);}
 Directory.CreateDirectory("work/NaturalSkyBackup");foreach(var label in new[]{"Demo","DesertOverview"}){
 string path="Assets/Scenes/"+label+".unity";var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);try{
 string copy="work/NaturalSkyBackup/"+label+".unity";if(!File.Exists(copy))EditorSceneManager.SaveScene(scene,copy,true);
 if(label=="Demo"){var car=scene.GetRootGameObjects().First(g=>g.name=="CarRoot").transform;var direction=Vector3.ProjectOnPlane(car.forward,Vector3.up).normalized;direction.y=.12f;sky.SetVector("_SunDirection",new Vector4(direction.x,direction.y,direction.z,0));EditorUtility.SetDirty(sky);}
 RenderSettings.skybox=sky;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.25f,.255f,.28f);RenderSettings.ambientEquatorColor=new Color(.235f,.205f,.175f);RenderSettings.ambientGroundColor=new Color(.12f,.105f,.09f);RenderSettings.ambientIntensity=1;RenderSettings.reflectionIntensity=.38f;RenderSettings.flareStrength=0;RenderSettings.haloStrength=0;
 foreach(var sun in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true)).Where(l=>l.type==LightType.Directional)){sun.color=new Color(1,.86f,.70f);sun.intensity=.38f;sun.shadowStrength=.8f;sun.transform.rotation=Quaternion.Euler(25,-65,0);RenderSettings.sun=sun;EditorUtility.SetDirty(sun);EditorUtility.SetDirty(sun.transform);}
 Preview(scene,label);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);report.AppendLine(label+": original balanced sunset retained; unified blue dusk palette; low amber sunset sun added with compact soft edge; broad halo and grey band removed; faint cloud texture retained; environment lighting preserved; wheel and sled dust tinted muted warm sand; preview rendered.");
 }finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}}
 AssetDatabase.SaveAssets();File.WriteAllText(Marker,report.ToString());SceneView.RepaintAll();
 }catch(Exception ex){File.WriteAllText("work/natural-sky-error.txt",ex.ToString());Debug.LogException(ex);}finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}}
}















