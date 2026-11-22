using System;
using System.Collections.Generic;
using System.IO;
using MagicMR;
using UnityEditor;
using UnityEngine;

public static class NaturalJellyfishAssetBuilder
{
    public const string Folder="Assets/MagicMR/NaturalJellyfish";
    public const string Prefab="Assets/Resources/MagicMR/NaturalModels/Jellyfish.prefab";
    sealed class Geometry
    {
        public readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Vector2> uv=new List<Vector2>(),anatomy=new List<Vector2>();
        readonly List<Color> colors=new List<Color>();
        readonly List<int> triangles=new List<int>();
        public int Add(Vector3 p,Vector2 tex,int role,float along,Color colour){int n=vertices.Count;vertices.Add(p);uv.Add(tex);anatomy.Add(new Vector2(role,along));colors.Add(colour);return n;}
        public void Quad(int a,int b,int c,int d){triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(a);triangles.Add(c);triangles.Add(d);}
        public void Triangle(int a,int b,int c){triangles.Add(a);triangles.Add(b);triangles.Add(c);}
        public void Tube(Func<float,Vector3> center,Func<float,float> radius,int steps,int sides,int role,float azimuth,Color colour)
        {
            int start=vertices.Count;
            for(int row=0;row<=steps;row++)
            {
                float t=row/(float)steps;var p=center(t);var direction=(center(Mathf.Min(1,t+.001f))-center(Mathf.Max(0,t-.001f))).normalized;
                var axis=Vector3.Cross(direction,Mathf.Abs(direction.y)>.9f?Vector3.right:Vector3.up).normalized;var other=Vector3.Cross(direction,axis).normalized;
                for(int side=0;side<=sides;side++){float a=side*Mathf.PI*2/sides;Add(p+(axis*Mathf.Cos(a)+other*Mathf.Sin(a))*radius(t),new Vector2(azimuth,t),role,t,colour);}
            }
            for(int row=0;row<steps;row++)for(int side=0;side<sides;side++){int a=start+row*(sides+1)+side;Quad(a,a+1,a+sides+2,a+sides+1);}
        }
        public Mesh Mesh()
        {
            var mesh=new Mesh{name="Original moon-inspired jellyfish"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetUVs(1,anatomy);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var bounds=mesh.bounds;bounds.Expand(new Vector3(.055f,.065f,.055f));mesh.bounds=bounds;
            if(mesh.vertexCount>15000||triangles.Count/3>15000)throw new Exception("Jellyfish geometry budget exceeded");return mesh;
        }
    }
    public static void Run()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);var g=new Geometry();const int rings=16,sides=64;var pearl=new Color(.86f,.92f,.95f,.23f);
        // A thin dome with a gently scalloped margin; no stacked opaque spheres.
        g.Add(new Vector3(0,.067f,0),new Vector2(.5f,0),0,0,pearl);
        for(int ring=1;ring<=rings;ring++)for(int side=0;side<=sides;side++)
        {
            float t=ring/(float)rings,a=side*Mathf.PI*2/sides,r=.118f*t;
            float y=.067f*Mathf.Sqrt(Mathf.Max(0,1-t*t))-.012f*t*t+Mathf.Sin(a*16)*.0018f*Mathf.Pow(t,6);
            g.Add(new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r),new Vector2(side/(float)sides,t),0,t,pearl);
        }
        for(int side=0;side<sides;side++)g.Triangle(0,side+2,side+1);
        for(int ring=0;ring<rings-1;ring++)for(int side=0;side<sides;side++){int a=1+ring*(sides+1)+side;g.Quad(a,a+1,a+sides+2,a+sides+1);}
        // Short fine marginal tentacles, distinct from the four longer oral arms.
        for(int i=0;i<48;i++)
        {
            float a=i*Mathf.PI*2/48,length=.025f+.014f*(.5f+.5f*Mathf.Sin(i*2.39996f));var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            float margin=Mathf.Sin(a*16)*.0018f;
            g.Tube(t=>radial*(.118f+Mathf.Sin(t*2.5f+i)*.003f*t)+Vector3.up*(-.012f+margin-length*t),t=>Mathf.Lerp(.00065f,.00013f,t),10,3,1,i/48f,new Color(.78f,.87f,.91f,.60f));
        }
        g.Tube(t=>new Vector3(0,.052f-t*.065f,0),t=>Mathf.Lerp(.006f,.014f,t),12,8,3,0,new Color(.82f,.85f,.9f,.34f));
        for(int arm=0;arm<4;arm++)
        {
            int start=g.vertices.Count;float a=arm*Mathf.PI*.5f+.35f;var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var cross=new Vector3(-radial.z,0,radial.x);
            const int rows=48,width=8;
            for(int row=0;row<=rows;row++)for(int side=0;side<=width;side++)
            {
                float t=row/(float)rows,u=side/(float)width*2-1;
                var center=radial*(.012f+t*.045f+Mathf.Sin(t*4.5f+arm)*.018f*t)+cross*(Mathf.Sin(t*2.7f+arm*.7f)*.025f*t)+Vector3.up*(-.008f-t*(.205f+arm*.009f));
                float w=.009f*(1-t*.65f)*(1+Mathf.Sin(t*33+arm)*.2f);
                var p=center+cross*u*w+radial*(Mathf.Sin(t*45+side*.6f+arm)*.003f*u*u);
                g.Add(p,new Vector2((arm+(u+1)*.04f)/4f,t),2,t,new Color(.82f,.83f,.88f,.64f));
            }
            for(int row=0;row<rows;row++)for(int side=0;side<width;side++){int n=start+row*(width+1)+side;g.Quad(n,n+width+1,n+width+2,n+1);}
        }
        // Four subdued horseshoe forms under the bell, not a face or emoji.
        for(int i=0;i<4;i++)
        {
            float a=i*Mathf.PI*.5f;var center=new Vector3(Mathf.Cos(a)*.027f,.037f,Mathf.Sin(a)*.027f);
            g.Tube(t=>center+new Vector3(Mathf.Cos(t*Mathf.PI*1.65f+a)*.014f,0,Mathf.Sin(t*Mathf.PI*1.65f+a)*.014f),t=>.0022f,28,5,3,i/4f,new Color(.64f,.52f,.59f,.42f));
        }
        var generated=g.Mesh();string meshPath=Folder+"/Jellyfish.mesh.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(mesh){EditorUtility.CopySerialized(generated,mesh);UnityEngine.Object.DestroyImmediate(generated);}else{mesh=generated;AssetDatabase.CreateAsset(mesh,meshPath);}
        var shader=Shader.Find("MagicMR/NaturalJellyfish");if(!shader)throw new Exception("Natural jellyfish shader missing");
        string materialPath=Folder+"/Jellyfish.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}material.shader=shader;material.SetFloat("_Opacity",1);material.SetFloat("_Motion",1);EditorUtility.SetDirty(material);
        var go=new GameObject("Original moon-inspired jellyfish lamp");
        try{go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;go.AddComponent<NaturalJellyfish>().Initialize();PrefabUtility.SaveAsPrefabAsset(go,Prefab);}
        finally{UnityEngine.Object.DestroyImmediate(go);}
        File.WriteAllText(Folder+"/provenance.txt","Original geometry and procedural shader authored for Magic Wand Amusement Park. Moon-jelly-inspired fantasy lamp, not a scientific species reconstruction or claim of natural bioluminescence. Shape reference only: https://www.montereybayaquarium.org/animals-the-ocean/animals-a-to-z/moon-jelly . No photos/models copied, no training.\n");
        AssetDatabase.SaveAssets();Debug.Log("NATURAL_JELLYFISH_ASSET_PASS: "+mesh.vertexCount+" vertices, "+mesh.triangles.Length/3+" triangles, one surface/material, no textures/bones/lights");
    }
}
