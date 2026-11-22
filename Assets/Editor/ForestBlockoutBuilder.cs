using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;

public static class ForestBlockoutBuilder
{
    public const string ScenePath = "Assets/Scenes/ForestBlockout.unity";
    const string Materials = "Assets/ForestBlockout";
    public const string Output = ".codex-tmp/forest-blockout";
    public static float Center(float z) => 2.4f * Mathf.Sin(z * .19f);
    public static float Height(float x, float z)
    {
        float shoulder = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.8f, 7, Mathf.Abs(x - Center(z))));
        return .35f * Mathf.Sin(z * .15f) + shoulder * (.8f + .4f * Mathf.Sin(x * .45f + z * .28f));
    }
    static Material Material(string name, Color color)
    {
        string path = Materials + "/" + name + ".mat";
        var value = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!value) { value = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(value, path); }
        value.SetColor("_BaseColor", color); value.SetFloat("_Smoothness", .08f); return value;
    }
    static GameObject Box(string name, Vector3 position, Vector3 size, Material material)
    {
        var mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
        mesh.name = name; mesh.transform.position = position;
        mesh.GetComponent<Renderer>().sharedMaterial = material;
        if (!mesh.GetComponent<Collider>()) mesh.gameObject.AddComponent<BoxCollider>();
        return mesh.gameObject;
    }
    [MenuItem("MagicMR/Forest/Create scale blockout")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play Mode before authoring");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath)) throw new Exception("Forest scene already exists; edit it rather than overwrite authored work");
        Directory.CreateDirectory(Materials); Directory.CreateDirectory(Output); AssetDatabase.Refresh();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var earth = Material("Blockout ground", new Color(.31f, .34f, .32f));
        var path = Material("Blockout route", new Color(.62f, .61f, .55f));
        var timber = Material("Blockout volumes", new Color(.27f, .30f, .31f));
        var marker = Material("Scale and stations", new Color(.65f, .5f, .28f));
        var verts = new List<Vector3>(); var faces = new List<Face>();
        // Shared spatial grid, split quad vertices allow independent face/material editing in ProBuilder.
        for (int z = -16; z < 16; z++) for (int x = -12; x < 12; x++)
        {
            int i = verts.Count;
            verts.Add(new Vector3(x, Height(x, z), z)); verts.Add(new Vector3(x, Height(x, z + 1), z + 1));
            verts.Add(new Vector3(x + 1, Height(x + 1, z), z)); verts.Add(new Vector3(x + 1, Height(x + 1, z + 1), z + 1));
            faces.Add(new Face(new[] { i, i + 1, i + 2, i + 2, i + 1, i + 3 }) { smoothingGroup = 1, submeshIndex = Mathf.Abs(x + .5f - Center(z + .5f)) <= 1.6f ? 1 : 0 });
        }
        var terrain = ProBuilderMesh.Create(verts, faces); terrain.name = "Editable terrain - Polybrush sculpt target";
        terrain.GetComponent<Renderer>().sharedMaterials = new[] { earth, path }; terrain.ToMesh(); terrain.Refresh();
        terrain.gameObject.AddComponent<MeshCollider>().sharedMesh = terrain.GetComponent<MeshFilter>().sharedMesh;
        // Deliberately coarse tree volumes: reserve canopy and trunk space, without pretending these are finished trees.
        for (int row = 0; row < 8; row++) for (int side = -1; side <= 1; side += 2)
        {
            float z = -13 + row * 3.8f, x = Center(z) + side * (3.6f + .35f * (row % 3));
            float y = Height(x, z), h = 5.5f + row % 3;
            Box("Trunk volume " + row + ":" + side, new Vector3(x, y + h / 2, z), new Vector3(.65f, h, .65f), timber);
            Box("Canopy clearance " + row + ":" + side, new Vector3(x, y + h, z), new Vector3(3.6f, 1.5f, 3.5f), timber);
        }
        foreach (int side in new[] { -1, 1 })
        {
            Box("Boundary X " + side, new Vector3(side * 12, 1, 0), new Vector3(.3f, 4, 32), earth);
            Box("Boundary Z " + side, new Vector3(0, 1, side * 16), new Vector3(24, 4, .3f), earth);
        }
        var stations = new List<ForestPreviewStation>();
        foreach (float z in new[] { -8f, 0f, 9f })
        {
            float x = Center(z) + 2; var p = new Vector3(x, Height(x, z), z);
            var go = Box("Inspect station " + stations.Count, p + Vector3.up * .55f, new Vector3(.4f, 1.1f, .4f), marker);
            var station = go.AddComponent<ForestPreviewStation>(); station.indicator = go.GetComponent<Renderer>();
            station.purpose = new[] { "Tree interaction space", "Pet encounter space", "Forest overlook" }[stations.Count]; stations.Add(station);
        }
        var scale = GameObject.CreatePrimitive(PrimitiveType.Capsule); scale.name = "Human scale reference 1.8 m";
        scale.transform.localScale = new Vector3(.6f, .9f, .6f);
        float sx = Center(-12) - 2.4f; scale.transform.position = new Vector3(sx, Height(sx, -12) + .9f, -12);
        scale.GetComponent<Renderer>().sharedMaterial = marker;
        var player = new GameObject("Desktop walker - 1.8 m body");
        player.transform.position = new Vector3(Center(-13), Height(Center(-13), -13) + .08f, -13);
        var body = player.AddComponent<CharacterController>(); body.height = 1.8f; body.radius = .3f; body.center = Vector3.up * .9f;
        body.stepOffset = .25f; body.slopeLimit = 40; body.skinWidth = .02f;
        var camera = new GameObject("Forest preview camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.transform.SetParent(player.transform, false); camera.transform.localPosition = Vector3.up * 1.65f;
        camera.nearClipPlane = .08f; camera.farClipPlane = 100; camera.fieldOfView = 65;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.49f, .55f, .59f);
        var walker = player.AddComponent<ForestPreviewWalker>(); walker.view = camera.transform; walker.stations = stations.ToArray();
        walker.route = Enumerable.Range(0, 53).Select(i => { float z = -13 + i * .5f; float x = Center(z); return new Vector3(x, Height(x,z), z); }).ToArray();
        var sun = new GameObject("Overcast key").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.1f;
        sun.transform.rotation = Quaternion.Euler(48, -30, 0); sun.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.53f, .57f, .6f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 16; RenderSettings.fogEndDistance = 55; RenderSettings.fogColor = camera.backgroundColor;
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
        ValidateAndRender();
    }
    public static void ValidateAndRender()
    {
        EditorSceneManager.OpenScene(ScenePath); Directory.CreateDirectory(Output); Physics.SyncTransforms();
        var walker = UnityEngine.Object.FindFirstObjectByType<ForestPreviewWalker>();
        if (!walker || walker.route.Length != 53 || walker.stations.Length != 3) throw new Exception("Forest wiring incomplete");
        walker.Body.enabled = false;
        int probes = 0; float maxSlope = 0;
        // Full body corridor checked along the path and both edges, not just a ray through its center.
        foreach (var p in walker.route) foreach (float offset in new[] { -1.3f, 0, 1.3f })
        {
            var q = p + Vector3.right * offset;
            if (!Physics.Raycast(q + Vector3.up * 4, Vector3.down, out var hit, 8)) throw new Exception("Route lacks ground");
            maxSlope = Mathf.Max(maxSlope, Vector3.Angle(hit.normal, Vector3.up));
            if (Physics.CheckCapsule(hit.point + Vector3.up * .34f, hit.point + Vector3.up * 1.5f, .29f)) throw new Exception("Route blocked at " + q);
            probes++;
        }
        if (maxSlope > 20) throw new Exception("Route slope exceeds blockout target");
        walker.Body.enabled = true;
        var camera = Camera.main; Capture(camera, "eye-level.png");
        var oldPos = camera.transform.position; var oldRot = camera.transform.rotation;
        bool fog = RenderSettings.fog;
        try { RenderSettings.fog = false; camera.transform.position = new Vector3(19, 24, -24); camera.transform.LookAt(new Vector3(0, 0, 0)); Capture(camera, "overview.png"); }
        finally { RenderSettings.fog = fog; }
        camera.transform.SetPositionAndRotation(oldPos, oldRot);
        File.WriteAllText(Output + "/geometry.json", JsonUtility.ToJson(new GeometryReport { corridorProbes = probes, maxGroundSlopeDegrees = maxSlope }, true));
        Debug.Log("FOREST_BLOCKOUT_GEOMETRY_PASS probes=" + probes + " slope=" + maxSlope + "; offline geometry/render only, Play Mode pending");
    }
    [Serializable] sealed class GeometryReport { public string scope = "ProBuilder blockout; capsule corridor and ground rays, not completed art or headset acceptance"; public int corridorProbes; public float maxGroundSlopeDegrees; public float bodyHeightMetres = 1.8f, eyeHeightMetres = 1.65f, designCorridorMetres = 3.2f; }
    public static void Capture(Camera camera, string name)
    {
        bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        var target = new RenderTexture(960, 640, 24); var pixels = new Texture2D(960, 640, TextureFormat.RGB24, false);
        var old = RenderTexture.active; var previous = camera.targetTexture;
        try { camera.targetTexture = target; camera.Render(); camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,960,640),0,0); pixels.Apply(); File.WriteAllBytes(Output + "/" + name, pixels.EncodeToPNG()); }
        finally { camera.targetTexture = previous; RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels); ShaderUtil.allowAsyncCompilation = async; }
    }
}
