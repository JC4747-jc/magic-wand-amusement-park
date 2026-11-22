using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class NaturalPetPlayChecks
{
    const string Key="MagicMR.PetPlay.Active";
    static readonly List<ParkActor> actors=new List<ParkActor>();
    static readonly List<GameObject> testHeads=new List<GameObject>();
    static Camera camera;
    static ParkAnimationFrameProbe probe;
    static float phase;
    static double started;
    static int stage;
    static readonly string Output=Path.GetFullPath(".codex-tmp/natural-pet-play");
    static NaturalPetPlayChecks(){EditorApplication.update+=Tick;}
    public static void RunAll()
    {
        BicolorCatAssetBuilder.Install();NaturalDogAssetBuilder.Install();ProjectSceneAudit.Run();ParkNaturalModelChecks.Render();Run();
    }
    public static void RunAllWithReview(){SessionState.SetBool(Key+".Review",true);RunAll();}
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Pet check requires idle editor");
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"samples.txt"),"");
        SessionState.SetString(Key+".Scene",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        SessionState.SetInt(Key+".Exit",-1);SessionState.SetBool(Key,true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.55f,.55f);
        var light=new GameObject("Pet key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(38,-35,0);
        camera=new GameObject("Pet lifecycle camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.nearClipPlane=.01f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.20f,.22f,.24f);camera.gameObject.AddComponent<AudioListener>();
        EditorApplication.EnterPlaymode();
    }
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static Transform Bubble(ParkActor actor)=>actor.visual.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Dream bubble");
    static void Content(ParkActor actor,int index)
    {
        var bubble=Bubble(actor);var contents=bubble.Cast<Transform>().Where(t=>t.name=="Virtual fish"||t.name=="Virtual bone"||t.name.StartsWith("Dream ")&&t.name!="Dream bubble outline").ToArray();
        Require(contents.Length==3&&contents.Count(t=>t.gameObject.activeSelf)==1&&contents[index].gameObject.activeSelf,"Dream selection incorrect: "+actor.species);
        Require(bubble.gameObject.activeSelf&&bubble.localScale.x>.19f,"Dream bubble failed to finish opening");
    }
    static void Action(ParkAction action){foreach(var a in actors)Require(a.TryAction(action,a.transform.position,true),a.species+" rejected "+action+": "+a.Feedback);}
    static void Next(){stage++;phase=probe.SimulatedSeconds;}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying)
        {
            int exit=SessionState.GetInt(Key+".Exit",-1);if(exit<0)return;SessionState.SetBool(Key,false);
            string previous=SessionState.GetString(Key+".Scene","");if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);
            if(exit==0&&SessionState.GetBool(Key+".Review",false))RiggedPetVisualReview.Run();else EditorApplication.Exit(exit);return;
        }
        try
        {
            if(stage==0)
            {
                camera=Camera.main;probe=camera.gameObject.AddComponent<ParkAnimationFrameProbe>();started=EditorApplication.timeSinceStartup;
                for(int i=0;i<2;i++)
                {
                    var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/ParkPet.prefab"));
                    var actor=go.GetComponent<ParkActor>();actor.species=i==0?"Cat":"Dog";actor.virtualTarget=true;actor.cooldown=0;actor.transitionSeconds=.3f;
                    go.transform.position=new Vector3(i*1.5f,0,0);go.SetActive(true);actor.Build();if(actor.label)actor.label.gameObject.SetActive(false);actors.Add(actor);
                }
                foreach(var a in actors)Require(a.GetComponentInChildren<NaturalPetRig>(),a.species+" scene still uses primitive anatomy");
                Next();return;
            }
            Require(EditorApplication.timeSinceStartup-started<75,"Pet Play Mode timed out");
            foreach(var a in actors)if(a)Require(a.transform.position==new Vector3(a.species=="Cat"?0:1.5f,0,0)&&a.transform.rotation==Quaternion.identity,"Pet action moved tracking anchor");else Require(stage==12,"Pet disappeared before exit");
            float elapsed=probe.SimulatedSeconds-phase;
            if(elapsed<(stage==1?.5f:stage==10||stage==12?.45f:.95f))return;
            switch(stage)
            {
                case 1:
                    foreach(var a in actors)
                    {
                        Require(a.EntryProgress>.99f&&!a.TryAction(ParkAction.Feed,a.transform.position,true),"Closed dream accepted feed");
                        Require(!a.TryAction(ParkAction.Pull,new Vector3(float.NaN,0,0),true)&&!a.TryAction(ParkAction.Pull,a.transform.position,false),"Invalid gesture accepted");
                    }
                    Action(ParkAction.Pull);break;
                case 2:
                    foreach(var a in actors)
                    {
                        Content(a,0);Capture(a,"Food");a.virtualTarget=false;
                        Require(!a.TryAction(ParkAction.Feed,a.transform.position,true)&&!a.TryAction(ParkAction.Poke,a.transform.position,true),"Unverified physical head accepted action");
                        a.reliableHeadPose=true;
                        Require(!a.TryAction(ParkAction.Feed,a.transform.position,true),"Head flag without anchor accepted physical feeding");
                        a.reliableHeadPose=false;a.virtualTarget=true;
                    }
                    foreach(var pet in actors){var rig=pet.GetComponentInChildren<NaturalPetRig>();Require(Vector3.Distance(Bubble(pet).position,rig.Head.position+Vector3.up*.12f)<.002f,"Virtual dream is detached from model head: "+pet.species);}
                    Action(ParkAction.Style);break;
                case 3:foreach(var a in actors){Content(a,1);Capture(a,"Star");}Action(ParkAction.Style);break;
                case 4:foreach(var a in actors){Content(a,2);Capture(a,"Heart");}Action(ParkAction.Style);break;
                case 5:
                    foreach(var a in actors)
                    {
                        Content(a,0);var head=new GameObject("Synthetic verified head - test only");testHeads.Add(head);
                        head.transform.position=a.transform.position+Vector3.up*.3f;a.headAnchor=head.transform;a.reliableHeadPose=true;a.virtualTarget=false;
                    }
                    Action(ParkAction.Feed);break;
                case 6:
                    foreach(var a in actors)Require(a.GetComponentInChildren<NaturalPetRig>().Animator.GetCurrentAnimatorStateInfo(0).IsName("Brief sniff"),"Accepted feed did not play sniff: "+a.species);
                    foreach(var a in actors){Require(a.Affection==1,"Feed did not increment virtual affection exactly once");Capture(a,"Feed");a.virtualTarget=true;a.Undo();Require(a.Affection==0,"Undo retained affection");}
                    Action(ParkAction.Poke);break;
                case 7:
                    foreach(var a in actors){Require(!Bubble(a).gameObject.activeSelf,"Poke left dream bubble visible");a.Undo();}
                    break;
                case 8:foreach(var a in actors)Content(a,0);Action(ParkAction.Reward);break;
                case 9:
                    foreach(var a in actors){Require(a.Affection==1&&!Bubble(a).gameObject.activeSelf,"Pet reward state incorrect");Capture(a,"Reward");}
                    Action(ParkAction.Stop);break;
                case 10:
                    foreach(var a in actors){Require(!a.Busy&&!Bubble(a).gameObject.activeSelf,"Stop retained fantasy");a.ResetEffects();Require(a.Affection==0&&!a.reliableHeadPose&&!a.headAnchor,"Reset retained pet state");}
                    Action(ParkAction.Pull);break;
                case 11:
                    foreach(var a in actors)
                    {
                        Content(a,0);a.reliableHeadPose=true;a.headAnchor=testHeads[actors.IndexOf(a)].transform;
                        a.SetTracked(false);Require(!a.Busy&&!a.visual.gameObject.activeSelf&&!a.reliableHeadPose&&!a.headAnchor,"Tracking loss retained presentation or stale head proof");
                        Require(!a.TryAction(ParkAction.Feed,a.transform.position,true),"Lost pet accepted feed");
                        a.SetTracked(true);Require(a.visual.gameObject.activeSelf&&Bubble(a).gameObject.activeSelf&&a.Affection==0,"Recovery discarded chosen dream/affection");
                        a.EndHandInteraction();a.ExitAndDestroy();
                    }
                    foreach(var head in testHeads)UnityEngine.Object.Destroy(head);break;
                case 12:
                    Require(actors.All(a=>!a)&&UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length==0,"Pet exit leaked actor/audio");
                    File.WriteAllText(Path.Combine(Output,"result.txt"),"PASS: natural Cat/Wolfhound actual Play Mode entry; food/star/heart follow each imported head; physical-head gates including null anchor; synthetic verified feed; both sniff states; affection/Undo/Poke/reward/Stop/Reset; tracking loss invalidates head proof while preserving dream; recovery/exit/audio cleanup. Ten rendered frames. No real head or headset validation.\n");
                    Debug.Log("NATURAL_PET_PLAYMODE_PASS: two species, ten actual frames, dream/feed/sniff/Undo/Poke/reward/Stop/Reset/head gates/loss/recovery/exit");Finish(0);return;
            }
            File.AppendAllText(Path.Combine(Output,"samples.txt"),"stage="+stage+" update_elapsed="+elapsed+" total_frames="+probe.Frames+"\n");Next();
        }
        catch(Exception e){Debug.LogException(e);File.WriteAllText(Path.Combine(Output,"result.txt"),"FAIL: "+e);Finish(1);}
    }
    static void Finish(int code){SessionState.SetInt(Key+".Exit",code);EditorApplication.ExitPlaymode();}
    static void Capture(ParkActor actor,string name)
    {
        var center=actor.transform.position+new Vector3(0,.25f,0);camera.orthographicSize=.34f;camera.transform.position=center+new Vector3(.9f,.4f,-1.6f);camera.transform.LookAt(center);
        var target=new RenderTexture(800,700,24);var pixels=new Texture2D(800,700,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,800,700),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(Output,actor.species+"-"+name+".png"),pixels.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
    }
}
