using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>Selects an authored Timeline. Movement, poses and camera curves live in Timeline assets.</summary>
public class DesertCinematicSequence : MonoBehaviour
{
    public PlayableDirector muflehTimeline;
    public PlayableDirector saqeeraTimeline;
    public Animator[] characterAnimators;
    public string configurationVersion;
    public Camera cinematicCamera;
    IEnumerator Start()
    {
        if (!muflehTimeline || !saqeeraTimeline)
        {
            Debug.LogError("Desert cinematic Timeline assets/bindings have not been built.", this);
            yield break;
        }
        if (cinematicCamera)
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                {
                    camera.enabled = camera == cinematicCamera;
                    var listener = camera.GetComponent<AudioListener>();
                    if (listener) listener.enabled = camera == cinematicCamera;
                }
        }
        foreach (var animator in characterAnimators)
            if (animator) { animator.enabled = true; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
        var director = DesertCharacterSelection.MuflehSelected ? muflehTimeline : saqeeraTimeline;
        director.time = 0;
        director.RebuildGraph();
        director.Evaluate();
        // Establish all six initial poses before rendering, then begin playback.
        yield return null;
        director.Play();
    }
}
