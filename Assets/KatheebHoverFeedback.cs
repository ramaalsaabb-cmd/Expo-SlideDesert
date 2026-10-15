using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class KatheebHoverFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    Vector3 restingScale;
    Outline glow;
    bool hovered, selected;
    void Awake()
    {
        restingScale=transform.localScale;
        glow=gameObject.AddComponent<Outline>();
        glow.effectColor=new Color(1f,0.56f,0.08f,0f);
        glow.effectDistance=new Vector2(2f,-2f);
        glow.useGraphicAlpha=true;
    }
    void Update()
    {
        bool active=(hovered || selected) && GetComponent<Selectable>().IsInteractable();
        float blend=1f-Mathf.Exp(-14f*Time.unscaledDeltaTime);
        transform.localScale=Vector3.Lerp(transform.localScale,restingScale*(active?1.045f:1f),blend);
        if(glow) glow.effectColor=Color.Lerp(glow.effectColor,new Color(1f,0.6f,0.12f,active?0.9f:0f),blend);
    }
    public void OnPointerEnter(PointerEventData data) { hovered=true; }
    public void OnPointerExit(PointerEventData data) { hovered=false; }
    public void OnSelect(BaseEventData data) { selected=true; }
    public void OnDeselect(BaseEventData data) { selected=false; }
    void OnDisable() { hovered=selected=false;transform.localScale=restingScale; }
}
