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
    public float seatedStartTime = -1f;
    public float seatedTurnDuration = 1.4f;
    public bool usePlacedAngle;
    public Vector3 placedRigOffset;
    public Quaternion placedRigRotation=Quaternion.identity;
    public bool hasPlacedEyeOffset;
    public Vector3 placedEyeOffset;
    public ClipCaps clipCaps => ClipCaps.None;
    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable=ScriptPlayable<DesertSelectedCameraBehaviour>.Create(graph);
        var behaviour=playable.GetBehaviour();
        behaviour.target=selectedCharacter.Resolve(graph.GetResolver());
        behaviour.rig=cameraRig.Resolve(graph.GetResolver());
        behaviour.distance=distance;
        behaviour.seatedStartTime=seatedStartTime;
        behaviour.seatedTurnDuration=seatedTurnDuration;
        behaviour.usePlacedAngle=usePlacedAngle;
        behaviour.placedRigOffset=placedRigOffset;
        behaviour.placedRigRotation=placedRigRotation;
        behaviour.hasPlacedEyeOffset=hasPlacedEyeOffset;
        behaviour.placedEyeOffset=placedEyeOffset;
        return playable;
    }
}
public class DesertSelectedCameraBehaviour : PlayableBehaviour
{
    public Transform target, rig;
    public float distance;
    public float seatedStartTime, seatedTurnDuration;
    public bool usePlacedAngle;
    public Vector3 placedRigOffset;
    public Quaternion placedRigRotation;
    public bool hasPlacedEyeOffset;
    public Vector3 placedEyeOffset;
    bool loggedTarget;
    Camera trackedCamera;
    Vector3 vrEye;
    Quaternion vrRotation;
    bool vrPoseReady,vrSubscribed;
    [BeforeRenderOrder(20000)]
    void ApplyVRPose()
    {
        if(!vrPoseReady||!trackedCamera||!rig)return;
        // Recalculate after the tracked-pose driver, which can replace the large
        // authored desktop camera-local offset with the real headset pose.
        var headOffset=rig.InverseTransformPoint(trackedCamera.transform.position);
        rig.SetPositionAndRotation(vrEye-vrRotation*Vector3.Scale(headOffset,rig.lossyScale),vrRotation);
    }
    Transform followedHead;
    float standingHeadHeight;
    bool poseReferenceReady;
    DesertCameraCollision collision;
    public override void OnPlayableDestroy(Playable playable)
    {
        if(vrSubscribed)Application.onBeforeRender-=ApplyVRPose;vrSubscribed=false;vrPoseReady=false;
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
        if(!poseReferenceReady)
        {
            foreach(var animator in target.GetComponentsInChildren<Animator>(true))
            {
                if(!animator.isHuman)continue;
                followedHead=animator.GetBoneTransform(HumanBodyBones.Head);
                if(followedHead)break;
            }
            if(followedHead)standingHeadHeight=followedHead.position.y-target.position.y;
            poseReferenceReady=true;
        }
        // The route root remains on the floor during sitting; follow the animated body lowering too.
        float seatedHeightOffset=0;
        if(followedHead)
        {
            float lowering=followedHead.position.y-target.position.y-standingHeadHeight;
            if(lowering<-.12f)seatedHeightOffset=lowering+.12f;
        }
        Vector3 forward=Vector3.ProjectOnPlane(target.forward,Vector3.up).normalized;
        if(forward.sqrMagnitude<0.1f)forward=Vector3.forward;
        if(collision==null)collision=new DesertCameraCollision(target.gameObject.scene);
        float radius=Mathf.Max(.22f,camera.nearClipPlane*1.5f);
        Vector3 localEye=rig.InverseTransformPoint(camera.transform.position);
        bool vr=XRSettings.isDeviceActive;
        bool placed=usePlacedAngle&&(!vr||hasPlacedEyeOffset);
        Vector3 desiredEye=placed
            ?target.position+Vector3.up*seatedHeightOffset+(hasPlacedEyeOffset?placedEyeOffset:placedRigOffset+placedRigRotation*Vector3.Scale(localEye,rig.lossyScale))
            :focus-forward*distance+Vector3.up*.45f;
        float seatedBlend=seatedStartTime<0?0:Mathf.SmoothStep(0,1,((float)playable.GetTime()-seatedStartTime)/Mathf.Max(.1f,seatedTurnDuration));
        Vector3 lookFocus=focus;
        if(seatedBlend>0 && followedHead)
        {
            Vector3 head=followedHead.position;
            Vector3 from=Vector3.ProjectOnPlane(desiredEye-head,Vector3.up);
            float startYaw=Mathf.Atan2(from.x,from.z)*Mathf.Rad2Deg;
            float rearYaw=Mathf.Atan2(-forward.x,-forward.z)*Mathf.Rad2Deg;
            float yaw=Mathf.LerpAngle(startYaw,rearYaw,seatedBlend)*Mathf.Deg2Rad;
            float orbitDistance=Mathf.Lerp(from.magnitude,1.15f,seatedBlend);
            desiredEye=head+new Vector3(Mathf.Sin(yaw)*orbitDistance,Mathf.Lerp(desiredEye.y-head.y,.22f,seatedBlend),Mathf.Cos(yaw)*orbitDistance);
            focus=Vector3.Lerp(focus,head,seatedBlend);
            lookFocus=Vector3.Lerp(lookFocus,head+forward*2f,seatedBlend);
        }
        Vector3 eye=collision.Resolve(focus,desiredEye,radius,target,rig);
        Quaternion look=Quaternion.LookRotation((lookFocus-eye).normalized,Vector3.up);
        Quaternion localRotation=Quaternion.Inverse(rig.rotation)*camera.transform.rotation;
        Quaternion followRotation=XRSettings.isDeviceActive?look:look*Quaternion.Inverse(localRotation);
        Quaternion rotation=placed?Quaternion.Slerp(placedRigRotation,followRotation,seatedBlend):followRotation;
        if(vr)
        {
            // Use the authored desktop lens position, not its distant rig pivot.
            // Tracked local head rotation remains owned by the XR pose driver.
            var facing=Vector3.ProjectOnPlane(lookFocus-eye,Vector3.up);
            if(facing.sqrMagnitude<.01f)facing=forward;
            vrRotation=placed?Quaternion.Slerp(placedRigRotation,look,seatedBlend):Quaternion.LookRotation(facing,Vector3.up);
            vrEye=eye;trackedCamera=camera;vrPoseReady=true;
            if(!vrSubscribed){Application.onBeforeRender+=ApplyVRPose;vrSubscribed=true;}
            ApplyVRPose();camera.enabled=true;
            return;
        }
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

