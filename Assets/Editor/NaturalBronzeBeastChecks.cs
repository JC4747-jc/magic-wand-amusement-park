using System;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class NaturalBronzeBeastChecks
{
    const string Evidence=".codex-tmp/bronze-fullbody-validation";
    public static void RunCandidate()
    {
        Require(Application.isBatchMode,"Use an isolated bounded batch editor, not the user's open scene");
        Directory.CreateDirectory(Evidence);
        Run();Render();
        File.WriteAllText(Path.Combine(Evidence,"result.txt"),"PASS bronze full-body native geometry/placement and four studio frames\nNot full B playback, real YOLO, visual approval, Android or PICO acceptance\n");
        EditorApplication.Exit(0);
    }
    static void Require(bool valid,string message){if(!valid)throw new Exception(message);}
    static Vector3[] Geometry(SkinnedMeshRenderer skin)
    {
        var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;
        var matrices=skin.bones.Select((b,i)=>b.localToWorldMatrix*mesh.bindposes[i]).ToArray();
        for(int i=0;i<vertices.Length;i++){
            var w=weights[i];var p=matrices[w.boneIndex0].MultiplyPoint3x4(vertices[i])*w.weight0;
            if(w.weight1>0)p+=matrices[w.boneIndex1].MultiplyPoint3x4(vertices[i])*w.weight1;
            Require(float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z),"Nonfinite bronze vertex");vertices[i]=p;
        }
        return vertices;
    }
    public static void BuildAndRun(){Run();Render();}
    public static void Run()
    {
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/ParkStatue.prefab"));
        try {
            var actor=go.GetComponent<ParkActor>();actor.species="BronzeBeast";actor.virtualTarget=true;go.SetActive(true);actor.Build();
            var rig=go.GetComponentInChildren<FullBodyBronzeBeast>();Require(rig,"BronzeBeast missing authored full body");rig.Initialize();
            var skin=rig.GetComponentsInChildren<SkinnedMeshRenderer>().Single();var mesh=skin.sharedMesh;
            Require(mesh.triangles.Length/3<=22000&&mesh.vertexCount<=12000&&skin.bones.Length==18&&rig.FootCount==4,"Bronze full-body geometry/rig budget changed");
            Require(skin.sharedMaterials.Length==4&&skin.sharedMaterials.All(m=>m&&m.name.StartsWith("Original bronze finish")),"Bronze authored finishes missing");
            Require(Mathf.Abs(mesh.bounds.size.y*rig.transform.localScale.y-.48f)<.0001f,"Bronze nominal height changed");
            go.transform.position=new Vector3(.6f,.05f,-.4f);Require(actor.ConfirmOrientation(37),"Bronze yaw confirmation failed");
            var bottom=go.transform.position+Vector3.up*.015f;Require(actor.FitRepresentation(bottom,bottom+Vector3.up*.35f,true),"Bronze manual fit rejected");
            var rootPosition=go.transform.position;var rootRotation=go.transform.rotation;var fit=actor.visual.parent;var fitPosition=fit.localPosition;var fitScale=fit.localScale;
            rig.ResetPose();var rest=Geometry(skin);float error=0,motion=0,footError=0;Vector3[] last=null;
            var feet=Enumerable.Range(0,4).Select(rig.FootPosition).ToArray();
            for(int i=0;i<=128;i++){
                rig.Sample(i/128f);var points=Geometry(skin);error=Mathf.Max(error,Mathf.Abs(points.Min(p=>p.y)-bottom.y));
                for(int foot=0;foot<4;foot++)footError=Mathf.Max(footError,Vector3.Distance(feet[foot],rig.FootPosition(foot)));
                for(int j=0;j<points.Length;j++)motion=Mathf.Max(motion,Vector3.Distance(rest[j],points[j]));
                Require(go.transform.position==rootPosition&&go.transform.rotation==rootRotation&&fit.localPosition==fitPosition&&fit.localScale==fitScale,"Bronze animation moved placement root");last=points;
            }
            float seam=rest.Zip(last,(a,b)=>Vector3.Distance(a,b)).Max();Require(error<.0001f&&footError<.0001f&&motion>.01f&&seam<.00001f,"Bronze planted/full-cycle failure: bottom="+error+" feet="+footError+" motion="+motion+" seam="+seam);
            var local=mesh.vertices;float low=local.Min(v=>v.y),high=local.Max(v=>v.y);Require(high>low,"Bronze local geometry invalid");
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<512;i++)rig.Sample(i/511f);long allocated=GC.GetAllocatedBytesForCurrentThread()-before;Require(allocated==0,"Bronze per-frame allocation: "+allocated);
            rig.Sample(.5f);actor.Undo();Require(rig.Progress==0,"Bronze Undo did not immediately reset procedural pose");rig.Sample(.5f);actor.SetTracked(false);Require(rig.Progress==0,"Bronze tracking loss retained pose");actor.SetTracked(true);
            rig.Sample(float.NaN);Require(rig.Progress==0,"Invalid bronze phase retained pose");
            Debug.Log("NATURAL_BRONZE_FULL_BODY_PASS: original fictional 18-bone/four-foot skin; 129 independent LBS samples at 35cm fit/37deg yaw; bottom_error_m="+error+" foot_error_m="+footError+" motion_m="+motion+" loop_seam_m="+seam+"; 512 samples zero allocation; Undo/loss/invalid-time reset; NOT gesture/PICO acceptance");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    public static void Render()=>RenderSequence(false);
    public static void RenderMotion()=>RenderSequence(true);
    static void RenderSequence(bool motion)
    {
        Require(Application.isBatchMode,"Studio rendering requires an isolated bounded batch editor");
        var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;var output=Path.GetFullPath(Path.Combine(Evidence,motion?"motion":"frames"));
        if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new Exception("Preserve prior bronze frames before a reviewed new run");
        Directory.CreateDirectory(output);long bytes=0;int frame=0;
        bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        try {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.38f,.4f,.43f);
            foreach(var entry in new[]{("Warm key",new Vector3(35,-35,0),2.3f), ("Cool fill",new Vector3(25,125,0),1.2f)}){
                var light=new GameObject(entry.Item1).AddComponent<Light>();light.type=LightType.Directional;light.intensity=entry.Item3;light.transform.rotation=Quaternion.Euler(entry.Item2);
            }
            var rig=FullBodyBronzeBeast.Create(null);var go=rig.gameObject;
            var skin=go.GetComponentInChildren<SkinnedMeshRenderer>();var native=new Mesh();skin.BakeMesh(native);Debug.Log("BRONZE_OFFLINE_NATIVE_BAKE_DIAGNOSTIC vertices="+native.vertexCount+" local_bounds="+native.bounds);UnityEngine.Object.DestroyImmediate(native);
            var snapshot=new NaturalPetMotionFrames.Snapshot(go.transform,skin);snapshot.Sample();
            var camera=new GameObject("Bronze studio review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.32f;camera.nearClipPlane=.01f;camera.farClipPlane=10;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.18f,.20f);
            var bounds=snapshot.Renderer.bounds;camera.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.x,bounds.extents.z)*1.45f;
            camera.transform.position=bounds.center+new Vector3(.47f,.10f,-.85f);camera.transform.LookAt(bounds.center);
            foreach(var phase in motion?Enumerable.Range(0,96).Select(i=>i/95f):new[]{0f,.45f,.75f,1f}){
                rig.Sample(phase);
                snapshot.Sample();
                var target=new RenderTexture(640,640,24);var pixels=new Texture2D(640,640,TextureFormat.RGB24,false);var old=RenderTexture.active;
                try{camera.targetTexture=target;camera.Render();camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,640,640),0,0);pixels.Apply();var colors=pixels.GetRawTextureData<byte>();int visible=0;for(int i=0;i<colors.Length;i+=3)if(Mathf.Abs(colors[i]-colors[0])+Mathf.Abs(colors[i+1]-colors[1])+Mathf.Abs(colors[i+2]-colors[2])>14)visible++;Require(visible>3000,"Bronze review rendered an empty image: "+visible);var png=pixels.EncodeToPNG();bytes+=png.Length;Require(bytes<=24*1024*1024,"Bronze motion frames exceeded 24MiB");File.WriteAllBytes(Path.Combine(output,motion?frame.ToString("D3")+".png":"Bronze-"+phase+".png"),png);if(!motion||frame==0||frame==95)Debug.Log("BRONZE_OFFLINE_POSE_RENDER phase="+phase+" changed_pixels="+visible+" bounds="+snapshot.Renderer.bounds);frame++;}
                finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
            }
            UnityEngine.Object.DestroyImmediate(snapshot.Mesh);
            if(motion)Debug.Log("BRONZE_OFFLINE_MOTION_PASS: "+frame+" frames/24fps/640x640/4s; "+bytes+" bytes; explicit bone-matrix pose samples, separate actual Play Mode lifecycle checks");
        }
        finally{ShaderUtil.allowAsyncCompilation=async;if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
}
