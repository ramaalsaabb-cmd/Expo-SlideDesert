using UnityEngine;

/// <summary>Locate the actual bumper and sled model; supports Play with scene reload disabled.</summary>
public static class TowRopeSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnPlay()
    {
        foreach(var follow in Object.FindObjectsByType<SledFollowCar>())
        {
            if(follow.gameObject.scene.path!="Assets/Scenes/Demo.unity") continue;
            var rope=Configure(follow);
            if(rope) rope.Initialize();
        }
    }

    public static FlexibleTowRope Configure(SledFollowCar follow)
    {
        if(!follow || !follow.car) return null;
        var existing=follow.GetComponentInChildren<FlexibleTowRope>();
        if(existing && existing.carAnchor && existing.sledAnchor)
        {
            ApplySize(existing, follow);
            AddTieLoop(existing.carAnchor,false);
            AddTieLoop(existing.sledAnchor,true);
            return existing;
        }
        var model=follow.car;
        var carAnchor=model.Find("CarTowHitch");
        if(!carAnchor)
        {
            carAnchor=new GameObject("CarTowHitch").transform;
            carAnchor.SetParent(model,false);
            Bounds bounds=ModelBounds(model);
            carAnchor.localPosition=new Vector3(bounds.center.x,bounds.min.y+bounds.size.y*0.17f,bounds.min.z-0.04f);
        }
        Transform sledModel=null;
        foreach(var child in follow.GetComponentsInChildren<Transform>())
            if(child.name=="Sled_Sandal_Rider") { sledModel=child; break; }
        if(!sledModel) { Debug.LogWarning("Tow rope: sled model not found.",follow); return null; }
        var sledAnchor=sledModel.Find("SledTowHitch");
        if(!sledAnchor)
        {
            sledAnchor=new GameObject("SledTowHitch").transform;
            sledAnchor.SetParent(sledModel,false);
            // Model-space molded sled front: validated against the glTF mesh bounds.
            sledAnchor.localPosition=new Vector3(0f,0.08f,0.59f);
        }
        AddTieLoop(carAnchor, false);
        AddTieLoop(sledAnchor, true);
        // Replace only the short decorative rope embedded in the sled model.
        foreach(var renderer in sledModel.GetComponentsInChildren<Renderer>())
            if(renderer.name=="Tow rope") renderer.enabled=false;
        var obj=new GameObject("Flexible Tow Rope");
        obj.transform.SetParent(follow.transform,false);
        var rope=obj.AddComponent<FlexibleTowRope>();
        rope.carAnchor=carAnchor; rope.sledAnchor=sledAnchor;
        rope.ropeMaterial=Resources.Load<Material>("TowRope");
        var car=model.GetComponentInParent<CarPathFollower>();
        float lag=car ? Mathf.Abs(car.speed)/Mathf.Max(0.1f,follow.followSpeed) : 0f;
        float carOffset=Vector3.Dot(carAnchor.position-model.position,model.forward);
        float sledOffset=Vector3.Dot(sledAnchor.position-follow.transform.position,follow.transform.forward);
        rope.ropeLength=Mathf.Max(2f,(follow.followDistance+lag+carOffset-sledOffset)*1.025f);
        ApplySize(rope, follow);
        return rope;
    }

    public static void ApplySize(FlexibleTowRope rope, SledFollowCar follow)
    {
        if (!rope || !follow || rope.sizingVersion >= 1) return;
        float reduction = rope.ropeLength * 0.05f;
        rope.ropeLength -= reduction;
        rope.radius = 0.0225f;
        // Reduce the gap too: pinned endpoints would otherwise stretch the shorter rope.
        follow.followDistance = Mathf.Max(2f, follow.followDistance - reduction);
        rope.sizingVersion = 1;
    }

    static void AddTieLoop(Transform anchor, bool horizontal)
    {
        Transform existingLoop=anchor.Find("Rope Tie Loop");
        GameObject obj=existingLoop ? existingLoop.gameObject : new GameObject("Rope Tie Loop");
        var filter=obj.GetComponent<MeshFilter>();
        if(filter && filter.sharedMesh) return;
        obj.transform.SetParent(anchor,false);
        if(horizontal) obj.transform.localRotation=Quaternion.Euler(90f,0f,0f);
        // Counter inherited model scale so both attachment loops remain about 10cm across.
        Vector3 scale=anchor.lossyScale;
        obj.transform.localScale=new Vector3(1f/Mathf.Max(0.01f,Mathf.Abs(scale.x)),
            1f/Mathf.Max(0.01f,Mathf.Abs(scale.y)),1f/Mathf.Max(0.01f,Mathf.Abs(scale.z)));
        const int Rings=24, Sides=8;
        var vertices=new Vector3[Rings*Sides];
        var normals=new Vector3[vertices.Length];
        var uv=new Vector2[vertices.Length];
        var triangles=new int[Rings*Sides*6];
        int index=0;
        for(int i=0;i<Rings;i++) for(int j=0;j<Sides;j++)
        {
            float around=i*Mathf.PI*2/Rings, tube=j*Mathf.PI*2/Sides;
            Vector3 outward=new Vector3(Mathf.Cos(around),Mathf.Sin(around),0f);
            Vector3 normal=outward*Mathf.Cos(tube)+Vector3.forward*Mathf.Sin(tube);
            int v=i*Sides+j, next=i*Sides+(j+1)%Sides;
            int ring=((i+1)%Rings)*Sides+j, nextRing=((i+1)%Rings)*Sides+(j+1)%Sides;
            vertices[v]=outward*0.05f+normal*0.012f;
            normals[v]=normal; uv[v]=new Vector2((float)j/Sides,(float)i/Rings*0.32f);
            triangles[index++]=v; triangles[index++]=ring; triangles[index++]=next;
            triangles[index++]=next; triangles[index++]=ring; triangles[index++]=nextRing;
        }
        var mesh=new Mesh { name="Tow Rope Tie Loop" };
        mesh.vertices=vertices; mesh.normals=normals; mesh.uv=uv; mesh.triangles=triangles; mesh.RecalculateBounds();
        if(!filter) filter=obj.AddComponent<MeshFilter>();
        filter.sharedMesh=mesh;
        var renderer=obj.GetComponent<MeshRenderer>();
        if(!renderer) renderer=obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=Resources.Load<Material>("TowRope");
    }

    static Bounds ModelBounds(Transform root)
    {
        Bounds bounds=new Bounds(Vector3.zero,Vector3.zero);
        bool found=false;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            if(!filter.sharedMesh) continue;
            Bounds mesh=filter.sharedMesh.bounds;
            for(int i=0;i<8;i++)
            {
                Vector3 corner=mesh.center+Vector3.Scale(mesh.extents,
                    new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                Vector3 local=root.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if(!found) { bounds=new Bounds(local,Vector3.zero); found=true; } else bounds.Encapsulate(local);
            }
        }
        return found ? bounds : new Bounds(new Vector3(0,1,-2.5f),new Vector3(2,2,5));
    }
}
