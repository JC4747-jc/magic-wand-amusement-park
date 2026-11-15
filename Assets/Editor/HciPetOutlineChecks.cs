using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Synthetic joints exercise the real shared recognizer. This is not device acceptance.
[InitializeOnLoad]
public static class HciPetOutlineChecks
{
    const string Key="MagicMR.HciPetOutline";
    static string Output=>Path.GetFullPath(".codex-tmp/hci-pet-outline");
    static ParkController controller;static ParkActor actor;static NaturalPetOutline outline;
    static Transform bubble,fit;static Renderer[] shells;static Vector3 anchor,fitPosition,fitScale;
    static int phase,species,errors,eventCount,traceIndex;static double started,next;
    static List<TabletopHandSample> trace;static TabletopGestureRules lighter;
    static EditDimension expectedDimension;static TabletopHandSample held;static bool holdValid;static float sampleAt;
    static HciPetOutlineChecks(){EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};}
    public static void Run(){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);EditorSceneManager.OpenScene("Assets/Scenes/ParkPet.unity");EditorApplication.EnterPlaymode();}
    static void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
    static void Fail(Exception e){File.WriteAllText(Output+"/result.txt","FAIL: "+e);SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(2);}
    static void Later(int value,double delay=1.2){phase=value;next=EditorApplication.timeSinceStartup+delay;}
    static void Act(ParkAction action)=>Require(controller.Dispatch(action,anchor,true),action+" rejected: "+controller.InteractionStatus);
    static bool BubbleVisible=>bubble.gameObject.activeInHierarchy;
    static void Check(bool comic,bool dream,int affection){Require(actor.PetComic==comic&&shells.All(r=>r.enabled==comic),"Outline state mismatch");Require(BubbleVisible==dream&&actor.Affection==affection,"Outline altered dream/affection");}
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
            Require(expected==actual,"Pet recognition differs from lighter: "+expected+"/"+actual+" "+controller.InteractionStatus);
            if(actual!=EditDimension.None){eventCount++;Require(actual==expectedDimension&&controller.LastMagicAccepted,"Wrong/rejected spell");}
            held=f;holdValid=true;if(traceIndex==trace.Count){Require(eventCount==1,"Expected one gesture event, got "+eventCount);trace=null;next=EditorApplication.timeSinceStartup+1.2;}
        }else if(holdValid){held.gesture.time=Time.unscaledTime;Require(controller.ProcessHandSample(held,true)==EditDimension.None,"Held hand repeated spell");}
    }catch(Exception e){Fail(e);}}
    static Color32[] Capture(string name){
        var camera=new GameObject("Synthetic pet review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.28f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.65f,.68f,.70f);camera.nearClipPlane=.01f;
        var target=actor.ConfirmedBottomWorld+Vector3.up*.16f;camera.transform.position=target+new Vector3(.7f,.25f,-1);camera.transform.LookAt(target);
        var rt=new RenderTexture(800,800,24);var pixels=new Texture2D(800,800,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,800,800),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+actor.species+"-"+name+".png",pixels.EncodeToPNG());return pixels.GetPixels32();}
        finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
    }
    static void Tick(){
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying){if(SessionState.GetBool(Key+".Done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}return;}
        try{
            if(phase==0){started=EditorApplication.timeSinceStartup;new GameObject("Synthetic pet joint driver").AddComponent<HciSeasonTreeHandPump>().sample=Pump;Later(1);return;}
            Require(errors==0,"Runtime error");Require(EditorApplication.timeSinceStartup-started<120,"Timeout");if(trace!=null||EditorApplication.timeSinceStartup<next)return;
            if(phase==1){controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();Require(controller&&controller.scenario==ParkScenario.Pet,"Saved Pet scene missing");controller.simulation=true;controller.SpawnVirtual();Later(2);}
            else if(phase==2){
                actor=controller.selected;Require(actor&&actor.species==(species==0?"Cat":"Dog"),"Wrong species");anchor=actor.transform.position;
                Require(actor.ConfirmOrientation(0)&&actor.FitRepresentation(anchor,anchor+Vector3.up*.3f,true),"Fit failed");
                fit=actor.transform.Find("Manual fit - separate from tracking and animation");fitPosition=fit.localPosition;fitScale=fit.localScale;
                outline=actor.GetComponentInChildren<NaturalPetOutline>();Require(outline&&outline.ShellCount>0,"Missing hull");shells=outline.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="Manga silhouette").ToArray();Require(shells.Length==outline.ShellCount,"Hull count mismatch");
                foreach(var r in shells){Require(!ShaderUtil.ShaderHasError(r.sharedMaterial.shader),"Outline shader failed");if(r is SkinnedMeshRenderer s){var source=s.transform.parent.GetComponent<SkinnedMeshRenderer>();Require(source&&s.sharedMesh==source.sharedMesh&&s.rootBone==source.rootBone&&s.bones.SequenceEqual(source.bones),"Hull disconnected from imported skeleton");}}
                bubble=actor.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Dream bubble");Check(false,false,0);Later(3);
            }
            else if(phase==3){Capture("00-natural");Require(!controller.Dispatch(ParkAction.Style,anchor,false)&&!controller.Dispatch(ParkAction.Style,anchor+Vector3.right*20,true),"Invalid/far A accepted");Cast(EditDimension.Appearance);Later(4);}
            else if(phase==4){Check(true,false,0);var ink=Capture("01-manga");outline.SetVisible(false);var plain=Capture("01-same-frame-natural");outline.SetVisible(true);int changed=0;for(int i=0;i<ink.Length;i++)if(ink[i].r<40&&ink[i].g<40&&ink[i].b<40&&plain[i].r>ink[i].r+30)changed++;Require(changed>400,"Manga silhouette not visibly rendered: "+changed);File.WriteAllText(Output+"/"+actor.species+"-pixel-check.txt","Same-frame pixels darkened by outline: "+changed+" (minimum 400)\n");ValidateMesh();Cast(EditDimension.Agency);Later(5);}
            else if(phase==5){Check(true,true,0);Capture("02-manga-dream");Cast(EditDimension.Appearance);Later(6);}
            else if(phase==6){Check(false,true,0);Act(ParkAction.Undo);Later(7);}
            else if(phase==7){Check(true,true,0);Cast(EditDimension.Rule);Later(8);}
            else if(phase==8){Check(true,false,0);Cast(EditDimension.Deconstruction);Later(9);}
            else if(phase==9){Check(true,false,1);Act(ParkAction.Undo);Later(10);}
            else if(phase==10){Check(true,false,0);Require(actor.TryPreviewPetWalk(true),"Walk rejected");Later(11,.5);}
            else if(phase==11){Check(true,false,0);Require(actor.PetWalking,"Walk state missing");Capture("03-manga-walk");Require(actor.TryPreviewPetWalk(false),"Idle rejected");holdValid=false;controller.ProcessHandSample(default,false);actor.SetTracked(false);Require(!actor.visual.gameObject.activeInHierarchy&&!controller.Dispatch(ParkAction.Style,anchor,true),"Lost target accepted A");actor.SetTracked(true);Later(12);}
            else if(phase==12){Check(true,false,0);Require(actor.transform.position==anchor&&fit.localPosition==fitPosition&&fit.localScale==fitScale,"Magic moved fit/anchor");Act(ParkAction.Reset);Later(13);}
            else if(phase==13){Check(false,false,0);Require(!actor.PetWalking&&!actor.Busy,"Reset retained motion");Capture("04-reset");if(species++==0){Require(controller.SelectVirtualPet("Dog"),"Dog switch failed");Later(2);}else{File.WriteAllText(Output+"/result.txt","PASS: saved ParkPet, Cat+Dog; synthetic production A/B/C/D joint traces match lighter exactly once; A toggles manga outline independently of B dream/C pop/D affection; Undo preserves independent states; shared imported mesh/bones; winding and same-frame visible-pixel checks; walking outline; lost/far/invalid rejection; stable fit/anchor; Reset; zero runtime errors; twelve Unity camera renders. Editor-only: no real YOLO, XR, PICO, stereo or Android acceptance.\n");SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();}}
        }catch(Exception e){Fail(e);}
    }
    static void ValidateMesh(){
        var lines=new List<string>();var sources=shells.Select(r=>r.transform.parent.GetComponent<Renderer>()).ToArray();
        foreach(var r in sources){var skin=r as SkinnedMeshRenderer;var mesh=new Mesh();if(skin)skin.BakeMesh(mesh);else mesh=UnityEngine.Object.Instantiate(r.GetComponent<MeshFilter>().sharedMesh);
            var v=mesh.vertices;var n=mesh.normals;var t=mesh.triangles;double dot=0,volume=0;int count=0;
            for(int i=0;i<t.Length;i+=3){volume+=Vector3.Dot(v[t[i]],Vector3.Cross(v[t[i+1]],v[t[i+2]]))/6;var face=Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]);if(face.sqrMagnitude<1e-20f)continue;dot+=Vector3.Dot(face.normalized,n[t[i]].normalized);count++;}
            Require((volume<0)==(actor.species=="Cat"),"Accepted mesh winding changed; revalidate outline orientation");
            lines.Add(r.name+" triangles="+t.Length/3+" signedVolume="+volume+" windingNormalDot="+dot/Math.Max(1,count)+" scale="+r.transform.lossyScale+" normals="+n.Length+" sourceBounds="+r.bounds+" hullBounds="+shells[Array.IndexOf(sources,r)].bounds);UnityEngine.Object.DestroyImmediate(mesh);
        }
        File.WriteAllLines(Output+"/"+actor.species+"-mesh-diagnostic.txt",lines);
    }
}
