using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.XR;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

[Serializable]
public class DesertSelectedCameraClip : PlayableAsset, ITimelineClipAsset
{
    public ExposedReference<Transform> selectedCharacter;
    public ExposedReference<Transform> cameraRig;
    public float distance = 2.6f;
    public ClipCaps clipCaps => ClipCaps.None;
    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable=ScriptPlayable<DesertSelectedCameraBehaviour>.Create(graph);
        var behaviour=playable.GetBehaviour();
        behaviour.target=selectedCharacter.Resolve(graph.GetResolver());
        behaviour.rig=cameraRig.Resolve(graph.GetResolver());
        behaviour.distance=distance;
        return playable;
    }
}
public class DesertSelectedCameraBehaviour : PlayableBehaviour
{
    public Transform target, rig;
    public float distance;
    bool loggedTarget;
    DesertCameraCollision collision;
    public override void OnPlayableDestroy(Playable playable)
    {
        collision?.Dispose();collision=null;
    }
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var camera=playerData as Camera;
        if(!target || !rig || !camera)return;
        Vector3 focus=target.position+Vector3.up;
        bool found=false;Bounds bounds=default;
        foreach(var renderer in target.GetComponentsInChildren<Renderer>(false))
        {
            if(!(renderer is SkinnedMeshRenderer || renderer is MeshRenderer)||!renderer.enabled)continue;
            if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);
        }
        if(found)focus=bounds.center;
        Vector3 forward=Vector3.ProjectOnPlane(target.forward,Vector3.up).normalized;
        if(forward.sqrMagnitude<0.1f)forward=Vector3.forward;
        if(collision==null)collision=new DesertCameraCollision(target.gameObject.scene);
        float radius=Mathf.Max(.22f,camera.nearClipPlane*1.5f);
        Vector3 eye=collision.Resolve(focus,focus-forward*distance+Vector3.up*0.45f,radius,target,rig);
        Quaternion look=Quaternion.LookRotation((focus-eye).normalized,Vector3.up);
        Vector3 localEye=rig.InverseTransformPoint(camera.transform.position);
        Quaternion localRotation=Quaternion.Inverse(rig.rotation)*camera.transform.rotation;
        Quaternion rotation=XRSettings.isDeviceActive?look:look*Quaternion.Inverse(localRotation);
        Vector3 position=eye-rotation*Vector3.Scale(localEye,rig.lossyScale);
        float blend=!Application.isPlaying||info.deltaTime<=0?1f:1-Mathf.Exp(-10f*(float)info.deltaTime);
        rig.SetPositionAndRotation(Vector3.Lerp(rig.position,position,blend),Quaternion.Slerp(rig.rotation,rotation,blend));
        // Recheck the actual lens after smoothing and XR head offset, not only the rig's destination.
        Vector3 correctedEye=collision.Resolve(focus,camera.transform.position,radius,target,rig);
        rig.position+=correctedEye-camera.transform.position;
        camera.enabled=true;
        if(Application.isPlaying && !loggedTarget)
        {
            Debug.Log("[Cinematic Camera] Following actual selected character: " + target.name + "; camera=" + camera.name + "; target position=" + target.position, camera);
            loggedTarget=true;
        }
    }
}

// Query geometry is disposable and never serialized into the user's scene.
internal sealed class DesertCameraCollision : IDisposable
{
    readonly List<GameObject> geometry=new List<GameObject>();
    public DesertCameraCollision(Scene scene)
    {
        foreach(var root in scene.GetRootGameObjects())
        {
            if(root.name!="Tent"&&root.name!="CarRoot"&&root.name!="SledRoot")continue;
            foreach(var mesh in root.GetComponentsInChildren<MeshFilter>(false))
            {
                string label=mesh.name.ToLowerInvariant();
                if(!mesh.sharedMesh||label.Contains("lod1")||label.Contains("lod2")||label.Contains("lod3")||label.Contains("decal"))continue;
                var renderer=mesh.GetComponent<MeshRenderer>();if(!renderer||!renderer.enabled)continue;
                var obj=new GameObject("Camera clearance: "+mesh.name);obj.hideFlags=HideFlags.HideAndDontSave;
                SceneManager.MoveGameObjectToScene(obj,scene);geometry.Add(obj);
                obj.transform.SetPositionAndRotation(mesh.transform.position,mesh.transform.rotation);obj.transform.localScale=mesh.transform.lossyScale;
                obj.AddComponent<MeshCollider>().sharedMesh=mesh.sharedMesh;
            }
        }
        Physics.SyncTransforms();
    }
    static bool Relevant(Collider collider,Transform target,Transform rig)
    {
        if(!collider||collider.isTrigger||collider.transform.IsChildOf(target)||collider.transform.IsChildOf(rig))return false;
        // The invisible driver wrappers must not make furniture invisible to the camera checks.
        var animator=collider.GetComponentInParent<Animator>();
        if(animator&&animator.GetComponentInChildren<SkinnedMeshRenderer>())return false;
        return true;
    }
    static bool Free(Vector3 p,float radius,Transform target,Transform rig)
    {
        foreach(var collider in Physics.OverlapSphere(p,radius,~0,QueryTriggerInteraction.Ignore))
            if(Relevant(collider,target,rig))return false;
        return true;
    }
    public Vector3 Resolve(Vector3 focus,Vector3 desired,float radius,Transform target,Transform rig)
    {
        Vector3 anchor=focus;
        if(!Free(anchor,radius,target,rig))
        {
            bool found=false;
            foreach(var offset in new[]{Vector3.up*.35f,Vector3.up*.7f,Vector3.right*.45f,Vector3.left*.45f,Vector3.forward*.45f,Vector3.back*.45f})
                if(Free(focus+offset,radius,target,rig)){anchor=focus+offset;found=true;break;}
            if(!found)return focus;
        }
        Vector3 delta=desired-anchor;float length=delta.magnitude;
        if(length<.001f)return anchor;
        float allowed=length;
        foreach(var hit in Physics.SphereCastAll(anchor,radius,delta/length,length,~0,QueryTriggerInteraction.Ignore))
            if(Relevant(hit.collider,target,rig))allowed=Mathf.Min(allowed,Mathf.Max(0,hit.distance-.06f));
        Vector3 result=anchor+delta/length*allowed;
        // Casts can miss an initially overlapping or very thin two-sided surface.
        for(int step=0;step<20&&!Free(result,radius,target,rig);step++)
        {
            allowed=Mathf.Max(0,allowed-.12f);result=anchor+delta/length*allowed;
        }
        return Free(result,radius,target,rig)?result:anchor;
    }
    public void Dispose()
    {
        foreach(var obj in geometry)if(obj)
        {
            if(Application.isPlaying)UnityEngine.Object.Destroy(obj);else UnityEngine.Object.DestroyImmediate(obj);
        }
        geometry.Clear();
    }
}
