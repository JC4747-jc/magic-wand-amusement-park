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
public static class HciSeasonTreeChecks
{
    const string Key="MagicMR.HciSeasonTree";
    static bool Willow=>SessionState.GetBool(Key+".Willow",false);
    static bool Live=>SessionState.GetBool(Key+".Live",false);
    static string Output=>Path.GetFullPath(Live?(Willow?".codex-tmp/hci-live-willow":".codex-tmp/hci-live-season-tree"):(Willow?".codex-tmp/hci-willow":".codex-tmp/hci-season-tree"));
    static ParkSharedInterface shared;static DataLogger logger;static bool menuCaptured;
    static Renderer scarRenderer,trunkRenderer;static Matrix4x4 trunkMatrix;static float minBreath=1,maxBreath;
    static ParkController controller;static ParkActor actor;static Renderer leaves;static Transform face,fit;
    static Vector3 anchor,fitPosition,fitScale;static Quaternion[] rest;
    static Transform[] branches;static int phase,errors;static double started,next;static float clock;
    static List<TabletopHandSample> trace;static int traceIndex,eventCount;static float nextSampleAt;
    static TabletopGestureRules lighter;static EditDimension expectedDimension;static TabletopHandSample heldSample;static bool holdValid;static double settleSeconds;
    static float minimumEyeHeight=float.PositiveInfinity,maximumEyeHeight;
    static HciSeasonTreeChecks(){EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};}
    static void Pump(){try{if(Live&&controller&&actor)controller.Observe(actor.instanceId,"Tree",anchor);if(controller)FeedTimedHand();if(Willow&&scarRenderer&&scarRenderer.gameObject.activeInHierarchy){var b=new MaterialPropertyBlock();scarRenderer.GetPropertyBlock(b);minBreath=Mathf.Min(minBreath,b.GetFloat("_Breath"));maxBreath=Mathf.Max(maxBreath,b.GetFloat("_Breath"));}if(face&&face.gameObject.activeInHierarchy){float h=face.GetChild(0).localScale.y;minimumEyeHeight=Mathf.Min(minimumEyeHeight,h);maximumEyeHeight=Mathf.Max(maximumEyeHeight,h);}}catch(Exception e){Fail(e);}}
    static void Fail(Exception e){File.WriteAllText(Output+"/result.txt","FAIL: "+e);SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(2);}
    static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
    public static void Run(){SessionState.SetBool(Key+".Live",false);SessionState.SetBool(Key+".Willow",false);Begin();}
    public static void RunWillow(){SessionState.SetBool(Key+".Live",false);SessionState.SetBool(Key+".Willow",true);Begin();}
    public static void RunLive(){SessionState.SetBool(Key+".Live",true);SessionState.SetBool(Key+".Willow",false);Begin();}
    public static void RunLiveWillow(){SessionState.SetBool(Key+".Live",true);SessionState.SetBool(Key+".Willow",true);Begin();}
    static void Begin(){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);EditorSceneManager.OpenScene("Assets/Scenes/ParkTree.unity");EditorApplication.EnterPlaymode();}
    static void Later(int value,double seconds=1.6){phase=value;settleSeconds=seconds;next=EditorApplication.timeSinceStartup+seconds;}
    static Button TreeButton(string name)=>UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(b=>b.name==name);
    static void Choose(bool fantasy){TreeButton(fantasy?"Fantasy tree":"Natural tree").onClick.Invoke();Require(controller.selected==actor&&actor.magicTree==fantasy&&!actor.virtualTarget,"UI tree choice replaced/relabelled target or failed: "+shared.Feedback);}
    static void ValidateLiveSelection(){
        // Synthetic Observe coordinates exercise the production physical-target branch, not YOLO/depth/device accuracy.
        // The disconnected hardware receiver expires targets every frame; isolate only that source in this fixture.
        foreach(var input in UnityEngine.Object.FindObjectsByType<Perception.ParkStereoReceiver>(FindObjectsSortMode.None))input.enabled=false;
        controller.simulation=false;anchor=Camera.main.transform.position+Camera.main.transform.forward*1.35f+Vector3.right*.5f;
        for(int i=0;i<3;i++)controller.Observe("SYNTHETIC_OBSERVE_TREE","Tree",anchor);
        var index=anchor-Vector3.forward*.08f;
        var select=TabletopHandSample.Create(Time.unscaledTime,index-Vector3.up*.04f,index,index+Vector3.right*.02f,index-Vector3.forward*.04f,Vector4.one*.95f,Vector3.right,.02f);
        controller.ProcessHandSample(select,true);actor=controller.selected;Require(actor&&!actor.virtualTarget&&actor.tracked,"Production point/pinch did not select synthetic physical Tree");
        controller.ProcessHandSample(default,false);shared=UnityEngine.Object.FindFirstObjectByType<ParkSharedInterface>();Require(shared,"Shared UI absent");
        logger=DataLogger.Instance;if(!logger)logger=new GameObject("Synthetic tree study logger").AddComponent<DataLogger>();
        Require(!logger.SessionActive,"Refuse to reuse an existing study session");logger.ConfigureLogging(false,.1f);logger.StartSession("SYNTHETIC_OBSERVE_TREE_"+(Willow?"WILLOW":"SEASON"),"C4");
        logger.LogEvent("synthetic_fixture",EditDimension.None,-1,-1,-1,notes:"Synthetic recognition coordinates and joint samples; hardware receiver disabled in fixture; no camera, YOLO, depth or PICO measurements");
        Require(actor.ConfirmOrientation(23)&&actor.FitRepresentation(anchor,anchor+Vector3.up*.6f,true),"Initial fit failed");
        var fitNode=actor.transform.Find("Manual fit - separate from tracking and animation");var fp=fitNode.localPosition;var fs=fitNode.localScale;var rotation=actor.transform.rotation;string identity=actor.instanceId;
        Choose(true);Choose(false);
        Require(actor.instanceId==identity&&actor.transform.position==anchor&&actor.transform.rotation==rotation&&actor.orientationConfirmed&&actor.sizeConfirmed&&fitNode.localPosition==fp&&fitNode.localScale==fs,"Choice lost physical identity or manual placement");
        int revision=actor.Revision;Choose(false);Require(actor.Revision==revision,"Idempotent choice reset effects");
        TreeButton("Test awaken").onClick.Invoke();Require(actor.Revision==revision&&!actor.Busy,"Virtual awaken accepted on physical target");
        shared.replayMode=true;TreeButton("Fantasy tree").onClick.Invoke();Require(!actor.magicTree&&actor.Revision==revision,"Replay changed live experience");shared.replayMode=false;
        controller.simulation=true;Require(!controller.SetTreeExperience(true)&&!controller.SetVirtualTreeMode(true),"Simulation relabelled physical target");controller.simulation=false;
        Require(controller.Dispatch(ParkAction.Style,anchor,true)&&actor.Busy,"Busy guard setup failed");Require(!controller.SetTreeExperience(true)&&!actor.magicTree&&actor.Busy,"Choice interrupted busy magic");controller.Dispatch(ParkAction.Reset,anchor,true);
        Require(controller.RequestFit()&&!controller.SetTreeExperience(true),"Fit guard failed");controller.Dispatch(ParkAction.Reset,anchor,true);
        Require(controller.RequestManualPlane()&&!controller.SetTreeExperience(true),"Plane guard failed");controller.Dispatch(ParkAction.Reset,anchor,true);
        var seen=(Dictionary<string,float>)typeof(ParkController).GetField("seen",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(controller);
        seen[identity]=Time.unscaledTime-controller.lostAfter-1;Require(!controller.SetTreeExperience(true),"Stale target accepted before Update expiry");controller.Observe(identity,"Tree",anchor);
        var foreign=UnityEngine.Object.Instantiate(actor);foreign.instanceId=identity;controller.selected=foreign;Require(!controller.SetTreeExperience(true),"Foreign instance accepted");controller.selected=actor;UnityEngine.Object.DestroyImmediate(foreign.gameObject);
        actor.SetTracked(false);Require(!controller.SetTreeExperience(true),"Lost target accepted");for(int i=0;i<3;i++)controller.Observe(identity,"Tree",anchor);Require(actor.tracked,"Target did not recover");
        actor.gameObject.SetActive(false);Require(!controller.SetTreeExperience(true),"Disabled target accepted");actor.gameObject.SetActive(true);
        controller.selected=null;Require(!controller.SetTreeExperience(true),"Missing target accepted");controller.selected=actor;
        var scenario=controller.scenario;controller.scenario=ParkScenario.Pet;Require(!controller.SetTreeExperience(true),"Wrong scenario accepted");controller.scenario=scenario;
        Choose(Willow);revision=actor.Revision;
        // A held UI pinch and its release must not become an Appearance spell.
        controller.SetRightHandUiCapture(true);Require(controller.ProcessHandSample(Frame(Time.unscaledTime,"open",.02f),true)==EditDimension.None,"UI pinch leaked");
        controller.SetRightHandUiCapture(false);Require(controller.ProcessHandSample(Frame(Time.unscaledTime+.02f,"open",.02f),true)==EditDimension.None,"Held UI pinch leaked");
        Require(controller.ProcessHandSample(Frame(Time.unscaledTime+.04f,"open"),true)==EditDimension.None&&actor.Revision==revision,"UI release became magic");controller.ProcessHandSample(default,false);
        File.WriteAllText(Output+"/selection-checks.txt","PASS: production point/pinch selected SYNTHETIC Observe target; UI both modes preserve identity/placement; idempotent choice; virtual-only awaken and replay denied; simulation/physical mismatch, Busy, Fit, Plane, stale, foreign, lost, disabled, missing, wrong scenario rejected; UI pinch/release does not cast. Synthetic fixture, no YOLO/PICO claim.\n");
    }
    static void FinishLiveEvidence(){if(!Live)return;logger.EndSession();File.Copy(logger.EventFilePath,Output+"/synthetic-events.csv",true);File.AppendAllText(Output+"/result.txt","Physical-target branch used synthetic Observe coordinates, same selected instance and shared UI experience selector; full effects driven by timed synthetic production hand samples. Not real YOLO/PICO validation.\n");}
    static void CaptureMenu(){
        var panel=TreeButton("Natural tree").transform.parent;var camera=new GameObject("Synthetic tree menu review camera").AddComponent<Camera>();
        camera.orthographic=true;camera.orthographicSize=.29f;camera.nearClipPlane=.01f;camera.farClipPlane=2;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.18f,.2f);
        camera.transform.SetPositionAndRotation(panel.position-panel.forward,panel.rotation);
        var rt=new RenderTexture(800,800,24);var pixels=new Texture2D(800,800,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{Canvas.ForceUpdateCanvases();camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,800,800),0,0);pixels.Apply();File.WriteAllBytes(Output+"/menu.png",pixels.EncodeToPNG());}
        finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
    }
    static float Property(string name){var b=new MaterialPropertyBlock();leaves.GetPropertyBlock(b);return b.GetFloat(name);}
    static TabletopHandSample Frame(float t,string shape,float pinch=.08f,Vector3 delta=default){
        var extension=shape=="fist"?Vector4.one*.55f:shape=="point"?new Vector4(.95f,.55f,.55f,.55f):Vector4.one*.95f;
        var p=actor.transform.position+Vector3.up*.05f+delta;
        return TabletopHandSample.Create(t,p,p+Vector3.up*.04f,p+Vector3.up*.04f+Vector3.right*pinch,p-Vector3.up*.02f,extension,Vector3.right,pinch);
    }
    static void Cast(EditDimension dimension){
        controller.ProcessHandSample(default,false);clock=0;eventCount=0;lighter=new TabletopGestureRules();trace=new List<TabletopHandSample>();traceIndex=0;nextSampleAt=Time.unscaledTime;expectedDimension=dimension;holdValid=false;
        void Feed(TabletopHandSample f)=>trace.Add(f);
        Feed(Frame(clock,"open"));
        Feed(Frame(clock+.02f,"open")); // First open releases selection/UI ownership; second seeds both recognizers.
        if(dimension==EditDimension.Appearance){for(int n=1;n<=8;n++)Feed(Frame(clock+n*.02f,"open",.02f));Feed(Frame(clock+.18f,"open"));}
        else if(dimension==EditDimension.Agency){for(int n=0;n<=32;n++){float a=n/32f*Mathf.PI*2;Feed(Frame(clock+.02f+n*.02f,"point",.08f,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.04f));}}
        else if(dimension==EditDimension.Rule){for(int n=1;n<=6;n++)Feed(Frame(clock+n*.03f,"open",.08f,Vector3.right*n*.03f));}
        else{for(int n=1;n<=45;n++)Feed(Frame(clock+n*.02f,"fist",.02f));Feed(Frame(clock+.92f,"open"));}
    }
    static bool FeedTimedHand(){
        if(trace!=null){
            if(Time.unscaledTime<nextSampleAt)return true;
            var f=trace[traceIndex++];f.gesture.time=Time.unscaledTime;nextSampleAt=Time.unscaledTime+.03f;
            var expected=traceIndex==1?EditDimension.None:lighter.Step(f.gesture);var actual=controller.ProcessHandSample(f,true);
            Require(expected==actual,"Recognition diverged from lighter: sample="+traceIndex+" expected="+expected+" actual="+actual+" time="+f.gesture.time+" status="+controller.InteractionStatus);
            if(actual!=EditDimension.None){eventCount++;Require(actual==expectedDimension&&controller.LastMagicAccepted,"Wrong/rejected gesture: "+controller.InteractionStatus);}
            heldSample=f;holdValid=true;
            if(traceIndex==trace.Count){Require(eventCount==1,"Gesture was not exactly once: "+expectedDimension+" events="+eventCount);trace=null;next=EditorApplication.timeSinceStartup+settleSeconds;}
            return true;
        }
        if(holdValid){heldSample.gesture.time=Time.unscaledTime;Require(controller.ProcessHandSample(heldSample,true)==EditDimension.None,"Static hand repeated cast");}
        return false;
    }
    static void Capture(string name,bool closeFace=false){
        var camera=new GameObject("Synthetic tree review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.38f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.18f,.20f);camera.nearClipPlane=.01f;
        var target=actor.ConfirmedBottomWorld+Vector3.up*.29f;camera.transform.position=target+new Vector3(0,.10f,-1.2f);camera.transform.LookAt(target);
        if(closeFace){var focus=Willow?scarRenderer.transform:face;camera.orthographicSize=.025f;camera.transform.position=focus.position+focus.forward*.2f;camera.transform.LookAt(focus.position,focus.up);}
        if(name=="06-chest-detail"){var chest=actor.visual.Find("Reward chest");camera.orthographicSize=.12f;camera.transform.position=chest.position+new Vector3(.15f,.14f,-.3f);camera.transform.LookAt(chest.position+Vector3.up*.045f);}
        var rt=new RenderTexture(800,800,24);var pixels=new Texture2D(800,800,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,800,800),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());}
        finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
    }
    static void Tick(){
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying){if(SessionState.GetBool(Key+".Done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}return;}
        try{
            if(phase==0){started=EditorApplication.timeSinceStartup;new GameObject("Synthetic hand frame driver").AddComponent<HciSeasonTreeHandPump>().sample=Pump;Later(1);return;}
            Require(errors==0,"Runtime error");Require(EditorApplication.timeSinceStartup-started<100,"Timeout");if(trace!=null)return;if(EditorApplication.timeSinceStartup<next)return;
            if(phase==1){
                controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();Require(controller,"Saved ParkTree absent");
                if(Live)ValidateLiveSelection();else{controller.simulation=true;controller.SpawnVirtual();Require(controller.SetVirtualTreeMode(Willow),"Tree mode unavailable");actor=controller.selected;}
                Require(actor.ConfirmOrientation(0)&&actor.FitRepresentation(actor.transform.position,actor.transform.position+Vector3.up*.6f,true),"Synthetic fit failed");
                fit=actor.transform.Find("Manual fit - separate from tracking and animation");fitPosition=fit.localPosition;fitScale=fit.localScale;anchor=actor.transform.position;
                leaves=actor.GetComponentsInChildren<Renderer>(true).Single(r=>r.sharedMaterial&&r.sharedMaterial.shader.name=="MagicMR/NaturalTreeLeaves");
                face=actor.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Eyes");branches=actor.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Branch_")).OrderBy(t=>t.name).ToArray();rest=branches.Select(t=>t.localRotation).ToArray();
                scarRenderer=actor.GetComponentsInChildren<Renderer>(true).Single(r=>r.sharedMaterial&&r.sharedMaterial.shader.name=="MagicMR/NaturalBarkMark");trunkRenderer=actor.GetComponentsInChildren<Renderer>().Single(r=>r.name=="Trunk");trunkMatrix=trunkRenderer.localToWorldMatrix;
                Require(!face.gameObject.activeInHierarchy,"Face visible before B");Require(!ShaderUtil.ShaderHasError(leaves.sharedMaterial.shader)&&!ShaderUtil.ShaderHasError(scarRenderer.sharedMaterial.shader),"Tree shader error");Later(2);return;
            }
            if(Live&&!menuCaptured){Require(TreeButton("Natural tree").gameObject.activeInHierarchy&&TreeButton("Fantasy tree").gameObject.activeInHierarchy&&TreeButton("Natural tree").interactable&&TreeButton("Fantasy tree").interactable&&!TreeButton("Test awaken").gameObject.activeInHierarchy,"Live shared mode buttons hidden or virtual test exposed");Require(TreeButton(Willow?"Fantasy tree":"Natural tree").GetComponentInChildren<Text>().color==Color.cyan,"Chosen experience not highlighted");CaptureMenu();menuCaptured=true;}
            if(Willow){TickWillow();return;}
            if(phase==2){Capture("00-natural-spring");Cast(EditDimension.Appearance);Later(3);}
            else if(phase==3){Capture("01-anime-spring");Require(Property("_ToonMix")>.99f&&Mathf.Abs(actor.seasonTime)<.001f,"A changed season or lacks anime shading: toon="+Property("_ToonMix")+" season="+actor.seasonTime+" fantasy="+actor.magicTree+" feedback="+actor.Feedback+" busy="+actor.Busy);Cast(EditDimension.Agency);Later(4);}
            else if(phase==4){Require(face.gameObject.activeInHierarchy&&face.GetComponentsInChildren<Renderer>().Length==5,"B face incomplete");Capture("02-awake");Capture("08-face-detail",true);Cast(EditDimension.Rule);Later(5,2);}
            else if(phase==5){Require(Mathf.Abs(actor.seasonTime-.25f)<.001f,"C did not select summer");Capture("03-summer");Cast(EditDimension.Rule);Later(6,2);}
            else if(phase==6){Require(Mathf.Abs(actor.seasonTime-.5f)<.001f&&Property("_SeasonMix")>.9f,"C did not select autumn");Capture("04-autumn");Cast(EditDimension.Deconstruction);Later(7,.65);}
            else if(phase==7){
                Require(branches.Where((t,i)=>Quaternion.Angle(t.localRotation,rest[i])>.1f).Any(),"D did not move branches");Require(!actor.visual.Find("Reward chest").gameObject.activeInHierarchy,"Season D opened fantasy chest");Capture("05-autumn-gust");Later(8,3);
            }
            else if(phase==8){Require(branches.Select((t,i)=>Quaternion.Angle(t.localRotation,rest[i])).Max()<.01f,"Bounded gust did not settle");Require(controller.Dispatch(ParkAction.Undo,anchor,true),"Undo D failed");Cast(EditDimension.Rule);Later(9,2);}
            else if(phase==9){Require(Mathf.Abs(actor.seasonTime-.75f)<.001f&&Property("_LeafCoverage")<.03f,"Winter canopy did not shed");Capture("06-winter");Cast(EditDimension.Rule);Later(10,2);}
            else if(phase==10){Require(Mathf.Abs(actor.seasonTime-1)<.001f&&Property("_LeafCoverage")>.99f,"Spring recovery failed");Capture("07-return-spring");Cast(EditDimension.Appearance);Later(11);}
            else if(phase==11){Require(Property("_ToonMix")<.001f&&actor.seasonTime==1,"A toggle changed season");Require(actor.transform.position==anchor&&fit.localPosition==fitPosition&&fit.localScale==fitScale&&actor.sizeConfirmed,"Effects changed placement");Require(controller.DispatchDimension(EditDimension.Agency,anchor,true),"Interruption setup failed");controller.Dispatch(ParkAction.Reset,anchor,true);holdValid=false;Later(12);}
            else if(phase==12){
                Require(!actor.Busy&&!face.gameObject.activeInHierarchy&&actor.seasonTime==0&&Property("_ToonMix")<.001f,"Mid-action reset leaked effects");
                Require(minimumEyeHeight<maximumEyeHeight*.5f,"B eyes did not blink");
                File.WriteAllText(Output+"/result.txt","PASS: saved ParkTree natural mode, synthetic joint traces through production ProcessHandSample match lighter exactly once; A reversible anime canopy without season change; B five-renderer face with measured blink; C summer/autumn/winter/spring including leaf shedding/recovery; D bounded gust with no fantasy chest; Undo; fit/anchor stable; interrupted Reset. 9 review renders, no real YOLO/XR/PICO or Android acceptance.\n");FinishLiveEvidence();SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();
            }
        }catch(Exception e){Fail(e);}
    }
    static void TickWillow(){
        var chest=actor.visual.Find("Reward chest");var block=new MaterialPropertyBlock();leaves.GetPropertyBlock(block);
        if(phase==2){Require(!controller.DispatchDimension(EditDimension.Deconstruction,anchor,true),"D accepted before scar reveal");Capture("00-natural");Cast(EditDimension.Appearance);Later(3);}
        else if(phase==3){Require(block.GetColor("_BaseColor").r<.6f&&actor.seasonTime==0,"A did not darken or changed season");Capture("01-dark");Cast(EditDimension.Agency);Later(4,.75);}
        else if(phase==4){Require(branches.Where((t,i)=>Quaternion.Angle(t.localRotation,rest[i])>2).Any(),"B did not lash branches");Require(!scarRenderer.gameObject.activeInHierarchy&&!face.gameObject.activeInHierarchy,"B showed C scar or natural-tree face");Capture("02-lash");Later(5,2.5);}
        else if(phase==5){Require(branches.Select((t,i)=>Quaternion.Angle(t.localRotation,rest[i])).Max()<.01f,"B did not recover");Cast(EditDimension.Rule);Later(6,1.8);}
        else if(phase==6){Require(scarRenderer.gameObject.activeInHierarchy&&maxBreath-minBreath>.5f,"C scar did not breathe");Capture("03-breathing-scar");Capture("04-scar-detail",true);Cast(EditDimension.Deconstruction);Later(7,1.5);}
        else if(phase==7){Require(chest.gameObject.activeInHierarchy&&Quaternion.Angle(chest.Find("Hinged lid").localRotation,Quaternion.identity)>100&&!scarRenderer.gameObject.activeInHierarchy,"D did not seal/open chest");Capture("05-sealed");Capture("06-chest-detail");Require(controller.Dispatch(ParkAction.Undo,anchor,true),"Undo seal rejected");Later(8);}
        else if(phase==8){Require(!chest.gameObject.activeInHierarchy&&scarRenderer.gameObject.activeInHierarchy,"Undo did not restore breathing scar");Cast(EditDimension.Appearance);Later(9);}
        else if(phase==9){Require(block.GetColor("_BaseColor").r>.99f&&actor.seasonTime==0,"A did not restore natural style");Require(trunkRenderer.localToWorldMatrix==trunkMatrix&&actor.transform.position==anchor&&fit.localPosition==fitPosition&&fit.localScale==fitScale,"Willow moved fitted trunk or anchor");controller.Dispatch(ParkAction.Reset,anchor,true);holdValid=false;Later(10);}
        else if(phase==10){Require(!actor.Busy&&!chest.gameObject.activeInHierarchy&&!scarRenderer.gameObject.activeInHierarchy&&Quaternion.Angle(chest.Find("Hinged lid").localRotation,Quaternion.identity)<.01f,"Reset leaked willow effects");File.WriteAllText(Output+"/result.txt","PASS: actual ParkTree fantasy variant; synthetic production hand traces match lighter exactly once. A dark/natural without season change; B bounded branch lash and recovery; C visible bark luminance breathing; D scar prerequisite, seal and hinged chest; Undo restores C; Reset closes chest; fitted trunk and anchor unchanged. Seven Unity renders. Existing small-tree mesh remains a botanical stand-in, not a finished willow; no YOLO/XR/PICO/Android acceptance.\n");FinishLiveEvidence();SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();}
    }
}

// Sample on game frames before ParkController.Update, like Dynamic XR updates.
// EditorApplication.update is not synchronized with the player's stale-sample guard.
[DefaultExecutionOrder(-32000)]
public sealed class HciSeasonTreeHandPump:MonoBehaviour {
    public Action sample;
    void Update(){sample?.Invoke();}
}

