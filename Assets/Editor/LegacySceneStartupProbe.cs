using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Loads the actual scenes without injecting simulated tracking, suppressing
// their components, repositioning cameras, or creating replacement models.
[InitializeOnLoad]
public static class LegacySceneStartupProbe
{
    const string Key="MagicMR.LegacyStartup";
    static readonly string[] LegacyNames={"BridgeTest","BridgeStereoFusion","BridgeStereoPoseFusion","BridgeStereoRepair","MagicMR","VstTest","SampleScene","DesktopCaptureDemo"};
    static readonly string[] AllNames=LegacyNames.Concat(ParkContentBuilder.Names).ToArray();
    static string[] Names=>SessionState.GetBool(Key+".AllProject",false)?AllNames:LegacyNames;
    static readonly string Output=Path.GetFullPath(".codex-tmp/legacy-startup");
    static readonly List<string> errors=new List<string>(),warnings=new List<string>();
    static int errorCount,warningCount;
    static bool started;
    static double startTime;
    [Serializable] sealed class SceneResult
    {
        public string scene,scope="Unmodified scene, actual Windows Editor Play Mode startup; no real camera/hand/AR/network service success claim";
        public int projectComponents,errors,warnings,realityEditors,activeRealityEditors,activeCameras;
        public string[] componentTypes,errorMessages,warningMessages,cameras;
        public bool capturePresent,captureTargetAssigned,captureCardAssigned,captureBackpackAssigned;
        public string snapshot;
        public int changedPixels;
        public string cameraInputStatus,editorImageStatus,tcpHost;
        public bool cameraHasFrame,editorImageSending;
        public int parkActors,activeParkActors;
        public string parkScenario;
    }
    static LegacySceneStartupProbe(){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    public static void Run()=>Start(false);
    public static void RunAllProjectScenes()=>Start(true);
    static void Start(bool allProject)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Startup probe requires idle editor");
        ProjectSceneAudit.Run();Directory.CreateDirectory(Output);
        SessionState.SetBool(Key+".AllProject",allProject);
        SessionState.SetString(Key+".Previous",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        SessionState.SetInt(Key+".Index",0);SessionState.SetString(Key+".Phase","open");SessionState.SetBool(Key,true);
    }
    public static void UpgradeAndRun(){LegacySceneMaintenance.Run();Run();}
    static void OnLog(string message,string stack,LogType type)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert){errorCount++;if(errors.Count<32)errors.Add(message.Length>700?message.Substring(0,700):message);}
        if(type==LogType.Warning){warningCount++;if(warnings.Count<32)warnings.Add(message.Length>500?message.Substring(0,500):message);}
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
                string phase=SessionState.GetString(Key+".Phase","");
                if(phase=="next")
                {
                    index++;SessionState.SetInt(Key+".Index",index);SessionState.SetString(Key+".Phase","open");
                    if(index>=Names.Length){Finish();return;}
                }
                if(SessionState.GetString(Key+".Phase","")!="open")return;
                started=false;errors.Clear();warnings.Clear();errorCount=warningCount=0;
                EditorSceneManager.OpenScene("Assets/Scenes/"+Names[index]+".unity",OpenSceneMode.Single);
                Debug.Log("LEGACY_STARTUP_BEGIN "+Names[index]);SessionState.SetString(Key+".Phase","entering");EditorApplication.EnterPlaymode();return;
            }
            if(!started){started=true;startTime=EditorApplication.timeSinceStartup;return;}
            if(SessionState.GetString(Key+".Phase","")=="next")return;
            if(EditorApplication.timeSinceStartup-startTime<2.5)return;
            Record(Names[index]);SessionState.SetString(Key+".Phase","next");EditorApplication.ExitPlaymode();
        }
        catch(Exception e)
        {
            SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(2);
        }
    }
    static void Record(string name)
    {
        var components=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(ProjectSceneAudit.IsProjectBehaviour).ToArray();
        var actors=UnityEngine.Object.FindObjectsByType<RealityEditor>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        var cameras=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        var capture=UnityEngine.Object.FindFirstObjectByType<CaptureTo2D>();
        var cameraInput=UnityEngine.Object.FindFirstObjectByType<Perception.PicoCameraCapture>();
        var imageInput=UnityEngine.Object.FindFirstObjectByType<Perception.PicoYoloEditorTestSource>();
        var tcp=UnityEngine.Object.FindFirstObjectByType<Perception.TcpClient>();
        var parkActors=UnityEngine.Object.FindObjectsByType<ParkActor>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        var controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();
        var result=new SceneResult{scene=name,projectComponents=components.Length,componentTypes=components.Select(c=>c.GetType().FullName+" ["+c.GetType().Assembly.GetName().Name+"] enabled="+c.enabled+" active="+c.gameObject.activeInHierarchy).OrderBy(c=>c).ToArray(),
            realityEditors=actors.Length,activeRealityEditors=actors.Count(a=>a.isActiveAndEnabled),activeCameras=cameras.Count(c=>c.isActiveAndEnabled),
            cameras=cameras.Select(c=>c.name+" enabled="+c.enabled+" active="+c.gameObject.activeInHierarchy+" tag="+c.tag+" clear="+c.clearFlags+" alpha="+c.backgroundColor.a).ToArray(),
            capturePresent=capture,captureTargetAssigned=capture&&capture.target3DObject,captureCardAssigned=capture&&capture.card2DUI,captureBackpackAssigned=capture&&capture.backpackIcon,
            cameraInputStatus=cameraInput?cameraInput.Status.ToString():"NO_CAMERA_COMPONENT",cameraHasFrame=cameraInput&&cameraInput.HasFrame,
            editorImageStatus=imageInput?imageInput.Status:"NO_EDITOR_SOURCE",editorImageSending=imageInput&&imageInput.IsSending,tcpHost=tcp?tcp.Host:"NO_TCP_COMPONENT",
            parkActors=parkActors.Length,activeParkActors=parkActors.Count(a=>a.isActiveAndEnabled),parkScenario=controller?controller.scenario.ToString():"NO_PARK_CONTROLLER"};
        var camera=Camera.main;if(!camera||!camera.isActiveAndEnabled)camera=cameras.FirstOrDefault(c=>c.isActiveAndEnabled);
        if(camera)
        {
            var target=new RenderTexture(640,480,24);var pixels=new Texture2D(640,480,TextureFormat.RGB24,false);var oldTarget=camera.targetTexture;var old=RenderTexture.active;
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,640,480),0,0);pixels.Apply();var colors=pixels.GetPixels32();var background=colors[0];
                result.changedPixels=colors.Count(c=>Math.Abs(c.r-background.r)+Math.Abs(c.g-background.g)+Math.Abs(c.b-background.b)>18);
                var png=pixels.EncodeToPNG();if(png.Length>3*1024*1024)throw new Exception("Startup snapshot exceeded cap");
                result.snapshot=name+".png";File.WriteAllBytes(Path.Combine(Output,result.snapshot),png);
            }
            finally{camera.targetTexture=oldTarget;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
        }
        result.errors=errorCount;result.warnings=warningCount;result.errorMessages=errors.ToArray();result.warningMessages=warnings.ToArray();
        File.WriteAllText(Path.Combine(Output,name+".json"),JsonUtility.ToJson(result,true));
        Debug.Log("LEGACY_STARTUP_RECORDED "+name+" errors="+result.errors+" warnings="+result.warnings+" actors="+result.realityEditors+" active="+result.activeRealityEditors+" cameras="+result.activeCameras+" changed_pixels="+result.changedPixels);
    }
    static void Finish()
    {
        SessionState.SetBool(Key,false);int totalErrors=0;
        foreach(var name in Names)totalErrors+=JsonUtility.FromJson<SceneResult>(File.ReadAllText(Path.Combine(Output,name+".json"))).errors;
        string previous=SessionState.GetString(Key+".Previous","");if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);
        File.WriteAllText(Path.Combine(Output,"result.txt"),"PROBE_COMPLETE: "+Names.Length+" actual scene startups; "+totalErrors+" error logs. Inspect per-scene JSON and snapshots; this is baseline evidence, not all-scene acceptance.\n");
        Debug.Log("LEGACY_STARTUP_PROBE_COMPLETE: "+Names.Length+" actual scenes; error logs="+totalErrors+"; inspect baseline before accepting");EditorApplication.Exit(totalErrors>0?1:0);
    }
}
