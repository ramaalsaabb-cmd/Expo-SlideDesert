using UnityEngine;

/// <summary>Wheel contact dust, measured motion, and exposure to the lingering wake.</summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(CarPathFollower))]
public class WheelDustController : MonoBehaviour
{
    public Material dustMaterial;
    public ParticleSystem legacyDust;
    public Camera playerCamera;
    public float minimumSpeed = 0.7f;
    public float fullDustSpeed = 20f;
    public float fullTurnRate = 65f;
    public float turnBoost = 2.5f;
    public float particlesPerWheel = 35f;
    public float cloudLifetime = 2.2f;
    public float cloudRadius = 2.4f;
    public LayerMask groundLayers = ~0;

    struct Cloud { public Vector3 position, velocity; public float born, strength; }
    readonly Cloud[] clouds = new Cloud[192];
    readonly RaycastHit[] groundHits = new RaycastHit[16];
    ParticleSystem[] emitters;
    Transform[] wheels;
    CarPathFollower car;
    Material runtimeMaterial;
    Renderer[] wheelRenderers;
    public float measuredSpeed;
    public int groundedWheels;
    public int liveParticles;
    public float headExposure;
    Vector3 previousPosition, previousForward;
    float intensity, sampleTimer;
    int nextCloud;
    VRDustVision vision;

