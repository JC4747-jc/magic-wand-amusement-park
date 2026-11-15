using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MagicMR;

[InitializeOnLoad]
public static class NaturalDogVisualReview
{
    const string Key="MagicMR.DogReview";
    static readonly string Output=Path.GetFullPath(".codex-tmp/natural-dog-unity-review");
    static readonly string[] States={"Restrained neutral idle","Brief sniff","Walk in place"};
    static readonly float[] Durations={20,2.5f,1.083334f};
    static GameObject model;static Camera camera;static Animator animator;static SkinnedMeshRenderer skin;
    static int phase,sample;static float started;static double wallStarted;static Vector3[] initial;static int[] feet;static float motion,footError;
    static NaturalDogVisualReview(){EditorApplication.update+=Tick;}
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        NaturalDogAssetBuilder.Run();SessionState.SetString(Key+".Scene",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"samples.txt"),"");SessionState.SetInt(Key+".Exit",-1);SessionState.SetBool(Key,true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.55f,.55f);
        foreach(var spec in new[]{new Vector3(38,-35,1.5f),new Vector3(25,145,.7f)}){var light=new GameObject("Studio light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=spec.z;light.transform.rotation=Quaternion.Euler(spec.x,spec.y,0);}
        camera=new GameObject("Dog review camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.nearClipPlane=.01f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.2f,.22f,.24f);camera.gameObject.AddComponent<AudioListener>();
        EditorApplication.EnterPlaymode();
    }
    static void BeginPhase()
    {
        animator.Play(States[phase],0,0);animator.Update(0);started=Time.time;sample=0;initial=null;motion=0;footError=0;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying)
        {
            int exit=SessionState.GetInt(Key+".Exit",-1);if(exit<0)return;SessionState.SetBool(Key,false);
            string previous=SessionState.GetString(Key+".Scene","");if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);EditorApplication.Exit(exit);return;
        }
        try
        {
            if(!model)
            {
                wallStarted=EditorApplication.timeSinceStartup;camera=Camera.main;
                model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NaturalDogAssetBuilder.Folder+"/Wolfhound.prefab"));
                animator=model.GetComponent<NaturalPetRig>().Animator;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;skin=model.GetComponentInChildren<SkinnedMeshRenderer>();skin.updateWhenOffscreen=true;
                var bounds=NaturalDogAssetBuilder.Geometry(model);Require(Mathf.Abs(bounds.size.y-.35f)<.002f,"Dog normalization invalid");
                camera.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.z*.85f)*1.35f;camera.transform.position=bounds.center+new Vector3(.9f,.5f,-1.6f);camera.transform.LookAt(bounds.center);BeginPhase();return;
            }
            Require(EditorApplication.timeSinceStartup-wallStarted<90,"Dog review timeout");
            Require(model.transform.position==Vector3.zero&&model.transform.rotation==Quaternion.identity,"Dog animation moved tracking root");
            // Feed samples finish before its exit transition; recovery is checked separately.
            float span=phase==1?2.0f:Durations[phase];float elapsed=Time.time-started;
            if(phase==1&&sample>=5)
            {
                if(elapsed<3.1f)return;
                Require(animator.GetCurrentAnimatorStateInfo(0).IsName(States[0]),"Sniff did not automatically recover to idle");phase++;BeginPhase();return;
            }
            if(elapsed<.12f+sample*span/4)return;
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName(States[phase]),"Expected active dog state "+States[phase]);
            var points=NaturalDogAssetBuilder.Vertices(skin);var boundsNow=NaturalDogAssetBuilder.Geometry(model);
            Require(model.GetComponent<NaturalPetRig>().Head.position.y>boundsNow.center.y,"Dog anatomical up direction inverted");
            if(initial==null){initial=points;feet=Enumerable.Range(0,points.Length).Where(i=>points[i].y<boundsNow.min.y+boundsNow.size.y*.04f).ToArray();Require(feet.Length>=8,"Dog foot sample empty");}
            else for(int i=0;i<points.Length;i++)motion=Mathf.Max(motion,Vector3.Distance(points[i],initial[i]));
            if(phase==1)foreach(int i in feet)footError=Mathf.Max(footError,Vector3.Distance(points[i],initial[i]));
            Capture("phase-"+phase+"-"+sample+".png");File.AppendAllText(Path.Combine(Output,"samples.txt"),States[phase]+" sample="+sample+" simulated_seconds="+elapsed+" max_deformation="+motion+" foot_error="+footError+" geometry="+boundsNow+"\n");sample++;
            if(sample<5)return;
            Require(motion>.00005f,"Dog clip did not deform geometry "+States[phase]);
            if(phase==0)
            {
                var position=camera.transform.position;var rotation=camera.transform.rotation;
                foreach(int i in Enumerable.Range(0,3)){var offsets=new[]{new Vector3(0,.2f,-1.6f),new Vector3(1.6f,.2f,0),new Vector3(-.9f,.4f,1.6f)};camera.transform.position=boundsNow.center+offsets[i];camera.transform.LookAt(boundsNow.center);Capture("view-"+i+".png");}
                camera.transform.SetPositionAndRotation(position,rotation);
            }
            if(phase==1){Require(footError<.001f,"Sniff displaced grounded foot geometry "+footError);return;}
            phase++;if(phase<3){BeginPhase();return;}
            model.GetComponent<NaturalPetRig>().ResetPose();Require(animator.GetCurrentAnimatorStateInfo(0).IsName(States[0]),"Dog reset did not return to idle");
            File.WriteAllText(Path.Combine(Output,"result.txt"),"PASS: actual Play Mode original idle, authored sniff, source walk in place; 15 motion samples plus 3 views; independent world-space skin deformation, grounded sniff and fixed tracking root; explicit Reset returns idle. Source walk is in place, not locomotion or hardware acceptance.\n");
            Debug.Log("NATURAL_DOG_PLAYMODE_REVIEW_PASS: 18 frames; original idle/source walk/authored sniff; feet and tracking root isolated");Finish(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Path.Combine(Output,"result.txt"),"FAIL: "+error);Finish(1);}
    }
    static void Finish(int code){SessionState.SetInt(Key+".Exit",code);EditorApplication.ExitPlaymode();}
    static void Capture(string name)
    {
        var target=new RenderTexture(900,700,24);var pixels=new Texture2D(900,700,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,900,700),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(Output,name),pixels.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
    }
}
