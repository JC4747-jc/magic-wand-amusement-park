using System;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using InputKey=UnityEngine.InputSystem.Key;
#endif

[InitializeOnLoad]
public static class HciStudyConditionChecks
{
    const string Key="MagicMR.HciStudyConditionCheck";
    static string Output=>Path.GetFullPath(".codex-tmp/hci-study-conditions");
    static readonly string[] Profiles={"tree","bugs","statue","room","plant","pet"};
    static readonly ParkAction[] Actions={ParkAction.Style,ParkAction.Awaken,ParkAction.Wind,ParkAction.Reward};
    static readonly EnabledDimensions[] Conditions={EnabledDimensions.None,EnabledDimensions.Appearance,EnabledDimensions.Appearance|EnabledDimensions.Agency,EnabledDimensions.All};
    static ParkController controller;
    static ExperimentConditionSwitcher study;
    static int phase,profile,errors,attempts;
    static double next,started;
    static HciStudyConditionChecks(){EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};}
    static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
    static void CheckKeyboardOwnership(){
#if ENABLE_INPUT_SYSTEM
        var previous=Keyboard.current;var previousSettings=InputSystem.settings;
        var settings=UnityEngine.Object.Instantiate(previousSettings);
        settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;InputSystem.settings=settings;
        var keyboard=InputSystem.AddDevice<Keyboard>("Synthetic condition shortcut check");
        try{
            keyboard.MakeCurrent();study.SetCondition(4);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(InputKey.LeftCtrl,InputKey.Digit1));InputSystem.Update();
            Require(keyboard.leftCtrlKey.isPressed&&keyboard.digit1Key.wasPressedThisFrame,"Synthetic shortcut input did not reach manual test update");
            Require(!ParkDesktopInput.KeyDown(KeyCode.Alpha1),"Ctrl preset also became Park magic");
            study.SendMessage("Update");Require(study.CurrentConditionId==1,"Ctrl+1 did not set Baseline");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(InputKey.Digit4));InputSystem.Update();
            Require(ParkDesktopInput.KeyDown(KeyCode.Alpha4),"Plain 4 lost virtual action mapping");
            study.SendMessage("Update");Require(study.CurrentConditionId==1,"Plain action key changed condition");
        }finally{InputSystem.RemoveDevice(keyboard);if(previous!=null&&previous.added)previous.MakeCurrent();InputSystem.settings=previousSettings;UnityEngine.Object.DestroyImmediate(settings);study.SetCondition(4);}
#endif
    }
    public static void Run(){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);EditorSceneManager.OpenScene("Assets/Scenes/ParkPlant.unity");EditorApplication.EnterPlaymode();}
    static void Tick(){
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying){if(SessionState.GetBool(Key+".Done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}return;}
        try{
            if(phase==0){
                started=EditorApplication.timeSinceStartup;controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();study=UnityEngine.Object.FindFirstObjectByType<ExperimentConditionSwitcher>();
                Require(controller&&study,"Missing saved scene controller/study switcher");CheckKeyboardOwnership();controller.simulation=true;controller.SpawnVirtual();
                Require(controller.StartStudySession("SYNTHETIC_CONDITION_CHECK",7),"Explicit synthetic session did not start");
                Require(!controller.StartStudySession("SECOND",8),"Active session replaced");phase=1;next=started+1;return;
            }
            Require(errors==0,"Runtime error");Require(EditorApplication.timeSinceStartup-started<60,"Timeout");if(EditorApplication.timeSinceStartup<next)return;
            if(phase==1){
                controller.ResetScene();Require(controller.SwitchContent(Profiles[profile],null),"Profile switch failed");controller.SpawnVirtual();phase=2;next=EditorApplication.timeSinceStartup+1;return;
            }
            if(phase==2){
                var actor=controller.selected;Require(actor,"No target");
                for(int c=0;c<4;c++){
                    study.SetCondition(c+1);Require(controller.StudyDimensions==Conditions[c],"Wrong condition mapping");
                    for(int a=0;a<4;a++){
                        actor.ResetEffects();int revision=actor.Revision;
                        bool accepted=controller.Dispatch(Actions[a],actor.transform.position,true);attempts++;
                        bool allowed=(Conditions[c]&(EnabledDimensions)(1<<a))!=0;
                        if(!allowed){Require(!accepted&&actor.Revision==revision,"Disabled action changed state");Require(controller.InteractionStatus.StartsWith("Disabled by study condition"),"Wrong rejection gate");}
                        else{Require(!controller.InteractionStatus.StartsWith("Disabled by study condition"),"Allowed action blocked by condition");if(a<2)Require(accepted,"Allowed A/B rejected");}
                    }
                    actor.ResetEffects();if(c<3)Require(!controller.Dispatch(ParkAction.Bloom,actor.transform.position,true),"Extension bypassed condition");
                    Require(controller.Dispatch(ParkAction.Reset,actor.transform.position,true),"Reset unavailable");
                }
                // Condition changes must clear accumulated effects and gesture ownership.
                study.SetCondition(1);Require(!actor.Busy,"Condition switch left animation running");
                profile++;phase=profile<Profiles.Length?1:3;return;
            }
            if(phase==3){
                // Shared recognizer still detects A in Baseline, but may not apply it.
                float t=Time.unscaledTime;var p=controller.selected.transform.position;
                TabletopHandSample Frame(float time,float pinch)=>TabletopHandSample.Create(time,p,p,p+Vector3.right*pinch,p-Vector3.up*.03f,Vector4.one*.95f,Vector3.right,pinch);
                controller.ProcessMagicSample(Frame(t,.08f),true);
                controller.ProcessMagicSample(Frame(t+.02f,.02f),true);
                controller.ProcessMagicSample(Frame(t+.10f,.02f),true);
                Require(controller.ProcessMagicSample(Frame(t+.12f,.08f),true)==EditDimension.Appearance&&!controller.LastMagicAccepted,"Baseline shared gesture gate failed");
                var logger=DataLogger.Instance;logger.EndSession();var text=File.ReadAllText(logger.EventFilePath);
                Require(text.Contains("SIM_SYNTHETIC_CONDITION_CHECK")&&text.Contains(",7,"),"Session/trial identity missing");
                Require(text.Contains("dimension_disabled")&&text.Contains("park_action_accepted")&&text.Contains("input=shared_hand_recognizer"),"Action outcomes/source missing");
                int logged=text.Split('\n').Count(l=>l.Contains("park_action_accepted")||l.Contains("park_action_rejected"));
                Require(logged==139,"Duplicate/missing action log: "+logged); // 96 matrix +18 extension+24 reset+1 gesture
                Require(new FileInfo(logger.EventFilePath).Length<128*1024,"Unexpected log growth");
                File.Copy(logger.EventFilePath,Output+"/synthetic-events.csv",true);
                var trajectory=logger.EventFilePath.Replace("_events.csv","_trajectory.csv");if(File.Exists(trajectory))File.Copy(trajectory,Output+"/synthetic-trajectory.csv",true);
                File.WriteAllText(Output+"/result.txt","PASS: six saved-content profiles x four conditions x four action dimensions (96 attempts), 139 exactly-once outcome records; disabled state unchanged; reset allowed; extensions gated; Baseline recognizes A but rejects magic. Explicit synthetic session/trial CSV, no real participant/YOLO/PICO acceptance.\n");
                SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();
            }
        }catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL: "+e);SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(2);}
    }
}

