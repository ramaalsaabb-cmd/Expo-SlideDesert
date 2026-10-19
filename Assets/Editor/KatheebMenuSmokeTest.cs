using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Editor-only end-to-end menu check; never claims physical controller/network verification.</summary>
[InitializeOnLoad]
public static class KatheebMenuSmokeTest
{
    const string Key="Katheeb.MenuSmokeTest",Report="Assets/KatheebMenu/EditorSmokeTest.txt";
    static int step;
    static double deadline;
    static Vector3 start;
    static KatheebMenuManager menu;
    static KatheebMenuSmokeTest()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){step=0;deadline=EditorApplication.timeSinceStartup+90;EditorApplication.update+=Tick;}
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;}
        };
    }
    [MenuItem("Tools/Desert VR/Test KATHEEB Menu Flow")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("Stop Play mode before running the menu flow check.");return;}
        if(SceneManager.sceneCount!=1||SceneManager.GetActiveScene().path!="Assets/Scenes/Demo.unity")
        {Debug.LogWarning("Open only Demo.unity, then run Test KATHEEB Menu Flow. Existing scenes are not closed automatically.");return;}
        SessionState.SetString(Key+".PreviousCharacter",PlayerPrefs.GetString("Desert.SelectedCharacter","Man"));
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void HardwareEnabled()
    {
        foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            if(behaviour&&(behaviour.GetType().Name=="HardwareOutput"||behaviour.GetType().Name=="WebSocketSender"||behaviour.GetType().Name=="SledMotionController"))
                Check(behaviour.isActiveAndEnabled,"Protected component was disabled: "+behaviour.GetType().Name);
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Menu flow timed out at step "+step);
            if(!menu)menu=UnityEngine.Object.FindAnyObjectByType<KatheebMenuManager>();
            if(!menu||!menu.IsReady)return;
            HardwareEnabled();
            if(step==0)
            {
                Check(menu.HasSelection&&DesertCharacterSelection.MuflehSelected,"Default Mufleh selection is missing.");
                Check(UnityEngine.Object.FindObjectsByType<EventSystem>().Length==1,"Duplicate EventSystems.");
                Check(!menu.gameplay.carMovement.enabled,"Car moved before countdown.");
                start=menu.gameplay.carMovement.transform.position;
                menu.OpenSelection();step=1;
            }
            else if(step==1&&menu.State==KatheebMenuManager.MenuState.CharacterSelection)
            {
                Check(!menu.gameplay.HasBegun,"Opening optional character selection began gameplay.");
                var actors=UnityEngine.Object.FindObjectsByType<KatheebCharacterInteractable>();Check(actors.Length==2,"Both 3D menu characters were not shown.");
                foreach(var candidate in actors)Check(candidate.animator&&candidate.animator.avatar&&candidate.animator.isHuman,"Invalid humanoid menu character.");
                var actor=actors.First(a=>a.selectionKey=="Woman");
                var hit=GameObject.Find("Saqeera XR Trigger Target");Check(hit&&hit.GetComponent<Button>(),"UI trigger target missing.");
                ExecuteEvents.Execute(hit,new PointerEventData(EventSystem.current){pointerId=-99},ExecuteEvents.pointerEnterHandler);
                hit.GetComponent<Button>().onClick.Invoke();step=3;
            }
            else if(step==3&&menu.State==KatheebMenuManager.MenuState.Countdown)
            {
                Check(menu.HasSelection&&!DesertCharacterSelection.MuflehSelected,"Selected Saqeera was not saved before countdown.");
                Check(!menu.gameplay.HasBegun&&!menu.gameplay.carMovement.enabled,"Car started before countdown ended.");
                var moved=menu.gameplay.carMovement.transform.position-start;moved.y=0;
                Check(moved.magnitude<.01f,"Car translated during character selection.");step=4;
            }
            else if(step==4&&menu.State==KatheebMenuManager.MenuState.Playing)
            {
                Check(menu.gameplay.HasBegun&&menu.gameplay.carMovement.enabled,"Countdown did not start existing movement.");
                Check(menu.womanRider.activeSelf&&!menu.manRider.activeSelf,"Incorrect experience rider enabled.");
                Finish("PASS: default Mufleh and optional selection, both animated 3D characters, UI hover/click dispatch, saved Saqeera selection, immediate countdown after selection, movement held, single countdown, selected rider and existing gameplay resumed. Protected hardware components stayed enabled. Physical Quest controller input and ESP32 connectivity were not tested.");
            }
        }
        catch(Exception ex){Finish("FAIL: "+ex);}
    }
    static void Finish(string result)
    {
        PlayerPrefs.SetString("Desert.SelectedCharacter",SessionState.GetString(Key+".PreviousCharacter","Man"));PlayerPrefs.Save();
        File.WriteAllText(Report,result);Debug.Log("[KATHEEB Smoke Test] "+result);EditorApplication.update-=Tick;
        EditorApplication.ExitPlaymode();
    }
}
