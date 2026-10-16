using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public static class KatheebVRPointerRegression
{
    const string Report="Assets/KatheebMenu/VRPointerRegression.txt";
    [InitializeOnLoadMethod]
    static void Queue(){if(!File.Exists(Report))EditorApplication.delayCall+=Run;}
    [MenuItem("Tools/Desert VR/Test VR Character Pointer Geometry")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        GameObject root=null;
        try
        {
            root=new GameObject("Temporary VR pointer test");root.SetActive(false);
            var manager=root.AddComponent<KatheebMenuManager>();
            var camera=new GameObject("Test camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.transform.position=new Vector3(0,1.35f,0);manager.playerCamera=camera;
            var canvas=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas));canvas.transform.SetParent(root.transform,false);canvas.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            canvas.transform.position=new Vector3(0,1.35f,2.4f);canvas.transform.localScale=Vector3.one*.0015f;
            var group=new GameObject("Selection",typeof(RectTransform),typeof(CanvasGroup));group.transform.SetParent(canvas.transform,false);
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            typeof(KatheebMenuManager).GetField("canvasObject",flags).SetValue(manager,canvas);
            typeof(KatheebMenuManager).GetField("selectionUI",flags).SetValue(manager,group.GetComponent<CanvasGroup>());
            var sync=typeof(KatheebMenuManager).GetMethod("SyncCharacterUI",flags);
            var intersect=typeof(TrackedDeviceGraphicRaycaster).GetMethod("RayIntersectsRectTransform",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(RectTransform),typeof(Vector4),typeof(Ray),typeof(Vector3).MakeByRefType(),typeof(float).MakeByRefType()},null);
            var hits=new RectTransform[2];
            for(int i=0;i<2;i++)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KatheebMenu/Prefabs/"+(i==0?"Mufleh":"Saqeera")+"Menu.prefab");
                var actor=Object.Instantiate(prefab,root.transform);actor.transform.position=new Vector3(i==0?-.62f:.62f,0,2.5f);actor.transform.localScale=Vector3.one*1.1f;
                hits[i]=new GameObject("Body pointer target",typeof(RectTransform)).GetComponent<RectTransform>();hits[i].SetParent(group.transform,false);
                sync.Invoke(manager,new object[]{actor.GetComponent<KatheebCharacterInteractable>(),hits[i],"unused"});
            }
            int checks=0;
            foreach(var hand in new[]{new Vector3(-.3f,1.05f,0),new Vector3(.3f,1.05f,0)})
                for(int i=0;i<2;i++)foreach(float height in new[]{-.35f,0,.35f})
                {
                    var aim=hits[i].position+Vector3.up*(hits[i].rect.height*hits[i].lossyScale.y*height);
                    var ray=new Ray(hand,aim-hand);
                    object[] args={hits[i],Vector4.zero,ray,Vector3.zero,0f};
                    if(!(bool)intersect.Invoke(null,args))throw new System.Exception("Hand ray missed intended body target.");
                    args[0]=hits[1-i];
                    if((bool)intersect.Invoke(null,args))throw new System.Exception("Body targets overlap for controller ray.");
                    checks++;
                }
            File.WriteAllText(Report,"PASS: "+checks+" left/right controller-origin rays hit the intended character target and miss the other, using XRI's actual rectangle-ray intersection. Synthetic geometry check; Quest trigger test still required.\n");
            Debug.Log("[KATHEEB] VR pointer geometry regression passed.");
        }
        catch(System.Exception ex){File.WriteAllText(Report,"FAIL: "+ex);Debug.LogException(ex);}
        finally{if(root)Object.DestroyImmediate(root);}
    }
}
