#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicMR;
using Perception;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Runs the real MonoBehaviour lifecycle in an isolated batch Play Mode scene.
// The event probe is synthetic; no live YOLO, image decoding or device acceptance.
[InitializeOnLoad]
public static class ParkSharedInterfaceLifecycleChecks
{
    const string Key="MagicMR.SharedInterfaceLifecycle";
    static string Output=>Path.GetFullPath(".codex-tmp/shared-interface-lifecycle");
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly string[] Lists={"buttons","plantSimulationButtons","petSimulationButtons","bugSimulationButtons","treeSimulationButtons","statueSimulationButtons","roomExperienceButtons","plantExperienceButtons"};
    static readonly string[] Panels={"Shared status","Shared scene menu","Shared YOLO detection","Choose recognized target"};
    static GameObject host;
    static ParkSharedInterface ui;
    static ParkController controller;
    static ParkStereoReceiver receiver;
    static PicoYoloFrameSender sender;
    static int phase,cycle,errors,changedFrame;
    static int[] counts;
    static double started;
    static ParkSharedInterfaceLifecycleChecks()
    {
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};
    }
    public static void Run()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Isolated batch fixture only; do not replace a user's scene.");
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void Require(bool valid,string message){if(!valid)throw new Exception(message);}
    static object Field(object owner,string name)=>owner.GetType().GetField(name,Flags).GetValue(owner);
    static int Count(string name)=>((ICollection)Field(ui,name)).Count;
    static int Listeners(object owner,string name)
    {
        var callbacks=(Delegate)Field(owner,name);
        return callbacks==null?0:callbacks.GetInvocationList().Count(d=>ReferenceEquals(d.Target,ui));
    }
    static GameObject[] OwnedPanels()=>Camera.main?Camera.main.GetComponentsInChildren<Canvas>(true)
        .Where(c=>Array.IndexOf(Panels,c.name)>=0).Select(c=>c.gameObject).ToArray():Array.Empty<GameObject>();
    static void CheckReady()
    {
        Require((bool)Field(ui,"interfaceInitialized"),"UI not initialized after camera/re-enable");
        var panels=OwnedPanels();
        Require(panels.Length==4&&Panels.All(n=>panels.Count(p=>p.name==n)==1),"Missing/duplicate panels after deferred destroy");
        Require((UnityEngine.UI.RawImage)Field(ui,"matchedPreview"),"Exact-frame preview not recreated");
        for(int i=0;i<Lists.Length;i++)Require(Count(Lists[i])==counts[i],"Button list grew/lost entries: "+Lists[i]);
        Require(Listeners(sender,"EncodedFrameSent")==1,"Preview subscription missing/duplicated");
        Require(Listeners(receiver,"RecognitionAccepted")==1&&Listeners(receiver,"DiscoverySceneRequested")==1,"Recognition subscription missing/duplicated");
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
            if(phase>0)Require(EditorApplication.timeSinceStartup-started<30,"Lifecycle check timeout");
            if(phase==0)
            {
                started=EditorApplication.timeSinceStartup;
                Require(!Camera.main,"Fixture must begin without a main camera");
                host=new GameObject("Shared interface lifecycle fixture");host.SetActive(false);
                controller=host.AddComponent<ParkController>();controller.enabled=false;controller.sharedInterface=true;
                receiver=host.AddComponent<ParkStereoReceiver>();receiver.enabled=false;
                sender=host.AddComponent<PicoYoloFrameSender>();sender.enabled=false;receiver.sender=sender;
                ui=host.AddComponent<ParkSharedInterface>();ui.controller=controller;ui.receiver=receiver;
                host.SetActive(true);phase=1;return;
            }
            if(phase==1)
            {
                if(!(bool)Field(ui,"interfaceStarted"))return;
                Require(!(bool)Field(ui,"interfaceInitialized")&&Count("buttons")==0,"UI initialized without a camera");
                var camera=new GameObject("Late lifecycle fixture camera").AddComponent<Camera>();camera.tag="MainCamera";
                phase=2;return;
            }
            if(phase==2)
            {
                if(!(bool)Field(ui,"interfaceInitialized"))return;
                counts=Lists.Select(Count).ToArray();Require(counts.All(n=>n>0),"Missing scenario menu coverage");
                CheckReady();phase=3;return;
            }
            if(phase==3)
            {
                // Six cycles cover the actual menu visibility branches for all Park scenarios.
                controller.scenario=(ParkScenario)cycle;controller.simulation=true;
                controller.SetRightHandUiCapture(true);controller.SetLeftHandUiCapture(true);
                typeof(ParkSharedInterface).GetProperty("DesktopPointerCaptured").SetValue(null,true);
                var callback=(Delegate)Field(sender,"EncodedFrameSent");callback.DynamicInvoke((uint)(100+cycle),new byte[]{1,2,3});
                Require(Count("sentImages")==1&&Count("sentImageOrder")==1,"Preview callback duplicated");
                ui.enabled=false;
                Require(OwnedPanels().All(p=>!p.activeInHierarchy),"Disabled UI remained visible before Destroy completed");
                Require(Lists.All(n=>Count(n)==0)&&Count("sentImages")==0&&Count("sentImageOrder")==0,"Disable retained menu/image state");
                Require(!ParkSharedInterface.DesktopPointerCaptured&&!(bool)Field(controller,"rightHandUiCapture")&&!(bool)Field(controller,"leftHandUiCapture"),"Disable retained hand/pointer capture");
                Require(Listeners(sender,"EncodedFrameSent")==0&&Listeners(receiver,"RecognitionAccepted")==0&&Listeners(receiver,"DiscoverySceneRequested")==0,"Disable retained subscriptions");
                ui.enabled=true;changedFrame=Time.frameCount;phase=4;return;
            }
            if(phase==4)
            {
                if(Time.frameCount<=changedFrame)return;
                CheckReady();cycle++;
                if(cycle<6){phase=3;return;}
                host.SetActive(false);changedFrame=Time.frameCount;phase=5;return;
            }
            if(phase==5)
            {
                if(Time.frameCount<=changedFrame)return;
                Require(OwnedPanels().Length==0,"Inactive host leaked camera-parented UI");
                host.SetActive(true);changedFrame=Time.frameCount;phase=6;return;
            }
            if(phase==6)
            {
                if(Time.frameCount<=changedFrame)return;
                CheckReady();
                File.WriteAllText(Output+"/result.txt","PASS: late-camera initialization, six scenario disable/re-enable cycles, deferred-destroy panel uniqueness, exact-frame preview and event rebinding, bounded menu lists, capture/cache release, inactive-host cleanup/recovery. Synthetic event bytes only; not YOLO, image decoding, rendering quality, Android or PICO acceptance.\n");
                SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();
            }
        }
        catch(Exception error){SessionState.SetBool(Key,false);File.WriteAllText(Output+"/result.txt","FAIL "+error);Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
#endif
