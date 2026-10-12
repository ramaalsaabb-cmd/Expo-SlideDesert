using UnityEngine;

/// <summary>Broad, low dust thrown from both sled runners, with world-space lingering clouds.</summary>
[DefaultExecutionOrder(150)]
[RequireComponent(typeof(SledFollowCar))]
public class SledDustController : MonoBehaviour
{
    public Material dustMaterial;
    public Transform sledModel;
    [Range(0.5f,3f)] public float cloudSize = 1.2f;
    [Range(10f,100f)] public float particlesPerSide = 25f;
    public float turnBoost = 0.7f;
    public float lifetime = 1.5f;
    [Range(0.05f,0.5f)] public float opacity = 0.18f;
    public float measuredSpeed;
    public int liveParticles;
    public int groundedSides;
    public float emissionPerSide;
    [HideInInspector] public int plumeVersion;
    ParticleSystem[] clouds;
    Material runtimeMaterial;
    readonly float[] accumulated = new float[2];
    Terrain[] terrains;
    Vector3 previousPosition, previousForward;

    void Start() { if (clouds == null) Initialize(); }
    public void Initialize()
    {
        if(!sledModel) foreach(var child in GetComponentsInChildren<Transform>(true))
            if(child.name.StartsWith("Sled_Sandal_Rider") || child.name.StartsWith("Woman_Refined_Sled")) { sledModel=child; break; }
        if(!dustMaterial) dustMaterial=Resources.Load<Material>("SledDust");
        if (plumeVersion < 3)
        {
            cloudSize = 1.2f;
            particlesPerSide = 25f;
            opacity = 0.18f;
            plumeVersion = 3;
        }
        if (runtimeMaterial) Destroy(runtimeMaterial);
        runtimeMaterial = dustMaterial ? new Material(dustMaterial) : null;
        if (runtimeMaterial)
        {
            runtimeMaterial.name = dustMaterial.name + " (Sled translucent runtime)";
            if (runtimeMaterial.HasProperty("_AlphaClip")) runtimeMaterial.SetFloat("_AlphaClip", 0f);
            runtimeMaterial.DisableKeyword("_ALPHATEST_ON");
        }
        terrains=Terrain.activeTerrains;
        previousPosition=transform.position; previousForward=transform.forward;
        accumulated[0]=accumulated[1]=0f;
        if(clouds!=null && clouds.Length==2 && clouds[0] && clouds[1])
        {
            foreach(var ps in clouds)
            {
                ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=runtimeMaterial;
                ps.Play();
            }
            return;
        }
        clouds=new ParticleSystem[2];
        for(int i=0;i<2;i++)
        {
            var obj=new GameObject(i==0?"Sled Dust Left":"Sled Dust Right");
            obj.transform.SetParent(transform,false);
            obj.layer=gameObject.layer;
            var ps=obj.AddComponent<ParticleSystem>();
            ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;
            main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            main.playOnAwake=false; main.loop=true; main.maxParticles=240; main.startSpeed=0f;
            var emission=ps.emission; emission.enabled=false;
            var shape=ps.shape; shape.enabled=false;
            var color=ps.colorOverLifetime; color.enabled=true;
            var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(Color.white,1f)},
                new[]{new GradientAlphaKey(0f,0f),new GradientAlphaKey(1f,0.12f),
                    new GradientAlphaKey(0.6f,0.5f),new GradientAlphaKey(0f,1f)});
            color.color=gradient;
            var size=ps.sizeOverLifetime; size.enabled=true;
            size.size=new ParticleSystem.MinMaxCurve(1f,AnimationCurve.Linear(0f,0.45f,1f,2.8f));
            var noise=ps.noise; noise.enabled=true; noise.strength=0.4f; noise.frequency=0.35f;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial=runtimeMaterial;
            renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.sortMode=ParticleSystemSortMode.Distance;
            renderer.maxParticleSize=1f;
            clouds[i]=ps; ps.Play();
        }
    }

    void LateUpdate()
    {
        if(clouds==null || terrains==null || !runtimeMaterial) Initialize();
        if(!sledModel || clouds==null) return;
        float dt=Time.deltaTime;
        if(dt<=0f) return;
        Vector3 travel=transform.position-previousPosition;
        Vector3 velocity=travel/dt;
        measuredSpeed=Vector3.ProjectOnPlane(velocity,Vector3.up).magnitude;
        float turn=Mathf.Abs(Vector3.SignedAngle(previousForward,transform.forward,Vector3.up))/dt;
        previousPosition=transform.position; previousForward=transform.forward;
        if(travel.magnitude>5f) { measuredSpeed=0f; velocity=Vector3.zero; }
        float movement=Mathf.InverseLerp(0.6f,18f,measuredSpeed);
        float turnDust=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(8f,40f,turn));
        float intensity=movement*(1f+turnDust*turnBoost);
        liveParticles=groundedSides=0;
        emissionPerSide=particlesPerSide*intensity;
        for(int i=0;i<2;i++)
        {
            if(!clouds[i]) continue;
            float sign=i==0?-1f:1f;
            Vector3 runner=sledModel.TransformPoint(new Vector3(sign*0.24f,0.035f,0.28f));
            bool grounded=GroundContact(runner,out Vector3 contact);
            if (grounded) groundedSides++;
            // Keep the emitter bounds at the actual runner, not the offset sled root.
            clouds[i].transform.position=contact;
            accumulated[i]+=grounded?particlesPerSide*intensity*dt:0f;
            int count=Mathf.Min(64,Mathf.FloorToInt(accumulated[i]));
            accumulated[i]-=count;
            if(!grounded || movement==0f) { accumulated[i]=0f; count=0; }
            for(int n=0;n<count;n++)
            {
                Vector2 spread=Random.insideUnitCircle*0.18f;
                var particle=new ParticleSystem.EmitParams();
                particle.position=contact+new Vector3(spread.x,Random.Range(0.18f,0.3f),spread.y);
                // Velocity is captured at emission, so old dust does not rotate with the sled.
                particle.velocity=velocity*0.3f-transform.forward*Random.Range(0.6f,1.3f)
                    +transform.right*sign*Random.Range(0.7f,1.3f)+Vector3.up*Random.Range(0.5f,0.95f);
                particle.startSize=Random.Range(0.55f,1f)*cloudSize;
                particle.startLifetime=Random.Range(lifetime*0.65f,lifetime);
                particle.startColor=new Color(0.72f,0.58f,0.39f,opacity);
                particle.rotation=Random.Range(0f,360f);
                clouds[i].Emit(particle,1);
            }
            liveParticles+=clouds[i].particleCount;
        }
    }

    bool GroundContact(Vector3 runner,out Vector3 contact)
    {
        contact=runner;
        if(terrains==null) return false;
        foreach(var terrain in terrains)
        {
            if(!terrain || !terrain.terrainData) continue;
            Vector3 local=runner-terrain.transform.position, size=terrain.terrainData.size;
            if(local.x<0 || local.z<0 || local.x>size.x || local.z>size.z) continue;
            float height=terrain.SampleHeight(runner)+terrain.transform.position.y;
            // This sled is scripted and uses an offset visual hierarchy, not wheel contacts.
            // Always place its scrape plume on the terrain directly below the runner.
            contact.y=height+0.06f; return true;
        }
        return false;
    }
    void OnDestroy() { if (runtimeMaterial) Destroy(runtimeMaterial); }
    void OnDisable()
    {
        if(clouds!=null) foreach(var ps in clouds) if(ps) ps.Stop();
    }
}





