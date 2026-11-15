using System;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class HciPlantFlowChecks
{
    const string Key="MagicMR.HciPlantFlow";
    static string Output=>Path.GetFullPath(".codex-tmp/hci-plant-flow");
    static ParkController controller;
    static ParkActor actor;
    static Transform fit,selection;
    static TextMesh speech;
    static Vector3 fitPosition,fitScale,anchor;
    static int stage,errors;
    static double next,started;
    static HciPlantFlowChecks(){EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    public static void Run(){
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Idle editor required");
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);
        SessionState.SetString(Key+".Run",Guid.NewGuid().ToString("N"));
        EditorSceneManager.OpenScene("Assets/Scenes/ParkPlant.unity");EditorApplication.EnterPlaymode();
    }
    static void Log(string message,string stack,LogType type){if(SessionState.GetBool(Key,false)&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))errors++;}
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Later(int value,double seconds=1.5){stage=value;next=EditorApplication.timeSinceStartup+seconds;}
    static void Cast(EditDimension dimension)=>Require(controller.DispatchDimension(dimension,actor.transform.position,true),dimension+": "+controller.InteractionStatus);
    static void Act(ParkAction action)=>Require(controller.Dispatch(action,actor.transform.position,true),action+": "+controller.InteractionStatus);
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
            if(stage==0){started=EditorApplication.timeSinceStartup;controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();Require(controller&&controller.scenario==ParkScenario.Plant,"Saved scene missing");Camera.main.GetComponentsInChildren<Button>(false).Single(b=>b.name=="Virtual test").onClick.Invoke();Later(1);return;}
            Require(EditorApplication.timeSinceStartup-started<90,"Timeout");Require(errors==0,"Runtime error");
            if(EditorApplication.timeSinceStartup<next)return;
            if(stage==1){
                actor=controller.selected;Require(actor&&actor.virtualTarget,"Virtual plant absent");
                Require(actor.ConfirmOrientation(0)&&actor.FitRepresentation(actor.transform.position,actor.transform.position+Vector3.up*.35f,true),"Synthetic fit failed");
                fit=actor.transform.Find("Manual fit - separate from tracking and animation");fitPosition=fit.localPosition;fitScale=fit.localScale;anchor=actor.transform.position;
                selection=fit.Find("Selected virtual target highlight");speech=actor.GetComponentsInChildren<TextMesh>(true).Single(t=>t.name=="Virtual plant dialogue");
                Require(!speech.gameObject.activeInHierarchy,"Dialogue visible before B");
                Require(!controller.DispatchDimension(EditDimension.Scale,anchor,true),"Unsupported dimension accepted");
                Require(!controller.DispatchDimension(EditDimension.Appearance,anchor,false),"Invalid hand accepted");
                Cast(EditDimension.Appearance);Later(2);
            }
            else if(stage==2){Require(selection.gameObject.activeInHierarchy&&!actor.PlantBloomVisible,"A failed selection or wrongly bloomed");Capture("A-select");Cast(EditDimension.Agency);Later(3);}
            else if(stage==3){Require(speech.gameObject.activeInHierarchy&&speech.text.Contains("Virtual plant dialogue"),"B dialogue absent/unlabelled");Require(actor.visual.Find("Eyes").GetComponentsInChildren<Renderer>().Length==5,"B face missing");Capture("B-dialogue");Cast(EditDimension.Rule);Later(4,3.4);}
            else if(stage==4){Require(Mathf.Abs(actor.growthTime-1)<.001f,"C did not reach grown state");Capture("C-grown");Cast(EditDimension.Rule);Later(5,3.4);}
            else if(stage==5){Require(actor.growthTime<.001f,"Second C did not reach seedling");Capture("C-seedling");Act(ParkAction.Bloom);Later(6);}
            else if(stage==6){Require(actor.PlantBloomVisible,"Extra bloom broken");Cast(EditDimension.Deconstruction);Later(7,2.2);}
            else if(stage==7){Require(!speech.gameObject.activeInHierarchy&&!actor.PlantBloomVisible&&Mathf.Abs(actor.growthTime-.45f)<.001f,"D did not clear dialogue/bloom/growth");Require(controller.selected==actor&&selection.gameObject.activeInHierarchy,"D lost selected target navigation");Require(actor.sizeConfirmed&&actor.orientationConfirmed&&fit.localPosition==fitPosition&&fit.localScale==fitScale,"D erased placement");Capture("D-clear");Act(ParkAction.Undo);Later(8,1);}
            else if(stage==8){Require(speech.gameObject.activeInHierarchy&&actor.PlantBloomVisible&&actor.growthTime<.001f,"Undo D did not restore state");Cast(EditDimension.Agency);Act(ParkAction.Reset);Later(9,1);}
            else if(stage==9){
                Require(!speech.gameObject.activeInHierarchy&&!actor.PlantBloomVisible&&!actor.Busy,"Mid-action Reset leaked effects");
                actor.SetTracked(false);Require(!controller.DispatchDimension(EditDimension.Agency,anchor,true),"Lost target accepted");actor.SetTracked(true);
                Require(actor.transform.position==anchor,"Magic moved anchor");
                File.WriteAllText(Output+"/result.txt","PASS "+SessionState.GetString(Key+".Run","")+": saved ParkPlant; A select/no bloom; B labelled dialogue + five face renderers; C grown/seedling; extra Bloom; D clears with fit/orientation retained; Undo D; mid-action Reset; invalid/unsupported/lost gates; anchor fixed; errors=0. Synthetic shared dimension API; physical gestures, audio speech and PICO not validated.\n");
                SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();
            }
            if(actor&&stage>1&&stage<9)Require(actor.visual.localScale==Vector3.one&&actor.transform.position==anchor,"Plant presentation moved/scaled pot");
        }catch(Exception e){SessionState.SetBool(Key,false);File.WriteAllText(Output+"/result.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
