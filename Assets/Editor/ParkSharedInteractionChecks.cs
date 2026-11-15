using UnityEditor;
using UnityEngine;
using MagicMR;
using Perception;
using System;
using System.IO;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

public static class ParkSharedInteractionChecks
{
    public static void CheckDefaultBuildEntry()
    {
        var enabled=EditorBuildSettings.scenes.Where(s=>s.enabled).ToArray();
        if(enabled.Length==0||enabled[0].path!="Assets/Scenes/BridgeStereoFusion.unity")
            throw new Exception("Ordinary Build/Build And Run must start at the shared lighter entry");
        foreach(var name in ParkContentBuilder.Names)
            if(enabled.Count(s=>s.path=="Assets/Scenes/"+name+".unity")!=1)
                throw new Exception("Missing or duplicate enabled Park scene: "+name);
        Debug.Log("DEFAULT_BUILD_ENTRY_PASS: main scene first, six Park scenes available exactly once");
    }
    public static void DiagnoseScene() {
        var script=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/MagicMR/ParkController.cs");
        Debug.Log("PARK_SCRIPT_TYPE="+(script?script.GetClass()?.AssemblyQualifiedName:"missing asset"));
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/ParkTree.unity",OpenSceneMode.Single);
        foreach(var root in scene.GetRootGameObjects()) {
            foreach(var component in root.GetComponentsInChildren<Component>(true))
                Debug.Log("PARK_SCENE_COMPONENT="+(component?component.gameObject.name+":"+component.GetType().FullName:"MISSING"));
        }
    }
    public static void RepairSceneRegistrations() {
        var registry=new ParkActor[6];
        for(int i=0;i<6;i++) {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/"+ParkContentBuilder.Names[i]+".prefab");
            registry[i]=prefab?prefab.GetComponent<ParkActor>():null;
            if(!registry[i])throw new Exception("Missing scene actor prefab: "+ParkContentBuilder.Names[i]);
        }
        foreach(var name in ParkContentBuilder.Names) {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity",OpenSceneMode.Additive);
            try {
                foreach(var root in scene.GetRootGameObjects()) {
                    var controller=root.GetComponentInChildren<ParkController>(true);
                    if(!controller)continue;
                    controller.registeredActors=registry;EditorUtility.SetDirty(controller);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene registration save failed: "+name);
                }
            } finally {EditorSceneManager.CloseScene(scene,true);}
        }
        Run();
    }
    [MenuItem("MagicMR/Checks/Shared Interaction Regression")]
    public static void Run()
    {
        ParkGestureSafetyChecks.Run();
        ParkRegressionChecks.Run();
        SharedInterfaceChecks.Run();
        RightHandSpellVfxChecks.Run();
        CheckLighterEntry();
        ExportGestureConfiguration();
        Debug.Log("PARK_SHARED_INTERACTION_REGRESSION_PASS: six scene fixtures, UI replay and lighter gestures/VFX; editor only, no device validation");
    }
    static void CheckLighterEntry() {
        CheckSceneSerialization("Assets/Scenes/BridgeStereoFusion.unity");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/BridgeStereoFusion.unity",OpenSceneMode.Additive);
        try {
            bool ui=false,sender=false,gestures=false;
            foreach(var root in scene.GetRootGameObjects()) {
                foreach(var node in root.GetComponentsInChildren<Transform>(true))
                    if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject)>0)throw new Exception("Missing lighter entry script: "+node.name);
                ui|=root.GetComponentInChildren<ParkSharedInterface>(true)!=null;
                sender|=root.GetComponentInChildren<PicoYoloFrameSender>(true)!=null;
                gestures|=root.GetComponentInChildren<GestureManager>(true)!=null;
            }
            if(!ui || !sender || !gestures)throw new Exception("Lighter shared entry incomplete: UI="+ui+" sender="+sender+" gestures="+gestures);
            Debug.Log("PARK_LIGHTER_ENTRY_LOAD_PASS: shared UI, sender and existing gesture manager; editor loading only");
        } finally {EditorSceneManager.CloseScene(scene,true);}
    }
    static void CheckSceneSerialization(string path) {
        string text=File.ReadAllText(path);var ids=new HashSet<long>();
        foreach(Match match in Regex.Matches(text,@"^--- !u!\d+ &(-?\d+)",RegexOptions.Multiline)) {
            if(!long.TryParse(match.Groups[1].Value,out long id) || !ids.Add(id))throw new Exception("Invalid or duplicate scene object ID: "+path+" "+match.Groups[1].Value);
        }
        foreach(Match match in Regex.Matches(text,@"\bfileID:\s*(-?\d+)"))
            if(!long.TryParse(match.Groups[1].Value,out _))throw new Exception("Scene reference outside signed 64-bit range: "+path);
        foreach(Match match in Regex.Matches(text,@"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([a-fA-F0-9]+)")) {
            string guid=match.Groups[1].Value;
            if(guid.Length!=32 || string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))throw new Exception("Unresolved scene script GUID: "+path+" "+guid);
        }
        Debug.Log("PARK_SCENE_SERIALIZATION_PASS: "+path+" objects="+ids.Count);
    }
    [Serializable] class GestureReport {
        public string generatedUtc,unityVersion,deviceValidation="PENDING_NO_PICO_TEST";
        public string configurationSource="Assets/Scenes/Park*.unity, loaded by Unity; missing serialized fields use current runtime defaults";
        public string handPolicy="All production magic uses TabletopHandSample + TabletopGestureRules + GestureReachWindow. Right hand: A pinch-release, B point-circle, C horizontal palm swipe, D fist-open. UI/Fit/Plane capture cancels casting; release required after interruption.";
        public string lighterPolicy="Lighter and all six Park profiles share classification thresholds. Different targets dispatch different effects. Synthetic editor checks do not validate YOLO or PICO.";
        public float sampleGapSeconds=TabletopGestureRules.MaximumSampleGapSeconds;
        public float pinchEnterMeters=TabletopGestureRules.PinchEnterMeters,pinchReleaseMeters=TabletopGestureRules.PinchReleaseMeters;
        public float pinchMinimumSeconds=TabletopGestureRules.PinchMinimumSeconds,pinchMaximumSeconds=TabletopGestureRules.PinchMaximumSeconds;
        public float fistMinimumSeconds=TabletopGestureRules.FistMinimumSeconds,cooldownSeconds=TabletopGestureRules.RepeatCooldownSeconds;
        public float reachEnterMeters=GestureReachWindow.EnterMeters,reachExitMeters=GestureReachWindow.ExitMeters,reachGraceSeconds=GestureReachWindow.GraceSeconds;
        public GestureScene[] scenes;
    }
    [Serializable] class GestureScene {
        public string scene,hint;
        public string configurationKind;
    }
    static void ExportGestureConfiguration() {
        var report=new GestureReport {generatedUtc=DateTime.UtcNow.ToString("o"),unityVersion=Application.unityVersion,scenes=new GestureScene[6]};
        var fixture=new GameObject("Gesture configuration export");
        try {
            for(int i=0;i<6;i++) {
                CheckSceneSerialization("Assets/Scenes/"+ParkContentBuilder.Names[i]+".unity");
                var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+ParkContentBuilder.Names[i]+".unity",OpenSceneMode.Additive);
                try {
                ParkController defaults=null;
                foreach(var root in scene.GetRootGameObjects()) {
                    foreach(var node in root.GetComponentsInChildren<Transform>(true))
                        if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject)>0)
                            throw new Exception("Missing script in "+scene.path+": "+node.name);
                    var found=root.GetComponentInChildren<ParkController>(true);
                    if(found) {if(defaults)throw new Exception("Multiple controllers in "+scene.path);defaults=found;}
                }
                if(!defaults || defaults.scenario!=(ParkScenario)i || !defaults.sharedInterface || defaults.registeredActors==null || defaults.registeredActors.Length!=6)
                    throw new Exception("Incomplete generated scene registration: "+scene.path+" controller="+(defaults!=null)+" scenario="+(defaults?defaults.scenario.ToString():"missing")+" sharedUI="+(defaults && defaults.sharedInterface)+" registry="+(defaults?.registeredActors?.Length??-1));
                for(int n=0;n<6;n++)if(!defaults.registeredActors[n] || defaults.registeredActors[n].scenario!=(ParkScenario)n)
                    throw new Exception("Wrong registered actor slot "+n+" in "+scene.path);
                var receiver=defaults.GetComponent<ParkStereoReceiver>();var ui=defaults.GetComponent<ParkSharedInterface>();
                if(!receiver || receiver.controller!=defaults || defaults.receiver!=receiver || !ui || ui.controller!=defaults || ui.receiver!=receiver || !receiver.sender || !receiver.transport)
                    throw new Exception("Shared recognition wiring incomplete: "+scene.path+" receiver="+(receiver!=null)+" UI="+(ui!=null)+" sender="+(receiver && receiver.sender)+" TCP="+(receiver && receiver.transport));
                report.scenes[i]=new GestureScene {
                    scene=scene.path,hint=defaults.GestureHint,configurationKind="Actual loaded scene wiring; live shared rule constants above. Legacy serialized Park gesture tuning fields are not used by production magic recognition."
                };
                if(string.IsNullOrEmpty(report.scenes[i].hint))throw new Exception("Missing gesture hint for "+defaults.scenario);
                } finally {EditorSceneManager.CloseScene(scene,true);}
            }
        } finally {UnityEngine.Object.DestroyImmediate(fixture);}
        Directory.CreateDirectory("Reports");File.WriteAllText("Reports/shared-gesture-scene-configuration.json",JsonUtility.ToJson(report,true));
        Debug.Log("PARK_GESTURE_CONFIGURATION_EXPORTED: actual six scene controllers and registry verified; PICO validation pending");
    }
}
