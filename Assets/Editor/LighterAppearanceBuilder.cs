using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class LighterAppearanceBuilder
{
    const string Folder="Assets/MagicMR/LighterGeometry/";
    public static void UpdateMaterials()
    {
        var plastic=AssetDatabase.LoadAssetAtPath<Material>(Folder+"BlueGlossPlastic.mat");
        plastic.SetColor("_BaseColor",new Color(1f,.24f,.015f));
        plastic.SetColor("_Color",new Color(1f,.24f,.015f));
        EditorUtility.SetDirty(plastic);
        const int size=512, cells=12;
        var random=new System.Random(147);
        var points=new Vector2[cells*cells];
        for(int y=0;y<cells;y++) for(int x=0;x<cells;x++) points[y*cells+x]=new Vector2(x+.15f+(float)random.NextDouble()*.7f,y+.15f+(float)random.NextDouble()*.7f);
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
        var pixels=new Color[size*size];
        for(int y=0;y<size;y++) for(int x=0;x<size;x++)
        {
            var p=new Vector2((float)x/size*cells,(float)y/size*cells);
            p+=new Vector2(Mathf.PerlinNoise(p.x*2,p.y*2),Mathf.PerlinNoise(p.x*2+31,p.y*2+7))*.22f;
            int cx=Mathf.FloorToInt(p.x),cy=Mathf.FloorToInt(p.y); float first=100,second=100;
            for(int j=-1;j<=1;j++) for(int k=-1;k<=1;k++) {
                int gx=cx+k,gy=cy+j; int ix=(gx%cells+cells)%cells,iy=(gy%cells+cells)%cells;
                var point=points[iy*cells+ix]+new Vector2(gx-ix,gy-iy);
                float d=(p-point).sqrMagnitude; if(d<first){second=first;first=d;}else if(d<second)second=d;
            }
            float edge=Mathf.Sqrt(second)-Mathf.Sqrt(first);
            float plate=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.012f,.10f,edge));
            float grain=(float)random.NextDouble();
            float ash=Mathf.SmoothStep(.30f,.80f,Mathf.PerlinNoise(p.x*1.7f+17,p.y*1.7f+8));
            Color color=Color.Lerp(new Color(.065f,.05f,.038f),new Color(.37f,.34f,.29f),ash*.85f);
            color*=.7f+grain*.45f;
            color=Color.Lerp(new Color(.012f,.009f,.007f),color,plate);
            color.a=plate*(.45f+ash*.30f)+grain*.12f;
            pixels[y*size+x]=color;
        }
        texture.SetPixels(pixels); texture.Apply();
        string path=Folder+"CharredAsh.png"; File.WriteAllBytes(path,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.wrapMode=TextureWrapMode.Repeat; importer.alphaSource=TextureImporterAlphaSource.FromInput; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        var map=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Configure("Assets/Models/Materials/burnt.mat",map,0);
        Directory.CreateDirectory("Assets/Resources/MagicMR"); AssetDatabase.Refresh();
        Configure("Assets/Resources/MagicMR/BurntMetal.mat",map,1);
        AssetDatabase.SaveAssets();
    }
    static void Configure(string path,Texture2D map,float metal)
    {
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("MagicMR/CharredSurface"));AssetDatabase.CreateAsset(m,path);}
        m.shader=Shader.Find("MagicMR/CharredSurface");m.SetTexture("_CharMap",map);m.SetFloat("_Metal",metal);EditorUtility.SetDirty(m);
    }
    public static void Preview()
    {
        UpdateMaterials();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
        var light=new GameObject("Studio key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(35,-35,0);
        var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.19f);camera.orthographic=true;camera.orthographicSize=.048f;camera.nearClipPlane=.001f;camera.farClipPlane=10;
        camera.transform.position=new Vector3(.085f,.066f,.20f);camera.transform.LookAt(Vector3.up*.04f);
        var root=GameObject.CreatePrimitive(PrimitiveType.Cube);root.name="VR Lighter";root.transform.localScale=new Vector3(.025f,.08f,.012f);root.transform.position=Vector3.up*.04f;
        LighterModelBuilder.Apply(root);
        Directory.CreateDirectory(".codex-tmp/lighter-appearance");
        Capture(camera,"original-orange");
        root.SetActive(false);
        var editor=root.AddComponent<MagicMR.RealityEditor>();
        var so=new SerializedObject(editor);so.FindProperty("m_BurntMaterial").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Materials/burnt.mat");so.ApplyModifiedPropertiesWithoutUndo();
        // Exercise the real appearance transition and reset on the same meshes used in the game.
        root.SetActive(true);
        typeof(MagicMR.RealityEditor).GetMethod("Awake",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(editor,null);
        editor.TriggerAppearance();
        foreach(var ps in root.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        Capture(camera,"carbonized");
        var body=root.GetComponent<Renderer>();
        if(body.sharedMaterial.shader.name!="MagicMR/CharredSurface")throw new Exception("Carbonization failed");
        var head=root.transform.Find("RealisticDetails/WindShield").GetComponent<Renderer>();
        if(head.sharedMaterial.GetFloat("_Metal")!=1)throw new Exception("Metal finish missing");
        editor.ResetTarget();
        if(body.sharedMaterial.GetColor("_BaseColor").r<.9f)throw new Exception("Reset lost orange finish");
        Debug.Log("LIGHTER_APPEARANCE_AND_RESET_PASS");
        AssetDatabase.SaveAssets();
    }
    static void Capture(Camera camera,string name)
    {
        var rt=new RenderTexture(1000,1200,24);camera.targetTexture=rt;var old=RenderTexture.active;
        try {camera.Render();camera.Render();RenderTexture.active=rt;var t=new Texture2D(1000,1200,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1000,1200),0,0);t.Apply();File.WriteAllBytes(".codex-tmp/lighter-appearance/"+name+".png",t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);}
        finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(rt);}
    }
}
