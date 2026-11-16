using System;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class HciPlantVoiceChecks
{
    const string Key="MagicMR.PlantVoiceChecks";
    static string Output=>Path.GetFullPath(".codex-tmp/hci-plant-voice");
    static ParkController controller;static ParkActor actor;static AudioSource source;
    static Transform mouth;static Vector3 anchor,rest;static int phase,errors,lipSamples;static float maximumLip;
    static double next,started;
    static HciPlantVoiceChecks(){EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};}
    public static void Run(){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);EditorSceneManager.OpenScene("Assets/Scenes/ParkPlant.unity");EditorApplication.EnterPlaymode();}
    static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Later(int p,double wait=1.3){phase=p;next=EditorApplication.timeSinceStartup+wait;}
    static void Cast()=>Require(controller.DispatchDimension(EditDimension.Agency,anchor,true),controller.InteractionStatus);
    static void Quiet(){Require(!actor.PlantVoiceActive&&actor.PlantVoiceMouth==0&&source.clip==null,"Cancelled dialogue retained playback state");}
    static void Capture(string name){
        var camera=Camera.main;var old=camera.targetTexture;var active=RenderTexture.active;
        var rt=new RenderTexture(960,720,24);var pixels=new Texture2D(960,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,960,720),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());}
        finally{camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
    }
    static void Tick(){
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying){if(SessionState.GetBool(Key+".Done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}return;}
        try{
            if(phase==0){started=EditorApplication.timeSinceStartup;Later(1);return;}
            Require(errors==0&&EditorApplication.timeSinceStartup-started<90,"Errors or timeout");
            if(actor){Require(actor.transform.position==anchor,"Speech moved tracking anchor");Require(actor.GetComponents<AudioSource>().Length==1,"Speech created extra AudioSource");
                if(phase==2&&actor.PlantVoiceActive){lipSamples++;maximumLip=Mathf.Max(maximumLip,actor.PlantVoiceMouth);}}
            if(EditorApplication.timeSinceStartup<next)return;
            if(phase==1){controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();Require(controller,"Controller missing");controller.SpawnVirtual();actor=controller.selected;Require(actor&&actor.scenario==ParkScenario.Plant,"Plant missing");anchor=actor.transform.position;source=actor.GetComponent<AudioSource>();mouth=actor.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Plant speaking mouth");rest=mouth.localScale;Quiet();Cast();Later(2,1.3);}
            else if(phase==2){Require(actor.PlantVoiceActive&&source.clip&&source.clip.channels==1&&actor.PlantVoiceDuration>1,"B did not start mono speech");Require(maximumLip>.05f&&lipSamples>2,"Mouth envelope did not animate: "+lipSamples+" / "+maximumLip);Capture("B-speaking");Later(3,actor.PlantVoiceDuration+.2);}
            else if(phase==3){Quiet();Require(mouth.localScale==rest,"Mouth did not close after speech");Capture("B-finished");Cast();Later(4,.5);}
            else if(phase==4){Require(actor.PlantVoiceActive,"Repeated B did not restart");actor.Undo();Quiet();Later(5);}
            else if(phase==5){Cast();Later(6,1.5);}
            else if(phase==6){Require(actor.PlantVoiceActive,"B absent before D");Require(controller.DispatchDimension(EditDimension.Deconstruction,anchor,true),"D rejected: "+controller.InteractionStatus+" / "+actor.Feedback+" busy="+actor.Busy);Later(16,1.3);}
            else if(phase==16){Quiet();Later(7,.1);}
            else if(phase==7){Cast();Later(8,.4);}
            else if(phase==8){actor.SetTracked(false);Quiet();actor.SetTracked(true);Later(9);}
            else if(phase==9){Cast();Later(10,.4);}
            else if(phase==10){actor.SendMessage("OnApplicationPause",true);Quiet();Later(11);}
            else if(phase==11){Cast();Later(12,.4);}
            else if(phase==12){actor.ResetEffects();Quiet();Later(13);}
            else if(phase==13){Cast();Later(14,.4);}
            else if(phase==14){actor.enabled=false;Quiet();File.WriteAllText(Output+"/result.txt","PASS: saved ParkPlant; B speech clip assigned, bounded mono PCM, mouth envelope sampled="+lipSamples+" max="+maximumLip+"; closes at completion; repeated B, Undo, D clear, tracking loss/recovery, pause, Reset and disable stop; single AudioSource; anchor stable; errors=0. Synthetic production dimension dispatch, not physical hand/YOLO/PICO or audible device-output acceptance.\n");SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();}
        }catch(Exception e){SessionState.SetBool(Key,false);File.WriteAllText(Output+"/result.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
