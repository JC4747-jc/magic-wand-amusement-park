using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class LegacySceneAnimationChecks
{
    const string Key="MagicMR.LegacyAnimation";
    const int ReviewLayer=30;
    static readonly string[] Names={"BridgeTest","BridgeStereoFusion","BridgeStereoPoseFusion","BridgeStereoRepair","MagicMR","SampleScene","DesktopCaptureDemo","VstTest"};
    static readonly string Output=Path.GetFullPath(".codex-tmp/legacy-animation");
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> isolated=new List<string>();
    static RealityEditor actor;
    static CaptureTo2D capture;
    static Camera camera;
    static ParkAnimationFrameProbe probe;
    static Renderer[] body;
    static Material[] original;
    static Vector3 basePosition,baseScale;
    static Vector3 backpackPosition,cardStartPosition;
    static Quaternion baseRotation;
    static Transform parent;
    static Matrix4x4 parentPose;
    static float phase,deconstructionDelay;
    static double started;
    static int stage,captures,warmFrame;
    static bool pulseMoved,dodgeMoved,cardMoved;
    static long bytes;
    [Serializable] sealed class Result {public string scene,scope;public string[] isolatedComponents;public int frames;public bool pass;}
    static LegacySceneAnimationChecks(){EditorApplication.update+=Tick;}
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static T Field<T>(string name)=>(T)typeof(RealityEditor).GetField(name,Private).GetValue(actor);
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Legacy animation checks require idle editor");
        Directory.CreateDirectory(Output);SessionState.SetString(Key+".Previous",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        SessionState.SetInt(Key+".Index",0);SessionState.SetString(Key+".Phase","open");SessionState.SetBool(Key,true);
    }
    public static void UpgradeAndRunAll(){LegacySceneMaintenance.Run();SessionState.SetBool(Key+".NextStartup",true);Run();}
    public static void RunCaptureAndVstThenStartup(){Run();SessionState.SetInt(Key+".Index",5);SessionState.SetBool(Key+".NextStartup",true);}
    static void Next(){stage++;phase=probe.SimulatedSeconds;}
    static bool Stable=>probe.Frames>=5&&Time.unscaledDeltaTime<.05f;
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        try
        {
            int index=SessionState.GetInt(Key+".Index",0);string name=index<Names.Length?Names[index]:"";
            if(!EditorApplication.isPlaying)
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                if(SessionState.GetString(Key+".Phase","")=="next")
                {
                    index++;SessionState.SetInt(Key+".Index",index);SessionState.SetString(Key+".Phase","open");
                    if(index>=Names.Length){Finish();return;}name=Names[index];
                }
                if(SessionState.GetString(Key+".Phase","")!="open")return;
                EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");Debug.Log("LEGACY_ANIMATION_BEGIN "+name);
                SessionState.SetString(Key+".Phase","entering");EditorApplication.EnterPlaymode();return;
            }
            if(SessionState.GetString(Key+".Phase","")=="next")return;
            if(stage==0)
            {
                Time.timeScale=1;started=EditorApplication.timeSinceStartup;probe=new GameObject("Legacy frame probe").AddComponent<ParkAnimationFrameProbe>();
                phase=probe.SimulatedSeconds;stage=1;return;
            }
            Require(EditorApplication.timeSinceStartup-started<35,"Legacy animation timed out: "+name);
            float elapsed=probe.SimulatedSeconds-phase;
            if(index<5)CheckLighter(name,elapsed);else if(index<7)CheckCapture(name,elapsed);else CheckVst(name,elapsed);
        }
        catch(Exception e){Time.timeScale=1;SessionState.SetBool(Key,false);Debug.LogException(e);File.WriteAllText(Path.Combine(Output,"failure.txt"),e.ToString());EditorApplication.Exit(1);}
    }
    static void IsolateInputs()
    {
        foreach(var b in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(!b||b==actor||!b.enabled)continue;var type=b.GetType();
            if(type.Namespace=="Perception"||b is PicoStereoCapture||b is GestureManager||b is HandGestureDetectorBase||b is LighterAnchorManager||b is MRGestureController||b is CalibrationRitual||b is PicoHandPoseGestureBridge)
            {isolated.Add(type.FullName);b.enabled=false;}
        }
    }
    static Bounds BoundsOf(GameObject model)
    {
        var renderers=model.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&(r is MeshRenderer||r is SkinnedMeshRenderer)).ToArray();
        Require(renderers.Length>0,"No visible model geometry");var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;
    }
    static void LayerModel(GameObject model)
    {
        foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=ReviewLayer;
        // The live stereo owner intentionally masks an unsummoned target.
        // It is disabled for this explicit animation-only review; opening its
        // rendering mask here never changes the production recognition gate.
        foreach(var r in model.GetComponentsInChildren<Renderer>(true))r.forceRenderingOff=false;
    }
    static void Focus(GameObject model,bool dodge=false)
    {
        LayerModel(model);var bounds=BoundsOf(model);var center=dodge?basePosition+Vector3.right*.08f:bounds.center;
        camera.transform.position=center+new Vector3(.22f,.10f,-.4f);camera.transform.LookAt(center);
        camera.orthographicSize=dodge?.23f:Mathf.Max(.055f,bounds.extents.y*1.45f,bounds.extents.x*1.2f,bounds.extents.z*1.2f);
    }
    static void ResetActor()
    {
        actor.ResetTarget();Require(!actor.IsDeconstructed&&!actor.InteriorVisible&&actor.ActiveDimension==EditDimension.None,"Legacy Reset retained state");
        Require(Vector3.Distance(actor.transform.position,basePosition)<.0001f&&(actor.transform.localScale-baseScale).sqrMagnitude<1e-10f,"Legacy Reset changed base pose/size");
        for(int i=0;i<body.Length;i++)Require(body[i].enabled&&body[i].sharedMaterial==original[i],"Legacy Reset lost material/renderer: "+body[i].name);
        var audio=actor.GetComponent<AudioSource>();Require(!audio||!audio.isPlaying,"Legacy Reset retained action audio");
        Require(!Field<GameObject>("m_ActiveDeconstructionVfx"),"Legacy Reset retained deconstruction VFX ownership");
    }
    static void CheckLighter(string name,float elapsed)
    {
        if(actor&&parent)Require(actor.transform.parent==parent&&parent.localToWorldMatrix==parentPose,"Model action moved its existing parent anchor");
        if(stage==1&&elapsed>.8f&&Stable)
        {
            actor=UnityEngine.Object.FindObjectsByType<RealityEditor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(a=>a.name=="Lighter");
            IsolateInputs();isolated.Add("Unsummoned renderer mask opened only for this isolated model review");actor.gameObject.SetActive(true);actor.enabled=true;actor.ResetTarget();actor.EnsureVisible();
            basePosition=actor.transform.position;baseRotation=actor.transform.rotation;baseScale=actor.transform.localScale;parent=actor.transform.parent;if(parent)parentPose=parent.localToWorldMatrix;
            body=Field<Renderer[]>("m_AllRenderers");original=body.Select(r=>r.sharedMaterial).ToArray();Require(body.Length>=10&&body.All(r=>r&&r.sharedMaterial),"Legacy scene lacks detailed model/materials");
            deconstructionDelay=Field<float>("m_DeconstructionDelay");Require(deconstructionDelay>=0&&deconstructionDelay<=5,"Unbounded deconstruction delay");
            camera=new GameObject("Actual model review camera - input isolated").AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.cullingMask=1<<ReviewLayer;camera.nearClipPlane=.001f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.20f,.22f);
            var key=new GameObject("Review key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.4f;key.transform.rotation=Quaternion.Euler(35,-30,0);key.cullingMask=1<<ReviewLayer;
            Focus(actor.gameObject);Snapshot(name,"Rest");warmFrame=probe.Frames;Next();return;
        }
        if(stage==2&&Stable&&probe.Frames>warmFrame+5){actor.TriggerAppearance();Next();}
        else if(stage==3&&elapsed>.2f)
        {
            Require(actor.ActiveDimension==EditDimension.Appearance&&body.Where((r,i)=>r.sharedMaterial!=original[i]).Any(),"Appearance did not change actual scene materials");
            Snapshot(name,"Appearance");ResetActor();actor.TriggerAgency();Next();
        }
        else if(stage==4)
        {
            if((actor.transform.localScale-baseScale).sqrMagnitude>1e-8f)pulseMoved=true;
            if(elapsed>.65f&&Stable){Require(pulseMoved&&Field<GameObject>("m_EyesObject").activeSelf,"Agency did not animate actual scene model/eyes");Snapshot(name,"Agency");ResetActor();Focus(actor.gameObject,true);actor.TriggerRule();Next();}
        }
        else if(stage==5)
        {
            if(Vector3.Distance(actor.transform.position,basePosition)>.01f)dodgeMoved=true;
            if(elapsed>.12f&&captures==3)Snapshot(name,"Dodge");
            if(elapsed>.8f&&Stable)
            {
                Require(dodgeMoved&&actor.EvadeCount==1&&Vector3.Distance(actor.transform.position,basePosition)<.001f,"Rule dodge did not return to logical base");ResetActor();
                for(int i=0;i<12&&!actor.InteriorVisible;i++)actor.TriggerScale();
                Require(actor.InteriorVisible&&Mathf.Abs(actor.transform.localScale.y/baseScale.y-StudySpec.ScaleMaxMultiplier)<.001f,"Scale cap/interior did not open");Next();
            }
        }
        else if(stage==6&&elapsed>.25f&&Stable){Focus(actor.gameObject);Snapshot(name,"Interior");ResetActor();actor.TriggerDeconstruction();Next();}
        else if(stage==7&&elapsed>deconstructionDelay+1)
        {
            var flower=Field<GameObject>("m_SpawnedFlower");Require(actor.IsDeconstructed&&flower&&body.All(r=>!r.enabled),"Deconstruction did not replace body with flower");
            var support=Field<Vector3>("m_FlowerSupportPoint");Require(Mathf.Abs(BoundsOf(flower).min.y-support.y)<.003f,"Actual scene flower left its support point");
            Focus(flower);Snapshot(name,"Flower");ResetActor();Next();
        }
        else if(stage==8&&elapsed>.2f&&Stable){actor.TriggerDeconstruction();actor.ResetTarget();Next();}
        else if(stage==9&&elapsed>deconstructionDelay+.5f)
        {
            Require(!actor.IsDeconstructed&&!Field<GameObject>("m_SpawnedFlower")&&UnityEngine.Object.FindObjectsByType<MagicMRBloomReveal>(FindObjectsSortMode.None).Length==0,"Cancelled deconstruction spawned/leaked a late flower");
            actor.TriggerDeconstruction();actor.ApplyDimension(EditDimension.Appearance,Vector3.zero,false);
            Require(!actor.IsDeconstructed&&!Field<GameObject>("m_ActiveDeconstructionVfx"),"Dimension switch retained pending deconstruction/VFX");ResetActor();Next();
        }
        else if(stage==10&&elapsed>deconstructionDelay+.5f)
        {
            Require(!Field<GameObject>("m_SpawnedFlower")&&UnityEngine.Object.FindObjectsByType<MagicMRBloomReveal>(FindObjectsSortMode.None).Length==0,"Dimension switch left a late flower");
            ResetActor();Require(actor.transform.rotation==baseRotation,"Reset changed legacy rotation");Focus(actor.gameObject);Snapshot(name,"Reset");Complete(name,"Actual saved scene target and all core actions; spatial/input components temporarily isolated for model review. No synthetic recognition or real hand/anchor acceptance.");
        }
    }
    static void CheckCapture(string name,float elapsed)
    {
        if(stage==1&&elapsed>.8f&&Stable)
        {
            capture=UnityEngine.Object.FindFirstObjectByType<CaptureTo2D>();Require(capture&&capture.target3DObject&&capture.card2DUI&&capture.backpackIcon&&capture.captureCamera,"Capture scene bindings missing");
            Require(!capture.captureCamera.enabled,"Dedicated snapshot camera renders continuously");
            Require(capture.backpackIcon.gameObject.activeInHierarchy,"Resting capture hid the backpack/shared canvas");backpackPosition=capture.backpackIcon.position;
            Time.timeScale=0;Require(capture.TryCapture()&&!capture.TryCapture()&&capture.IsAnimating&&capture.IsCaptured&&!capture.target3DObject.activeSelf,"Capture/duplicate guard failed");
            CaptureCard(name);Next();
        }
        else if(stage==2&&elapsed>.15f){capture.ResetCapture();Require(capture.target3DObject.activeSelf&&!capture.IsCaptured&&!capture.IsAnimating,"Mid-flight capture Reset failed");Require(capture.TryCapture(),"Capture could not restart");cardStartPosition=capture.card2DUI.transform.position;Next();}
        else if(stage==3)
        {
            Require(capture.backpackIcon.gameObject.activeInHierarchy&&Vector3.Distance(capture.backpackIcon.position,backpackPosition)<.1f,"Card animation moved/disabled the backpack/shared canvas");
            if(elapsed>.7f&&elapsed<1.5f&&Vector3.Distance(capture.card2DUI.transform.position,cardStartPosition)>.1f)cardMoved=true;
            if(elapsed<=2.2f)return;
            Require(cardMoved&&capture.IsCaptured&&!capture.IsAnimating&&!capture.card2DUI.gameObject.activeInHierarchy,"Capture did not move/finish while timeScale=0");
            capture.enabled=false;Require(capture.target3DObject.activeSelf&&!capture.IsCaptured,"Disabled capture left source hidden");capture.enabled=true;Time.timeScale=1;
            Complete(name,"Actual scene capture bindings, nonblank card snapshot, duplicate guard, mid-flight reset/reuse, completion while timeScale=0 and disable cleanup. SampleScene AR/provider success remains device-only.");
        }
    }
    static void CheckVst(string name,float elapsed)
    {
        if(stage!=1||elapsed<1||!Stable)return;
        var vst=UnityEngine.Object.FindFirstObjectByType<VstTestRunner>();var main=Camera.main;
        Require(vst&&main&&main.clearFlags==CameraClearFlags.SolidColor&&main.backgroundColor.a==0,"VST runner/camera alpha not configured");
        var data=main.GetComponent<UniversalAdditionalCameraData>();Require(data&&!data.renderPostProcessing,"VST post processing still opaque");
        Require(UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).All(v=>!v.isActiveAndEnabled),"VST left opaque volume active");
        Complete(name,"Actual VstTest guarded runner, active XR camera alpha=0 and post-processing/volume isolation verified in Editor. No passthrough, device image or native toggle acceptance.");
    }
    static void CaptureCard(string name)
    {
        var target=capture.captureCamera.targetTexture;Require(target&&target.IsCreated()&&target.width*target.height<=2048*2048,"Invalid/unbounded card render texture");
        var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();SavePixels(name,"Card",pixels);}
        finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);}
    }
    static void Snapshot(string scene,string label)
    {
        LayerModel(actor.gameObject);var flower=Field<GameObject>("m_SpawnedFlower");if(flower)LayerModel(flower);
        var target=new RenderTexture(800,700,24);var pixels=new Texture2D(800,700,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{camera.targetTexture=target;camera.Render();camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,800,700),0,0);pixels.Apply();SavePixels(scene,label,pixels);}
        finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
    }
    static void SavePixels(string scene,string label,Texture2D pixels)
    {
        var colors=pixels.GetPixels32();var background=colors[0];Require(colors.Count(c=>Math.Abs(c.r-background.r)+Math.Abs(c.g-background.g)+Math.Abs(c.b-background.b)>24)>100,"Blank legacy animation/card image: "+scene+" "+label);
        var png=pixels.EncodeToPNG();bytes+=png.Length;Require(bytes<16*1024*1024,"Per-scene legacy image budget exceeded");File.WriteAllBytes(Path.Combine(Output,scene+"-"+label+".png"),png);captures++;
    }
    static void Complete(string name,string scope)
    {
        Time.timeScale=1;File.WriteAllText(Path.Combine(Output,name+".json"),JsonUtility.ToJson(new Result{scene=name,scope=scope,isolatedComponents=isolated.ToArray(),frames=captures,pass=true},true));
        Debug.Log("LEGACY_SCENE_ANIMATION_PASS "+name+" frames="+captures+" scope="+scope);SessionState.SetString(Key+".Phase","next");EditorApplication.ExitPlaymode();
    }
    static void Finish()
    {
        foreach(var name in Names)Require(File.Exists(Path.Combine(Output,name+".json"))&&JsonUtility.FromJson<Result>(File.ReadAllText(Path.Combine(Output,name+".json"))).pass,"Missing scene animation result: "+name);
        SessionState.SetBool(Key,false);string previous=SessionState.GetString(Key+".Previous","");if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);
        File.WriteAllText(Path.Combine(Output,"result.txt"),"PASS: five actual scene lighter models/core actions/reset/cancellation, two actual capture scenes, and VST camera configuration. Model reviews explicitly isolated spatial/input owners; raw startup evidence is separate. No real detection, head/hand/AR/passthrough/device performance acceptance.\n");
        Debug.Log("LEGACY_ALL_SCENE_ANIMATION_PASS: five lighter scenes, two capture scenes, VST configuration; separate raw-startup and hardware boundaries");
        if(SessionState.GetBool(Key+".NextStartup",false))LegacySceneStartupProbe.Run();else EditorApplication.Exit(0);
    }
}
