using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class HciStatuePlaybackChecks
{
    const string Key="MagicMR.HciStatuePlayback";
    static string Output=>Path.GetFullPath(".codex-tmp/hci-statue-playback");
    static ParkController controller;static ParkActor actor;static NaturalStatueStretch animation;static StatueWishGarden garden;
    static Transform fit,hand,footL,footR,pelvis;static Vector3 anchor,fitPosition,fitScale,restHand,restFootL,restFootR,restPelvis;
    static Transform[] bones;static Quaternion[] restRotations;
    static int phase,errors,eventCount,traceIndex,samples;static int observedFrame=-1;static double started,next;static float maxRise,lastRecorded;
    static bool watching,peakCaptured;
    static readonly List<string> records=new List<string>{"time,progress,leftHandRise,leftFootDrift,rightFootDrift,pelvisDrift,busy"};
    static List<TabletopHandSample> trace;static TabletopGestureRules lighter;
    static EditDimension expectedDimension;static TabletopHandSample held;static bool holdValid;static float sampleAt;
    static HciStatuePlaybackChecks(){EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};}
    public static void Run(){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);EditorSceneManager.OpenScene("Assets/Scenes/ParkStatue.unity");EditorApplication.EnterPlaymode();}
    static void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
    static void Fail(Exception e){File.WriteAllText(Output+"/result.txt","FAIL: "+e);SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(2);}
    static void Later(int value,double delay=1.2){phase=value;next=EditorApplication.timeSinceStartup+delay;}
    static void Act(ParkAction action)=>Require(controller.Dispatch(action,anchor,true),action+" rejected: "+controller.InteractionStatus);
    static void Rest(){for(int i=0;i<bones.Length;i++)Require(Quaternion.Angle(bones[i].localRotation,restRotations[i])<.1f,"Pose did not return: "+bones[i].name);}
    static void Fit(){Require(actor.ConfirmOrientation(0)&&actor.FitRepresentation(anchor,anchor+Vector3.up*.4f,true),"Fit failed");fitPosition=fit.localPosition;fitScale=fit.localScale;restHand=hand.position;restFootL=footL.position;restFootR=footR.position;restPelvis=pelvis.position;}
    static void Watch(){
        if(observedFrame==Time.frameCount)return;observedFrame=Time.frameCount;
        Require(actor.transform.position==anchor&&fit.localPosition==fitPosition&&fit.localScale==fitScale,"Animation moved tracking or fit");
        float l=Vector3.Distance(footL.position,restFootL),r=Vector3.Distance(footR.position,restFootR),p=Vector3.Distance(pelvis.position,restPelvis);
        Require(l<.0001f&&r<.0001f&&p<.0001f,"Foot or pelvis drift");maxRise=Mathf.Max(maxRise,hand.position.y-restHand.y);samples++;
        if(Time.unscaledTime-lastRecorded>.05f){lastRecorded=Time.unscaledTime;records.Add(FormattableString.Invariant($"{Time.unscaledTime:F4},{animation.Progress:F4},{hand.position.y-restHand.y:F6},{l:F7},{r:F7},{p:F7},{actor.Busy}"));}
    }
    static TabletopHandSample Frame(string shape,float pinch=.08f,Vector3 delta=default){var extension=shape=="fist"?Vector4.one*.55f:shape=="point"?new Vector4(.95f,.55f,.55f,.55f):Vector4.one*.95f;var p=anchor+Vector3.up*.05f+delta;return TabletopHandSample.Create(0,p,p+Vector3.up*.04f,p+Vector3.up*.04f+Vector3.right*pinch,p-Vector3.up*.02f,extension,Vector3.right,pinch);}
    static void Cast(EditDimension dimension){
        controller.ProcessHandSample(default,false);holdValid=false;eventCount=0;traceIndex=0;sampleAt=Time.unscaledTime;lighter=new TabletopGestureRules();expectedDimension=dimension;trace=new List<TabletopHandSample>{Frame("open"),Frame("open")};
        if(dimension==EditDimension.Appearance){for(int n=1;n<=8;n++)trace.Add(Frame("open",.02f));trace.Add(Frame("open"));}
        else if(dimension==EditDimension.Agency){for(int n=0;n<=32;n++){float a=n/32f*Mathf.PI*2;trace.Add(Frame("point",.08f,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.04f));}}
        else if(dimension==EditDimension.Rule){for(int n=1;n<=6;n++)trace.Add(Frame("open",.08f,Vector3.right*n*.03f));}
        else{for(int n=1;n<=45;n++)trace.Add(Frame("fist",.02f));trace.Add(Frame("open"));}
    }
    static void Pump(){try{
        if(!controller)return;
        if(trace!=null){if(Time.unscaledTime<sampleAt)return;var f=trace[traceIndex++];f.gesture.time=Time.unscaledTime;sampleAt=Time.unscaledTime+.03f;
            var expected=traceIndex==1?EditDimension.None:lighter.Step(f.gesture);var actual=controller.ProcessHandSample(f,true);
            Require(expected==actual,"Statue recognition differs from lighter: "+expected+"/"+actual+" "+controller.InteractionStatus);
            if(actual!=EditDimension.None){eventCount++;Require(actual==expectedDimension&&controller.LastMagicAccepted,"Wrong/rejected spell");}
            held=f;holdValid=true;if(traceIndex==trace.Count){Require(eventCount==1,"Expected one gesture event, got "+eventCount);trace=null;next=EditorApplication.timeSinceStartup+1.2;}
        }else if(holdValid){held.gesture.time=Time.unscaledTime;Require(controller.ProcessHandSample(held,true)==EditDimension.None,"Held hand repeated spell");}
    }catch(Exception e){Fail(e);}}
    static Color32[] Capture(string name){
        var camera=new GameObject("Synthetic statue review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.32f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.65f,.68f,.70f);camera.nearClipPlane=.01f;
        var target=actor.ConfirmedBottomWorld+Vector3.up*.20f;camera.transform.position=target+new Vector3(.7f,.25f,-1);camera.transform.LookAt(target);
        var rt=new RenderTexture(800,800,24);var pixels=new Texture2D(800,800,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,800,800),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+actor.species+"-"+name+".png",pixels.EncodeToPNG());return pixels.GetPixels32();}
        finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
    }
    static void Tick(){
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying){if(SessionState.GetBool(Key+".Done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}return;}
        try{
            if(phase==0){started=EditorApplication.timeSinceStartup;new GameObject("Synthetic statue joint driver").AddComponent<HciSeasonTreeHandPump>().sample=Pump;Later(1);return;}
            Require(errors==0,"Runtime error");Require(EditorApplication.timeSinceStartup-started<150,"Timeout");
            if(watching)Watch();
            if(phase==6&&animation&&animation.Progress>.4f&&!peakCaptured){Require(actor.Busy&&actor.StatueCartoon&&garden.Progress>.99f,"B overwrote A/D or released busy early");Capture("02-stretch");peakCaptured=true;}
            if((phase==8||phase==10||phase==12)&&animation&&animation.Progress>.3f&&actor.Busy){
                Require(animation.GraphValid,"Missing live graph");watching=false;trace=null;holdValid=false;controller.ProcessHandSample(default,false);
                if(phase==8){Act(ParkAction.Stop);Later(9);}
                else if(phase==10){Act(ParkAction.Reset);Later(11);}
                else{actor.SetTracked(false);Require(!animation.GraphValid,"Lost target retained graph");Later(13);}
                return;
            }
            if(trace!=null||EditorApplication.timeSinceStartup<next)return;
            if(phase==1){controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();Require(controller&&controller.scenario==ParkScenario.Statue,"Wrong saved scene");controller.simulation=true;controller.SpawnVirtual();Later(2);}
            else if(phase==2){
                actor=controller.selected;Require(actor&&actor.species=="Statue","Wrong statue");anchor=actor.transform.position;animation=actor.GetComponentInChildren<NaturalStatueStretch>();Require(animation&&animation.GraphValid,"Standing rig not initialized");
                Require(animation.GetComponentsInChildren<SkinnedMeshRenderer>().Sum(r=>(int)r.sharedMesh.GetIndexCount(0)/3)==12950,"Unexpected production geometry");Require(!animation.GetComponentInChildren<Animator>().applyRootMotion,"Root motion enabled");
                var transforms=animation.GetComponentsInChildren<Transform>();hand=transforms.Single(t=>t.name=="hand_l");footL=transforms.Single(t=>t.name=="foot_l");footR=transforms.Single(t=>t.name=="foot_r");pelvis=transforms.Single(t=>t.name=="pelvis");bones=transforms;restRotations=bones.Select(t=>t.localRotation).ToArray();
                fit=actor.transform.Find("Manual fit - separate from tracking and animation");Fit();garden=actor.GetComponentInChildren<StatueWishGarden>(true);Capture("00-idle");Cast(EditDimension.Appearance);Later(3);
            }
            else if(phase==3){Require(actor.StatueCartoon,"A failed");holdValid=false;controller.ProcessHandSample(default,false);Require(controller.RequestManualPlane()&&controller.ConfirmManualPlane(anchor,true),"Ground failed");Cast(EditDimension.Deconstruction);Later(4);}
            else if(phase==4){if(actor.Busy||garden.Progress<.99f)return;Capture("01-before-stretch");watching=true;Cast(EditDimension.Agency);Later(6);}
            else if(phase==6){if(actor.Busy)return;Require(peakCaptured&&maxRise>.12f&&samples>30,"No full live stretch: "+maxRise);Rest();Capture("03-returned");Require(actor.StatueCartoon&&garden.Progress>.99f,"B changed A/D");Act(ParkAction.Undo);Later(7);}
            else if(phase==7){Rest();Require(actor.StatueCartoon&&garden.Progress>.99f,"Undo removed prior A/D");Cast(EditDimension.Agency);Later(8,12);}
            else if(phase==8||phase==10||phase==12){if(actor.Busy)return;throw new Exception("Missed mid-animation interruption window");}
            else if(phase==9){Rest();Require(!actor.Busy&&garden.Progress<.01f,"Stop failed");Capture("04-stopped");Cast(EditDimension.Agency);Later(10,12);}
            else if(phase==11){Rest();Require(!actor.Busy&&!actor.StatueCartoon&&!actor.confirmedPlane,"Reset failed");Fit();Cast(EditDimension.Agency);Later(12,12);}
            else if(phase==13){Rest();Require(!actor.visual.gameObject.activeInHierarchy&&!controller.Dispatch(ParkAction.Awaken,anchor,true),"Lost target accepted B");actor.SetTracked(true);Fit();Cast(EditDimension.Agency);Later(14);}
            else if(phase==14){if(actor.Busy)return;Require(animation.GraphValid,"Graph not recreated on retrack");Rest();Capture("05-retracked-return");holdValid=false;controller.ProcessHandSample(default,false);Require(controller.SelectVirtualStatue("BronzeBeast"),"Variant switch failed");Later(15);}
            else if(phase==15){Require(!actor,"Old actor not destroyed");Require(controller.selected&&controller.selected.species=="BronzeBeast"&&!controller.selected.GetComponentInChildren<NaturalStatueStretch>(),"Bronze was replaced by human rig");
                File.WriteAllLines(Output+"/trajectory.csv",records);File.WriteAllText(Output+"/result.txt",FormattableString.Invariant($"PASS: saved ParkStatue full-body human; production synthetic A/B/D hand traces match lighter once; live PlayableGraph stretch and return; {samples} observed frames, hand rise {maxRise:F3}m at 0.4m fit; feet/pelvis <0.1mm drift; root/fit unchanged; A/D preserved; Undo; mid-play Stop/Reset/tracking-loss; graph teardown/recreate; variant switch cleanup; six live camera renders; zero runtime errors. Bronze full-body animation, real YOLO/PICO and Android remain pending.\n"));SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();}
        }catch(Exception e){Fail(e);}
    }
}