    void Start()
    {
        car = GetComponent<CarPathFollower>();
        wheels = car.wheels ?? new Transform[0];
        emitters = new ParticleSystem[wheels.Length];
        wheelRenderers = new Renderer[wheels.Length];
        if (legacyDust) legacyDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (dustMaterial)
        {
            runtimeMaterial = new Material(dustMaterial);
            if (runtimeMaterial.HasProperty("_AlphaClip")) runtimeMaterial.SetFloat("_AlphaClip", 0f);
            runtimeMaterial.DisableKeyword("_ALPHATEST_ON");
            if (runtimeMaterial.HasProperty("_Cull")) runtimeMaterial.SetFloat("_Cull", 0f);
        }
        for (int i = 0; i < wheels.Length; i++)
        {
            if (!wheels[i]) continue;
            wheelRenderers[i] = wheels[i].GetComponentInChildren<Renderer>();
            var obj = new GameObject("Wheel Dust " + i);
            obj.transform.SetParent(transform, false);
            var ps = obj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, cloudLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
            main.startColor = new Color(0.72f, 0.58f, 0.39f, 0.5f);
            main.maxParticles = 350;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.16f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 3f));
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.4f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = runtimeMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            emitters[i] = ps;
            ps.Play();
        }
        previousPosition = transform.position;
        previousForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        Vector3 displacement = transform.position - previousPosition;
        float speed = Vector3.ProjectOnPlane(displacement, Vector3.up).magnitude / dt;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        float turn = Mathf.Abs(Vector3.SignedAngle(previousForward, forward, Vector3.up)) / dt;
        previousPosition = transform.position;
        previousForward = forward;
        // Ignore teleports rather than filling the entire map with a dust wake.
        if (displacement.magnitude > 5f) speed = 0f;
        float target = Mathf.InverseLerp(minimumSpeed, Mathf.Max(minimumSpeed + 0.1f, fullDustSpeed), speed)
            * (1f + Mathf.Clamp01(turn / Mathf.Max(1f, fullTurnRate)) * turnBoost);
        intensity = Mathf.Lerp(intensity, target, 1f - Mathf.Exp(-8f * dt));
        measuredSpeed = speed;
        groundedWheels = liveParticles = 0;
        sampleTimer += dt;
        bool sample = sampleTimer >= 0.08f;
        if (sample) sampleTimer = 0f;
        for (int i = 0; i < emitters.Length; i++)
        {
            if (!emitters[i] || !wheels[i]) continue;
            // Imported wheel pivots can lie outside the visible tire; use mesh bounds.
            Vector3 bottom = wheels[i].position - Vector3.up * car.wheelRadius;
            if (wheelRenderers[i])
            {
                Bounds bounds = wheelRenderers[i].bounds;
                bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }
            Vector3 origin = bottom + Vector3.up * 0.7f;
            bool grounded = FindGround(origin, out RaycastHit hit);
            Vector3 contact = grounded ? hit.point + Vector3.up * 0.08f : bottom;
            if (!grounded) grounded = SampleTerrainContact(bottom, out contact);
            if (grounded) groundedWheels++;
            liveParticles += emitters[i].particleCount;
            emitters[i].transform.position = contact;
            var emission = emitters[i].emission;
            emission.rateOverTime = grounded ? particlesPerWheel * intensity : 0f;
            var velocity = emitters[i].velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            Vector3 drift = -forward * Mathf.Min(speed * 0.18f, 4f) + Vector3.up * (0.8f + intensity * 0.25f);
            velocity.x = drift.x;
            velocity.y = drift.y;
            velocity.z = drift.z;
            if (sample && grounded && intensity > 0.05f)
                clouds[nextCloud++ % clouds.Length] = new Cloud { position = contact, velocity = drift, born = Time.time, strength = intensity };
        }
        if (!playerCamera) playerCamera = Camera.main;
        if (!playerCamera) return;
        if (!vision) vision = playerCamera.GetComponent<VRDustVision>();
        if (!vision) return;
        float exposure = 0f;
        foreach (var cloud in clouds)
        {
            if (cloud.strength <= 0f) continue;
            float age = Time.time - cloud.born;
            if (age < 0f || age > cloudLifetime) continue;
            Vector3 center = cloud.position + cloud.velocity * age;
            float radius = cloudRadius * Mathf.Lerp(0.45f, 1f, age / cloudLifetime);
            float proximity = 1f - Mathf.Clamp01(Vector3.Distance(center, playerCamera.transform.position) / radius);
            exposure = Mathf.Max(exposure, proximity * cloud.strength * (1f - age / cloudLifetime));
        }
        headExposure = Mathf.Clamp01(exposure);
        vision.SetExposure(headExposure);
    }

    bool SampleTerrainContact(Vector3 bottom, out Vector3 contact)
    {
        contact = bottom;
        foreach (var terrain in Terrain.activeTerrains)
        {
            if (!terrain.terrainData || (groundLayers.value & (1 << terrain.gameObject.layer)) == 0) continue;
            Vector3 local = bottom - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z) continue;
            float height = terrain.SampleHeight(bottom) + terrain.transform.position.y;
            if (Mathf.Abs(bottom.y - height) > 1.5f) continue;
            contact = new Vector3(bottom.x, height + 0.08f, bottom.z);
            return true;
        }
        return false;
    }

    bool FindGround(Vector3 origin, out RaycastHit nearest)
    {
        nearest = default;
        float distance = float.PositiveInfinity;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits,
            car.wheelRadius + 0.75f, groundLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i];
            if (hit.transform.IsChildOf(transform) || hit.distance >= distance) continue;
            nearest = hit;
            distance = hit.distance;
        }
        return distance < float.PositiveInfinity;
    }

    void OnDisable()
    {
        if (emitters != null) foreach (var ps in emitters) if (ps) ps.Stop();
        if (vision) vision.SetExposure(0f);
    }
    public void ResetForPlay()
    {
        if (dustMaterial && (runtimeMaterial == null || runtimeMaterial.shader != dustMaterial.shader))
        {
            if (runtimeMaterial) Destroy(runtimeMaterial);
            runtimeMaterial = new Material(dustMaterial);
        }
        if (emitters != null && runtimeMaterial) foreach (var ps in emitters)
        {
            if (!ps) continue;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = runtimeMaterial;
            var main = ps.main;
            main.startColor = new Color(0.72f, 0.58f, 0.39f, 0.5f);
        }
        System.Array.Clear(clouds, 0, clouds.Length);
        nextCloud = 0;
        sampleTimer = intensity = 0f;
        previousPosition = transform.position;
        previousForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (emitters != null) foreach (var ps in emitters)
        {
            if (!ps) continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play();
        }
    }

    void OnEnable()
    {
        previousPosition = transform.position;
        previousForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (emitters != null) foreach (var ps in emitters) if (ps) ps.Play();
    }
    void OnDestroy() { if (runtimeMaterial) Destroy(runtimeMaterial); }
}
