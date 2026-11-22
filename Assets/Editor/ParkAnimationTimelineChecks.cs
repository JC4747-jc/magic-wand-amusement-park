using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Real Play Mode time sampling, isolated from tracking hardware and network clients.
[InitializeOnLoad]
public static class ParkAnimationTimelineChecks
{
    const string Key="MagicMR.ParkTimeline.Active";
    static readonly List<ParkActor> actors=new List<ParkActor>();
    static readonly List<Vector3[]> initialPositions=new List<Vector3[]>();
    static readonly List<Quaternion[]> initialRotations=new List<Quaternion[]>();
    static readonly List<bool[]> initialVisibility=new List<bool[]>();
    static readonly bool[] changed=new bool[6];
    static Camera camera;
    static ParkAnimationFrameProbe probe;
    static double started,phaseStart;
    static float phaseGame;
    static int stage,sample;
    static readonly float[] sampleTimes={.36f,.55f,.95f};
    static readonly ParkAction[] actions={ParkAction.Wind,ParkAction.Awaken,ParkAction.Awaken,ParkAction.Awaken,ParkAction.Bloom,ParkAction.Awaken};
    static readonly string Output=Path.GetFullPath(".codex-tmp/park-animation-timeline");
    static ParkAnimationTimelineChecks() {EditorApplication.update+=Tick;}
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Timeline requires an idle editor");
        SessionState.SetString(Key+".Scene",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        SessionState.SetInt(Key+".Exit",-1);SessionState.SetBool(Key,true);
        Directory.CreateDirectory(Output);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var key=new GameObject("Key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.4f;key.transform.rotation=Quaternion.Euler(38,-35,0);
        camera=new GameObject("Timeline camera").AddComponent<Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.20f,.22f,.24f);camera.nearClipPlane=.01f;camera.orthographic=true;
        camera.gameObject.AddComponent<AudioListener>();
        EditorApplication.EnterPlaymode();
    }
    static void Require(bool value,string message) {if(!value)throw new Exception(message);}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying)
        {
            int exit=SessionState.GetInt(Key+".Exit",-1);
            if(exit<0)return;
            SessionState.SetBool(Key,false);
            string previous=SessionState.GetString(Key+".Scene","");
            if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);
            EditorApplication.Exit(exit);return;
        }
        try
        {
            if(stage==0)
            {
                started=phaseStart=EditorApplication.timeSinceStartup;camera=Camera.main;
                Require(camera,"Review camera missing after Play Mode reload");
                probe=camera.gameObject.AddComponent<ParkAnimationFrameProbe>();phaseGame=probe.SimulatedSeconds;
                for(int i=0;i<6;i++)
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/"+ParkContentBuilder.Names[i]+".prefab");
                    var go=UnityEngine.Object.Instantiate(prefab);go.SetActive(true);go.transform.position=new Vector3(i*2,0,0);
                    var actor=go.GetComponent<ParkActor>();actor.virtualTarget=true;actor.cooldown=0;actor.transitionSeconds=.35f;actor.Build();
                    if(actor.label)actor.label.gameObject.SetActive(false);actors.Add(actor);
                }
                stage=1;return;
            }
            Require(EditorApplication.timeSinceStartup-started<25,"Play Mode timeline timed out");
            double elapsed=probe.SimulatedSeconds-phaseGame;
            if(stage==1 && elapsed>.5)
            {
                for(int i=0;i<6;i++)
                {
                    var actor=actors[i];Require(actor.EntryProgress>.99f,"Entry animation did not finish");
                    var parts=actor.visual.GetComponentsInChildren<Transform>(true);
                    initialPositions.Add(parts.Select(t=>t.localPosition).ToArray());initialRotations.Add(parts.Select(t=>t.localRotation).ToArray());initialVisibility.Add(parts.Select(t=>t.gameObject.activeSelf).ToArray());
                    Require(actor.TryAction(actions[i],actor.transform.position,true),"Action rejected for "+actor.scenario+": "+actor.Feedback);
                }
                stage=2;phaseStart=EditorApplication.timeSinceStartup;phaseGame=probe.SimulatedSeconds;return;
            }
            if(stage==2)
            {
                for(int i=0;i<6;i++)
                {
                    var actor=actors[i];Require(actor.transform.position==new Vector3(i*2,0,0)&&actor.transform.rotation==Quaternion.identity,"Presentation moved tracking anchor");
                    var parts=actor.visual.GetComponentsInChildren<Transform>(true);
                    Require(parts.Length==initialPositions[i].Length,"Animation allocated extra transforms");
                    for(int n=0;n<parts.Length;n++)if((parts[n].localPosition-initialPositions[i][n]).sqrMagnitude>1e-8f || Quaternion.Angle(parts[n].localRotation,initialRotations[i][n])>.05f || parts[n].gameObject.activeSelf!=initialVisibility[i][n])changed[i]=true;
                }
                if(sample<sampleTimes.Length && elapsed>=sampleTimes[sample])
                {
                    for(int i=0;i<6;i++)Capture(actors[i],sample);
                    File.AppendAllText(Path.Combine(Output,"sample-times.txt"),sample+" actual_update_elapsed="+elapsed.ToString("F4")+" wall_elapsed="+(EditorApplication.timeSinceStartup-phaseStart).ToString("F4")+"\n");sample++;
                }
                if(elapsed>1.1 && sample==sampleTimes.Length)
                {
                    for(int i=0;i<6;i++)
                    {
                        var actor=actors[i];Require(changed[i],"No temporal pose/visibility change in "+actor.scenario);Require(!actor.Busy,"Transition did not finish in "+actor.scenario);
                        actor.ResetEffects();Require(!actor.Busy,"Reset left motion active");
                        Require(actor.TryAction(actions[i],actor.transform.position,true),"Restart rejected");
                        actor.SetTracked(false);Require(!actor.Busy&&!actor.visual.gameObject.activeSelf,"Tracking loss did not cancel/hide "+actor.scenario);
                        actor.SetTracked(true);actor.ResetEffects();actor.ExitAndDestroy();
                    }
                    stage=3;phaseStart=EditorApplication.timeSinceStartup;phaseGame=probe.SimulatedSeconds;
                }
            }
            else if(stage==3 && elapsed>.4)
            {
                Require(actors.All(a=>!a),"Exit left a live actor");
                Require(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length==0,"Exit leaked actor audio");
                File.WriteAllText(Path.Combine(Output,"result.txt"),"PASS: actual Play Mode entry/action/temporal pose/anchor isolation/reset/tracking loss/recovery/exit; six scenarios, 18 Unity frames. No headset or recognition-accuracy claim.\n");
                Debug.Log("PARK_PLAYMODE_TIMELINE_PASS: six actors, actual time and coroutine updates, 18 frames, lifecycle cleanup");Finish(0);
            }
        }
        catch(Exception error) {Debug.LogException(error);File.WriteAllText(Path.Combine(Output,"result.txt"),"FAIL: "+error);Finish(1);}
    }
    static void Finish(int code) {SessionState.SetInt(Key+".Exit",code);EditorApplication.ExitPlaymode();}
    static void Capture(ParkActor actor,int index)
    {
        var renderers=actor.visual.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
        Require(renderers.Length>0,"Animation rendered an empty actor");var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        camera.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.x*.8f,bounds.extents.z*.9f)*1.5f;
        camera.transform.position=bounds.center+new Vector3(.9f,.55f,-1.6f).normalized*Mathf.Max(bounds.size.magnitude*2,1);camera.transform.LookAt(bounds.center);
        var target=new RenderTexture(640,480,24);var pixels=new Texture2D(640,480,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try {camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,640,480),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(Output,actor.scenario+"-PLAY-"+index+".png"),pixels.EncodeToPNG());}
        finally {camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
    }
}
