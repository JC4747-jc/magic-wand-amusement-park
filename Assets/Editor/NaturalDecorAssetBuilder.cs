using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NaturalDecorAssetBuilder
{
    const string Folder="Assets/ThirdParty/NaturalDecor/Statue";
    public static void Run()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        string modelPath=Folder+"/MarbleBust.fbx";
        var importer=AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if(!importer)throw new Exception("Missing converted bust");
        importer.importAnimation=false;importer.isReadable=false;
        var shader=Shader.Find("Universal Render Pipeline/Lit");
        string materialPath=Folder+"/Marble.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(!material) {material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
        foreach(var suffix in new[]{"diff","nor_gl","arm"})
        {
            string path=Folder+"/marble_bust_01_"+suffix+".jpg";
            var textureImporter=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!textureImporter)throw new Exception("Missing bust PBR texture "+suffix);
            textureImporter.maxTextureSize=1024;textureImporter.sRGBTexture=suffix=="diff";
            if(suffix=="nor_gl")textureImporter.textureType=TextureImporterType.NormalMap;
            textureImporter.SaveAndReimport();
        }
        material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.24f);
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/marble_bust_01_diff.jpg"));
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/marble_bust_01_nor_gl.jpg"));
        material.SetFloat("_BumpScale",.65f);material.EnableKeyword("_NORMALMAP");
        // glTF ARM has occlusion in R, roughness in G. It is not a URP metallic/smoothness map.
        // Preserve the original packed map without binding its incompatible channels.
        EditorUtility.SetDirty(material);
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"marble_bust_01"),material);
        importer.SaveAndReimport();
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=new GameObject("Marble bust - CC0 Poly Haven");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);model.transform.SetParent(root.transform,false);
            // Keep the FBX axis-conversion rotation when correcting its heading.
            model.transform.localRotation=Quaternion.Euler(0,180,0)*model.transform.localRotation;
            var renderers=model.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0 || renderers.Any(r=>r.sharedMaterials.Any(m=>m!=material)))throw new Exception("Bust material mapping failed");
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            float scale=.55f/bounds.size.y;model.transform.localScale*=scale;
            model.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
            int triangles=model.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
            if(triangles>20000)throw new Exception("Bust triangle budget exceeded");
            Directory.CreateDirectory("Assets/Resources/MagicMR/NaturalModels");AssetDatabase.Refresh();
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/MagicMR/NaturalModels/MarbleBust.prefab");
            Debug.Log("NATURAL_STATUE_IMPORT_PASS triangles="+triangles+"; 1K texture limit; CC0 source recorded");
        }
        finally {EditorSceneManager.ClosePreviewScene(scene);}
        AssetDatabase.SaveAssets();
    }
}
