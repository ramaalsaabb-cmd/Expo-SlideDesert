using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>World-space KATHEEB presentation. No hardware or network component is controlled here.</summary>
[DefaultExecutionOrder(-11000)]
public sealed class KatheebMenuManager : MonoBehaviour
{
    public enum MenuState {MainMenu,CharacterSelection,Transitioning,Countdown,Playing}
    public MenuState State {get;private set;}
    public bool IsReady {get;private set;}
    public KatheebGameplayStartAdapter gameplay;
    public Camera playerCamera;
    public GameObject manRider,womanRider;
    public GameObject muflehSelectionPrefab,saqeeraSelectionPrefab;
    public GameObject menuTentPrefab;
    public bool desktopTestInEditor=true;
    public string setupVersion;
    GameObject canvasObject,stage,mainContent,countdownObject,menuScenery;
    const float MenuCharacterScale=1.1f;
    Transform menuPresentationAnchor;
    Vector3 menuFloorCenter;
    RectTransform buttonGroup,logo;
    CanvasGroup content,countdown,selectionUI;
    Image backing;
    AudioSource countdownAudio;
    AudioClip countdownTick,countdownGo;
    Text number;
    RawImage hint;
    KatheebCharacterInteractable mufleh,saqeera;
    readonly List<KatheebVRButton> targets=new List<KatheebVRButton>();
    readonly List<Material> uiMaterials=new List<Material>();
    // Hide visuals only: XR tracking, colliders and hardware components stay live.
    readonly Dictionary<Renderer,bool> hiddenRideRenderers=new Dictionary<Renderer,bool>();
    readonly Dictionary<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor,bool> rayUIBlocks=new Dictionary<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor,bool>();
    readonly Dictionary<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor,bool> nearUIBlocks=new Dictionary<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor,bool>();
    RectTransform manHit,womanHit;
    string selectedKey;
    bool busy,desktop;
    Coroutine stateRoutine;
    public bool HasSelection=>!string.IsNullOrEmpty(selectedKey);
    void Awake()
    {
        busy=true;State=MenuState.MainMenu;
        if(gameplay){gameplay.Hold();HideRideVisuals();}
    }
    IEnumerator Start()
    {
        if(!playerCamera)playerCamera=Camera.main;
        if(!playerCamera||!gameplay||!manRider||!womanRider||!muflehSelectionPrefab||!saqeeraSelectionPrefab)
        {Debug.LogError("[KATHEEB] Menu references are incomplete. Run Tools > Desert VR > Install KATHEEB Interactive Menu.",this);yield break;}
        desktop=Application.isEditor&&desktopTestInEditor&&!XRSettings.isDeviceActive;
        // Do not inherit a default character silently. The user explicitly chooses before starting.
        manRider.SetActive(false);womanRider.SetActive(false);
        // The headset pose and XRSeatController calibration arrive asynchronously.
        // Two ordinary Update frames do not guarantee a valid final XR camera pose.
        if(!desktop)yield return WaitForTrackedMenuPose();
        else {yield return null;yield return null;}
        ConfigureEvents();BuildUI();BuildCharacters();
        PlaceMenu();BuildMenuScenery();AttachMenuToOrigin();stage.SetActive(false);countdownObject.SetActive(false);busy=false;IsReady=true;
        Debug.Log("[KATHEEB] Main menu ready. XR UI trigger input, 3D character interactions and desktop testing enabled. Protected hardware components remain enabled.",this);
    }
    IEnumerator WaitForTrackedMenuPose()
    {
        while(!TrackedHeadReady())yield return null;
        // Let the seat controller finish LateUpdate / before-render calibration.
        float elapsed=0;
        while(elapsed<.75f)
        {
            yield return new WaitForEndOfFrame();
            if(!TrackedHeadReady()){elapsed=0;continue;}
            elapsed+=Time.unscaledDeltaTime;
        }
    }
    static bool TrackedHeadReady()
    {
        if(!XRSettings.isDeviceActive)return false;
        var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if(!head.isValid)return false;
        if(head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked,out bool tracked)&&!tracked)return false;
        if(head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.userPresence,out bool present)&&!present)return false;
        return head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation,out Quaternion rotation);
    }
    void AttachMenuToOrigin()
    {
        if(desktop)return;
        var origin=playerCamera.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
        if(!origin)return;
        // Follow rig recentering, never individual head turns. Keep the existing
        // tracked camera, origin transform and seat/hardware components untouched.
        menuPresentationAnchor=new GameObject("KATHEEB VR Menu Presentation Anchor").transform;
        menuPresentationAnchor.SetParent(origin.transform,false);
        canvasObject.transform.SetParent(menuPresentationAnchor,true);
        stage.transform.SetParent(menuPresentationAnchor,true);
        if(menuScenery)menuScenery.transform.SetParent(menuPresentationAnchor,true);
    }
    void HideRideVisuals()
    {
        if(gameplay.carMovement)HideVisualsUnder(gameplay.carMovement.transform);
        if(gameplay.sledFollowing)HideVisualsUnder(gameplay.sledFollowing.transform);
        // The tow rope is a separate scene object, rather than a child of either vehicle.
        foreach(var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(renderer.gameObject.scene==gameObject.scene&&(renderer.name.Contains("Tow Rope")||renderer.name.Contains("TowRope")))HideRideRenderer(renderer);
    }
    void HideVisualsUnder(Transform root)
    {
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            // Preserve tracked controller/hand visuals inside the existing sled rig.
            if(renderer.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>())continue;
            HideRideRenderer(renderer);
        }
    }
    void HideRideRenderer(Renderer renderer)
    {
        if(hiddenRideRenderers.ContainsKey(renderer)){renderer.forceRenderingOff=true;return;}
        hiddenRideRenderers.Add(renderer,renderer.forceRenderingOff);renderer.forceRenderingOff=true;
    }
    void RestoreRideVisuals()
    {
        foreach(var entry in hiddenRideRenderers)if(entry.Key)entry.Key.forceRenderingOff=entry.Value;
        hiddenRideRenderers.Clear();
    }
    void ConfigureEvents()
    {
        var events=EventSystem.current;
        if(!events)events=new GameObject("KATHEEB EventSystem").AddComponent<EventSystem>();
        foreach(var module in events.GetComponents<BaseInputModule>())if(!(module is XRUIInputModule))module.enabled=false;
        var input=events.GetComponent<XRUIInputModule>();if(!input)input=events.gameObject.AddComponent<XRUIInputModule>();
        input.enabled=true;input.enableXRInput=true;input.enableMouseInput=true;
        if(!desktop)
        {
            foreach(var ray in FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(ray.name.ToLowerInvariant().Contains("teleport")||!ray.enableUIInteraction)continue;
                rayUIBlocks[ray]=ray.blockUIOnInteractableSelection;ray.blockUIOnInteractableSelection=false;
            }
            foreach(var near in FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(!near.enableUIInteraction)continue;
                nearUIBlocks[near]=near.blockUIOnInteractableSelection;near.blockUIOnInteractableSelection=false;
            }
        }
    }
    RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 position)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
        rect.sizeDelta=size;rect.anchoredPosition=position;return rect;
    }
    static Rect Region(float x,float y,float w,float h,float iw,float ih)=>new Rect(x/iw,1-(y+h)/ih,w/iw,h/ih);
    RawImage Art(string name,string resource,Transform parent,Vector2 size,Vector2 position,Rect uv,bool key=false,float round=0)
    {
        var image=Rect(name,parent,size,position).gameObject.AddComponent<RawImage>();
        image.texture=Resources.Load<Texture2D>(resource);image.uvRect=uv;image.raycastTarget=false;
        var shader=Resources.Load<Shader>("KatheebUI/Artwork");
        if(shader)
        {
            var material=new Material(shader){name="KATHEEB "+name};
            material.SetVector("_UVRect",new Vector4(uv.x,uv.y,uv.width,uv.height));material.SetFloat("_ChromaKey",key?1:0);
            material.SetFloat("_Roundness",round);material.SetFloat("_Aspect",size.x/size.y);image.material=material;uiMaterials.Add(material);
        }
        if(!image.texture)Debug.LogError("[KATHEEB] Missing UI artwork: "+resource,this);
        return image;
    }
    KatheebVRButton Target(Graphic graphic,System.Action click,bool scale=true)
    {
        graphic.raycastTarget=true;var target=graphic.gameObject.AddComponent<KatheebVRButton>();
        target.clicked=click;target.animateScale=scale;
        var button=graphic.gameObject.AddComponent<Button>();button.targetGraphic=graphic;button.transition=Selectable.Transition.None;button.onClick.AddListener(target.Activate);
        targets.Add(target);return target;
    }
    void BuildUI()
    {
        canvasObject=Rect("KATHEEB Interactive World Canvas",transform,new Vector2(1600,900),Vector2.zero).gameObject;
        canvasObject.transform.localScale=Vector3.one*.0015f;
        var canvas=canvasObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=playerCamera;canvas.sortingOrder=200;
        if(desktop)
        {
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvasObject.transform.localScale=Vector3.one;
            var scaler=canvasObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=1f;
            canvasObject.AddComponent<GraphicRaycaster>();
        }
        else
        {
            ((RectTransform)canvasObject.transform).sizeDelta=new Vector2(2400,1000);
            var raycaster=canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            raycaster.checkFor2DOcclusion=false;raycaster.checkFor3DOcclusion=false;
        }
        backing=Rect("Translucent Black Background",canvasObject.transform,new Vector2(6000,6000),Vector2.zero).gameObject.AddComponent<Image>();
        backing.color=new Color(0,0,0,.18f);backing.raycastTarget=false;
        mainContent=Rect("Main Menu Content",canvasObject.transform,new Vector2(1600,900),Vector2.zero).gameObject;content=mainContent.AddComponent<CanvasGroup>();
        logo=Art("كثيب KATHEEB Logo","KatheebUI/Menu",mainContent.transform,new Vector2(440,266),new Vector2(0,285),Region(265,25,1000,610,1536,1024),true).rectTransform;
        buttonGroup=Rect("Animated Main Buttons",mainContent.transform,new Vector2(650,240),new Vector2(0,-170));
        MakeMenuButton("Start","Start",new Vector2(0,65),RequestStart);
        MakeMenuButton("Choose characters","Choose",new Vector2(0,-65),OpenSelection);
        hint=Art("اختر شخصيتك أولاً","KatheebUI/October16/ChooseFirst",mainContent.transform,new Vector2(670,62),new Vector2(0,-390),new Rect(0,0,1,1));hint.gameObject.SetActive(false);
        var selection=Rect("Character Names and Trigger Targets",canvasObject.transform,new Vector2(1600,900),Vector2.zero).gameObject;selectionUI=selection.AddComponent<CanvasGroup>();selection.SetActive(false);
        manHit=CreateCharacterHit("Mufleh",selection.transform);womanHit=CreateCharacterHit("Saqeera",selection.transform);
        countdownObject=Rect("استعد للانطلاق Countdown",canvasObject.transform,new Vector2(1600,900),Vector2.zero).gameObject;
        countdown=countdownObject.AddComponent<CanvasGroup>();countdown.blocksRaycasts=false;
        Art("استعد للانطلاق","KatheebUI/Countdown",countdownObject.transform,new Vector2(1000,286),new Vector2(0,170),Region(500,145,850,245,1848,851));
        number=Rect("Animated 3 2 1",countdownObject.transform,new Vector2(300,280),new Vector2(0,-110)).gameObject.AddComponent<Text>();
        number.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");number.fontSize=240;number.fontStyle=FontStyle.Bold;number.alignment=TextAnchor.MiddleCenter;number.color=new Color(1,.91f,.72f);number.raycastTarget=false;
        var outline=number.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.9f,.32f,.035f,.9f);outline.effectDistance=new Vector2(4,-4);
        foreach(int side in new[]{-1,1})
        {
            var line=Rect("Gold Countdown Line",countdownObject.transform,new Vector2(150,3),new Vector2(side*230,-110)).gameObject.AddComponent<Image>();line.color=new Color(1,.73f,.34f);line.raycastTarget=false;
            var diamond=Rect("Desert Diamond",countdownObject.transform,new Vector2(16,16),new Vector2(side*305,-110));diamond.localRotation=Quaternion.Euler(0,0,45);
            var image=diamond.gameObject.AddComponent<Image>();image.color=new Color(1,.8f,.45f);image.raycastTarget=false;
        }
    }
    void MakeMenuButton(string title,string resource,Vector2 at,System.Action click)
    {
        var frame=Rect(title,buttonGroup,new Vector2(500,104),at).gameObject.AddComponent<RawImage>();
        frame.texture=Texture2D.whiteTexture;frame.color=Color.white;
        var shader=Resources.Load<Shader>("KatheebUI/DesertButton");
        if(shader){var material=new Material(shader){name="KATHEEB gold desert button"};frame.material=material;uiMaterials.Add(material);}
        Target(frame,click);
        Art(title+" Arabic Label","KatheebUI/October16/"+resource,frame.transform,new Vector2(365,76),new Vector2(5,0),new Rect(0,0,1,1));
        var accent=Rect("Gold desert diamond",frame.transform,new Vector2(10,10),new Vector2(205,0));accent.localRotation=Quaternion.Euler(0,0,45);
        var gem=accent.gameObject.AddComponent<Image>();gem.color=new Color(1,.8f,.43f);gem.raycastTarget=false;
    }
    RectTransform CreateCharacterHit(string label,Transform parent)
    {
        var rect=Rect(label+" XR Trigger Target",parent,new Vector2(260,700),Vector2.zero);
        var image=rect.gameObject.AddComponent<Image>();image.color=Color.clear;
        var target=Target(image,()=>{var actor=label=="Mufleh"?mufleh:saqeera;if(actor)actor.Select();},false);
        target.hoverChanged=value=>{var actor=label=="Mufleh"?mufleh:saqeera;if(actor)actor.SetUIHover(value);};
        Art(label+" Arabic Name","KatheebUI/October16/"+label,parent,new Vector2(250,70),Vector2.zero,new Rect(0,0,1,1)).gameObject.name=label+" Name";
        return rect;
    }
    void BuildCharacters()
    {
        stage=new GameObject("KATHEEB Selection Stage");stage.transform.SetParent(transform,false);
        mufleh=Instantiate(muflehSelectionPrefab,stage.transform).GetComponent<KatheebCharacterInteractable>();
        saqeera=Instantiate(saqeeraSelectionPrefab,stage.transform).GetComponent<KatheebCharacterInteractable>();
        mufleh.selected=SelectCharacter;saqeera.selected=SelectCharacter;
        if(!desktop)foreach(var actor in new[]{mufleh,saqeera})
        {
            // Use the same UI-press trigger as the buttons. Physics select/grab
            // must not compete with UI or hide the controller pointer.
            var xr=actor.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if(xr)xr.enabled=false;
            foreach(var collider in actor.GetComponentsInChildren<Collider>(true))collider.enabled=false;
        }
        stage.SetActive(false);
    }
    void PlaceMenu()
    {
        Vector3 forward=Vector3.ProjectOnPlane(playerCamera.transform.forward,Vector3.up).normalized;
        if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
        if(!desktop)canvasObject.transform.SetPositionAndRotation(playerCamera.transform.position+forward*2.4f,Quaternion.LookRotation(forward));
        var right=Vector3.Cross(Vector3.up,forward);
        menuFloorCenter=desktop
            ?playerCamera.transform.position+playerCamera.transform.forward*2.5f-Vector3.up*.95f
            :playerCamera.transform.position+forward*2.5f-Vector3.up*.95f;
        stage.transform.SetPositionAndRotation(playerCamera.transform.position+forward*3.0f,Quaternion.LookRotation(-forward));
        // Characters stand independently to the left of the shifted buttons.
        PlaceActor(mufleh,menuFloorCenter-right*.62f,forward);
        PlaceActor(saqeera,menuFloorCenter+right*.62f,forward);
        SyncCharacterUI(mufleh,manHit,"Mufleh Name");SyncCharacterUI(saqeera,womanHit,"Saqeera Name");
    }
    void PlaceActor(KatheebCharacterInteractable actor,Vector3 position,Vector3 forward)
    {
        position.y=menuFloorCenter.y;
        actor.transform.SetPositionAndRotation(position,Quaternion.LookRotation(-forward));
        foreach(var lod in actor.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);
    }
    void SyncCharacterUI(KatheebCharacterInteractable actor,RectTransform hit,string name)
    {
        Vector2 point;
        if(desktop)
        {
            var screen=playerCamera.WorldToScreenPoint(actor.transform.position+Vector3.up*.95f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvasObject.transform,screen,null,out point);
        }
        else
        {
            // Match the visible body in WORLD space. Each UI target lies just in
            // front of its own character, not on a head-projected shared plane.
            var forward=canvasObject.transform.forward;var right=canvasObject.transform.right;
            bool found=false;Bounds body=default;
            foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(!renderer.enabled)continue;
                if(!found){body=renderer.bounds;found=true;}else body.Encapsulate(renderer.bounds);
            }
            if(!found)body=new Bounds(actor.transform.position+Vector3.up*.95f,new Vector3(.65f,1.9f,.4f));
            float depth=Vector3.Dot(new Vector3(Mathf.Abs(forward.x),Mathf.Abs(forward.y),Mathf.Abs(forward.z)),body.extents);
            float width=2*Vector3.Dot(new Vector3(Mathf.Abs(right.x),Mathf.Abs(right.y),Mathf.Abs(right.z)),body.extents);
            hit.SetPositionAndRotation(body.center-forward*(depth+.025f),Quaternion.LookRotation(forward));
            hit.localScale=Vector3.one;
            hit.sizeDelta=new Vector2(Mathf.Clamp(width+.06f,.55f,.95f),body.size.y+.06f)/Mathf.Abs(hit.lossyScale.y);
            point=hit.anchoredPosition;
        }
        if(desktop)hit.anchoredPosition=point;
        var label=selectionUI.transform.Find(name) as RectTransform;if(label)
        {
            var foot=actor.transform.position+Vector3.up*.02f;
            if(desktop)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvasObject.transform,playerCamera.WorldToScreenPoint(foot),null,out var labelPoint);
                label.anchoredPosition=labelPoint+Vector2.down*32;
            }
            else
            {
                var footRay=new Ray(playerCamera.transform.position,foot-playerCamera.transform.position);
                var footPlane=new Plane(canvasObject.transform.forward,canvasObject.transform.position);footPlane.Raycast(footRay,out float footDistance);
                label.anchoredPosition=(Vector2)canvasObject.transform.InverseTransformPoint(footRay.GetPoint(footDistance))+Vector2.down*32;
            }
        }
    }
    void BuildMenuScenery()
    {
        if(!menuTentPrefab){Debug.LogError("[KATHEEB] Missing menu tent prefab.",this);return;}
        menuScenery=Instantiate(menuTentPrefab,transform);menuScenery.name="KATHEEB Menu-only Bedouin Tent";
        var shell=System.Array.Find(menuScenery.GetComponentsInChildren<Transform>(true),t=>t!=menuScenery.transform&&t.name=="Tent");
        if(!shell)shell=menuScenery.transform;
        var layoutAsset=Resources.Load<TextAsset>("KatheebUI/MenuTentLayout");
        if(!layoutAsset){Debug.LogError("[KATHEEB] Bake the menu tent layout before building.",this);Destroy(menuScenery);return;}
        var layout=JsonUtility.FromJson<KatheebTentLayout>(layoutAsset.text);
        Vector3 forward=Vector3.ProjectOnPlane(playerCamera.transform.forward,Vector3.up).normalized;
        if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
        menuScenery.transform.localScale*=layout.scale;
        var doorway=menuScenery.transform.TransformPoint(layout.door);
        var outward=menuScenery.transform.TransformDirection(layout.outward).normalized;
        var turn=Quaternion.FromToRotation(outward,-forward);
        var root=menuScenery.transform;
        var doorLocal=root.InverseTransformPoint(doorway);
        root.rotation=turn*root.rotation;
        var desired=menuFloorCenter+forward*.20f;
        // This tent has two porch bays divided by a center support. The clear
        // walking opening is in the right bay; frame the whole entrance facade.
        root.position+=desired-root.TransformPoint(doorLocal)+Vector3.Cross(Vector3.up,forward)*1.2f;
        // Raise the architecture above the enlarged preview heads, keeping its
        // bottom anchored and leaving carpets, furniture and characters untouched.
        var floorPoint=layout.shellFloor;
        var anchoredFloor=shell.TransformPoint(floorPoint);
        shell.localScale=Vector3.Scale(shell.localScale,new Vector3(1,1.25f,1));
        shell.position+=anchoredFloor-shell.TransformPoint(floorPoint);
        // Both preview characters share the doorway floor and sit just outside it.
        var right=Vector3.Cross(Vector3.up,forward);
        PlaceActor(mufleh,desired-forward*.22f-right*.62f,forward);
        PlaceActor(saqeera,desired-forward*.22f+right*.62f,forward);
        SyncCharacterUI(mufleh,manHit,"Mufleh Name");SyncCharacterUI(saqeera,womanHit,"Saqeera Name");
        foreach(var collider in menuScenery.GetComponentsInChildren<Collider>(true))collider.enabled=false;
        foreach(var lod in menuScenery.GetComponentsInChildren<LODGroup>(true))if(lod.enabled)lod.ForceLOD(0);
        foreach(var light in menuScenery.GetComponentsInChildren<Light>(true))light.enabled=false;
        var fill=new GameObject("Warm menu character lighting",typeof(Light));fill.transform.SetParent(menuScenery.transform,true);
        fill.transform.position=playerCamera.transform.position+Vector3.up*.6f+forward*.5f;
        var lamp=fill.GetComponent<Light>();lamp.type=LightType.Directional;fill.transform.rotation=Quaternion.LookRotation(forward+Vector3.down*.5f);lamp.intensity=1.1f;lamp.color=new Color(1,.83f,.63f);lamp.shadows=LightShadows.None;
    }
    void FindTentDoorway(Transform shell,Bounds local,out Vector3 doorway,out Vector3 outward)
    {
        var center=shell.TransformPoint(local.center);
        var floor=shell.TransformPoint(new Vector3(local.center.x,local.min.y,local.center.z)).y;
        center.y=floor;
        float radius=Mathf.Max(local.size.x,local.size.z)*shell.lossyScale.x*1.5f;
        radius=Mathf.Max(radius,6);
        var probes=new List<MeshCollider>();
        foreach(var filter in shell.GetComponentsInChildren<MeshFilter>(true))
        {
            if(!filter.sharedMesh)continue;
            var visible=filter.GetComponent<MeshRenderer>();
            if(!visible||!visible.enabled||!filter.gameObject.activeInHierarchy)continue;
            var meshName=filter.sharedMesh.name.ToLowerInvariant();
            if(meshName.Contains("lod1")||meshName.Contains("lod2")||meshName.Contains("lod3"))continue;
            var probe=new GameObject("Temporary menu doorway probe");probe.layer=2;
            probe.transform.SetPositionAndRotation(filter.transform.position,filter.transform.rotation);
            probe.transform.localScale=filter.transform.lossyScale;
            var collider=probe.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;probes.Add(collider);
        }
        Physics.SyncTransforms();
        const int samples=120;
        var clear=new bool[samples];var edges=new Vector3[samples];
        for(int i=0;i<samples;i++)
        {
            float angle=i*Mathf.PI*2/samples;var direction=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
            bool blocked=false;float closest=radius;
            foreach(float height in new[]{.85f,1.25f})
            {
                var ray=new Ray(center+Vector3.up*height+direction*radius,-direction);
                foreach(var probe in probes)
                    if(probe.Raycast(ray,out var hit,radius-.35f)&&hit.distance<closest)
                    {closest=hit.distance;edges[i]=hit.point;blocked=true;}
            }
            clear[i]=!blocked;
        }
        // A narrow center support or guy rope must not split one wide entrance
        // into two doors. Join only short occlusions, retaining the cloth walls.
        var joined=(bool[])clear.Clone();
        for(int i=0;i<samples;i++)
        {
            if(clear[i]||!clear[(i+samples-1)%samples])continue;
            int length=0;while(length<samples&&!clear[(i+length)%samples])length++;
            if(length<=5)for(int j=0;j<length;j++)joined[(i+j)%samples]=true;
        }
        clear=joined;
        int bestStart=0,bestLength=0;
        for(int i=0;i<samples;i++)
        {
            if(!clear[i]||clear[(i+samples-1)%samples])continue;
            int length=0;while(length<samples&&clear[(i+length)%samples])length++;
            if(length>bestLength){bestStart=i;bestLength=length;}
        }
        if(bestLength>0)
        {
            var left=edges[(bestStart+samples-1)%samples];var right=edges[(bestStart+bestLength)%samples];
            doorway=(left+right)*.5f;doorway.y=floor;
            var across=Vector3.ProjectOnPlane(right-left,Vector3.up);
            outward=Vector3.Cross(Vector3.up,across).normalized;
            if(Vector3.Dot(outward,doorway-center)<0)outward=-outward;
            if(outward.sqrMagnitude<.1f)outward=shell.forward;
        }
        else
        {
            doorway=shell.TransformPoint(new Vector3(local.center.x,local.min.y,local.min.z));
            outward=Vector3.ProjectOnPlane(-shell.forward,Vector3.up).normalized;
            Debug.LogWarning("[KATHEEB] Doorway probe found no opening; using the authored front.",this);
        }
        foreach(var probe in probes){probe.enabled=false;if(Application.isPlaying)Destroy(probe.gameObject);else DestroyImmediate(probe.gameObject);}
    }
    void SetInput(bool value){foreach(var target in targets)if(target){target.interactable=value;var button=target.GetComponent<Button>();if(button)button.interactable=value;}content.blocksRaycasts=value;selectionUI.blocksRaycasts=value;}
    public void OpenSelection()
    {
        if(busy||State==MenuState.Playing||State==MenuState.Countdown||State==MenuState.CharacterSelection)return;
        stateRoutine=StartCoroutine(ShowSelection());
    }
    IEnumerator ShowSelection()
    {
        busy=true;State=MenuState.Transitioning;SetInput(false);
        stage.SetActive(true);mufleh.transform.localScale=saqeera.transform.localScale=Vector3.one*(MenuCharacterScale*.97f);selectionUI.gameObject.SetActive(true);selectionUI.alpha=0;
        Vector2 from=buttonGroup.anchoredPosition,to=desktop?new Vector2(620,-180):new Vector2(980,-100);
        Vector2 logoFrom=logo.anchoredPosition,logoTo=new Vector2(0,335);float logoScale=.65f;
        for(float t=0;t<.4f;t+=Time.unscaledDeltaTime)
        {
            float ease=Mathf.SmoothStep(0,1,t/.4f);buttonGroup.anchoredPosition=Vector2.Lerp(from,to,ease);
            logo.anchoredPosition=Vector2.Lerp(logoFrom,logoTo,ease);logo.localScale=Vector3.one*Mathf.Lerp(1,logoScale,ease);
            selectionUI.alpha=ease;buttonGroup.localScale=Vector3.one*Mathf.Lerp(1,.8f,ease);mufleh.transform.localScale=saqeera.transform.localScale=Vector3.one*(MenuCharacterScale*Mathf.Lerp(.97f,1,ease));backing.color=new Color(0,0,0,Mathf.Lerp(.18f,.05f,ease));yield return null;
        }
        logo.anchoredPosition=logoTo;logo.localScale=Vector3.one*logoScale;buttonGroup.anchoredPosition=to;buttonGroup.localScale=Vector3.one*.8f;selectionUI.alpha=1;mufleh.transform.localScale=saqeera.transform.localScale=Vector3.one*MenuCharacterScale;State=MenuState.CharacterSelection;busy=false;SetInput(true);
    }
    void SelectCharacter(KatheebCharacterInteractable actor)
    {
        if(busy||State!=MenuState.CharacterSelection)return;
        stateRoutine=StartCoroutine(ConfirmSelection(actor));
    }
    IEnumerator ConfirmSelection(KatheebCharacterInteractable actor)
    {
        busy=true;State=MenuState.Transitioning;SetInput(false);
        selectedKey=actor.selectionKey;DesertCharacterSelection.Save(selectedKey);
        yield return actor.Confirm();
        var from=buttonGroup.anchoredPosition;var logoFrom=logo.anchoredPosition;float logoScale=logo.localScale.x;
        for(float t=0;t<.4f;t+=Time.unscaledDeltaTime)
        {
            float ease=Mathf.SmoothStep(0,1,t/.4f);buttonGroup.anchoredPosition=Vector2.Lerp(from,new Vector2(0,-170),ease);selectionUI.alpha=1-ease;buttonGroup.localScale=Vector3.one*Mathf.Lerp(.8f,1,ease);
            logo.anchoredPosition=Vector2.Lerp(logoFrom,new Vector2(0,285),ease);logo.localScale=Vector3.one*Mathf.Lerp(logoScale,1,ease);
            backing.color=new Color(0,0,0,Mathf.Lerp(.05f,.18f,ease));yield return null;
        }
        stage.SetActive(false);selectionUI.gameObject.SetActive(false);hint.gameObject.SetActive(false);
        logo.anchoredPosition=new Vector2(0,285);logo.localScale=Vector3.one;buttonGroup.anchoredPosition=new Vector2(0,-170);buttonGroup.localScale=Vector3.one;State=MenuState.MainMenu;busy=false;SetInput(true);
    }
    public void RequestStart()
    {
        if(busy||State==MenuState.Countdown||State==MenuState.Playing)return;
        if(!HasSelection){hint.gameObject.SetActive(true);OpenSelection();return;}
        stateRoutine=StartCoroutine(StartExperience());
    }
    IEnumerator StartExperience()
    {
        busy=true;State=MenuState.Transitioning;SetInput(false);
        float from=backing.color.a;
        for(float t=0;t<.35f;t+=Time.unscaledDeltaTime){float a=Mathf.SmoothStep(0,1,t/.35f);content.alpha=1-a;selectionUI.alpha=1-a;backing.color=new Color(0,0,0,Mathf.Lerp(from,1,a));yield return null;}
        mainContent.SetActive(false);selectionUI.gameObject.SetActive(false);stage.SetActive(false);if(menuScenery)menuScenery.SetActive(false);
        State=MenuState.Countdown;RestorePointerSettings();RestoreRideVisuals();
        manRider.SetActive(selectedKey=="Man");womanRider.SetActive(selectedKey=="Woman");
        // The existing XR rig stays in its sled hierarchy. Reposition only this UI under black.
        if(!desktop)canvasObject.transform.SetPositionAndRotation(playerCamera.transform.position+playerCamera.transform.forward*2.4f,playerCamera.transform.rotation);
        yield return null;
        countdownObject.SetActive(true);countdown.alpha=0;
        for(float t=0;t<.3f;t+=Time.unscaledDeltaTime){backing.color=new Color(0,0,0,Mathf.Lerp(1,0,t/.3f));yield return null;}
        backing.color=Color.clear;
        State=MenuState.Countdown;
        foreach(string digit in new[]{"3","2","1"})
        {
            number.text=digit;PlayCountdownSound(false);
            for(float elapsed=0;elapsed<1;elapsed+=Time.unscaledDeltaTime)
            {
                float enter=Mathf.SmoothStep(0,1,elapsed/.16f),leave=1-Mathf.SmoothStep(0,1,(elapsed-.78f)/.22f);
                countdown.alpha=enter*leave;number.transform.localScale=Vector3.one*Mathf.Lerp(.94f,1.02f,enter);yield return null;
            }
        }
        countdownObject.SetActive(false);canvasObject.SetActive(false);State=MenuState.Playing;busy=false;
        PlayCountdownSound(true);
        gameplay.BeginOnce();
    }
    void PlayCountdownSound(bool start)
    {
        if(!countdownAudio)
        {
            countdownAudio=gameObject.AddComponent<AudioSource>();
            countdownAudio.playOnAwake=false;countdownAudio.loop=false;
            countdownAudio.spatialBlend=0;countdownAudio.volume=.65f;
            countdownAudio.ignoreListenerPause=true;
            countdownTick=Resources.Load<AudioClip>("KatheebUI/Audio/CountdownTick");
            countdownGo=Resources.Load<AudioClip>("KatheebUI/Audio/CountdownGo");
        }
        var clip=start?countdownGo:countdownTick;
        if(clip)countdownAudio.PlayOneShot(clip);
    }
    void OnEnable(){Application.onBeforeRender+=FollowCountdown;}
    void OnDisable(){Application.onBeforeRender-=FollowCountdown;}
    void LateUpdate()
    {
        FollowCountdown();
        if(!desktop&&IsReady&&stage&&stage.activeInHierarchy)
        {
            SyncCharacterUI(mufleh,manHit,"Mufleh Name");SyncCharacterUI(saqeera,womanHit,"Saqeera Name");
        }
        if(State==MenuState.MainMenu||State==MenuState.CharacterSelection||State==MenuState.Transitioning)HideRideVisuals();
    }
    void FollowCountdown()
    {
        if(State==MenuState.MainMenu||State==MenuState.CharacterSelection||State==MenuState.Transitioning)HideRideVisuals();
        if(!desktop&&canvasObject&&playerCamera&&State==MenuState.Countdown)
            canvasObject.transform.SetPositionAndRotation(playerCamera.transform.position+playerCamera.transform.forward*2.4f,playerCamera.transform.rotation);
    }
    void Update()
    {
        if(!desktop||!canvasObject||busy||State==MenuState.Playing)return;
        var keyboard=Keyboard.current;if(keyboard==null)return;
        if(keyboard.cKey.wasPressedThisFrame)OpenSelection();
        if(State==MenuState.CharacterSelection)
        {
            if(keyboard.digit1Key.wasPressedThisFrame)mufleh.Select();
            if(keyboard.digit2Key.wasPressedThisFrame)saqeera.Select();
        }
        if(keyboard.enterKey.wasPressedThisFrame)RequestStart();
    }
    void RestorePointerSettings()
    {
        foreach(var entry in rayUIBlocks)if(entry.Key)entry.Key.blockUIOnInteractableSelection=entry.Value;
        foreach(var entry in nearUIBlocks)if(entry.Key)entry.Key.blockUIOnInteractableSelection=entry.Value;
        rayUIBlocks.Clear();nearUIBlocks.Clear();
    }
    void OnDestroy(){RestorePointerSettings();RestoreRideVisuals();foreach(var material in uiMaterials)if(material)Destroy(material);if(stage)Destroy(stage);if(menuScenery)Destroy(menuScenery);if(menuPresentationAnchor)Destroy(menuPresentationAnchor.gameObject);}
}

[System.Serializable]
public class KatheebTentLayout
{
    public float scale;
    public Vector3 door,outward,shellFloor;
}
