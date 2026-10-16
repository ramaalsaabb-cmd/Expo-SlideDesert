using UnityEngine;
using UnityEngine.UI;

/// <summary>Head-following stereo eyelids and a soft dust veil; never overrides XR tracking.</summary>
[RequireComponent(typeof(Camera))]
public class VRDustVision : MonoBehaviour
{
    [Range(0f, 1f)] public float blinkThreshold = 0.65f;
    public float recoverySeconds = 0.8f;
    public float blinkInterval = 12f;
    [Range(0f, 0.5f)] public float maximumVeil = 0.01f;
    public bool effectEnabled = true;
    public bool blinkingEnabled = false;
    Image veil;
    CurvedBlinkGraphic eyelids;
    GameObject overlay;
    float exposure, irritation, nextBlink, blinkTime = -1f;
    bool blinkArmed = true;
    public int blinkCount { get; private set; }
    public float currentClosure { get; private set; }
    ParticleSystem eyeDust;
    Material eyeMaterial;
    readonly ParticleSystem.Particle[] eyeParticles = new ParticleSystem.Particle[24];
    float gustArrival = -1f, gustStrength, nextGust;
    Vector3 previousHead;
    bool headReady;

    // A wind-borne gust travels from the turning car to the seated rider.
    public void QueueTurnDust(Material material, float strength, float travelSeconds)
    {
        if (!effectEnabled || gustArrival >= 0f || Time.time < nextGust || !material) return;
        if (!eyeDust)
        {
            var obj = new GameObject("Turn dust at eyes");
            obj.layer = gameObject.layer;
            obj.transform.SetParent(transform, false);
            eyeDust = obj.AddComponent<ParticleSystem>();
            eyeDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = eyeDust.main;
            main.loop = true; main.playOnAwake = false; main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = eyeDust.emission; emission.enabled = false;
            var shape = eyeDust.shape; shape.enabled = false;
            var fade = eyeDust.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            eyeMaterial = new Material(material);
            if (eyeMaterial.HasProperty("_AlphaClip")) eyeMaterial.SetFloat("_AlphaClip", 0f);
            eyeMaterial.DisableKeyword("_ALPHATEST_ON");
            var renderer = eyeDust.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = eyeMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            eyeDust.Play();
        }
        if (!eyeDust.isPlaying) eyeDust.Play();
        gustStrength = Mathf.Clamp01(strength);
        gustArrival = Time.time + Mathf.Clamp(travelSeconds, 0.4f, 1.5f);
        nextGust = Time.time + Mathf.Max(12f, blinkInterval);
    }

    float UpdateEyeDust()
    {
        Vector3 head = transform.position;
        Vector3 movement = headReady ? head - previousHead : Vector3.zero;
        previousHead = head; headReady = true;
        if (!eyeDust) return 0f;
        // The gust is carried with the rider's moving air, and crosses the face briefly.
        int count = eyeDust.GetParticles(eyeParticles);
        float amount = 0f;
        for (int i = 0; i < count; i++)
        {
            eyeParticles[i].position += movement;
            if (Vector3.Distance(eyeParticles[i].position, head) < 0.85f) amount = gustStrength;
        }
        eyeDust.SetParticles(eyeParticles, count);
        if (gustArrival >= 0f && Time.time >= gustArrival)
        {
            gustArrival = -1f;
            for (int i = 0; i < 8; i++)
            {
                var birth = new ParticleSystem.EmitParams();
                birth.position = head + transform.forward * 1.6f + transform.right * Random.Range(-0.3f, 0.3f)
                    + transform.up * Random.Range(-0.25f, 0.1f);
                birth.velocity = -transform.forward * 3.5f;
                birth.startSize = Random.Range(0.3f, 0.5f);
                birth.startLifetime = 0.65f;
                birth.startColor = new Color(0.64f, 0.53f, 0.42f, 0.22f);
                eyeDust.Emit(birth, 1);
            }
        }
        return amount;
    }
    const float BlinkDuration = 0.36f;

    void Start() { EnsureOverlay(); }

