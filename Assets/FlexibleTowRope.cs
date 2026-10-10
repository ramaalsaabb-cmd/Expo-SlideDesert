using UnityEngine;
using NVector = System.Numerics.Vector3;

/// <summary>Round flexible visual tether; the existing scripted sled remains in control.</summary>
[DefaultExecutionOrder(300)]
public class FlexibleTowRope : MonoBehaviour
{
    public Transform carAnchor;
    public Transform sledAnchor;
    public Material ropeMaterial;
    [Min(0.1f)] public float ropeLength = 22.8f;
    [Range(16,64)] public int segments = 40;
    [Range(0.008f,0.05f)] public float radius = 0.0225f;
    [Range(0f,4f)] public float damping = 0.7f;
    [Range(8,64)] public int solverIterations = 32;
    [HideInInspector] public int sizingVersion;
    public float currentSpan;
    public float extension;
    TowRopeSimulation simulation;
    Mesh mesh;
    MeshRenderer meshRenderer;
    Vector3[] vertices, normals;
    Vector2[] uvs;
    Terrain[] terrains;
    const int Sides=8;

    void Start() { Initialize(); }
    public void Initialize()
    {
        if(!carAnchor || !sledAnchor) return;
        terrains=Terrain.activeTerrains;
        simulation=new TowRopeSimulation(segments+1,ropeLength,ToN(carAnchor.position),ToN(sledAnchor.position));
        simulation.ResolveGround=Ground;
        simulation.Reset(ToN(carAnchor.position),ToN(sledAnchor.position));
        if(!mesh)
        {
            mesh=new Mesh { name="Flexible Tow Rope Mesh" };
            mesh.MarkDynamic();
            var filter=GetComponent<MeshFilter>();
            if(!filter) filter=gameObject.AddComponent<MeshFilter>();
            meshRenderer=GetComponent<MeshRenderer>();
            if(!meshRenderer) meshRenderer=gameObject.AddComponent<MeshRenderer>();
            filter.sharedMesh=mesh;
        }
        meshRenderer.sharedMaterial=ropeMaterial ? ropeMaterial : Resources.Load<Material>("TowRope");
        int rings=simulation.Points.Length;
        vertices=new Vector3[rings*Sides]; normals=new Vector3[vertices.Length]; uvs=new Vector2[vertices.Length];
        var triangles=new int[(rings-1)*Sides*6];
        int index=0;
        for(int i=0;i<rings;i++) for(int j=0;j<Sides;j++)
        {
            int v=i*Sides+j;
            uvs[v]=new Vector2((float)j/Sides,(float)i/(rings-1)*ropeLength);
            if(i==rings-1) continue;
            int next=i*Sides+(j+1)%Sides;
            triangles[index++]=v; triangles[index++]=next; triangles[index++]=v+Sides;
            triangles[index++]=next; triangles[index++]=next+Sides; triangles[index++]=v+Sides;
        }
        mesh.Clear(); mesh.vertices=vertices; mesh.triangles=triangles; mesh.uv=uvs;
        UpdateMesh();
    }

    void LateUpdate()
    {
        if(!carAnchor || !sledAnchor) return;
        if(simulation==null || simulation.Points.Length!=segments+1) Initialize();
        simulation.Length=ropeLength;
        simulation.Damping=damping; simulation.Iterations=solverIterations;
        simulation.Step(Time.deltaTime,ToN(carAnchor.position),ToN(sledAnchor.position));
        currentSpan=Vector3.Distance(carAnchor.position,sledAnchor.position);
        extension=Mathf.Max(0f,currentSpan-ropeLength);
        UpdateMesh();
    }

    NVector Ground(NVector point)
    {
        Vector3 p=FromN(point);
        if(terrains!=null) foreach(var terrain in terrains)
        {
            if(!terrain || !terrain.terrainData) continue;
            Vector3 local=p-terrain.transform.position, size=terrain.terrainData.size;
            if(local.x<0f || local.z<0f || local.x>size.x || local.z>size.z) continue;
            p.y=Mathf.Max(p.y,terrain.SampleHeight(p)+terrain.transform.position.y+radius);
            break;
        }
        return ToN(p);
    }

    void UpdateMesh()
    {
        if(simulation==null || !mesh) return;
        Vector3 previousSide=Vector3.right;
        for(int i=0;i<simulation.Points.Length;i++)
        {
            Vector3 p=FromN(simulation.Points[i]);
            Vector3 tangent=FromN(simulation.Points[Mathf.Min(i+1,simulation.Points.Length-1)])
                -FromN(simulation.Points[Mathf.Max(0,i-1)]);
            if(tangent.sqrMagnitude<0.00001f) tangent=Vector3.forward;
            tangent.Normalize();
            Vector3 side=Vector3.ProjectOnPlane(previousSide,tangent);
            if(side.sqrMagnitude<0.0001f) side=Vector3.Cross(tangent,Vector3.up);
            if(side.sqrMagnitude<0.0001f) side=Vector3.right;
            side.Normalize(); previousSide=side;
            Vector3 up=Vector3.Cross(tangent,side).normalized;
            for(int j=0;j<Sides;j++)
            {
                float angle=j*Mathf.PI*2/Sides;
                Vector3 normal=side*Mathf.Cos(angle)+up*Mathf.Sin(angle);
                int v=i*Sides+j;
                vertices[v]=transform.InverseTransformPoint(p+normal*radius);
                normals[v]=transform.InverseTransformDirection(normal);
            }
        }
        mesh.vertices=vertices; mesh.normals=normals; mesh.RecalculateBounds();
    }
    static NVector ToN(Vector3 v) { return new NVector(v.x,v.y,v.z); }
    static Vector3 FromN(NVector v) { return new Vector3(v.X,v.Y,v.Z); }
    void OnDestroy() { if(mesh) Destroy(mesh); }
}
