using UnityEditor;
using UnityEngine;
using System.IO;
public static class RiderFeetPreview {
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Run;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Run;};}
 static void Run(){if(!File.Exists("work/original-character-feet-v1.txt")){EditorApplication.delayCall+=Run;return;}if(EditorApplication.isPlayingOrWillChangePlaymode||File.Exists("work/rider-original-feet-v10.txt"))return;foreach(var name in new[]{"Desert_Robe_Character_Sled","Traditional_Woman_Sled"}){var g=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/"+name+".glb"));foreach(var riderNode in g.GetComponentsInChildren<Transform>(true))if(riderNode.name=="Desert_Robe_Character_ROOT"||riderNode.name=="Traditional_Woman_ROOT")SledVisibleFeetSetup.Attach(riderNode.gameObject,riderNode.name=="Traditional_Woman_ROOT");g.transform.position=new Vector3(0,1000,0);var bounds=new Bounds(g.transform.position,Vector3.zero);bool first=true;foreach(var r in g.GetComponentsInChildren<Renderer>()){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}var c=new GameObject("preview").AddComponent<Camera>();c.transform.position=bounds.center+new Vector3(1.3f,.9f,1.8f);c.transform.LookAt(bounds.center);c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=Color.gray;c.nearClipPlane=.01f;var rt=new RenderTexture(800,800,24);c.targetTexture=rt;c.Render();var old=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(800,800,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,800,800),0,0);t.Apply();File.WriteAllBytes("work/"+name+"-feet.png",t.EncodeToPNG());RenderTexture.active=old;Object.DestroyImmediate(t);Object.DestroyImmediate(c.gameObject);rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(g);}File.WriteAllText("work/rider-original-feet-v10.txt","done");}
}










