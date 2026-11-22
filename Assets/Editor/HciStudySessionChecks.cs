using System;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Synthetic lifecycle checks using saved scenes, never participant evidence.
[InitializeOnLoad]
public static class HciStudySessionChecks
{
    const string Key="MagicMR.HciStudySessionChecks";
    static string Output=>Path.GetFullPath(".codex-tmp/hci-study-session");
    static int phase,errors;
    static double next,started;
    static DataLogger logger;
    static string sessionPath,subject;
    static GestureManager oldManager;
    static HciStudySessionChecks(){EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors++;};}
    static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
    public static void Run(){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Done",false);EditorSceneManager.OpenScene("Assets/Scenes/ParkPlant.unity");EditorApplication.EnterPlaymode();}
    static void Load(string scene){EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/"+scene+".unity",new LoadSceneParameters(LoadSceneMode.Single));next=EditorApplication.timeSinceStartup+2;}
    static void AssertSession(){
        Require(logger&&logger==DataLogger.Instance&&logger.SessionActive,"Persistent session lost");
        Require(logger.EventFilePath==sessionPath&&logger.SubjectId==subject&&logger.TrialId==7&&logger.ConditionId==3,"Session identity/condition changed across scenes");
        Require(UnityEngine.Object.FindObjectsByType<DataLogger>(FindObjectsSortMode.None).Length==1,"Duplicate logger");
    }
    static void AssertManager(){
        var manager=UnityEngine.Object.FindFirstObjectByType<GestureManager>();Require(manager,"Missing lighter manager");
        Require(manager.SubjectId==subject&&manager.TrialId==7&&manager.Condition==logger.Condition,"Lighter manager did not join session");
        oldManager=manager;
    }
    static void AssertNoLighterConsumer(){
        Require(!oldManager,"Old lighter manager persisted into Park");
        Require(!UnityEngine.Object.FindObjectsByType<GestureManager>(FindObjectsSortMode.None).Any(m=>m.isActiveAndEnabled),"Active lighter consumer in Park");
    }
    static void Tick(){
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying){if(SessionState.GetBool(Key+".Done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}return;}
        try{
            if(phase==0){started=EditorApplication.timeSinceStartup;phase=1;next=started+2;return;}
            Require(errors==0,"Runtime error");Require(EditorApplication.timeSinceStartup-started<90,"Timeout");if(EditorApplication.timeSinceStartup<next)return;
            if(phase==1){
                var park=UnityEngine.Object.FindFirstObjectByType<ParkController>();Require(park,"Missing Park");park.simulation=true;
                Require(park.StartStudySession("SYNTHETIC_SESSION_CHECK",7),"Session not started");
                UnityEngine.Object.FindFirstObjectByType<ExperimentConditionSwitcher>().SetCondition(3);
                logger=DataLogger.Instance;sessionPath=logger.EventFilePath;subject=logger.SubjectId;
                logger.StartSession("DO_NOT_REPLACE", "C1",99);AssertSession();
                logger.LogEvent("synthetic_csv",EditDimension.None,-1,-1,notes:"line one, \"quoted\"\r\nline two");
                phase=2;Load("BridgeStereoFusion");return;
            }
            if(phase==2){AssertSession();AssertManager();phase=3;Load("MagicMR");return;}
            if(phase==3){Require(!oldManager,"Old lighter manager persisted");AssertSession();AssertManager();phase=4;Load("ParkPet");return;}
            if(phase==4){
                AssertNoLighterConsumer();AssertSession();
                var park=UnityEngine.Object.FindFirstObjectByType<ParkController>();Require(park&&park.StudyDimensions==ExperimentConditionSwitcher.DimensionsFor(3),"Park did not inherit condition");
                logger.EndSession();logger.EndSession();File.Copy(sessionPath,Output+"/synthetic-cross-scene.csv",true);
                var original=File.ReadAllBytes(sessionPath);logger.ConfigureLogging(true,float.NaN);
                const string unusual="SIM_TEST../\\name,\"quoted\"\r\nsecond line";
                logger.StartSession(unusual,"C4",9);var pathA=logger.EventFilePath;
                Require(string.Equals(Path.GetFullPath(Path.GetDirectoryName(pathA)),Path.GetFullPath(Path.Combine(Application.persistentDataPath,"StudyLogs")),StringComparison.OrdinalIgnoreCase),"Filename escaped StudyLogs");
                logger.LogHandTrajectory(new Vector3(float.NaN,0,0),Vector3.zero);
                for(int i=0;i<100;i++)logger.LogHandTrajectory(Vector3.one,Vector3.zero);
                logger.EndSession();File.Copy(pathA,Output+"/synthetic-escaped.csv",true);File.Copy(logger.TrajectoryFilePath,Output+"/synthetic-throttled.csv",true);
                logger.StartSession(unusual,"C4",10);Require(logger.EventFilePath!=pathA&&logger.EventFilePath!=sessionPath,"Rapid sessions collided");logger.EndSession();
                Require(original.SequenceEqual(File.ReadAllBytes(sessionPath)),"Prior session overwritten");
                UnityEngine.Object.DestroyImmediate(logger.gameObject);phase=5;Load("MagicMR");return;
            }
            if(phase==5){
                logger=DataLogger.Instance;Require(logger&&logger.SessionActive,"Standalone MagicMR session missing");
                // This automatic legacy session is synthetic: the whole run is synthetic.
                Require(logger.GetComponent<GestureManager>()==null&&logger.GetComponentInChildren<TabletopGestureRecognizer>()==null,"Logger owns scene gesture consumers");
                sessionPath=logger.EventFilePath;oldManager=UnityEngine.Object.FindFirstObjectByType<GestureManager>();Require(oldManager,"Fresh lighter manager missing");
                phase=6;Load("ParkPlant");return;
            }
            if(phase==6){
                AssertNoLighterConsumer();
                Require(logger&&logger.SessionActive&&logger.EventFilePath==sessionPath,"Fresh session lost crossing scene");
                logger.EndSession();File.Copy(sessionPath,Output+"/synthetic-legacy-bootstrap.csv",true);
                File.WriteAllText(Output+"/result.txt","PASS: saved ParkPlant -> BridgeStereoFusion -> MagicMR -> ParkPet preserves one session, subject, C3 and trial 7; no scene-owned GestureManager survives. Repeated start/end protected, rapid files unique, original CSV preserved, special-character identity confined to StudyLogs, finite throttled trajectory. Fresh MagicMR -> ParkPlant also releases old gesture consumers. Synthetic only; no real YOLO, XR hands or PICO acceptance.\n");
                SessionState.SetBool(Key+".Done",true);EditorApplication.ExitPlaymode();
            }
        }catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL: "+e);SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(2);}
    }
}
