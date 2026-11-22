using System;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NaturalBronzeBeastAssetBuilder
{
    public const string Folder="Assets/ThirdParty/NaturalDecor/BronzeBeast";
    public const string Prefab="Assets/Resources/MagicMR/NaturalModels/BronzeBeast.prefab";
    public static void Run()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        string modelPath=Folder+"/BronzeBeast.fbx";
        foreach(var entry in new[]{("bull_head_diff_1k.jpg",true,false),("bull_head_nor_gl_1k.jpg",false,true),("Bronze_MetalAO_Smooth.png",false,false)})
        {
            var texture=AssetImporter.GetAtPath(Folder+"/"+entry.Item1) as TextureImporter;
            if(!texture)throw new Exception("Missing original bronze map "+entry.Item1);
            texture.maxTextureSize=1024;texture.sRGBTexture=entry.Item2;texture.textureType=entry.Item3?TextureImporterType.NormalMap:TextureImporterType.Default;texture.SaveAndReimport();
        }
        var shader=Shader.Find("Universal Render Pipeline/Lit");string matPath=Folder+"/Bronze.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,matPath);}
        material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/bull_head_diff_1k.jpg"));
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/bull_head_nor_gl_1k.jpg"));material.SetFloat("_BumpScale",1);material.EnableKeyword("_NORMALMAP");
        var packed=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Bronze_MetalAO_Smooth.png");material.SetTexture("_MetallicGlossMap",packed);material.SetTexture("_OcclusionMap",packed);
        material.SetFloat("_Metallic",1);material.SetFloat("_Smoothness",1);material.SetFloat("_OcclusionStrength",1);material.EnableKeyword("_METALLICSPECGLOSSMAP");material.EnableKeyword("_OCCLUSIONMAP");material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
        var importer=AssetImporter.GetAtPath(modelPath) as ModelImporter;if(!importer)throw new Exception("Missing bronze conversion");
        importer.importAnimation=false;importer.isReadable=true;importer.animationType=ModelImporterAnimationType.Generic;
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"bull_head"),material);importer.SaveAndReimport();
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=new GameObject("Bronze bovine bust - CC0 Tina / Poly Haven");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var alignment=new GameObject("Bronze alignment").transform;alignment.SetParent(root.transform,false);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),scene);model.transform.SetParent(alignment,false);
            var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();if(skins.Length!=1||skins[0].bones.Length!=2)throw new Exception("Bronze skin/bone budget changed");
            var skin=skins[0];if(skin.sharedMesh.triangles.Length/3!=17148||skin.sharedMaterials.Any(m=>m!=material))throw new Exception("Bronze topology/material mapping changed");
            var bounds=skin.bounds;var marker=model.GetComponentsInChildren<Transform>().Single(t=>t.name=="FrontMarker");var heading=marker.position-bounds.center;heading.y=0;
            alignment.localRotation=Quaternion.AngleAxis(Vector3.SignedAngle(heading,Vector3.back,Vector3.up),Vector3.up);
            bounds=skin.bounds;float scale=.48f/bounds.size.y;alignment.localScale=Vector3.one*scale;alignment.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
            var localBounds=skin.localBounds;localBounds.Expand(.04f);skin.localBounds=localBounds;skin.updateWhenOffscreen=true;
            root.AddComponent<NaturalBronzeBeast>().Initialize();PrefabUtility.SaveAsPrefabAsset(root,Prefab);
            Debug.Log("NATURAL_BRONZE_IMPORT_PASS: 17148 original triangles, one skin/material, two authored bones, original 1K PBR, 0.48m nominal height");
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
        AssetDatabase.SaveAssets();
    }
}
