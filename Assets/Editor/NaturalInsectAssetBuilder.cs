using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using MagicMR;

public static class NaturalInsectAssetBuilder
{
    const string Folder="Assets/MagicMR/NaturalInsects/";
    public static void DiagnoseScale()
    {
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"Cockroach.fbx"));var baked=new Mesh();
        try
        {
            var skin=go.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;var poses=mesh.bindposes;var bones=skin.bones;
            var direct=new Bounds(skin.transform.TransformPoint(vertices[0]),Vector3.zero);var reference=new Bounds();
            for(int i=0;i<vertices.Length;i++)
            {
                direct.Encapsulate(skin.transform.TransformPoint(vertices[i]));var weight=weights[i];var point=bones[weight.boneIndex0].localToWorldMatrix.MultiplyPoint3x4(poses[weight.boneIndex0].MultiplyPoint3x4(vertices[i]));
                if(i==0)reference=new Bounds(point,Vector3.zero);else reference.Encapsulate(point);
            }
            skin.BakeMesh(baked,false);var points=baked.vertices;var bake=new Bounds(skin.transform.TransformPoint(points[0]),Vector3.zero);foreach(var point in points)bake.Encapsulate(skin.transform.TransformPoint(point));
            Debug.Log("INSECT_SCALE_DIAGNOSTIC renderer="+skin.bounds+" sharedWorld="+direct+" boneReferenceWorld="+reference+" bakeFalseWorld="+bake+" rendererLocalScale="+skin.transform.localScale);
        }
        finally{UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(go);}
    }
    public static void Install()
    {
        Run();Directory.CreateDirectory("Assets/Resources/MagicMR/NaturalModels");AssetDatabase.Refresh();
        foreach(var name in new[]{"Cockroach","Spider"})
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+".prefab"),scene);
                PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                PrefabUtility.SaveAsPrefabAsset(instance,"Assets/Resources/MagicMR/NaturalModels/"+name+".prefab");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        AssetDatabase.SaveAssets();
    }
    public static void ValidateAndRender(){Install();ParkNaturalModelChecks.Render();}
    public static void Run()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var shader=Shader.Find("MagicMR/NaturalCuticle");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Cuticle shader unavailable");
        var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Cuticle.mat");if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,Folder+"Cuticle.mat");}
        foreach(var name in new[]{"Cockroach","Spider"})
        {
            var importer=AssetImporter.GetAtPath(Folder+name+".fbx") as ModelImporter;if(!importer)throw new Exception("Original insect geometry missing");
            importer.importAnimation=false;importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.NoAvatar;importer.optimizeGameObjects=false;importer.SaveAndReimport();
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var root=new GameObject("Original anatomical "+name);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+".fbx"),scene);model.transform.SetParent(root.transform,false);
                PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                model.transform.localRotation=Quaternion.Euler(0,180,0)*model.transform.localRotation;
                var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();if(skins.Length!=1)throw new Exception("Insect must use one renderer");var skin=skins[0];skin.sharedMaterial=material;skin.quality=SkinQuality.Bone1;
                if(skin.sharedMesh.triangles.Length/3>30000||skin.bones.Length>64||skin.sharedMesh.colors.Length!=skin.sharedMesh.vertexCount)throw new Exception("Insect geometry/colour/rig budget failed");
                // At bind pose, mesh vertices transformed once agree with the independent bone/bind-matrix reference.
                // BakeMesh already includes this FBX renderer's scale; applying that transform again would scale it twice.
                var points=skin.sharedMesh.vertices;var bounds=new Bounds(skin.transform.TransformPoint(points[0]),Vector3.zero);
                foreach(var point in points)bounds.Encapsulate(skin.transform.TransformPoint(point));
                float scale=.34f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);model.transform.localScale*=scale;
                model.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
                var local=skin.sharedMesh.bounds;local.Expand(local.size*.2f);skin.localBounds=local;
                var animator=model.GetComponent<Animator>();if(animator)UnityEngine.Object.DestroyImmediate(animator);
                var driver=root.AddComponent<NaturalInsectRig>();driver.Initialize();if(driver.LegCount!=(name=="Spider"?8:6))throw new Exception("Insect leg count mismatch");
                PrefabUtility.SaveAsPrefabAsset(root,Folder+name+".prefab");
                Debug.Log("ORIGINAL_INSECT_IMPORT_PASS "+name+" triangles="+skin.sharedMesh.triangles.Length/3+" bones="+skin.bones.Length+"; candidate only");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        AssetDatabase.SaveAssets();
    }
    public static void RenderCandidates()
    {
        bool async=ShaderUtil.allowAsyncCompilation;string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        try
        {
            ShaderUtil.allowAsyncCompilation=false;Run();Directory.CreateDirectory(".codex-tmp/natural-insect-review");
            foreach(var name in new[]{"Cockroach","Spider"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.4f,.42f,.45f);
                var key=new GameObject("Studio key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.5f;key.transform.rotation=Quaternion.Euler(38,-35,0);
                var camera=new GameObject("Insect review").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.24f,.26f,.28f);camera.nearClipPlane=.001f;camera.orthographic=true;camera.orthographicSize=.22f;
                var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+".prefab"));
                var bounds=instance.GetComponentInChildren<Renderer>().bounds;camera.transform.position=bounds.center+new Vector3(.23f,.35f,-.32f);camera.transform.LookAt(bounds.center);
                float extent=0;
                for(int i=0;i<8;i++)
                {
                    var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var point=camera.transform.InverseTransformPoint(corner);extent=Mathf.Max(extent,Mathf.Abs(point.x),Mathf.Abs(point.y));
                }
                camera.orthographicSize=extent*1.18f;
                var target=new RenderTexture(1000,1000,24);var pixels=new Texture2D(1000,1000,TextureFormat.RGB24,false);var old=RenderTexture.active;
                try{camera.targetTexture=target;camera.Render();camera.Render();camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1000,1000),0,0);pixels.Apply();File.WriteAllBytes(".codex-tmp/natural-insect-review/"+name+".png",pixels.EncodeToPNG());}
                finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
            }
            Debug.Log("ORIGINAL_INSECT_CANDIDATES_RENDERED: visual review required, not installed into Resources");
        }
        finally{ShaderUtil.allowAsyncCompilation=async;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
}
