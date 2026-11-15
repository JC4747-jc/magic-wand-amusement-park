using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class NaturalTreeAssetBuilder
{
    const string Folder="Assets/ThirdParty/NaturalTrees/SmallTree/";
    public static void ValidateWithTimeline(){RenderCandidate();ProjectSceneAudit.RunWithTimeline();}
    public static void Run()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var importer=AssetImporter.GetAtPath(Folder+"SmallTree.fbx") as ModelImporter;if(!importer)throw new Exception("Converted tree missing");
        importer.importAnimation=false;importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.NoAvatar;importer.optimizeGameObjects=false;
        foreach(var kind in new[]{"Trunk","Branches","Leaves"})
        {
            string prefix=kind=="Trunk"?"":kind=="Branches"?"branch_":"leaves_";
            foreach(var suffix in new[]{"diff","nor_gl"})
            {
                var texture=AssetImporter.GetAtPath(Folder+prefix+suffix+".jpg") as TextureImporter;if(!texture)throw new Exception("Original tree map missing");
                texture.maxTextureSize=1024;texture.sRGBTexture=suffix=="diff";if(suffix=="nor_gl")texture.textureType=TextureImporterType.NormalMap;texture.SaveAndReimport();
            }
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+kind+".mat");
            var shader=Shader.Find(kind=="Leaves"?"MagicMR/NaturalTreeLeaves":"Universal Render Pipeline/Lit");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Tree shader failed: "+kind);
            if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,Folder+kind+".mat");}material.shader=shader;
            material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+prefix+"diff.jpg"));
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+prefix+"nor_gl.jpg"));material.SetFloat("_BumpScale",.5f);
            if(kind!="Leaves"){material.SetFloat("_Smoothness",.12f);material.SetFloat("_Cull",0);material.EnableKeyword("_NORMALMAP");}
            else
            {
                var alpha=AssetImporter.GetAtPath(Folder+"leaves_alpha.jpg") as TextureImporter;if(!alpha)throw new Exception("Original leaf silhouette missing");
                alpha.maxTextureSize=1024;alpha.sRGBTexture=false;alpha.SaveAndReimport();
                material.SetTexture("_AlphaMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"leaves_alpha.jpg"));material.SetFloat("_Cutoff",.4f);
            }
            EditorUtility.SetDirty(material);
            string source="tree_small_02_"+(kind=="Branches"?"branches":kind.ToLowerInvariant());
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),source),material);
        }
        importer.SaveAndReimport();
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=new GameObject("Natural small tree - Rico Cilliers - CC0");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var growth=new GameObject("TreeGrowth").transform;growth.SetParent(root.transform,false);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"SmallTree.fbx"),scene);model.transform.SetParent(growth,false);
            PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var renderers=model.GetComponentsInChildren<Renderer>();if(renderers.Length!=3)throw new Exception("Tree must have three material renderers");
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            float scale=.8f/bounds.size.y;model.transform.localScale*=scale;model.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
            var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();if(skins.Length!=2||skins.Any(s=>s.bones.Length>6))throw new Exception("Tree skin budget failed");
            foreach(var s in skins)
            {
                var mesh=s.sharedMesh;var uv=mesh.uv;var normals=mesh.normals;var tangents=mesh.tangents;
                Debug.Log("TREE_MESH_DIAGNOSTIC "+s.name+" verts="+mesh.vertexCount+" UV="+uv.Length+" bounds="+(uv.Length>0?uv.Min(p=>p.x)+","+uv.Max(p=>p.x)+","+uv.Min(p=>p.y)+","+uv.Max(p=>p.y):"none")+" badNormals="+normals.Count(n=>n.sqrMagnitude<.5f)+" tangents="+tangents.Length+" badTangents="+tangents.Count(t=>new Vector3(t.x,t.y,t.z).sqrMagnitude<.5f));
            }
            foreach(var skin in skins){skin.quality=SkinQuality.Bone4;var local=skin.localBounds;local.Expand(local.size*.15f);skin.localBounds=local;}
            int triangles=model.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3)+skins.Sum(s=>s.sharedMesh.triangles.Length/3);
            if(triangles>90000||skins.Sum(s=>s.sharedMesh.vertexCount)>160000)throw new Exception("Tree geometry/vertex budget failed");
            var animator=model.GetComponent<Animator>();if(animator)UnityEngine.Object.DestroyImmediate(animator);
            Directory.CreateDirectory("Assets/Resources/MagicMR/NaturalModels");AssetDatabase.Refresh();
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/MagicMR/NaturalModels/SmallTree.prefab");
            Debug.Log("NATURAL_TREE_IMPORT_PASS triangles="+triangles+"; three renderers, six bones, 1K maps");
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}AssetDatabase.SaveAssets();NaturalTreeMarkAssetBuilder.Run();
    }
    public static void RenderCandidate()
    {
        bool async=ShaderUtil.allowAsyncCompilation;string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        try
        {
            ShaderUtil.allowAsyncCompilation=false;Run();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.45f,.47f,.5f);
            var key=new GameObject("Studio key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.4f;key.transform.rotation=Quaternion.Euler(38,-35,0);
            var camera=new GameObject("Tree review").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.20f,.22f,.24f);camera.nearClipPlane=.01f;camera.orthographic=true;camera.orthographicSize=.6f;
            camera.transform.position=new Vector3(.8f,.7f,-1.4f);camera.transform.LookAt(new Vector3(0,.4f,0));
            var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/MagicMR/NaturalModels/SmallTree.prefab"));
            var target=new RenderTexture(1000,1000,24);var pixels=new Texture2D(1000,1000,TextureFormat.RGB24,false);var old=RenderTexture.active;
            try
            {
                camera.targetTexture=target;Directory.CreateDirectory(".codex-tmp/natural-tree-review");
                var leaves=instance.GetComponentsInChildren<Renderer>().Single(r=>r.name=="Leaves");var original=leaves.sharedMaterial;
                foreach(var mode in new[]{"Tree","Autumn","Winter"})
                {
                    Material control=new Material(original);
                    try
                    {
                        control.SetFloat("_SeasonMix",mode=="Tree"?0:.92f);control.SetFloat("_LeafCoverage",mode=="Tree"?1:mode=="Autumn"?.65f:.025f);
                        control.SetColor("_SeasonTint",new Color(.95f,.48f,.08f));leaves.sharedMaterial=control;
                        // Discard initialization frames; a first synchronous Camera.Render can still use stale lighting/skinning buffers.
                        camera.Render();camera.Render();
                        camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1000,1000),0,0);pixels.Apply();File.WriteAllBytes(".codex-tmp/natural-tree-review/"+mode+".png",pixels.EncodeToPNG());
                    }
                    finally{leaves.sharedMaterial=original;if(control)UnityEngine.Object.DestroyImmediate(control);}
                }
            }
            finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
            Debug.Log("NATURAL_TREE_CANDIDATE_RENDERED: visual review required");
        }
        finally{ShaderUtil.allowAsyncCompilation=async;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
}
