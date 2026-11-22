using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class NaturalTreeMarkAssetBuilder
{
    public const string Folder="Assets/MagicMR/NaturalTreeScar";
    const string Tree="Assets/Resources/MagicMR/NaturalModels/SmallTree.prefab";
    struct Hit {public Vector3 point,normal;public float distance;}
    static bool Surface(Ray ray,Vector3[] vertices,int[] triangles,out Hit hit)
    {
        hit=new Hit{distance=float.PositiveInfinity};bool found=false;
        for(int i=0;i<triangles.Length;i+=3)
        {
            var a=vertices[triangles[i]];var u=vertices[triangles[i+1]]-a;var v=vertices[triangles[i+2]]-a;var p=Vector3.Cross(ray.direction,v);float determinant=Vector3.Dot(u,p);
            if(Mathf.Abs(determinant)<1e-10f)continue;var s=ray.origin-a;float x=Vector3.Dot(s,p)/determinant;if(x<0||x>1)continue;var q=Vector3.Cross(s,u);float y=Vector3.Dot(ray.direction,q)/determinant;if(y<0||x+y>1)continue;
            float distance=Vector3.Dot(v,q)/determinant;if(distance<0||distance>=hit.distance)continue;
            var normal=Vector3.Cross(u,v).normalized;if(Vector3.Dot(normal,ray.direction)>0)normal=-normal;
            hit=new Hit{point=ray.GetPoint(distance),normal=normal,distance=distance};found=true;
        }
        return found;
    }
    public static void Run()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var root=PrefabUtility.LoadPrefabContents(Tree);
        try
        {
            var growth=root.transform.Find("TreeGrowth");var trunk=root.GetComponentsInChildren<MeshFilter>().Single();var vertices=trunk.sharedMesh.vertices.Select(trunk.transform.TransformPoint).ToArray();var triangles=trunk.sharedMesh.triangles;
            var bounds=trunk.GetComponent<Renderer>().bounds;var row=new List<Hit>();var longest=new List<Hit>();float step=bounds.size.x/160;
            for(int attempt=0;attempt<4&&longest.Count<3;attempt++)
            {
                row.Clear();float height=bounds.min.y+bounds.size.y*(.28f+attempt*.08f);
                for(int i=0;i<=160;i++)
                {
                    if(Surface(new Ray(new Vector3(bounds.min.x+step*i,height,bounds.min.z-.2f),Vector3.forward),vertices,triangles,out var candidate))row.Add(candidate);
                    else{if(row.Count>longest.Count)longest=new List<Hit>(row);row.Clear();}
                }
                if(row.Count>longest.Count)longest=new List<Hit>(row);
            }
            if(longest.Count<3)throw new Exception("No trunk surface for fantasy bark mark");
            var center=longest[longest.Count/2];float width=(longest.Last().point.x-longest.First().point.x+step)*.42f;float heightMark=Mathf.Min(.065f,width*2);
            var marker=growth.Find("FantasyScarAnchor");if(!marker){marker=new GameObject("FantasyScarAnchor").transform;marker.SetParent(growth,false);}
            marker.SetPositionAndRotation(center.point,Quaternion.LookRotation(center.normal,Vector3.up));marker.localScale=Vector3.one;
            const int sides=32;var points=new List<Vector3>();var colours=new List<Color>();var uv=new List<Vector2>();var indices=new List<int>();
            float[] radii={1,.78f,.48f};Color[] palette={new Color(.16f,.12f,.075f),new Color(.34f,.25f,.14f),new Color(.28f,.17f,.08f)};float[] offsets={.0002f,.0009f,.00055f};float maximumOffset=0;
            void Add(float x,float y,Color colour,float offset)
            {
                var sample=marker.TransformPoint(new Vector3(x*width,y*heightMark,0));
                if(!Surface(new Ray(sample+center.normal*.1f,-center.normal),vertices,triangles,out var contact))throw new Exception("Bark mark extends outside trunk silhouette");
                points.Add(marker.InverseTransformPoint(contact.point+contact.normal*offset));colours.Add(colour.linear);uv.Add(new Vector2(x+.5f,y+.5f));maximumOffset=Mathf.Max(maximumOffset,offset);
            }
            for(int ring=0;ring<3;ring++)for(int side=0;side<sides;side++)
            {
                float a=side/(float)sides*Mathf.PI*2,r=radii[ring]*(1+.055f*Mathf.Sin(a*5)+.035f*Mathf.Cos(a*3));
                Add(Mathf.Cos(a)*r*.5f,Mathf.Sin(a)*r*.5f,palette[ring],offsets[ring]);
                if(ring<2){int p=ring*sides+side,q=ring*sides+(side+1)%sides;indices.AddRange(new[]{p,q,p+sides,q,q+sides,p+sides});}
            }
            int middle=points.Count;Add(0,0,new Color(.11f,.075f,.045f),.0003f);for(int i=0;i<sides;i++)indices.AddRange(new[]{2*sides+i,2*sides+(i+1)%sides,middle});
            var mesh=new Mesh{name="Surface-conforming fantasy bark mark"};mesh.SetVertices(points);mesh.SetColors(colours);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            string meshPath=Folder+"/Scar.mesh.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,meshPath);
            var shader=Shader.Find("MagicMR/NaturalBarkMark");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Bark mark shader failed");
            string matPath=Folder+"/Scar.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,matPath);}material.shader=shader;material.SetColor("_BaseColor",Color.white);EditorUtility.SetDirty(material);
            PrefabUtility.SaveAsPrefabAsset(root,Tree);var mark=new GameObject("Fantasy bark mark");try{mark.AddComponent<MeshFilter>().sharedMesh=mesh;mark.AddComponent<MeshRenderer>().sharedMaterial=material;PrefabUtility.SaveAsPrefabAsset(mark,"Assets/Resources/MagicMR/NaturalModels/TreeScar.prefab");}finally{UnityEngine.Object.DestroyImmediate(mark);}
            AssetDatabase.SaveAssets();Debug.Log("NATURAL_TREE_MARK_BUILD_PASS: "+mesh.vertexCount+" vertices/"+indices.Count/3+" triangles; all vertices ray-fitted to original trunk, max offset="+maximumOffset+" m; nominal width="+width+" height="+heightMark+"; fantasy overlay, no real wound inference");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
