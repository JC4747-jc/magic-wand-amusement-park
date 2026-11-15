using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class NaturalInsectPlayChecks
{
    const string Key="MagicMR.InsectPlay.Active";
    static readonly List<ParkActor> actors=new List<ParkActor>();
    static readonly List<NaturalInsectRig> rigs=new List<NaturalInsectRig>();
    static readonly List<Quaternion[]> rest=new List<Quaternion[]>();
    static readonly List<Bounds> frames=new List<Bounds>();
    // Perform first transfers the spell for .3 s, then starts the requested effect.
    static readonly float[] times={.38f,.52f,.75f,1.1f,1.5f};
    static readonly bool[] moved=new bool[2];
    static Camera camera;
    static ParkAnimationFrameProbe probe;
    static double started,phase;
    static float phaseGame;
    static int stage,sample;
    static readonly string Output=Path.GetFullPath(".codex-tmp/natural-insect-play");
    static NaturalInsectPlayChecks(){EditorApplication.update+=Tick;}
    public static void RunAll()
    {
        NaturalInsectAssetBuilder.RenderCandidates();NaturalInsectAssetBuilder.Install();ProjectSceneAudit.Run();ParkNaturalModelChecks.Render();Start(true);
    }
    public static void Run(){Start(false);}
    static void Start(bool nextPark)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Insect check requires an idle editor");
        Directory.CreateDirectory(Output);SessionState.SetString(Key+".Scene",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        SessionState.SetInt(Key+".Exit",-1);SessionState.SetBool(Key+".NextPark",nextPark);SessionState.SetBool(Key,true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var key=new GameObject("Insect key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.4f;key.transform.rotation=Quaternion.Euler(38,-35,0);
        camera=new GameObject("Insect lifecycle camera").AddComponent<Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.24f,.26f,.28f);camera.nearClipPlane=.001f;camera.orthographic=true;
        camera.gameObject.AddComponent<AudioListener>();
        EditorApplication.EnterPlaymode();
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying)
        {
            int exit=SessionState.GetInt(Key+".Exit",-1);if(exit<0)return;SessionState.SetBool(Key,false);
            string previous=SessionState.GetString(Key+".Scene","");if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);
            if(exit==0&&SessionState.GetBool(Key+".NextPark",false))ParkAnimationTimelineChecks.Run();else EditorApplication.Exit(exit);return;
        }
        try
        {
            if(stage==0)
            {
                started=phase=EditorApplication.timeSinceStartup;camera=Camera.main;probe=camera.gameObject.AddComponent<ParkAnimationFrameProbe>();phaseGame=probe.SimulatedSeconds;
                for(int i=0;i<2;i++)
                {
                    var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/ParkBugs.prefab"));
                    var actor=go.GetComponent<ParkActor>();actor.species=i==0?"Cockroach":"Spider";actor.virtualTarget=true;actor.cooldown=0;actor.transitionSeconds=.35f;
                    go.transform.position=new Vector3(i*.8f,0,0);go.SetActive(true);actor.Build();if(actor.label)actor.label.gameObject.SetActive(false);
                    var rig=actor.visual.GetComponentInChildren<NaturalInsectRig>();rig.Initialize();actors.Add(actor);rigs.Add(rig);rest.Add(rig.Skin.bones.Select(b=>b.localRotation).ToArray());
                }
                stage=1;return;
            }
            Require(EditorApplication.timeSinceStartup-started<25,"Insect Play Mode timed out");double elapsed=probe.SimulatedSeconds-phaseGame;
            if(stage==1&&elapsed>.5)
            {
                for(int i=0;i<2;i++)
                {
                    Require(actors[i].EntryProgress>.99f,"Insect entry did not finish");frames.Add(rigs[i].Skin.bounds);
                    Require(actors[i].TryAction(ParkAction.Awaken,actors[i].transform.position,true),"Insect awaken rejected");
                }
                stage=2;phase=EditorApplication.timeSinceStartup;phaseGame=probe.SimulatedSeconds;return;
            }
            if(stage==2)
            {
                for(int i=0;i<2;i++)
                {
                    var actor=actors[i];var rig=rigs[i];Require(actor.transform.position==new Vector3(i*.8f,0,0)&&actor.transform.rotation==Quaternion.identity,"Insect animation moved tracking anchor");
                    for(int leg=0;leg<rig.LegCount;leg++)Require(Vector3.Distance(rig.FootLocal(leg),rig.FootTarget(leg))<.0008f,"Live insect IK missed its target");
                    var bones=rig.Skin.bones;for(int n=0;n<bones.Length;n++)if(bones[n].name.StartsWith("Leg_")&&Quaternion.Angle(bones[n].localRotation,rest[i][n])>.1f)moved[i]=true;
                }
                if(sample<times.Length&&elapsed>=times[sample])
                {
                    for(int i=0;i<2;i++)Capture(i,"Gait-"+sample);
                    File.AppendAllText(Path.Combine(Output,"samples.txt"),"gait "+sample+" actual_update_elapsed="+elapsed.ToString("F4")+" wall_elapsed="+(EditorApplication.timeSinceStartup-phase).ToString("F4")+" frames="+probe.Frames+"\n");sample++;
                }
                if(elapsed>1.8&&sample==times.Length)
                {
                    for(int i=0;i<2;i++){Require(moved[i]&&!actors[i].Busy,"Insect gait/awaken did not advance");Require(actors[i].TryAction(ParkAction.Reward,actors[i].transform.position,true),"Insect reward rejected");}
                    stage=3;phase=EditorApplication.timeSinceStartup;phaseGame=probe.SimulatedSeconds;sample=0;
                }
            }
            else if(stage==3)
            {
                if(sample==0&&elapsed>.42){for(int i=0;i<2;i++)Capture(i,"Impact");sample++;}
                if(elapsed>.3f+Mathf.Max(.3f,actors[0].transitionSeconds)+.15f)
                {
                    for(int i=0;i<2;i++)
                    {
                        var actor=actors[i];var rig=rigs[i];
                        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                        Require(actor.VirtualScore==100&&!rig.Skin.enabled&&rig.transform.localScale.sqrMagnitude<1e-8f,"Skinned insect did not disappear after defeat: "+actor.species+" score="+actor.VirtualScore+" enabled="+rig.Skin.enabled+" scale="+rig.transform.localScale+" busy="+actor.Busy+" progress="+typeof(ParkActor).GetField("bugDefeatProgress",flags).GetValue(actor)+" tracked="+actor.tracked+" active="+actor.isActiveAndEnabled+" stopping="+(typeof(ParkActor).GetField("stoppingEffects",flags).GetValue(actor)!=null)+" elapsed="+elapsed+" dt="+Time.unscaledDeltaTime);
                        Require(!actor.TryAction(ParkAction.Reward,actor.transform.position,true),"Duplicate defeat awarded another round");
                        actor.Undo();Require(actor.VirtualScore==0&&rig.Skin.enabled&&(rig.transform.localScale-Vector3.one).sqrMagnitude<1e-8f,"Undo did not immediately restore skinned insect");Capture(i,"Undo");
                        actor.ResetEffects();var bones=rig.Skin.bones;for(int n=0;n<bones.Length;n++)Require(Quaternion.Angle(bones[n].localRotation,rest[i][n])<.01f,"Reset retained an insect joint pose");
                        Require(actor.TryAction(ParkAction.Awaken,actor.transform.position,true),"Insect restart rejected");actor.SetTracked(false);Require(!actor.Busy&&!actor.visual.gameObject.activeSelf,"Tracking loss retained live insect effect");
                        actor.SetTracked(true);actor.ResetEffects();actor.ExitAndDestroy();
                    }
                    stage=4;phase=EditorApplication.timeSinceStartup;phaseGame=probe.SimulatedSeconds;
                }
            }
            else if(stage==4&&elapsed>.4)
            {
                Require(actors.All(a=>!a),"Insect exit left live actors");Require(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length==0,"Insect exit leaked audio");
                File.WriteAllText(Path.Combine(Output,"result.txt"),"PASS: two species, actual Play Mode entry/gait/IK/action/defeat/duplicate rejection/immediate Undo/Reset/tracking loss/recovery/exit; 14 rendered frames. No physical recognition or headset claim.\n");
                Debug.Log("NATURAL_INSECT_PLAYMODE_PASS: two species, 14 actual frames; entry/gait/defeat/Undo/Reset/loss/recovery/exit and audio cleanup");Finish(0);
            }
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Path.Combine(Output,"result.txt"),"FAIL: "+error);Finish(1);}
    }
    static void Finish(int code){SessionState.SetInt(Key+".Exit",code);EditorApplication.ExitPlaymode();}
    static void Capture(int index,string sample)
    {
        var bounds=frames[index];bounds.Encapsulate(actors[index].transform.position+Vector3.up*.32f);
        camera.transform.position=bounds.center+new Vector3(.23f,.35f,-.32f);camera.transform.LookAt(bounds.center);camera.orthographicSize=Mathf.Max(bounds.extents.x,bounds.extents.y,bounds.extents.z)*1.8f;
        var target=new RenderTexture(640,640,24);var pixels=new Texture2D(640,640,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,640,640),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(Output,actors[index].species+"-"+sample+".png"),pixels.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
    }
}
