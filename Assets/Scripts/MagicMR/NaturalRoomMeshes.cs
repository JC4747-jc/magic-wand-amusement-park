using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicMR
{
    // Metric UVs and millimetre bevels; shared immutable geometry for every room.
    public static class NaturalRoomMeshes
    {
        static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        public static Mesh Get(string name)
        {
            // Scene transitions can unload native Mesh objects while managed
            // dictionary entries remain. Rebuild that cache instead of using
            // Unity's destroyed-object references as empty room geometry.
            if(!meshes.TryGetValue(name,out var mesh)||!mesh){meshes.Clear();Build();}
            return meshes[name];
        }
        sealed class Batch
        {
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();readonly List<int> triangles=new List<int>();int boxes;
            public void Box(Vector3 center,Vector3 size,float radius,float repeats=4,float roll=0)
            {
                radius=Mathf.Min(radius,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.45f);var half=size*.5f;var rotation=Quaternion.Euler(0,0,roll);
                if(radius<=0)
                {
                    foreach(var face in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
                    {
                        var u=Mathf.Abs(face.y)>.5f?Vector3.right:Vector3.Cross(Vector3.up,face);var v=Vector3.Cross(face,u);
                        float uh=Mathf.Abs(Vector3.Dot(half,u)),vh=Mathf.Abs(Vector3.Dot(half,v)),fh=Mathf.Abs(Vector3.Dot(half,face));int start=vertices.Count;
                        foreach(var coordinate in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(-1,1),new Vector2(1,1)})
                        {
                            var point=center+rotation*(face*fh+u*coordinate.x*uh+v*coordinate.y*vh);vertices.Add(point);normals.Add(rotation*face);
                            uv.Add(new Vector2(Vector3.Dot(point,u)*repeats,Vector3.Dot(point,v)*repeats));
                        }
                        triangles.AddRange(new[]{start,start+1,start+2,start+1,start+3,start+2});
                    }
                    boxes++;return;
                }
                foreach(var face in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
                {
                    var u=Mathf.Abs(face.y)>.5f?Vector3.right:Vector3.Cross(Vector3.up,face);var v=Vector3.Cross(face,u);
                    float uh=Mathf.Abs(Vector3.Dot(half,u)),vh=Mathf.Abs(Vector3.Dot(half,v)),fh=Mathf.Abs(Vector3.Dot(half,face));
                    float[] xs={-uh,-uh+radius,uh-radius,uh},ys={-vh,-vh+radius,vh-radius,vh};int start=vertices.Count;
                    for(int y=0;y<4;y++)for(int x=0;x<4;x++)
                    {
                        var p=face*fh+u*xs[x]+v*ys[y];var inner=new Vector3(Mathf.Clamp(p.x,-half.x+radius,half.x-radius),Mathf.Clamp(p.y,-half.y+radius,half.y-radius),Mathf.Clamp(p.z,-half.z+radius,half.z-radius));
                        var normal=(p-inner).normalized;var point=center+rotation*(inner+normal*radius);
                        vertices.Add(point);normals.Add(rotation*normal);uv.Add(new Vector2(Vector3.Dot(point,u)*repeats+boxes*.137f,Vector3.Dot(point,v)*repeats+boxes*.233f));
                        if(x==3||y==3)continue;int n=start+y*4+x;triangles.AddRange(new[]{n,n+1,n+4,n+1,n+5,n+4});
                    }
                }
                boxes++;
            }
            public void Lathe(Vector3 center,Vector2[] profile,int sides=48)
            {
                int start=vertices.Count;
                for(int ring=0;ring<profile.Length;ring++)
                {
                    var delta=profile[Mathf.Min(ring+1,profile.Length-1)]-profile[Mathf.Max(0,ring-1)];var normal=new Vector2(delta.y,-delta.x).normalized;
                    for(int side=0;side<=sides;side++)
                    {
                        float a=side/(float)sides*Mathf.PI*2;var point=center+new Vector3(Mathf.Cos(a)*profile[ring].x,profile[ring].y,Mathf.Sin(a)*profile[ring].x);
                        vertices.Add(point);normals.Add(new Vector3(Mathf.Cos(a)*normal.x,normal.y,Mathf.Sin(a)*normal.x));
                        uv.Add(Mathf.Abs(normal.y)>.7f?new Vector2(point.x*4,point.z*4):new Vector2(side/(float)sides*2,profile[ring].y*4));
                        if(ring==profile.Length-1||side==sides)continue;int n=start+ring*(sides+1)+side,q=n+sides+1;
                        if(profile[ring].x>0)triangles.AddRange(new[]{n,q,n+1});if(profile[ring+1].x>0)triangles.AddRange(new[]{n+1,q,q+1});
                    }
                }
            }
            public Mesh Finish(string name)
            {
                var mesh=new Mesh{name="Natural room shared "+name};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
            }
        }
        static void Build()
        {
            var wood=new Batch();var wall=new Batch();var rug=new Batch();var metal=new Batch();var rust=new Batch();var olive=new Batch();var paper=new Batch();
            var returnWall=new Batch();
            var sideboard=new Batch();
            var sideboardDoors=new Batch();
            var sideboardHardware=new Batch();
            // Separate boards, narrow joints and a genuine opening in the wall.
            for(int i=0;i<6;i++)wood.Box(new Vector3(-.4f+(i+.5f)*.8f/6,.006f,.025f),new Vector3(.8f/6-.0012f,.012f,.55f),.0012f);
            wall.Box(new Vector3(0,.141f,.29f),new Vector3(.8f,.258f,.018f),0,2);
            wall.Box(new Vector3(-.38f,.40f,.29f),new Vector3(.04f,.26f,.018f),0,2);
            wall.Box(new Vector3(.18f,.40f,.29f),new Vector3(.44f,.26f,.018f),0,2);
            wall.Box(new Vector3(0,.54f,.29f),new Vector3(.8f,.02f,.018f),0,2);
            returnWall.Box(new Vector3(-.391f,.114f,.088f),new Vector3(.018f,.204f,.382f),.004f,2);
            returnWall.Box(new Vector3(-.391f,.218f,.088f),new Vector3(.026f,.009f,.386f),.002f,2);
            returnWall.Box(new Vector3(.391f,.096f,.180f),new Vector3(.018f,.168f,.200f),.004f,2);
            returnWall.Box(new Vector3(.391f,.184f,.180f),new Vector3(.026f,.009f,.204f),.002f,2);
            // Storage sits below the existing shelf and leaves the table and window accessible.
            sideboard.Box(new Vector3(.186f,.104f,.211f),new Vector3(.34f,.132f,.122f),.004f);
            sideboard.Box(new Vector3(.186f,.175f,.211f),new Vector3(.354f,.013f,.132f),.004f);
            foreach(float x in new[]{.040f,.332f})
            foreach(float z in new[]{.161f,.259f})
                sideboard.Box(new Vector3(x,.026f,z),new Vector3(.017f,.028f,.017f),.002f);
            foreach(float x in new[]{.107f,.265f})
            {
                sideboardDoors.Box(new Vector3(x,.106f,.147f),new Vector3(.151f,.111f,.012f),.003f);
                float handleX=x+(x<.186f?.048f:-.048f);
                sideboardHardware.Box(new Vector3(handleX,.116f,.136f),new Vector3(.008f,.040f,.012f),.002f);
            }
            wood.Box(new Vector3(0,.035f,.275f),new Vector3(.8f,.045f,.012f),.0014f);
            foreach(float x in new[]{-.36f,-.04f})wood.Box(new Vector3(x,.4f,.274f),new Vector3(.014f,.274f,.032f),.0015f);
            foreach(float y in new[]{.27f,.53f})wood.Box(new Vector3(-.2f,y,.274f),new Vector3(.334f,.014f,.032f),.0015f);
            wood.Box(new Vector3(-.2f,.4f,.28f),new Vector3(.007f,.25f,.012f),.001f);
            wood.Box(new Vector3(-.2f,.4f,.28f),new Vector3(.314f,.007f,.012f),.001f);
            wood.Box(new Vector3(-.2f,.26f,.26f),new Vector3(.36f,.016f,.065f),.002f);
            wood.Box(new Vector3(.17f,.432f,.238f),new Vector3(.39f,.015f,.10f),.002f);
            rug.Box(new Vector3(-.16f,.013f,.025f),new Vector3(.47f,.002f,.39f),.0006f,1.5f);
            wood.Lathe(new Vector3(.21f,0,.045f),new[]{new Vector2(0,.219f),new Vector2(.094f,.219f),new Vector2(.10f,.222f),new Vector2(.10f,.230f),new Vector2(.097f,.233f),new Vector2(0,.233f)});
            metal.Lathe(new Vector3(.21f,0,.045f),new[]{new Vector2(0,.012f),new Vector2(.066f,.012f),new Vector2(.075f,.015f),new Vector2(.073f,.022f),new Vector2(.018f,.027f),new Vector2(.010f,.034f),new Vector2(.007f,.211f),new Vector2(.016f,.218f),new Vector2(0,.219f)});
            for(int i=0;i<5;i++)
            {
                float x=.055f+i*.043f,h=.083f+(i%3)*.008f;var cover=i%2==0?olive:rust;var center=new Vector3(x,.44f+h*.5f,.238f);
                cover.Box(center+Vector3.left*.014f,new Vector3(.002f,h+.003f,.071f),.0005f,18);
                cover.Box(center+Vector3.right*.014f,new Vector3(.002f,h+.003f,.071f),.0005f,18);
                cover.Box(center+Vector3.back*.034f,new Vector3(.029f,h+.003f,.003f),.0007f,18);
                paper.Box(center+Vector3.forward*.002f,new Vector3(.025f,h-.005f,.064f),.0005f,4);
            }
            foreach(var item in new[]{("Timber joinery",wood),("Plaster walls",wall),("Woven rug",rug),("Table pedestal",metal),("Rust book cloth",rust),("Olive book cloth",olive),("Book pages",paper)})meshes.Add(item.Item1,item.Item2.Finish(item.Item1));
            meshes.Add("Miniature return wall",returnWall.Finish("Miniature return wall"));
            meshes.Add("Miniature sideboard",sideboard.Finish("Miniature sideboard"));
            meshes.Add("Miniature sideboard doors",sideboardDoors.Finish("Miniature sideboard doors"));
            meshes.Add("Miniature sideboard handles",sideboardHardware.Finish("Miniature sideboard handles"));
        }
    }
}
