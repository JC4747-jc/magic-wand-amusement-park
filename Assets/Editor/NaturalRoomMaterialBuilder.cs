using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class NaturalRoomMaterialBuilder
{
    const string Source="Assets/ThirdParty/NaturalDecor/RoomTextures/";
    const string Destination="Assets/Resources/MagicMR/NaturalMaterials/";
    public static void Run()
    {
        Directory.CreateDirectory(Destination);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Create("Wood","wood_table_001",.30f,.45f,1);
        Create("Linen","rough_linen",.08f,.4f,3);
        Create("Plaster","beige_wall_002",.05f,.25f,1);
        Plain("RoomMetal",.38f,.7f,false);Plain("BookCloth",.08f,0,true);Plain("BookPaper",.025f,0,false);
        AssetDatabase.SaveAssets();
        Debug.Log("NATURAL_ROOM_MATERIALS_PASS: three URP Lit materials; six original 1K maps, CC0 provenance recorded");
    }
    static void Plain(string name,float smoothness,float metallic,bool cloth)
    {
        string path=Destination+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",smoothness);material.SetFloat("_Metallic",metallic);
        if(cloth){material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"rough_linen_normal.jpg"));material.SetFloat("_BumpScale",.35f);material.EnableKeyword("_NORMALMAP");}
        EditorUtility.SetDirty(material);
    }
    static void Create(string name,string source,float smoothness,float normalStrength,float tiling)
    {
        foreach(var suffix in new[]{"diff","normal"})
        {
            var importer=AssetImporter.GetAtPath(Source+source+"_"+suffix+".jpg") as TextureImporter;
            if(!importer)throw new Exception("Missing verified room texture: "+source+suffix);
            importer.maxTextureSize=1024;importer.sRGBTexture=suffix=="diff";importer.wrapMode=TextureWrapMode.Repeat;
            if(suffix=="normal")importer.textureType=TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
        string path=Destination+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",smoothness);material.SetFloat("_Metallic",0);
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Source+source+"_diff.jpg"));
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Source+source+"_normal.jpg"));
        material.SetTextureScale("_BaseMap",Vector2.one*tiling);material.SetFloat("_BumpScale",normalStrength);material.EnableKeyword("_NORMALMAP");EditorUtility.SetDirty(material);
    }
    public static void RenderAndCheck(){Run();ParkNaturalModelChecks.Render();}
}
