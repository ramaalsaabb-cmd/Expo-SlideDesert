using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Persistent stereo fade, with uninterrupted black coverage during scene activation.</summary>
[DefaultExecutionOrder(10000)]
public class DesertSceneTransition : MonoBehaviour
{
    public string nextScene = "DesertOverview";
    public float fadeSeconds = 1.2f;
    static DesertSceneTransition active;
    CanvasGroup veil;
    Canvas canvas;
    RectTransform overlay;
    Camera view;
    Camera loadingCamera;
    bool transitioning;
    Material fadeMaterial;

    void Awake()
    {
        if (active && active != this) { Destroy(gameObject); return; }
        active = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        Application.onBeforeRender += FollowCamera;
    }
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Application.onBeforeRender -= FollowCamera;
        if (active == this) active = null;
        if (fadeMaterial) Destroy(fadeMaterial);
    }
    void LateUpdate() { FollowCamera(); }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!transitioning) return;
        // The same fully opaque overlay is rebound before the new scene's first render.
        view = FindSceneCamera(scene);
        if (view) BindCamera();
    }
    static Camera FindSceneCamera(Scene scene)
    {
        Camera fallback = null;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var candidate in root.GetComponentsInChildren<Camera>(true))
            {
                if (!candidate.isActiveAndEnabled) continue;
                if (candidate.CompareTag("MainCamera")) return candidate;
                if (!fallback) fallback = candidate;
            }
        return fallback;
    }
    void CreateOverlay()
    {
        var obj = new GameObject("Persistent Scene Fade", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        obj.transform.SetParent(transform, false);
        overlay = obj.GetComponent<RectTransform>();
        canvas = obj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32767;
        veil = obj.GetComponent<CanvasGroup>();
        veil.alpha = 0f;
        veil.interactable = false;
        veil.blocksRaycasts = false;
        var panel = new GameObject("Black", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(obj.transform, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = panel.GetComponent<Image>();
        image.color = Color.black;
        var shader = Shader.Find("UI/Default");
        if (shader)
        {
            fadeMaterial = new Material(shader) { name = "Scene Fade Always Visible" };
            fadeMaterial.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            image.material = fadeMaterial;
        }
        image.raycastTarget = false;
        BindCamera();
    }
    void BindCamera()
    {
        if (!view || !overlay) return;
        canvas.worldCamera = view;
        int layer = view.gameObject.layer;
        if ((view.cullingMask & (1 << layer)) == 0)
            for (int i = 0; i < 32; i++)
                if ((view.cullingMask & (1 << i)) != 0) { layer = i; break; }
        overlay.gameObject.layer = layer;
        overlay.GetChild(0).gameObject.layer = layer;
        FollowCamera();
    }
    void FollowCamera()
    {
        if (!view || !overlay) return;
        float distance = Mathf.Max(view.nearClipPlane + 0.05f, 0.3f);
        // Overscan both eye projections and their horizontal lens offsets.
        float halfWidth = 0f, halfHeight = 0f;
        if (view.stereoEnabled)
        {
            for (int eye = 0; eye < 2; eye++)
            {
                var projection = view.GetStereoProjectionMatrix((Camera.StereoscopicEye)eye);
                halfWidth = Mathf.Max(halfWidth, distance * (1f + Mathf.Abs(projection.m02)) / Mathf.Max(0.01f, Mathf.Abs(projection.m00)));
                halfHeight = Mathf.Max(halfHeight, distance * (1f + Mathf.Abs(projection.m12)) / Mathf.Max(0.01f, Mathf.Abs(projection.m11)));
            }
        }
        else if (view.orthographic)
        {
            halfHeight = view.orthographicSize;
            halfWidth = halfHeight * view.aspect;
        }
        else
        {
            halfHeight = distance * Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * 0.5f);
            halfWidth = halfHeight * view.aspect;
        }
        overlay.sizeDelta = new Vector2((halfWidth + 0.15f) * 2.5f, (halfHeight + 0.05f) * 2.5f);
        overlay.localScale = Vector3.one;
        overlay.SetPositionAndRotation(view.transform.position + view.transform.forward * distance, view.transform.rotation);
    }
    public void Begin()
    {
        if (transitioning || active != this) return;
        if (!Application.CanStreamedLevelBeLoaded(nextScene))
        {
            Debug.LogError("Missing destination scene in build settings: " + nextScene, this);
            return;
        }
        view = Camera.main;
        if (!view) { Debug.LogError("Scene transition requires an active MainCamera.", this); return; }
        transitioning = true;
        CreateOverlay();
        StartCoroutine(Transition());
    }
    IEnumerator Fade(float from, float to)
    {
        float duration = Mathf.Max(0.01f, fadeSeconds);
        veil.alpha = from;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            veil.alpha = Mathf.Lerp(from, to, t * t * (3f - 2f * t));
            yield return null;
        }
        veil.alpha = to;
    }
    IEnumerator Transition()
    {
        yield return Fade(0f, 1f);
        // A persistent black camera covers the interval where the source camera is destroyed.
        var backup = new GameObject("Black Loading Camera");
        backup.transform.SetParent(transform, false);
        loadingCamera = backup.AddComponent<Camera>();
        loadingCamera.CopyFrom(view);
        loadingCamera.clearFlags = CameraClearFlags.SolidColor;
        loadingCamera.backgroundColor = Color.black;
        loadingCamera.cullingMask = 0;
        loadingCamera.depth = 10000f;
        backup.transform.SetPositionAndRotation(view.transform.position, view.transform.rotation);
        yield return null;
        var operation = SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single);
        if (operation == null)
        {
            loadingCamera.enabled = false;
            yield return Fade(1f, 0f);
            Destroy(gameObject);
            yield break;
        }
        yield return operation;
        float deadline = Time.realtimeSinceStartup + 5f;
        while ((!view || !view.isActiveAndEnabled) && Time.realtimeSinceStartup < deadline)
        {
            view = FindSceneCamera(SceneManager.GetActiveScene());
            yield return null;
        }
        if (!view || !view.isActiveAndEnabled)
        {
            Debug.LogError("Destination has no active camera; keeping the transition black.", this);
            yield break;
        }
        BindCamera();
        // Allow Start, XR tracking, and the new camera's first frames to settle under black.
        yield return null;
        yield return null;
        loadingCamera.enabled = false;
        yield return null;
        yield return Fade(1f, 0f);
        overlay.gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
