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

// Saved ParkBugs scene; timed synthetic production hand samples, actual coroutine
// and LateUpdate samples. This does not claim hand/camera/YOLO/device coverage.
[InitializeOnLoad]
public static class HciBugDanceChecks
{
    const string Key="MagicMR.HciBugDance";
    static string Output=>Path.GetFullPath(".codex-tmp/hci-bug-dance");
    static readonly List<string> rows=new List<string>{"species,appearance,mode,frame,time,busy,x,y,z,anchorError,frameDelta,observedFrameSeconds,maximumIKError"};
    static readonly List<string> joints=new List<string>{"species,appearance,attempt,sample,time,expected,actual,accepted"};
    static readonly List<string> results=new List<string>();
    static ParkController controller;
    static ParkActor actor;
    static Camera camera;
    static HciBugDanceProbe probe;
    static NaturalInsectRig rig;static Mesh naturalMesh;
    static Vector3 anchor;
    static Quaternion anchorRotation;
    static int phase,species,errors,observedFrame,samples,holds,switches,stableFrames,settledFrames;
    static bool capturedLeft,capturedRight,hadBusy,interrupt;
    static float acceptedAt,lastX,maxAnchorError,observedFrameSeconds,movedAt,maxIKError;
    static List<TabletopHandSample> trace;static TabletopGestureRules reference;
    static TabletopHandSample held;static bool holdValid;static int traceIndex,gestureEvents,acceptedGestures;static float nextInputAt;
    static double started,next;
    static string Species=>species%2==0?"Cockroach":"Spider";
    static bool Pixel=>species>=2;
    static string Appearance=>Pixel?"pixel":"natural";
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
        File.WriteAllLines(Output+"/joint-trace.csv",joints);
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
            File.WriteAllBytes(Output+"/"+Species+"-"+Appearance+"-"+name+".png",pixels.EncodeToPNG());
        }
        finally{RenderTexture.active=prior;camera.targetTexture=priorTarget;if(pixels)UnityEngine.Object.Destroy(pixels);RenderTexture.ReleaseTemporary(rt);}
    }
    static void Begin(bool cancel)
    {
        samples=holds=switches=settledFrames=0;maxAnchorError=observedFrameSeconds=maxIKError=0;movedAt=-1;lastX=0;hadBusy=capturedLeft=capturedRight=false;
        interrupt=cancel;observedFrame=-1;holdValid=false;controller.ProcessHandSample(default,false);
        reference=new TabletopGestureRules();traceIndex=gestureEvents=0;nextInputAt=Time.unscaledTime;
        TabletopHandSample Frame(bool point,Vector3 delta){var p=anchor+Vector3.up*.05f+delta;return TabletopHandSample.Create(0,p,p+Vector3.up*.04f,p+Vector3.up*.04f+Vector3.right*.08f,p-Vector3.up*.02f,point?new Vector4(.95f,.55f,.55f,.55f):Vector4.one*.95f,Vector3.right,.08f);}
        trace=new List<TabletopHandSample>{Frame(false,Vector3.zero),Frame(false,Vector3.zero)};
        for(int n=0;n<=32;n++){float angle=n/32f*Mathf.PI*2;trace.Add(Frame(true,new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*.04f));}
        phase=30;
    }
    static void Pump(){try{
        if(!controller)return;
        if(trace!=null){
            if(Time.unscaledTime<nextInputAt)return;var sample=trace[traceIndex++];sample.gesture.time=Time.unscaledTime;nextInputAt=Time.unscaledTime+.03f;
            var expected=traceIndex==1?EditDimension.None:reference.Step(sample.gesture);var actual=controller.ProcessHandSample(sample,true);
            Require(expected==actual,"B joint recognition differs from shared rules: "+controller.InteractionStatus);
            joints.Add(FormattableString.Invariant($"{Species},{Appearance},{(interrupt?"reset":"complete")},{traceIndex},{sample.gesture.time:R},{expected},{actual},{controller.LastMagicAccepted}"));
            if(actual!=EditDimension.None){Require(actual==EditDimension.Agency&&controller.LastMagicAccepted,"Wrong/rejected B joint event");gestureEvents++;acceptedGestures++;acceptedAt=Time.unscaledTime;phase=interrupt?5:3;}
            held=sample;holdValid=true;
            if(traceIndex==trace.Count){Require(gestureEvents==1,"B trace did not fire exactly once");trace=null;}
        }else if(holdValid){held.gesture.time=Time.unscaledTime;Require(controller.ProcessHandSample(held,true)==EditDimension.None,"Held hand repeated B");}
    }catch(Exception e){Fail(e);}}
    static float ShaderStyle(){var block=new MaterialPropertyBlock();rig.Skin.GetPropertyBlock(block);return block.GetFloat("_Style");}
    static void PrepareCase(){
        float style=ShaderStyle();Require(Pixel?style>1.99f:style<.01f,"Appearance not settled before B");
        File.AppendAllText(Output+"/appearance-state.txt",FormattableString.Invariant($"{Species}/{Appearance}: style={style:R}; mesh={rig.Skin.sharedMesh.name}\n"));
        PrepareCamera();Capture("00-before");stableFrames=0;Later(20,0);
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
                Require(rig.Skin.sharedMesh.name.StartsWith("Pixel"+Species)==Pixel&&(Pixel?ShaderStyle()>1.99f:ShaderStyle()<.01f),"B changed insect appearance");
                for(int i=0;i<rig.LegCount;i++){float errorIK=Vector3.Distance(rig.FootLocal(i),rig.FootTarget(i));maxIKError=Mathf.Max(maxIKError,errorIK);Require(errorIK<.0008f,"Dance broke leg IK");}
                Require(Mathf.Abs(Mathf.Abs(p.x)-.04f)<.0001f||Mathf.Abs(p.x)<.0001f,"Unexpected non-discrete dance translation");
                if(Mathf.Abs(p.x-lastX)<.0001f)holds++;else switches++;
                lastX=p.x;
                if(movedAt<0&&Mathf.Abs(p.x)>.03f)movedAt=Time.unscaledTime;
                if(!interrupt&&!capturedLeft&&p.x<-.03f){capturedLeft=true;Capture("02-left-beat");}
                if(!interrupt&&!capturedRight&&p.x>.03f){capturedRight=true;Capture("01-right-beat");}
            }
            else settledFrames++;
            rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4:F6},{5},{6:F6},{7:F6},{8:F6},{9:E4},{10:F6},{11:F6},{12:E6}",Species,Appearance,interrupt?"reset":"complete",Time.frameCount,Time.unscaledTime-acceptedAt,actor.Busy,p.x,p.y,p.z,error,frameDelta,observedFrameSeconds,maxIKError));
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
                new GameObject("Synthetic bug B joint driver").AddComponent<HciSeasonTreeHandPump>().sample=Pump;
                camera=new GameObject("Bug dance evidence camera").AddComponent<Camera>();camera.enabled=false;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.18f,.21f);
                Later(1);return;
            }
            Require(EditorApplication.timeSinceStartup-started<90,"Four-appearance joint playback observation timed out");
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
                rig=actor.GetComponentInChildren<NaturalInsectRig>();Require(rig&&rig.LegCount==(species%2==0?6:8),"Missing articulated production insect");naturalMesh=rig.Skin.sharedMesh;
                anchor=actor.transform.position;
                Require(actor.ConfirmOrientation(0)&&actor.FitRepresentation(anchor,anchor+Vector3.up*.08f,true),"Manual insect fit rejected");
                anchorRotation=actor.transform.rotation;if(actor.label)actor.label.gameObject.SetActive(false);
                if(Pixel){Require(controller.Dispatch(ParkAction.Style,anchor,true),"Pixel setup A rejected");Later(21,0);}else PrepareCase();
            }
            else if(phase==21){if(actor.Busy||ShaderStyle()<=1.99f)return;Require(rig.Skin.sharedMesh!=naturalMesh&&rig.Skin.sharedMesh.name.StartsWith("Pixel"+Species),"Pixel mesh not selected by production A");PrepareCase();}
            else if(phase==20)
            {
                if(stableFrames<12)return;
                Begin(false);
            }
            else if(phase==3)
            {
                if(actor.Busy||settledFrames<2)return;
                // Production uses six normalized beat segments over the configured
                // .7s action, not twelve switches. Retain the prior four-switch
                // observation floor plus full-duration and both-sign guards.
                Require(hadBusy&&samples>=12&&holds>=6&&switches>=4,"Insufficient hold/step timeline: "+samples+"/"+holds+"/"+switches);
                Require(movedAt>=0&&Time.unscaledTime-movedAt>=actor.transitionSeconds-.06f,"Dance ended before configured full duration");
                Require(capturedLeft&&capturedRight&&actor.visual.localPosition.sqrMagnitude<1e-10f,"Dance did not return to origin");
                results.Add(string.Format(CultureInfo.InvariantCulture,"{0}/{1}: samples={2}; holds={3}; switches={4}; wallClockSeconds={5:F4}; observedFrameSeconds={6:F4}; configuredSeconds={7:F4}; danceSeconds={8:F4}; maxAnchorError={9:E4}; maxIKError={10:E6}",Species,Appearance,samples,holds,switches,Time.unscaledTime-acceptedAt,observedFrameSeconds,actor.transitionSeconds,Time.unscaledTime-movedAt,maxAnchorError,maxIKError));
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
                results.Add(Species+"/"+Appearance+": completed Undo accepted; mid-dance Undo blocked by existing busy guard; immediate Reset cleared offset; anchor preserved");holdValid=false;controller.ProcessHandSample(default,false);
                if(++species<4){Later(1);return;}
                Require(acceptedGestures==8,"Expected eight exactly-once B gestures");
                Require(errors==0,"Unity errors during playback: "+errors);Save();
                File.WriteAllText(Output+"/result.txt","PASS: saved ParkBugs, Cockroach/Spider natural and pixel, eight exactly-once timed synthetic production B joint traces; complete configured dance, discrete holds/steps, leg IK, appearance/anchor invariance, completed Undo, busy-guarded Undo and immediate Reset. Actual coroutine and camera renders; no physical camera/YOLO/hands/device or final art acceptance.\n");
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