    void EnsureOverlay()
    {
        if (overlay && veil && eyelids) return;
        if (overlay) { overlay.SetActive(false); Destroy(overlay); }
        var cam = GetComponent<Camera>();
        overlay = new GameObject("Dust Vision Overlay", typeof(RectTransform), typeof(Canvas));
        overlay.layer = gameObject.layer;
        overlay.transform.SetParent(transform, false);
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        canvas.sortingOrder = 32000;
        float distance = Mathf.Max(cam.nearClipPlane + 0.05f, 0.25f);
        overlay.transform.localPosition = Vector3.forward * distance;
        // Match the actual projection so a smooth lid closure is visible throughout the blink.
        Matrix4x4 projection = cam.stereoEnabled ? cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left) : cam.projectionMatrix;
        float height = 2f * distance / Mathf.Max(0.1f, Mathf.Abs(projection.m11)) * 1.15f;
        var rect = overlay.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(height * Mathf.Max(1.8f, cam.aspect * 1.15f), height);
        rect.localScale = Vector3.one;
        veil = MakeImage("Dust veil", new Color(0.66f, 0.52f, 0.34f, 0f));
        var lids = new GameObject("Curved eyelids", typeof(RectTransform), typeof(CanvasRenderer), typeof(CurvedBlinkGraphic));
        lids.layer = gameObject.layer;
        lids.transform.SetParent(overlay.transform, false);
        eyelids = lids.GetComponent<CurvedBlinkGraphic>();
        eyelids.color = new Color(0.025f, 0.018f, 0.012f, 1f);
        eyelids.raycastTarget = false;
        eyelids.rectTransform.anchorMin = Vector2.zero;
        eyelids.rectTransform.anchorMax = Vector2.one;
        eyelids.rectTransform.offsetMin = eyelids.rectTransform.offsetMax = Vector2.zero;
        SetLids(0f);
    }

    Image MakeImage(string label, Color color)
    {
        var obj = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.layer = gameObject.layer;
        obj.transform.SetParent(overlay.transform, false);
        var img = obj.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        img.rectTransform.anchorMin = Vector2.zero;
        img.rectTransform.anchorMax = Vector2.one;
        img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
        return img;
    }

    public void SetExposure(float amount) { exposure = Mathf.Clamp01(amount); }

    void LateUpdate()
    {
        EnsureOverlay();
        if (!overlay) return;
        overlay.SetActive(effectEnabled);
        if (!effectEnabled) { irritation = 0f; blinkTime = -1f; SetLids(0f); return; }
        float eyeExposure = Mathf.Max(exposure, UpdateEyeDust());
        float dt = Time.deltaTime;
        irritation = Mathf.MoveTowards(irritation, eyeExposure,
            dt / (eyeExposure > irritation ? 0.18f : Mathf.Max(0.1f, recoverySeconds)));
        var tint = veil.color;
        tint.a = irritation * maximumVeil;
        veil.color = tint;
        // Rearm only after leaving the cloud: sustained dust never repeats blinks.
        if (eyeExposure < 0.18f) blinkArmed = true;
        if (blinkingEnabled && blinkArmed && blinkTime < 0f && eyeExposure >= blinkThreshold && Time.time >= nextBlink)
        {
            blinkArmed = false;
            blinkTime = 0f;
            blinkCount++;
            nextBlink = Time.time + Mathf.Max(0.5f, blinkInterval);
        }
        if (!blinkingEnabled) blinkTime = -1f;
        float closure = 0f;
        if (blinkTime >= 0f)
        {
            blinkTime += dt;
            float phase = Mathf.Clamp01(blinkTime / BlinkDuration);
            // Gentle acceleration and deceleration, with a slower opening.
            closure = phase < 0.36f ? EaseLid(phase / 0.36f)
                : phase < 0.40f ? 1f : 1f - EaseLid((phase - 0.40f) / 0.60f);
            if (phase >= 1f) blinkTime = -1f;
        }
        SetLids(closure);
    }

    static float EaseLid(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }
    void SetLids(float closure)
    {
        currentClosure = closure;
        if (!eyelids) return;
        eyelids.SetClosure(closure);
        eyelids.gameObject.SetActive(closure > 0.001f);
    }
    public void ResetForPlay()
    {
        exposure = irritation = nextBlink = 0f;
        blinkCount = 0;
        gustArrival = -1f; nextGust = 0f; headReady = false;
        if (eyeDust) eyeDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        blinkTime = -1f;
        blinkArmed = true;
        if (overlay)
        {
            overlay.SetActive(effectEnabled);
            var color = veil.color;
            color.a = 0f;
            veil.color = color;
            SetLids(0f);
        }
    }
    void OnDisable() { exposure = irritation = 0f; blinkTime = -1f; if (overlay) overlay.SetActive(false); if (eyeDust) eyeDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); gustArrival = -1f; }
    void OnDestroy() { if (overlay) Destroy(overlay); if (eyeMaterial) Destroy(eyeMaterial); if (eyeDust) Destroy(eyeDust.gameObject); }
}










