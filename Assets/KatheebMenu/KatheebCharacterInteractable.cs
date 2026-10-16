using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Selection-only presentation; never modifies the rider or gameplay character rig.</summary>
public sealed class KatheebCharacterInteractable : MonoBehaviour
{
    public Animator animator;
    public string selectionKey;
    public float reactionSeconds=2f;
    public GameObject indicator;
    public Action<KatheebCharacterInteractable> selected;
    bool uiHovered,xrHovered,confirming;
    Coroutine reaction;
    static readonly int Idle=Animator.StringToHash("Idle"),React=Animator.StringToHash("React");
    void Awake()
    {
        var xr=GetComponent<XRSimpleInteractable>();
        if(xr)
        {
            xr.hoverEntered.AddListener(_=>{xrHovered=true;RefreshHover();});
            xr.hoverExited.AddListener(_=>{xrHovered=xr.isHovered;RefreshHover();});
            xr.selectEntered.AddListener(_=>Select());
        }
    }
    void OnEnable(){confirming=false;uiHovered=xrHovered=false;if(indicator)indicator.SetActive(false);if(animator){animator.applyRootMotion=false;animator.Play(Idle,0,0);}}
    public void SetUIHover(bool value){uiHovered=value;RefreshHover();}
    void RefreshHover()
    {
        if(confirming)return;
        bool active=uiHovered||xrHovered;
        if(indicator)indicator.SetActive(active);
        if(active&&reaction==null)reaction=StartCoroutine(ReactOnce());
        else if(!active&&reaction==null&&animator)animator.CrossFadeInFixedTime(Idle,.18f);
    }
    IEnumerator ReactOnce()
    {
        if(animator)animator.CrossFadeInFixedTime(React,.18f,0,0);
        yield return new WaitForSecondsRealtime(reactionSeconds);
        if(!confirming&&animator)animator.CrossFadeInFixedTime(Idle,.2f);
        reaction=null;
    }
    public void Select(){if(!confirming&&isActiveAndEnabled)selected?.Invoke(this);}
    public IEnumerator Confirm()
    {
        confirming=true;if(reaction!=null){StopCoroutine(reaction);reaction=null;}
        if(indicator)indicator.SetActive(true);
        if(animator)animator.CrossFadeInFixedTime(React,.15f,0,0);
        yield return new WaitForSecondsRealtime(reactionSeconds);
    }
    void OnDisable(){if(reaction!=null)StopCoroutine(reaction);reaction=null;uiHovered=xrHovered=false;}
}
