#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Offline bounded conversion of the project's authored insect skin. No model
// download, training, runtime voxelization or per-frame mesh/object allocation.
public static class InsectPixelAssetBuilder
{
    const string Folder="Assets/Resources/MagicMR/NaturalModels/";
    const int MaximumCells=1500;
    sealed class Cell {public BoneWeight weight;public Color color;public float cost;}
    static readonly Vector3Int[] Directions={Vector3Int.right,Vector3Int.left,Vector3Int.up,Vector3Int.down,new Vector3Int(0,0,1),new Vector3Int(0,0,-1)};
    static readonly Vector3[] U={Vector3.up,Vector3.forward,Vector3.forward,Vector3.right,Vector3.right,Vector3.up};
    static readonly Vector3[] V={Vector3.forward,Vector3.up,Vector3.right,Vector3.forward,Vector3.up,Vector3.right};
    static bool Sample(Mesh source,float cellSize,out Dictionary<Vector3Int,Cell> cells)
    {
        cells=new Dictionary<Vector3Int,Cell>();var vertices=source.vertices;var triangles=source.triangles;var colors=source.colors;var weights=source.boneWeights;
        if(colors.Length!=vertices.Length||weights.Length!=vertices.Length)throw new Exception("Source insect must retain per-vertex colour and skin weights");
        int budget=0;
        for(int t=0;t<triangles.Length;t+=3)
        {
            int a=triangles[t],b=triangles[t+1],c=triangles[t+2];var av=vertices[a];var bv=vertices[b];var cv=vertices[c];
            int divisions=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(Vector3.Distance(av,bv),Mathf.Max(Vector3.Distance(bv,cv),Vector3.Distance(cv,av)))/cellSize*1.5f),1,96);
            for(int x=0;x<=divisions;x++)for(int y=0;y<=divisions-x;y++)
            {
                if(++budget>750000)throw new Exception("Voxel sampling budget exceeded");
                float wb=(float)x/divisions,wc=(float)y/divisions,wa=1-wb-wc;Vector3 p=av*wa+bv*wb+cv*wc;
                var key=new Vector3Int(Mathf.FloorToInt(p.x/cellSize),Mathf.FloorToInt(p.y/cellSize),Mathf.FloorToInt(p.z/cellSize));
                float cost=Vector3.SqrMagnitude(p-(new Vector3(key.x,key.y,key.z)+Vector3.one*.5f)*cellSize);
                if(cells.TryGetValue(key,out var old)&&old.cost<=cost)continue;
                int nearest=wa>=wb&&wa>=wc?a:wb>=wc?b:c;
                cells[key]=new Cell{weight=weights[nearest],color=colors[a]*wa+colors[b]*wb+colors[c]*wc,cost=cost};
                if(cells.Count>MaximumCells)return false;
            }
        }
        return true;
    }
    static Mesh Build(string species,out string measurements)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+species+".prefab");
        if(!prefab)throw new Exception("Missing authored insect prefab: "+species);
        var skin=prefab.GetComponentInChildren<SkinnedMeshRenderer>();var source=skin.sharedMesh;
        float span=Mathf.Max(source.bounds.size.x,Mathf.Max(source.bounds.size.y,source.bounds.size.z)),cellSize=0;
        Dictionary<Vector3Int,Cell> cells=null;
        foreach(int resolution in new[]{48,40,32,24,20,16,12})
        {
            cellSize=span/resolution;if(Sample(source,cellSize,out cells))break;cells=null;
        }
        if(cells==null||cells.Count<20)throw new Exception("Could not create bounded pixel silhouette: "+species);
        var positions=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();var uv=new List<Vector2>();var weights=new List<BoneWeight>();var indices=new List<int>();
        foreach(var pair in cells.OrderBy(p=>p.Key.x).ThenBy(p=>p.Key.y).ThenBy(p=>p.Key.z))
        {
            var center=(new Vector3(pair.Key.x,pair.Key.y,pair.Key.z)+Vector3.one*.5f)*cellSize;
            for(int face=0;face<6;face++)
            {
                if(cells.ContainsKey(pair.Key+Directions[face]))continue;
                var normal=(Vector3)Directions[face];var at=center+normal*cellSize*.5f;int start=positions.Count;
                for(int n=0;n<4;n++)
                {
                    float u=n==0||n==3?-.5f:.5f,v=n<2?-.5f:.5f;
                    positions.Add(at+(U[face]*u+V[face]*v)*cellSize);normals.Add(normal);colors.Add(pair.Value.color);uv.Add(new Vector2(u+.5f,v+.5f));weights.Add(pair.Value.weight);
                }
                indices.Add(start);indices.Add(start+1);indices.Add(start+2);indices.Add(start);indices.Add(start+2);indices.Add(start+3);
            }
        }
        if(positions.Count>MaximumCells*24||positions.Count>=65535)throw new Exception("Pixel vertex budget exceeded");
        var mesh=new Mesh{name="Pixel"+species};mesh.SetVertices(positions);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.boneWeights=weights.ToArray();mesh.bindposes=source.bindposes;mesh.SetTriangles(indices,0);
        mesh.RecalculateBounds();var bounds=mesh.bounds;bounds.Expand(cellSize*2);mesh.bounds=bounds;
        if(mesh.bindposes.Length!=skin.bones.Length||mesh.boneWeights.Length!=mesh.vertexCount)throw new Exception("Pixel skin topology mismatch");
        measurements=species+": sourceVertices="+source.vertexCount+"; cells="+cells.Count+"; cellSize="+cellSize.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"; pixelVertices="+mesh.vertexCount+"; triangles="+indices.Count/3+"; bones="+mesh.bindposes.Length;
        return mesh;
    }
    public static void Run()
    {
        string output=Path.GetFullPath(".codex-tmp/hci-bug-pixel-assets");Directory.CreateDirectory(output);File.WriteAllText(output+"/result.txt","RUNNING\n");
        try
        {
            if(!Application.isBatchMode)throw new Exception("Use the bounded isolated fixture");
            var names=new[]{"Cockroach","Spider"};foreach(var name in names)if(File.Exists(Folder+"Pixel"+name+".asset"))throw new Exception("Do not overwrite an existing pixel asset");
            var meshes=new List<Mesh>();var records=new List<string>();
            foreach(var name in names){meshes.Add(Build(name,out string description));records.Add(description);}
            for(int i=0;i<names.Length;i++)AssetDatabase.CreateAsset(meshes[i],Folder+"Pixel"+names[i]+".asset");
            AssetDatabase.SaveAssets();File.WriteAllLines(output+"/measurements.txt",records);
            File.WriteAllText(output+"/result.txt","PASS: two bounded skinned voxel meshes generated from authored insect geometry; original prefabs/FBX and bone bindings preserved; no scene save.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(output+"/result.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(2);}
    }
}
#endif
