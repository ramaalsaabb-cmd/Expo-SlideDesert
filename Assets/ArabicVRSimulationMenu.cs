using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>Arabic VR test menu. Pauses driving, swaps only the rider, then counts down.</summary>
[DefaultExecutionOrder(-10000)]
public class ArabicVRSimulationMenu : MonoBehaviour
{
    [Header("Unity Play Mode testing")]
    public bool desktopTestInEditor = true;
    bool desktopMode;
    Behaviour poseDriver;
    Quaternion desktopViewRotation;
    float lookYaw, lookPitch;
    readonly List<Behaviour> suspended = new List<Behaviour>();
    readonly List<Camera> previews = new List<Camera>();
    readonly List<RenderTexture> textures = new List<RenderTexture>();
    Camera player;
    GameObject menu, mainPanel, characterPanel, countdownPanel;
    Transform riderSlot;
    GameObject originalRider, selectedRider;
    RawImage selectedLabel, countdownLabel;
    bool counting, started;
    readonly Color background = new Color(0.055f, 0.075f, 0.085f, 0.98f);
    readonly Color accent = new Color(0.19f, 0.36f, 0.34f, 1f);
    static readonly HashSet<string> RideScripts = new HashSet<string> {
        "CarPathFollower", "CarTerrainFollow", "SledFollowCar", "SledMotionController",
        "WheelDustController", "SledDustController", "VRCameraEffects", "HardwareOutput", "WebSocketSender"
    };
    void Awake() { SuspendRide(); }
    void SuspendRide()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (script && script.enabled && RideScripts.Contains(script.GetType().Name))
                { suspended.Add(script); script.enabled = false; }
    }
    IEnumerator Start()
    {
        // Capture dust scripts installed by the project's runtime scene setup too.
        SuspendRide();
        player = Camera.main;
        if (!player) { Debug.LogError("Arabic VR menu: MainCamera missing.", this); yield break; }
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Woman_ROOT" || t.name == "Man_ROOT")
                { originalRider = t.gameObject; riderSlot = t.parent; break; }
        desktopMode = Application.isEditor && desktopTestInEditor && !XRSettings.isDeviceActive;
        if (desktopMode)
        {
            foreach (var component in player.GetComponents<MonoBehaviour>())
                if (component && component.enabled && component.GetType().Name == "TrackedPoseDriver")
                { poseDriver = component; poseDriver.enabled = false; }
            desktopViewRotation = player.transform.localRotation;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        ConfigureInput();
        CreateMenu();
        // Give seated XR calibration time to establish the initial view.
        yield return null;
        yield return null;
        PlaceMenu();
        MainMenu();
    }
    void ConfigureInput()
    {
        var events = EventSystem.current;
        if (!events) events = new GameObject("VR Menu Event System").AddComponent<EventSystem>();
        foreach (var module in events.GetComponents<BaseInputModule>())
            if (!(module is XRUIInputModule)) module.enabled = false;
        var xr = events.GetComponent<XRUIInputModule>();
        if (!xr) xr = events.gameObject.AddComponent<XRUIInputModule>();
        xr.enabled = true;
        xr.enableXRInput = !desktopMode;
        xr.enableMouseInput = true;
    }
    void PlaceMenu()
    {
        if (desktopMode) return;
        Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        menu.transform.SetPositionAndRotation(player.transform.position + forward * 1.8f,
            Quaternion.LookRotation(forward, Vector3.up));
    }
    RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 at)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = size; rect.anchoredPosition = at;
        return rect;
    }
    RawImage Label(string name, Transform parent, Vector2 size, Vector2 at)
    {
        var rect = Rect(name, parent, size, at);
        var label = rect.gameObject.AddComponent<RawImage>();
        label.texture = Resources.Load<Texture2D>("ArabicVRMenu/" + name);
        label.raycastTarget = false;
        return label;
    }
    Button Button(string title, Transform parent, Vector2 size, Vector2 at, UnityEngine.Events.UnityAction action)
    {
        var rect = Rect(title, parent, size, at);
        var image = rect.gameObject.AddComponent<Image>(); image.color = accent;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.3f,1.25f,1.05f,1f);
        colors.pressedColor = new Color(0.7f,0.8f,0.75f,1f);
        button.colors = colors;
        button.onClick.AddListener(action);
        Label(title, rect, new Vector2(size.x * 0.95f, size.y * 0.95f), Vector2.zero);
        return button;
    }
    GameObject Panel(string name)
    {
        return Rect(name, menu.transform, new Vector2(1100,760), Vector2.zero).gameObject;
    }
    void CreateMenu()
    {
        menu = Rect("القائمة العربية", transform, new Vector2(1100,760), Vector2.zero).gameObject;
        menu.transform.localScale = Vector3.one * 0.00135f;
        var canvas = menu.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = player; canvas.sortingOrder = 200;
        if (desktopMode)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            menu.transform.localScale = Vector3.one;
            var scaler = menu.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1200,850);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }
        if (desktopMode) menu.AddComponent<GraphicRaycaster>();
        else menu.AddComponent<TrackedDeviceGraphicRaycaster>();
        var backdrop = menu.AddComponent<Image>(); backdrop.color = background;
        mainPanel = Panel("القائمة الرئيسية");
        Label("title",mainPanel.transform,new Vector2(1000,130),new Vector2(0,265));
        Label("subtitle",mainPanel.transform,new Vector2(950,90),new Vector2(0,170));
        selectedLabel = Label("selectedWoman",mainPanel.transform,new Vector2(950,90),new Vector2(0,80));
        Button("change",mainPanel.transform,new Vector2(850,120),new Vector2(0,-45),ChooseMenu);
        Button("start",mainPanel.transform,new Vector2(850,120),new Vector2(0,-195),BeginCountdown);
        Label(desktopMode ? "desktopInstruction" : "instruction",mainPanel.transform,new Vector2(900,75),new Vector2(0,-310));
        characterPanel = Panel("اختيار الشخصية");
        Label("choose",characterPanel.transform,new Vector2(1000,120),new Vector2(0,285));
        AddCard("Man","Man_ROOT","man",new Vector2(240,-5));
        AddCard("Woman","Woman_ROOT","woman",new Vector2(-240,-5));
        Button("back",characterPanel.transform,new Vector2(750,90),new Vector2(0,-290),MainMenu);
        countdownPanel = Panel("العد التنازلي");
        Label("ready",countdownPanel.transform,new Vector2(1000,120),new Vector2(0,230));
        countdownLabel = Label("three",countdownPanel.transform,new Vector2(320,320),new Vector2(0,-10));
    }
    void AddCard(string asset, string node, string label, Vector2 at)
    {
        var card = Button(label,characterPanel.transform,new Vector2(430,440),at,() => SelectCharacter(asset,node));
        // The button caption stays below a live 3D portrait of the supplied model.
        var caption = card.GetComponentInChildren<RawImage>().rectTransform;
        caption.sizeDelta = new Vector2(410,90); caption.anchoredPosition = new Vector2(0,-170);
        var imageRect = Rect("معاينة الشخصية",card.transform,new Vector2(400,320),new Vector2(0,45));
        var image = imageRect.gameObject.AddComponent<RawImage>(); image.raycastTarget = false;
        var prefab = Resources.Load<GameObject>("VRCharacters/"+asset);
        var source = FindNode(prefab,node);
        if (!source) { card.interactable = false; return; }
        var stage = new GameObject("Character Preview " + asset);
        stage.transform.SetParent(transform,false);
        stage.transform.position = new Vector3(10000f + previews.Count*20f,10000f,10000f);
        var model = Instantiate(source.gameObject,stage.transform,false);
        foreach (var child in model.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=30;
        Bounds bounds = new Bounds(model.transform.position,Vector3.zero); bool first=true;
        foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
        { if(first) { bounds=renderer.bounds;first=false; } else bounds.Encapsulate(renderer.bounds); }
        var obj = new GameObject("Portrait Camera"); obj.transform.SetParent(stage.transform,false);
        var camera = obj.AddComponent<Camera>();
        camera.stereoTargetEye=StereoTargetEyeMask.None;
        camera.orthographic=true;camera.orthographicSize=Mathf.Max(0.2f,bounds.size.y*0.6f);
        camera.nearClipPlane=0.01f;camera.farClipPlane=20f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0.14f,0.18f,0.18f);
        camera.cullingMask=1<<30;
        obj.transform.position=bounds.center+Vector3.forward*Mathf.Max(2f,bounds.size.magnitude*2f);
        obj.transform.LookAt(bounds.center);
        var texture=new RenderTexture(512,512,24);texture.Create();textures.Add(texture);
        camera.targetTexture=texture;camera.enabled=false;image.texture=texture;previews.Add(camera);
        var lamp=new GameObject("Portrait Light");lamp.transform.SetParent(obj.transform,false);
        var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.range=15f;light.intensity=3f;light.cullingMask=1<<30;
    }
    static Transform FindNode(GameObject root, string name)
    {
        if (!root) return null;
        foreach(var child in root.GetComponentsInChildren<Transform>(true)) if(child.name==name) return child;
        return null;
    }
    void MainMenu()
    {
        if(counting || started) return;
        mainPanel.SetActive(true);characterPanel.SetActive(false);countdownPanel.SetActive(false);
        foreach(var preview in previews) preview.enabled=false;
    }
    void ChooseMenu()
    {
        if(counting || started) return;
        mainPanel.SetActive(false);characterPanel.SetActive(true);countdownPanel.SetActive(false);
        foreach(var preview in previews) preview.enabled=true;
    }
    void SelectCharacter(string asset,string node)
    {
        if(counting || started || !riderSlot || !originalRider) return;
        var source=FindNode(Resources.Load<GameObject>("VRCharacters/"+asset),node);
        if(!source) { selectedLabel.texture=Resources.Load<Texture2D>("ArabicVRMenu/error");MainMenu();return; }
        if(selectedRider) { selectedRider.SetActive(false);Destroy(selectedRider); }
        originalRider.SetActive(false);
        selectedRider=Instantiate(source.gameObject,riderSlot,false);
        selectedRider.transform.localPosition=originalRider.transform.localPosition;
        selectedRider.transform.localRotation=originalRider.transform.localRotation;
        selectedRider.transform.localScale=originalRider.transform.localScale;
        foreach(var body in selectedRider.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic=true;body.useGravity=false; }
        foreach(var filter in selectedRider.GetComponentsInChildren<MeshFilter>(true))
        {
            var view=filter.GetComponent<SeatedRiderView>();
            if(!view) view=filter.gameObject.AddComponent<SeatedRiderView>();
            view.Configure(player);
        }
        selectedLabel.texture=Resources.Load<Texture2D>("ArabicVRMenu/selected"+asset);
        MainMenu();
    }
    void BeginCountdown()
    {
        if(counting || started) return;
        counting=true;mainPanel.SetActive(false);characterPanel.SetActive(false);countdownPanel.SetActive(true);
        StartCoroutine(Countdown());
    }
    IEnumerator Countdown()
    {
        foreach(string number in new[]{"three","two","one"})
        { countdownLabel.texture=Resources.Load<Texture2D>("ArabicVRMenu/"+number);yield return new WaitForSecondsRealtime(1f); }
        menu.SetActive(false);
        foreach(var preview in previews) preview.enabled=false;
        foreach(var script in suspended) if(script) script.enabled=true;
        suspended.Clear();started=true;counting=false;
    }
    void Update()
    {
        if (!desktopMode || !menu || counting) return;
        var keyboard = Keyboard.current;
        if (keyboard == null || started) return;
        if (keyboard.cKey.wasPressedThisFrame) ChooseMenu();
        if (keyboard.escapeKey.wasPressedThisFrame) MainMenu();
        if (characterPanel.activeSelf)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) SelectCharacter("Man", "Man_ROOT");
            if (keyboard.digit2Key.wasPressedThisFrame) SelectCharacter("Woman", "Woman_ROOT");
        }
        else if (mainPanel.activeSelf && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            BeginCountdown();
    }
    void LateUpdate()
    {
        if (!desktopMode || !player) return;
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse != null && mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            lookYaw += delta.x * 0.12f; lookPitch -= delta.y * 0.12f;
        }
        if (keyboard != null)
        {
            float rate = 65f * Time.unscaledDeltaTime;
            if (keyboard.leftArrowKey.isPressed) lookYaw -= rate;
            if (keyboard.rightArrowKey.isPressed) lookYaw += rate;
            if (keyboard.upArrowKey.isPressed) lookPitch -= rate;
            if (keyboard.downArrowKey.isPressed) lookPitch += rate;
        }
        lookPitch = Mathf.Clamp(lookPitch, -65f, 65f);
        player.transform.localRotation = desktopViewRotation * Quaternion.Euler(lookPitch,lookYaw,0f);
    }
    void OnDestroy()
    {
        if (poseDriver) poseDriver.enabled = true;
        foreach(var texture in textures) { if(texture) { texture.Release();Destroy(texture); } }
    }
}
