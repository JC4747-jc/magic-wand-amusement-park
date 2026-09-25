using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Authored meshes are saved in the existing prefab: no runtime mesh generation.
public static class FlowerModelBuilder
{
    const string Folder="Assets/MagicMR/FlowerGeometry";
    const string Prefab="Assets/MagicMR/Prefabs/Flower.prefab";
    public static void PreviewAndBuild() { Preview(); BridgeStereoBuild.Build(); }
    public static void Preview()
    {
        Generate();
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        var flower=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
        MagicMR.RealityEditor.FitFlowerHeight(flower.transform,.11f);
        MagicMR.RealityEditor.CoverLighterWithStem(flower.transform,new Vector3(.025f,.08f,.012f));
        MagicMR.RealityEditor.AlignFlowerBase(flower.transform,Vector3.zero);
        var light=new GameObject("Preview light").AddComponent<Light>();
        light.type=LightType.Directional; light.intensity=1.2f;
        light.transform.rotation=Quaternion.Euler(40,-35,0);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.5f,.5f,.5f);
        var camera=new GameObject("Preview camera").AddComponent<Camera>();
        camera.transform.position=new Vector3(.075f,.11f,-.22f);
        camera.transform.LookAt(Vector3.up*.065f);
        camera.orthographic=true; camera.orthographicSize=.115f;
        camera.nearClipPlane=.005f; camera.farClipPlane=2;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.23f,.24f,.26f);
        var rt=new RenderTexture(900,1000,24); camera.targetTexture=rt;
        var previous=RenderTexture.active;
        try
        {
            camera.Render(); RenderTexture.active=rt;
            var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); texture.Apply();
            Directory.CreateDirectory(".codex-tmp");
            File.WriteAllBytes(".codex-tmp/compact-flower-preview.png",texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
        finally { camera.targetTexture=null; RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(rt); }
        StereoIntegrationChecks.Run();
        Debug.Log("COMPACT_FLOWER_PREVIEW_PASS");
    }
    sealed class Geometry
    {
        public readonly List<Vector3> vertices=new();
        public readonly List<Color> colors=new();
        public readonly List<int> triangles=new();
        public void Grid(int rows,int columns,Func<float,float,Vector3> point,Func<float,float,Color> color)
        {
            int start=vertices.Count;
            for(int y=0;y<=rows;y++) for(int x=0;x<=columns;x++)
            { float t=(float)y/rows,w=(float)x/columns; vertices.Add(point(t,w)); colors.Add(color(t,w).linear); }
            for(int y=0;y<rows;y++) for(int x=0;x<columns;x++)
            {
                int a=start+y*(columns+1)+x,b=a+columns+1;
                triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});
            }
        }
        public Mesh Save(string name)
        {
            foreach(var v in vertices) if(!float.IsFinite(v.sqrMagnitude)) throw new Exception("Nonfinite flower geometry: "+name);
            string path=$"{Folder}/{name}.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null) { mesh=new Mesh(); AssetDatabase.CreateAsset(mesh,path); }
            mesh.Clear(); mesh.name=name; mesh.SetVertices(vertices); mesh.SetColors(colors);
            mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh); return mesh;
        }
    }
    static Color Pink(float t,float w)
    {
        float rim=Mathf.Pow(Mathf.Abs(w*2-1),5);
        return Color.Lerp(new Color(.67f,.09f,.18f),new Color(1f,.63f,.49f),Mathf.Clamp01(t*.85f+rim*.35f));
    }
    static void Part(Transform parent,string name,Geometry geometry,Material material)
    {
        var go=new GameObject(name); go.transform.SetParent(parent,false);
        go.AddComponent<MeshFilter>().sharedMesh=geometry.Save(name);
        go.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    [MenuItem("Bridge/Rebuild Compact Flower")]
    public static void Generate()
    {
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var shader=Shader.Find("MagicMR/FlowerVertexLit");
        if(shader==null) throw new Exception("Flower shader unavailable");
        string matPath=Folder+"/FlowerGradient.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(material==null) { material=new Material(shader); AssetDatabase.CreateAsset(material,matPath); }
        material.shader=shader; EditorUtility.SetDirty(material);
        // Edit the original root to retain its fileID and all scene references.
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            while(root.transform.childCount>0) UnityEngine.Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
            root.transform.localScale=Vector3.one;
            var stem=new Geometry();
            stem.Grid(24,32,(t,w)=>new Vector3(.009f*Mathf.Cos(w*Mathf.PI*2),t*.061f,.009f*Mathf.Sin(w*Mathf.PI*2)),
                (t,w)=>Color.Lerp(new Color(.10f,.24f,.055f),new Color(.28f,.43f,.09f),.35f+.25f*Mathf.Cos(w*Mathf.PI*12)+t*.15f));
            foreach(float capY in new[]{0f,.061f}) stem.Grid(1,32,(t,w)=>new Vector3(t*.009f*Mathf.Cos(w*Mathf.PI*2),capY,t*.009f*Mathf.Sin(w*Mathf.PI*2)),(t,w)=>new Color(.17f,.32f,.065f));
            Part(root.transform,"Stem",stem,material);
            var leaves=new Geometry();
            for(int leaf=0;leaf<3;leaf++)
            {
                float y=.014f+leaf*.014f; float angle=leaf*145f+20;
                var rotation=Quaternion.Euler(0,angle,0);
                leaves.Grid(16,10,(t,w)=>new Vector3(0,y,0)+rotation*new Vector3(.0085f+t*.025f,.009f*Mathf.Sin(t*Mathf.PI*.8f)+.003f*(1-Mathf.Abs(w*2-1))*Mathf.Sin(t*Mathf.PI),
                    (w*2-1)*.008f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.8f)),
                    (t,w)=>Color.Lerp(new Color(.09f,.27f,.055f),new Color(.43f,.58f,.13f),.3f*t+.55f*Mathf.Pow(1-Mathf.Abs(w*2-1),12)));
            }
            Part(root.transform,"Leaves",leaves,material);
            var bloom=new GameObject("Blossom").transform; bloom.SetParent(root.transform,false);
            bloom.localPosition=Vector3.up*.061f; bloom.localRotation=Quaternion.Euler(-18,0,0);
            bloom.localScale=Vector3.one*1.6f;
            var petals=new Geometry();
            for(int layer=0;layer<3;layer++) for(int petal=0;petal<8;petal++)
            {
                int ring=layer;
                float angle=petal*45+layer*21;
                var rotation=Quaternion.Euler(0,angle,0);
                float length=.032f-layer*.006f,width=.012f-layer*.0016f;
                petals.Grid(24,12,(t,w)=>{
                    float side=w*2-1;
                    float spread=width*Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.7f);
                    float height=.002f+ring*.003f+(.009f+ring*.003f)*t*t+.004f*side*side*Mathf.Sin(t*Mathf.PI)-.004f*Mathf.Sin(t*Mathf.PI);
                    return rotation*new Vector3(side*spread,height,.002f+t*length);
                },Pink);
            }
            Part(bloom,"LayeredPetals",petals,material);
            var center=new Geometry();
            center.Grid(8,12,(t,w)=>new Vector3(Mathf.Sin(t*Mathf.PI)*Mathf.Cos(w*Mathf.PI*2)*.004f,.0015f+Mathf.Cos(t*Mathf.PI)*.0025f,Mathf.Sin(t*Mathf.PI)*Mathf.Sin(w*Mathf.PI*2)*.004f),
                (t,w)=>new Color(.23f,.40f,.085f));
            for(int stamen=0;stamen<7;stamen++)
            {
                float a=stamen*Mathf.PI*2/7;
                Vector3 origin=new Vector3(Mathf.Cos(a)*.004f,.013f,Mathf.Sin(a)*.004f);
                center.Grid(6,8,(t,w)=>origin+new Vector3(Mathf.Sin(t*Mathf.PI)*Mathf.Cos(w*Mathf.PI*2)*.002f,Mathf.Cos(t*Mathf.PI)*.003f,Mathf.Sin(t*Mathf.PI)*Mathf.Sin(w*Mathf.PI*2)*.002f),
                    (t,w)=>Color.Lerp(new Color(1f,.78f,.25f),new Color(.86f,.39f,.035f),t));
                center.Grid(2,6,(t,w)=>new Vector3(origin.x+Mathf.Cos(w*Mathf.PI*2)*.00055f,.004f+t*.009f,origin.z+Mathf.Sin(w*Mathf.PI*2)*.00055f),(t,w)=>new Color(.95f,.60f,.12f));
            }
            Part(bloom,"GoldenStamens",center,material);
            var basePoint=new GameObject("Base"); basePoint.transform.SetParent(root.transform,false);
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("COMPACT_FLOWER_GENERATED");
    }
}
