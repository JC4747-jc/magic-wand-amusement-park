using System;
using System.IO;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class HciLighterAgencyChecks
{
    const string Key="MagicMR.LighterAgencyChecks";
    static string Output=>Path.GetFullPath(".codex-tmp/hci-lighter-agency");
    static RealityEditor editor;static LighterAgencyVisual effect;static GameObject root;static Camera camera;
    static Vector3 anchor;static int phase,errors;static double next,started;static float minimum=100,maximum=-100;
    static Mesh mesh;static int coughs;
    static HciLighterAgencyChecks(){EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};}
    public static void Run(){TabletopGestureChecks.Run();Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Later(int p,double wait=1){phase=p;next=EditorApplication.timeSinceStartup+wait;}
    static void Quiet(){Require(!effect.Visible&&effect.SmokeCount==0,"Inactive wings/smoke retained");}
    static void Capture(string name){
        var rt=new RenderTexture(900,900,24);var pixels=new Texture2D(900,900,TextureFormat.RGB24,false);var active=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,900,900),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
    }
    static void Cast()=>editor.ApplyDimension(EditDimension.Agency,anchor,true);
    static void Tick(){
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying){if(SessionState.GetBool(Key+".Done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}return;}
        try{
            Require(errors==0,"Runtime error");
            if(phase==0){
                started=EditorApplication.timeSinceStartup;camera=new GameObject("Agency review camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=.075f;camera.nearClipPlane=.01f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.34f,.37f,.39f);
                var key=new GameObject("Studio light").AddComponent<Light>();key.type=LightType.Directional;key.intensity=2;key.transform.rotation=Quaternion.Euler(40,-20,0);
                RenderSettings.ambientLight=Color.gray;
                root=new GameObject("Registered production lighter mesh fixture");root.transform.localScale=new Vector3(.025f,.08f,.012f);root.transform.rotation=Quaternion.Euler(12,37,0);
                foreach(var name in new[]{"RoundedBody","Collar","WindShield","SerratedWheel","ThumbLever","Vent0","Vent1","Vent2","Vent3","Vent4","BaseSeam"}){
                    var body=new GameObject(name);body.transform.SetParent(root.transform,false);body.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/MagicMR/LighterGeometry/"+name+".asset");
                    string mat=name=="RoundedBody"?"BlueGlossPlastic":name=="WindShield"||name=="SerratedWheel"?"BrushedSteel":name=="ThumbLever"?"IgnitionLever":"DarkRecess";
                    body.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/MagicMR/LighterGeometry/"+mat+".mat");
                }
                editor=root.AddComponent<RealityEditor>();root.GetComponent<Rigidbody>().isKinematic=true;editor.RegisterVisualBounds(Vector3.zero,.08f);anchor=root.transform.position;
                camera.transform.position=anchor+new Vector3(.14f,.065f,.30f);camera.transform.LookAt(anchor);Cast();effect=root.GetComponent<LighterAgencyVisual>();Require(effect,"B missing decorative component");mesh=effect.SharedWingMesh;Require(mesh.vertexCount==16&&mesh.triangles.Length==36,"Wing mesh budget changed");
                Require(Array.TrueForAll(mesh.normals,n=>n.sqrMagnitude>.5f),"Degenerate membrane normals");Require(MagicMRAudioFactory.Cough==MagicMRAudioFactory.Cough&&MagicMRAudioFactory.Cough.samples==10143,"Cough audio cache/budget changed");Later(1,.55);return;
            }
            Require(EditorApplication.timeSinceStartup-started<90,"Timeout");
            if(effect.Visible){minimum=Mathf.Min(minimum,effect.WingAngle);maximum=Mathf.Max(maximum,effect.WingAngle);Require(effect.SmokeCount<=14,"Smoke pool overflow");}
            if(phase<3)Require(Vector3.Distance(root.transform.position,anchor)<.00001f,"Agency moved tracking anchor");
            if(EditorApplication.timeSinceStartup<next)return;
            if(phase==1){Require(effect.WingReach>.04f,"Nonuniform authored scale crushed wings");Require(effect.Visible&&effect.CoughCount==1&&effect.SmokeCount>0,"First timed cough absent");Require(root.transform.Find("AgencyEyes").localPosition.z>0,"Front eyes hidden inside body");Capture("B-first-cough");camera.transform.position=anchor+new Vector3(-.14f,.065f,-.30f);camera.transform.LookAt(anchor);Later(2,3.5);}
            else if(phase==2){Require(maximum-minimum>20&&effect.CoughCount>=2,"Wing motion or periodic cough absent");Require(root.transform.Find("AgencyEyes").localPosition.z<0,"Eyes did not face rear viewer");Capture("B-wing-flap");coughs=effect.CoughCount;Cast();Require(root.GetComponent<LighterAgencyVisual>()==effect&&effect.SharedWingMesh==mesh&&effect.CoughCount==0,"Repeated B allocated or did not restart");Later(3,.55);}
            else if(phase==3){Require(effect.CoughCount==1,"Repeated B cough missing");editor.TriggerRule();Quiet();Later(4);}
            else if(phase==4){editor.ResetTarget();Quiet();Cast();Later(5,.55);}
            else if(phase==5){editor.TriggerAppearance();Quiet();Cast();Later(6,.55);}
            else if(phase==6){editor.SendMessage("OnApplicationPause",true);Quiet();Cast();Later(7,.55);}
            else if(phase==7){root.SetActive(false);Quiet();root.SetActive(true);Cast();Later(8,.55);}
            else if(phase==8){editor.TriggerDeconstruction();Quiet();Later(9,1);}
            else if(phase==9){editor.ResetTarget();Quiet();Require(effect.SharedWingMesh==mesh&&root.GetComponents<LighterAgencyVisual>().Length==1,"Lifetime resource grew");Require(root.GetComponents<AudioSource>().Length==1,"Cough added AudioSource");Capture("Reset");File.WriteAllText(Output+"/result.txt","PASS: real RealityEditor.ApplyDimension B on production mesh fixture with nonuniform authored scale and rotated root; shared 16-vertex/12-triangle membrane, bounded flap, repeated timed cough, max 14 smoke particles, repeat/reset/A/C/D/pause/hide cleanup, fixed Agency root, tabletop gesture regression, no resource growth, single AudioSource; previous coughs="+coughs+"; errors=0. Not live YOLO/hand, saved-scene art or PICO/audible output acceptance.\n");SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();}
        }catch(Exception e){SessionState.SetBool(Key,false);File.WriteAllText(Output+"/result.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
