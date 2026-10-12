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
    public float turnBoost = 1f;
    public float particlesPerWheel = 12f;
    public float cloudLifetime = 2.2f;
    public float cloudRadius = 2.4f;
    public LayerMask groundLayers = ~0;

    struct Cloud { public Vector3 position, velocity; public float born, strength; }
    readonly Cloud[] clouds = new Cloud[192];
    readonly RaycastHit[] groundHits = new RaycastHit[16];
    ParticleSystem[] emitters;
    Transform[] wheels;
    CarPathFollower car;
    ParticleSystem[][] templateSystems;
    float[][] timeRates, distanceRates;
    bool[] emitting;
    float[] wheelBudget;
    readonly System.Collections.Generic.List<Material> wheelMaterials = new System.Collections.Generic.List<Material>();
    Renderer[] wheelRenderers;
    public float measuredSpeed;
    public int groundedWheels;
    public int liveParticles;
    public float headExposure;
    Vector3 previousPosition, previousForward, previousHeadPosition;
    bool headPositionValid;
    float intensity, sampleTimer;
    int nextCloud;
    VRDustVision vision;

    void Start()
    {
        if (emitters == null) BuildWheelEffects();
    }

    void BuildWheelEffects()
    {
        car = GetComponent<CarPathFollower>();
        wheels = car.wheels ?? new Transform[0];
        if (emitters != null) foreach (var old in emitters) if (old)
        {
            old.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            old.gameObject.SetActive(false);
            Destroy(old.gameObject);
        }
        emitters = new ParticleSystem[wheels.Length];
        wheelRenderers = new Renderer[wheels.Length];
        templateSystems = new ParticleSystem[wheels.Length][];
        timeRates = new float[wheels.Length][];
        distanceRates = new float[wheels.Length][];
        emitting = new bool[wheels.Length];
        wheelBudget = new float[wheels.Length];
        foreach (var material in wheelMaterials) if (material) Destroy(material);
        wheelMaterials.Clear();
        if (!legacyDust)
        {
            Debug.LogWarning("Wheel dust: assign your Particle System to Legacy Dust on CarRoot.", this);
            return;
        }
        legacyDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        for (int i = 0; i < wheels.Length; i++)
        {
            if (!wheels[i]) continue;
            wheelRenderers[i] = wheels[i].GetComponentInChildren<Renderer>();
            // Clone the complete authored effect, including child systems and materials.
            var ps = Instantiate(legacyDust, transform);
            ps.gameObject.name = "Wheel Dust " + i;
            ps.gameObject.SetActive(true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var systems = ps.GetComponentsInChildren<ParticleSystem>(true);
            templateSystems[i] = systems;
            timeRates[i] = new float[systems.Length];
            distanceRates[i] = new float[systems.Length];
            for (int j = 0; j < systems.Length; j++)
            {
                var main = systems[j].main;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                // All appearance modules, textures, colors, sizes and curves stay authored.
                var emission = systems[j].emission;
                timeRates[i][j] = emission.rateOverTimeMultiplier;
                distanceRates[i][j] = emission.rateOverDistanceMultiplier;
            }
            var rootMain = ps.main;
            rootMain.loop = true;
            // Root births are placed explicitly at tire contact; retain authored appearance.
            var rootEmission = ps.emission;
            rootEmission.enabled = false;
            foreach (var renderer in ps.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    if (!materials[m]) continue;
                    var copy = new Material(materials[m]);
                    if (copy.HasProperty("_AlphaClip")) copy.SetFloat("_AlphaClip", 0f);
                    copy.DisableKeyword("_ALPHATEST_ON");
                    wheelMaterials.Add(copy);
                    materials[m] = copy;
                }
                renderer.sharedMaterials = materials;
            }
            emitters[i] = ps;
            ps.Play(true);
        }
        previousPosition = transform.position;
        previousForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
    }
    void LateUpdate()
    {
        // Jagged arrays are not restored by Unity's script hot reload.
        if (emitters == null || templateSystems == null || timeRates == null || wheelBudget == null)
            ResetForPlay();
        if (!playerCamera) playerCamera = Camera.main;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        Vector3 headVelocity = Vector3.zero;
        if (playerCamera)
        {
            Vector3 headPosition = playerCamera.transform.position;
            if (headPositionValid) headVelocity = (headPosition - previousHeadPosition) / dt;
            if (headVelocity.magnitude > 60f) headVelocity = Vector3.zero;
            previousHeadPosition = headPosition;
            headPositionValid = true;
        }
        Vector3 displacement = transform.position - previousPosition;
        float speed = Vector3.ProjectOnPlane(displacement, Vector3.up).magnitude / dt;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        float turn = Mathf.Abs(Vector3.SignedAngle(previousForward, forward, Vector3.up)) / dt;
        previousPosition = transform.position;
        previousForward = forward;
        // Ignore teleports rather than filling the entire map with a dust wake.
        if (displacement.magnitude > 5f) speed = 0f;
        float sharpTurn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(25f, Mathf.Max(45f, fullTurnRate), turn));
        float visibleTurn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 40f, turn));
        float target = Mathf.InverseLerp(minimumSpeed, Mathf.Max(minimumSpeed + 0.1f, fullDustSpeed), speed)
            * (1f + visibleTurn * Mathf.Min(turnBoost, 2f));
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
            emitters[i].transform.rotation = transform.rotation * legacyDust.transform.rotation;
            bool active = grounded && speed > minimumSpeed;
            var systems = templateSystems[i];
            for (int j = 1; j < systems.Length; j++)
            {
                if (!systems[j]) continue;
                var emission = systems[j].emission;
                emission.rateOverTimeMultiplier = timeRates[i][j] * (active ? intensity * 0.1f : 0f);
                emission.rateOverDistanceMultiplier = distanceRates[i][j] * (active ? intensity * 0.1f : 0f);
            }
            if (!emitters[i].isPlaying) emitters[i].Play(true);
            if (!active) wheelBudget[i] = 0f;
            else wheelBudget[i] += Mathf.Min(particlesPerWheel, 22f) * intensity * dt;
            int count = Mathf.Min(64, Mathf.FloorToInt(wheelBudget[i]));
            wheelBudget[i] -= count;
            for (int n = 0; n < count; n++)
            {
                var birth = new ParticleSystem.EmitParams();
                birth.position = contact + Vector3.up * 0.15f
                    + transform.right * Random.Range(-0.1f, 0.1f);
                birth.velocity = -forward * Random.Range(1.5f, 3.5f)
                    + Vector3.up * Random.Range(0.6f, 1.2f);
                var authored = emitters[i].main;
                birth.startSize = Mathf.Max(0.1f, authored.startSize.constantMax * Mathf.Lerp(0.6f, 0.8f, visibleTurn));
                birth.startLifetime = Mathf.Lerp(0.8f, 1.3f, sharpTurn);
                Color tint = authored.startColor.color;
                tint.a *= Mathf.Lerp(0.28f, 0.42f, visibleTurn);
                birth.startColor = tint;
                // Only a small portion of a sharp-turn wake rises to eye height.
                if (playerCamera && sharpTurn > 0.5f && n == 0)
                    birth.velocity = headVelocity + (playerCamera.transform.position - birth.position) / 1.25f;
                birth.applyShapeToPosition = false;
                emitters[i].Emit(birth, 1);
            }
            // Eye exposure follows a sharp-turn plume, with travel time from the wheels.
            if (sample && grounded && speed > minimumSpeed && sharpTurn > 0.5f && playerCamera)
                clouds[nextCloud++ % clouds.Length] = new Cloud {
                    position = contact,
                    velocity = headVelocity + (playerCamera.transform.position - contact) / 1.25f,
                    born = Time.time, strength = sharpTurn * 2.2f
                };
        }
        if (!playerCamera) playerCamera = Camera.main;
        if (!playerCamera) return;
        if (!vision) vision = playerCamera.GetComponent<VRDustVision>();
        if (!vision) return;
        if (sample && groundedWheels > 0 && speed > minimumSpeed && sharpTurn > 0.65f)
        {
            foreach (var emitter in emitters) if (emitter)
            {
                var renderer = emitter.GetComponent<ParticleSystemRenderer>();
                vision.QueueTurnDust(renderer.sharedMaterial, Mathf.Max(0.8f, sharpTurn),
                    Vector3.Distance(transform.position, playerCamera.transform.position) / 20f);
                break;
            }
        }
        float exposure = 0f;
        foreach (var cloud in clouds)
        {
            if (cloud.strength <= 0f) continue;
            float age = Time.time - cloud.born;
            if (age < 0f || age > cloudLifetime) continue;
            Vector3 center = cloud.position + cloud.velocity * age;
            float radius = Mathf.Min(cloudRadius, 2f);
            float proximity = 1f - Mathf.Clamp01(Vector3.Distance(center, playerCamera.transform.position) / radius);
            exposure = Mathf.Max(exposure, proximity * cloud.strength * Mathf.Clamp01(age / 0.8f) * (1f - age / 3f));
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
            if (bottom.y - height > 1.5f) continue;
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
        BuildWheelEffects();
        System.Array.Clear(clouds, 0, clouds.Length);
        nextCloud = 0;
        sampleTimer = intensity = 0f;
        headPositionValid = false;
    }
    void OnDestroy()
    {
        foreach (var material in wheelMaterials) if (material) Destroy(material);
    }

    void OnEnable()
    {
        previousPosition = transform.position;
        previousForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

    }

}






