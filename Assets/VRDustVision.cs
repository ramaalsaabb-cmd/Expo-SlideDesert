using UnityEngine;
using UnityEngine.UI;

/// <summary>Head-following stereo eyelids and a soft dust veil; never overrides XR tracking.</summary>
[RequireComponent(typeof(Camera))]
public class VRDustVision : MonoBehaviour
{
    [Range(0f, 1f)] public float blinkThreshold = 0.65f;
    public float recoverySeconds = 0.8f;
    public float blinkInterval = 12f;
    [Range(0f, 0.5f)] public float maximumVeil = 0.06f;
    public bool effectEnabled = true;
    Image veil;
    RectTransform upper, lower;
    GameObject overlay;
    float exposure, irritation, nextBlink, blinkTime = -1f;
    bool blinkArmed = true;
    const float BlinkDuration = 0.14f;

    void Start()
    {
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
        // Overscan accommodates wide headset FOV and both stereo eye offsets.
        float height = 2f * distance * Mathf.Tan(75f * Mathf.Deg2Rad);
        var rect = overlay.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(height * 2.5f, height);
        rect.localScale = Vector3.one;
        veil = MakeImage("Dust veil", new Color(0.66f, 0.52f, 0.34f, 0f));
        upper = MakeImage("Upper eyelid", Color.black).rectTransform;
        lower = MakeImage("Lower eyelid", Color.black).rectTransform;
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
        if (!overlay) return;
        overlay.SetActive(effectEnabled);
        if (!effectEnabled) { irritation = 0f; blinkTime = -1f; SetLids(0f); return; }
        float dt = Time.deltaTime;
        irritation = Mathf.MoveTowards(irritation, exposure,
            dt / (exposure > irritation ? 0.18f : Mathf.Max(0.1f, recoverySeconds)));
        var tint = veil.color;
        tint.a = irritation * maximumVeil;
        veil.color = tint;
        // Rearm only after leaving the cloud: sustained dust never repeats blinks.
        if (exposure < 0.18f) blinkArmed = true;
        if (blinkArmed && blinkTime < 0f && exposure >= blinkThreshold && Time.time >= nextBlink)
        {
            blinkArmed = false;
            blinkTime = 0f;
            nextBlink = Time.time + Mathf.Max(0.5f, blinkInterval);
        }
        float closure = 0f;
        if (blinkTime >= 0f)
        {
            blinkTime += dt;
            float phase = Mathf.Clamp01(blinkTime / BlinkDuration);
            // Fast closing, brief closed phase, slower reopening.
            closure = phase < 0.3f ? Mathf.SmoothStep(0f, 1f, phase / 0.3f)
                : phase < 0.45f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (phase - 0.45f) / 0.55f);
            if (phase >= 1f) blinkTime = -1f;
        }
        SetLids(closure);
    }

    void SetLids(float closure)
    {
        upper.anchorMin = new Vector2(0f, 1f - closure * 0.5f);
        upper.anchorMax = Vector2.one;
        lower.anchorMin = Vector2.zero;
        lower.anchorMax = new Vector2(1f, closure * 0.5f);
        upper.offsetMin = upper.offsetMax = lower.offsetMin = lower.offsetMax = Vector2.zero;
        upper.gameObject.SetActive(closure > 0.001f);
        lower.gameObject.SetActive(closure > 0.001f);
    }
    public void ResetForPlay()
    {
        exposure = irritation = nextBlink = 0f;
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
    void OnDisable() { exposure = irritation = 0f; blinkTime = -1f; if (overlay) overlay.SetActive(false); }
    void OnDestroy() { if (overlay) Destroy(overlay); }
}
