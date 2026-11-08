using System;
using System.Collections;
using System.IO;
using System.Reflection;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class MagicMRVfxPreview
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Output = ".codex-tmp/vfx-polish";
    [MenuItem("MagicMR/Checks/Polished VFX and Render Previews")]
    public static void Run()
    {
        // Batch renders in an isolated scene; never overwrite a user's open scene.
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!Application.isBatchMode && (previous.isDirty || string.IsNullOrEmpty(previous.path)))
            throw new InvalidOperationException("Save the current scene before rendering previews.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        var random = UnityEngine.Random.state;
        try
        {
            Directory.CreateDirectory(Output);
            RightHandSpellVfxChecks.Run();
            foreach (string path in new[] { "Assets/Resources/MagicMR/SoftParticle.shader", "Assets/MagicMR/Materials/CharredSurface.shader" })
                if (ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Shader>(path))) throw new Exception("Shader error: " + path);
            CheckAppearanceReset();
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.55f,.55f,.55f);
            var light = new GameObject("Studio key").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; light.transform.rotation = Quaternion.Euler(35,-35,0);
            var camera = new GameObject("VFX camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.085f,.11f,.15f);
            camera.orthographic = true; camera.orthographicSize = .105f; camera.nearClipPlane = .001f; camera.farClipPlane = 3;
            camera.transform.position = new Vector3(.07f,.115f,.32f); camera.transform.LookAt(new Vector3(-.015f,.065f,0));
            foreach (EditDimension dimension in new[] { EditDimension.Appearance, EditDimension.Agency, EditDimension.Rule, EditDimension.Deconstruction, EditDimension.Scale })
            {
                UnityEngine.Random.InitState(271 + (int)dimension);
                var root = Lighter();
                var owner = new GameObject("Hand effects"); var fx = owner.AddComponent<RightHandSpellVfx>(); Invoke(fx,"Awake");
                Vector3 source = new Vector3(-.09f,.075f,0), target = new Vector3(0,.055f,0);
                GameObject flower = null;
                if (dimension == EditDimension.Appearance)
                {
                    foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                    {
                        bool metal = r.sharedMaterial.HasProperty("_Metallic") && r.sharedMaterial.GetFloat("_Metallic") > .5f;
                        r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(metal ? "Assets/Resources/MagicMR/BurntMetal.mat" : "Assets/Models/Materials/burnt.mat");
                        var block = new MaterialPropertyBlock(); block.SetFloat("_Heat",.8f); r.SetPropertyBlock(block);
                    }
                    var smoke = MagicMRVfxFactory.CreateSmoke(root.transform);
                    smoke.Simulate(.32f,true,true,false);
                    if (smoke.particleCount == 0 || smoke.main.maxParticles > 40) throw new Exception("Smoke missing or unbounded");
                    if (Vector3.Distance(smoke.transform.lossyScale,Vector3.one) > .001f) throw new Exception("Smoke inherited lighter scale");
                }
                if (dimension == EditDimension.Deconstruction)
                {
                    root.SetActive(false);
                    flower = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Prefabs/Flower.prefab"));
                    RealityEditor.FitFlowerHeight(flower.transform,.11f); RealityEditor.CoverLighterWithStem(flower.transform,new Vector3(.025f,.08f,.012f)); RealityEditor.AlignFlowerBase(flower.transform,Vector3.zero);
                    MagicMRVfxFactory.CreateGoldDust(flower.transform).Simulate(.5f,true,true,false);
                    var blossom = flower.transform.Find("Blossom");
                    var full = blossom.localScale; var support = flower.transform.Find("Base").position;
                    var reveal = blossom.gameObject.AddComponent<MagicMRBloomReveal>(); Invoke(reveal,"Awake");
                    reveal.Sample(.35f);
                    if (blossom.localScale.x >= full.x || Vector3.Distance(support,flower.transform.Find("Base").position) > .00001f) throw new Exception("Bloom moved base or failed to unfurl");
                    reveal.Sample(.7f);
                    if (Vector3.Distance(blossom.localScale,full) > .00001f) throw new Exception("Bloom did not restore full size");
                }
                if (dimension == EditDimension.Scale) root.transform.localScale *= 1.5f;
                if (dimension == EditDimension.Rule)
                {
                    var ghost = MagicMRVfxFactory.CreateGhostTrailClone(root.transform,.5f);
                    ghost.transform.position -= Vector3.right * .025f;
                    if (ghost.GetComponentsInChildren<RealityEditor>().Length != 0) throw new Exception("Ghost cloned gameplay");
                    CaptureCast(fx,dimension,source,target,camera);
                    UnityEngine.Object.DestroyImmediate(ghost);
                }
                else CaptureCast(fx,dimension,source,target,camera);
                if (flower != null) UnityEngine.Object.DestroyImmediate(flower);
                UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(root);
            }
            // Continuous finger trace uses the actual Preview API; no tracked hand is claimed.
            var fingerOwner = new GameObject("Finger tracing sample"); var fingerFx = fingerOwner.AddComponent<RightHandSpellVfx>(); Invoke(fingerFx,"Awake");
            for (int i=0;i<64;i++) { float a=i/63f*Mathf.PI*1.8f; Vector3 tip=new Vector3(Mathf.Cos(a)*.035f,.065f+Mathf.Sin(a)*.035f,0); fingerFx.Preview(tip-Vector3.up*.03f,tip,tip-Vector3.right*.02f,true,false,false,false); }
            Age(fingerFx,.06f); Capture(camera,"finger-trace"); fingerFx.Clear();
            fingerFx.Summon(new Vector3(-.08f,.05f,0),new Vector3(.025f,.075f,0)); Age(fingerFx,.16f); Capture(camera,"summon");
            UnityEngine.Object.DestroyImmediate(fingerOwner);
            Debug.Log("POLISHED_VFX_PASS: shader, particle emission/scale/budget, geometry-only ghost, existing gesture/reset/pool regression, seven renders");
        }
        finally { UnityEngine.Random.state=random; if (previous.IsValid() && previous.isLoaded) { EditorSceneManager.CloseScene(scene,true); UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous); } }
    }
    static void CheckAppearanceReset()
    {
        var root = Lighter();
        try
        {
            var original = root.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            var editor = root.AddComponent<RealityEditor>(); Invoke(editor,"Awake");
            var settings = new SerializedObject(editor);
            settings.FindProperty("m_BurntMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Materials/burnt.mat");
            settings.ApplyModifiedPropertiesWithoutUndo();
            editor.TriggerAppearance();
            var renderer = root.GetComponentInChildren<MeshRenderer>(); var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            if (block.GetFloat("_Heat") < .9f) throw new Exception("Missing initial ember heat");
            editor.ResetTarget(); renderer.GetPropertyBlock(block);
            if (block.GetFloat("_Heat") != 0 || renderer.sharedMaterial != original) throw new Exception("Reset retained heat or burnt material");
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>()) if (ps.particleCount != 0 || ps.isPlaying) throw new Exception("Reset retained smoke");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    static void CaptureCast(RightHandSpellVfx fx,EditDimension dimension,Vector3 source,Vector3 target,Camera camera)
    {
        fx.Cast(dimension,target,source,true); Age(fx,.20f); Capture(camera,dimension.ToString().ToLowerInvariant());
    }
    static void Age(RightHandSpellVfx fx,float age)
    {
        foreach (var stroke in (IEnumerable)typeof(RightHandSpellVfx).GetField("pool",Private).GetValue(fx))
            stroke.GetType().GetField("start").SetValue(stroke,Time.unscaledTime-age);
        Invoke(fx,"Update");
    }
    static void Invoke(object obj,string method) { obj.GetType().GetMethod(method,Private).Invoke(obj,null); }
    static GameObject Lighter()
    {
        var root = new GameObject("Lighter preview"); root.transform.localScale=new Vector3(.025f,.08f,.012f); root.transform.position=Vector3.up*.04f;
        foreach (string name in new[] { "RoundedBody","Collar","WindShield","SerratedWheel","ThumbLever","Vent0","Vent1","Vent2","Vent3","Vent4","BaseSeam" })
        {
            var go=new GameObject(name); go.transform.SetParent(root.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/MagicMR/LighterGeometry/"+name+".asset");
            string material=name=="RoundedBody"?"BlueGlossPlastic":name=="WindShield"||name=="SerratedWheel"?"BrushedSteel":name=="ThumbLever"?"IgnitionLever":"DarkRecess";
            go.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/MagicMR/LighterGeometry/"+material+".mat");
        }
        return root;
    }
    static void Capture(Camera camera,string label)
    {
        var rt=new RenderTexture(1200,1000,24); var old=RenderTexture.active; camera.targetTexture=rt;
        try { camera.Render(); camera.Render(); RenderTexture.active=rt; var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false); tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); tex.Apply(); File.WriteAllBytes(Output+"/"+label+".png",tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex); }
        finally { camera.targetTexture=null; RenderTexture.active=old; UnityEngine.Object.DestroyImmediate(rt); }
    }
}
