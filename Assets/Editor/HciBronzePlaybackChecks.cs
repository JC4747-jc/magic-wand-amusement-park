#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Synthetic joint samples use the actual controller and play the production coroutine.
// No replacement recognizer, detector, manual rig.Sample calls or real-device claim.
[InitializeOnLoad]
public static class HciBronzePlaybackChecks
{
    const string Key="MagicMR.HciBronzePlayback";
    static string Output=>Path.GetFullPath(".codex-tmp/hci-bronze-playback");
    static ParkController controller;
    static ParkActor actor;
    static FullBodyBronzeBeast animation;
    static StatueWishGarden garden;
    static Transform fit;
    static Vector3 anchor,fitPosition,fitScale,restHead;
    static Quaternion anchorRotation;
    static Transform[] bones;
    static Vector3[] restPositions,restFeet;
    static Quaternion[] restRotations;
    static int phase,errors,eventCount,traceIndex,samples,observedFrame=-1,casts;
    static double started,next;
    static float maxMotion,maxFootDrift,lastRecorded,acceptedAt,firstCycleSeconds;
    static bool watching,peakCaptured,holdValid;
    static TabletopHandSample held;
    static List<TabletopHandSample> trace;
    static TabletopGestureRules lighter;
    static EditDimension expectedDimension;
    static float sampleAt,traceStarted;
    static readonly List<string> joints=new List<string>{"dimension,time,pinch,expected,actual,ruleStatus"};
    static readonly List<string> records=new List<string>{"time,phase,progress,headMotion,maximumFootDrift,busy"};
    static HciBronzePlaybackChecks()
    {
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};
    }
    public static void Run()
    {
        Require(Application.isBatchMode,"Run only in an isolated bounded batch editor");
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);
        EditorSceneManager.OpenScene("Assets/Scenes/ParkStatue.unity");EditorApplication.EnterPlaymode();
    }
    static void Require(bool value,string reason){if(!value)throw new Exception(reason);}
    static void Fail(Exception error){SessionState.SetBool(Key,false);File.WriteAllLines(Output+"/joint-trace.csv",joints);File.WriteAllText(Output+"/result.txt","FAIL "+error);Debug.LogException(error);EditorApplication.Exit(2);}
    static void Later(int value,double delay=1.2){phase=value;next=EditorApplication.timeSinceStartup+delay;}
    static void Act(ParkAction action)=>Require(controller.Dispatch(action,anchor,true),action+" rejected: "+controller.InteractionStatus);
    static void Fit()
    {
        Require(actor.ConfirmOrientation(37)&&actor.FitRepresentation(anchor,anchor+Vector3.up*.35f,true),"Manual fit rejected");
        anchorRotation=actor.transform.rotation;fitPosition=fit.localPosition;fitScale=fit.localScale;
        restHead=animation.Head.position;restFeet=Enumerable.Range(0,4).Select(animation.FootPosition).ToArray();
    }
    static void Rest()
    {
        for(int i=0;i<bones.Length;i++)Require(Vector3.Distance(bones[i].localPosition,restPositions[i])<.00001f&&Quaternion.Angle(bones[i].localRotation,restRotations[i])<.1f,"Bone did not restore: "+bones[i].name);
    }
    static void Watch()
    {
        if(observedFrame==Time.frameCount)return;observedFrame=Time.frameCount;
        Require(actor.transform.position==anchor&&actor.transform.rotation==anchorRotation&&fit.localPosition==fitPosition&&fit.localScale==fitScale,"Live B moved tracking root or fit");
        float drift=0;for(int i=0;i<4;i++)drift=Mathf.Max(drift,Vector3.Distance(restFeet[i],animation.FootPosition(i)));
        Require(drift<.0001f,"Live B foot drift: "+drift);
        float motion=Vector3.Distance(restHead,animation.Head.position);maxMotion=Mathf.Max(maxMotion,motion);maxFootDrift=Mathf.Max(maxFootDrift,drift);samples++;
        if(Time.unscaledTime-lastRecorded>.05f){lastRecorded=Time.unscaledTime;records.Add(FormattableString.Invariant($"{Time.unscaledTime:F4},{phase},{animation.Progress:F5},{motion:F7},{drift:F9},{actor.Busy}"));}
    }
    static TabletopHandSample Frame(string shape,float pinch=.08f,Vector3 delta=default)
    {
        var extension=shape=="fist"?Vector4.one*.55f:shape=="point"?new Vector4(.95f,.55f,.55f,.55f):Vector4.one*.95f;
        var p=anchor+Vector3.up*.05f+delta;
        return TabletopHandSample.Create(0,p,p+Vector3.up*.04f,p+Vector3.up*.04f+Vector3.right*pinch,p-Vector3.up*.02f,extension,Vector3.right,pinch);
    }
    static void Cast(EditDimension dimension)
    {
        controller.ProcessHandSample(default,false);holdValid=false;eventCount=0;traceIndex=0;sampleAt=Time.unscaledTime;
        lighter=new TabletopGestureRules();expectedDimension=dimension;trace=new List<TabletopHandSample>{Frame("open"),Frame("open")};
        if(dimension==EditDimension.Appearance){for(int n=1;n<=8;n++)trace.Add(Frame("open",.02f));trace.Add(Frame("open"));}
        else if(dimension==EditDimension.Agency){for(int n=0;n<=32;n++){float a=n/32f*Mathf.PI*2;trace.Add(Frame("point",.08f,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.04f));}}
        // Current production burst window is .04-.55s; the legacy 45-frame
        // hold exceeds it. Four actual-time samples exercise the short burst.
        else{for(int n=1;n<=4;n++)trace.Add(Frame("fist",.02f));trace.Add(Frame("open"));}
    }
    static void Pump()
    {
        try
        {
            if(!controller)return;
            if(trace!=null)
            {
                if(Time.unscaledTime<sampleAt)return;
                var f=trace[traceIndex++];f.gesture.time=Time.unscaledTime;sampleAt=Time.unscaledTime+.03f;
                if(traceIndex==1)traceStarted=f.gesture.time;
                var expected=traceIndex==1?EditDimension.None:lighter.Step(f.gesture);var actual=controller.ProcessHandSample(f,true);
                joints.Add(FormattableString.Invariant($"{expectedDimension},{f.gesture.time:F5},{f.gesture.pinch:F4},{expected},{actual},{lighter.Status}"));
                Require(expected==actual,"Bronze recognition differs from shared lighter rules: "+expected+"/"+actual);
                if(actual!=EditDimension.None){eventCount++;casts++;Require(actual==expectedDimension&&controller.LastMagicAccepted,"Wrong/rejected spell: "+controller.InteractionStatus);if(actual==EditDimension.Agency)acceptedAt=Time.unscaledTime;}
                held=f;holdValid=true;
                if(traceIndex==trace.Count){Require(eventCount==1,"Expected exactly one "+expectedDimension+" event; got "+eventCount+" in "+(f.gesture.time-traceStarted)+"s, rule="+lighter.Status);trace=null;next=EditorApplication.timeSinceStartup+1.2;}
            }
            else if(holdValid){held.gesture.time=Time.unscaledTime;Require(controller.ProcessHandSample(held,true)==EditDimension.None,"Held hand repeated B");}
        }
        catch(Exception error){Fail(error);}
    }
    static void Capture(string name)
    {
        var camera=new GameObject("Synthetic bronze playback review camera").AddComponent<Camera>();
        camera.orthographic=true;camera.orthographicSize=.34f;camera.nearClipPlane=.01f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.65f,.68f,.7f);
        var target=actor.ConfirmedBottomWorld+Vector3.up*.20f;camera.transform.position=target+new Vector3(.7f,.25f,-1);camera.transform.LookAt(target);
        var rt=new RenderTexture(800,800,24);var pixels=new Texture2D(800,800,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,800,800),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());}
        finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying){if(SessionState.GetBool(Key+".Done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}return;}
        try
        {
            if(phase==0){started=EditorApplication.timeSinceStartup;new GameObject("Synthetic bronze joint driver").AddComponent<HciSeasonTreeHandPump>().sample=Pump;Later(1);return;}
            Require(errors==0,"Runtime error; inspect bounded log");Require(EditorApplication.timeSinceStartup-started<150,"Playback timeout");
            if(watching)Watch();
            if(phase==6&&animation&&animation.Progress>.4f&&!peakCaptured){Require(actor.Busy&&actor.StatueCartoon&&garden.Progress>.99f,"B changed A/D or released busy early");Capture("02-live-stretch");peakCaptured=true;}
            if((phase==8||phase==10||phase==12)&&animation&&animation.Progress>.3f&&actor.Busy)
            {
                watching=false;trace=null;holdValid=false;controller.ProcessHandSample(default,false);
                if(phase==8){Act(ParkAction.Stop);Later(9);}
                else if(phase==10){Act(ParkAction.Reset);Later(11);}
                else{actor.SetTracked(false);Require(animation.Progress==0,"Lost target retained procedural pose");Later(13);}
                return;
            }
            if(trace!=null||EditorApplication.timeSinceStartup<next)return;
            if(phase==1){controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();Require(controller&&controller.scenario==ParkScenario.Statue,"Wrong saved scene");controller.simulation=true;controller.ResetScene();controller.SpawnVirtual("BronzeBeast");Later(2);}
            else if(phase==2)
            {
                actor=controller.selected;Require(actor&&actor.species=="BronzeBeast","Wrong variant");anchor=actor.transform.position;
                animation=actor.GetComponentInChildren<FullBodyBronzeBeast>();Require(animation&&animation.FootCount==4&&animation.Skin.bones.Length==18,"Missing production full-body rig");
                bones=animation.Skin.bones;restPositions=bones.Select(b=>b.localPosition).ToArray();restRotations=bones.Select(b=>b.localRotation).ToArray();
                fit=actor.transform.Find("Manual fit - separate from tracking and animation");Fit();garden=actor.GetComponentInChildren<StatueWishGarden>(true);Require(garden,"Missing production wish garden");
                Capture("00-idle");Cast(EditDimension.Appearance);Later(3);
            }
            else if(phase==3){Require(actor.StatueCartoon,"A failed");holdValid=false;controller.ProcessHandSample(default,false);Require(controller.RequestManualPlane()&&controller.ConfirmManualPlane(anchor,true),"Ground confirmation failed");Cast(EditDimension.Deconstruction);Later(4);}
            else if(phase==4){if(actor.Busy||garden.Progress<.99f)return;Capture("01-before-B");watching=true;Cast(EditDimension.Agency);Later(6);}
            else if(phase==6)
            {
                if(actor.Busy)return;firstCycleSeconds=Time.unscaledTime-acceptedAt;
                Require(peakCaptured&&maxMotion>.01f&&samples>30&&firstCycleSeconds>=animation.Duration-.1f,"No complete real-time stretch");
                Rest();Capture("03-returned");Require(actor.StatueCartoon&&garden.Progress>.99f,"B changed existing A/D");Act(ParkAction.Undo);Later(7);
            }
            else if(phase==7){Rest();Require(actor.StatueCartoon&&garden.Progress>.99f,"Undo removed earlier A/D");Cast(EditDimension.Agency);Later(8,12);}
            else if(phase==8||phase==10||phase==12){if(actor.Busy)return;throw new Exception("Missed live interruption window");}
            else if(phase==9){Rest();Require(!actor.Busy&&garden.Progress<.01f,"Stop cleanup failed");Capture("04-stopped");Cast(EditDimension.Agency);Later(10,12);}
            else if(phase==11){Rest();Require(!actor.Busy&&!actor.StatueCartoon&&!actor.confirmedPlane,"Reset cleanup failed");Fit();Cast(EditDimension.Agency);Later(12,12);}
            else if(phase==13){Rest();Require(!actor.visual.gameObject.activeInHierarchy&&!controller.Dispatch(ParkAction.Awaken,anchor,true),"Lost target accepted B");actor.SetTracked(true);Fit();watching=true;Cast(EditDimension.Agency);Later(14);}
            else if(phase==14){if(actor.Busy)return;Rest();Capture("05-retracked-return");watching=false;trace=null;holdValid=false;controller.ProcessHandSample(default,false);Require(controller.SelectVirtualStatue("Statue"),"Variant cleanup switch failed");Later(15);}
            else if(phase==15)
            {
                Require(!actor&&!animation,"Old bronze actor/rig retained after variant switch");Require(controller.selected&&controller.selected.species=="Statue"&&controller.selected.GetComponentInChildren<NaturalStatueStretch>(),"Human variant not restored");
                File.WriteAllLines(Output+"/trajectory.csv",records);
                File.WriteAllLines(Output+"/joint-trace.csv",joints);
                File.WriteAllText(Output+"/result.txt",FormattableString.Invariant($"PASS: saved ParkStatue BronzeBeast; synthetic production A/B/D traces each match shared rules once; {casts} accepted spells; live B coroutine observed {samples} frames, first cycle {firstCycleSeconds:F3}s, head motion {maxMotion:F6}m, max four-foot drift {maxFootDrift:F9}m; root/fit stable; A/D preserved; Undo; mid-play Stop/Reset/loss; retrack; variant cleanup; six real live-state renders. Not live YOLO, physical hand, real audio, Android or PICO acceptance.\n"));
                SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();
            }
        }
        catch(Exception error){Fail(error);}
    }
}
#endif
