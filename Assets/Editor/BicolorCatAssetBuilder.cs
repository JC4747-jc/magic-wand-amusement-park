using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using MagicMR;

public static class BicolorCatAssetBuilder
{
    public const string Folder="Assets/ThirdParty/NaturalPets/BicolorCat";
    [Serializable] class MaterialInfo {public string name,albedo,normal;public float roughness;public int normalUV;}
    [Serializable] class Conversion {public MaterialInfo[] materials;}
    static void Require(bool ok,string message) {if(!ok)throw new Exception(message);}
    public static void Run()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        string modelPath=Folder+"/BicolorCat.fbx";
        var importer=AssetImporter.GetAtPath(modelPath) as ModelImporter;Require(importer,"Missing converted CC BY cat");
        importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;
        importer.animationCompression=ModelImporterAnimationCompression.Off;
        importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.optimizeGameObjects=false;
        importer.isReadable=true;
        var clips=importer.defaultClipAnimations;Require(clips.Length>0,"Original source animation was lost");
        foreach(var clip in clips) {clip.loopTime=!clip.name.EndsWith("BicolorCat_Feed",StringComparison.Ordinal);clip.lockRootRotation=true;clip.lockRootPositionXZ=true;clip.lockRootHeightY=true;clip.keepOriginalOrientation=true;clip.keepOriginalPositionXZ=true;clip.keepOriginalPositionY=true;}
        importer.clipAnimations=clips;importer.SaveAndReimport();
        var conversion=JsonUtility.FromJson<Conversion>(File.ReadAllText(Folder+"/source-and-conversion.json"));
        var shader=Shader.Find("Universal Render Pipeline/Lit");Require(shader,"URP shader missing");
        foreach(var entry in conversion.materials)
        {
            string path=Folder+"/"+entry.name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material) {material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",1-entry.roughness);
            material.SetFloat("_Cull",0); // The source glTF materials are double sided.
            foreach(var image in new[]{entry.albedo,entry.normal}.Where(p=>!string.IsNullOrEmpty(p)))
            {
                string texturePath=Folder+"/"+image;var textureImporter=AssetImporter.GetAtPath(texturePath) as TextureImporter;
                Require(textureImporter,"Missing original cat texture");textureImporter.maxTextureSize=1024;textureImporter.sRGBTexture=image==entry.albedo;
                if(image==entry.normal)textureImporter.textureType=TextureImporterType.NormalMap;
                textureImporter.SaveAndReimport();
            }
            if(!string.IsNullOrEmpty(entry.albedo))material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/"+entry.albedo));
            // URP/Lit samples its normal map on UV0. The source cat uses UV2;
            // applying that map on UV0 produces false dents and protruding eyes.
            material.SetTexture("_BumpMap",null);material.DisableKeyword("_NORMALMAP");
            if(!string.IsNullOrEmpty(entry.normal)&&entry.normalUV==0) {material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/"+entry.normal));material.EnableKeyword("_NORMALMAP");}
            EditorUtility.SetDirty(material);importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),entry.name),material);
        }
        importer.SaveAndReimport();
        var animation=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview")&&c.name.Contains("BicolorCat_Idle"));
        Require(animation&&animation.length>1,"Authored neutral idle animation is empty");
        string controllerPath=Folder+"/BicolorCat.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if(!controller) {controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);controller.layers[0].stateMachine.defaultState=controller.layers[0].stateMachine.AddState("Restrained neutral idle");}
        controller.layers[0].stateMachine.defaultState.name="Restrained neutral idle";
        controller.layers[0].stateMachine.defaultState.motion=animation;EditorUtility.SetDirty(controller);
        var machine=controller.layers[0].stateMachine;
        var idle=machine.defaultState;
        var feed=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="Brief sniff");
        if(!feed)feed=machine.AddState("Brief sniff");
        feed.motion=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview")&&c.name.Contains("BicolorCat_Feed"));
        if(!controller.parameters.Any(p=>p.name=="Sniff"))controller.AddParameter("Sniff",AnimatorControllerParameterType.Trigger);
        foreach(var state in new[]{idle,feed})foreach(var transition in state.transitions)state.RemoveTransition(transition);
        var enter=idle.AddTransition(feed);enter.hasExitTime=false;enter.duration=.08f;enter.AddCondition(AnimatorConditionMode.If,0,"Sniff");
        var leave=feed.AddTransition(idle);leave.hasExitTime=true;leave.exitTime=.95f;leave.duration=.08f;
        NaturalPetAnimatorBuilder.Configure(controller,idle,feed,AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview")&&c.name.EndsWith("BicolorCat_Walk",StringComparison.Ordinal)));
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=new GameObject("Bicolor Cat - kenchoo and guillaume bolis - CC BY 4.0");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var alignment=new GameObject("Model alignment").transform;alignment.SetParent(root.transform,false);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),scene);model.transform.SetParent(alignment,false);
            var marker=model.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="HeadMarker");Require(marker,"Heading marker missing");
            var renderers=model.GetComponentsInChildren<Renderer>();Require(renderers.Length>0&&renderers.Length<=4,"Unexpected helper or excessive mesh count");
            Bounds BoundsOf() {return NaturalDogAssetBuilder.Geometry(model);}
            var bounds=BoundsOf();var heading=marker.position-bounds.center;heading.y=0;Require(heading.sqrMagnitude>.00001f,"Invalid anatomical heading");
            alignment.localRotation=Quaternion.AngleAxis(Vector3.SignedAngle(heading,Vector3.back,Vector3.up),Vector3.up);bounds=BoundsOf();
            Require(float.IsFinite(bounds.size.y)&&bounds.size.y>.00001f,"Nonfinite cat bounds");
            float scale=.35f/bounds.size.y;alignment.localScale=Vector3.one*scale;
            alignment.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
            var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();Require(skins.Length>0&&skins.All(s=>s.bones.Length<=64),"Skinning budget exceeded");
            long triangles=0;foreach(var mesh in skins.Select(s=>s.sharedMesh).Concat(model.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh)))
                for(int i=0;i<mesh.subMeshCount;i++)triangles+=mesh.GetIndexCount(i)/3;
            Require(triangles<15000,"Cat triangle budget exceeded");
            Require(renderers.All(r=>r.sharedMaterials.All(m=>m&&m.shader==shader)),"Cat material remapping failed");
            var animator=model.GetComponent<Animator>();Require(animator,"Source animator missing");animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
            root.AddComponent<NaturalPetRig>().Initialize();
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"/BicolorCat.prefab");
            Debug.Log("BICOLOR_CAT_IMPORT_PASS triangles="+triangles+" skins="+skins.Length+" authored_idle_seconds="+animation.length+"; candidate requires visual review");
        }
        finally {EditorSceneManager.ClosePreviewScene(scene);}
        AssetDatabase.SaveAssets();
    }
    public static void Install()
    {
        Run();var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/BicolorCat.prefab"));
        try {PrefabUtility.SaveAsPrefabAsset(instance,"Assets/Resources/MagicMR/NaturalModels/BicolorCat.prefab");}
        finally {UnityEngine.Object.DestroyImmediate(instance);}
        AssetDatabase.SaveAssets();
    }
}
