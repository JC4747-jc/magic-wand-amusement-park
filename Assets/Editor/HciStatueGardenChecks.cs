using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class HciStatueGardenChecks
{
    const string Key="MagicMR.HciStatueGarden";
    static string Output=>Path.GetFullPath(".codex-tmp/hci-statue-garden");
    static ParkController controller;static ParkActor actor;static StatueWishGarden garden;
    static Vector3 anchor,ground;static Transform fit;static Vector3 fitPosition,fitScale;
    static int phase,species,errors,eventCount,traceIndex,vertices,triangles;static double started,next;
    static Quaternion ribbonRotation;static bool openingCaptured;static Material material;static Mesh[] meshes;
    static List<TabletopHandSample> trace;static TabletopGestureRules lighter;
    static EditDimension expectedDimension;static TabletopHandSample held;static bool holdValid;static float sampleAt;
    static HciStatueGardenChecks(){EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};}
    public static void Run(){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);EditorSceneManager.OpenScene("Assets/Scenes/ParkStatue.unity");EditorApplication.EnterPlaymode();}
    static void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
    static void Fail(Exception e){File.WriteAllText(Output+"/result.txt","FAIL: "+e);SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(2);}
    static void Later(int value,double delay=1.5){phase=value;next=EditorApplication.timeSinceStartup+delay;}
    static void Act(ParkAction action)=>Require(controller.Dispatch(action,anchor,true),action+" rejected: "+controller.InteractionStatus);
    static void Ground(){holdValid=false;controller.ProcessHandSample(default,false);Require(controller.RequestManualPlane(),"Request plane failed");Require(!controller.ConfirmManualPlane(new Vector3(float.NaN,0,0),true),"NaN plane accepted");Require(controller.ConfirmManualPlane(ground,true),"Manual plane failed");}
    static void Open(){Require(garden.gameObject.activeInHierarchy&&garden.Progress>.99f,"Garden not open: "+garden.Progress+" active="+garden.gameObject.activeInHierarchy);Require(garden.transform.position==ground,"Garden moved from confirmed ground");Require(actor.transform.position==anchor&&fit.localPosition==fitPosition&&fit.localScale==fitScale,"Garden moved tracked object/fit");Require(garden.VertexCount==vertices&&garden.TriangleCount==triangles,"Geometry grew");Require(garden.GetComponentsInChildren<MeshFilter>(true).Select(m=>m.sharedMesh).Distinct().SequenceEqual(meshes),"Meshes replaced");Require(garden.GetComponentsInChildren<Renderer>(true).All(m=>m.sharedMaterial==material),"Materials duplicated");}
    static void Closed(){Require(!garden.gameObject.activeInHierarchy&&garden.Progress==0,"Garden not cleared");}
    static TabletopHandSample Frame(string shape,float pinch=.08f,Vector3 delta=default){var extension=shape=="fist"?Vector4.one*.55f:shape=="point"?new Vector4(.95f,.55f,.55f,.55f):Vector4.one*.95f;var p=anchor+Vector3.up*.05f+delta;return TabletopHandSample.Create(0,p,p+Vector3.up*.04f,p+Vector3.up*.04f+Vector3.right*pinch,p-Vector3.up*.02f,extension,Vector3.right,pinch);}
    const float SampleCadenceSeconds=.03f;
    static int plannedFistSamples;static float fistBeganAt=-1;
    static void Cast(EditDimension dimension){
        controller.ProcessHandSample(default,false);holdValid=false;eventCount=0;traceIndex=0;fistBeganAt=-1;sampleAt=Time.unscaledTime;lighter=new TabletopGestureRules();expectedDimension=dimension;trace=new List<TabletopHandSample>{Frame("open"),Frame("open")};
        if(dimension==EditDimension.Appearance){for(int n=1;n<=8;n++)trace.Add(Frame("open",.02f));trace.Add(Frame("open"));}
        else if(dimension==EditDimension.Agency){for(int n=0;n<=32;n++){float a=n/32f*Mathf.PI*2;trace.Add(Frame("point",.08f,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.04f));}}
        else if(dimension==EditDimension.Rule){for(int n=1;n<=6;n++)trace.Add(Frame("open",.08f,Vector3.right*n*.03f));}
        else{plannedFistSamples=Mathf.CeilToInt(TabletopGestureRules.FistMinimumSeconds/SampleCadenceSeconds)+1;Require((plannedFistSamples+1)*SampleCadenceSeconds<TabletopGestureRules.FistMaximumSeconds,"No safe synthetic D sampling window");for(int n=0;n<plannedFistSamples;n++)trace.Add(Frame("fist",.02f));trace.Add(Frame("open"));}
    }
    static void Pump(){try{
        if(!controller)return;
        if(trace!=null){if(Time.unscaledTime<sampleAt)return;var f=trace[traceIndex++];f.gesture.time=Time.unscaledTime;sampleAt=Time.unscaledTime+SampleCadenceSeconds;if(expectedDimension==EditDimension.Deconstruction&&traceIndex==3)fistBeganAt=f.gesture.time;
            var expected=traceIndex==1?EditDimension.None:lighter.Step(f.gesture);var actual=controller.ProcessHandSample(f,true);
            Require(expected==actual,"Statue recognition differs from lighter: "+expected+"/"+actual+" "+controller.InteractionStatus);
            if(actual!=EditDimension.None){eventCount++;Require(actual==expectedDimension&&controller.LastMagicAccepted,"Wrong/rejected spell");}
            held=f;holdValid=true;if(traceIndex==trace.Count){if(expectedDimension==EditDimension.Deconstruction){float duration=f.gesture.time-fistBeganAt;Require(fistBeganAt>=0&&duration>=TabletopGestureRules.FistMinimumSeconds&&duration<=TabletopGestureRules.FistMaximumSeconds,"Synthetic D missed production window");File.AppendAllText(Output+"/gesture-timing.txt",FormattableString.Invariant($"{actor.species}: D fist_seconds={duration:R} samples={plannedFistSamples} events={eventCount}\n"));}Require(eventCount==1,"Expected one gesture event, got "+eventCount);trace=null;next=EditorApplication.timeSinceStartup+2.2;}
        }else if(holdValid){held.gesture.time=Time.unscaledTime;Require(controller.ProcessHandSample(held,true)==EditDimension.None,"Held hand repeated spell");}
    }catch(Exception e){Fail(e);}}
    static void Capture(string name,bool detail=false){
        var camera=new GameObject("Synthetic garden review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=detail?.18f:.78f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.53f,.57f,.58f);camera.nearClipPlane=.01f;
        var target=detail?garden.transform.Find("Lotus 0").position+Vector3.up*.04f:ground+Vector3.up*.28f;
        camera.transform.position=target+(detail?new Vector3(.4f,.4f,-.6f):new Vector3(.8f,.65f,-1.5f));camera.transform.LookAt(target);
        var rt=new RenderTexture(960,960,24);var pixels=new Texture2D(960,960,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,960,960),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+actor.species+"-"+name+".png",pixels.EncodeToPNG());}
        finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
    }
    static void Tick(){
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying){if(SessionState.GetBool(Key+".Done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}return;}
        try{
            if(phase==0){started=EditorApplication.timeSinceStartup;new GameObject("Synthetic garden joint driver").AddComponent<HciSeasonTreeHandPump>().sample=Pump;Later(1);return;}
            Require(errors==0,"Runtime error");Require(EditorApplication.timeSinceStartup-started<150,"Timeout");
            if(garden&&!openingCaptured&&garden.Progress>.15f&&garden.Progress<.6f){Capture("01-opening");openingCaptured=true;}
            if(phase==14&&trace==null&&garden.Progress>.1f&&garden.Progress<.6f){Act(ParkAction.Stop);Later(15);return;}
            if(trace!=null||EditorApplication.timeSinceStartup<next)return;
            if(phase==1){controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();Require(controller&&controller.scenario==ParkScenario.Statue,"Saved scene missing");controller.simulation=true;controller.SpawnVirtual();Later(2);}
            else if(phase==2){actor=controller.selected;Require(actor&&actor.species==(species==0?"Statue":"BronzeBeast"),"Wrong variant");anchor=actor.transform.position;ground=anchor+new Vector3(.05f,-.1f,.05f);
                Require(actor.ConfirmOrientation(0)&&actor.FitRepresentation(anchor,anchor+Vector3.up*.4f,true),"Fit failed");fit=actor.transform.Find("Manual fit - separate from tracking and animation");fitPosition=fit.localPosition;fitScale=fit.localScale;
                garden=actor.GetComponentInChildren<StatueWishGarden>(true);Require(garden,"Missing garden");Closed();vertices=garden.VertexCount;triangles=garden.TriangleCount;Require(vertices<3000&&triangles<5000&&garden.FlowerCount==6&&garden.CardCount==7,"Geometry budget/content mismatch");meshes=garden.GetComponentsInChildren<MeshFilter>(true).Select(m=>m.sharedMesh).Distinct().ToArray();Require(meshes.Length==3,"Shared mesh count");material=garden.GetComponentInChildren<Renderer>(true).sharedMaterial;Require(!ShaderUtil.ShaderHasError(material.shader),"Shader error");Require(garden.GetComponentsInChildren<Collider>(true).Length==0,"Virtual garden blocks interaction ray");
                Require(!controller.Dispatch(ParkAction.Reward,anchor,true),"D accepted without ground");Ground();Cast(EditDimension.Appearance);openingCaptured=false;Later(3);}
            else if(phase==3){Require(actor.StatueCartoon,"A failed");Cast(EditDimension.Deconstruction);Later(4);}
            else if(phase==4){Open();Require(openingCaptured&&actor.StatueCartoon,"No progressive growth or D changed A");Capture("02-open");Capture("03-lotus-detail",true);ribbonRotation=garden.transform.Find("Wish tree growth/Wish ribbon hinge 0").localRotation;var property=new MaterialPropertyBlock();foreach(var renderer in garden.GetComponentsInChildren<Renderer>()){renderer.GetPropertyBlock(property);if(renderer.name.StartsWith("Lotus"))Require(property.GetFloat("_Bloom")>.99f,"Petal morph not open");}File.WriteAllText(Output+"/"+actor.species+"-geometry.txt","Unique meshes: "+meshes.Length+"; unique vertices: "+vertices+"; unique triangles: "+triangles+"; renderers: "+garden.GetComponentsInChildren<Renderer>().Length+"\n");Act(ParkAction.Undo);Later(5,2);}
            else if(phase==5){Closed();Require(actor.StatueCartoon,"Undo changed A");Cast(EditDimension.Deconstruction);Later(6);}
            else if(phase==6){Open();Require(Quaternion.Angle(ribbonRotation,garden.transform.Find("Wish tree growth/Wish ribbon hinge 0").localRotation)>.01f,"Wish ribbon did not sway");Act(ParkAction.Stop);Later(7);}
            else if(phase==7){Closed();Cast(EditDimension.Deconstruction);Later(8);}
            else if(phase==8){Open();holdValid=false;controller.ProcessHandSample(default,false);actor.confirmedPlane=false;Later(9);}
            else if(phase==9){Closed();Act(ParkAction.Reset);Later(10);}
            else if(phase==10){Closed();Require(!actor.StatueCartoon&&!actor.confirmedPlane&&!actor.environmentAnchor,"Reset retained style/surface");Require(actor.ConfirmOrientation(0)&&actor.FitRepresentation(anchor,anchor+Vector3.up*.4f,true),"Refit after reset failed");fitPosition=fit.localPosition;fitScale=fit.localScale;Ground();Cast(EditDimension.Deconstruction);Later(11);}
            else if(phase==11){Open();holdValid=false;controller.ProcessHandSample(default,false);actor.SetTracked(false);Later(12);}
            else if(phase==12){Closed();Require(!controller.Dispatch(ParkAction.Reward,anchor,true),"Lost target accepted D");actor.SetTracked(true);Act(ParkAction.Reset);Later(13);}
            else if(phase==13){Closed();Capture("04-reset");Ground();Cast(EditDimension.Deconstruction);Later(14,8);}
            else if(phase==14){throw new Exception("Missed opening Stop window");}
            else if(phase==15){Closed();Require(!actor.Busy,"Opening Stop left motion");if(species++==0){Require(controller.SelectVirtualStatue("BronzeBeast"),"Bronze switch failed");Later(2);}else{File.WriteAllText(Output+"/result.txt","PASS: saved ParkStatue standing human and bronze; production synthetic A/D joints equal lighter rules exactly once; D requires finite manually confirmed ground; six layered morphing lotus flowers and seven wish ribbons; progressive growth; A unchanged; Undo/Stop (including opening)/Reset/invalid plane/target loss clear garden; three shared meshes and one material reused; wish ribbons sway; no colliders; tracked root and fit unchanged; zero runtime/shader errors; eight camera renders. Editor only, synthetic manual ground is not room reconstruction or real YOLO/PICO acceptance.\n");SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();}}
        }catch(Exception e){Fail(e);}
    }
}
