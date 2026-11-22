using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NaturalPlantAssetBuilder
{
    const string Folder="Assets/ThirdParty/NaturalPlants/Haworthia";
    public static void Run()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        string path=Folder+"/Haworthia.fbx";var importer=AssetImporter.GetAtPath(path) as ModelImporter;
        if(!importer)throw new Exception("Converted Haworthia missing");
        importer.importAnimation=false;importer.animationType=ModelImporterAnimationType.Generic;
        importer.avatarSetup=ModelImporterAvatarSetup.NoAvatar;importer.optimizeGameObjects=false;
        string materialPath=Folder+"/Haworthia.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(!material) {material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,materialPath);}
        foreach(var suffix in new[]{"diff","nor_gl","arm"})
        {
            var textureImporter=AssetImporter.GetAtPath(Folder+"/"+suffix+".jpg") as TextureImporter;
            if(!textureImporter)throw new Exception("Original plant texture missing: "+suffix);
            textureImporter.maxTextureSize=1024;textureImporter.sRGBTexture=suffix=="diff";
            if(suffix=="nor_gl")textureImporter.textureType=TextureImporterType.NormalMap;
            textureImporter.SaveAndReimport();
        }
        material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.27f);
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/diff.jpg"));
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/nor_gl.jpg"));
        material.SetFloat("_BumpScale",.65f);material.EnableKeyword("_NORMALMAP");
        // Keep ARM original; its roughness/metal channels are not URP's packed layout.
        EditorUtility.SetDirty(material);importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"potted_plant_04"),material);importer.SaveAndReimport();
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=new GameObject("Natural Haworthia - James Ray Cock - CC0");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);model.transform.SetParent(root.transform,false);
            PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var renderers=model.GetComponentsInChildren<Renderer>();if(renderers.Length!=2||renderers.Any(r=>r.sharedMaterial!=material))throw new Exception("Plant container/foliage or material mapping invalid");
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            if(!float.IsFinite(bounds.size.y)||bounds.size.y<.00001f)throw new Exception("Invalid plant bounds");
            float scale=.30f/bounds.size.y;model.transform.localScale*=scale;model.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
            var rig=model.GetComponentsInChildren<Transform>().First(t=>t.name=="FoliageRig");
            var anchor=rig.GetComponentsInChildren<Transform>().First(t=>t.name=="PlantRoot");
            var growth=new GameObject("Growth").transform;growth.SetParent(root.transform,false);growth.position=anchor.position;
            rig.SetParent(growth,true); // Preserve the FBX's axis conversion beneath an upright animation pivot.
            var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();
            // FBX imports the skinned mesh beside its armature. Move both while preserving their bind-space transforms.
            if(skin&&!skin.transform.IsChildOf(growth))skin.transform.SetParent(growth,true);
            if(!skin||!skin.transform.IsChildOf(growth)||skin.bones.Length>64||skin.bones.Count(b=>b.name.StartsWith("Leaf_"))!=40)
                throw new Exception("Plant leaf skeleton/growth ownership invalid: "+(skin?string.Join(",",skin.bones.Select(b=>b?b.name:"null")):"no skin"));
            skin.quality=SkinQuality.Bone1;
            var animator=model.GetComponent<Animator>();if(animator)UnityEngine.Object.DestroyImmediate(animator);
            Directory.CreateDirectory("Assets/Resources/MagicMR/NaturalModels");AssetDatabase.Refresh();
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/MagicMR/NaturalModels/Haworthia.prefab");
            Debug.Log("NATURAL_PLANT_IMPORT_PASS: two renderers, 40 leaf pivots, 1K original textures; growth pivot isolated from container");
        }
        finally {EditorSceneManager.ClosePreviewScene(scene);}
        AssetDatabase.SaveAssets();
    }
    public static void RenderAndCheck() {Run();ParkNaturalModelChecks.Render();}
}
