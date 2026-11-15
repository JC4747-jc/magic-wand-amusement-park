using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using MagicMR;

public static class NaturalDogAssetBuilder
{
    public const string Folder="Assets/ThirdParty/NaturalPets/Wolfhound";
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    // Explicit world-space linear blend skinning avoids FBX's scale-100 BakeMesh ambiguity.
    public static Vector3[] Vertices(SkinnedMeshRenderer skin)
    {
        var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;
        var matrices=skin.bones.Select((bone,i)=>bone.localToWorldMatrix*mesh.bindposes[i]).ToArray();
        for(int i=0;i<vertices.Length;i++)
        {
            var w=weights[i];var p=matrices[w.boneIndex0].MultiplyPoint3x4(vertices[i])*w.weight0;
            if(w.weight1>0)p+=matrices[w.boneIndex1].MultiplyPoint3x4(vertices[i])*w.weight1;
            if(w.weight2>0)p+=matrices[w.boneIndex2].MultiplyPoint3x4(vertices[i])*w.weight2;
            if(w.weight3>0)p+=matrices[w.boneIndex3].MultiplyPoint3x4(vertices[i])*w.weight3;
            Require(float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z),"Nonfinite dog skin");vertices[i]=p;
        }
        return vertices;
    }
    public static Bounds Geometry(GameObject root)
    {
        var points=root.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(Vertices).ToArray();Require(points.Length>0,"Missing dog skin");
        var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);return bounds;
    }
    public static void Run()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        string path=Folder+"/Wolfhound.fbx";var importer=AssetImporter.GetAtPath(path) as ModelImporter;Require(importer,"Missing licensed dog source");
        importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
        importer.animationCompression=ModelImporterAnimationCompression.Off;importer.optimizeGameObjects=false;importer.isReadable=true;
        var clips=importer.defaultClipAnimations;Require(clips.Length==4,"Expected three source clips and authored sniff");
        foreach(var clip in clips){clip.loopTime=!clip.name.EndsWith("Wolfhound_Feed",StringComparison.Ordinal);clip.lockRootRotation=true;clip.lockRootPositionXZ=true;clip.lockRootHeightY=true;clip.keepOriginalOrientation=true;clip.keepOriginalPositionXZ=true;clip.keepOriginalPositionY=true;}
        importer.clipAnimations=clips;importer.SaveAndReimport();
        var textureImporter=(TextureImporter)AssetImporter.GetAtPath(Folder+"/Wolfhound_Albedo.png");textureImporter.maxTextureSize=256;textureImporter.sRGBTexture=true;textureImporter.SaveAndReimport();
        var shader=Shader.Find("Universal Render Pipeline/Lit");Require(shader,"Missing URP shader");
        var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Wolfhound.mat");if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,Folder+"/Wolfhound.mat");}
        material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.15f);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Wolfhound_Albedo.png"));
        var imported=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        foreach(var source in imported.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m).Select(m=>m.name).Distinct())importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),source),material);
        EditorUtility.SetDirty(material);importer.SaveAndReimport();
        var animations=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
        AnimationClip Clip(string name){var clip=animations.Single(c=>c.name.EndsWith(name,StringComparison.Ordinal));Require(clip.length>1&&clip.length<=20.01f,"Invalid dog clip "+name);return clip;}
        string controllerPath=Folder+"/Wolfhound.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine=controller.layers[0].stateMachine;
        AnimatorState State(string name,AnimationClip clip){var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==name);if(!state)state=machine.AddState(name);state.motion=clip;foreach(var t in state.transitions)state.RemoveTransition(t);return state;}
        var idle=State("Restrained neutral idle",Clip("Wolfhound_Idle"));var sniff=State("Brief sniff",Clip("Wolfhound_Feed"));
        State("Source idle look",Clip("Wolfhound_IdleLook"));machine.defaultState=idle;
        if(!controller.parameters.Any(p=>p.name=="Sniff"))controller.AddParameter("Sniff",AnimatorControllerParameterType.Trigger);
        var enter=idle.AddTransition(sniff);enter.hasExitTime=false;enter.duration=.08f;enter.AddCondition(AnimatorConditionMode.If,0,"Sniff");
        var leave=sniff.AddTransition(idle);leave.hasExitTime=true;leave.exitTime=.95f;leave.duration=.08f;EditorUtility.SetDirty(controller);
        NaturalPetAnimatorBuilder.Configure(controller,idle,sniff,Clip("Wolfhound_WalkSource"));
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=new GameObject("Wolfhound - Wildfire Games - CC BY-SA 3.0");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var alignment=new GameObject("Model alignment").transform;alignment.SetParent(root.transform,false);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);model.transform.SetParent(alignment,false);
            var head=model.GetComponentsInChildren<Transform>().Single(t=>t.name=="Head");var bounds=Geometry(model);var heading=head.position-bounds.center;heading.y=0;Require(heading.sqrMagnitude>.00001f,"Invalid dog heading");
            // Opposite forward vectors make FromToRotation choose an arbitrary
            // 180-degree axis, which can invert an upright quadruped. Yaw only.
            alignment.localRotation=Quaternion.AngleAxis(Vector3.SignedAngle(heading,Vector3.back,Vector3.up),Vector3.up);bounds=Geometry(model);
            float scale=.35f/bounds.size.y;alignment.localScale=Vector3.one*scale;alignment.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
            var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();Require(skins.Length==1&&skins[0].bones.Length<=47,"Unexpected dog render/bone count");
            long triangles=skins.Sum(s=>Enumerable.Range(0,s.sharedMesh.subMeshCount).Sum(i=>(long)s.sharedMesh.GetIndexCount(i)/3));Require(triangles==849,"Original dog topology changed");
            Require(skins.All(s=>s.sharedMaterials.All(m=>m==material)),"Dog original coat remapping failed");
            var animator=model.GetComponent<Animator>();Require(animator,"Missing dog animator");animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
            var rig=root.AddComponent<NaturalPetRig>();rig.headBoneName="Head";rig.Initialize();
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Wolfhound.prefab");
            Debug.Log("NATURAL_DOG_IMPORT_PASS triangles="+triangles+" bones="+skins[0].bones.Length+" clips="+animations.Length+" geometric_bounds="+Geometry(root)+"; visual review pending");
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
        AssetDatabase.SaveAssets();
    }
    public static void Install()
    {
        Run();var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Wolfhound.prefab"));
        try{PrefabUtility.SaveAsPrefabAsset(instance,"Assets/Resources/MagicMR/NaturalModels/Wolfhound.prefab");}
        finally{UnityEngine.Object.DestroyImmediate(instance);}
        AssetDatabase.SaveAssets();
    }
    public static void Diagnose()
    {
        Run();var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Wolfhound.prefab"));
        try
        {
            var animator=instance.GetComponentInChildren<Animator>();
            void Dump(string label){foreach(var t in instance.GetComponentsInChildren<Transform>().Where(t=>t.parent==instance.transform||t.name=="Head"||t.name=="Pelvis"||t.name.Contains("nominal")||t.name.Contains("woulf")||t.name.Contains("Foot")))Debug.Log("DOG_DIAG "+label+" "+t.name+" parent="+(t.parent?t.parent.name:"null")+" position="+t.position+" rotation="+t.eulerAngles+" local_rot="+t.localEulerAngles+" scale="+t.localScale);}
            Dump("bind");animator.Rebind();animator.Play("Restrained neutral idle",0,0);animator.Update(0);Dump("idle");
            var clip=animator.runtimeAnimatorController.animationClips.Single(c=>c.name.EndsWith("Wolfhound_Idle",StringComparison.Ordinal));
            foreach(var binding in AnimationUtility.GetCurveBindings(clip).Where(b=>b.path.Split('/').Length<3))Debug.Log("DOG_BINDING "+binding.path+" "+binding.propertyName);
        }
        finally{UnityEngine.Object.DestroyImmediate(instance);}
    }
}
