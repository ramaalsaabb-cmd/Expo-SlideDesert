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
    public bool showConnectionDiagnostics = false;
    Text countdownNumber;
    CanvasGroup countdownGroup;
    Image backdrop;
    bool desktopMode;
    Behaviour poseDriver;
    Quaternion desktopViewRotation;
    float lookYaw, lookPitch;
    readonly List<Behaviour> suspended = new List<Behaviour>();
    readonly List<Camera> previews = new List<Camera>();
    readonly List<Material> portraitMaterials = new List<Material>();
    readonly List<RenderTexture> textures = new List<RenderTexture>();
    Camera player;
    GameObject menu, mainPanel, characterPanel, countdownPanel;
    [Header("Existing riders placed on the sled (optional explicit references)")]
    public GameObject manRider;
    public GameObject womanRider;
    GameObject selectedRider;
    RawImage selectedLabel, countdownLabel;
    Text espStatus;
    WebSocketSender espSender;
    bool counting, started;
    readonly Color background = new Color(0f, 0f, 0f, 0.76f);
    readonly Color accent = new Color(0.19f, 0.36f, 0.34f, 1f);
    static readonly HashSet<string> RideScripts = new HashSet<string> {
        "CarPathFollower", "CarTerrainFollow", "SledFollowCar", "SledMotionController",
        "WheelDustController", "SledDustController", "VRCameraEffects", "HardwareOutput"
    };
    void Awake() { espSender = WebSocketSender.EnsureForScene(gameObject.scene); SuspendRide(); }
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
        FindPlacedRiders();
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
        SelectCharacter(womanRider ? "Woman" : "Man", "");
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
        return Rect(name, menu.transform, new Vector2(1100,900), Vector2.zero).gameObject;
    }
    Rect SourceRegion(float x,float y,float width,float height,float imageWidth,float imageHeight)
    { return new Rect(x/imageWidth,1f-(y+height)/imageHeight,width/imageWidth,height/imageHeight); }
    RawImage Artwork(string name,string resource,Transform parent,Vector2 size,Vector2 at,Rect region,bool removeGrey=false,float corners=0f)
    {
        var rect=Rect(name,parent,size,at);var graphic=rect.gameObject.AddComponent<RawImage>();
        graphic.texture=Resources.Load<Texture2D>("KatheebUI/"+resource);graphic.uvRect=region;graphic.raycastTarget=false;
        var shader=Resources.Load<Shader>("KatheebUI/Artwork");
        if(!shader) shader=Shader.Find("UI/KatheebArtwork");
        if(shader)
        {
            var material=new Material(shader) {name="Katheeb "+name};
            material.SetVector("_UVRect",new Vector4(region.x,region.y,region.width,region.height));
            material.SetFloat("_ChromaKey",removeGrey?1f:0f);material.SetFloat("_Roundness",corners);
            material.SetFloat("_Aspect",size.x/size.y);graphic.material=material;portraitMaterials.Add(material);
        }
        return graphic;
    }
    Button ArtButton(string name,string resource,Transform parent,Vector2 size,Vector2 at,Rect region,float corners,UnityEngine.Events.UnityAction action)
    {
        var graphic=Artwork(name,resource,parent,size,at,region,false,corners);graphic.raycastTarget=true;
        var button=graphic.gameObject.AddComponent<Button>();button.targetGraphic=graphic;
        var colors=button.colors;colors.highlightedColor=new Color(1f,0.94f,0.78f);colors.pressedColor=new Color(0.8f,0.67f,0.45f);button.colors=colors;
        button.onClick.AddListener(action);button.gameObject.AddComponent<KatheebHoverFeedback>();return button;
    }
    void CreateMenu()
    {
        menu=Rect("Katheeb Arabic Menu",transform,new Vector2(1100,900),Vector2.zero).gameObject;
        menu.transform.localScale=Vector3.one*0.00135f;
        var canvas=menu.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=player;canvas.sortingOrder=200;
        if(desktopMode)
        {
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;menu.transform.localScale=Vector3.one;
            var scaler=menu.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1200,1000);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            menu.AddComponent<GraphicRaycaster>();
        }
        else menu.AddComponent<TrackedDeviceGraphicRaycaster>();
        var backingRect=Rect("Translucent Black Backing",menu.transform,new Vector2(1100,900),Vector2.zero);
        backingRect.anchorMin=Vector2.zero;backingRect.anchorMax=Vector2.one;
        backingRect.offsetMin=backingRect.offsetMax=Vector2.zero;
        backdrop=backingRect.gameObject.AddComponent<Image>();backdrop.color=background;backdrop.raycastTarget=false;
        var backingShader=Resources.Load<Shader>("KatheebUI/Artwork");
        if(backingShader)
        {
            var backingMaterial=new Material(backingShader) {name="Black Menu Backing"};
            backingMaterial.SetVector("_UVRect",new Vector4(0,0,1,1));
            backdrop.material=backingMaterial;portraitMaterials.Add(backingMaterial);
        }
        var statusRect=Rect("ESP diagnostics",menu.transform,new Vector2(1040,72),new Vector2(0,410));
        espStatus=statusRect.gameObject.AddComponent<Text>();espStatus.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");espStatus.fontSize=18;
        espStatus.alignment=TextAnchor.MiddleCenter;espStatus.raycastTarget=false;espStatus.gameObject.SetActive(showConnectionDiagnostics);
        espSender=WebSocketSender.EnsureForScene(gameObject.scene);
        mainPanel=Panel("Katheeb Main Menu");
        Artwork("Katheeb Logo","Menu",mainPanel.transform,new Vector2(700,420),new Vector2(0,175),SourceRegion(265,25,1000,610,1536,1024),true);
        ArtButton("Start Simulation","Menu",mainPanel.transform,new Vector2(820,124),new Vector2(0,-135),SourceRegion(297,644,949,148,1536,1024),0.48f,BeginCountdown);
        ArtButton("Choose Character","Menu",mainPanel.transform,new Vector2(820,124),new Vector2(0,-285),SourceRegion(297,802,949,148,1536,1024),0.48f,ChooseMenu);
        selectedLabel=Label("selectedWoman",mainPanel.transform,new Vector2(640,44),new Vector2(0,-390));selectedLabel.color=new Color(1f,0.81f,0.47f);
        characterPanel=Panel("Katheeb Character Selection");
        AddCard("Woman","","woman",new Vector2(-235,5));AddCard("Man","","man",new Vector2(235,5));
        countdownPanel=Panel("Katheeb Countdown");countdownGroup=countdownPanel.AddComponent<CanvasGroup>();countdownGroup.blocksRaycasts=false;
        countdownLabel=Artwork("Countdown Header","Countdown",countdownPanel.transform,new Vector2(1000,286),new Vector2(0,150),SourceRegion(500,145,850,245,1848,851));
        var numberRect=Rect("3 2 1",countdownPanel.transform,new Vector2(300,300),new Vector2(0,-100));
        countdownNumber=numberRect.gameObject.AddComponent<Text>();countdownNumber.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        countdownNumber.fontSize=245;countdownNumber.fontStyle=FontStyle.Bold;countdownNumber.alignment=TextAnchor.MiddleCenter;
        countdownNumber.color=new Color(1f,0.91f,0.72f);countdownNumber.raycastTarget=false;
        var glow=numberRect.gameObject.AddComponent<Outline>();glow.effectColor=new Color(1f,0.39f,0.06f,0.65f);glow.effectDistance=new Vector2(4,-4);
        foreach(int side in new[]{-1,1})
        {
            var line=Rect("Gold Countdown Line",countdownPanel.transform,new Vector2(150,4),new Vector2(side*220,-95)).gameObject.AddComponent<Image>();line.color=new Color(1f,0.72f,0.31f);line.raycastTarget=false;
            var diamond=Rect("Gold Diamond",countdownPanel.transform,new Vector2(18,18),new Vector2(side*295,-95));diamond.localRotation=Quaternion.Euler(0,0,45);
            var ornament=diamond.gameObject.AddComponent<Image>();ornament.color=new Color(1f,0.83f,0.5f);ornament.raycastTarget=false;
        }
    }
    void AddCard(string asset,string node,string label,Vector2 at)
    {
        Rect region=asset=="Woman"?SourceRegion(427,209,453,485,1774,887):SourceRegion(890,209,450,485,1774,887);
        ArtButton("Choose "+asset,"Cards",characterPanel.transform,new Vector2(410,444),at,region,0.075f,()=>SelectCharacter(asset,node));
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
        if(backdrop) backdrop.color=background;
        mainPanel.SetActive(true);characterPanel.SetActive(false);countdownPanel.SetActive(false);
        foreach(var preview in previews) preview.enabled=false;
    }
    void ChooseMenu()
    {
        if(counting || started) return;
        mainPanel.SetActive(false);characterPanel.SetActive(true);countdownPanel.SetActive(false);
        foreach(var preview in previews) preview.enabled=true;
    }
    void FindPlacedRiders()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!manRider && t.name == "Desert_Robe_Character_ROOT") manRider = t.gameObject;
                if (!womanRider && t.name == "Traditional_Woman_ROOT") womanRider = t.gameObject;
            }
        if (!manRider || !womanRider)
            Debug.LogError("Arabic menu: assign the manually placed manRider and womanRider in the Inspector.", this);
    }
    void SetRiderVisible(GameObject rider, bool visible)
    {
        if (!rider) return;
        // Control only the character subtree: the user's camera and sled stay active.
        rider.SetActive(visible);
        foreach (var renderer in rider.GetComponentsInChildren<Renderer>(true))
        {
            renderer.forceRenderingOff = false;
            renderer.enabled = true;
            if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
        }
    }
    void SelectCharacter(string asset,string node)
    {
        if (counting || started) return;
        var rider = asset == "Man" ? manRider : womanRider;
        if (!rider)
        {
            Debug.LogError("Arabic menu: placed " + asset + " rider was not found.", this);
            return;
        }
        selectedRider = rider;
        DesertCharacterSelection.Save(asset);
        SetRiderVisible(manRider, false);
        SetRiderVisible(womanRider, false);
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
        if(backdrop) backdrop.color=new Color(0f,0f,0f,0.58f);
        foreach(string number in new[]{"3","2","1"})
        {
            countdownNumber.text=number;
            for(float elapsed=0f;elapsed<1f;elapsed+=Time.unscaledDeltaTime)
            {
                float t=Mathf.Clamp01(elapsed/0.22f);
                countdownNumber.transform.localScale=Vector3.one*Mathf.Lerp(1.16f,1f,t*t*(3f-2f*t));
                countdownGroup.alpha=Mathf.Clamp01(elapsed/0.09f);
                yield return null;
            }
        }
        menu.SetActive(false);
        SetRiderVisible(selectedRider, true);
        foreach(var script in suspended) if(script) script.enabled=true;
        suspended.Clear();started=true;counting=false;
    }
    void Update()
    {
        if (espStatus)
        {
            if (!espSender)
            {
                espStatus.text = "ESP TEST v2 | ERROR: WebSocketSender component is missing";
                espStatus.color = new Color(1f,0.5f,0.35f);
            }
            else
            {
                espStatus.text = "ESP TEST v2 | " + espSender.serverURL + " | attempt " + espSender.connectionAttempts
                    + "\n" + (espSender.isActiveAndEnabled ? espSender.connectionStatus : "ERROR: Sender disabled")
                    + (string.IsNullOrEmpty(espSender.lastConnectionError) ? "" : " | " + espSender.lastConnectionError);
                espStatus.color = espSender.connectionStatus == "Connected" ? new Color(0.4f,1f,0.65f) : Color.white;
            }
        }
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
        foreach(var material in portraitMaterials) if(material) Destroy(material);
        foreach(var texture in textures) { if(texture) { texture.Release();Destroy(texture); } }
    }
}
