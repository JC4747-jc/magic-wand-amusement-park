#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Synthetic joint-derived samples, actual player time, production hand/action
// pipeline. Virtual targets and synthetic tracking loss; no real YOLO/PICO claim.
[InitializeOnLoad]
public static class HciBugOutcomeChecks
{
    const string Key="MagicMR.HciBugOutcome";
    static bool UsePixel=>SessionState.GetBool(Key+".Pixel",true);
    static string Output=>Path.GetFullPath(UsePixel?".codex-tmp/hci-bug-outcome":".codex-tmp/hci-bug-outcome-natural");
    static readonly List<string> joints=new List<string>{"species,trace,index,time,raw,actual,accepted,status"};
    static readonly List<string> poses=new List<string>{"species,phase,frame,time,busy,rotationTravel,score,skinVisible,feedback,coins,anchorError,rewardAge"};
    static readonly List<string> results=new List<string>();
    static ParkController controller;
    static ParkActor actor;
    static NaturalInsectRig rig;
    static Mesh original;
    static MeshFilter[] pool;
    static Camera camera;
    static Vector3 anchor,rigScale;
    static Quaternion anchorRotation,previousRotation;
    static List<TabletopHandSample> trace;
    static TabletopGestureRules reference;
    static TabletopHandSample held;
    static EditDimension expected;
    static bool allow,accept,holdValid,watchC,watchD,capturedC,capturedD;
    static int phase,species,errors,traceIndex,rawEvents,actualEvents,totalCasts,stableFrames,holdSamples,duplicateRevision,observedFrame=-1;
    static float sampleAt,travel,maxAnchorError,rewardVisibleAt=-1;
    static double started,next;
    static string Species=>species==0?"Cockroach":"Spider";
    static HciBugOutcomeChecks()
    {
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};
    }
    public static void Run()=>Start(true);
    public static void RunNatural()=>Start(false);
    static void Start(bool pixel)
    {
        Require(Application.isBatchMode,"Bounded isolated editor required");SessionState.SetBool(Key+".Pixel",pixel);Directory.CreateDirectory(Output);
        File.WriteAllText(Output+"/result.txt","RUNNING\n");SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);
        EditorSceneManager.OpenScene("Assets/Scenes/ParkBugs.unity",OpenSceneMode.Single);EditorApplication.EnterPlaymode();
    }
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Later(int value,double delay=.1){phase=value;next=EditorApplication.timeSinceStartup+delay;}
    static float Style(){var b=new MaterialPropertyBlock();rig.Skin.GetPropertyBlock(b);return b.GetFloat("_Style");}
    static int Feedback(string shape=null){int count=0;foreach(var p in pool)if(p&&p.gameObject.activeInHierarchy&&(shape==null||p.sharedMesh.name=="ParkFeedback_"+shape))count++;return count;}
    static void Save(){File.WriteAllLines(Output+"/joints.csv",joints);File.WriteAllLines(Output+"/poses.csv",poses);File.WriteAllLines(Output+"/measurements.txt",results);}
    static void Fail(Exception e){SessionState.SetBool(Key,false);Save();File.WriteAllText(Output+"/result.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(2);}
    static void Fit(){Require(actor.ConfirmOrientation(0)&&actor.FitRepresentation(anchor,anchor+Vector3.up*.08f,true),"Manual test fit rejected");anchorRotation=actor.transform.rotation;}
    static TabletopHandSample Frame(string shape,float pinch=.08f,Vector3 delta=default)
    {
        var extensions=shape=="fist"?Vector4.one*.55f:Vector4.one*.95f;var p=anchor+Vector3.up*.05f+delta;
        return TabletopHandSample.Create(0,p,p+Vector3.up*.04f,p+Vector3.up*.04f+Vector3.right*pinch,p-Vector3.up*.02f,extensions,Vector3.right,pinch);
    }
    static void Cast(EditDimension dimension,bool targetAllowed=true,bool expectedAcceptance=true)
    {
        controller.ProcessHandSample(default,false);holdValid=false;traceIndex=rawEvents=actualEvents=0;sampleAt=Time.unscaledTime;
        expected=dimension;allow=targetAllowed;accept=expectedAcceptance;reference=new TabletopGestureRules();trace=new List<TabletopHandSample>{Frame("open"),Frame("open")};
        if(dimension==EditDimension.Appearance){for(int n=0;n<8;n++)trace.Add(Frame("open",.02f));trace.Add(Frame("open"));}
        else if(dimension==EditDimension.Rule){for(int n=1;n<=8;n++)trace.Add(Frame("open",.08f,Vector3.right*n*.02f));}
        else{for(int n=0;n<6;n++)trace.Add(Frame("fist",.02f));trace.Add(Frame("open"));}
    }
    public static void Sample()
    {
        if(!SessionState.GetBool(Key,false))return;stableFrames=Time.unscaledDeltaTime<.05f?stableFrames+1:0;
        try
        {
            if(controller&&Time.unscaledTime>=sampleAt)
            {
                sampleAt=Time.unscaledTime+.03f;
                if(trace!=null)
                {
                    var frame=trace[traceIndex++];frame.gesture.time=Time.unscaledTime;
                    var raw=traceIndex==1?EditDimension.None:reference.Step(frame.gesture);
                    var actual=controller.ProcessHandSample(frame,true);
                    if(raw!=EditDimension.None){rawEvents++;Require(raw==expected,"Wrong reference gesture");}
                    Require(actual==(allow?raw:EditDimension.None),"Gesture/policy mismatch: "+raw+"/"+actual+" "+controller.InteractionStatus);
                    if(actual!=EditDimension.None){actualEvents++;totalCasts++;Require(controller.LastMagicAccepted==accept,"Unexpected acceptance: "+controller.InteractionStatus);}
                    joints.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3:F6},{4},{5},{6},{7}",Species,expected,traceIndex,frame.gesture.time,raw,actual,controller.LastMagicAccepted,controller.InteractionStatus.Replace(',',';')));
                    held=frame;holdValid=true;
                    if(traceIndex==trace.Count){Require(rawEvents==1&&actualEvents==(allow?1:0),"Trace event count mismatch");trace=null;}
                }
                else if(holdValid){held.gesture.time=Time.unscaledTime;Require(controller.ProcessHandSample(held,true)==EditDimension.None,"Held sample repeated a spell");holdSamples++;}
            }
            if(!actor||observedFrame==Time.frameCount)return;observedFrame=Time.frameCount;
            float error=Vector3.Distance(actor.transform.position,anchor);maxAnchorError=Mathf.Max(maxAnchorError,error);
            Require(error<.00001f&&Quaternion.Angle(actor.transform.rotation,anchorRotation)<.01f,"Effect moved recognition anchor");
            if(watchC){travel+=Quaternion.Angle(previousRotation,actor.visual.localRotation);previousRotation=actor.visual.localRotation;Require(actor.VirtualScore==0,"C awarded D score");}
            if(watchD&&actor.VirtualScore==100&&rewardVisibleAt<0)rewardVisibleAt=Time.unscaledTime;
            if(watchC||watchD)poses.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3:F6},{4},{5:F3},{6},{7},{8},{9},{10:E4},{11:F6}",Species,phase,Time.frameCount,Time.unscaledTime,actor.Busy,travel,actor.VirtualScore,rig.Skin.enabled,Feedback(),Feedback("Coin"),error,rewardVisibleAt<0?-1:Time.unscaledTime-rewardVisibleAt));
        }catch(Exception e){Fail(e);}
    }
    static void FrameCamera()
    {
        // Fit after actual frames have settled the skinned bounds. Keep this
        // camera fixed for C, D and Undo so it cannot cancel their motion.
        var bounds=rig.Skin.bounds;var center=anchor+Vector3.up*.2f;
        float radius=Mathf.Max(.5f,bounds.extents.magnitude+Vector3.Distance(bounds.center,center));
        camera.orthographicSize=radius*1.05f;camera.transform.position=center+new Vector3(.15f,.20f,-.25f).normalized*2;camera.transform.LookAt(center);
        results.Add(Species+": fixed render radius="+radius.ToString("F4",CultureInfo.InvariantCulture));
    }
    static void Capture(string name)
    {
        var rt=RenderTexture.GetTemporary(960,640,24);var old=RenderTexture.active;var target=camera.targetTexture;Texture2D image=null;
        try{camera.targetTexture=rt;camera.Render();if(name=="warmup"||!UsePixel&&(name=="00-before-C"||name=="03-Undo-restored"))return;RenderTexture.active=rt;image=new Texture2D(960,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,640),0,0);image.Apply();File.WriteAllBytes(Output+"/"+Species+"-"+name+".png",image.EncodeToPNG());}
        finally{RenderTexture.active=old;camera.targetTexture=target;if(image)UnityEngine.Object.Destroy(image);RenderTexture.ReleaseTemporary(rt);}
    }
    static void StartC(int value)
    {
        travel=0;previousRotation=actor.visual.localRotation;watchC=true;capturedC=false;Cast(EditDimension.Rule);Later(value,0);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;EditorApplication.QueuePlayerLoopUpdate();
        if(!EditorApplication.isPlaying){if(SessionState.GetBool(Key+".Done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}return;}
        try
        {
            if(phase==0)
            {
                started=EditorApplication.timeSinceStartup;QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
                new GameObject("Synthetic insect hand observer").AddComponent<HciBugOutcomeProbe>();
                camera=new GameObject("Insect C D evidence camera").AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.18f,.21f);camera.cullingMask=1<<31;Later(1,.6);return;
            }
            Require(EditorApplication.timeSinceStartup-started<120,"Outcome playback timeout");Require(errors==0,"Unity errors: "+errors);
            if(EditorApplication.timeSinceStartup<next)return;
            if(phase==1){controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();Require(controller&&controller.scenario==ParkScenario.Bugs,"Wrong saved scene");controller.simulation=true;controller.ResetScene();controller.SpawnVirtual(Species);Later(2,.6);}
            else if(phase==2)
            {
                actor=controller.selected;Require(actor&&actor.species==Species,"Wrong insect");rig=actor.GetComponentInChildren<NaturalInsectRig>();rig.Initialize();original=rig.Skin.sharedMesh;
                anchor=actor.transform.position;Fit();rigScale=rig.transform.localScale;if(actor.label)actor.label.gameObject.SetActive(false);
                pool=actor.visual.GetComponentsInChildren<MeshFilter>(true).Where(p=>p.name.StartsWith("Pooled feedback ")).ToArray();Require(pool.Length==32,"Feedback pool must remain bounded at 32");
                foreach(var t in actor.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                var bounds=rig.Skin.bounds;bounds.Encapsulate(anchor+new Vector3(-.35f,-.10f,-.35f));bounds.Encapsulate(anchor+new Vector3(.35f,.6f,.35f));
                camera.orthographic=true;camera.orthographicSize=bounds.extents.magnitude*1.1f;camera.transform.position=bounds.center+new Vector3(.15f,.20f,-.25f).normalized*2;camera.transform.LookAt(bounds.center);camera.nearClipPlane=.001f;camera.farClipPlane=6;
                Capture("warmup");stableFrames=0;Later(3,0);
            }
            else if(phase==3){if(stableFrames<12)return;if(UsePixel)Cast(EditDimension.Appearance);Later(4,0);}
            else if(phase==4){if(trace!=null||actor.Busy||UsePixel&&Style()<1.99f)return;Require(UsePixel?rig.Skin.sharedMesh.name=="Pixel"+Species:rig.Skin.sharedMesh==original&&Style()<.01f,"Wrong appearance before C");FrameCamera();Capture("00-before-C");StartC(5);}
            else if(phase==5)
            {
                if(trace!=null)return;
                if(!capturedC&&travel>180&&actor.Busy){Require(Feedback("Star")>0,"C has no star feedback");Capture("01-C-live");capturedC=true;}
                if(actor.Busy)return;Require(capturedC&&travel>650&&travel<800,"C spin travel missing/incorrect: "+travel);Require(Quaternion.Angle(actor.visual.localRotation,Quaternion.identity)<.1f,"C did not restore orientation");
                results.Add(Species+": C rotationTravelDegrees="+travel.ToString("F3",CultureInfo.InvariantCulture)+"; stars present; anchor preserved");watchC=false;watchD=true;capturedD=false;rewardVisibleAt=-1;Cast(EditDimension.Deconstruction);Later(6,0);
            }
            else if(phase==6)
            {
                if(trace!=null)return;
                if(!capturedD&&rewardVisibleAt>=0&&Time.unscaledTime-rewardVisibleAt>=1.05f&&actor.VirtualScore==100&&!rig.Skin.enabled&&Feedback("Coin")>0){Require(Feedback("Coin")==16&&Feedback()==24,"D must emit 16 coins and 8 fragments from existing pool");Require(actor.gameObject.activeInHierarchy&&actor.tracked,"D deleted/untracked the selected anchor");Capture("02-D-coins");capturedD=true;}
                if(actor.Busy||Feedback()>0)return;Require(capturedD&&actor.VirtualScore==100&&!rig.Skin.enabled,"D final state missing");watchD=false;duplicateRevision=actor.Revision;Cast(EditDimension.Deconstruction,true,false);Later(7,0);
            }
            else if(phase==7)
            {
                if(trace!=null)return;Require(actor.Revision==duplicateRevision&&actor.VirtualScore==100&&!actor.Busy&&Feedback()==0,"Duplicate D restarted reward");
                Require(controller.Dispatch(ParkAction.Undo,anchor,true),"Completed D Undo rejected");Later(8,.1);
            }
            else if(phase==8)
            {
                Require(actor.VirtualScore==0&&rig.Skin.enabled&&(rig.transform.localScale-rigScale).sqrMagnitude<1e-8f,"Undo did not restore visible full-size insect");Require(UsePixel?rig.Skin.sharedMesh.name=="Pixel"+Species&&Style()>1.99f:rig.Skin.sharedMesh==original&&Style()<.01f,"D Undo changed appearance");Capture("03-Undo-restored");StartC(9);
            }
            else if(phase==9)
            {
                if(trace!=null||travel<180)return;Require(actor.Busy,"Loss must interrupt actual C motion");watchC=false;holdValid=false;actor.SetTracked(false);
                Require(!actor.Busy&&!actor.visual.gameObject.activeSelf&&Feedback()==0&&actor.VirtualScore==0&&rig.Skin.sharedMesh==original,"Tracking loss retained effects/reward/pixel mesh");Cast(EditDimension.Deconstruction,false,false);Later(10,0);
            }
            else if(phase==10)
            {
                if(trace!=null)return;Require(actor.VirtualScore==0&&!actor.Busy,"Lost target accepted a magic");actor.SetTracked(true);Fit();holdSamples=0;Later(11,.2);
            }
            else if(phase==11)
            {
                if(holdSamples<8)return;Require(actor.VirtualScore==0&&!actor.Busy&&rig.Skin.enabled,"Recovered held sample repeated old gesture");if(UsePixel)Cast(EditDimension.Appearance);Later(12,0);
            }
            else if(phase==12){if(trace!=null||actor.Busy||UsePixel&&Style()<1.99f)return;watchD=true;rewardVisibleAt=-1;Cast(EditDimension.Deconstruction);Later(13,0);}
            else if(phase==13)
            {
                if(trace!=null||actor.Busy)return;Require(actor.VirtualScore==100&&!rig.Skin.enabled&&pool.Length==32,"Fresh D after recovery failed or pool grew");watchD=false;holdValid=false;Require(controller.Dispatch(ParkAction.Reset,anchor,true),"Final Reset rejected");Later(14,.1);
            }
            else if(phase==14)
            {
                Require(actor.VirtualScore==0&&rig.Skin.enabled&&rig.Skin.sharedMesh==original&&Feedback()==0&&!actor.Busy,"Final Reset did not clear all C/D state");
                results.Add(Species+": appearance="+(UsePixel?"pixel":"natural")+"; D=16 coins+8 fragments; duplicate rejected; Undo retained appearance; loss/recovery held-input gate and fresh D passed; pool=32; maxAnchorError="+maxAnchorError.ToString("E4",CultureInfo.InvariantCulture));
                trace=null;holdValid=watchC=watchD=false;
                if(species++==0){Later(1,.6);return;}controller.ResetScene();Later(15,.6);
            }
            else if(phase==15)
            {
                Require(UnityEngine.Object.FindObjectsByType<NaturalInsectRig>(FindObjectsSortMode.None).Length==0,"Scene reset leaked insect rig");Save();
                File.WriteAllText(Output+"/result.txt","PASS: saved ParkBugs two species; "+(UsePixel?"pixel A/C/D":"natural C/D without A")+" synthetic joint samples via production ProcessHandSample, C spin/stars, D coins/fragments/duplicate gate/Undo, interrupted tracking loss, recovery held-input/new action, bounded pool and cleanup. Not physical YOLO/XR/PICO acceptance.\n");SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();
            }
        }catch(Exception e){Fail(e);}
    }
}
[DefaultExecutionOrder(20000)] public sealed class HciBugOutcomeProbe:MonoBehaviour{void LateUpdate()=>HciBugOutcomeChecks.Sample();}
#endif
