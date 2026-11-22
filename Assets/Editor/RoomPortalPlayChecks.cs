using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class RoomPortalPlayChecks
{
    const string Key="MagicMR.RoomPortalChecks";
    static string Output=>Path.GetFullPath(".codex-tmp/room-portal-checks");
    static ParkController controller;static ParkActor actor;static RoomPortalPuzzle portal;
    static int phase,errors,index,events;static double start,next;static float sampleAt;
    static List<TabletopHandSample> trace;static TabletopHandSample held;static bool holding;
    static TabletopGestureRules comparison;static EditDimension expected;static Vector3 anchor;static Matrix4x4 fit;
    static readonly List<string> classifications=new List<string>();
    static RoomPortalPlayChecks(){EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};}
    public static void Run(){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);EditorSceneManager.OpenScene("Assets/Scenes/ParkRoom.unity");EditorApplication.EnterPlaymode();}
    static void Require(bool yes,string why){if(!yes)throw new Exception(why);}
    static void Later(int p,double delay=.5){phase=p;next=EditorApplication.timeSinceStartup+delay;}
    static void Finish(bool pass,string message){File.WriteAllText(Output+"/result.txt",(pass?"PASS: saved ParkRoom portal interaction; ":"FAIL: ")+message+"\n");SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();if(!pass)Debug.LogError(message);}
    static TabletopHandSample Frame(Vector3 p,string shape="open",float pinch=.08f)
    {var extension=shape=="fist"?Vector4.one*.55f:shape=="point"?new Vector4(.95f,.55f,.55f,.55f):Vector4.one*.95f;return TabletopHandSample.Create(0,p-Vector3.up*.04f,p,p+Vector3.right*pinch,p-Vector3.up*.06f,extension,Vector3.right,pinch);}
    static void Cast(EditDimension dimension,Vector3 p){
        controller.ProcessMagicSample(default,false);holding=false;expected=dimension;comparison=new TabletopGestureRules();index=events=0;sampleAt=Time.unscaledTime;
        trace=new List<TabletopHandSample>{Frame(p),Frame(p)};
        if(dimension==EditDimension.Appearance){for(int n=0;n<8;n++)trace.Add(Frame(p,"open",.02f));trace.Add(Frame(p));}
        else if(dimension==EditDimension.Agency){for(int n=0;n<=32;n++){float a=n*Mathf.PI*2/32;trace.Add(Frame(p+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.04f,"point"));}}
        else if(dimension==EditDimension.Rule){for(int n=1;n<=6;n++)trace.Add(Frame(p+Vector3.right*n*.03f));}
        else{int count=Mathf.CeilToInt(TabletopGestureRules.FistMinimumSeconds/.03f)+1;for(int n=0;n<count;n++)trace.Add(Frame(p,"fist",.02f));trace.Add(Frame(p));}
    }
    static void Pump(){try{
        if(!controller)return;
        if(trace!=null){
            if(Time.unscaledTime<sampleAt)return;var f=trace[index++];f.gesture.time=Time.unscaledTime;sampleAt=Time.unscaledTime+.03f;
            var want=index==1?EditDimension.None:comparison.Step(f.gesture);var got=controller.ProcessHandSample(f,true);
            Require(want==got,"Shared classification mismatch "+want+"/"+got);
            if(got!=EditDimension.None){events++;Require(got==expected&&controller.LastMagicAccepted,"Portal action rejected: "+controller.InteractionStatus);}
            held=f;holding=true;
            if(index==trace.Count){Require(events==1,"Repeated/missing portal gesture");classifications.Add(expected+":1");trace=null;next=EditorApplication.timeSinceStartup+1.3;}
        }else if(holding){held.gesture.time=Time.unscaledTime;Require(controller.ProcessHandSample(held,true)==EditDimension.None,"Held hand repeated a cast");}
    }catch(Exception e){Finish(false,e.ToString());}}
    static void Capture(string name,float eye=0){
        var go=new GameObject("Synthetic portal review camera");var camera=go.AddComponent<Camera>();var center=(portal.Entrance.position+portal.Exit.position)*.5f;
        camera.orthographic=false;camera.fieldOfView=40;camera.nearClipPlane=.005f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.05f,.075f);
        float reviewScale=portal.UsesConfirmedSurface?1f:actor.visual.lossyScale.y;
        camera.transform.position=center+actor.visual.TransformDirection(new Vector3(eye,.045f,-.65f))*reviewScale;camera.transform.LookAt(center);
        var rt=RenderTexture.GetTemporary(1000,700,24);var previous=RenderTexture.active;Texture2D image=null;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image=new Texture2D(1000,700,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1000,700),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(image)UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(go);}
    }
    static void MathChecks(){
        var a=new GameObject("Synthetic portal A").transform;var b=new GameObject("Synthetic portal B").transform;
        try{for(int i=0;i<256;i++){
            a.SetPositionAndRotation(new Vector3(i*.013f,.3f,-.4f),Quaternion.Euler(i%37,i*2.1f,0));b.SetPositionAndRotation(new Vector3(-.2f,.7f,i*.01f),Quaternion.Euler(0,i*3.7f,13));
            a.localScale=new Vector3(.7f,1.2f,.9f);b.localScale=new Vector3(1.3f,.8f,1.1f);var point=new Vector3(Mathf.Sin(i),.1f,Mathf.Cos(i));
            var mapped=RoomPortalPuzzle.MapPoint(a,b,point);var round=RoomPortalPuzzle.MapPoint(b,a,mapped);Require(Vector3.Distance(round,point)<.00001f,"Portal coordinate round trip");
        }
        Require(RoomPortalPuzzle.CrossesOpening(a,a.TransformPoint(new Vector3(0,0,-.02f)),a.TransformPoint(new Vector3(0,0,.02f)),.065f),"Opening crossing missed");
        Require(!RoomPortalPuzzle.CrossesOpening(a,a.TransformPoint(new Vector3(.2f,0,-.02f)),a.TransformPoint(new Vector3(.2f,0,.02f)),.065f),"Outside ring crossed");
        Require(!RoomPortalPuzzle.CrossesOpening(a,Vector3.one*float.NaN,Vector3.zero,.065f),"Invalid crossing accepted");}
        finally{UnityEngine.Object.Destroy(a.gameObject);UnityEngine.Object.Destroy(b.gameObject);}
    }
    static void Tick(){
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying){if(SessionState.GetBool(Key+".Done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.Exit(errors==0?0:2);}return;}
        try{
            if(phase==0){start=EditorApplication.timeSinceStartup;new GameObject("Synthetic shared portal hand pump").AddComponent<HciSeasonTreeHandPump>().sample=Pump;MathChecks();Later(1);return;}
            Require(errors==0,"Runtime/shader error");Require(EditorApplication.timeSinceStartup-start<130,"Portal test timeout");
            if(trace!=null||EditorApplication.timeSinceStartup<next||actor&&actor.Busy)return;
            if(phase==1){controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();controller.simulation=true;controller.SpawnVirtual();Later(2);}
            else if(phase==2){actor=controller.selected;Require(actor&&controller.SetRoomPortalExperience(true),"Content choice failed");portal=actor.RoomPortals;anchor=actor.transform.position;fit=actor.visual.parent.localToWorldMatrix;Require(portal&&portal.OwnedMaterialCount==8,"Portal content/material budget");
                var scale=actor.visual.localScale;actor.visual.localScale=scale*12;portal.RefreshReachLayout();
                Require(Vector3.Distance(portal.Entrance.position,anchor)<.19f&&Vector3.Distance(portal.Exit.position,anchor)<.19f&&Vector3.Distance(portal.Seed.position,anchor)<.19f,"Fitted room portal interaction exceeds shared 20cm reach");actor.visual.localScale=scale;portal.RefreshReachLayout();
                Capture("00-closed");Cast(EditDimension.Agency,actor.transform.position+Vector3.up*.05f);Later(3);}
            else if(phase==3){Require(portal.Open,"B failed to link portals");Capture("01-linked");Cast(EditDimension.Appearance,portal.Seed.position);Later(4);}
            else if(phase==4){Require(portal.Held,"A failed to pick up light seed");Capture("02-held");Cast(EditDimension.Rule,portal.Seed.position);Later(5);}
            else if(phase==5){Require(portal.Powered&&!portal.Held,"C did not arrive/power receiver");Capture("03-powered-left",-.06f);Capture("04-powered-right",.06f);Cast(EditDimension.Deconstruction,actor.transform.position+Vector3.up*.05f);Later(6);}
            else if(phase==6){Require(portal.Powered,"D lost powered state");Require(actor.visual.parent.localToWorldMatrix==fit,"Interaction changed fit root");Require(controller.Dispatch(ParkAction.Undo,anchor,true),"Undo failed");Later(7);}
            else if(phase==7){Require(portal.Powered,"Undo celebration should preserve powered socket");Require(controller.Dispatch(ParkAction.Reset,anchor,true),"Reset failed");Later(8);}
            else if(phase==8){Require(!portal.Open&&!portal.Powered&&!portal.Held,"Reset retained portal effect");
                float time=Time.unscaledTime;
                Require(portal.TryAction(ParkAction.Awaken,portal.Seed.position),"Direct crossing setup B");
                portal.ObserveHand(portal.Seed.position,time);
                Require(portal.TryAction(ParkAction.Style,portal.Seed.position),"Direct crossing setup A at fresh fingertip");
                var before=portal.Entrance.TransformPoint(new Vector3(0,0,-.025f));var after=portal.Entrance.TransformPoint(new Vector3(0,0,.025f));
                portal.ObserveHand(before,time+.03f);portal.ObserveHand(after,time+.06f);
                Require(portal.Mapped&&Vector3.Distance(portal.Seed.position,RoomPortalPuzzle.MapPoint(portal.Entrance,portal.Exit,after))<1e-5f,"Held object did not cross paired openings");
                portal.ObserveHand(before,time+.09f);Require(!portal.Mapped,"Reverse crossing failed");portal.Clear();
                portal.TryAction(ParkAction.Awaken,portal.Seed.position);portal.TryAction(ParkAction.Style,portal.Seed.position);
                controller.SetRightHandUiCapture(true);controller.ProcessHandSample(Frame(portal.Seed.position),true);Require(!portal.Held&&!portal.Open,"UI capture did not clear hand interaction");controller.SetRightHandUiCapture(false);
                portal.TryAction(ParkAction.Awaken,portal.Seed.position);portal.TryAction(ParkAction.Style,portal.Seed.position);actor.SetTracked(false);Require(!portal.Held&&!portal.Open,"Target loss retained portal interaction");actor.SetTracked(true);Later(9);}
            else if(phase==9){
                Require(actor.transform.position==anchor,"Portal moved target anchor");Require(portal.OwnedMaterialCount==8,"Repeated interaction grew materials");
                Require(controller.RequestManualPlane(),"Portal surface confirmation could not start");
                Require(!controller.ConfirmManualPlane(Vector3.one*float.NaN,true)&&controller.PlaneConfirmationPending,"Invalid surface ended confirmation");
                var surface=anchor+new Vector3(.34f,-.085f,-.03f);
                controller.ProcessHandSample(Frame(surface),true);
                controller.ProcessHandSample(Frame(surface,"open",.02f),true);
                Require(portal.UsesConfirmedSurface&&!controller.PlaneConfirmationPending&&!actor.confirmedPlane,"Portal pinch did not place independent surface");
                Require(actor.visual.Find("Timber joinery").GetComponent<Renderer>().forceRenderingOff,"Reference model occludes surface-placed portals");
                Require(Vector3.Distance(actor.InteractionCenter,surface+Vector3.up*.085f)<1e-5f,"Placement centre missed confirmed surface");
                var entrance=portal.Entrance.position;var exit=portal.Exit.position;
                var originalRotation=actor.transform.rotation;var originalScale=actor.visual.localScale;
                actor.transform.SetPositionAndRotation(anchor+Vector3.left*.06f,Quaternion.Euler(0,21,0)*originalRotation);
                actor.visual.localScale=originalScale*2;
                Require(Vector3.Distance(portal.Entrance.position,entrance)<1e-5f&&Vector3.Distance(portal.Exit.position,exit)<1e-5f,"Placed portals drifted with tracked translation, yaw or model scale");
                actor.transform.SetPositionAndRotation(anchor,originalRotation);actor.visual.localScale=originalScale;portal.RefreshReachLayout();
                Cast(EditDimension.Agency,actor.InteractionCenter);Later(11);}
            else if(phase==11){
                Require(portal.Open,"Shared B did not open surface-placed doors");
                var table=GameObject.CreatePrimitive(PrimitiveType.Cube);
                table.name="Synthetic tabletop for render review";
                table.transform.SetPositionAndRotation(actor.InteractionCenter-Vector3.up*.105f,portal.Entrance.rotation);
                table.transform.localScale=new Vector3(.49f,.04f,.27f);
                table.GetComponent<Collider>().enabled=false;
                var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor",new Color(.28f,.18f,.11f));
                table.GetComponent<Renderer>().sharedMaterial=material;
                try { Capture("Portal-Manual-Surface"); }
                finally { UnityEngine.Object.Destroy(table);UnityEngine.Object.Destroy(material); }
                Cast(EditDimension.Appearance,portal.Seed.position);Later(12);}
            else if(phase==12){
                Require(portal.Held,"Shared A did not pick up the surface-placed seed");
                actor.SetTracked(false);
                Require(!portal.UsesConfirmedSurface&&!portal.Held&&!portal.Open&&actor.InteractionCenter==anchor,"Target loss did not release the surface and hand");
                actor.SetTracked(true);
                Require(!actor.visual.Find("Timber joinery").GetComponent<Renderer>().forceRenderingOff&&portal.Entrance.IsChildOf(actor.visual),"Target recovery did not restore reference mode");
                Require(controller.RequestManualPlane()&&controller.ConfirmManualPlane(anchor+Vector3.right*.32f,true),"Recovery could not place doors anew");
                Require(controller.Dispatch(ParkAction.Reset,anchor,true)&&!portal.UsesConfirmedSurface,"Reset retained the placed doors");
                Require(controller.RequestManualPlane()&&controller.ConfirmManualPlane(anchor,true),"Placement before disabling actor failed");
                actor.gameObject.SetActive(false);
                Require(!portal.Entrance.gameObject.activeInHierarchy&&!portal.UsesConfirmedSurface,"Disabled actor left a world portal visible");
                actor.gameObject.SetActive(true);
                Require(portal.Entrance.IsChildOf(actor.visual)&&portal.Entrance.gameObject.activeInHierarchy,"Re-enabled actor did not restore the original portal layout");
                Require(controller.RequestManualPlane()&&controller.ConfirmManualPlane(anchor,true),"Second placement before mode switch failed");
                Require(controller.SetRoomPortalExperience(false),"Original room choice failed");Require(!portal.Enabled&&!actor.RoomPortalMode,"Portal mode stayed active");
                Require(!portal.UsesConfirmedSurface&&!actor.confirmedPlane,"Mode switch leaked the portal surface into Magic window");
                Require(controller.Dispatch(ParkAction.Awaken,anchor,true),"Original room B rejected after content switch");Later(10,2.6);}
            else if(phase==10){Require(actor.RoomWindowRequested&&actor.GetComponent<RoomMagicVisual>().WindowProgress>.99f,"Original photo window no longer works");File.WriteAllText(Output+"/gestures.txt",string.Join("\n",classifications)+"\n");
                Finish(true,"shared synthetic A/B/C/D each matches production recognizer once per cast; B links stencil windows, A carries/releases seed, C transmits/powers, D celebrates; 256 nonuniform transform round trips and crossing/invalid guards; synthetic left/right camera renders; manual surface pinch, world placement fixed under target transform changes, shared B/A at placed pose, cleanup on loss/Reset/mode switch; Undo/UI capture/recovery; 8 owned materials; original photo-window B restored. Editor only, no room mesh, physical hand replicas, wire physics, real YOLO/PICO, Android, performance or final-art acceptance.");}
        }catch(Exception e){Finish(false,e.ToString());}
    }
}
