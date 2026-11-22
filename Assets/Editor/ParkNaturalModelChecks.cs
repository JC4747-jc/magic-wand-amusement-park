using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ParkNaturalModelChecks
{
    static void Require(bool ok,string message) {if(!ok)throw new Exception(message);}
    public static void Run()
    {
        NaturalBronzeBeastChecks.Run();
        foreach(var mesh in new[]{ParkModelMeshes.Trunk,ParkModelMeshes.Leaf,ParkModelMeshes.Canopy,ParkModelMeshes.Ear,ParkModelMeshes.Tail,ParkModelMeshes.Pot,ParkModelMeshes.RoundedBox,ParkModelMeshes.Flower})
        {
            Require(mesh && mesh.vertexCount>0 && mesh.vertexCount<5000,"Invalid or excessive shared geometry");
            Require(mesh.vertices.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)),"Nonfinite model geometry");
            Require(mesh.triangles.All(i=>i>=0&&i<mesh.vertexCount),"Mesh index out of bounds");
        }
        Require(ParkModelMeshes.Canopy==ParkModelMeshes.Canopy,"Canopy cache is not reused");
        NaturalPlantChecks.Run();
        NaturalTreeChecks.Run();
        NaturalTreeMarkChecks.Run();
        NaturalInsectChecks.Run();
        NaturalPetModelChecks.Run();
        NaturalCatWalkChecks.Run();
        NaturalDogModelChecks.Run();
        NaturalJellyfishChecks.Run();
        NaturalRoomDetailChecks.Run();
        ParkRegressionChecks.Run();
        var update=typeof(ParkActor).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(var name in ParkContentBuilder.Names)
        {
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/"+name+".prefab"));
            try
            {
                var actor=go.GetComponent<ParkActor>();go.SetActive(true);actor.Build();
                var renderer=actor.visual.GetComponentsInChildren<Renderer>().First(r=>r is SkinnedMeshRenderer||r.GetComponent<MeshFilter>());
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);var original=block.GetColor("_BaseColor");
                for(int i=0;i<4;i++)update.Invoke(actor,null);
                actor.ResetEffects();renderer.GetPropertyBlock(block);
                Require(block.GetColor("_BaseColor")==original,"Idle/reset changed component albedo in "+name);
                Require(actor.visual.GetComponentsInChildren<Renderer>(true).Length<100,"Model exceeded renderer budget");
            }
            finally {UnityEngine.Object.DestroyImmediate(go);}
        }
        Debug.Log("PARK_NATURAL_MODEL_CHECKS_PASS: component albedo restoration, finite cached meshes and renderer budgets; editor only");
    }
    public static void Render()
    {
        Run();
        string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        var previousAmbient=RenderSettings.ambientMode;var previousColor=RenderSettings.ambientLight;
        bool previousAsync=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        string output=Path.GetFullPath(".codex-tmp/park-natural-models");Directory.CreateDirectory(output);
        try
        {
            foreach(var name in ParkContentBuilder.Names)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.45f,.47f,.5f);
                var light=new GameObject("Studio key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(38,-35,0);
                var camera=new GameObject("Model review camera").AddComponent<Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.20f,.22f,.24f);camera.nearClipPlane=.01f;camera.orthographic=true;
                var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/"+name+".prefab"));
                go.SetActive(true);var actor=go.GetComponent<ParkActor>();actor.virtualTarget=true;actor.Build();actor.ResetEffects();if(actor.label)actor.label.gameObject.SetActive(false);
                Require(actor.visual.GetComponentsInChildren<MeshFilter>().All(f=>f.sharedMesh&&f.sharedMesh.vertexCount>0),"Scene transition lost visible mesh in "+name);
                var renderers=actor.visual.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
                Require(renderers.Length>0,"No visible model in "+name);var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                camera.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.x*.8f,bounds.extents.z*.9f)*1.5f;
                camera.transform.position=bounds.center+new Vector3(.9f,.55f,-1.6f).normalized*Mathf.Max(bounds.size.magnitude*2,1);camera.transform.LookAt(bounds.center);
                var target=new RenderTexture(1000,900,24);var pixels=new Texture2D(1000,900,TextureFormat.RGB24,false);var old=RenderTexture.active;
                try {camera.targetTexture=target;camera.Render();camera.Render();camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1000,900),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,name+"-EDITOR.png"),pixels.EncodeToPNG());}
                finally {camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
            }
            Debug.Log("PARK_NATURAL_MODEL_RENDERS: six actual Unity model views; visual review required, no headset validation");
        }
        finally {ShaderUtil.allowAsyncCompilation=previousAsync;RenderSettings.ambientMode=previousAmbient;RenderSettings.ambientLight=previousColor;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
}
