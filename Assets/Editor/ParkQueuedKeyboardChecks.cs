using System;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Public Input System queue -> real controller Update -> model state, not direct action calls.
// Batch-only in-memory GameView routing; native/physical keyboard remains a separate check.
[InitializeOnLoad]
public static class ParkQueuedKeyboardChecks
{
    const string Flag="MagicMR.QueuedKeyboardChecks";
    static Keyboard keyboard,previousKeyboard;static InputSettings previousSettings,scopedSettings;
    static ParkController controller;static ParkSharedInterface ui;static ParkActor actor;static Camera camera;
    static Key? pending;static bool received2,receivedT,mapped2,mappedT;static int stage,errors,revision,presses,holdUpdates;
    static double started,next;static Vector3 root,cameraPosition;static Quaternion rotation,cameraRotation;
    static ParkQueuedKeyboardChecks(){EditorApplication.update+=Tick;InputSystem.onBeforeUpdate+=BeforeInput;InputSystem.onAfterUpdate+=AfterInput;Application.logMessageReceived+=Log;}
    static void Require(bool ok,string text){if(!ok)throw new Exception(text);}
    static void Log(string message,string trace,LogType type){if(SessionState.GetBool(Flag,false)&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))errors++;}
    public static void Run()
    {
        SessionState.SetString(Flag+".Scene",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);SessionState.SetInt(Flag+".Exit",-1);SessionState.SetBool(Flag,true);
        Directory.CreateDirectory(".codex-tmp/desktop-input");File.WriteAllText(".codex-tmp/desktop-input/queued-keyboard-result.txt","RUNNING\n");
        EditorSceneManager.OpenScene("Assets/Scenes/ParkRoom.unity");EditorApplication.EnterPlaymode();
    }
    static void BeforeInput()
    {
        if(!SessionState.GetBool(Flag,false)||!Application.isPlaying||keyboard==null||InputState.currentUpdateType!=InputUpdateType.Dynamic)return;
        if(pending.HasValue){InputSystem.QueueStateEvent(keyboard,new KeyboardState(pending.Value));pending=null;holdUpdates=4;presses++;}
        else if(holdUpdates>0&&--holdUpdates==0)InputSystem.QueueStateEvent(keyboard,new KeyboardState());
    }
    static void AfterInput()
    {
        if(keyboard==null||InputState.currentUpdateType!=InputUpdateType.Dynamic)return;
        if(keyboard.digit2Key.wasPressedThisFrame){received2=true;mapped2|=ParkDesktopInput.KeyDown(KeyCode.Alpha2);}
        if(keyboard.tKey.wasPressedThisFrame){receivedT=true;mappedT|=ParkDesktopInput.KeyDown(KeyCode.T);}
    }
    static void Later(int value,double delay){stage=value;next=EditorApplication.timeSinceStartup+delay;}
    static void Press(Key value){Require(!pending.HasValue&&holdUpdates==0,"Previous key not released");pending=value;}
    static void FixedRoot(){Require(actor.transform.position==root&&actor.transform.rotation==rotation,"Queued key moved tracking root");}
    static void Restore()
    {
        pending=null;holdUpdates=0;if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);keyboard=null;
        if(previousKeyboard!=null&&previousKeyboard.added)previousKeyboard.MakeCurrent();
        if(previousSettings)InputSystem.settings=previousSettings;if(scopedSettings)UnityEngine.Object.DestroyImmediate(scopedSettings);scopedSettings=null;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false))return;
        if(!EditorApplication.isPlaying)
        {
            int code=SessionState.GetInt(Flag+".Exit",-1);if(code<0)return;SessionState.SetBool(Flag,false);
            string scene=SessionState.GetString(Flag+".Scene","");if(!string.IsNullOrEmpty(scene))EditorSceneManager.OpenScene(scene);EditorApplication.Exit(code);return;
        }
        try
        {
            if(stage==0)
            {
                started=EditorApplication.timeSinceStartup;controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();ui=UnityEngine.Object.FindFirstObjectByType<ParkSharedInterface>();camera=Camera.main;
                Require(controller&&controller.isActiveAndEnabled&&ui&&camera,"Saved scene controller/camera unavailable");cameraPosition=camera.transform.position;cameraRotation=camera.transform.rotation;
                previousKeyboard=Keyboard.current;previousSettings=InputSystem.settings;scopedSettings=UnityEngine.Object.Instantiate(previousSettings);scopedSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;scopedSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=scopedSettings;
                keyboard=InputSystem.AddDevice<Keyboard>("Explicit synthetic preview keyboard");keyboard.MakeCurrent();Application.targetFrameRate=60;Later(1,1.2);return;
            }
            Require(EditorApplication.timeSinceStartup-started<40,"Queued keyboard check timed out at "+stage);
            Require(camera.transform.position==cameraPosition&&camera.transform.rotation==cameraRotation,"Keyboard fixture changed scene camera");
            if(EditorApplication.timeSinceStartup<next)return;
            switch(stage)
            {
                case 1:ui.Command("Virtual test");actor=controller.selected;Require(actor&&controller.simulation,"Virtual Room did not spawn");root=actor.transform.position;rotation=actor.transform.rotation;Later(2,.9);break;
                case 2:revision=actor.Revision;Press(Key.Digit2);Later(3,1.5);break;
                case 3:
                    Require(received2&&mapped2&&actor.Revision==revision+1&&actor.GetComponentInChildren<NaturalJellyfish>(true).Visibility>.99f,"Digit2 queue/mapping/controller failure: presses="+presses+" pending="+pending+" received="+received2+" mapped="+mapped2+" revision="+actor.Revision+" expected="+(revision+1)+" jelly="+actor.GetComponentInChildren<NaturalJellyfish>(true).Visibility+" controllerEnabled="+controller.isActiveAndEnabled+" keyboardEnabled="+keyboard.enabled+" keyboardCurrent="+ReferenceEquals(Keyboard.current,keyboard)+" inputUpdate="+InputState.currentUpdateType+" feedback="+actor.Feedback);FixedRoot();Press(Key.Z);Later(4,.8);break;
                case 4:
                    Require(actor.GetComponentInChildren<NaturalJellyfish>(true).Visibility<.01f,"Queued Z did not undo Room awakening");FixedRoot();Press(Key.R);Later(5,.6);break;
                case 5:
                    Require(!actor.Busy&&!actor.sizeConfirmed,"Queued R did not restore idle state");ui.Command("Tree");actor=controller.selected;Require(actor&&actor.scenario==ParkScenario.Tree&&!actor.magicTree,"Tree route failed");root=actor.transform.position;rotation=actor.transform.rotation;Later(6,.9);break;
                case 6:Press(Key.T);Later(7,.35);break;
                case 7:Require(receivedT&&mappedT&&actor.magicTree,"Queued T did not switch virtual tree mode");FixedRoot();Press(Key.Digit2);Later(8,1.5);break;
                case 8:
                    Require(actor.visual.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Virtual scar").gameObject.activeSelf,"Queued Digit2 did not awaken fantasy tree");FixedRoot();Press(Key.Z);Later(9,.6);break;
                case 9:
                    Require(!actor.visual.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Virtual scar").gameObject.activeSelf,"Queued Z did not restore tree state");FixedRoot();ui.Command("Reset");Later(10,.8);break;
                case 10:
                    Require(!actor&&controller.InstanceCount==0&&errors==0,"Queued keyboard fixture leaked actor or errors="+errors);Restore();
                    string report="PASS: "+presses+" queued key presses held for four input updates; Room Digit2 reveal/Z undo/R reset and Tree T mode/Digit2 awaken/Z undo, actual controller Update and fixed root/camera. Scoped in-memory batch IgnoreFocus + GameView routing restored after test; no OS/native/physical keyboard or headset acceptance.\n";
                    File.WriteAllText(".codex-tmp/desktop-input/queued-keyboard-result.txt",report);Debug.Log("PARK_QUEUED_KEYBOARD_PASS: "+report);SessionState.SetInt(Flag+".Exit",0);EditorApplication.ExitPlaymode();break;
            }
        }
        catch(Exception error){Restore();Debug.LogException(error);File.WriteAllText(".codex-tmp/desktop-input/queued-keyboard-result.txt","FAIL: "+error);SessionState.SetInt(Flag+".Exit",1);EditorApplication.ExitPlaymode();}
    }
}
