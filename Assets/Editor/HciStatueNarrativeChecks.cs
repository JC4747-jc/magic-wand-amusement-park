using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class HciStatueNarrativeChecks
{
    const string Key="MagicMR.HciStatueNarrative";
    static string Output=>Path.GetFullPath(".codex-tmp/hci-statue-narrative");
    static ParkController controller;static ParkActor actor;static NaturalCartoonStyle style;static Camera reviewCamera;
    static Renderer[] bodies;static Material[][] original;static int owned;
    static Transform fit;static Vector3 anchor,fitPosition,fitScale;
    static int phase,species,errors,eventCount,traceIndex,nodes,readingView;static double started,next;
    static List<TabletopHandSample> trace;static TabletopGestureRules lighter;
    static EditDimension expectedDimension;static TabletopHandSample held;static bool holdValid;static float sampleAt;
    static HciStatueNarrativeChecks(){EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};}
    public static void Run(){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");File.WriteAllText(Output+"/layout.csv","species,view,bodyRightPixel,panelLeftPixel,gapPixels\n");SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);EditorSceneManager.OpenScene("Assets/Scenes/ParkStatue.unity");EditorApplication.EnterPlaymode();}
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
    static void ConfigureReviewCamera(){
        foreach(var existing in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))if(existing.CompareTag("MainCamera")){existing.tag="Untagged";existing.enabled=false;}
        if(!reviewCamera)reviewCamera=new GameObject("Synthetic statue review camera").AddComponent<Camera>();
        reviewCamera.tag="MainCamera";reviewCamera.enabled=true;reviewCamera.orthographic=true;reviewCamera.orthographicSize=.65f;reviewCamera.aspect=1;reviewCamera.clearFlags=CameraClearFlags.SolidColor;reviewCamera.backgroundColor=new Color(.65f,.68f,.70f);reviewCamera.nearClipPlane=.01f;
        var target=actor.ConfirmedBottomWorld+Vector3.up*.20f+Vector3.right*.13f;reviewCamera.transform.position=target+new Vector3(.7f,.25f,-1);reviewCamera.transform.LookAt(target);
    }
    static void CheckNarrativeLayout(){
        Require(reviewCamera&&Camera.main==reviewCamera,"Review camera is not the viewer");
        var labels=actor.GetComponentsInChildren<TextMesh>();
        foreach(var slot in new[]{("Chapter",.20f,.30f),("Narrative",-.115f,.185f),("Source",-.30f,-.20f)}){
            var label=labels.Single(t=>t.name==slot.Item1);var sheet=label.transform.parent;var bounds=label.GetComponent<Renderer>().localBounds;
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++){
                var corner=new Vector3(x==0?bounds.min.x:bounds.max.x,y==0?bounds.min.y:bounds.max.y,z==0?bounds.min.z:bounds.max.z);
                var local=sheet.InverseTransformPoint(label.transform.TransformPoint(corner));
                Require(local.x>=-.452f&&local.x<=.452f&&local.y>=slot.Item2-.002f&&local.y<=slot.Item3+.002f,"Scroll label exceeds its slot: "+slot.Item1);
            }
            var root=sheet.parent;Require(Vector3.Dot(root.forward,(root.position-reviewCamera.transform.position).normalized)>.995f,"Scroll does not face the review camera");
        }
    }
    static Color32[] Capture(string name,int size=800){
        Require(reviewCamera&&Camera.main==reviewCamera,"Missing persistent review camera");var camera=reviewCamera;var oldTarget=camera.targetTexture;
        var rt=new RenderTexture(size,size,24);var pixels=new Texture2D(size,size,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,size,size),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+actor.species+"-"+name+".png",pixels.EncodeToPNG());return pixels.GetPixels32();}
        finally{camera.targetTexture=oldTarget;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
    }
    static string ViewName=>new[]{"front","left","right","perspective"}[readingView];
    static void SetReadingView(){
        var target=actor.ConfirmedBottomWorld+Vector3.up*.20f;
        var offsets=new[]{new Vector3(0,.2f,-1.2f),new Vector3(-.7f,.25f,-1.2f),new Vector3(.7f,.25f,-1.2f),new Vector3(.5f,.25f,-1.8f)};
        reviewCamera.orthographic=readingView!=3;reviewCamera.orthographicSize=.65f;reviewCamera.fieldOfView=55;
        reviewCamera.transform.position=target+offsets[readingView];reviewCamera.transform.LookAt(target);
    }
    static void FrameReadingView(){
        // Framing is a test camera operation; the production target/fit stays fixed.
        var panel=actor.GetComponentsInChildren<Transform>().Single(t=>t.name=="Statue C - anchored narrative scroll");
        float minX=float.PositiveInfinity,minY=minX,maxX=float.NegativeInfinity,maxY=maxX;
        foreach(var renderer in bodies.Concat(panel.GetComponentsInChildren<Renderer>())){
            if(!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;var b=renderer.bounds;
            for(int i=0;i<8;i++){
                var p=reviewCamera.WorldToViewportPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                Require(p.z>reviewCamera.nearClipPlane,"Review geometry behind camera");minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);
            }
        }
        float distance=Vector3.Dot(panel.position-reviewCamera.transform.position,reviewCamera.transform.forward);
        float halfHeight=reviewCamera.orthographic?reviewCamera.orthographicSize:Mathf.Tan(reviewCamera.fieldOfView*Mathf.Deg2Rad*.5f)*distance;
        reviewCamera.transform.position+=reviewCamera.transform.right*((minX+maxX-1)*halfHeight*reviewCamera.aspect)+reviewCamera.transform.up*((minY+maxY-1)*halfHeight);
        float factor=Mathf.Max(.1f,Mathf.Max(maxX-minX,maxY-minY)*1.16f);
        if(reviewCamera.orthographic)reviewCamera.orthographicSize*=factor;else reviewCamera.transform.position-=reviewCamera.transform.forward*(distance*(Mathf.Max(1,factor)-1));
    }
    static void CheckPixelSeparation(string view){
        var panel=actor.GetComponentsInChildren<Transform>().Single(t=>t.name=="Statue C - anchored narrative scroll");
        var scene=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);var enabled=scene.Select(r=>r.enabled).ToArray();
        var background=reviewCamera.backgroundColor;var shader=Shader.Find("Unlit/Color");Require(shader,"Missing independent geometry-mask shader");
        Color32[] bodyPixels,panelPixels;
        try{
            reviewCamera.backgroundColor=Color.black;reviewCamera.SetReplacementShader(shader,"");
            var selected=new HashSet<Renderer>(bodies);
            for(int i=0;i<scene.Length;i++)scene[i].enabled=enabled[i]&&selected.Contains(scene[i]);
            bodyPixels=Capture("mask-"+view+"-body");
            selected=new HashSet<Renderer>(panel.GetComponentsInChildren<Renderer>());
            for(int i=0;i<scene.Length;i++)scene[i].enabled=enabled[i]&&selected.Contains(scene[i]);
            panelPixels=Capture("mask-"+view+"-panel");
        }finally{reviewCamera.ResetReplacementShader();reviewCamera.backgroundColor=background;for(int i=0;i<scene.Length;i++)if(scene[i])scene[i].enabled=enabled[i];}
        int right=-1,left=800,bodyCount=0,panelCount=0;
        for(int i=0;i<bodyPixels.Length;i++){
            bool b=bodyPixels[i].r+bodyPixels[i].g+bodyPixels[i].b>12,p=panelPixels[i].r+panelPixels[i].g+panelPixels[i].b>12;
            if(b){bodyCount++;right=Mathf.Max(right,i%800);}if(p){panelCount++;left=Mathf.Min(left,i%800);Require(i%800>3&&i%800<796&&i/800>3&&i/800<796,"Scroll clipped in "+view);}
        }
        Require(bodyCount>1000&&panelCount>1000,"Geometry masks empty or too small");
        File.AppendAllText(Output+"/layout.csv",actor.species+","+view+","+right+","+left+","+(left-right)+"\n");
        Require(left-right>=2,"Scroll overlaps model in "+view+": gap="+(left-right));
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
                Require(owned>0&&owned<=4,"Material budget exceeded");Check(false);ConfigureReviewCamera();Later(3);
            }
            else if(phase==3){Capture("00-original");Require(!controller.Dispatch(ParkAction.Style,anchor,false)&&!controller.Dispatch(ParkAction.Style,anchor+Vector3.right*20,true),"Invalid/far A accepted");Cast(EditDimension.Appearance);Later(4);}
            else if(phase==4){
                Check(true);var toon=Capture("01-cartoon");style.SetActive(false);var plain=Capture("01-same-frame-original");style.SetActive(true);int changed=0;
                for(int i=0;i<toon.Length;i++)if(Mathf.Abs(toon[i].r-plain[i].r)+Mathf.Abs(toon[i].g-plain[i].g)+Mathf.Abs(toon[i].b-plain[i].b)>60)changed++;
                Require(changed>4000,"Cartoon rendering not visibly different: "+changed);File.WriteAllText(Output+"/"+actor.species+"-pixels.txt","Same-frame changed pixels: "+changed+"; minimum 4000\n");
                Require(actor.Feedback.Contains("cartoon"),"A still selects story chapters");Cast(EditDimension.Agency);Later(5);
            }
            else if(phase==5){Check(true);Cast(EditDimension.Rule);Later(6);}
            else if(phase==6){Check(true);Require(actor.Feedback.Contains(actor.storySource)&&actor.Feedback.Contains(actor.story),"C lost story/source");Require(actor.StatueNarrativeVisible&&actor.StatueNarrativePage==0&&actor.StatueNarrativePageCount==4,"Missing first scroll chapter");nodes=actor.GetComponentsInChildren<Transform>(true).Length;CheckNarrativeLayout();Capture("02-after-story");Later(60,4.2);}
            else if(phase==60){Require(actor.StatueNarrativePage>=1,"Scroll did not advance");CheckNarrativeLayout();Capture("04-next-chapter");readingView=0;SetReadingView();Later(70,.2);}
            else if(phase==70){FrameReadingView();Later(71,.2);}
            else if(phase==71){CheckNarrativeLayout();CheckPixelSeparation(ViewName);Capture("05-view-"+ViewName,1600);if(++readingView<4){SetReadingView();Later(70,.2);}else{ConfigureReviewCamera();Later(72,.2);}}
            else if(phase==72){Act(ParkAction.Wind);Later(61);}
            else if(phase==61){Require(actor.StatueNarrativeVisible&&actor.StatueNarrativePage==0,"C did not restart scroll");Require(actor.GetComponentsInChildren<Transform>(true).Length==nodes,"Scroll replay allocated extra nodes");actor.EndHandInteraction();Require(!actor.StatueNarrativeVisible,"Stop left scroll");Act(ParkAction.Wind);Later(62);}
            else if(phase==62){actor.SendMessage("OnApplicationPause",true);Require(!actor.StatueNarrativeVisible&&!actor.GetComponent<AudioSource>().isPlaying,"Pause retained story/audio");Cast(EditDimension.Appearance);Later(7);}
            else if(phase==7){Check(false);Act(ParkAction.Undo);Later(8);}
            else if(phase==8){Check(true);Require(actor.transform.position==anchor&&fit.localPosition==fitPosition&&fit.localScale==fitScale,"Style moved fit/anchor");holdValid=false;controller.ProcessHandSample(default,false);Act(ParkAction.Wind);actor.SetTracked(false);Require(!actor.StatueNarrativeVisible,"Lost target retained scroll");Require(!actor.visual.gameObject.activeInHierarchy&&!controller.Dispatch(ParkAction.Style,anchor,true),"Lost target accepted A");actor.SetTracked(true);Later(9);}
            else if(phase==9){Check(false);Cast(EditDimension.Appearance);Later(10);}
            else if(phase==10){Check(true);Act(ParkAction.Reset);Later(11);}
            else if(phase==11){Check(false);Require(!actor.Busy,"Reset retained motion");Require(!actor.StatueNarrativeVisible,"Reset retained scroll");Capture("03-reset");if(species++==0){Require(controller.SelectVirtualStatue("BronzeBeast"),"Bronze switch failed");Later(2);}else{
                File.WriteAllText(Output+"/result.txt","PASS: progressive scroll opens; four bounded chapters; timed chapter change; repeated C restarts without extra nodes; Stop/pause/lost/reset hide panel and stop narration; saved ParkStatue, standing human + bronze; synthetic production A/B/C joint traces match lighter exactly once; A reversible cartoon material rather than story selection; B/C preserve appearance; C retains story/source; original albedo and exact shared material restoration; bounded material count; same-frame visible pixel checks; invalid/far/lost rejection; Undo/Reset; stable fit/anchor; zero runtime errors; twenty review renders including eight at 1600 pixels and sixteen independent 800-pixel geometry masks; model/scroll separation at front/left/right/perspective views. Editor only. B/D motion has separate validation; no real YOLO/XR/PICO/Android acceptance.\n");SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();}}
        }catch(Exception e){Fail(e);}
    }
}
