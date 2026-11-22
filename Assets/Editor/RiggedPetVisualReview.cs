using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class RiggedPetVisualReview
{
    const string Key="MagicMR.RiggedPetReview";
    static Camera camera;
    static GameObject model;
    static Animator animator;
    static SkinnedMeshRenderer[] skins;
    static Mesh[] samples;
    static Vector3[][] initialVertices;
    static Bounds initialBounds;
    static double started;
    static int sample;
    static float duration;
    static bool moved;
    static bool feeding,feedStateSeen;
    static readonly string Output=Path.GetFullPath(".codex-tmp/bicolor-cat-review");
    static RiggedPetVisualReview() {EditorApplication.update+=Tick;}
    public static void Run()
    {
        BicolorCatAssetBuilder.Run();
        SessionState.SetString(Key+".Scene",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        SessionState.SetInt(Key+".Exit",-1);SessionState.SetBool(Key,true);Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"samples.txt"),"");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.55f,.55f);
        var light=new GameObject("Studio key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(38,-35,0);
        var fill=new GameObject("Studio fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.7f;fill.transform.rotation=Quaternion.Euler(25,145,0);
        camera=new GameObject("Pet review camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.nearClipPlane=.01f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.20f,.22f,.24f);
        camera.gameObject.AddComponent<AudioListener>();
        EditorApplication.EnterPlaymode();
    }
    static void Require(bool ok,string message) {if(!ok)throw new Exception(message);}
    static Bounds BoundsOf() {var rs=model.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying)
        {
            int code=SessionState.GetInt(Key+".Exit",-1);if(code<0)return;SessionState.SetBool(Key,false);
            string previous=SessionState.GetString(Key+".Scene","");if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);
            EditorApplication.Exit(code);return;
        }
        try
        {
            if(!model)
            {
                started=EditorApplication.timeSinceStartup;camera=Camera.main;
                model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BicolorCatAssetBuilder.Folder+"/BicolorCat.prefab"));
                animator=model.GetComponentInChildren<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;
                duration=animator.runtimeAnimatorController.animationClips.Max(c=>c.length);
                Require(duration>1&&duration<20,"Unbounded or empty authored animation");
                skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();foreach(var skin in skins)skin.updateWhenOffscreen=true;
                samples=skins.Select(s=>new Mesh()).ToArray();return;
            }
            double elapsed=EditorApplication.timeSinceStartup-started;
            Require(elapsed<30,"Rigged preview timed out");
            Require(model.transform.position==Vector3.zero&&model.transform.rotation==Quaternion.identity,"Animation moved tracking root");
            if(feeding&&animator.GetCurrentAnimatorStateInfo(0).IsName("Brief sniff"))feedStateSeen=true;
            if(elapsed<.15+sample*duration/4)return;
            if(sample==0)
            {
                initialBounds=BoundsOf();Require(initialBounds.size.y>.25f&&initialBounds.size.y<.5f,"Cat preview scale invalid");
                camera.orthographicSize=Mathf.Max(initialBounds.extents.y,initialBounds.extents.x*.8f,initialBounds.extents.z*.9f)*1.25f;
                camera.transform.position=initialBounds.center+new Vector3(.9f,.55f,-1.6f).normalized*1.5f;camera.transform.LookAt(initialBounds.center);
                initialVertices=new Vector3[skins.Length][];
            }
            var bounds=BoundsOf();Require((bounds.center-initialBounds.center).magnitude<initialBounds.size.y*.7f,"Source animation drifts away from its anchor");
            for(int i=0;i<skins.Length;i++)
            {
                skins[i].BakeMesh(samples[i]);var vertices=samples[i].vertices;
                if(sample==0)initialVertices[i]=vertices;
                else for(int n=0;n<vertices.Length;n++)if((vertices[n]-initialVertices[i][n]).sqrMagnitude>1e-8f) {moved=true;break;}
            }
            Capture(sample);File.AppendAllText(Path.Combine(Output,"samples.txt"),(feeding?"Feed ":"Idle ")+sample+" actual_elapsed="+elapsed.ToString("F4")+" bounds="+bounds+"\n");sample++;
            if(sample>=5)
            {
                Require(moved,"Authored clip did not deform the skinned geometry");
                if(!feeding)
                {
                    var position=camera.transform.position;var rotation=camera.transform.rotation;
                    var angles=new[]{new Vector3(0,.25f,-1.6f),new Vector3(1.6f,.25f,0),new Vector3(-.9f,.5f,1.6f)};
                    for(int i=0;i<angles.Length;i++){camera.transform.position=initialBounds.center+angles[i];camera.transform.LookAt(initialBounds.center);CaptureFile("BicolorCat-View-"+i+".png");}
                    camera.transform.SetPositionAndRotation(position,rotation);
                    model.GetComponent<MagicMR.NaturalPetRig>().Sniff();feeding=true;sample=0;moved=false;started=EditorApplication.timeSinceStartup;return;
                }
                Require(feedStateSeen,"Authored sniff state was never active");
                File.WriteAllText(Path.Combine(Output,"result.txt"),"PASS: five real idle frames and five real sniff/recovery frames, three additional views, original 40-bone rig, CPU skin deformation, root/center drift checked. UV0 normal reprojection; no physical pet or headset acceptance.\n");
                Debug.Log("BICOLOR_CAT_PLAYMODE_REVIEW_PASS: ten real idle/sniff frames and three views, original rig deformation, root isolation");Finish(0);
            }
        }
        catch(Exception error) {Debug.LogException(error);File.WriteAllText(Path.Combine(Output,"result.txt"),"FAIL: "+error);Finish(1);}
    }
    static void Finish(int code)
    {
        if(samples!=null)foreach(var mesh in samples)if(mesh)UnityEngine.Object.DestroyImmediate(mesh);
        SessionState.SetInt(Key+".Exit",code);EditorApplication.ExitPlaymode();
    }
    static void Capture(int index)
    {
        CaptureFile("BicolorCat-"+(feeding?"Feed-":"")+"PLAY-"+index+".png");
    }
    static void CaptureFile(string name)
    {
        var target=new RenderTexture(900,700,24);var pixels=new Texture2D(900,700,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try {camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,900,700),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(Output,name),pixels.EncodeToPNG());}
        finally {camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
    }
}
