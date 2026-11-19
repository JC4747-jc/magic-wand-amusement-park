#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Synthetic calls exercise the real restored controller; they do not prove hand/YOLO/PICO input.
[InitializeOnLoad]
public static class LighterSnapshotChecks
{
    const string Key="MagicMR.LighterSnapshotChecks";
    static string Output=>Path.GetFullPath(".codex-tmp/lighter-snapshot-validation");
    static RealityEditor target;
    static GameObject root;
    static Camera camera;
    static Vector3 anchor;
    static int phase, errors;
    static double next, started;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static LighterSnapshotChecks()
    {
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};
    }
    public static void Run()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Run in an isolated batch fixture; do not replace a user's open scene.");
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output+"/result.txt","RUNNING\n");
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Later(int value,double seconds){phase=value;next=EditorApplication.timeSinceStartup+seconds;}
    static void Set(string field,object value)=>typeof(RealityEditor).GetField(field,Flags).SetValue(target,value);
    static GameObject Get(string field)=>(GameObject)typeof(RealityEditor).GetField(field,Flags).GetValue(target);
    static void Cast(EditDimension dimension)=>target.ApplyDimension(dimension,anchor,true);
    static void Capture(string name)
    {
        // Review framing only: preserve production asset transforms and controller behavior.
        bool found=false;Bounds visible=new Bounds();
        Action<GameObject> include=owner=>{
            if(!owner)return;
            foreach(var renderer in owner.GetComponentsInChildren<MeshRenderer>(false))
                if(renderer.enabled){if(!found){visible=renderer.bounds;found=true;}else visible.Encapsulate(renderer.bounds);}
        };
        include(root);include(Get("m_SpawnedFlower"));Require(found,"No visible meshes to frame");
        camera.orthographicSize=Mathf.Max(.02f,Mathf.Max(visible.extents.x,visible.extents.y)*1.2f);
        float distance=Mathf.Max(.25f,visible.size.magnitude*2);
        camera.farClipPlane=distance*4+1;
        camera.transform.position=visible.center+Vector3.back*distance;
        camera.transform.LookAt(visible.center);
        for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){
            var corner=visible.center+Vector3.Scale(visible.extents,new Vector3(x,y,z));
            var viewport=camera.WorldToViewportPoint(corner);
            Require(viewport.z>camera.nearClipPlane&&viewport.x>.02f&&viewport.x<.98f&&viewport.y>.02f&&viewport.y<.98f,"Visible mesh bounds cropped by review camera");
        }
        var texture=new RenderTexture(900,900,24);
        var pixels=new Texture2D(900,900,TextureFormat.RGB24,false);
        var active=RenderTexture.active;
        try{camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;pixels.ReadPixels(new Rect(0,0,900,900),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(pixels);}
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying)
        {
            if(SessionState.GetBool(Key+".Done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}
            return;
        }
        try
        {
            Require(errors==0,"Unity runtime error; inspect native log");
            if(phase==0)
            {
                started=EditorApplication.timeSinceStartup;
                camera=new GameObject("Snapshot review camera").AddComponent<Camera>();camera.tag="MainCamera";
                camera.orthographic=true;camera.orthographicSize=.08f;camera.nearClipPlane=.01f;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.32f,.35f,.38f);
                var light=new GameObject("Snapshot review light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(40,-20,0);
                root=new GameObject("Snapshot lighter controller fixture");root.transform.position=new Vector3(.11f,.95f,.3f);
                foreach(var name in new[]{"RoundedBody","Collar","WindShield","SerratedWheel","ThumbLever","Vent0","Vent1","Vent2","Vent3","Vent4","BaseSeam"})
                {
                    var part=new GameObject(name);part.transform.SetParent(root.transform,false);
                    var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/MagicMR/LighterGeometry/"+name+".asset");Require(mesh,"Missing snapshot mesh: "+name);
                    part.AddComponent<MeshFilter>().sharedMesh=mesh;
                    string material=name=="RoundedBody"?"BlueGlossPlastic":name=="WindShield"||name=="SerratedWheel"?"BrushedSteel":name=="ThumbLever"?"IgnitionLever":"DarkRecess";
                    part.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/MagicMR/LighterGeometry/"+material+".mat");
                }
                target=root.AddComponent<RealityEditor>();root.GetComponent<Rigidbody>().isKinematic=true;
                target.RegisterVisualBounds(Vector3.zero,.08f);anchor=root.transform.position;
                Set("m_BurntMaterial",AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Materials/burnt.mat"));
                Set("m_FlowerPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/LighterSnapshot/Flower.prefab"));
                Set("m_DeconstructionDelay",.2f);
                camera.transform.position=anchor+new Vector3(0,.04f,-.28f);camera.transform.LookAt(anchor+Vector3.up*.04f);
                Require(!target.HciAgencyVisualsEnabled&&!target.TrySetHciAgencyVisuals(true),"Restored lighter enabled a later visual style");
                Capture("00-baseline");Cast(EditDimension.Appearance);Later(1,.3);return;
            }
            if(EditorApplication.timeSinceStartup-started>35)throw new TimeoutException("Snapshot check timeline timeout");
            if(EditorApplication.timeSinceStartup<next)return;
            if(phase==1)
            {
                Require(target.ActiveDimension==EditDimension.Appearance,"Appearance not active");Capture("01-charred");target.ResetTarget();Cast(EditDimension.Agency);Later(2,.3);
            }
            else if(phase==2)
            {
                Require(target.ActiveDimension==EditDimension.Agency,"Agency not active");
                Require(root.transform.Find("AgencyEyes")&&root.transform.Find("AgencyEyes").gameObject.activeSelf,"Snapshot eyes missing");
                Require(!root.GetComponent<LighterAgencyVisual>(),"Snapshot allocated wings/cough component");Require(root.transform.position==anchor,"Agency displaced anchor");
                Capture("02-eyes-pulse");target.ResetTarget();Cast(EditDimension.Rule);Later(3,.08);
            }
            else if(phase==3)
            {
                Require(target.ActiveDimension==EditDimension.Rule,"Rule not active");Require(Vector3.Distance(root.transform.position,anchor)>.0001f,"Rule produced no dodge displacement");
                Capture("03-dodge");Later(4,1.5);
            }
            else if(phase==4)
            {
                Require(Vector3.Distance(root.transform.position,anchor)<.0001f,"Dodge did not return to anchor");
                target.ResetTarget();Cast(EditDimension.Deconstruction);Later(5,.6);
            }
            else if(phase==5)
            {
                Require(target.IsDeconstructed&&Get("m_SpawnedFlower"),"Snapshot flower not spawned");
                Require(!root.transform.Find("RoundedBody").GetComponent<Renderer>().enabled,"Deconstruction retained lighter body");
                Capture("04-snapshot-flower");target.ResetTarget();Later(6,.2);
            }
            else if(phase==6)
            {
                Require(!target.IsDeconstructed&&!Get("m_SpawnedFlower")&&!Get("m_ActiveDeconstructionVfx"),"Reset retained flower/VFX");
                Require(root.transform.Find("RoundedBody").GetComponent<Renderer>().enabled,"Reset did not restore lighter");
                Require(root.transform.position==anchor,"Reset changed anchor");Capture("05-reset");
                File.WriteAllText(Output+"/result.txt","PASS: restored controller synthetic A/B/C/D calls, snapshot eyes/pulse policy, no wings component, dodge/return, isolated snapshot flower, reset cleanup and anchor preservation; six actual native renders saved. Not hand/YOLO input, saved-scene art, Android, audible output or PICO acceptance.\n");
                SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();
            }
        }
        catch(Exception error){SessionState.SetBool(Key,false);File.WriteAllText(Output+"/result.txt","FAIL "+error);Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
#endif
