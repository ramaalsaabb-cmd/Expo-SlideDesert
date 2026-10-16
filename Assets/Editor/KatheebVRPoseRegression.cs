using System.IO;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class KatheebVRPoseRegression
{
    const string Report="Assets/KatheebMenu/VRPoseRegression.txt";
    [InitializeOnLoadMethod]
    static void Queue(){if(!File.Exists(Report)||!File.Exists("Assets/KatheebMenu/DesktopVRParityRegression.txt"))EditorApplication.delayCall+=Run;}
    [MenuItem("Tools/Desert VR/Test VR Camera Offset Recovery")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        GameObject root=null;
        try
        {
            root=new GameObject("Temporary VR offset regression");root.SetActive(false);
            root.transform.SetPositionAndRotation(new Vector3(-24,8,-22),Quaternion.Euler(13,134,0));
            var offset=new GameObject("Camera Offset").transform;offset.SetParent(root.transform,false);offset.localPosition=Vector3.up*1.65f;
            var lens=new GameObject("Tracked camera").AddComponent<Camera>();lens.transform.SetParent(offset,false);
            var follow=new DesertSelectedCameraBehaviour{rig=root.transform};
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var type=typeof(DesertSelectedCameraBehaviour);
            var desired=new Vector3(70,1.5f,34);var yaw=Quaternion.Euler(0,20,0);
            type.GetField("trackedCamera",flags).SetValue(follow,lens);
            type.GetField("vrEye",flags).SetValue(follow,desired);
            type.GetField("vrRotation",flags).SetValue(follow,yaw);
            type.GetField("vrPoseReady",flags).SetValue(follow,true);
            foreach(var head in new[]{new Vector3(-25.86f,.8f,20.15f),new Vector3(.08f,1.6f,.12f),new Vector3(-.12f,1.3f,-.05f)})
            {
                lens.transform.localPosition=head;lens.transform.localRotation=Quaternion.Euler(15,35,4);
                var before=lens.transform.localRotation;
                type.GetMethod("ApplyVRPose",flags).Invoke(follow,null);
                if(Vector3.Distance(lens.transform.position,desired)>.001f)throw new System.Exception("VR eye moved away from the close follow anchor.");
                if(lens.transform.localPosition!=head||Quaternion.Angle(before,lens.transform.localRotation)>.001f)throw new System.Exception("Tracked head pose was overwritten.");
            }
            foreach(var path in new[]{"Assets/Cinematics/Mufleh Selected.playable","Assets/Cinematics/Saqeera Selected.playable"})
            {
                var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<DesertSelectedCameraClip>().Single();
                var desktopEye=clip.placedRigOffset+clip.placedRigRotation*new Vector3(-25.86f,2.45f,20.15f);
                if(!clip.hasPlacedEyeOffset||Vector3.Distance(desktopEye,clip.placedEyeOffset)>.001f)
                    throw new System.Exception("Desktop/VR lens framing differs: "+path);
                type.GetField("vrEye",flags).SetValue(follow,clip.placedEyeOffset);
                type.GetField("vrRotation",flags).SetValue(follow,clip.placedRigRotation);
                lens.transform.localPosition=new Vector3(.1f,1.6f,-.12f);
                var trackedRotation=lens.transform.localRotation;
                type.GetMethod("ApplyVRPose",flags).Invoke(follow,null);
                if(Vector3.Distance(lens.transform.position,desktopEye)>.001f||Quaternion.Angle(trackedRotation,lens.transform.localRotation)>.001f)
                    throw new System.Exception("Authored framing changed headset tracking: "+path);
            }
            File.WriteAllText("Assets/KatheebMenu/DesktopVRParityRegression.txt","PASS: Both character branches share the original desktop lens offset within 1 mm; VR tracking retains local head rotation. Synthetic Editor test; headset visual/performance verification still required.\n");
            File.WriteAllText(Report,"PASS: authored 33 m camera-local offset and two tracked headset poses recover to the same eye anchor; headset local position/rotation preserved. Synthetic Editor test, not a physical Quest test.\n");
            Debug.Log("[KATHEEB] VR camera offset recovery regression passed.");
        }
        catch(System.Exception ex){File.WriteAllText(Report,"FAIL: "+ex);Debug.LogException(ex);}
        finally{if(root)Object.DestroyImmediate(root);}
    }
}
