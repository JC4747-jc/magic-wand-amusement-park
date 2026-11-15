using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class HciSharedGestureChecks
{
    const string Key="MagicMR.SharedGestureCheck";
    static string Output=>Path.GetFullPath(".codex-tmp/hci-shared-gestures");
    static readonly string[] Profiles={"tree","bugs","statue","room","plant","pet"};
    static ParkController controller;
    static int phase,profile,errors;
    static double started,next;
    static float clock;
    static readonly List<string> checks=new List<string>();
    static HciSharedGestureChecks(){EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Exception||t==LogType.Error||t==LogType.Assert))errors++;};}
    static void Require(bool ok,string text){if(!ok)throw new Exception(text);}
    public static void Run(){
        TabletopGestureChecks.Run();
        Require(!TabletopHandSample.TryRead(default,null,Vector3.right,0,out _),"Missing XR hand accepted");
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);
        SessionState.SetString(Key+".Run",Guid.NewGuid().ToString("N"));
        EditorSceneManager.OpenScene("Assets/Scenes/ParkPlant.unity");EditorApplication.EnterPlaymode();
    }
    static TabletopHandSample Frame(float time,string shape,float pinch=.08f,Vector3 delta=default) {
        Vector4 e=shape=="fist"?Vector4.one*.55f:shape=="point"?new Vector4(.95f,.55f,.55f,.55f):Vector4.one*.95f;
        Vector3 p=controller.selected.transform.position+Vector3.up*.05f+delta;
        return TabletopHandSample.Create(time,p,p+Vector3.up*.04f,p+Vector3.up*.04f+Vector3.right*pinch,p-Vector3.up*.02f,e,Vector3.right,pinch);
    }
    static List<TabletopHandSample> Trace(EditDimension dimension) {
        clock+=5;var frames=new List<TabletopHandSample>{Frame(clock,"open")};
        if(dimension==EditDimension.Appearance) {
            for(int n=1;n<=8;n++)frames.Add(Frame(clock+n*.02f,"open",.02f));
            frames.Add(Frame(clock+.18f,"open",.08f));
        } else if(dimension==EditDimension.Agency) {
            for(int n=0;n<=50;n++){float a=n/50f*Mathf.PI*2;frames.Add(Frame(clock+.02f+n*.02f,"point",.08f,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.04f));}
        } else if(dimension==EditDimension.Rule) {
            for(int n=1;n<=12;n++)frames.Add(Frame(clock+n*.02f,"open",.08f,Vector3.right*n*.014f));
        } else {
            // A long fist must remain D; it must not become Park's old Undo.
            for(int n=1;n<=45;n++)frames.Add(Frame(clock+n*.02f,"fist",.02f));
            frames.Add(Frame(clock+.92f,"open"));
        }
        return frames;
    }
    static void Replay(List<TabletopHandSample> frames,EditDimension expected,bool mustAccept) {
        controller.ProcessHandSample(default,false);
        var lighter=new TabletopGestureRules();var expectedEvents=new List<EditDimension>();var actualEvents=new List<EditDimension>();
        bool accepted=false;
        foreach(var f in frames) {
            var a=lighter.Step(f.gesture);var b=controller.ProcessHandSample(f,true);
            if(a!=EditDimension.None)expectedEvents.Add(a);
            if(b!=EditDimension.None){actualEvents.Add(b);accepted|=controller.LastMagicAccepted;}
        }
        Require(expectedEvents.SequenceEqual(actualEvents),Profiles[profile]+": lighter/Park classifications differ");
        Require(expected==EditDimension.None?actualEvents.Count==0:actualEvents.SequenceEqual(new[]{expected}),"Wrong or repeated gesture: "+expected);
        if(mustAccept)Require(accepted,Profiles[profile]+": mapped action rejected: "+controller.InteractionStatus);
        checks.Add(Profiles[profile]+" "+expected+" matched lighter; accepted="+accepted);
    }
    static void Safety(){
        var trace=Trace(EditDimension.Appearance);
        controller.SetRightHandUiCapture(true);
        foreach(var f in trace)Require(controller.ProcessMagicSample(f,true)==EditDimension.None,"UI capture leaked cast");
        controller.SetRightHandUiCapture(false);
        controller.selected.SetTracked(false);
        foreach(var f in trace)Require(controller.ProcessMagicSample(f,true)==EditDimension.None,"Lost target cast");
        controller.selected.SetTracked(true);
        foreach(var f in trace)Require(controller.ProcessMagicSample(f,false)==EditDimension.None,"Stale hand cast");
        var far=trace.Select(f=>{f.gesture.palm+=Vector3.right*5;f.gesture.index+=Vector3.right*5;f.thumb+=Vector3.right*5;return f;}).ToList();
        controller.ProcessMagicSample(default,false);
        foreach(var f in far)Require(controller.ProcessMagicSample(f,true)==EditDimension.None,"Far hand cast");
        Require(controller.RequestFit(),"Fit could not begin");clock+=5;
        controller.ProcessHandSample(Frame(clock,"open"),true);
        controller.ProcessHandSample(Frame(clock+.02f,"open",.02f),true);
        for(int n=2;n<=9;n++)controller.ProcessHandSample(Frame(clock+n*.02f,"open",.02f,Vector3.up*.15f),true);
        Require(controller.FitConfirmationPending,"One held pinch confirmed both fit points");
        controller.ProcessHandSample(Frame(clock+.2f,"open",.08f,Vector3.up*.15f),true);
        controller.ProcessHandSample(Frame(clock+.22f,"open",.02f,Vector3.up*.15f),true);
        Require(!controller.FitConfirmationPending&&controller.selected.sizeConfirmed,"Separate pinch did not confirm fit top");
        controller.ProcessHandSample(Frame(clock+.24f,"open"),true);
        controller.RequestManualPlane();
        foreach(var f in trace)Require(controller.ProcessMagicSample(f,true)==EditDimension.None,"Plane capture leaked cast");
        Require(controller.ConfirmManualPlane(controller.selected.transform.position,true),"Fixture plane confirmation failed");
        var still=new List<TabletopHandSample>();clock+=5;
        for(int n=0;n<=50;n++)still.Add(Frame(clock+n*.02f,"open"));
        Replay(still,EditDimension.None,false);
        var vertical=new List<TabletopHandSample>();clock+=5;
        for(int n=0;n<=12;n++)vertical.Add(Frame(clock+n*.02f,"open",.08f,Vector3.up*n*.014f));
        Replay(vertical,EditDimension.None,false);
        var gap=Trace(EditDimension.Appearance);gap.RemoveRange(2,6);var last=gap[gap.Count-1];last.gesture.time+=.3f;gap[gap.Count-1]=last;
        Replay(gap,EditDimension.None,false);
        checks.Add(Profiles[profile]+" UI/plane/lost/stale/far gates; separate Fit pinch edges; no stationary/vertical extra gesture; gap cancels pinch");
    }
    static void Tick(){
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying){if(SessionState.GetBool(Key+".Done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}return;}
        try {
            if(phase==0){started=EditorApplication.timeSinceStartup;controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();Require(controller,"Saved scene controller missing");Camera.main.GetComponentsInChildren<Button>(false).Single(b=>b.name=="Virtual test").onClick.Invoke();phase=1;next=EditorApplication.timeSinceStartup+1;return;}
            Require(errors==0,"Runtime error");Require(EditorApplication.timeSinceStartup-started<120,"Timeout");
            if(EditorApplication.timeSinceStartup<next)return;
            if(phase==1){Require(controller.SwitchContent(Profiles[profile],null),"Content switch blocked");controller.SpawnVirtual();phase=2;}
            else if(phase==2){Safety();Replay(Trace(EditDimension.Appearance),EditDimension.Appearance,true);phase=3;}
            else if(phase==3){Replay(Trace(EditDimension.Agency),EditDimension.Agency,true);phase=4;}
            else if(phase==4){Replay(Trace(EditDimension.Rule),EditDimension.Rule,true);phase=5;}
            else if(phase==5){Replay(Trace(EditDimension.Deconstruction),EditDimension.Deconstruction,profile!=3);phase=6;}
            else if(phase==6){
                if(++profile<Profiles.Length){phase=1;}
                else {
                    File.WriteAllText(Output+"/result.txt","PASS "+SessionState.GetString(Key+".Run","")+"\n"+string.Join("\n",checks)+"\nActual saved ParkPlant controller with six registered content prefabs; synthetic joint-derived trajectories through live ProcessHandSample (selection/calibration/recognition) and lighter rules. Real XR provider, YOLO recognition, room geometry and PICO not validated.\n");
                    SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();return;
                }
            }
            next=EditorApplication.timeSinceStartup+1.5;
        } catch(Exception e){SessionState.SetBool(Key,false);File.WriteAllText(Output+"/result.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
