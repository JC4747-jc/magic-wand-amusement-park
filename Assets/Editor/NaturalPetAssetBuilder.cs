using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NaturalPetAssetBuilder
{
    const string Folder="Assets/ThirdParty/NaturalPets/Cat";
    const string Model=Folder+"/NaturalCat.fbx";
    [Serializable] class MaterialInfo {public string name,albedo;public float[] color;public float roughness;}
    [Serializable] class Conversion {public MaterialInfo[] materials;}
    static void Require(bool valid,string reason) {if(!valid)throw new Exception(reason);}
    public static void Run()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var importer=AssetImporter.GetAtPath(Model) as ModelImporter;Require(importer!=null,"Cat FBX importer missing");
        importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;
        importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.optimizeGameObjects=false;
        var clips=importer.defaultClipAnimations;
        foreach(var clip in clips)clip.loopTime=true;
        importer.clipAnimations=clips;importer.SaveAndReimport();
        var conversion=JsonUtility.FromJson<Conversion>(File.ReadAllText(Folder+"/source-and-conversion.json"));
        var shader=Shader.Find("Universal Render Pipeline/Lit");Require(shader,"URP Lit shader missing");
        for(int i=0;i<conversion.materials.Length;i++)
        {
            var entry=conversion.materials[i];string path=Folder+"/CatMaterial"+i+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material) {material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",new Color(entry.color[0],entry.color[1],entry.color[2],entry.color[3]));
            material.SetFloat("_Smoothness",1-entry.roughness);
            if(!string.IsNullOrEmpty(entry.albedo))
            {
                string texturePath=Folder+"/"+entry.albedo;var textureImporter=AssetImporter.GetAtPath(texturePath) as TextureImporter;
                Require(textureImporter!=null,"Albedo image missing");textureImporter.maxTextureSize=1024;textureImporter.sRGBTexture=true;textureImporter.SaveAndReimport();
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            }
            EditorUtility.SetDirty(material);importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),entry.name),material);
        }
        importer.SaveAndReimport();
        var animations=AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
        var idle=animations.FirstOrDefault(c=>c.name.Contains("Idle"));var walk=animations.FirstOrDefault(c=>c.name.Contains("Walk"));
        Require(idle&&walk,"Expected authored idle and walk clips");
        string controllerPath=Folder+"/NaturalCat.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if(!controller)
        {
            controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);controller.AddParameter("Alert",AnimatorControllerParameterType.Bool);
            var machine=controller.layers[0].stateMachine;var rest=machine.AddState("Idle");rest.motion=idle;machine.defaultState=rest;
            var moving=machine.AddState("In-place walk");moving.motion=walk;
            var enter=rest.AddTransition(moving);enter.hasExitTime=false;enter.duration=.25f;enter.AddCondition(AnimatorConditionMode.If,0,"Alert");
            var exit=moving.AddTransition(rest);exit.hasExitTime=false;exit.duration=.25f;exit.AddCondition(AnimatorConditionMode.IfNot,0,"Alert");
        }
        var preview=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=new GameObject("NaturalCat");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,preview);
            var alignment=new GameObject("Model alignment");alignment.transform.SetParent(root.transform,false);
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Model);Require(source,"Cat model missing");
            var model=(GameObject)PrefabUtility.InstantiatePrefab(source,preview);model.transform.SetParent(alignment.transform,false);
            var marker=model.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="HeadMarker");Require(marker,"Orientation marker missing");
            Bounds bounds=BoundsOf(model);var heading=marker.position-bounds.center;heading.y=0;
            Require(heading.sqrMagnitude>.001f,"Invalid model orientation");alignment.transform.rotation=Quaternion.FromToRotation(heading.normalized,Vector3.back);
            bounds=BoundsOf(model);float scale=.35f/bounds.size.y;alignment.transform.localScale=Vector3.one*scale;
            alignment.transform.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
            var animator=model.GetComponent<Animator>();Require(animator,"Generic animator missing");animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
            var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();Require(skin&&skin.bones.Length>=10,"Authored skeleton missing");skin.updateWhenOffscreen=true;
            Require(skin.sharedMesh.triangles.Length/3<35000,"Cat candidate exceeds mobile triangle budget");
            Require(skin.sharedMaterials.All(m=>m&&m.shader==shader),"Material remap incomplete");
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"/NaturalCat.prefab");
        }
        finally {EditorSceneManager.ClosePreviewScene(preview);}
        AssetDatabase.SaveAssets();
        Render(idle,walk);
    }
    static Bounds BoundsOf(GameObject root)
    {
        var renderers=root.GetComponentsInChildren<Renderer>();Require(renderers.Length>0,"Empty model");var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;
    }
    static void Render(AnimationClip idle,AnimationClip walk)
    {
        string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        string output=Path.GetFullPath(".codex-tmp/natural-cat-candidate");Directory.CreateDirectory(output);
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var key=new GameObject("Key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.7f;key.transform.rotation=Quaternion.Euler(35,-30,0);
            var camera=new GameObject("Review camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.25f,.27f,.29f);camera.orthographic=true;camera.orthographicSize=.30f;camera.nearClipPlane=.01f;
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/NaturalCat.prefab"));
            var animator=root.GetComponentInChildren<Animator>();animator.enabled=false;
            var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();
            var snapshot=new GameObject("CPU skinned review snapshot");snapshot.transform.rotation=skin.transform.rotation;
            var filter=snapshot.AddComponent<MeshFilter>();var display=snapshot.AddComponent<MeshRenderer>();display.sharedMaterials=skin.sharedMaterials;
            var baked=new Mesh();filter.sharedMesh=baked;
            var motionReport=new System.Collections.Generic.List<string>();
            Vector3[] firstPose=null;
            camera.transform.position=new Vector3(.6f,.32f,-.85f);camera.transform.LookAt(new Vector3(0,.17f,0));
            foreach(var clip in new[]{idle,walk})for(int i=0;i<3;i++)
            {
                clip.SampleAnimation(animator.gameObject,i*.5f);Require(root.transform.position==Vector3.zero,"Animation moved tracking root");
                skin.BakeMesh(baked);baked.RecalculateBounds();skin.enabled=false;display.ResetBounds();
                if(clip==idle && i==0)
                {
                    var visible=display.bounds;Require(float.IsFinite(visible.size.y)&&visible.size.y>.00001f,"Invalid skinned snapshot bounds");
                    // BakeMesh has already evaluated skinning/bind-pose scale.
                    // Fit its actual geometry once, rather than applying FBX scale twice.
                    snapshot.transform.localScale=Vector3.one*(.35f/visible.size.y);
                    visible=display.bounds;snapshot.transform.position-=new Vector3(visible.center.x,visible.min.y,visible.center.z);
                    visible=display.bounds;
                    camera.orthographicSize=Mathf.Max(visible.extents.y,visible.extents.z)*1.4f;
                    camera.transform.position=visible.center+new Vector3(.6f,.32f,-.85f);camera.transform.LookAt(visible.center);
                }
                var vertices=baked.vertices;
                if(i==0)firstPose=vertices;
                else Require(vertices.Where((v,index)=>(v-firstPose[index]).sqrMagnitude>1e-10f).Any(),"Animation did not deform mesh: "+clip.name);
                motionReport.Add(clip.name+" time="+(i*.5f)+" bindings="+AnimationUtility.GetCurveBindings(clip).Length+" bone="+skin.bones[2].localRotation.ToString("F5")+" vertex="+baked.vertices[0].ToString("F5"));
                var target=new RenderTexture(1100,900,24);var pixels=new Texture2D(1100,900,TextureFormat.RGB24,false);var old=RenderTexture.active;
                try {camera.targetTexture=target;camera.Render();camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1100,900),0,0);pixels.Apply();var colors=pixels.GetPixels32();var background=colors[0];Require(colors.Count(p=>Math.Abs(p.r-background.r)+Math.Abs(p.g-background.g)+Math.Abs(p.b-background.b)>20)>colors.Length/100,"Blank candidate render");File.WriteAllBytes(Path.Combine(output,(clip==idle?"Idle":"Walk")+i+"-EDITOR.png"),pixels.EncodeToPNG());}
                finally {camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
            }
            File.WriteAllLines(Path.Combine(output,"animation-samples.txt"),motionReport);
            UnityEngine.Object.DestroyImmediate(baked);
            Debug.Log("NATURAL_CAT_CANDIDATE_RENDERED: CC0 mesh, URP materials, generic skeleton, six authored animation samples; not headset validation");
        }
        finally {EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
}
