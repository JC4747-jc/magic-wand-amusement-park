#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Saved ParkBugs scene; synthetic action dispatch, actual production coroutine
// and LateUpdate samples. This does not claim hand/camera/YOLO/device coverage.
[InitializeOnLoad]
public static class HciBugDanceChecks
{
    const string Key="MagicMR.HciBugDance";
    static string Output=>Path.GetFullPath(".codex-tmp/hci-bug-dance");
    static readonly List<string> rows=new List<string>{"species,mode,frame,time,busy,x,y,z,anchorError,frameDelta,observedFrameSeconds"};
    static readonly List<string> results=new List<string>();
    static ParkController controller;
    static ParkActor actor;
    static Camera camera;
    static HciBugDanceProbe probe;
    static Vector3 anchor;
    static Quaternion anchorRotation;
    static int phase,species,errors,observedFrame,samples,holds,switches,stableFrames,settledFrames;
    static bool capturedLeft,capturedRight,hadBusy,interrupt;
    static float acceptedAt,lastX,maxAnchorError,observedFrameSeconds;
    static double started,next;
    static string Species=>species==0?"Cockroach":"Spider";
    static HciBugDanceChecks()
    {
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};
    }
    public static void Run()
    {
        Require(Application.isBatchMode,"Use an isolated bounded batch editor");
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);
        EditorSceneManager.OpenScene("Assets/Scenes/ParkBugs.unity",OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Later(int value,float seconds=.6f){phase=value;next=EditorApplication.timeSinceStartup+seconds;}
    static void Save()
    {
        File.WriteAllLines(Output+"/timeline.csv",rows);
        File.WriteAllLines(Output+"/measurements.txt",results);
    }
    static void PrepareCamera()
    {
        var skin=actor.GetComponentInChildren<NaturalInsectRig>().Skin;
        var bounds=skin.bounds;
        camera.orthographic=true;camera.orthographicSize=Mathf.Max(.05f,bounds.extents.magnitude*1.35f+.055f);
        camera.transform.position=bounds.center+new Vector3(.15f,.19f,-.22f).normalized*1.5f;
        camera.transform.LookAt(bounds.center);camera.nearClipPlane=.001f;camera.farClipPlane=5;
        camera.cullingMask=1<<31;
        foreach(var child in actor.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
    }
    static void Capture(string name)
    {
        var rt=RenderTexture.GetTemporary(960,640,24);
        var prior=RenderTexture.active;var priorTarget=camera.targetTexture;
        Texture2D pixels=null;
        try
        {
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            pixels=new Texture2D(960,640,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,960,640),0,0);pixels.Apply();
            File.WriteAllBytes(Output+"/"+Species+"-"+name+".png",pixels.EncodeToPNG());
        }
        finally{RenderTexture.active=prior;camera.targetTexture=priorTarget;if(pixels)UnityEngine.Object.Destroy(pixels);RenderTexture.ReleaseTemporary(rt);}
    }
    static void Begin(bool cancel)
    {
        samples=holds=switches=settledFrames=0;maxAnchorError=observedFrameSeconds=0;lastX=0;hadBusy=capturedLeft=capturedRight=false;
        interrupt=cancel;acceptedAt=Time.unscaledTime;observedFrame=-1;
        Require(controller.Dispatch(ParkAction.Awaken,anchor,true),"B dispatch rejected: "+controller.InteractionStatus);
        phase=cancel?5:3;
    }
    public static void Sample()
    {
        if(!SessionState.GetBool(Key,false))return;
        stableFrames=Time.unscaledDeltaTime<.05f?stableFrames+1:0;
        if(!actor||(phase!=3&&phase!=5)||observedFrame==Time.frameCount)return;
        observedFrame=Time.frameCount;
        try
        {
            float frameDelta=Time.unscaledDeltaTime;observedFrameSeconds+=frameDelta;
            var p=actor.visual.localPosition;var error=Vector3.Distance(actor.transform.position,anchor);
            maxAnchorError=Mathf.Max(maxAnchorError,error);
            Require(error<.00001f&&Quaternion.Angle(actor.transform.rotation,anchorRotation)<.01f,"Dance moved recognition anchor");
            Require(Mathf.Abs(p.y)<.00001f&&Mathf.Abs(p.z)<.00001f,"Dance drifted outside horizontal beat axis");
            if(actor.Busy)
            {
                hadBusy=true;samples++;
                Require(Mathf.Abs(Mathf.Abs(p.x)-.04f)<.0001f||Mathf.Abs(p.x)<.0001f,"Unexpected non-discrete dance translation");
                if(Mathf.Abs(p.x-lastX)<.0001f)holds++;else switches++;
                lastX=p.x;
                if(!interrupt&&!capturedLeft&&p.x<-.03f){capturedLeft=true;Capture("02-left-beat");}
                if(!interrupt&&!capturedRight&&p.x>.03f){capturedRight=true;Capture("01-right-beat");}
            }
            else settledFrames++;
            rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3:F6},{4},{5:F6},{6:F6},{7:F6},{8:E4},{9:F6},{10:F6}",Species,interrupt?"reset":"complete",Time.frameCount,Time.unscaledTime-acceptedAt,actor.Busy,p.x,p.y,p.z,error,frameDelta,observedFrameSeconds));
        }
        catch(Exception e){Fail(e);}
    }
    static void Fail(Exception e)
    {
        SessionState.SetBool(Key,false);Save();File.WriteAllText(Output+"/result.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(2);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        EditorApplication.QueuePlayerLoopUpdate();
        if(!EditorApplication.isPlaying)
        {
            if(SessionState.GetBool(Key+".Done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}return;
        }
        try
        {
            if(phase==0)
            {
                started=EditorApplication.timeSinceStartup;QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
                probe=new GameObject("Bug dance frame observer").AddComponent<HciBugDanceProbe>();
                camera=new GameObject("Bug dance evidence camera").AddComponent<Camera>();camera.enabled=false;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.18f,.21f);
                Later(1);return;
            }
            Require(EditorApplication.timeSinceStartup-started<50,"Playback observation timed out");
            if(EditorApplication.timeSinceStartup<next)return;
            if(phase==1)
            {
                controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();
                Require(controller&&controller.scenario==ParkScenario.Bugs,"Not the saved ParkBugs scene");
                controller.simulation=true;controller.ResetScene();controller.SpawnVirtual(Species);Later(2);
            }
            else if(phase==2)
            {
                actor=controller.selected;Require(actor&&actor.species==Species,"Wrong insect variant");
                var rig=actor.GetComponentInChildren<NaturalInsectRig>();Require(rig&&rig.LegCount==(species==0?6:8),"Missing articulated production insect");
                anchor=actor.transform.position;
                Require(actor.ConfirmOrientation(0)&&actor.FitRepresentation(anchor,anchor+Vector3.up*.08f,true),"Manual insect fit rejected");
                anchorRotation=actor.transform.rotation;if(actor.label)actor.label.gameObject.SetActive(false);
                PrepareCamera();Capture("00-before");stableFrames=0;Later(20,0);
            }
            else if(phase==20)
            {
                if(stableFrames<12)return;
                Begin(false);
            }
            else if(phase==3)
            {
                if(actor.Busy||settledFrames<2)return;
                Require(hadBusy&&samples>=12&&holds>=6&&switches>=4,"Insufficient hold/step timeline: "+samples+"/"+holds+"/"+switches);
                Require(capturedLeft&&capturedRight&&actor.visual.localPosition.sqrMagnitude<1e-10f,"Dance did not return to origin");
                results.Add(string.Format(CultureInfo.InvariantCulture,"{0}: samples={1}; holds={2}; switches={3}; wallClockSeconds={4:F4}; observedFrameSeconds={5:F4}; configuredSeconds={6:F4}; maxAnchorError={7:E4}",Species,samples,holds,switches,Time.unscaledTime-acceptedAt,observedFrameSeconds,actor.transitionSeconds,maxAnchorError));
                Capture("03-return");
                Require(controller.Dispatch(ParkAction.Undo,anchor,true),"Completed dance Undo rejected");Later(4);
            }
            else if(phase==4)Begin(true);
            else if(phase==5)
            {
                // Perform has a scan lead-in before the actual dance. Observe
                // two real beat transitions instead of guessing from duration.
                if(actor.Busy&&switches<2)return;
                Require(actor.Busy&&hadBusy&&switches>=2,"Cancellation did not target a live dance");
                Require(!controller.Dispatch(ParkAction.Undo,anchor,true)&&actor.Busy,"Mid-dance Undo bypassed the production busy guard");
                Require(controller.Dispatch(ParkAction.Reset,anchor,true),"Immediate Reset rejected");Later(6,.1f);
            }
            else if(phase==6)
            {
                Require(!actor.Busy&&actor.visual.localPosition.sqrMagnitude<1e-10f,"Interrupted dance retained root offset");
                results.Add(Species+": completed Undo accepted; mid-dance Undo blocked by existing busy guard; immediate Reset cleared offset; anchor preserved");
                if(species++==0){Later(1);return;}
                Require(errors==0,"Unity errors during playback: "+errors);Save();
                File.WriteAllText(Output+"/result.txt","PASS: saved ParkBugs, Cockroach/Spider production B timeline, discrete holds/steps, anchor invariance, completed Undo, busy-guarded Undo and immediate Reset; synthetic action dispatch only, no YOLO/gesture/device acceptance.\n");
                SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();
            }
        }
        catch(Exception e){Fail(e);}
    }
}
[DefaultExecutionOrder(10000)]
public sealed class HciBugDanceProbe:MonoBehaviour
{
    void LateUpdate()=>HciBugDanceChecks.Sample();
}
#endif
