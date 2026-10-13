using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Fade both headset eyes through a camera-space canvas before changing scenes.</summary>
public class DesertSceneTransition : MonoBehaviour
{
    public string nextScene = "DesertOverview";
    public float fadeSeconds = 1.2f;
    CanvasGroup veil;
    bool transitioning;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (transitioning) AttachVeil();
    }
    void AttachVeil()
    {
        var camera = Camera.main;
        if (!camera) return;
        if (veil) Destroy(veil.gameObject);
        var overlay = new GameObject("Scene Fade", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        overlay.transform.SetParent(camera.transform, false);
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = camera.nearClipPlane + 0.01f;
        canvas.sortingOrder = 32767;
        veil = overlay.GetComponent<CanvasGroup>();
        veil.alpha = 1f;
        veil.blocksRaycasts = false;
        var panel = new GameObject("Black", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(overlay.transform, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = Color.black;
        panel.GetComponent<Image>().raycastTarget = false;
    }
    public void Begin()
    {
        if (transitioning) return;
        if (!Application.CanStreamedLevelBeLoaded(nextScene))
        {
            Debug.LogError("Missing destination scene in build settings: " + nextScene, this);
            return;
        }
        transitioning = true;
        StartCoroutine(Transition());
    }
    IEnumerator Fade(float from, float to)
    {
        float duration = Mathf.Max(0.01f, fadeSeconds);
        for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
        {
            if (veil) veil.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        if (veil) veil.alpha = to;
    }
    IEnumerator Transition()
    {
        AttachVeil();
        yield return Fade(0f, 1f);
        yield return SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single);
        yield return null;
        yield return Fade(1f, 0f);
        if (veil) Destroy(veil.gameObject);
        Destroy(gameObject);
    }
}
