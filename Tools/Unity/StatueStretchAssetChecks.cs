using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Asset-level import/render validation. Does not claim the HCI gesture or XR route is integrated.
public static class StatueStretchAssetChecks
{
    const string Asset="Assets/StatueStretchCandidate/StandingStatue.fbx";
    static string Output=>Path.GetFullPath(".codex-tmp/statue-stretch-asset");
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run()
    {
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");
        try{
            var importer=AssetImporter.GetAtPath(Asset) as ModelImporter;Require(importer,"Missing explicitly staged candidate FBX");
            importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;importer.isReadable=false;importer.animationCompression=ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
            var clips=AssetDatabase.LoadAllAssetsAtPath(Asset).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            Require(clips.Length==1,"Expected exactly one authored stretch clip");var clip=clips[0];Require(clip.length>=5&&clip.length<=5.2f,"Wrong duration");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Asset));
            var animator=model.GetComponent<Animator>();if(animator)animator.enabled=false;
            var renderers=model.GetComponentsInChildren<SkinnedMeshRenderer>();Require(renderers.Length>=2,"Missing skinned body and drape");
            int triangles=renderers.Sum(r=>(int)r.sharedMesh.GetIndexCount(0)/3);Require(triangles<16000,"Triangle budget exceeded");
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",new Color(.56f,.51f,.42f));material.SetFloat("_Smoothness",.15f);
            foreach(var r in renderers){r.sharedMaterials=Enumerable.Repeat(material,r.sharedMaterials.Length).ToArray();r.updateWhenOffscreen=true;Require(r.bones.All(b=>b),"Null skin bone");}
            Transform Bone(string name)=>model.GetComponentsInChildren<Transform>().Single(t=>t.name==name);
            clip.SampleAnimation(model,0);var footL=Bone("foot_l").position;var footR=Bone("foot_r").position;var pelvis=Bone("pelvis").position;var handL=Bone("hand_l").position;var handR=Bone("hand_r").position;
            var rootPosition=model.transform.position;var rootRotation=model.transform.rotation;var rootScale=model.transform.localScale;
            var trajectory=new List<string>{"time,leftHandY,rightHandY,leftFootDrift,rightFootDrift,pelvisDrift"};float rise=0;float maxSkinRise=0;
            var baked=new Mesh();float initialTop=0;
            for(int f=0;f<=152;f++){
                float time=clip.length*f/152;clip.SampleAnimation(model,time);
                float left=Vector3.Distance(Bone("foot_l").position,footL),right=Vector3.Distance(Bone("foot_r").position,footR),hip=Vector3.Distance(Bone("pelvis").position,pelvis);
                Require(left<.0001f&&right<.0001f&&hip<.0001f,"Anchor drift at frame "+f);
                Require(model.transform.position==rootPosition&&model.transform.rotation==rootRotation&&model.transform.localScale==rootScale,"Clip moves model root");
                rise=Mathf.Max(rise,Bone("hand_l").position.y-handL.y);
                if(f==0||f==63){float top=float.NegativeInfinity;foreach(var r in renderers){r.BakeMesh(baked,true);foreach(var p in baked.vertices)top=Mathf.Max(top,r.transform.TransformPoint(p).y);}if(f==0)initialTop=top;else maxSkinRise=top-initialTop;}
                trajectory.Add(FormattableString.Invariant($"{time:F5},{Bone("hand_l").position.y:F6},{Bone("hand_r").position.y:F6},{left:F8},{right:F8},{hip:F8}"));
            }
            Require(initialTop>1.5f&&initialTop<2.1f&&rise>.9f&&rise<1.2f&&maxSkinRise>.2f&&maxSkinRise<.6f,"Skeleton moves but mesh does not visibly stretch");
            Require(Vector3.Distance(Bone("hand_l").position,handL)<.0001f&&Vector3.Distance(Bone("hand_r").position,handR)<.0001f,"Clip does not settle to original pose");
            var sun=new GameObject("Review key").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.6f;sun.transform.rotation=Quaternion.Euler(38,-140,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.5f,.5f);
            var camera=new GameObject("Review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.35f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.20f,.23f,.26f);
            var target=new Vector3(0,1.05f,0);camera.transform.position=target+new Vector3(3,1.5f,-6);camera.transform.LookAt(target);
            foreach(int f in new[]{0,42,63,97,152}){clip.SampleAnimation(model,clip.length*f/152);CaptureSkinned(camera,renderers,"pose-"+f.ToString("000"));}
            camera.transform.position=target+new Vector3(-3,1.5f,6);camera.transform.LookAt(target);clip.SampleAnimation(model,clip.length*63/152);CaptureSkinned(camera,renderers,"reverse-063");
            File.WriteAllLines(Output+"/trajectory.csv",trajectory);
            File.WriteAllText(Output+"/result.txt",FormattableString.Invariant($"PASS: Unity 6000.0.33f1 imports one {clip.length:F3}s generic skinned clip; {triangles} triangles; 153 sampled poses; feet/pelvis drift <0.1mm; root unchanged; hands settle to rest; hand rise {rise:F3}m and baked skin height rise {maxSkinRise:F3}m; six Unity camera renders of CPU-skinned sampled poses (not Play Mode playback). Asset candidate only: not yet wired into ParkStatue, shared gestures, Android or PICO.\n"));
            UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(material);EditorApplication.Exit(0);
        }catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL: "+e);Debug.LogException(e);EditorApplication.Exit(2);}
    }
    // Synchronous Editor sampling does not advance the GPU skinning frame.
    // Render explicit CPU-skinned snapshots, never label these Play Mode playback.
    static void CaptureSkinned(Camera camera,SkinnedMeshRenderer[] renderers,string name){
        var temporary=new List<GameObject>();var meshes=new List<Mesh>();
        try{
            foreach(var r in renderers){
                var mesh=new Mesh();r.BakeMesh(mesh,true);meshes.Add(mesh);
                var go=new GameObject("CPU sampled "+r.name);temporary.Add(go);
                go.transform.SetPositionAndRotation(r.transform.position,r.transform.rotation);go.transform.localScale=r.transform.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=r.sharedMaterials;r.enabled=false;
            }
            Capture(camera,name);
        }finally{foreach(var go in temporary)UnityEngine.Object.DestroyImmediate(go);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);foreach(var r in renderers)r.enabled=true;}
    }
    static void Capture(Camera camera,string name){
        var rt=new RenderTexture(720,900,24);var texture=new Texture2D(720,900,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,720,900),0,0);texture.Apply();File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);}
    }
}
