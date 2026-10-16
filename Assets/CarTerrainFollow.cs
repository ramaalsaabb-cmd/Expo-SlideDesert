using System.Collections.Generic;
using UnityEngine;

public class CarTerrainFollow : MonoBehaviour
{
    public Terrain[] terrains;
    public float groundOffset=.5f;
    public float heightSmooth=10f;
    public float sampleDistance=1.5f;
    public float rotationSmooth=8f;
    [Tooltip("Minimum wheel and underbody clearance above the sampled sand.")]
    public float sandClearance=.035f;
    readonly List<Vector3> supports=new List<Vector3>();
    bool initialized;
    void Start(){InitializeSupports();}
    void InitializeSupports()
    {
        supports.Clear();var path=GetComponent<CarPathFollower>();
        if(path&&path.wheels!=null)foreach(var wheel in path.wheels)
        {
            if(!wheel)continue;var renderers=wheel.GetComponentsInChildren<Renderer>();if(renderers.Length==0)continue;
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            supports.Add(transform.InverseTransformPoint(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)));
        }
        if(supports.Count<4){supports.Clear();foreach(float x in new[]{-.85f,.85f})foreach(float z in new[]{-sampleDistance,sampleDistance})supports.Add(new Vector3(x,-groundOffset,z));}
        // Also sample between the axles: a narrow crest can reach the chassis before the center follows it.
        float minX=float.PositiveInfinity,maxX=float.NegativeInfinity,minZ=float.PositiveInfinity,maxZ=float.NegativeInfinity,bottom=float.PositiveInfinity;
        foreach(var p in supports){minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minZ=Mathf.Min(minZ,p.z);maxZ=Mathf.Max(maxZ,p.z);bottom=Mathf.Min(bottom,p.y);}
        foreach(float x in new[]{minX,(minX+maxX)*.5f,maxX})foreach(float z in new[]{minZ,(minZ+maxZ)*.5f,maxZ})supports.Add(new Vector3(x,bottom+.18f,z));
        initialized=true;
    }
    void LateUpdate(){FollowGround(Time.deltaTime);}
    public void FollowGround(float deltaTime)
    {
        if(!initialized)InitializeSupports();if(!TryHeight(transform.position,out float centerHeight))return;
        var heading=Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized;if(heading.sqrMagnitude<.01f)return;
        var front=transform.position+heading*sampleDistance;var back=transform.position-heading*sampleDistance;
        if(!TryHeight(front,out float frontHeight))frontHeight=centerHeight;if(!TryHeight(back,out float backHeight))backHeight=centerHeight;
        front.y=frontHeight;back.y=backHeight;var slope=front-back;
        if(slope.sqrMagnitude>.001f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(slope.normalized,Vector3.up),1-Mathf.Exp(-Mathf.Max(1,rotationSmooth)*deltaTime));
        float required=centerHeight+groundOffset;
        foreach(var local in supports){var sample=transform.TransformPoint(local);if(TryHeight(sample,out float height))required=Mathf.Max(required,transform.position.y+height+sandClearance-sample.y);}
        var position=transform.position;float eased=Mathf.Lerp(position.y,required,1-Mathf.Exp(-Mathf.Max(1,heightSmooth)*deltaTime));
        // Never smooth upward through the terrain. Retain smooth settling after the crest.
        position.y=Mathf.Max(required,eased);transform.position=position;
    }
    bool TryHeight(Vector3 position,out float height)
    {
        height=0;if(terrains==null)return false;
        foreach(var terrain in terrains){if(!terrain||!terrain.terrainData)continue;var local=position-terrain.transform.position;var size=terrain.terrainData.size;if(local.x<0||local.z<0||local.x>size.x||local.z>size.z)continue;height=terrain.SampleHeight(position)+terrain.transform.position.y;return true;}return false;
    }
    public float MinimumSupportClearance()
    {
        if(!initialized)InitializeSupports();float minimum=float.PositiveInfinity;foreach(var local in supports){var point=transform.TransformPoint(local);if(TryHeight(point,out float height))minimum=Mathf.Min(minimum,point.y-height);}return minimum;
    }
}
