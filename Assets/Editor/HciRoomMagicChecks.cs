using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class HciRoomMagicChecks {
    const string Key="MagicMR.HciRoomMagic";
    static string Output=>Path.GetFullPath(".codex-tmp/hci-room-magic");
    static ParkController controller;static ParkActor actor;static RoomMagicVisual room;static NaturalJellyfish jelly;
    static Vector3 anchor;static int phase,errors,eventCount,traceIndex,owned;static double started,next;
    static List<TabletopHandSample> trace;static TabletopGestureRules lighter;static EditDimension expectedDimension;static TabletopHandSample held;static bool holdValid;static float sampleAt;
    static bool sawOpening;static Renderer[] furniture;static Material[][] original;static Matrix4x4[] matrices;static List<Transform> pool;
    static bool lampSampled,candySampled;
    static float lampPhaseBefore,lampTimeBefore,candyTimeBefore;
    static Vector3[] candyPositionsBefore;
    static HciRoomMagicChecks(){EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};}
    public static void Run(){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);EditorSceneManager.OpenScene("Assets/Scenes/ParkRoom.unity");EditorApplication.EnterPlaymode();}
    static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Fail(Exception e){File.WriteAllText(Output+"/result.txt","FAIL: "+e);SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(2);}
    static void Later(int p,double delay=1.2){phase=p;next=EditorApplication.timeSinceStartup+delay;}
    static void Act(ParkAction a)=>Require(controller.Dispatch(a,anchor,true),a+": "+controller.InteractionStatus);
    static void Rain(bool visible){Require(pool.Count==32,"Pool grew");Require(pool.Count(p=>p.gameObject.activeSelf)==(visible?24:0),"Unexpected continuous rain count");if(visible)foreach(var p in pool.Where(p=>p.gameObject.activeSelf))Require(p.position.y<actor.environmentAnchor.position.y&&p.position.y>actor.environmentAnchor.position.y-.35f,"Rain left ceiling fall budget");}
    static void Stable(){Require(actor.transform.position==anchor,"Tracking anchor moved");if(phase<12)for(int i=0;i<furniture.Length;i++)Require(furniture[i].transform.localToWorldMatrix==matrices[i],"Furniture moved during magic");Require(room.OwnedMaterialCount==owned,"Materials grew");}
    static void Capture(string name,bool jellyOnly=false){
        var camera=new GameObject("Synthetic room review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.52f;camera.nearClipPlane=.01f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.14f,.16f,.18f);
        if(jellyOnly)camera.backgroundColor=Color.black;
        var center=actor.visual.TransformPoint(new Vector3(0,.38f,.025f));camera.orthographicSize=.50f*actor.visual.lossyScale.y;
        camera.transform.position=center+new Vector3(.7f,.3f,-1.5f);camera.transform.LookAt(center);
        var rt=new RenderTexture(960,800,24);var pixels=new Texture2D(960,800,TextureFormat.RGB24,false);var old=RenderTexture.active;
        Renderer[] hidden=null;
        try{
            if(jellyOnly){hidden=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.enabled&&r!=jelly.Surface).ToArray();foreach(var r in hidden)r.enabled=false;}
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,960,800),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());
        }
        finally{if(hidden!=null)foreach(var r in hidden)if(r)r.enabled=true;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
    }
    static string R(float value)=>value.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
    static void LampUniform(){
        var block=new MaterialPropertyBlock();jelly.Surface.GetPropertyBlock(block);
        Require(Mathf.Abs(block.GetFloat("_Phase")-jelly.Phase)<.0001f&&block.GetFloat("_Opacity")>.99f&&jelly.Surface.sharedMaterial.GetFloat("_Motion")>.99f,"Lamp GPU phase/opacity/motion not bound");
    }
    static TabletopHandSample Frame(string shape,float pinch=.08f,Vector3 delta=default){var extension=shape=="fist"?Vector4.one*.55f:shape=="point"?new Vector4(.95f,.55f,.55f,.55f):Vector4.one*.95f;var p=anchor+Vector3.up*.05f+delta;return TabletopHandSample.Create(0,p,p+Vector3.up*.04f,p+Vector3.up*.04f+Vector3.right*pinch,p-Vector3.up*.02f,extension,Vector3.right,pinch);}
    const float SampleCadenceSeconds=.03f;
    static float fistBeganAt=-1;
    static int plannedFistSamples;
    static void Cast(EditDimension dimension){
        controller.ProcessMagicSample(default,false);holdValid=false;eventCount=0;traceIndex=0;sampleAt=Time.unscaledTime;lighter=new TabletopGestureRules();expectedDimension=dimension;trace=new List<TabletopHandSample>{Frame("open"),Frame("open")};
        if(dimension==EditDimension.Appearance){for(int n=1;n<=8;n++)trace.Add(Frame("open",.02f));trace.Add(Frame("open"));}
        else if(dimension==EditDimension.Agency){for(int n=0;n<=32;n++){float a=n/32f*Mathf.PI*2;trace.Add(Frame("point",.08f,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.04f));}}
        else if(dimension==EditDimension.Rule){for(int n=1;n<=6;n++)trace.Add(Frame("open",.08f,Vector3.right*n*.03f));}
        else{
            // An expired hold is correctly rejected by the production burst window.
            // Use a short valid trace, without changing the lighter/HCI detector rules.
            fistBeganAt=-1;
            plannedFistSamples=Mathf.CeilToInt(TabletopGestureRules.FistMinimumSeconds/SampleCadenceSeconds)+1;
            Require(plannedFistSamples*TabletopGestureRules.MaximumSampleGapSeconds<TabletopGestureRules.FistMaximumSeconds,"Synthetic D cannot fit the current burst window");
            for(int n=0;n<plannedFistSamples;n++)trace.Add(Frame("fist",.02f));
            trace.Add(Frame("open"));
        }
    }
    static void Pump(){try{
        if(!controller)return;
        if(trace!=null){if(Time.unscaledTime<sampleAt)return;var f=trace[traceIndex++];f.gesture.time=Time.unscaledTime;sampleAt=Time.unscaledTime+SampleCadenceSeconds;
            if(expectedDimension==EditDimension.Deconstruction&&f.gesture.fist&&fistBeganAt<0)fistBeganAt=f.gesture.time;
            var expected=traceIndex==1?EditDimension.None:lighter.Step(f.gesture);var actual=controller.ProcessHandSample(f,true);
            Require(expected==actual,"Room recognition differs from lighter: "+expected+"/"+actual+" "+controller.InteractionStatus);
            if(actual!=EditDimension.None){eventCount++;Require(actual==expectedDimension&&controller.LastMagicAccepted,"Wrong/rejected spell");}
            held=f;holdValid=true;if(traceIndex==trace.Count){
                if(expectedDimension==EditDimension.Deconstruction){
                    float duration=f.gesture.time-fistBeganAt;
                    Require(fistBeganAt>=0&&duration>=TabletopGestureRules.FistMinimumSeconds&&duration<=TabletopGestureRules.FistMaximumSeconds,"Synthetic D missed the existing production time window");
                    File.AppendAllText(Output+"/gesture-timing.txt","D fist_seconds="+duration.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+" samples="+plannedFistSamples+" min="+TabletopGestureRules.FistMinimumSeconds.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+" max="+TabletopGestureRules.FistMaximumSeconds.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+" events="+eventCount+"\n");
                }
                Require(eventCount==1,"Expected one gesture event, got "+eventCount);trace=null;next=EditorApplication.timeSinceStartup+1.2;
            }
        }else if(holdValid){held.gesture.time=Time.unscaledTime;Require(controller.ProcessHandSample(held,true)==EditDimension.None,"Held hand repeated spell");}
    }catch(Exception e){Fail(e);}}
    static void Tick(){
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying){if(SessionState.GetBool(Key+".Done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}return;}
        try{
            if(phase==0){started=EditorApplication.timeSinceStartup;new GameObject("Synthetic room joint pump").AddComponent<HciSeasonTreeHandPump>().sample=Pump;Later(1);return;}
            Require(errors==0,"Runtime/shader errors");Require(EditorApplication.timeSinceStartup-started<150,"Timeout");
            if(matrices!=null)Stable();
            if(phase==3&&room.WindowProgress>.1f&&room.WindowProgress<.9f&&!sawOpening){sawOpening=true;Capture("01-window-opening");}
            if(trace!=null||EditorApplication.timeSinceStartup<next||actor&&actor.Busy)return;
            if(phase==1){controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();Require(controller&&controller.scenario==ParkScenario.Room,"Wrong saved room");controller.simulation=true;controller.SpawnVirtual();Later(2);}
            else if(phase==2){actor=controller.selected;anchor=actor.transform.position;room=actor.GetComponent<RoomMagicVisual>();jelly=actor.GetComponentInChildren<NaturalJellyfish>(true);Require(room&&jelly,"Missing room content");owned=room.OwnedMaterialCount;
                var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                furniture=((List<Renderer>)typeof(ParkActor).GetField("body",flags).GetValue(actor)).ToArray();original=furniture.Select(r=>r.sharedMaterials).ToArray();matrices=furniture.Select(r=>r.transform.localToWorldMatrix).ToArray();pool=(List<Transform>)typeof(ParkActor).GetField("pool",flags).GetValue(actor);
                Require(!controller.Dispatch(ParkAction.Reward,anchor,true),"D accepted without ceiling");Require(controller.RequestManualPlane()&&controller.ConfirmManualPlane(actor.visual.TransformPoint(Vector3.up*.9f),true),"Ceiling confirmation failed");Capture("00-original");Cast(EditDimension.Agency);Later(3);
            }
            else if(phase==3){Require(sawOpening&&room.WindowProgress>.99f&&actor.RoomWindowRequested&&!actor.RoomLampRequested&&jelly.Visibility==0,"B is not independent window");Rain(false);Capture("02-window-open");Cast(EditDimension.Rule);Later(4);}
            else if(phase==4){
                Require(actor.RoomLampRequested&&jelly.Visibility>.99f&&room.WindowProgress>.99f,"C did not add lamp");LampUniform();
                if(!lampSampled){lampSampled=true;lampPhaseBefore=jelly.Phase;lampTimeBefore=Time.unscaledTime;Capture("03-lamp");Capture("03-lamp-only-a",true);Later(4,.4);return;}
                Require(Mathf.Abs(Mathf.DeltaAngle(lampPhaseBefore*Mathf.Rad2Deg,jelly.Phase*Mathf.Rad2Deg))*Mathf.Deg2Rad>.1f,"Lamp shader animation phase did not advance");
                Capture("03-lamp-motion");Capture("03-lamp-only-b",true);
                File.AppendAllText(Output+"/motion-samples.txt","C time0="+R(lampTimeBefore)+" time1="+R(Time.unscaledTime)+" phase0="+R(lampPhaseBefore)+" phase1="+R(jelly.Phase)+" opacity="+R(jelly.Visibility)+"\n");
                Cast(EditDimension.Appearance);Later(5);
            }
            else if(phase==5){Require(room.Style==1&&jelly.Visibility>.99f&&room.WindowProgress>.99f,"A watercolor replaced B/C");Capture("04-watercolor");Cast(EditDimension.Appearance);Later(6);}
            else if(phase==6){Require(room.Style==2&&jelly.Visibility>.99f&&room.WindowProgress>.99f,"A cyber replaced B/C");Capture("05-cyber");Cast(EditDimension.Deconstruction);Later(7);}
            else if(phase==7){
                Rain(true);Require(room.Style==2&&jelly.Visibility>.99f&&room.WindowProgress>.99f,"D replaced A/B/C");
                if(!candySampled){candySampled=true;candyTimeBefore=Time.unscaledTime;candyPositionsBefore=pool.Select(p=>p.position).ToArray();Capture("06-candy");Later(7,.4);return;}
                int moved=0;float maxMove=0;
                for(int i=0;i<pool.Count;i++)if(pool[i].gameObject.activeSelf){float distance=Vector3.Distance(pool[i].position,candyPositionsBefore[i]);if(distance>.0001f)moved++;maxMove=Mathf.Max(maxMove,distance);}
                Require(moved>0,"Candy pool is visible but not moving");Capture("06-candy-motion");
                File.AppendAllText(Output+"/motion-samples.txt","D time0="+R(candyTimeBefore)+" time1="+R(Time.unscaledTime)+" moved="+moved+" active="+pool.Count(p=>p.gameObject.activeSelf)+" pool="+pool.Count+" max_distance="+R(maxMove)+"\n");
                Act(ParkAction.Undo);Later(8);
            }
            else if(phase==8){Rain(false);Require(jelly.Visibility>.99f&&room.WindowProgress>.99f&&room.Style==2,"Undo D removed B/C/style");Cast(EditDimension.Deconstruction);Later(9);}
            else if(phase==9){Rain(true);Cast(EditDimension.Rule);Later(10);}
            else if(phase==10){Rain(true);Require(jelly.Visibility>.99f,"Repeat C lost lamp");Act(ParkAction.Stop);Later(11);}
            else if(phase==11){Rain(false);Require(room.WindowProgress==0&&jelly.Visibility==0&&room.Style==2,"Stop did not clear transient effects/preserve A");Capture("07-stopped");Cast(EditDimension.Agency);Later(12);}
            else if(phase==12){Require(room.WindowProgress>.99f,"B restart failed");actor.SetTracked(false);holdValid=false;trace=null;Require(!room.Window.gameObject.activeSelf&&room.Style==0,"Target loss retained room magic");Later(13);}
            else if(phase==13){actor.SetTracked(true);Cast(EditDimension.Agency);Later(14);}
            else if(phase==14){Require(room.WindowProgress>.99f,"B after tracking recovery failed");Act(ParkAction.Undo);Later(15);}
            else if(phase==15){Require(room.WindowProgress==0,"Undo B retained window");Cast(EditDimension.Appearance);Later(16);}
            else if(phase==16){Require(room.Style==1,"A after recovery failed");Act(ParkAction.Reset);Later(17);}
            else if(phase==17){Rain(false);Require(room.Style==0&&room.WindowProgress==0&&jelly.Visibility==0&&!actor.confirmedPlane,"Reset incomplete");for(int i=0;i<furniture.Length;i++)Require(furniture[i].sharedMaterials.SequenceEqual(original[i]),"Original room materials not restored");Capture("08-reset");
                File.WriteAllText(Output+"/result.txt","PASS: saved ParkRoom; production synthetic shared lighter A/B/C/D classification exactly once; A watercolor/cyber independent; B animated floor-to-ceiling virtual window; C jellyfish lamp; D ceiling-gated continuous candy; A/B/C/D coexist; repeated C preserves D; Undo D/B, Stop, target loss/recovery, Reset; furniture/anchor stable during magic (loss/reset clear calibration); bounded material/pool counts; original materials restored; nine camera views including opening; zero runtime errors. Editor synthetic only; no real room reconstruction, YOLO/PICO or Android acceptance.\n");SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();}
        }catch(Exception e){Fail(e);}
    }
}

