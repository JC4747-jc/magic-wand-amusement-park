using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class HciStatueStyleChecks
{
    const string Key="MagicMR.HciStatueStyle";
    static string Output=>Path.GetFullPath(".codex-tmp/hci-statue-style");
    static ParkController controller;static ParkActor actor;static NaturalCartoonStyle style;
    static Renderer[] bodies;static Material[][] original;static int owned;
    static Transform fit;static Vector3 anchor,fitPosition,fitScale;
    static int phase,species,errors,eventCount,traceIndex;static double started,next;
    static List<TabletopHandSample> trace;static TabletopGestureRules lighter;
    static EditDimension expectedDimension;static TabletopHandSample held;static bool holdValid;static float sampleAt;
    static HciStatueStyleChecks(){EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};}
    public static void Run(){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);EditorSceneManager.OpenScene("Assets/Scenes/ParkStatue.unity");EditorApplication.EnterPlaymode();}
    static void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
    static void Fail(Exception e){File.WriteAllText(Output+"/result.txt","FAIL: "+e);SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(2);}
    static void Later(int value,double delay=1.2){phase=value;next=EditorApplication.timeSinceStartup+delay;}
    static void Act(ParkAction action)=>Require(controller.Dispatch(action,anchor,true),action+" rejected: "+controller.InteractionStatus);
    static void Check(bool cartoon){
        Require(actor.StatueCartoon==cartoon&&style.Active==cartoon,"Appearance state mismatch");Require(style.OwnedMaterialCount==owned,"Material count grew");
        for(int i=0;i<bodies.Length;i++){var actual=bodies[i].sharedMaterials;Require(actual.Length==original[i].Length,"Material slots changed");for(int j=0;j<actual.Length;j++){
            Require(cartoon?actual[j].shader.name=="MagicMR/StatueCartoon":actual[j]==original[i][j],"Appearance material mismatch");
            Require(actual[j].GetTexture("_BaseMap")==original[i][j].GetTexture("_BaseMap"),"Original albedo lost");
            Require(!ShaderUtil.ShaderHasError(actual[j].shader),"Shader error");
        }}
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
            Require(errors==0,"Runtime error");Require(EditorApplication.timeSinceStartup-started<100,"Timeout");if(trace!=null||EditorApplication.timeSinceStartup<next||(actor&&actor.Busy))return;
            if(phase==1){controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();Require(controller&&controller.scenario==ParkScenario.Statue,"Saved Statue scene missing");controller.simulation=true;controller.SpawnVirtual();Later(2);}
            else if(phase==2){
                actor=controller.selected;Require(actor&&actor.species==(species==0?"Statue":"BronzeBeast"),"Wrong variant");anchor=actor.transform.position;
                Require(actor.ConfirmOrientation(0)&&actor.FitRepresentation(anchor,anchor+Vector3.up*.4f,true),"Fit failed");
                fit=actor.transform.Find("Manual fit - separate from tracking and animation");fitPosition=fit.localPosition;fitScale=fit.localScale;
                style=actor.GetComponentInChildren<NaturalCartoonStyle>();Require(style,"Missing cartoon style");bodies=style.GetComponentsInChildren<Renderer>();original=bodies.Select(b=>b.sharedMaterials).ToArray();owned=style.OwnedMaterialCount;
                Require(owned>0&&owned<=4,"Material budget exceeded");Check(false);Later(3);
            }
            else if(phase==3){Capture("00-original");Require(!controller.Dispatch(ParkAction.Style,anchor,false)&&!controller.Dispatch(ParkAction.Style,anchor+Vector3.right*20,true),"Invalid/far A accepted");Cast(EditDimension.Appearance);Later(4);}
            else if(phase==4){
                Check(true);var toon=Capture("01-cartoon");style.SetActive(false);var plain=Capture("01-same-frame-original");style.SetActive(true);int changed=0;
                for(int i=0;i<toon.Length;i++)if(Mathf.Abs(toon[i].r-plain[i].r)+Mathf.Abs(toon[i].g-plain[i].g)+Mathf.Abs(toon[i].b-plain[i].b)>60)changed++;
                Require(changed>4000,"Cartoon rendering not visibly different: "+changed);File.WriteAllText(Output+"/"+actor.species+"-pixels.txt","Same-frame changed pixels: "+changed+"; minimum 4000\n");
                Require(actor.Feedback.Contains("cartoon"),"A still selects story chapters");Cast(EditDimension.Agency);Later(5);
            }
            else if(phase==5){Check(true);Cast(EditDimension.Rule);Later(6);}
            else if(phase==6){Check(true);Require(actor.Feedback.Contains(actor.storySource)&&actor.Feedback.Contains(actor.story),"C lost story/source");Capture("02-after-story");Cast(EditDimension.Appearance);Later(7);}
            else if(phase==7){Check(false);Act(ParkAction.Undo);Later(8);}
            else if(phase==8){Check(true);Require(actor.transform.position==anchor&&fit.localPosition==fitPosition&&fit.localScale==fitScale,"Style moved fit/anchor");holdValid=false;controller.ProcessHandSample(default,false);actor.SetTracked(false);Require(!actor.visual.gameObject.activeInHierarchy&&!controller.Dispatch(ParkAction.Style,anchor,true),"Lost target accepted A");actor.SetTracked(true);Later(9);}
            else if(phase==9){Check(false);Cast(EditDimension.Appearance);Later(10);}
            else if(phase==10){Check(true);Act(ParkAction.Reset);Later(11);}
            else if(phase==11){Check(false);Require(!actor.Busy,"Reset retained motion");Capture("03-reset");if(species++==0){Require(controller.SelectVirtualStatue("BronzeBeast"),"Bronze switch failed");Later(2);}else{
                File.WriteAllText(Output+"/result.txt","PASS: saved ParkStatue, standing human + bronze; synthetic production A/B/C joint traces match lighter exactly once; A reversible cartoon material rather than story selection; B/C preserve appearance; C retains story/source; original albedo and exact shared material restoration; bounded material count; same-frame visible pixel checks; invalid/far/lost rejection; Undo/Reset; stable fit/anchor; zero runtime errors; ten camera renders. Editor only. Human B full stretching and D lotus/wish-tree lifecycle have separate validation; bronze full-body animation remains pending; no real YOLO/XR/PICO/Android acceptance.\n");SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();}}
        }catch(Exception e){Fail(e);}
    }
}
