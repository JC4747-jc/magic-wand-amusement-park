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
    public static void BuildAndRun(){NaturalBronzeBeastAssetBuilder.Run();Run();Render();}
    public static void Run()
    {
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/ParkStatue.prefab"));
        try {
            var actor=go.GetComponent<ParkActor>();actor.species="BronzeBeast";actor.virtualTarget=true;go.SetActive(true);actor.Build();
            var rig=go.GetComponentInChildren<NaturalBronzeBeast>();Require(rig,"BronzeBeast still uses human statue");rig.Initialize();
            var skin=rig.GetComponentsInChildren<SkinnedMeshRenderer>().Single();var mesh=skin.sharedMesh;
            Require(mesh.triangles.Length/3==17148&&skin.bones.Length==2,"Bronze geometry/rig budget changed");
            var material=skin.sharedMaterial;Require(material.GetTexture("_BaseMap").name=="bull_head_diff_1k"&&material.GetTexture("_BumpMap").name=="bull_head_nor_gl_1k","Original bronze PBR missing");
            Require(material.GetTexture("_MetallicGlossMap")==material.GetTexture("_OcclusionMap")&&material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"),"Bronze ARM not remapped to URP");
            foreach(var suffix in new[]{"bull_head_nor_gl_1k.jpg","Bronze_MetalAO_Smooth.png"})Require(!((TextureImporter)AssetImporter.GetAtPath(NaturalBronzeBeastAssetBuilder.Folder+"/"+suffix)).sRGBTexture,"Bronze data map is sRGB");
            go.transform.position=new Vector3(.6f,.05f,-.4f);Require(actor.ConfirmOrientation(37),"Bronze yaw confirmation failed");
            var bottom=go.transform.position+Vector3.up*.015f;Require(actor.FitRepresentation(bottom,bottom+Vector3.up*.35f,true),"Bronze manual fit rejected");
            var rootPosition=go.transform.position;var rootRotation=go.transform.rotation;var fit=actor.visual.parent;var fitPosition=fit.localPosition;var fitScale=fit.localScale;
            for(int i=0;i<4;i++)rig.Step(.1f,0,true);var rest=Geometry(skin);float error=0,motion=0;Vector3[] last=null;
            for(int i=0;i<=128;i++){
                rig.Step(.016f,i/32f,true);var points=Geometry(skin);error=Mathf.Max(error,Mathf.Abs(points.Min(p=>p.y)-bottom.y));
                for(int j=0;j<points.Length;j++)motion=Mathf.Max(motion,Vector3.Distance(rest[j],points[j]));
                Require(go.transform.position==rootPosition&&go.transform.rotation==rootRotation&&fit.localPosition==fitPosition&&fit.localScale==fitScale,"Bronze animation moved placement root");last=points;
            }
            float seam=rest.Zip(last,(a,b)=>Vector3.Distance(a,b)).Max();Require(error<.0001f&&motion>.001f&&seam<.00001f,"Bronze fixed-base/loop failure: bottom="+error+" motion="+motion+" seam="+seam);
            // Every low original vertex is rigidly weighted to the stationary base.
            var local=mesh.vertices;float low=local.Min(v=>v.y),high=local.Max(v=>v.y);Require(high>low,"Bronze local geometry invalid");
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<512;i++)rig.Step(.016f,i*.016f,true);long allocated=GC.GetAllocatedBytesForCurrentThread()-before;Require(allocated==0,"Bronze per-frame allocation: "+allocated);
            actor.Undo();Require(rig.Blend==0,"Bronze Undo did not immediately reset procedural pose");rig.Step(.1f,1,true);actor.SetTracked(false);Require(rig.Blend==0,"Bronze tracking loss retained pose");actor.SetTracked(true);
            rig.Step(.1f,float.NaN,true);Require(rig.Blend==0,"Invalid bronze phase retained pose");
            Debug.Log("NATURAL_BRONZE_MODEL_PASS: 17148 original triangles/one skin/two bones; original 1K PBR; 129 independent LBS samples at 35cm fit/37deg yaw; bottom_error_m="+error+" motion_m="+motion+" loop_seam_m="+seam+"; 512 steps zero allocation; Undo/loss/invalid-time reset");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    public static void Render()=>RenderSequence(false);
    public static void RenderMotion()=>RenderSequence(true);
    static void RenderSequence(bool motion)
    {
        var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;var output=Path.GetFullPath(motion?".codex-tmp/bronze-motion/BronzeAwaken":".codex-tmp/bronze-beast-review");Directory.CreateDirectory(output);long bytes=0;int frame=0;
        bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        try {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.38f,.4f,.43f);
            foreach(var entry in new[]{("Warm key",new Vector3(35,-35,0),2.3f), ("Cool fill",new Vector3(25,125,0),1.2f)}){
                var light=new GameObject(entry.Item1).AddComponent<Light>();light.type=LightType.Directional;light.intensity=entry.Item3;light.transform.rotation=Quaternion.Euler(entry.Item2);
            }
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NaturalBronzeBeastAssetBuilder.Prefab));var rig=go.GetComponent<NaturalBronzeBeast>();rig.Initialize();
            var skin=go.GetComponentInChildren<SkinnedMeshRenderer>();var native=new Mesh();skin.BakeMesh(native);Debug.Log("BRONZE_OFFLINE_NATIVE_BAKE_DIAGNOSTIC vertices="+native.vertexCount+" local_bounds="+native.bounds);UnityEngine.Object.DestroyImmediate(native);
            var snapshot=new NaturalPetMotionFrames.Snapshot(go.transform,skin);snapshot.Sample();
            var camera=new GameObject("Bronze studio review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.32f;camera.nearClipPlane=.01f;camera.farClipPlane=10;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.18f,.20f);
            var bounds=snapshot.Renderer.bounds;camera.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.x,bounds.extents.z)*1.45f;
            camera.transform.position=bounds.center+new Vector3(.47f,.10f,-.85f);camera.transform.LookAt(bounds.center);
            foreach(var phase in motion?Enumerable.Range(0,96).Select(i=>i/24f):new[]{0f,1f,2f,3f}){
                for(int i=0;i<4;i++)rig.Step(.1f,phase,true);
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
