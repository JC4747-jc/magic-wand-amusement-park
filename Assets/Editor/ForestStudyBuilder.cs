using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ForestStudyBuilder
{
    const string Folder = "Assets/ThirdParty/ForestStudy/";
    public const string ScenePath = "Assets/Scenes/ForestStudy.unity";
    const string Output = ".codex-tmp/forest-study";
    static Material MapMaterial(string name, string diffuse, string normal, bool foliage = false)
    {
        string path = Folder + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat) { mat = new Material(Shader.Find(foliage ? "MagicMR/NaturalTreeLeaves" : "Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + diffuse));
        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + normal)); mat.SetFloat("_BumpScale", .65f);
        mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Smoothness", .05f); mat.EnableKeyword("_NORMALMAP");
        if (foliage) { mat.SetTexture("_AlphaMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Fern_alpha.jpg")); mat.SetFloat("_Cutoff", .4f); mat.SetFloat("_LeafCoverage", 1); }
        mat.enableInstancing = true; EditorUtility.SetDirty(mat); return mat;
    }
    static Bounds Bounds(GameObject obj)
    {
        var all = obj.GetComponentsInChildren<Renderer>(); if (all.Length == 0) throw new Exception("Mesh missing");
        var bounds = all[0].bounds; foreach (var r in all) bounds.Encapsulate(r.bounds); return bounds;
    }
    static void RemapTree(GameObject obj)
    {
        foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
        {
            string part = renderer.name.Contains("Leaves") ? "Leaves" : renderer.name.Contains("Branches") ? "Branches" : "Trunk";
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/ThirdParty/NaturalTrees/SmallTree/" + part + ".mat");
            if (!renderer.sharedMaterial) throw new Exception("Original tree material missing");
        }
    }
    [MenuItem("MagicMR/Forest/Create natural asset study")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play Mode first");
        if (File.Exists(ScenePath)) throw new Exception("Existing forest study must be edited, not overwritten");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (var path in Directory.GetFiles(Folder, "*.jpg"))
        {
            var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as TextureImporter;
            importer.maxTextureSize = 1024; importer.sRGBTexture = path.Contains("diff"); importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 4;
            if (path.Contains("normal")) importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
        foreach (var file in new[] { "TreeLOD0.fbx", "TreeLOD1.fbx", "TreeLOD2.fbx", "Fern.fbx" })
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + file); importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None; importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }
        var ground = MapMaterial("Forest ground", "Ground_diff.jpg", "Ground_normal.jpg"); ground.SetTextureScale("_BaseMap", Vector2.one * .5f);
        var routeMat = MapMaterial("Forest path", "Ground_diff.jpg", "Ground_normal.jpg"); routeMat.SetTextureScale("_BaseMap", Vector2.one * .5f); routeMat.SetColor("_BaseColor", new Color(.72f, .68f, .58f));
        var fernMat = MapMaterial("Fern", "Fern_diff.jpg", "Fern_normal.jpg", true);
        EditorSceneManager.OpenScene(ForestBlockoutBuilder.ScenePath);
        var terrain = GameObject.Find("Editable terrain - Polybrush sculpt target"); terrain.GetComponent<Renderer>().sharedMaterials = new[] { ground, routeMat };
        var visualRoot = new GameObject("Natural forest visual study - CC0 assets");
        var lodTemplate = new GameObject("Static tree with three LODs");
        var lods = new List<LOD>(); Bounds basis = default; float unit = 1;
        for (int level = 0; level < 3; level++)
        {
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "TreeLOD" + level + ".fbx"));
            model.name = "Tree LOD " + level; model.transform.SetParent(lodTemplate.transform, false); RemapTree(model);
            if (level == 0) { basis = Bounds(model); unit = 1 / basis.size.y; }
            model.transform.localScale *= unit; model.transform.localPosition -= new Vector3(basis.center.x, basis.min.y, basis.center.z) * unit;
            lods.Add(new LOD(new[] { .35f, .16f, .025f }[level], model.GetComponentsInChildren<Renderer>()));
        }
        var group = lodTemplate.AddComponent<LODGroup>(); group.SetLODs(lods.ToArray()); group.RecalculateBounds();
        for (int row = 0; row < 8; row++) for (int side = -1; side <= 1; side += 2)
        {
            var trunk = GameObject.Find("Trunk volume " + row + ":" + side); trunk.GetComponent<Renderer>().enabled = false;
            GameObject.Find("Canopy clearance " + row + ":" + side).GetComponent<Renderer>().enabled = false;
            var tree = UnityEngine.Object.Instantiate(lodTemplate, visualRoot.transform); tree.name = "Tree " + row + ":" + side;
            var p = trunk.transform.position; p.y = ForestBlockoutBuilder.Height(p.x, p.z);
            tree.transform.SetPositionAndRotation(p, Quaternion.Euler(0, row * 71 + side * 39, 0));
            tree.transform.localScale = Vector3.one * (6.5f + (row % 3) * .65f);
        }
        // Outer trees create a second depth layer and hide the whitebox perimeter without changing its collision.
        for (int n = 0; n < 12; n++)
        {
            float z = -13 + (n / 2) * 5f, x = (n % 2 == 0 ? -1 : 1) * 9;
            var tree = UnityEngine.Object.Instantiate(lodTemplate, visualRoot.transform); tree.name = "Background tree " + n;
            tree.transform.position = new Vector3(x, ForestBlockoutBuilder.Height(x,z), z); tree.transform.rotation = Quaternion.Euler(0, n * 113, 0);
            tree.transform.localScale = Vector3.one * (8 + n % 3);
        }
        UnityEngine.Object.DestroyImmediate(lodTemplate);
        var fernTemplate = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Fern.fbx"));
        foreach (var r in fernTemplate.GetComponentsInChildren<Renderer>()) r.sharedMaterial = fernMat;
        var fb = Bounds(fernTemplate); float fernScale = .48f / fb.size.y;
        var fernRoot = new GameObject("Fern template"); fernTemplate.transform.SetParent(fernRoot.transform, false);
        fernTemplate.transform.localScale *= fernScale; fernTemplate.transform.localPosition -= new Vector3(fb.center.x, fb.min.y, fb.center.z) * fernScale;
        var fernLod = fernRoot.AddComponent<LODGroup>(); fernLod.SetLODs(new[] { new LOD(.018f, fernTemplate.GetComponentsInChildren<Renderer>()) }); fernLod.RecalculateBounds();
        for (int n = 0; n < 42; n++)
        {
            float z = -14 + (n / 2) * 1.35f, side = n % 2 == 0 ? -1 : 1;
            float x = ForestBlockoutBuilder.Center(z) + side * (2.5f + (n % 4) * .55f);
            var fern = UnityEngine.Object.Instantiate(fernRoot, visualRoot.transform); fern.name = "Fern cluster " + n;
            fern.transform.position = new Vector3(x, ForestBlockoutBuilder.Height(x,z), z);
            fern.transform.rotation = Quaternion.Euler(0, n * 137.5f, 0); fern.transform.localScale *= .75f + (n % 5) * .08f;
        }
        UnityEngine.Object.DestroyImmediate(fernRoot);
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            if (renderer.name.StartsWith("Boundary ")) renderer.enabled = false;
        var light = GameObject.Find("Overcast key").GetComponent<Light>(); light.transform.rotation = Quaternion.Euler(48, -45, 0); light.intensity = 1.2f;
        light.color = new Color(1, .95f, .84f); light.shadowBias = .025f; light.shadowNormalBias = .25f;
        RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.28f, .33f, .36f);
        RenderSettings.ambientEquatorColor = new Color(.16f, .19f, .14f); RenderSettings.ambientGroundColor = new Color(.08f, .07f, .055f);
        RenderSettings.fogColor = new Color(.22f, .28f, .27f); RenderSettings.fogStartDistance = 16; RenderSettings.fogEndDistance = 62;
        Camera.main.backgroundColor = RenderSettings.fogColor;
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
        Render();
    }
    public static void Render()
    {
        EditorSceneManager.OpenScene(ScenePath); Directory.CreateDirectory(Output);
        var camera = Camera.main; var renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        foreach (var renderer in renderers)
            if (renderer.enabled && renderer.sharedMaterials.Any(m => !m || !m.shader || ShaderUtil.ShaderHasError(m.shader))) throw new Exception("Missing or failed material: " + renderer.name);
        var groups = UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
        if (groups.Length != 70 && groups.Length != 96) throw new Exception("Forest LOD groups incomplete: " + groups.Length);
        Capture(camera, "eye-level.png"); camera.transform.position = new Vector3(1, 1.8f, -3); camera.transform.LookAt(new Vector3(2, 1.3f, 9)); Capture(camera, "middle.png");
        camera.transform.position = new Vector3(19, 24, -24); camera.transform.LookAt(Vector3.zero);
        bool fog = RenderSettings.fog; RenderSettings.fog = false; Capture(camera, "overview.png"); RenderSettings.fog = fog;
        File.WriteAllText(Output + "/result.txt", "Static natural-asset candidate rendered: " + (groups.Length-42) + " trees with 3 LODs and 42 fern clusters with distance culling; no missing/failed materials; art and PICO acceptance pending.\n");
        Debug.Log("FOREST_STUDY_RENDER_PASS lodGroups=" + groups.Length + "; static asset study, not headset performance acceptance");
    }
    public static void Refine()
    {
        EditorSceneManager.OpenScene(ScenePath);
        if (GameObject.Find("Outer ground study")) throw new Exception("Refinement already applied; edit existing scene");
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Forest ground.mat");
        material.shader = Shader.Find("MagicMR/ForestGround");
        if (!material.shader || ShaderUtil.ShaderHasError(material.shader)) throw new Exception("Forest ground shader error");
        material.SetFloat("_TileMetres",2); material.SetFloat("_BumpScale",.55f); material.SetColor("_BaseColor",Color.white);
        GameObject.Find("Editable terrain - Polybrush sculpt target").GetComponent<Renderer>().sharedMaterials = new[]{ material, material };
        var vertices = new List<Vector3>(); var indices = new List<int>();
        for (int z=-30;z<34;z+=2) for(int x=-30;x<30;x+=2)
        {
            if(x>=-12 && x<12 && z>=-16 && z<16) continue;
            int i=vertices.Count;
            foreach(var v in new[]{new Vector2(x,z),new Vector2(x,z+2),new Vector2(x+2,z),new Vector2(x+2,z+2)})
                vertices.Add(new Vector3(v.x,ForestBlockoutBuilder.Height(v.x,v.y)-.03f,v.y));
            indices.AddRange(new[]{i,i+1,i+2,i+2,i+1,i+3});
        }
        var mesh = new Mesh { name="Background ground ring" }; mesh.SetVertices(vertices); mesh.SetTriangles(indices,0); mesh.RecalculateNormals();
        AssetDatabase.CreateAsset(mesh,Folder+"Background ground.asset");
        var ring = new GameObject("Outer ground study"); ring.AddComponent<MeshFilter>().sharedMesh=mesh; ring.AddComponent<MeshRenderer>().sharedMaterial=material;
        var parent=GameObject.Find("Natural forest visual study - CC0 assets").transform;
        var template=GameObject.Find("Tree 0:-1");
        for(int n=0;n<26;n++)
        {
            float x,z;
            if(n<14){x=-15+(n%7)*5;z=n<7?19:26;}
            else {x=n%2==0?-17:17;z=-14+(n/2-7)*6;}
            var tree=UnityEngine.Object.Instantiate(template,parent);tree.name="Forest depth tree "+n;
            tree.transform.position=new Vector3(x,ForestBlockoutBuilder.Height(x,z),z); tree.transform.rotation=Quaternion.Euler(0,n*137.5f,0);
            tree.transform.localScale=Vector3.one*(7.5f+n%4);
        }
        RenderSettings.ambientSkyColor=new Color(.4f,.47f,.49f);RenderSettings.ambientEquatorColor=new Color(.27f,.3f,.24f);RenderSettings.ambientGroundColor=new Color(.13f,.12f,.09f);
        GameObject.Find("Overcast key").GetComponent<Light>().intensity=1;
        foreach(var station in UnityEngine.Object.FindObjectsByType<ForestPreviewStation>(FindObjectsSortMode.None))
            station.indicator.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/ThirdParty/NaturalTrees/SmallTree/Trunk.mat");
        EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());Render();
    }
    public static void AddWind()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var shader = Shader.Find("MagicMR/ForestFoliage");
        if (!shader || ShaderUtil.ShaderHasError(shader)) throw new Exception("Forest foliage shader failed");
        var leafPath=Folder+"Forest leaves.mat";
        if (File.Exists(leafPath)) throw new Exception("Wind already authored");
        var leaves=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/ThirdParty/NaturalTrees/SmallTree/Leaves.mat"));
        leaves.shader=shader; leaves.SetFloat("_WindAmplitude",.035f); leaves.SetFloat("_WindTime",-1); AssetDatabase.CreateAsset(leaves,leafPath);
        var fern=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Fern.mat"); fern.shader=shader; fern.SetFloat("_WindAmplitude",.018f); fern.SetFloat("_WindTime",-1);
        var leafRenderers=new List<MeshRenderer>();var fernRenderers=new List<MeshRenderer>();
        foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if(r.name.Contains("Leaves")){r.sharedMaterial=leaves;leafRenderers.Add(r);}
            else if(r.sharedMaterial==fern) fernRenderers.Add(r);
        }
        ConfigureWind(leaves,leafRenderers);ConfigureWind(fern,fernRenderers);
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        RenderWindPhases(leaves,fern);
    }
    static void ConfigureWind(Material material,IEnumerable<MeshRenderer> renderers)
    {
        var heights=new List<float>();
        foreach(var r in renderers)
        {
            var bounds=r.GetComponent<MeshFilter>().sharedMesh.bounds;
            var up=r.transform.InverseTransformDirection(Vector3.up).normalized;
            for(int i=0;i<8;i++) heights.Add(Vector3.Dot(up,bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
        }
        if(heights.Count==0)throw new Exception("Wind geometry not assigned");
        float min=heights.Min(),height=heights.Max()-min;
        if(height<.001f)throw new Exception("Degenerate wind height");
        material.SetFloat("_WindBase",min);material.SetFloat("_WindHeight",height);EditorUtility.SetDirty(material);
    }
    public static void RefreshWindWeights()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var leaves=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Forest leaves.mat");
        var fern=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Fern.mat");
        var renderers=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        ConfigureWind(leaves,renderers.Where(r=>r.sharedMaterial==leaves));ConfigureWind(fern,renderers.Where(r=>r.sharedMaterial==fern));
        AssetDatabase.SaveAssets(); RenderWindPhases(leaves,fern);
    }
    static void RenderWindPhases(Material leaves,Material fern)
    {
        Render(); EditorSceneManager.OpenScene(ScenePath);
        try
        {
            for(int frame=0;frame<2;frame++)
            {
                leaves.SetFloat("_WindTime",frame*1.2f);fern.SetFloat("_WindTime",frame*1.2f); Capture(Camera.main,"wind-"+frame+".png");
            }
        }
        finally{leaves.SetFloat("_WindTime",-1);fern.SetFloat("_WindTime",-1);EditorUtility.SetDirty(leaves);EditorUtility.SetDirty(fern);AssetDatabase.SaveAssets();}
        Debug.Log("FOREST_WIND_RENDERED: base-weighted GPU wind, two deterministic phases; pixel difference verification pending");
    }
    static void Capture(Camera camera, string name)
    {
        bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        var target = new RenderTexture(1280, 800, 24); var pixels = new Texture2D(1280,800,TextureFormat.RGB24,false); var old = RenderTexture.active;
        try { camera.targetTexture = target; camera.Render(); camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,1280,800),0,0); pixels.Apply(); File.WriteAllBytes(Output + "/" + name,pixels.EncodeToPNG()); }
        finally { camera.targetTexture = null; RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels); ShaderUtil.allowAsyncCompilation = async; }
    }
}
