using System;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Native saved-scene lifecycle/render evidence. Dispatch inputs here are synthetic.
[InitializeOnLoad]
public static class RoomPhotoSceneChecks
{
    const string Key="MagicMR.RoomPhotoScene";
    static string Output=>Path.GetFullPath(".codex-tmp/room-photo-scene");
    static ParkController controller;static ParkActor actor;static RoomMagicVisual room;static RoomPhotoTransfer photo;
    static int phase,stageMask,errors;static double start,next;
    static Vector3 anchor;static Texture2D ownedPhoto;static Material[] ownedMaterials;static int ownedCount;
    static RoomPhotoSceneChecks(){EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};}
    public static void Run(){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);EditorSceneManager.OpenScene("Assets/Scenes/ParkRoom.unity");EditorApplication.EnterPlaymode();}
    static void Require(bool yes,string why){if(!yes)throw new Exception(why);}
    static void Later(int value,double delay=.5){phase=value;next=EditorApplication.timeSinceStartup+delay;}
    static void Act(ParkAction value)=>Require(controller.Dispatch(value,anchor+new Vector3(.10f,.10f,-.10f),true),value+": "+controller.InteractionStatus);
    static int MaterialCount()=>Resources.FindObjectsOfTypeAll<Material>().Count(m=>m&&(m.name=="Owned room photo card"||m.name=="Owned room photo particles"));
    static void Clear(){Require(!photo.Running&&photo.LiveParticles==0,"Photo effect survived interruption");Require(room.WindowProgress==0,"Window survived interruption");}
    static void Capture(string name){
        var go=new GameObject("Synthetic photo scene review camera");var camera=go.AddComponent<Camera>();
        var center=actor.visual.TransformPoint(new Vector3(0,.29f,.01f));
        camera.orthographic=true;camera.orthographicSize=.48f*actor.visual.lossyScale.y;camera.nearClipPlane=.01f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.065f,.085f);
        camera.transform.position=center+new Vector3(.65f,.24f,-1.4f);camera.transform.LookAt(center);
        var rt=RenderTexture.GetTemporary(1200,1200,24);var previous=RenderTexture.active;Texture2D image=null;
        try {camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image=new Texture2D(1200,1200,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,1200),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(image)UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(go);}
    }
    static void Finish(bool pass,string message){File.WriteAllText(Output+"/result.txt",(pass?"PASS: saved ParkRoom photo transfer; ":"FAIL: ")+message+"\n");SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();if(!pass)Debug.LogError(message);}
    static void Tick(){
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying){if(SessionState.GetBool(Key+".Done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.Exit(errors==0?0:2);}return;}
        try {
            if(phase==0){start=EditorApplication.timeSinceStartup;Later(1);return;}
            Require(errors==0,"Runtime/shader error");Require(EditorApplication.timeSinceStartup-start<100,"Photo scene timeout");
            if(phase==3&&photo.Running){
                int bit=photo.Stage=="photo"?1:photo.Stage=="gather"?2:photo.Stage=="held"?4:8;
                if((stageMask&bit)==0&&(bit!=2||photo.Elapsed>.48f)&&(bit!=8||photo.Elapsed>1.4f)){
                    stageMask|=bit;Capture("0"+stageMask+"-"+photo.Stage);
                    Require(bit==1||photo.LiveParticles>0,"No particles during transfer");
                }
                Require(photo.LiveParticles<=RoomPhotoTransfer.ParticleBudget,"Particle budget exceeded");
            }
            if(EditorApplication.timeSinceStartup<next||actor&&actor.Busy)return;
            if(phase==1){controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();Require(controller&&controller.scenario==ParkScenario.Room,"Wrong saved scene");controller.simulation=true;controller.SpawnVirtual();Later(2);}
            else if(phase==2){actor=controller.selected;anchor=actor.transform.position;room=actor.GetComponent<RoomMagicVisual>();photo=room.PhotoTransfer;Require(photo&&!photo.Photo,"Preview must be lazy");Act(ParkAction.Awaken);Later(3,3);}
            else if(phase==3){Require(stageMask==15,"Missing transition stages: "+stageMask);Require(!photo.Running&&photo.LiveParticles==0&&room.WindowProgress>.99f,"Scene did not resolve");Capture("04-released-scene");
                ownedPhoto=photo.Photo;ownedCount=MaterialCount();Require(ownedCount==2,"Unexpected owned material budget");File.WriteAllBytes(Output+"/room-source-photo.png",ownedPhoto.EncodeToPNG());
                ownedMaterials=Resources.FindObjectsOfTypeAll<Material>().Where(m=>m&&(m.name=="Owned room photo card"||m.name=="Owned room photo particles")).ToArray();Act(ParkAction.Awaken);Later(4,.7);}
            else if(phase==4){Require(photo.Running,"Repeated B did not restart");Require(photo.Photo==ownedPhoto&&MaterialCount()==ownedCount,"Replay reallocated preview/materials");Act(ParkAction.Stop);Later(5);}
            else if(phase==5){Clear();Act(ParkAction.Awaken);Later(6,.7);}
            else if(phase==6){Require(photo.Running,"Undo precondition");Act(ParkAction.Undo);Later(7);}
            else if(phase==7){Clear();Act(ParkAction.Awaken);Later(8,.7);}
            else if(phase==8){Require(photo.Running,"Loss precondition");actor.SetTracked(false);Clear();actor.SetTracked(true);Later(9);}
            else if(phase==9){Clear();Act(ParkAction.Awaken);Later(10,.7);}
            else if(phase==10){Require(photo.Running,"Reset precondition");Act(ParkAction.Reset);Later(11);}
            else if(phase==11){Clear();
                for(int i=0;i<12;i++){photo.Begin(anchor+Vector3.up*.08f);photo.Step(.08f);photo.Cancel();}
                photo.Begin(new Vector3(float.NaN,0,0));Require(!photo.Running,"Invalid focus accepted");Require(photo.Photo==ownedPhoto&&MaterialCount()==ownedCount,"Resources grew over replay/cancel cycles");
                Require(actor.transform.position==anchor,"Target anchor moved");Capture("05-reset");UnityEngine.Object.Destroy(room);Later(12);}
            else if(phase==12){Require(photo==null&&ownedPhoto==null&&ownedMaterials.All(m=>m==null),"Component-only removal leaked photo native resources");Require(MaterialCount()==0,"Owned photo materials leaked");
                Finish(true,"four observed transition stages and source photo rendered; bounded 1536 particles; repeat reuses native resources; Stop/Undo/loss/recovery/Reset; twelve replay/cancel cycles; invalid focus rejected; target anchor unchanged; component-only removal frees photo texture/materials/particles. Synthetic Editor scope; no real YOLO/PICO, arbitrary-photo reconstruction, Android or final art acceptance.");}
        }catch(Exception e){Finish(false,e.ToString());}
    }
}
