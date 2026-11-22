using System;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NaturalStandingStatueBuilder
{
    public const string Folder="Assets/ThirdParty/NaturalDecor/StandingStatue";
    public static void Run()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var path=Folder+"/StandingStatue.fbx";var importer=AssetImporter.GetAtPath(path) as ModelImporter;
        if(!importer)throw new Exception("Missing verified standing statue FBX");
        importer.useFileScale=true;importer.globalScale=1;importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;importer.isReadable=false;
        importer.animationCompression=ModelImporterAnimationCompression.Off;importer.SaveAndReimport();
        var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview__"));
        var matPath=Folder+"/StandingStone.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,matPath);}
        material.SetColor("_BaseColor",new Color(.56f,.51f,.42f));material.SetFloat("_Smoothness",.15f);EditorUtility.SetDirty(material);
        var scene=EditorSceneManager.NewPreviewScene();
        try{
            var root=new GameObject("Fictional standing sculpture - Quaternius CC0 base");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);model.transform.SetParent(root.transform,false);
            if(!model.GetComponent<Animator>())model.AddComponent<Animator>();model.GetComponent<Animator>().enabled=false;clip.SampleAnimation(model,0);var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();
            var box=new Bounds();bool first=true;var baked=new Mesh();
            foreach(var skin in skins){
                skin.sharedMaterials=Enumerable.Repeat(material,skin.sharedMaterials.Length).ToArray();skin.updateWhenOffscreen=true;
                skin.BakeMesh(baked,true);foreach(var v in baked.vertices){var p=skin.transform.TransformPoint(v);if(first){box=new Bounds(p,Vector3.zero);first=false;}else box.Encapsulate(p);}
            }
            UnityEngine.Object.DestroyImmediate(baked);
            if(box.size.y<1.5f||box.size.y>2.1f)throw new Exception("Unexpected source scale: "+box);
            float scale=.48f/box.size.y;model.transform.localScale*=scale;model.transform.localPosition=-new Vector3(box.center.x,box.min.y,box.center.z)*scale;
            var animator=model.GetComponent<Animator>();animator.enabled=true;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            root.AddComponent<NaturalStatueStretch>().Configure(animator,clip);
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/MagicMR/NaturalModels/StandingStatue.prefab");
            Debug.Log("STANDING_STATUE_BUILT: 0.48m neutral height; one bounded full-body stretch; no root motion");
        }finally{EditorSceneManager.ClosePreviewScene(scene);}
        AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
}
