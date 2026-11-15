using System;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Actual saved scenes and their generated Button callbacks. Synthetic input is
// explicit: virtual mode, controller action API and manual fit/plane points.
// No components are suppressed and the scene camera is never repositioned.
[InitializeOnLoad]
public static class ParkSceneFlowChecks
{
    const string Key="MagicMR.ParkSceneFlow";
    static readonly string Output=Path.GetFullPath(".codex-tmp/park-scene-flow");
    static readonly string[] Profiles={"Tree","Bugs","Statue","Room","Plant","Pet"};
    static ParkController controller;
    static ParkSharedInterface ui;
    static ParkActor original,switched,replayed;
    static Camera camera;
    static int stage,errors,snapshots;
    static double started,next;
    static Vector3 cameraPosition;
    static Quaternion cameraRotation;
    static float cameraFov;
    static bool previousAutomatic;
    [Serializable] sealed class Result
    {
        public string scene,scope="Actual Windows Editor scene; original camera/components; actual Button callbacks and synthetic controller actions/fit/plane. No physical pointer, hands, recognition accuracy, native camera or headset acceptance.";
        public bool passed;
        public string runId;
        public int errorLogs,snapshots;
        public string[] checks={"Virtual button entry", "UI Fit and Plane with synthetic points", "Controller animation and busy switch rejection", "Stop/Reset and exit size/bottom continuity", "Virtual content switch", "Mid-action Reset", "Replay candidate button", "Replay to virtual to live boundary", "Actor/audio/surface cleanup"};
    }
    static ParkSceneFlowChecks(){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Scene flow requires idle editor");
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"result.txt"),"RUNNING: scene flow has not yet passed.\n");
        SessionState.SetString(Key+".Run",Guid.NewGuid().ToString("N"));SessionState.SetString(Key+".Previous",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        SessionState.SetInt(Key+".Index",0);SessionState.SetString(Key+".Phase","open");SessionState.SetBool(Key,true);
    }
    static void Require(bool valid,string message){if(!valid)throw new Exception(message);}
    static void OnLog(string message,string stack,LogType type)
    {
        if(SessionState.GetBool(Key,false)&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))errors++;
    }
    static Button FindButton(string name)
    {
        var found=camera.GetComponentsInChildren<Button>(false).Where(b=>b.name==name).ToArray();
        Require(found.Length==1,"Expected one active button "+name+", got "+found.Length);return found[0];
    }
    static void Click(Button button)
    {
        Require(button&&button.interactable,"Inactive callback target");
        var rect=button.GetComponent<RectTransform>();var center=rect.TransformPoint(rect.rect.center);
        Require(ParkSharedInterface.TryButtonRayHit(rect,new Ray(camera.transform.position,center-camera.transform.position),out _),"Button center ray cannot hit "+button.name);
        button.onClick.Invoke();
    }
    static void Click(string name)=>Click(FindButton(name));
    static void Later(int value,double seconds){stage=value;next=EditorApplication.timeSinceStartup+seconds;}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        try
        {
            int index=SessionState.GetInt(Key+".Index",0);
            if(!EditorApplication.isPlaying)
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                if(SessionState.GetString(Key+".Phase","")=="next")
                {
                    index++;SessionState.SetInt(Key+".Index",index);SessionState.SetString(Key+".Phase","open");
                    if(index==6){Finish();return;}
                }
                if(SessionState.GetString(Key+".Phase","")!="open")return;
                stage=errors=snapshots=0;controller=null;ui=null;original=switched=replayed=null;
                EditorSceneManager.OpenScene("Assets/Scenes/"+ParkContentBuilder.Names[index]+".unity");
                SessionState.SetString(Key+".Phase","playing");EditorApplication.EnterPlaymode();return;
            }
            if(SessionState.GetString(Key+".Phase","")!="playing")return;
            if(stage==0)
            {
                started=EditorApplication.timeSinceStartup;camera=Camera.main;Require(camera,"Actual scene camera missing");
                controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();ui=UnityEngine.Object.FindFirstObjectByType<ParkSharedInterface>();
                Require(controller&&ui&&ui.controller==controller&&ui.receiver&&controller.receiver==ui.receiver,"Actual scene wiring missing");
                Require((int)controller.scenario==index&&!controller.simulation&&!ui.replayMode&&controller.InstanceCount==0,"Scene started in unexpected mode");
                cameraPosition=camera.transform.position;cameraRotation=camera.transform.rotation;cameraFov=camera.fieldOfView;
                previousAutomatic=ui.Selection.automatic;Later(1,1.5);return;
            }
            Require(EditorApplication.timeSinceStartup-started<50,"Actual scene flow timed out at stage "+stage);
            Require(camera.transform.position==cameraPosition&&camera.transform.rotation==cameraRotation&&camera.fieldOfView==cameraFov,"Flow changed scene camera");
            if(EditorApplication.timeSinceStartup<next)return;
            if(stage==1)
            {
                Click("Virtual test");original=controller.selected;
                Require(original&&original.virtualTarget&&controller.simulation&&controller.InstanceCount==1&&original.scenario==(ParkScenario)index,"Virtual button did not spawn scene model");
                Later(2,1.2);
            }
            else if(stage==2)
            {
                Require(original.EntryProgress>.99f&&!original.Busy,"Entry did not finish");Capture("Virtual");
                Click("Plane");Require(controller.PlaneConfirmationPending,"Plane button not connected");
                Require(controller.ConfirmManualPlane(original.transform.position+Vector3.up*.6f,true)&&original.confirmedPlane&&original.environmentAnchor,"Synthetic manual plane failed");
                Click("Fit");Require(controller.FitConfirmationPending&&!original.confirmedPlane&&!original.environmentAnchor,"Fit retained previous plane");
                Vector3 bottom=original.transform.position;
                // An insect's height is a small fraction of its antenna span.
                // Use a 2cm synthetic insect, rather than an oversized 30cm body.
                float height=index==1?.02f:.3f;
                Require(controller.ConfirmFit(bottom,true)&&controller.ConfirmFit(bottom+Vector3.up*height,true)&&original.sizeConfirmed&&!controller.FitConfirmationPending&&Mathf.Abs(original.ConfirmedHeight-height)<.0001f,"Synthetic manual Fit failed");
                if(index==4)Click("Test bloom");
                else if(index==0||index==2||index==3)Click("Test awaken");
                else Require(controller.Dispatch(ParkAction.Awaken,original.transform.position,true),"Scene controller rejected animation: "+controller.InteractionStatus);
                Require(original.Busy,"Actual model coroutine did not start");
                Click(index==2?"Plant":Profiles[(index+1)%6]);Require(controller.selected==original&&(int)controller.scenario==index&&ui.Feedback.Contains("Wait for animation"),"UI switched while animation busy or missed busy gate");
                Later(3,1.6);
            }
            else if(stage==3)
            {
                Require(!original.Busy&&original.Revision>0,"Scene animation did not finish");
                if(index==4)Require(original.PlantBloomVisible,"Actual plant UI bloom not visible");Capture("Action");
                if(index==3||index==5){Click("Stop");Require(original.Busy,"Stop button did not begin fade");}
                Later(4,.6);
            }
            else if(stage==4)
            {
                Require(!original.Busy,"Stop did not finish");
                Click("Plane");Require(controller.ConfirmManualPlane(original.transform.position+Vector3.up*.6f,true)&&original.confirmedPlane,"Post-fit manual plane failed");
                var fit=original.transform.Find("Manual fit - separate from tracking and animation");var fitPosition=fit.localPosition;var fitScale=fit.localScale;
                Click("Reset");
                Require(original.IsExiting&&fit.localPosition==fitPosition&&fit.localScale==fitScale,"Exit snapped fitted model to default size/bottom");Capture("Reset-exit");
                Require(controller.InstanceCount==0&&!controller.selected&&!controller.FitConfirmationPending&&!controller.PlaneConfirmationPending,"Reset left selection/confirmation");
                Click("Virtual test");Require(!controller.simulation,"Virtual toggle did not leave preview");Click("Virtual test");
                Require(controller.selected&&controller.simulation,"Virtual preview could not restart");
                Click("Room");Require(ui.Feedback.Contains("Confirm room"),"Room route skipped manual acknowledgement");
                Click("Confirm room");Click(Profiles[(index+1)%6]);switched=controller.selected;
                Require(switched&&controller.simulation&&(int)controller.scenario==(index+1)%6&&switched.scenario==controller.scenario&&controller.InstanceCount==1,"Virtual content switch cleared model without spawning selected content");
                Later(5,1.2);
            }
            else if(stage==5)
            {
                Require(!original&&!switched.Busy&&UnityEngine.Object.FindObjectsByType<ParkActor>(FindObjectsSortMode.None).Length==1,"Content switch leaked old model");Capture("Switched");
                Require(controller.Dispatch(ParkAction.Awaken,switched.transform.position,true),"Switched model rejected action");Click("Reset");
                Require(controller.InstanceCount==0&&!controller.selected,"Mid-action Reset retained actor");
                Click("Replay");Require(ui.replayMode&&controller.simulation&&!ui.receiver.enabled,"Replay did not isolate real receiver");
                if(ui.Selection.automatic)Click("Auto");Later(6,2.2);
            }
            else if(stage==6)
            {
                Require(!switched&&ui.CurrentFrame!=null&&ui.Selection.Candidates.Any(c=>c.instance_id=="simulation-tree"),"Actual resource replay did not populate candidate");
                var candidate=camera.GetComponentsInChildren<Button>(false).Single(b=>b.transform.parent.name=="Choose recognized target"&&b.name.Contains("simulation-tree"));Click(candidate);replayed=controller.selected;
                Require(replayed&&replayed.virtualTarget&&controller.scenario==ParkScenario.Tree&&controller.recognitionLock=="simulation-tree","Replay candidate callback did not select virtual tree");
                Later(7,1.2);
            }
            else if(stage==7)
            {
                Capture("Replay");Click("Virtual test");
                Require(!ui.replayMode&&controller.simulation&&controller.selected&&ui.receiver.enabled&&ui.CurrentFrame==null&&ui.Selection.LockedId==null&&controller.recognitionLock==null,"Replay to Virtual button retained replay state or disabled simulation");
                Later(8,1.1);
            }
            else if(stage==8)
            {
                Require(!replayed&&controller.InstanceCount==1,"Replay boundary leaked old actor");Click("Virtual test");
                Require(!controller.simulation&&!ui.replayMode&&ui.receiver.enabled&&!controller.selected&&controller.InstanceCount==0,"Virtual to live boundary retained actors/mode");
                if(ui.Selection.automatic!=previousAutomatic)Click("Auto");Later(9,.7);
            }
            else if(stage==9)
            {
                Require(UnityEngine.Object.FindObjectsByType<ParkActor>(FindObjectsSortMode.None).Length==0&&UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length==0,"Flow leaked actor/audio");
                Require(!controller.GetComponentsInChildren<Transform>(true).Any(t=>t.name.StartsWith("Manual horizontal surface -")),"Flow leaked surface anchor");
                Require(camera.GetComponentsInChildren<Button>(true).Count(b=>b.transform.parent.name=="Shared scene menu")==29,"Flow duplicated menu");
                Require(errors==0,"Flow logged runtime errors: "+errors);
                File.WriteAllText(Path.Combine(Output,ParkContentBuilder.Names[index]+".json"),JsonUtility.ToJson(new Result{scene=ParkContentBuilder.Names[index],passed=true,runId=SessionState.GetString(Key+".Run",""),errorLogs=errors,snapshots=snapshots},true));
                Debug.Log("PARK_ACTUAL_SCENE_FLOW_PASS "+ParkContentBuilder.Names[index]+" original camera, callbacks, virtual switch, replay boundaries, cleanup");
                SessionState.SetString(Key+".Phase","next");EditorApplication.ExitPlaymode();
            }
        }
        catch(Exception e){SessionState.SetBool(Key,false);Debug.LogException(e);File.WriteAllText(Path.Combine(Output,"failure.txt"),e.ToString());File.WriteAllText(Path.Combine(Output,"result.txt"),"FAIL: "+e);EditorApplication.Exit(1);}
    }
    static void Capture(string label)
    {
        var target=new RenderTexture(1024,768,24);var pixels=new Texture2D(1024,768,TextureFormat.RGB24,false);var old=RenderTexture.active;var oldTarget=camera.targetTexture;
        try
        {
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1024,768),0,0);pixels.Apply();var png=pixels.EncodeToPNG();
            Require(png.Length<3*1024*1024,"Scene snapshot exceeded cap");File.WriteAllBytes(Path.Combine(Output,ParkContentBuilder.Names[SessionState.GetInt(Key+".Index",0)]+"-"+label+".png"),png);snapshots++;
        }
        finally{camera.targetTexture=oldTarget;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
    }
    static void Finish()
    {
        foreach(var name in ParkContentBuilder.Names) {
            var result=JsonUtility.FromJson<Result>(File.ReadAllText(Path.Combine(Output,name+".json")));
            Require(result.passed&&result.runId==SessionState.GetString(Key+".Run",""),"Missing current scene result "+name);
        }
        SessionState.SetBool(Key,false);string previous=SessionState.GetString(Key+".Previous","");if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);
        File.WriteAllText(Path.Combine(Output,"result.txt"),"PASS: six actual saved Park scenes, original camera/components, Button callbacks, virtual content switch, controller action, manual fit/plane using synthetic points, Reset/Stop and exit size/bottom continuity, replay candidate and mode boundaries, actor/audio/surface cleanup; 30 scene snapshots. No real pointer/gesture/recognition/native/device acceptance.\n");
        Debug.Log("PARK_ACTUAL_SIX_SCENE_FLOW_PASS");EditorApplication.Exit(0);
    }
}
