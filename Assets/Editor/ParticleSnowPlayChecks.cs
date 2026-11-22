using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ParticleSnowPlayChecks
{
    const string Key="MagicMR.ParticleSnowChecks";
    static string Output=>Path.GetFullPath(".codex-tmp/particle-snow-checks");
    static ParkController controller;static ParkActor actor;static ParkSharedInterface ui;static PointFlowerMorph flower;static RoomSnowMagic snow;
    static int phase,errors,index,events;static double start,next;static float sampleAt;static Vector3 anchor;static Vector3[] frozen;
    static List<TabletopHandSample> trace;static TabletopHandSample held;static bool holding;
    static TabletopGestureRules comparison;static EditDimension expected;static readonly List<string> evidence=new List<string>();
    static ParticleSnowPlayChecks(){EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};}
    public static void Run(){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);EditorSceneManager.OpenScene("Assets/Scenes/ParkPlant.unity");EditorApplication.EnterPlaymode();}
    static void Require(bool yes,string why){if(!yes)throw new Exception(why);}
    static void Later(int p,double delay=.7){phase=p;next=EditorApplication.timeSinceStartup+delay;}
    static void Finish(bool pass,string message){File.WriteAllText(Output+"/result.txt",(pass?"PASS: saved Plant point flower and Room snow; ":"FAIL: ")+message+"\n");SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();if(!pass)Debug.LogError(message);}
    static TabletopHandSample Frame(Vector3 p,string shape="open",float pinch=.08f)
    {var extension=shape=="fist"?Vector4.one*.55f:shape=="point"?new Vector4(.95f,.55f,.55f,.55f):Vector4.one*.95f;return TabletopHandSample.Create(0,p-Vector3.up*.04f,p,p+Vector3.right*pinch,p-Vector3.up*.06f,extension,Vector3.right,pinch);}
    static void Cast(EditDimension dimension){
        var p=actor.transform.position+Vector3.up*.075f;controller.ProcessMagicSample(default,false);holding=false;expected=dimension;comparison=new TabletopGestureRules();index=events=0;sampleAt=Time.unscaledTime;
        trace=new List<TabletopHandSample>{Frame(p),Frame(p)};
        if(dimension==EditDimension.Appearance){for(int n=0;n<8;n++)trace.Add(Frame(p,"open",.02f));trace.Add(Frame(p));}
        else if(dimension==EditDimension.Agency){for(int n=0;n<=32;n++){float a=n*Mathf.PI*2/32;trace.Add(Frame(p+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.04f,"point"));}}
        else if(dimension==EditDimension.Rule){for(int n=1;n<=6;n++)trace.Add(Frame(p+Vector3.right*n*.03f));}
        else{int count=Mathf.CeilToInt(TabletopGestureRules.FistMinimumSeconds/.03f)+1;for(int n=0;n<count;n++)trace.Add(Frame(p,"fist",.02f));trace.Add(Frame(p));}
    }
    static void Pump(){try{
        if(!controller)return;
        if(trace!=null){if(Time.unscaledTime<sampleAt)return;var f=trace[index++];f.gesture.time=Time.unscaledTime;sampleAt=Time.unscaledTime+.03f;
            var want=index==1?EditDimension.None:comparison.Step(f.gesture);var got=controller.ProcessHandSample(f,true);Require(want==got,"Shared recognition mismatch");
            if(got!=EditDimension.None){events++;Require(got==expected&&controller.LastMagicAccepted,"Rejected shared action: "+controller.InteractionStatus);}
            held=f;holding=true;if(index==trace.Count){Require(events==1,"Repeated/missing shared event");evidence.Add(actor.scenario+"/"+expected+":1");trace=null;next=EditorApplication.timeSinceStartup+(phase==9?.2:1.3);}
        }else if(holding){held.gesture.time=Time.unscaledTime;Require(controller.ProcessHandSample(held,true)==EditDimension.None,"Held hand repeated effect");}
    }catch(Exception e){Finish(false,e.ToString());}}
    static void Bounds(){
        var menu=typeof(ParkSharedInterface).GetField("menuRoot",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ui) as GameObject;Require(menu,"Shared menu missing");
        var parent=menu.GetComponent<RectTransform>();var buttons=menu.GetComponentsInChildren<Button>(false);var rects=new List<Rect>();
        foreach(var button in buttons){var rt=button.GetComponent<RectTransform>();var rect=new Rect(rt.anchoredPosition-rt.sizeDelta*.5f,rt.sizeDelta);
            Require(rect.xMin>=parent.rect.xMin-.1f&&rect.xMax<=parent.rect.xMax+.1f&&rect.yMin>=parent.rect.yMin-.1f&&rect.yMax<=parent.rect.yMax+.1f,"Button outside menu: "+button.name);
            Require(!rects.Any(r=>r.Overlaps(rect)),"Active menu buttons overlap: "+button.name);rects.Add(rect);
        }
        evidence.Add(actor.scenario+" active-menu-buttons="+buttons.Length+" contained/nonoverlapping");
    }
    static void Capture(string name,bool plant){
        var go=new GameObject("Synthetic particle experience review camera");var camera=go.AddComponent<Camera>();
        var center=actor.visual.TransformPoint(plant?new Vector3(0,.26f,0):new Vector3(0,.32f,0));float scale=actor.visual.lossyScale.y;
        camera.orthographic=true;camera.orthographicSize=(plant?.30f:.49f)*scale;camera.nearClipPlane=.005f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.05f,.075f);
        camera.transform.position=center+new Vector3(.5f,.35f,-1.1f)*scale;camera.transform.LookAt(center);
        var rt=RenderTexture.GetTemporary(1000,1000,24);var previous=RenderTexture.active;Texture2D image=null;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image=new Texture2D(1000,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1000,1000),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(image)UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(go);}
    }
    static void Points(int shape){Require(flower.Shape==shape&&flower.Transition>=.999f&&flower.LivePoints==PointFlowerMorph.PointBudget,"Point shape/transition/budget expected="+shape+" actual="+flower.Shape+" transition="+flower.Transition+" points="+flower.LivePoints);foreach(var p in flower.CurrentPoints)Require(TabletopHandSample.Finite(p)&&p.magnitude<.25f,"Invalid/unbounded point");}
    static void Tick(){
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying){if(SessionState.GetBool(Key+".Done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.Exit(errors==0?0:2);}return;}
        try{
            if(phase==0){start=EditorApplication.timeSinceStartup;new GameObject("Synthetic particle shared hand pump").AddComponent<HciSeasonTreeHandPump>().sample=Pump;Later(1);return;}
            Require(errors==0,"Runtime/shader error");Require(EditorApplication.timeSinceStartup-start<180,"Particle/snow timeout");
            if(trace!=null||EditorApplication.timeSinceStartup<next||actor&&actor.Busy)return;
            if(phase==11&&flower.Transition<.999f)return; // Undo must finish its actual .85s morph; no shortened fixed wait.
            if(phase==18&&snow&&snow.Gathering&&snow.Positions.Average(p=>Vector3.Distance(p,snow.FocusLocal))>.075f)return; // Observe convergence, not just a flag or a fixed wall-clock delay.
            if(phase==1){controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();Require(controller.scenario==ParkScenario.Plant,"Wrong initial saved scene");controller.simulation=true;controller.SpawnVirtual();Later(2);}
            else if(phase==2){actor=controller.selected;anchor=actor.transform.position;ui=UnityEngine.Object.FindFirstObjectByType<ParkSharedInterface>();Require(ui,"Original shared UI missing");ui.Command("Particle flower");flower=actor.PointFlower;Require(actor.PointFlowerMode&&flower,"Original UI choice did not enable point flower");Later(3);}
            else if(phase==3){Bounds();Points(0);Capture("00-pink-flower",true);Cast(EditDimension.Appearance);Later(4);}
            else if(phase==4){Require(flower.Palette==1,"A palette");Cast(EditDimension.Agency);Later(5);}
            else if(phase==5){Require(flower.Alive,"B breathing");Cast(EditDimension.Rule);Later(6);}
            else if(phase==6){Points(1);Require(flower.CurrentPoints.All(p=>Mathf.Abs(p.magnitude-.105f)<.0002f),"Sphere surface not spherical");Capture("01-sphere",true);Cast(EditDimension.Rule);Later(7);}
            else if(phase==7){Points(2);Require(flower.CurrentPoints.All(p=>Mathf.Abs(Mathf.Max(Mathf.Abs(p.x),Mathf.Max(Mathf.Abs(p.y),Mathf.Abs(p.z)))-.095f)<.0002f),"Cube surface missing faces");Capture("02-cube",true);Cast(EditDimension.Rule);Later(8);}
            else if(phase==8){Points(3);Capture("03-ring",true);Cast(EditDimension.Deconstruction);Later(9);}
            else if(phase==9){Capture("04-scatter",true);Later(10,1.1);}
            else if(phase==10){Points(3);Require(controller.Dispatch(ParkAction.Undo,anchor,true),"Point Undo");Later(11);}
            else if(phase==11){Points(3);Require(controller.Dispatch(ParkAction.Reset,anchor,true),"Point Reset");Later(12);}
            else if(phase==12){Points(0);Require(flower.Palette==0&&!flower.Alive&&flower.OwnedMaterialCount==1,"Point reset/material budget");
                var copy=(Vector3[])flower.CurrentPoints.Clone();flower.Step(float.NaN);Require(copy.SequenceEqual(flower.CurrentPoints),"Invalid dt changed points");flower.MorphTo(2);flower.Step(.08f);var mid=(Vector3[])flower.CurrentPoints.Clone();flower.MorphTo(1);Require(mid.SequenceEqual(flower.CurrentPoints),"Interrupted morph jumped");
                Require(controller.Dispatch(ParkAction.Stop,anchor,true),"Point Stop");actor.SetTracked(false);actor.SetTracked(true);Later(13);}
            else if(phase==13){Points(0);Require(actor.transform.position==anchor,"Point mode moved target");ui.Command("Plant effects");Require(!flower.Active&&!actor.PointFlowerMode,"Default Plant choice failed");Bounds();holding=false;controller=null;actor=null;
                EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/ParkRoom.unity",new LoadSceneParameters(LoadSceneMode.Single));Later(14,1);}
            else if(phase==14){controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();Require(controller.scenario==ParkScenario.Room,"Saved Room not loaded");controller.simulation=true;controller.SpawnVirtual();new GameObject("Synthetic snow shared hand pump").AddComponent<HciSeasonTreeHandPump>().sample=Pump;Later(15);}
            else if(phase==15){actor=controller.selected;anchor=actor.transform.position;ui=UnityEngine.Object.FindFirstObjectByType<ParkSharedInterface>();ui.Command("Snow room");snow=actor.RoomSnow;Require(actor.RoomSnowMode&&snow&&!actor.RoomPortalMode,"Snow UI choice");Later(16);}
            else if(phase==16){Bounds();Require(snow.LiveFlakes==0,"Snow before A");Cast(EditDimension.Appearance);Later(17);}
            else if(phase==17){Require(snow.Snowing&&snow.LiveFlakes==RoomSnowMagic.FlakeBudget,"A snowfall/budget");Capture("05-snowfall",false);Cast(EditDimension.Agency);Later(18);}
            else if(phase==18){Require(snow.Gathering&&!snow.Frozen,"B gather");Capture("06-gathered-snow",false);Cast(EditDimension.Rule);Later(19);}
            else if(phase==19){Require(!snow.Gathering&&!snow.Frozen,"C wind state");Capture("07-wind",false);Cast(EditDimension.Deconstruction);Later(20);}
            else if(phase==20){Require(snow.Frozen,"D freeze");frozen=(Vector3[])snow.Positions.Clone();Capture("08-frozen",false);Later(21);}
            else if(phase==21){Require(frozen.SequenceEqual(snow.Positions),"Frozen flakes moved");Require(snow.Positions.All(p=>TabletopHandSample.Finite(p)&&Mathf.Abs(p.x)<.32f&&p.y>=.035f&&p.y<=.625f&&Mathf.Abs(p.z)<.27f),"Snow escaped room budget");Cast(EditDimension.Deconstruction);Later(22);}
            else if(phase==22){Require(!snow.Frozen&&!frozen.SequenceEqual(snow.Positions),"D resume");Require(controller.Dispatch(ParkAction.Undo,anchor,true),"Snow Undo");Later(23);}
            else if(phase==23){Require(snow.Frozen,"Snow Undo did not restore freeze");Require(controller.Dispatch(ParkAction.Stop,anchor,true),"Snow Stop");Later(24);}
            else if(phase==24){Require(!snow.Snowing&&snow.LiveFlakes==0,"Snow Stop cleanup");Require(controller.Dispatch(ParkAction.Reset,anchor,true),"Snow Reset");ui.Command("Magic window");Require(!snow.Active&&!actor.RoomSnowMode,"Original Room choice failed");Require(snow.OwnedMaterialCount==1,"Snow material growth");Bounds();
                File.WriteAllText(Output+"/gestures-and-ui.txt",string.Join("\n",evidence)+"\n");Finish(true,"original Plant/Room UI callbacks; shared synthetic A/B/C/D classification once per trace;2048-point flower/sphere/cube/ring,continuous interrupted morph,bounded material,scatter/Undo/Reset/Stop/loss/recovery;160 procedural stereo-shader snowflakes,fall/gather/wind/freeze/resume/exact freeze and Undo,room bounds/Stop/Reset;active menu buttons contained/nonoverlapping;default content restored;actual review renders. Editor synthetic only; no real YOLO/hands/PICO/Android/performance/final-art acceptance.");}
        }catch(Exception e){Finish(false,e.ToString());}
    }
}
