using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RoomFurnitureAssetBuilder
{
    [Serializable] class MaterialInfo{public string name,albedo,normal,packed;}
    [Serializable] class Conversion{public MaterialInfo[] materials;public int triangles;}
    public static void Run()
    {
        NaturalRoomMaterialBuilder.Run();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(var entry in new[]{("ReadingChair",.37f),("CeramicVase",.095f)})Build(entry.Item1,entry.Item2);
        AssetDatabase.SaveAssets();
    }
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Build(string name,float height)
    {
        string folder="Assets/ThirdParty/NaturalDecor/"+name,path=folder+"/"+name+".fbx";var conversion=JsonUtility.FromJson<Conversion>(File.ReadAllText(folder+"/source-and-conversion.json"));
        var importer=AssetImporter.GetAtPath(path) as ModelImporter;Require(importer,"Missing verified room prop "+name);
        var shader=Shader.Find("Universal Render Pipeline/Lit");
        // Texture reimports may reload dependent material/model assets. Finish
        // them before editing material objects or accumulating importer remaps.
        foreach(var entry in conversion.materials)foreach(var image in new[]{entry.albedo,entry.normal,entry.packed})
        {
                var textureImporter=(TextureImporter)AssetImporter.GetAtPath(folder+"/"+image);Require(textureImporter,"Missing prop map "+image);
                textureImporter.maxTextureSize=1024;textureImporter.sRGBTexture=image==entry.albedo;textureImporter.textureType=image==entry.normal?TextureImporterType.NormalMap:TextureImporterType.Default;textureImporter.SaveAndReimport();
        }
        foreach(var entry in conversion.materials)
        {
            string materialPath=folder+"/"+entry.name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
            material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/"+entry.albedo));
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/"+entry.normal));material.SetFloat("_BumpScale",.8f);material.EnableKeyword("_NORMALMAP");
            var packed=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/"+entry.packed);material.SetTexture("_MetallicGlossMap",packed);material.SetTexture("_OcclusionMap",packed);
            material.SetFloat("_Metallic",1);material.SetFloat("_Smoothness",1);material.SetFloat("_OcclusionStrength",1);material.EnableKeyword("_METALLICSPECGLOSSMAP");material.EnableKeyword("_OCCLUSIONMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.importAnimation=false;importer.isReadable=true;
        foreach(var entry in conversion.materials)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),entry.name),AssetDatabase.LoadAssetAtPath<Material>(folder+"/"+entry.name+".mat"));
        importer.SaveAndReimport();var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=new GameObject(name+" - CC0 Poly Haven");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var alignment=new GameObject("Prop alignment").transform;alignment.SetParent(root.transform,false);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);model.transform.SetParent(alignment,false);
            var renderers=model.GetComponentsInChildren<Renderer>();Require(renderers.Length>0&&renderers.Length<=4,"Unexpected prop renderers");
            Bounds BoundsOf(){var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);return bounds;}
            var bounds=BoundsOf();var marker=model.GetComponentsInChildren<Transform>().Single(t=>t.name=="FrontMarker");var heading=marker.position-bounds.center;heading.y=0;
            alignment.localRotation=Quaternion.AngleAxis(Vector3.SignedAngle(heading,Vector3.back,Vector3.up),Vector3.up);bounds=BoundsOf();float scale=height/bounds.size.y;
            alignment.localScale=Vector3.one*scale;alignment.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
            int triangles=model.GetComponentsInChildren<MeshFilter>().Sum(m=>m.sharedMesh.triangles.Length/3);Require(triangles==conversion.triangles,"Source prop topology changed");
            Require(renderers.All(r=>r.sharedMaterials.All(m=>m&&m.shader==shader&&m.GetTexture("_BaseMap")&&m.GetTexture("_BumpMap")&&m.GetTexture("_MetallicGlossMap"))),"Prop PBR remapping failed: "+string.Join(";",renderers.SelectMany(r=>r.sharedMaterials).Select(m=>m?m.name+" shader="+m.shader.name+" base="+m.GetTexture("_BaseMap")+" normal="+m.GetTexture("_BumpMap")+" packed="+m.GetTexture("_MetallicGlossMap"):"null")));
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/MagicMR/NaturalModels/"+name+".prefab");Debug.Log("ROOM_PROP_IMPORT_PASS "+name+" triangles="+triangles+" renderers="+renderers.Length+" height="+height+" bounds="+BoundsOf());
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
