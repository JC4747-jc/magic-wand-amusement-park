#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class HciBugPixelChecks
{
    const string Key="MagicMR.HciBugPixel";
    static string Output=>Path.GetFullPath(".codex-tmp/hci-bug-pixel");
    static readonly List<string> records=new List<string>();
    static ParkController controller;
    static ParkActor actor;
    static NaturalInsectRig rig;
    static Mesh original;
    static Camera camera;
    static Vector3 anchor;
    static Quaternion anchorRotation;
    static Vector3[] bakedRest;
    static Quaternion[] boneRest;
    static int phase,species,errors,stableFrames,frames,watchFrames;
    static double started,next;
    static bool watching;
    static string Species=>species==0?"Cockroach":"Spider";
    static HciBugPixelChecks()
    {
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Assert||t==LogType.Exception))errors++;};
    }
    public static void Run()
    {
        Require(Application.isBatchMode,"Bounded isolated editor required");Directory.CreateDirectory(Output);
        File.WriteAllText(Output+"/result.txt","RUNNING\n");SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);
        EditorSceneManager.OpenScene("Assets/Scenes/ParkBugs.unity",OpenSceneMode.Single);EditorApplication.EnterPlaymode();
    }
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Later(int value,double delay=1.3){phase=value;next=EditorApplication.timeSinceStartup+delay;}
    static float Style(){var b=new MaterialPropertyBlock();rig.Skin.GetPropertyBlock(b);return b.GetFloat("_Style");}
    static void Act(ParkAction action)=>Require(controller.Dispatch(action,anchor,true),action+" rejected: "+controller.InteractionStatus);
    static void Pixel(){Require(Style()>1.99f,"First A must select pixel, actual shader style="+Style());Require(rig.Skin.sharedMesh!=original&&rig.Skin.sharedMesh.name.StartsWith("Pixel"+Species),"Pixel geometry missing");Require(rig.Skin.sharedMesh.boneWeights.Length==rig.Skin.sharedMesh.vertexCount&&rig.Skin.sharedMesh.bindposes.Length==rig.Skin.bones.Length,"Pixel geometry lost skin bindings");}
    static void Natural(){Require(rig.Skin.sharedMesh==original&&Style()<.01f,"Original mesh/material state not restored");}
    static Vector3[] Bake(){var m=new Mesh();try{rig.Skin.BakeMesh(m);return m.vertices;}finally{UnityEngine.Object.Destroy(m);}}
    static void Capture(string name)
    {
        var rt=RenderTexture.GetTemporary(960,640,24);var old=RenderTexture.active;var target=camera.targetTexture;Texture2D image=null;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image=new Texture2D(960,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,640),0,0);image.Apply();File.WriteAllBytes(Output+"/"+Species+"-"+name+".png",image.EncodeToPNG());}
        finally{RenderTexture.active=old;camera.targetTexture=target;if(image)UnityEngine.Object.Destroy(image);RenderTexture.ReleaseTemporary(rt);}
    }
    public static void Sample()
    {
        if(!SessionState.GetBool(Key,false))return;frames++;stableFrames=Time.unscaledDeltaTime<.05f?stableFrames+1:0;
        if(!watching||!actor)return;
        try
        {
            watchFrames++;Require(Vector3.Distance(actor.transform.position,anchor)<.00001f&&Quaternion.Angle(actor.transform.rotation,anchorRotation)<.01f,"Pixel animation moved recognition anchor");
            for(int i=0;i<rig.LegCount;i++)Require(Vector3.Distance(rig.FootLocal(i),rig.FootTarget(i))<.0008f,"Pixel representation broke leg IK");
        }catch(Exception e){Fail(e);}
    }
    static void Fail(Exception e){SessionState.SetBool(Key,false);File.WriteAllLines(Output+"/measurements.txt",records);File.WriteAllText(Output+"/result.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(2);}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;EditorApplication.QueuePlayerLoopUpdate();
        if(!EditorApplication.isPlaying){if(SessionState.GetBool(Key+".Done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}return;}
        try
        {
            if(phase==0){started=EditorApplication.timeSinceStartup;QualitySettings.vSyncCount=0;Application.targetFrameRate=60;new GameObject("Pixel model observation").AddComponent<HciBugPixelProbe>();camera=new GameObject("Pixel fixed evidence camera").AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.18f,.21f);Later(1,.6);return;}
            Require(EditorApplication.timeSinceStartup-started<100,"Pixel lifecycle timed out");if(EditorApplication.timeSinceStartup<next)return;
            if(phase==1){controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();Require(controller&&controller.scenario==ParkScenario.Bugs,"Wrong saved scene");controller.simulation=true;controller.ResetScene();controller.SpawnVirtual(Species);Later(2,.6);}
            else if(phase==2)
            {
                actor=controller.selected;Require(actor&&actor.species==Species,"Wrong variant");rig=actor.GetComponentInChildren<NaturalInsectRig>();rig.Initialize();original=rig.Skin.sharedMesh;
                anchor=actor.transform.position;Require(actor.ConfirmOrientation(0)&&actor.FitRepresentation(anchor,anchor+Vector3.up*.08f,true),"Fit rejected");anchorRotation=actor.transform.rotation;
                if(actor.label)actor.label.gameObject.SetActive(false);foreach(var t in actor.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                var b=rig.Skin.bounds;camera.cullingMask=1<<31;camera.orthographic=true;camera.orthographicSize=Mathf.Max(.05f,b.extents.magnitude*1.35f+.055f);camera.transform.position=b.center+new Vector3(.15f,.19f,-.22f).normalized*1.5f;camera.transform.LookAt(b.center);camera.nearClipPlane=.001f;camera.farClipPlane=5;
                Capture("00-natural");stableFrames=0;Later(3,0);
            }
            else if(phase==3){if(stableFrames<12)return;Natural();Act(ParkAction.Style);Later(4);}
            else if(phase==4)
            {
                if(actor.Busy||Style()<1.99f)return;Capture("01-first-A");Pixel();bakedRest=Bake();boneRest=rig.Skin.bones.Select(t=>t.localRotation).ToArray();watchFrames=0;watching=true;Act(ParkAction.Awaken);Later(5,0);
            }
            else if(phase==5)
            {
                if(!actor.Busy)return;
                float legMotion=0;for(int i=0;i<boneRest.Length;i++)if(rig.Skin.bones[i].name.StartsWith("Leg_"))legMotion=Mathf.Max(legMotion,Quaternion.Angle(boneRest[i],rig.Skin.bones[i].localRotation));
                if(legMotion<1||actor.visual.localPosition.sqrMagnitude<.0001f)return;
                Pixel();var pose=Bake();Require(pose.Length==bakedRest.Length,"Pixel mesh changed mid-animation");float movement=0;for(int i=0;i<pose.Length;i++)movement=Mathf.Max(movement,Vector3.Distance(pose[i],bakedRest[i]));Require(movement>.0001f,"Skinned pixel mesh did not deform");
                Capture("02-pixel-B");records.Add(Species+": pixelVertices="+pose.Length+"; movingLegDegrees="+legMotion+"; bakedVertexMotion="+movement);Later(6,0);
            }
            else if(phase==6){if(actor.Busy)return;watching=false;Require(watchFrames>10,"Insufficient pixel animation frames");Act(ParkAction.Undo);Pixel();Act(ParkAction.Style);Later(7);}
            else if(phase==7){if(actor.Busy||Style()>.01f)return;Natural();Capture("03-natural-restored");Act(ParkAction.Style);Later(8);}
            else if(phase==8){if(actor.Busy||Style()<1.99f)return;Pixel();Act(ParkAction.Undo);Later(9,.1);}
            else if(phase==9){Require(rig.Skin.sharedMesh==original,"Undo retained pixel geometry");if(Style()>.01f)return;Natural();Act(ParkAction.Style);Later(10);}
            else if(phase==10){if(actor.Busy||Style()<1.99f)return;Pixel();Act(ParkAction.Reset);Later(11,.1);}
            else if(phase==11)
            {
                Natural();records.Add(Species+": first-A pixel, B actual skinning/IK/anchor, toggle, Undo and Reset restored original shared mesh; observationFrames="+watchFrames);
                if(species++==0){Later(1,.6);return;}Require(errors==0,"Unity errors: "+errors);File.WriteAllLines(Output+"/measurements.txt",records);
                File.WriteAllText(Output+"/result.txt","PASS: saved ParkBugs two species, first-A skinned pixel geometry, production B animation/IK/anchor, toggle/Undo/Reset; synthetic action dispatch, no physical YOLO/gesture/PICO claim.\n");SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();
            }
        }catch(Exception e){Fail(e);}
    }
}
[DefaultExecutionOrder(20000)] public sealed class HciBugPixelProbe:MonoBehaviour {void LateUpdate()=>HciBugPixelChecks.Sample();}
#endif
