using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Shared mouse/XR UI hover feedback, with independent pointer ownership.</summary>
public sealed class KatheebVRButton : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler
{
    public Action<bool> hoverChanged;
    public Action clicked;
    public bool interactable=true;
    public bool animateScale=true;
    readonly HashSet<int> pointers=new HashSet<int>();
    Vector3 rest;
    Graphic graphic;
    Color baseColor;
    bool pressed;
    void Awake(){rest=transform.localScale;graphic=GetComponent<Graphic>();if(graphic)baseColor=graphic.color;}
    void Update()
    {
        bool active=interactable&&pointers.Count>0;
        float blend=1-Mathf.Exp(-15*Time.unscaledDeltaTime);
        if(animateScale)transform.localScale=Vector3.Lerp(transform.localScale,rest*(pressed&&interactable?.97f:(active?1.06f:1)),blend);
        if(graphic&&baseColor.a>0)graphic.color=Color.Lerp(graphic.color,active?new Color(Mathf.Min(1,baseColor.r*1.2f),Mathf.Min(1,baseColor.g*1.15f),baseColor.b,baseColor.a):baseColor,blend);
    }
    public void OnPointerEnter(PointerEventData data){if(interactable&&pointers.Add(data.pointerId)&&pointers.Count==1)hoverChanged?.Invoke(true);}
    public void OnPointerExit(PointerEventData data){if(pointers.Remove(data.pointerId)&&pointers.Count==0)hoverChanged?.Invoke(false);}
    public void Activate(){if(interactable)clicked?.Invoke();}
    public void OnPointerDown(PointerEventData data){if(interactable)pressed=true;}
    public void OnPointerUp(PointerEventData data){pressed=false;}
    void OnDisable(){pressed=false;pointers.Clear();hoverChanged?.Invoke(false);transform.localScale=rest;}
}
