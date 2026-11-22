using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-only native lifetime checks. No gesture, camera, calibration or fire input.
[InitializeOnLoad]
public static class RoomWindowOwnershipChecks
{
    const string Key="MagicMR.RoomWindowOwnership";
    static string Output=>Path.GetFullPath(".codex-tmp/room-window-ownership");
    static GameObject parent,owner;static RoomMagicVisual room;static Mesh mesh;
    static Renderer landscape;static Material[] materials;static int phase,nextFrame,iteration,errors;
    static double started;static Report report;
    [Serializable] public sealed class Cycle {public int iteration;public string removal;public int meshesAfter,materialsAfter,windowChildrenAfter;}
    [Serializable] public sealed class Report {
        public string checkedAtUtc;public int cycles,componentOnlyCycles,ownerCycles,baselineMeshes,baselineMaterials,maximumObservedMeshes,maximumObservedMaterials;
        public bool passed;public List<Cycle> observations=new List<Cycle>();
    }
    static RoomWindowOwnershipChecks(){
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};
    }
    public static void Run(){
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);SessionState.SetInt(Key+".Exit",1);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();
    }
    static int MeshCount()=>Resources.FindObjectsOfTypeAll<Mesh>().Count(m=>m&&m.name=="Owned room window mountain-lake relief");
    static int MaterialCount()=>Resources.FindObjectsOfTypeAll<Material>().Count(m=>m&&(m.name=="Owned room window landscape"||m.name=="Owned window warm brass"));
    static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Finish(bool pass,string message){
        if(report!=null){report.passed=pass;report.checkedAtUtc=DateTime.UtcNow.ToString("o");File.WriteAllText(Output+"/ownership.json",JsonUtility.ToJson(report,true)+"\n");}
        File.WriteAllText(Output+"/result.txt",(pass?"PASS: owned room window lifecycle; ":"FAIL: ")+message+"\n");
        SessionState.SetInt(Key+".Exit",pass?0:1);SessionState.SetBool(Key+".Done",true);EditorApplication.isPlaying=false;
    }
    static void Tick(){
        if(!SessionState.GetBool(Key,false))return;
        if(SessionState.GetBool(Key+".Done",false)){if(!EditorApplication.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.Exit(SessionState.GetInt(Key+".Exit",1));return;}
        if(!EditorApplication.isPlaying)return;
        try{
            if(started==0){
                started=EditorApplication.timeSinceStartup;report=new Report{baselineMeshes=MeshCount(),baselineMaterials=MaterialCount()};
                Require(report.baselineMeshes==0&&report.baselineMaterials==0,"Empty scene already contains room-owned resources");
                parent=new GameObject("Persistent external window parent");
            }
            Require(errors==0,"Unity logged a runtime error");Require(EditorApplication.timeSinceStartup-started<90,"Ownership review exceeded deadline");
            if(Time.frameCount<nextFrame)return;
            if(phase==0){
                if(iteration==20){
                    Require(parent.transform.childCount==0&&MeshCount()==0&&MaterialCount()==0,"Resources survived final destruction");
                    report.cycles=iteration;UnityEngine.Object.Destroy(parent);
                    Finish(true,"20 native deferred-destruction cycles (10 component-only, 10 owner); meshes/materials/window roots return to zero each cycle. No real-room/PICO or long-duration memory claim.");return;
                }
                owner=new GameObject("Room ownership probe "+iteration);room=owner.AddComponent<RoomMagicVisual>();room.Initialize(Array.Empty<Renderer>(),parent.transform);
                for(int i=0;i<7;i++)room.Step(.1f,true);
                mesh=room.LandscapeRenderer.GetComponent<MeshFilter>().sharedMesh;landscape=room.LandscapeRenderer;
                materials=room.Window.GetComponentsInChildren<Renderer>(true).Select(r=>r.sharedMaterial).Distinct().ToArray();
                Require(room.WindowProgress>.99f&&parent.transform.childCount==1&&materials.Length==2&&mesh.vertexCount==426,"Probe did not create the production room window");
                report.maximumObservedMeshes=Math.Max(report.maximumObservedMeshes,MeshCount());report.maximumObservedMaterials=Math.Max(report.maximumObservedMaterials,MaterialCount());
                Require(MeshCount()==1&&MaterialCount()==2,"Resources accumulated while creating a new window");
                phase=1;nextFrame=Time.frameCount+2;return;
            }
            if(phase==1){
                Require(room.Window.GetComponentsInChildren<Collider>(true).Length==0,"Window retained deferred primitive colliders");
                if(iteration%2==0){UnityEngine.Object.Destroy(room);report.componentOnlyCycles++;}
                else{UnityEngine.Object.Destroy(owner);report.ownerCycles++;}
                phase=2;nextFrame=Time.frameCount+3;return;
            }
            Require(mesh==null&&landscape==null&&materials.All(m=>m==null),"Destroyed window still owns native mesh/renderer/material objects");
            Require(parent.transform.childCount==0&&MeshCount()==0&&MaterialCount()==0,"Window children or native resources survived component/owner removal");
            if(iteration%2==0){Require(owner&&owner.GetComponent<RoomMagicVisual>()==null,"Component-only case also removed the owner");UnityEngine.Object.Destroy(owner);}
            else Require(owner==null,"Owner destruction did not complete");
            report.observations.Add(new Cycle{iteration=iteration,removal=iteration%2==0?"component-only":"owner",meshesAfter=MeshCount(),materialsAfter=MaterialCount(),windowChildrenAfter=parent.transform.childCount});
            iteration++;phase=0;nextFrame=Time.frameCount+1;
        }catch(Exception e){Finish(false,e.ToString());}
    }
}
