using System.Collections.Generic;
using UnityEngine;

/// <summary>Widely separated, curved sand ridges on disposable copies of the terrain.</summary>
[DefaultExecutionOrder(-5000)]
public class RouteTerrainBumps : MonoBehaviour
{
    public CarPathFollower route;
    public Terrain[] terrains;
    public float spacing = 105f;
    public float halfWidth = 15f;
    public float halfLength = 5f;
    public float minimumHeight = .4f;
    public float maximumHeight = .7f;
    public float endClearance = 75f;
    public float minimumWorldSeparation = 65f;
    readonly Dictionary<Terrain, TerrainData> originals = new Dictionary<Terrain, TerrainData>();
    readonly List<TerrainData> generated = new List<TerrainData>();
    Vector3 routeStart;
    bool hasRouteStart;
    struct Bump { public Vector3 center, forward; public float height, width, length, phase; }

    List<Vector3> BuildRoute()
    {
        var anchors = new List<Vector3>();
        if (!route || route.points == null) return anchors;
        anchors.Add(hasRouteStart ? routeStart : route.transform.position);
        foreach (var point in route.points)
            if (point && Vector3.ProjectOnPlane(point.position - anchors[anchors.Count - 1], Vector3.up).sqrMagnitude > .01f)
                anchors.Add(point.position);
        var path = new List<Vector3>();
        if (anchors.Count < 2) return path;
        path.Add(anchors[0]);
        // Match the car's smoothed path so each ridge crosses the driven road.
        for (int i = 0; i < anchors.Count - 1; i++)
        {
            var a=anchors[Mathf.Max(0,i-1)];var b=anchors[i];var c=anchors[i+1];var d=anchors[Mathf.Min(anchors.Count-1,i+2)];
            int samples=Mathf.Max(12,Mathf.CeilToInt(Vector3.Distance(b,c)/1.5f));
            for(int j=1;j<=samples;j++){float t=j/(float)samples;path.Add(.5f*(2*b+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));}
        }
        return path;
    }
    List<Bump> BuildBumps()
    {
        var result=new List<Bump>();var path=BuildRoute();if(path.Count<2)return result;
        float total=0;for(int i=1;i<path.Count;i++)total+=Vector3.ProjectOnPlane(path[i]-path[i-1],Vector3.up).magnitude;
        int attempt=0;
        for(float distance=65f;distance<total-endClearance;distance+=Mathf.Max(80f,spacing)*new[]{1.12f,.94f,1.07f,1f}[attempt++%4])
        {
            float remaining=distance;
            for(int i=1;i<path.Count;i++)
            {
                var direction=Vector3.ProjectOnPlane(path[i]-path[i-1],Vector3.up);float length=direction.magnitude;if(length<.001f)continue;
                if(remaining>length){remaining-=length;continue;}
                var center=Vector3.Lerp(path[i-1],path[i],remaining/length);
                bool separated=true;foreach(var prior in result)if(Vector3.ProjectOnPlane(center-prior.center,Vector3.up).magnitude<minimumWorldSeparation){separated=false;break;}
                if(separated){int n=result.Count;result.Add(new Bump{center=center,forward=Quaternion.Euler(0,n%2==0?10f:-12f,0)*direction.normalized,height=Mathf.Lerp(minimumHeight,maximumHeight,new[]{.2f,.85f,.45f,1f}[n%4]),width=halfWidth*new[]{1f,.85f,1.1f,.95f}[n%4],length=halfLength*new[]{1f,1.15f,.9f,1.05f}[n%4],phase=n*1.73f});}
                break;
            }
        }
        return result;
    }
    public Vector3[] GetBumpCenters(){var bumps=BuildBumps();var centers=new Vector3[bumps.Count];for(int i=0;i<bumps.Count;i++)centers[i]=bumps[i].center;return centers;}
    void Awake(){if(route){routeStart=route.transform.position;hasRouteStart=true;}RebuildTerrain();}
    public void RebuildTerrain()
    {
        RestoreTerrain();var bumps=BuildBumps();if(terrains==null)return;
        foreach(var terrain in terrains)
        {
            if(!terrain||!terrain.terrainData)continue;
            var original=terrain.terrainData;var data=Instantiate(original);data.name=original.name+" (spaced sand ridges runtime)";data.hideFlags=HideFlags.DontSave;
            originals.Add(terrain,original);generated.Add(data);
            int resolution=data.heightmapResolution;var heights=data.GetHeights(0,0,resolution,resolution);var size=data.size;var origin=terrain.transform.position;
            foreach(var bump in bumps)
            {
                float radius=Mathf.Sqrt(bump.width*bump.width+bump.length*bump.length*2.25f)+2;
                int minX=Mathf.Clamp(Mathf.FloorToInt((bump.center.x-radius-origin.x)/size.x*(resolution-1)),0,resolution-1),maxX=Mathf.Clamp(Mathf.CeilToInt((bump.center.x+radius-origin.x)/size.x*(resolution-1)),0,resolution-1);
                int minZ=Mathf.Clamp(Mathf.FloorToInt((bump.center.z-radius-origin.z)/size.z*(resolution-1)),0,resolution-1),maxZ=Mathf.Clamp(Mathf.CeilToInt((bump.center.z+radius-origin.z)/size.z*(resolution-1)),0,resolution-1);
                var right=Vector3.Cross(Vector3.up,bump.forward);
                for(int z=minZ;z<=maxZ;z++)for(int x=minX;x<=maxX;x++)
                {
                    var delta=new Vector3(origin.x+x*size.x/(resolution-1)-bump.center.x,0,origin.z+z*size.z/(resolution-1)-bump.center.z);
                    float across=Vector3.Dot(delta,right)/Mathf.Max(1,bump.width);if(Mathf.Abs(across)>=1)continue;
                    // A curved crest, a long windward slope and a shorter lee slope, with uneven sand shoulders.
                    float crest=1.1f*Mathf.Sin(across*3.3f+bump.phase)+.35f*Mathf.Sin(across*8.5f-bump.phase);
                    float along=Vector3.Dot(delta,bump.forward)-crest;
                    float normalized=along/(bump.length*(along<0?1.35f:.72f));if(Mathf.Abs(normalized)>=1)continue;
                    float profile=.5f+.5f*Mathf.Cos(normalized*Mathf.PI);
                    float taper=Mathf.Pow(.5f+.5f*Mathf.Cos(across*Mathf.PI),.75f);
                    float irregular=1+.1f*Mathf.Sin(across*10f+bump.phase)+.04f*Mathf.Sin(across*23f);
                    heights[z,x]=Mathf.Clamp01(heights[z,x]+bump.height*profile*taper*irregular/size.y);
                }
            }
            data.SetHeights(0,0,heights);terrain.terrainData=data;var collider=terrain.GetComponent<TerrainCollider>();if(collider)collider.terrainData=data;
        }
        Physics.SyncTransforms();
    }
    public void RestoreTerrain()
    {
        foreach(var pair in originals){if(!pair.Key)continue;pair.Key.terrainData=pair.Value;var collider=pair.Key.GetComponent<TerrainCollider>();if(collider)collider.terrainData=pair.Value;}
        originals.Clear();foreach(var data in generated)if(data){if(Application.isPlaying)Destroy(data);else DestroyImmediate(data);}generated.Clear();
    }
    void OnDestroy(){RestoreTerrain();}
    void OnDrawGizmosSelected()
    {
        var path=BuildRoute();Gizmos.color=Color.cyan;for(int i=1;i<path.Count;i++)Gizmos.DrawLine(path[i-1],path[i]);
        Gizmos.color=Color.yellow;foreach(var bump in BuildBumps()){var right=Vector3.Cross(Vector3.up,bump.forward);Vector3 previous=default;for(int i=0;i<=24;i++){float across=-1+i/12f;float crest=1.1f*Mathf.Sin(across*3.3f+bump.phase)+.35f*Mathf.Sin(across*8.5f-bump.phase);var p=bump.center+right*(across*bump.width)+bump.forward*crest+Vector3.up*bump.height;if(i>0)Gizmos.DrawLine(previous,p);previous=p;}}
    }
}
