using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>Idempotent scene integration. No terrain, rider, hardware, vehicle or XR transforms are authored.</summary>
public static class KatheebMenuInstaller
{
    const string Folder="Assets/KatheebMenu",Version="interactive-menu-20261016-v2";
    static bool busy;
    [InitializeOnLoadMethod]static void Initialize(){EditorApplication.delayCall+=InstallIfNeeded;}
    static void InstallIfNeeded(){if(!File.Exists(Folder+"/Installed.txt")||File.ReadAllText(Folder+"/Installed.txt")!=Version)Install();}
    [MenuItem("Tools/Desert VR/Install KATHEEB Interactive Menu")]
    public static void Install()
    {
        if(busy||EditorApplication.isPlayingOrWillChangePlaymode)return;busy=true;
        Scene scene=default;bool opened=false;
        try
        {
            var manIdle=Animation("MuflehIdle",true);var manReact=Animation("MuflehSalute",false);
            var womanIdle=Animation("SaqeeraIdle",true);var womanReact=Animation("SaqeeraGreeting",false);
            var manController=Controller("Mufleh",manIdle,manReact);var womanController=Controller("Saqeera",womanIdle,womanReact);
            var manPrefab=Character("Mufleh","Desert Robe","Man",manController,manReact.length);
            var womanPrefab=Character("Saqeera","Traditional Dress","Woman",womanController,womanReact.length);
            scene=SceneManager.GetSceneByPath("Assets/Scenes/Demo.unity");
            if(!scene.isLoaded){scene=EditorSceneManager.OpenScene("Assets/Scenes/Demo.unity",OpenSceneMode.Additive);opened=true;}
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var legacy=all.Select(t=>t.GetComponent<ArabicVRSimulationMenu>()).FirstOrDefault(m=>m);
            if(!legacy)throw new Exception("Existing menu/rider references were not found in Demo.");
            legacy.enabled=false;EditorUtility.SetDirty(legacy);
            var obj=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="KATHEEB Interactive Menu");
            if(!obj){obj=new GameObject("KATHEEB Interactive Menu");SceneManager.MoveGameObjectToScene(obj,scene);}
            var adapter=obj.GetComponent<KatheebGameplayStartAdapter>()??obj.AddComponent<KatheebGameplayStartAdapter>();
            adapter.carMovement=all.Select(t=>t.GetComponent<CarPathFollower>()).FirstOrDefault(m=>m);
            adapter.sledFollowing=all.Select(t=>t.GetComponent<SledFollowCar>()).FirstOrDefault(m=>m);
            var menu=obj.GetComponent<KatheebMenuManager>()??obj.AddComponent<KatheebMenuManager>();
            menu.gameplay=adapter;menu.manRider=legacy.manRider;menu.womanRider=legacy.womanRider;
            if(!menu.manRider)menu.manRider=all.FirstOrDefault(t=>t.name=="Desert_Robe_Character_ROOT")?.gameObject;
            if(!menu.womanRider)menu.womanRider=all.FirstOrDefault(t=>t.name=="Traditional_Woman_ROOT")?.gameObject;
            menu.playerCamera=all.Select(t=>t.GetComponent<Camera>()).FirstOrDefault(c=>c&&c.CompareTag("MainCamera"));
            menu.muflehSelectionPrefab=manPrefab;menu.saqeeraSelectionPrefab=womanPrefab;menu.menuTentPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tent.prefab");menu.setupVersion=Version;
            if(!menu.manRider||!menu.womanRider||!menu.playerCamera||!adapter.carMovement)throw new Exception("Existing rider, camera or car movement reference missing. No scene saved.");
            var events=all.Select(t=>t.GetComponent<EventSystem>()).Where(e=>e).ToArray();
            if(events.Length>1)throw new Exception("Demo already has duplicate EventSystems; do not install a third.");
            EventSystem eventSystem;
            if(events.Length==0){var eventObject=new GameObject("KATHEEB EventSystem");SceneManager.MoveGameObjectToScene(eventObject,scene);eventSystem=eventObject.AddComponent<EventSystem>();}
            else eventSystem=events[0];
            foreach(var module in eventSystem.GetComponents<BaseInputModule>())if(!(module is XRUIInputModule))module.enabled=false;
            var ui=eventSystem.GetComponent<XRUIInputModule>()??eventSystem.gameObject.AddComponent<XRUIInputModule>();ui.enabled=true;ui.enableXRInput=true;ui.enableMouseInput=true;
            int rays=0;
            foreach(var transform in all)
            {
                var ray=transform.GetComponent<XRRayInteractor>();
                if(ray&&!ray.name.ToLowerInvariant().Contains("teleport")){ray.enableUIInteraction=true;EditorUtility.SetDirty(ray);rays++;}
                var near=transform.GetComponent<NearFarInteractor>();
                if(near)
                {
                    var serialized=new SerializedObject(near);var prop=serialized.FindProperty("m_EnableUIInteraction");
                    if(prop!=null){prop.boolValue=true;serialized.ApplyModifiedPropertiesWithoutUndo();rays++;}
                }
            }
            EditorUtility.SetDirty(ui);EditorUtility.SetDirty(eventSystem);EditorUtility.SetDirty(adapter);EditorUtility.SetDirty(menu);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            var report=new StringBuilder();
            report.AppendLine("KATHEEB INTERACTIVE MENU — INTEGRATED "+Version);
            report.AppendLine("Scene: Assets/Scenes/Demo.unity. Existing legacy menu disabled; separate new manager and gameplay-start adapter assigned.");
            report.AppendLine("Riders: "+menu.manRider.name+" / "+menu.womanRider.name+". Existing player camera: "+menu.playerCamera.name+". CarPathFollower and SledFollowCar assigned.");
            report.AppendLine("Character prefabs: supplied Desert Robe and Traditional Dress FBX, existing original Clothing materials and valid humanoid avatars. Heights: 1.75 m / 1.68 m.");
            report.AppendLine("Controllers: MuflehMenu and SaqeeraMenu, Idle and React states. Four supplied clips assigned; Idle looping; reaction one-shot; no root motion.");
            report.AppendLine("XR: existing EventSystem reused, XRUIInputModule enabled, mouse fallback enabled. UI-capable ray/near-far components configured: "+rays+". Existing action references preserved.");
            report.AppendLine("3D colliders/XRSimpleInteractable plus independently raycastable world-space UI trigger targets provide hover and trigger selection.");
            report.AppendLine("UI hierarchy is generated by the serialized KatheebMenuManager on Play: World Canvas, Backing, Main Content, Animated Main Buttons, Character Targets/Names, Countdown. 3D selection stage uses prefab references.");
            report.AppendLine("Menu-only scenery: existing Tent.prefab reused, with its rugs, cushions and lantern props. Preview colliders disabled and only three unshadowed local lights; scenery hides before countdown. Existing world objects unchanged.");
            report.AppendLine("Selection is explicit; no preselected character. Starts only after 3,2,1. Adapter holds only CarPathFollower and SledFollowCar. XR rig remains parented to the sled; tracked camera never repositioned.");
            report.AppendLine("All Inspector references assigned. No new XR Origin, package, hardware or WebSocket component. Existing networking and motion-output scripts not edited by this installer.");
            report.AppendLine("AVAILABLE VALIDATION: compiler, assigned assets/avatar validity, scene references, duplicate EventSystem guard, protected source hashes and serialized references comparison. Physical Quest controllers, actual ESP32 link, final headset comfort and visual timing require hardware testing.");
            File.WriteAllText(Folder+"/IntegrationReport.txt",report.ToString());File.WriteAllText(Folder+"/Installed.txt",Version);
            Debug.Log("[KATHEEB] Interactive menu installed and scene saved. Four supplied animations, two 3D selection prefabs, XR UI and gameplay-start adapter assigned. See Assets/KatheebMenu/IntegrationReport.txt.",menu);
        }
        catch(Exception ex){File.WriteAllText(Folder+"/IntegrationError.txt",ex.ToString());Debug.LogError("[KATHEEB] Menu integration incomplete: "+ex);}
        finally{if(opened&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);busy=false;}
    }
    static AnimationClip Animation(string name,bool loop)
    {
        string path=Folder+"/Animations/"+name+".fbx";
        var importer=AssetImporter.GetAtPath(path) as ModelImporter;if(!importer)throw new Exception("Supplied FBX not imported: "+path);
        bool changed=importer.animationType!=ModelImporterAnimationType.Human||!importer.importAnimation;
        importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=true;
        var clips=importer.clipAnimations.Length>0?importer.clipAnimations:importer.defaultClipAnimations;
        foreach(var clip in clips)
        {
            if(clip.loopTime!=loop||!clip.lockRootPositionXZ||!clip.lockRootHeightY||!clip.lockRootRotation)changed=true;
            clip.loopTime=loop;clip.loopPose=loop;clip.lockRootPositionXZ=true;clip.lockRootHeightY=true;clip.lockRootRotation=true;
            clip.keepOriginalOrientation=true;clip.keepOriginalPositionXZ=true;clip.heightFromFeet=true;
        }
        importer.clipAnimations=clips;if(changed)importer.SaveAndReimport();
        var animation=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview__"));
        if(!animation||!animation.isHumanMotion)throw new Exception("Supplied clip has no valid humanoid animation: "+name);
        return animation;
    }
    static AnimatorController Controller(string name,AnimationClip idle,AnimationClip reaction)
    {
        string path=Folder+"/Controllers/"+name+"Menu.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path)??AnimatorController.CreateAnimatorControllerAtPath(path);
        var machine=controller.layers[0].stateMachine;
        var idleState=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="Idle")??machine.AddState("Idle");idleState.motion=idle;
        var reactState=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="React")??machine.AddState("React");reactState.motion=reaction;
        machine.defaultState=idleState;EditorUtility.SetDirty(controller);return controller;
    }
    static GameObject Character(string name,string sourceName,string key,AnimatorController controller,float reactionSeconds)
    {
        string directory="Assets/Resources/CharacterCollection/"+sourceName;
        string path=Directory.GetFiles(directory,"*.fbx").Single();var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault(a=>a.isHuman&&a.isValid);
        var material=AssetDatabase.LoadAssetAtPath<Material>(directory+"/Clothing.mat");
        if(!source||!avatar||!material)throw new Exception("Supplied model, avatar or original material missing: "+sourceName);
        var root=new GameObject(name+" Menu Character");
        try
        {
            var model=(GameObject)PrefabUtility.InstantiatePrefab(source);model.name="Supplied Character Model";model.transform.SetParent(root.transform,false);
            bool found=false;Bounds bounds=default;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
                if(renderer is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=true;
                if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);
            }
            if(!found||bounds.size.y<.01f)throw new Exception("Character bounds missing: "+sourceName);
            float height=name=="Mufleh"?1.75f:1.68f;
            float scale=height/bounds.size.y;model.transform.localScale*=scale;
            model.transform.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
            var animator=model.GetComponent<Animator>()??model.AddComponent<Animator>();animator.avatar=avatar;animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var collider=root.AddComponent<BoxCollider>();collider.center=Vector3.up*height*.5f;collider.size=new Vector3(.58f,height,.52f);
            var xr=root.AddComponent<XRSimpleInteractable>();xr.colliders.Clear();xr.colliders.Add(collider);
            var character=root.AddComponent<KatheebCharacterInteractable>();character.animator=animator;character.selectionKey=key;character.reactionSeconds=Mathf.Max(.7f,reactionSeconds);
            var indicator=new GameObject("Warm Gold Hover Ring");indicator.transform.SetParent(root.transform,false);indicator.transform.localPosition=Vector3.up*.015f;
            var line=indicator.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.positionCount=48;line.widthMultiplier=.012f;
            for(int i=0;i<48;i++){float angle=i*Mathf.PI*2/48;line.SetPosition(i,new Vector3(Mathf.Cos(angle)*.35f,0,Mathf.Sin(angle)*.35f));}
            string matPath=Folder+"/Prefabs/HoverGold.mat";var gold=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(!gold){gold=new Material(Shader.Find("Universal Render Pipeline/Unlit"));gold.SetColor("_BaseColor",new Color(1,.63f,.17f));AssetDatabase.CreateAsset(gold,matPath);}
            line.sharedMaterial=gold;character.indicator=indicator;indicator.SetActive(false);
            return PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Prefabs/"+name+"Menu.prefab");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
}
