using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class DesertMatchSceneSky {
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Apply;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Apply;};}
 static void Apply(){if(EditorApplication.isPlayingOrWillChangePlaymode||File.Exists("work/scene-sky-matched-v1.txt"))return;var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath("Assets/Scenes/Demo.unity");bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/Demo.unity",OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);try{var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath("6d9a3c5fc6a241ada5d4103add34dc39"));if(!material)throw new System.Exception("Overview skybox material missing");RenderSettings.skybox=material;EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText("work/scene-sky-matched-v1.txt","Demo skybox matches DesertOverview: "+AssetDatabase.GetAssetPath(material));}finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}}
}
