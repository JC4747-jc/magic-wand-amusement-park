using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class NaturalRoomPlayChecks
{
    const string Key="MagicMR.RoomPlay.Active";
    static readonly string Output=Path.GetFullPath(".codex-tmp/natural-room-play");
    static readonly float[] PulseTimes={.38f,.55f,.95f};
    static ParkActor actor;
    static NaturalJellyfish jelly;
    static Transform lamp;
    static Camera camera;
    static ParkAnimationFrameProbe probe;
    static List<Renderer> furniture;
    static List<Transform> pool;
    static Matrix4x4[] rest;
    static GameObject ceiling;
    static float phase;
    static double started;
    static int stage,sample,captures,ceilingMovedFrame,restCaptureFrame;
    static long captureBytes;
    static bool partialStop,movedCeiling,rainCaptured,restCaptured;
    static bool StableFrame=>probe.Frames>=5&&Time.unscaledDeltaTime<.05f;
    static NaturalRoomPlayChecks(){EditorApplication.update+=Tick;}
    public static void RunAll()
    {
        NaturalJellyfishAssetBuilder.Run();NaturalJellyfishChecks.Render();NaturalJellyfishMotionFrames.Run();ProjectSceneAudit.Run();ParkNaturalModelChecks.Render();ParkSharedInteractionChecks.Run();
        SessionState.SetBool(Key+".NextPark",true);Run();
    }
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Room check requires idle editor");
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"samples.txt"),"");
        SessionState.SetString(Key+".Scene",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);SessionState.SetInt(Key+".Exit",-1);SessionState.SetBool(Key,true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.36f,.38f,.40f);
        var key=new GameObject("Room key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.3f;key.transform.rotation=Quaternion.Euler(38,-35,0);
        camera=new GameObject("Room lifecycle camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=.57f;camera.nearClipPlane=.01f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.14f,.16f);camera.gameObject.AddComponent<AudioListener>();
        camera.transform.position=new Vector3(.9f,.75f,-1.6f);camera.transform.LookAt(new Vector3(0,.40f,.07f));
        EditorApplication.EnterPlaymode();
    }
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Next(){stage++;phase=probe.SimulatedSeconds;}
    static void Action(ParkAction action){Require(actor.TryAction(action,actor.transform.position,true),"Room rejected "+action+": "+actor.Feedback);}
    static void StableFurniture()
    {
        Require(actor.transform.position==Vector3.zero&&actor.transform.rotation==Quaternion.identity,"Room action moved tracking anchor");
        if(rest==null)return;
        Require(actor.visual.localScale==Vector3.one&&actor.visual.localRotation==Quaternion.identity,"Room awakening distorted architecture");
        for(int i=0;i<furniture.Count;i++)Require(furniture[i].transform.localToWorldMatrix==rest[i],"Furniture moved during fantasy: "+furniture[i].name);
        Require(pool.Count==32,"Room feedback pool grew");
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying)
        {
            int exit=SessionState.GetInt(Key+".Exit",-1);if(exit<0)return;SessionState.SetBool(Key,false);
            string previous=SessionState.GetString(Key+".Scene","");if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);
            if(exit==0&&SessionState.GetBool(Key+".NextPark",false))ParkAnimationTimelineChecks.Run();else EditorApplication.Exit(exit);return;
        }
        try
        {
            if(stage==0)
            {
                started=EditorApplication.timeSinceStartup;camera=Camera.main;probe=camera.gameObject.AddComponent<ParkAnimationFrameProbe>();
                var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/ParkRoom.prefab"));actor=go.GetComponent<ParkActor>();
                actor.virtualTarget=true;actor.cooldown=0;actor.transitionSeconds=.3f;go.SetActive(true);actor.Build();if(actor.label)actor.label.gameObject.SetActive(false);
                jelly=go.GetComponentInChildren<NaturalJellyfish>(true);Require(jelly,"Room still uses primitive jellyfish");lamp=jelly.transform.parent;
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;furniture=(List<Renderer>)typeof(ParkActor).GetField("body",flags).GetValue(actor);pool=(List<Transform>)typeof(ParkActor).GetField("pool",flags).GetValue(actor);
                Next();return;
            }
            Require(EditorApplication.timeSinceStartup-started<60,"Room Play Mode timed out");
            if(stage<=10)StableFurniture();float elapsed=probe.SimulatedSeconds-phase;
            // Loading the Play Mode scene can produce a >1s first delta. Start
            // gestures only after several real stable frames, as the input
            // sampler also requires a stable run before dispatching a gesture.
            if(stage==1&&elapsed>.5f&&StableFrame)
            {
                // ReadPixels/first-use rendering can itself stall a frame.
                // Finish that warmup, then observe new stable frames before
                // dispatching input rather than starting a coroutine on it.
                if(!restCaptured){rest=furniture.Select(r=>r.transform.localToWorldMatrix).ToArray();Require(!lamp.gameObject.activeSelf&&jelly.Visibility==0,"Resting room retained lamp");Capture("Rest");restCaptured=true;restCaptureFrame=probe.Frames;return;}
                if(probe.Frames<restCaptureFrame+5)return;
                Require(!actor.TryAction(ParkAction.Reward,Vector3.zero,true),"Room reward without plane accepted");Action(ParkAction.Wind);Next();
            }
            else if(stage==2)
            {
                if(sample<PulseTimes.Length&&elapsed>=PulseTimes[sample])
                {
                    if(sample==0)Require(jelly.Visibility>0&&jelly.Visibility<1,"Jellyfish opening popped instead of fading: opacity="+jelly.Visibility+" action_elapsed="+elapsed+" dt="+Time.unscaledDeltaTime+" busy="+actor.Busy);
                    Capture("Pulse-"+sample);sample++;
                }
                if(elapsed>1.2f&&sample==3&&StableFrame){Require(!actor.Busy&&jelly.Visibility==1&&lamp.gameObject.activeSelf,"Jellyfish did not finish opening");Require(pool.Count(p=>p.gameObject.activeSelf)==6&&pool.Where(p=>p.gameObject.activeSelf).All(p=>p.localPosition.y>.59f&&p.localScale.x<=.0091f),"Lamp feedback lost bounded small motes near the bell");Action(ParkAction.Stop);Next();}
            }
            else if(stage==3)
            {
                if(!partialStop&&elapsed>.12f){Require(jelly.Visibility>0&&jelly.Visibility<1&&actor.Busy,"Jellyfish Stop did not fade");partialStop=true;Capture("Stopping");}
                if(elapsed>.45f&&StableFrame){Require(partialStop&&!actor.Busy&&!lamp.gameObject.activeSelf&&jelly.Visibility==0,"Stopped jellyfish reappeared");Action(ParkAction.Wind);Next();}
            }
            else if(stage==4&&elapsed>.85f){Require(jelly.Visibility==1,"Jellyfish restart failed");actor.Undo();Next();}
            else if(stage==5&&elapsed>.45f){Require(jelly.Visibility==0&&!lamp.gameObject.activeSelf,"Undo retained lamp");Capture("Undo");Action(ParkAction.Wind);Next();}
            else if(stage==6&&elapsed>.9f&&StableFrame){Action(ParkAction.Stop);Next();}
            else if(stage==7&&elapsed>1)
            {
                Require(!actor.Busy&&!lamp.gameObject.activeSelf&&jelly.Visibility==0,"Stop retained jellyfish");
                Require(!actor.TryAction(ParkAction.Reward,Vector3.zero,true),"Candy rain without ceiling accepted");
                ceiling=new GameObject("Synthetic horizontal ceiling - test only");ceiling.transform.position=new Vector3(.12f,.87f,.05f);actor.environmentAnchor=ceiling.transform;actor.confirmedPlane=true;
                Action(ParkAction.Reward);Next();
            }
            else if(stage==8)
            {
                if(movedCeiling&&probe.Frames<=ceilingMovedFrame)return;
                if(elapsed>.75f)
                {
                    Require(pool.Count(p=>p.gameObject.activeSelf)==24,"Candy rain ended after one burst or spawned extra particles");
                    foreach(var p in pool.Where(p=>p.gameObject.activeSelf))Require(p.position.y<ceiling.transform.position.y-.019f&&p.position.y>ceiling.transform.position.y-.31f,"Candy is above the confirmed ceiling or outside bounded fall");
                    if(!rainCaptured){Capture("Candy");rainCaptured=true;}
                }
                if(elapsed>1.7f&&!movedCeiling){ceiling.transform.position+=new Vector3(.13f,.05f,0);movedCeiling=true;ceilingMovedFrame=probe.Frames;return;}
                if(elapsed>3.2f){Require(movedCeiling,"Ceiling movement not exercised");Capture("Candy-moved-ceiling");Action(ParkAction.Stop);Next();}
            }
            else if(stage==9&&elapsed>.45f)
            {
                Require(!actor.Busy&&pool.All(p=>!p.gameObject.activeSelf)&&!lamp.gameObject.activeSelf,"Stop retained room fantasy");
                Require(actor.confirmedPlane&&actor.environmentAnchor==ceiling.transform,"Stop discarded manual ceiling");Capture("Stopped");
                actor.ResetEffects();Require(!actor.confirmedPlane&&!actor.environmentAnchor&&jelly.Visibility==0,"Reset retained ceiling/lamp");
                Action(ParkAction.Wind);actor.SetTracked(false);Require(!actor.Busy&&!actor.visual.gameObject.activeSelf&&!actor.confirmedPlane,"Room loss retained effect/plane");
                actor.SetTracked(true);Next();
            }
            else if(stage==10&&elapsed>.35f){Require(actor.visual.gameObject.activeSelf&&!lamp.gameObject.activeSelf,"Room recovery revived stale fantasy");Capture("Recovery");actor.ExitAndDestroy();UnityEngine.Object.Destroy(ceiling);Next();}
            else if(stage==11&&elapsed>.4f)
            {
                Require(!actor&&UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length==0,"Room exit leaked actor/audio");
                File.WriteAllText(Path.Combine(Output,"result.txt"),"PASS: actual Play Mode entry; fixed architecture through all effects; jellyfish pulse/open/Stop fade/Undo/restart/explicit Stop before D; synthetic ceiling gate, 24 pooled candy particles sustained >3s below ceiling and following moved anchor; Stop/Reset/loss/recovery/exit/audio cleanup. "+captures+" rendered frames. No real room mapping, tracking or headset acceptance.\n");
                Debug.Log("NATURAL_ROOM_PLAYMODE_PASS: "+captures+" actual frames; fixed furniture, jellyfish lifecycle, below-ceiling bounded sustained candy, loss/recovery/exit/audio");Finish(0);
            }
        }
        catch(Exception e){Debug.LogException(e);File.WriteAllText(Path.Combine(Output,"result.txt"),"FAIL: "+e);Finish(1);}
    }
    static void Finish(int code){SessionState.SetInt(Key+".Exit",code);EditorApplication.ExitPlaymode();}
    static void Capture(string name)
    {
        var target=new RenderTexture(1000,900,24);var pixels=new Texture2D(1000,900,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1000,900),0,0);pixels.Apply();var png=pixels.EncodeToPNG();captureBytes+=png.Length;Require(png.Length<2*1024*1024&&captureBytes<24*1024*1024,"Room frames exceeded size cap");File.WriteAllBytes(Path.Combine(Output,name+".png"),png);captures++;File.AppendAllText(Path.Combine(Output,"samples.txt"),name+" stage="+stage+" update_elapsed="+(probe.SimulatedSeconds-phase)+" total_frames="+probe.Frames+" opacity="+jelly.Visibility+" pulse="+jelly.Phase+"\n");}
        finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);}
    }
}
