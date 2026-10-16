using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

// Menu-only pointer: the displayed endpoint and activation share one world-space hit.
// Runs after seated-rig correction. Never raycasts against the floor or scenery.
[DefaultExecutionOrder(11000)]
public sealed class KatheebMenuPointer : MonoBehaviour
{
    sealed class Hand
    {
        public XRNode node;
        public Transform origin;
        public LineRenderer line;
        public Transform dot;
        public KatheebVRButton hover, pressed;
        public PointerEventData data;
        public bool wasDown, armed;
    }
    readonly List<Hand> hands = new List<Hand>();
    readonly List<System.Action> restore = new List<System.Action>();
    readonly List<Behaviour> visuals = new List<Behaviour>();
    KatheebMenuManager menu;
    List<KatheebVRButton> targets;
    Material material;
    bool stopped;

    public void Initialize(KatheebMenuManager owner, List<KatheebVRButton> buttons)
    {
        menu = owner; targets = buttons;
        var rig = owner.playerCamera.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
        if (!rig) { enabled = false; return; }
        foreach (var near in rig.GetComponentsInChildren<NearFarInteractor>(true))
        {
            var origin = near.farInteractionCaster?.castOrigin;
            if (!origin) continue;
            string path = origin.name;
            for (var p = origin.parent; p && p != rig.transform; p = p.parent) path += " " + p.name;
            XRNode node;
            if (path.ToLowerInvariant().Contains("left")) node = XRNode.LeftHand;
            else if (path.ToLowerInvariant().Contains("right")) node = XRNode.RightHand;
            else continue;
            if (hands.Exists(h => h.node == node)) continue;
            hands.Add(new Hand { node = node, origin = origin,
                data = new PointerEventData(EventSystem.current) { pointerId = node == XRNode.LeftHand ? -801 : -802 } });
        }
        // Keep the stock path if no usable tracked controller was found.
        if (hands.Count == 0) { enabled = false; return; }
        foreach (var near in rig.GetComponentsInChildren<NearFarInteractor>(true))
        { bool value = near.enableUIInteraction; restore.Add(() => { if (near) near.enableUIInteraction = value; }); near.enableUIInteraction = false; }
        foreach (var ray in rig.GetComponentsInChildren<XRRayInteractor>(true))
        { bool value = ray.enableUIInteraction; restore.Add(() => { if (ray) ray.enableUIInteraction = value; }); ray.enableUIInteraction = false; }
        foreach (var visual in rig.GetComponentsInChildren<CurveVisualController>(true)) Hide(visual);
        foreach (var visual in rig.GetComponentsInChildren<XRInteractorLineVisual>(true)) Hide(visual);
        var shader = Shader.Find("UI/Default");
        if (!shader) shader = Shader.Find("Sprites/Default");
        material = new Material(shader);
        material.color = Color.white;
        foreach (var hand in hands)
        {
            var obj = new GameObject("KATHEEB " + hand.node + " menu ray"); obj.transform.SetParent(transform, false);
            hand.line = obj.AddComponent<LineRenderer>(); hand.line.sharedMaterial = material;
            hand.line.useWorldSpace = true; hand.line.positionCount = 2;
            hand.line.startWidth = .002f; hand.line.endWidth = .002f;
            hand.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hand.line.receiveShadows = false;
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere); dot.name = "Actual button hit";
            dot.transform.SetParent(transform, false); dot.transform.localScale = Vector3.one * .012f;
            Destroy(dot.GetComponent<Collider>()); dot.GetComponent<Renderer>().sharedMaterial = material;
            hand.dot = dot.transform; dot.SetActive(false);
        }
    }
    void Hide(Behaviour visual)
    {
        bool value = visual.enabled; restore.Add(() => { if (visual) visual.enabled = value; });
        visual.enabled = false; visuals.Add(visual);
        // Disabling a visual controller alone can leave its last rendered line visible.
        foreach (var line in visual.GetComponentsInChildren<LineRenderer>(true))
        { bool visible = line.enabled; restore.Add(() => { if (line) line.enabled = visible; }); line.enabled = false; }
    }
    public static bool HitRect(RectTransform rect, Ray ray, out Vector3 point, out float distance)
    {
        point = default; distance = 0;
        if (!new Plane(rect.forward, rect.position).Raycast(ray, out distance) || distance > 6f) return false;
        point = ray.GetPoint(distance);
        return rect.rect.Contains((Vector2)rect.InverseTransformPoint(point));
    }
    void LateUpdate()
    {
        if (!menu || menu.State == KatheebMenuManager.MenuState.Countdown || menu.State == KatheebMenuManager.MenuState.Playing)
        { StopPointer(); enabled = false; return; }
        // Other controller visibility scripts may re-enable a stock line each frame.
        foreach (var visual in visuals) if (visual)
            foreach (var line in visual.GetComponentsInChildren<LineRenderer>(true)) line.enabled = false;
        foreach (var hand in hands) Tick(hand);
    }
    void Tick(Hand hand)
    {
        var device = InputDevices.GetDeviceAtXRNode(hand.node);
        bool tracked = hand.origin && hand.origin.gameObject.activeInHierarchy && device.isValid && (!device.TryGetFeatureValue(CommonUsages.isTracked, out bool t) || t);
        hand.line.enabled = tracked && hand.origin;
        if (!tracked || !hand.origin) { Clear(hand); hand.armed = false; hand.wasDown = false; return; }
        var ray = new Ray(hand.origin.position, hand.origin.forward);
        KatheebVRButton hit = null; Vector3 endpoint = ray.GetPoint(.55f); float nearest = 6f;
        foreach (var target in targets)
        {
            if (!target || !target.isActiveAndEnabled || !target.interactable) continue;
            bool blocked = false;
            foreach (var group in target.GetComponentsInParent<CanvasGroup>())
                if (!group.blocksRaycasts || !group.interactable || group.alpha < .01f) { blocked = true; break; }
            if (blocked) continue;
            if (HitRect((RectTransform)target.transform, ray, out var point, out float distance) && distance < nearest)
            { nearest = distance; hit = target; endpoint = point; }
        }
        hand.line.SetPosition(0, ray.origin); hand.line.SetPosition(1, endpoint);
        hand.line.startColor = hand.line.endColor = hit ? new Color(1,.78f,.35f) : new Color(.7f,.75f,.8f);
        hand.dot.gameObject.SetActive(hit);
        hand.dot.position = endpoint - ray.direction * .008f;
        if (hand.hover != hit)
        {
            if (hand.hover) hand.hover.OnPointerExit(hand.data);
            hand.hover = hit; if (hit) hit.OnPointerEnter(hand.data);
        }
        bool down = device.TryGetFeatureValue(CommonUsages.triggerButton, out bool trigger) && trigger;
        if (!down) hand.armed = true; // Opening/reconnecting while held must not click.
        if (down && !hand.wasDown && hand.armed)
        { hand.pressed = hit; if (hit) hit.OnPointerDown(hand.data); }
        if (!down && hand.wasDown)
        {
            var pressed = hand.pressed; hand.pressed = null;
            if (pressed) pressed.OnPointerUp(hand.data);
            if (pressed && pressed == hit && pressed.interactable) pressed.Activate();
        }
        hand.wasDown = down;
    }
    void Clear(Hand hand)
    {
        if (hand.hover) hand.hover.OnPointerExit(hand.data);
        if (hand.pressed) hand.pressed.OnPointerUp(hand.data);
        hand.hover = hand.pressed = null;
        if (hand.line) hand.line.enabled = false;
        if (hand.dot) hand.dot.gameObject.SetActive(false);
    }
    void StopPointer()
    {
        if (stopped) return; stopped = true;
        foreach (var hand in hands) Clear(hand);
        foreach (var action in restore) action(); restore.Clear();
    }
    void OnDisable() { StopPointer(); }
    void OnDestroy() { StopPointer(); if (material) Destroy(material); }
}
