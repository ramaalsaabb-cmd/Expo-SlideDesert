using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Bake on the desktop so Android never has to cook non-readable tent meshes.
public static class KatheebTentLayoutBaker
{
    const string Output="Assets/Resources/KatheebUI/MenuTentLayout.json";
    [InitializeOnLoadMethod]
    static void Queue(){if(!File.Exists(Output))EditorApplication.delayCall+=Bake;}
    [MenuItem("Tools/Desert VR/Bake Menu Tent Entrance")]
    public static void Bake()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tent.prefab");
        if(!prefab)return;
        GameObject tent=null,helper=null;
        try
        {
            tent=Object.Instantiate(prefab);
            var shell=System.Array.Find(tent.GetComponentsInChildren<Transform>(true),t=>t!=tent.transform&&t.name=="Tent");
            if(!shell)throw new System.Exception("Tent architecture container missing.");
            bool found=false;Bounds local=default;
            foreach(var filter in shell.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.sharedMesh)continue;
                var renderer=filter.GetComponent<MeshRenderer>();if(!renderer)continue;
                var materials=renderer.sharedMaterials;
                for(int sub=0;sub<filter.sharedMesh.subMeshCount;sub++)
                {
                    var mat=sub<materials.Length?materials[sub]:null;
                    if(mat&&(mat.name.Contains("Strings")||mat.name.Contains("Stand")))continue;
                    var bounds=filter.sharedMesh.GetSubMesh(sub).bounds;
                    if(bounds.size.sqrMagnitude<.001f)bounds=filter.sharedMesh.bounds;
                    for(int i=0;i<8;i++)
                    {
                        var point=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                        point=shell.InverseTransformPoint(filter.transform.TransformPoint(point));
                        if(!found){local=new Bounds(point,Vector3.zero);found=true;}else local.Encapsulate(point);
                    }
                }
            }
            if(!found)throw new System.Exception("Tent cloth bounds missing.");
            float scale=3.6f/local.size.y;tent.transform.localScale*=scale;
            helper=new GameObject("Temporary menu entrance baker");helper.SetActive(false);
            var manager=helper.AddComponent<KatheebMenuManager>();
            object[] args={shell,local,null,null};
            typeof(KatheebMenuManager).GetMethod("FindTentDoorway",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(manager,args);
            var layout=new KatheebTentLayout{scale=scale,door=tent.transform.InverseTransformPoint((Vector3)args[2]),outward=tent.transform.InverseTransformDirection((Vector3)args[3]),shellFloor=new Vector3(local.center.x,local.min.y,local.center.z)};
            File.WriteAllText(Output,JsonUtility.ToJson(layout,true));AssetDatabase.ImportAsset(Output);
            Debug.Log("[KATHEEB] Saved deterministic menu doorway for desktop and Quest.");
        }
        catch(System.Exception ex){Debug.LogException(ex);}
        finally{if(helper)Object.DestroyImmediate(helper);if(tent)Object.DestroyImmediate(tent);}
    }
}
