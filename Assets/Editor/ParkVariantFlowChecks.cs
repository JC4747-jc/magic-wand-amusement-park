using System;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Actual saved scenes/cameras/components and generated Button callbacks.
// Placement, tracking and defeat inputs are explicitly synthetic test input.
[InitializeOnLoad]
public static class ParkVariantFlowChecks
{
    const string Key="MagicMR.VariantFlow";
    static readonly string[] Scenes={"ParkBugs","ParkTree","ParkStatue"};
    static readonly string Output=Path.GetFullPath(".codex-tmp/variant-flow");
    static Camera camera;static ParkController controller;static ParkSharedInterface ui;static ParkActor actor,old;
    static Quaternion bronzeRestHead;
    static int stage,errors,captures;static double started,next;static Vector3 position,fitPosition,fitScale,cameraPosition;static Quaternion rotation,cameraRotation;static float fov;static bool automatic;
    static ParkVariantFlowChecks(){EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    static void Log(string text,string trace,LogType type){if(SessionState.GetBool(Key,false)&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))errors++;}
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"result.txt"),"RUNNING\n");
        SessionState.SetString(Key+".Previous",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);SessionState.SetInt(Key+".Index",0);SessionState.SetString(Key+".Phase","open");SessionState.SetBool(Key,true);
    }
    static void Click(string name)
    {
        var button=camera.GetComponentsInChildren<Button>(false).Single(b=>b.name==name);var rect=button.GetComponent<RectTransform>();var center=rect.TransformPoint(rect.rect.center);
        Require(button.interactable&&ParkSharedInterface.TryButtonRayHit(rect,new Ray(camera.transform.position,center-camera.transform.position),out _),"Variant button unreachable: "+name);button.onClick.Invoke();
    }
    static void Later(int value,double delay){stage=value;next=EditorApplication.timeSinceStartup+delay;}
    static void RememberRoot(){position=actor.transform.position;rotation=actor.transform.rotation;}
    static void FixedRoot(){Require(actor.transform.position==position&&actor.transform.rotation==rotation,"Variant interaction moved tracking root");}
    static Transform Fit=>actor.transform.Find("Manual fit - separate from tracking and animation");
    static Transform Scar=>actor.visual.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Virtual scar");
    static void FitRetained(){FixedRoot();Require(actor.sizeConfirmed&&actor.orientationConfirmed&&Fit.localPosition==fitPosition&&Fit.localScale==fitScale,"Tree mode reset manual placement");}
    static void OnlyVisible(string[] expected)
    {
        string[] all={"Preview Cat","Preview Dog","Walk in place","Cockroach","Spider","Test defeat","Natural tree","Fantasy tree","Test awaken","Marble statue","Bronze beast"};
        var active=camera.GetComponentsInChildren<Button>(false).Select(b=>b.name).Where(n=>all.Contains(n)).OrderBy(n=>n).ToArray();Require(active.SequenceEqual(expected.OrderBy(n=>n)),"Variant controls leaked into wrong profile/mode");
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        try
        {
            int index=SessionState.GetInt(Key+".Index",0);
            if(!EditorApplication.isPlaying)
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                if(SessionState.GetString(Key+".Phase","")=="next")
                {
                    index++;SessionState.SetInt(Key+".Index",index);SessionState.SetString(Key+".Phase","open");if(index==Scenes.Length){Finish();return;}
                }
                if(SessionState.GetString(Key+".Phase","")!="open")return;
                stage=errors=captures=0;actor=old=null;EditorSceneManager.OpenScene("Assets/Scenes/"+Scenes[index]+".unity");SessionState.SetString(Key+".Phase","playing");EditorApplication.EnterPlaymode();return;
            }
            if(stage==0)
            {
                started=EditorApplication.timeSinceStartup;camera=Camera.main;controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();ui=UnityEngine.Object.FindFirstObjectByType<ParkSharedInterface>();
                Require(camera&&controller&&ui&&ui.controller==controller&&!controller.simulation&&!ui.replayMode,"Variant scene wiring invalid");
                cameraPosition=camera.transform.position;cameraRotation=camera.transform.rotation;fov=camera.fieldOfView;automatic=ui.Selection.automatic;Application.targetFrameRate=60;Later(1,1.2);return;
            }
            Require(EditorApplication.timeSinceStartup-started<70,"Variant flow timed out: "+Scenes[index]+" stage="+stage);
            Require(camera.transform.position==cameraPosition&&camera.transform.rotation==cameraRotation&&camera.fieldOfView==fov,"Variant flow changed original camera");
            if(EditorApplication.timeSinceStartup<next)return;
            if(index==0)Bugs();else if(index==1)Tree();else Statue();
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Path.Combine(Output,"result.txt"),"FAIL: "+error);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
    }
    static void Bugs()
    {
        switch(stage)
        {
            case 1:Click("Virtual test");actor=controller.selected;Require(actor&&actor.species=="Cockroach","Default virtual bug changed");Later(2,.8);break;
            case 2:
                OnlyVisible(new[]{"Cockroach","Spider","Test defeat"});Require(!controller.SelectVirtualBug("Dog")&&!controller.SelectVirtualPet("Cat"),"Invalid/cross-profile variant accepted");Capture("Cockroach");
                old=actor;Click("Spider");actor=controller.selected;Require(actor&&actor!=old&&actor.species=="Spider"&&old.IsExiting,"Spider UI failed to replace cockroach");Later(3,.8);break;
            case 3:
                Require(!old&&controller.InstanceCount==1,"Bug switch leaked actor");RememberRoot();Capture("Spider");Click("Test defeat");Require(actor.Busy,"Defeat UI did not start animation");Click("Cockroach");Require(controller.selected==actor,"Species switched during defeat");Later(4,1.3);break;
            case 4:
                FixedRoot();Require(!actor.Busy,"Defeat never finished");int revision=actor.Revision;Click("Test defeat");Require(!actor.Busy&&actor.Revision==revision&&ui.Feedback.Contains("Already defeated"),"Defeat awarded twice");Capture("Defeated");
                actor.Undo();Require(actor.GetComponentsInChildren<SkinnedMeshRenderer>().All(r=>r.enabled),"Undo did not restore spider");Capture("Spider-Undo");old=actor;Click("Cockroach");actor=controller.selected;Later(5,.8);break;
            case 5:
                Require(!old&&actor.species=="Cockroach","Cockroach callback did not restore species");old=actor;Click("Reset");Click("Spider");actor=controller.selected;Require(actor&&actor.species=="Spider","Reset left no way to respawn spider");Later(6,.8);break;
            case 6:
                Require(!old&&controller.InstanceCount==1,"Bug respawn leaked old actor");if(ui.Selection.automatic)Click("Auto");Click("Replay");Later(7,.2);break;
            case 7:
                OnlyVisible(Array.Empty<string>());ui.Command("Spider");ui.Command("Test defeat");Require(controller.InstanceCount==0,"Replay accepted live variant controls");Click("Virtual test");Later(8,.8);break;
            case 8:
                Require(controller.selected&&controller.selected.species=="Cockroach"&&!ui.replayMode,"Replay boundary lost default bug");Click("Virtual test");Later(9,.8);break;
            case 9:
                OnlyVisible(Array.Empty<string>());Require(!controller.SelectVirtualBug("Spider"),"Live mode accepted virtual bug API");ui.Command("Spider");Require(controller.InstanceCount==0,"Live command spawned virtual spider");CompleteScene();break;
        }
    }
    static void Tree()
    {
        switch(stage)
        {
            case 1:Click("Virtual test");actor=controller.selected;Require(actor&&!actor.magicTree,"Default tree mode changed");Later(2,.8);break;
            case 2:
                OnlyVisible(new[]{"Natural tree","Fantasy tree","Test awaken"});Require(actor.ConfirmOrientation(37),"Tree orientation confirmation failed");RememberRoot();
                Click("Fit");Click("Fantasy tree");Require(!actor.magicTree&&controller.FitConfirmationPending,"Tree mode switched during Fit");
                var bottom=actor.transform.position+Vector3.up*.01f;Require(controller.ConfirmFit(bottom,true)&&controller.ConfirmFit(bottom+Vector3.up*.35f,true),"Tree fit failed");fitPosition=Fit.localPosition;fitScale=Fit.localScale;
                Click("Plane");Require(controller.ConfirmManualPlane(actor.transform.position+Vector3.up*.6f,true)&&actor.environmentAnchor,"Tree plane fixture failed");Capture("Natural");Click("Fantasy tree");Require(actor.magicTree&&!actor.confirmedPlane&&!actor.environmentAnchor,"Tree mode retained stale surface");FitRetained();Later(3,.2);break;
            case 3:
                Require(!controller.GetComponentsInChildren<Transform>(true).Any(t=>t.name.StartsWith("Manual horizontal surface -")),"Tree mode leaked surface owner");
                Require(!Scar.gameObject.activeSelf,"Fantasy tree started with stale scar");Click("Test awaken");Require(actor.Busy,"Tree awakening callback failed");Later(4,.48);break;
            case 4:
                Require(actor.Busy&&Scar.gameObject.activeSelf,"Fantasy tree failed to reveal scar during action");
                var motes=actor.visual.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("Pooled feedback ")).ToArray();Require(motes.Length==6&&motes.All(f=>f.sharedMesh==ParkFeedbackMeshes.Get(ParkFeedbackShape.Mote)&&f.transform.localScale.x<=.0091f),"Tree feedback exceeded six small warm motes");
                Capture("Fantasy-Scar");Click("Natural tree");Require(!actor.magicTree&&!actor.Busy&&!Scar.gameObject.activeSelf,"Mode switch did not interrupt/clear fantasy");FitRetained();actor.Undo();Require(!Scar.gameObject.activeSelf,"Mode switch kept old undo history");Capture("Mode-Reset");Later(5,.4);break;
            case 5:Click("Test awaken");Later(6,1.3);break;
            case 6:
                Require(!actor.Busy&&!Scar.gameObject.activeSelf,"Natural tree gained fantasy scar");FitRetained();Capture("Natural-Awakened");actor.SetTracked(false);Require(!controller.SetVirtualTreeMode(true),"Lost tree accepted mode switch");actor.SetTracked(true);
                actor.virtualTarget=false;Require(!controller.SetVirtualTreeMode(true),"Physical tree accepted virtual mode");actor.virtualTarget=true;
                old=actor;Click("Reset");Click("Fantasy tree");actor=controller.selected;Require(actor&&actor.magicTree,"Reset left no way to respawn fantasy tree");Later(7,.8);break;
            case 7:Require(!old&&controller.InstanceCount==1&&!Scar.gameObject.activeSelf,"Tree respawn leaked old mode/actor");Click("Test awaken");Later(8,1.3);break;
            case 8:
                Require(Scar.gameObject.activeSelf&&Scar.parent.name=="FantasyScarAnchor"&&actor.TreeScarWorldPosition==Scar.position,"Respawned tree lost its surface-bound touch target");RememberRoot();
                Require(controller.Dispatch(ParkAction.Poke,actor.TreeScarWorldPosition,true),"Synthetic soothe action refused bark target");Later(80,1.3);break;
            case 80:
                FixedRoot();Require(!Scar.gameObject.activeSelf,"Soothe did not hide bark mark");actor.Undo();Require(Scar.gameObject.activeSelf,"Undo did not restore bark mark");
                if(ui.Selection.automatic)Click("Auto");Click("Replay");Later(9,.2);break;
            case 9:
                OnlyVisible(Array.Empty<string>());ui.Command("Fantasy tree");Require(controller.InstanceCount==0,"Replay mode accepted tree mode command");Click("Virtual test");Later(10,.8);break;
            case 10:Require(controller.selected&&!controller.selected.magicTree,"Virtual restart kept previous tree mode");Click("Virtual test");Later(11,.8);break;
            case 11:
                OnlyVisible(Array.Empty<string>());Require(!controller.SetVirtualTreeMode(true),"Live mode accepted tree preview API");ui.Command("Fantasy tree");Require(controller.InstanceCount==0,"Live command spawned fantasy tree");CompleteScene();break;
        }
    }
    static void Statue()
    {
        switch(stage)
        {
            case 1:Click("Virtual test");actor=controller.selected;Require(actor&&actor.species=="Statue"&&!actor.GetComponentInChildren<NaturalBronzeBeast>(),"Default marble statue changed");Later(2,.8);break;
            case 2:
                OnlyVisible(new[]{"Marble statue","Bronze beast","Test awaken"});Require(!controller.SelectVirtualStatue("Cat")&&!controller.SelectVirtualPet("Dog"),"Invalid/cross-profile statue accepted");Capture("Marble");
                old=actor;Click("Bronze beast");actor=controller.selected;Require(actor&&actor!=old&&actor.species=="BronzeBeast"&&old.IsExiting,"Bronze callback did not replace marble");Later(3,.8);break;
            case 3:
                Require(!old&&controller.InstanceCount==1&&actor.GetComponentInChildren<NaturalBronzeBeast>(),"Bronze variant missing or leaked old actor");
                Require(actor.ConfirmOrientation(37),"Bronze orientation fixture failed");RememberRoot();Click("Fit");Click("Marble statue");Require(controller.selected==actor&&controller.FitConfirmationPending,"Statue switched during Fit");
                var bottom=actor.transform.position+Vector3.up*.01f;Require(controller.ConfirmFit(bottom,true)&&controller.ConfirmFit(bottom+Vector3.up*.35f,true),"Bronze manual Fit failed");fitPosition=Fit.localPosition;fitScale=Fit.localScale;
                bronzeRestHead=actor.GetComponentInChildren<NaturalBronzeBeast>().Head.localRotation;Capture("Bronze");Click("Test awaken");Require(actor.Busy,"Bronze awaken callback failed");Click("Marble statue");Require(controller.selected==actor,"Statue switched during awakening");Later(4,.5);break;
            case 4:
                FitRetained();var bronze=actor.GetComponentInChildren<NaturalBronzeBeast>();Require(actor.Busy&&bronze.Blend>0&&Quaternion.Angle(bronze.Head.localRotation,bronzeRestHead)>.05f,"Bronze bone did not move in actual Play Mode");Capture("Bronze-Scan");Later(5,1.2);break;
            case 5:
                FitRetained();Require(!actor.Busy&&actor.visual.localScale==Vector3.one&&actor.visual.localRotation==Quaternion.identity,"Bronze pedestal breathed/stretched/rocked");Capture("Bronze-Awakened");
                actor.Undo();Require(actor.GetComponentInChildren<NaturalBronzeBeast>().Blend==0,"Bronze Undo retained head motion");Capture("Bronze-Undo");Click("Test awaken");Later(6,1.3);break;
            case 6:Require(actor.GetComponentInChildren<NaturalBronzeBeast>().Blend>0,"Second bronze awakening failed");Click("Stop");Later(7,.5);break;
            case 7:
                Require(!actor.Busy&&actor.GetComponentInChildren<NaturalBronzeBeast>().Blend==0,"Bronze Stop retained motion");actor.SetTracked(false);Require(actor.GetComponentInChildren<NaturalBronzeBeast>(true).Blend==0,"Lost bronze retained motion");actor.SetTracked(true);
                actor.virtualTarget=false;Require(!controller.SelectVirtualStatue("Statue"),"Physical target accepted statue preview API");ui.Command("Marble statue");Require(controller.selected==actor,"Physical target accepted statue UI");actor.virtualTarget=true;
                old=actor;Click("Marble statue");actor=controller.selected;Later(8,.8);break;
            case 8:
                Require(!old&&actor.species=="Statue"&&!actor.GetComponentInChildren<NaturalBronzeBeast>(),"Marble switch retained bronze rig");old=actor;Click("Reset");Click("Bronze beast");actor=controller.selected;Require(actor&&actor.species=="BronzeBeast","Reset could not respawn bronze");Later(9,.8);break;
            case 9:Require(!old&&controller.InstanceCount==1,"Bronze respawn leaked actor");if(ui.Selection.automatic)Click("Auto");Click("Replay");Later(10,.2);break;
            case 10:
                OnlyVisible(Array.Empty<string>());ui.Command("Bronze beast");Require(controller.InstanceCount==0,"Replay spawned bronze variant");Click("Virtual test");Later(11,.8);break;
            case 11:Require(controller.selected&&controller.selected.species=="Statue"&&!ui.replayMode,"Replay boundary lost marble default");Click("Virtual test");Later(12,.8);break;
            case 12:
                OnlyVisible(Array.Empty<string>());Require(!controller.SelectVirtualStatue("BronzeBeast"),"Live mode accepted bronze preview API");ui.Command("Bronze beast");Require(controller.InstanceCount==0,"Live command spawned bronze");CompleteScene();break;
        }
    }
    static void CompleteScene()
    {
        Require(UnityEngine.Object.FindObjectsByType<ParkActor>(FindObjectsSortMode.None).Length==0&&UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length==0&&errors==0,"Variant scene leaked actor/audio or runtime errors="+errors);
        if(ui.Selection.automatic!=automatic)Click("Auto");string scene=Scenes[SessionState.GetInt(Key+".Index",0)];
        File.WriteAllText(Path.Combine(Output,scene+".txt"),"PASS: actual saved scene and original camera/input, actual Button callbacks and ray geometry; virtual variants, action/mode interruption, gates and cleanup; "+captures+" scene captures. Synthetic placement/tracking/actions only; no native/pointer/gesture/device acceptance.\n");
        Debug.Log("PARK_VARIANT_SCENE_PASS "+scene+" captures="+captures+" errors="+errors);SessionState.SetString(Key+".Phase","next");EditorApplication.ExitPlaymode();
    }
    static void Finish()
    {
        SessionState.SetBool(Key,false);string previous=SessionState.GetString(Key+".Previous","");if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);
        File.WriteAllText(Path.Combine(Output,"result.txt"),"PASS: actual ParkBugs/ParkTree/ParkStatue variant buttons, species/defeat/Undo, retained manual fit/yaw, surface ownership, bronze fixed pedestal/head animation/Stop, physical/lost/replay/live/calibration gates, Reset respawn and actor/audio cleanup. No physical pointer/gesture/headset acceptance.\n");
        Debug.Log("PARK_VARIANT_FLOW_PASS: three actual scenes; explicit simulation entry, lifetime and placement gates");EditorApplication.Exit(0);
    }
    static void Capture(string name)
    {
        var target=new RenderTexture(1000,800,24);var pixels=new Texture2D(1000,800,TextureFormat.RGB24,false);var oldTarget=RenderTexture.active;
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1000,800),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(Output,Scenes[SessionState.GetInt(Key+".Index",0)]+"-"+name+".png"),pixels.EncodeToPNG());captures++;}
        finally{camera.targetTexture=null;RenderTexture.active=oldTarget;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
    }
}
