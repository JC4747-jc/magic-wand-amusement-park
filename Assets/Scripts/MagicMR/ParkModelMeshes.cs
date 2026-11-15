using System.Collections.Generic;
using UnityEngine;

namespace MagicMR
{
    // Shared meshes, independent of actor count. No per-frame geometry allocation.
    public static class ParkModelMeshes
    {
        static Mesh trunk,leaf,canopy,ear,tail,pot,roundedBox,flower;
        public static Mesh Flower=>flower?flower:flower=MakeFlower();
        public static Mesh RoundedBox=>roundedBox?roundedBox:roundedBox=MakeRoundedBox();
        public static Mesh Pot=>pot?pot:pot=MakePot();
        public static Mesh Trunk=>trunk?trunk:trunk=Tube(false);
        public static Mesh Tail=>tail?tail:tail=Tube(true);
        public static Mesh Leaf=>leaf?leaf:leaf=MakeLeaf();
        public static Mesh Canopy=>canopy?canopy:canopy=MakeCanopy();
        public static Mesh Ear=>ear?ear:ear=MakeEar();

        static Mesh Finish(string name,List<Vector3> vertices,List<int> triangles)
        {
            var mesh=new Mesh {name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh MakeRoundedBox()
        {
            const int steps=8;const float radius=.10f;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var tangents=new List<Vector4>();var triangles=new List<int>();
            foreach(var face in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
            {
                var u=Mathf.Abs(face.y)>.5f?Vector3.right:Vector3.Cross(Vector3.up,face);
                var v=Vector3.Cross(face,u);int start=vertices.Count;
                for(int y=0;y<=steps;y++)for(int x=0;x<=steps;x++)
                {
                    var point=face*.5f+u*(x/(float)steps-.5f)+v*(y/(float)steps-.5f);
                    var inner=new Vector3(Mathf.Clamp(point.x,-.5f+radius,.5f-radius),Mathf.Clamp(point.y,-.5f+radius,.5f-radius),Mathf.Clamp(point.z,-.5f+radius,.5f-radius));
                    var normal=(point-inner).normalized;vertices.Add(inner+normal*radius);normals.Add(normal);uv.Add(new Vector2(x/(float)steps,y/(float)steps));
                    var tangent=(u-normal*Vector3.Dot(normal,u)).normalized;tangents.Add(new Vector4(tangent.x,tangent.y,tangent.z,1));
                    if(x==steps||y==steps)continue;int p=start+y*(steps+1)+x;
                    triangles.AddRange(new[]{p,p+1,p+steps+1,p+1,p+steps+2,p+steps+1});
                }
            }
            var mesh=new Mesh {name="Park shared softly bevelled furniture"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTangents(tangents);mesh.RecalculateBounds();return mesh;
        }
        static Mesh MakeFlower()
        {
            // Small tubular Haworthia flower: slender base, pale six-lobed mouth.
            var vertices=new List<Vector3>();var triangles=new List<int>();const int rings=5,sides=24;
            for(int ring=0;ring<=rings;ring++)for(int side=0;side<sides;side++)
            {
                float u=ring/(float)rings,a=side*Mathf.PI*2/sides;
                float radius=Mathf.Lerp(.10f,.32f,u*u)*(1+Mathf.Cos(a*6)*.10f*u);
                vertices.Add(new Vector3(Mathf.Cos(a)*radius,u,Mathf.Sin(a)*radius));
                if(ring==rings)continue;int p=ring*sides+side,q=ring*sides+(side+1)%sides;
                triangles.AddRange(new[]{p,p+sides,q,q,p+sides,q+sides});
            }
            // Separate back vertices preserve normals; reversed faces on shared vertices would cancel them.
            int count=vertices.Count,indices=triangles.Count;vertices.AddRange(vertices.ToArray());
            for(int i=0;i<indices;i+=3)triangles.AddRange(new[]{triangles[i+2]+count,triangles[i+1]+count,triangles[i]+count});
            return Finish("Park shared tubular six-lobed flower",vertices,triangles);
        }
        static Mesh Tube(bool curved)
        {
            const int rings=10,sides=12;
            var v=new List<Vector3>();var t=new List<int>();
            for(int ring=0;ring<=rings;ring++)
            {
                float u=ring/(float)rings;
                Vector3 centre=curved?new Vector3(Mathf.Sin(u*2.4f)*.18f,u*.7f,u*.75f):new Vector3(Mathf.Sin(u*4)*.09f,u*2-1,Mathf.Sin(u*3)*.04f);
                float radius=curved?Mathf.Lerp(.075f,.012f,u):Mathf.Lerp(.58f,.23f,u);
                for(int side=0;side<sides;side++)
                {
                    float angle=side*Mathf.PI*2/sides;
                    float r=radius*(curved?1:1+.10f*Mathf.Sin(side*2.3f+ring*.8f));
                    v.Add(centre+new Vector3(Mathf.Cos(angle)*r,0,Mathf.Sin(angle)*r));
                    if(ring==rings)continue;
                    int a=ring*sides+side,b=ring*sides+(side+1)%sides,c=a+sides,d=b+sides;
                    t.AddRange(new[]{a,c,b,b,c,d});
                }
            }
            // Cap both ends so cut surfaces never reveal hollow geometry.
            for(int side=1;side<sides-1;side++) {t.AddRange(new[]{0,side,side+1});int last=rings*sides;t.AddRange(new[]{last,last+side+1,last+side});}
            return Finish(curved?"Park shared curved tail":"Park shared tapered bark",v,t);
        }
        static Mesh MakeLeaf()
        {
            var v=new List<Vector3>();var t=new List<int>();
            const int rings=8,sides=4;
            for(int i=0;i<=rings;i++)
            {
                float u=i/(float)rings,w=Mathf.Max(.002f,Mathf.Pow(Mathf.Clamp01(Mathf.Sin(u*Mathf.PI)),.8f))*.5f;
                for(int side=0;side<sides;side++)
                {
                    float a=side*Mathf.PI*.5f;
                    v.Add(new Vector3(u-.5f,Mathf.Sin(u*Mathf.PI)*.12f+Mathf.Cos(a)*.025f,Mathf.Sin(a)*w));
                    if(i==rings)continue;int p=i*sides+side,q=i*sides+(side+1)%sides;
                    t.AddRange(new[]{p,p+sides,q,q,p+sides,q+sides});
                }
            }
            return Finish("Park shared folded lanceolate leaf",v,t);
        }
        static Mesh MakeCanopy()
        {
            var parts=new CombineInstance[28];
            for(int i=0;i<parts.Length;i++)
            {
                float angle=i*2.399963f,height=(i/(float)parts.Length-.5f)*.65f,radius=Mathf.Sqrt(Mathf.Max(0,1-Mathf.Pow(height*2,2)))*.35f;
                var position=new Vector3(Mathf.Cos(angle)*radius,height,Mathf.Sin(angle)*radius);
                parts[i]=new CombineInstance {mesh=Leaf,transform=Matrix4x4.TRS(position,Quaternion.Euler(i%3*12,i*137.5f,15),new Vector3(.48f,.3f,.25f))};
            }
            var mesh=new Mesh {name="Park shared leaf canopy"};mesh.CombineMeshes(parts,true,true);mesh.RecalculateBounds();return mesh;
        }
        static Mesh MakeEar()
        {
            return Finish("Park shared anatomical pinna",new List<Vector3>{new Vector3(-.5f,0,-.4f),new Vector3(.5f,0,-.4f),new Vector3(0,1,0),new Vector3(0,0,.4f)},
                new List<int>{0,2,1,0,3,2,1,2,3,0,1,3});
        }
        static Mesh MakePot()
        {
            var profile=new[]{new Vector2(.37f,-1),new Vector2(.50f,.86f),new Vector2(.52f,.88f),new Vector2(.52f,1),new Vector2(.44f,1),new Vector2(.43f,.82f),new Vector2(.32f,-.72f),new Vector2(0,-.72f),new Vector2(0,-1)};
            var v=new List<Vector3>();var t=new List<int>();const int sides=24;
            for(int ring=0;ring<profile.Length;ring++)for(int side=0;side<sides;side++)
            {
                float a=side*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(a)*profile[ring].x,profile[ring].y,Mathf.Sin(a)*profile[ring].x));
                int nextRing=(ring+1)%profile.Length,p=ring*sides+side,q=ring*sides+(side+1)%sides,r=nextRing*sides+side,s=nextRing*sides+(side+1)%sides;
                t.AddRange(new[]{p,r,q,q,r,s});
            }
            return Finish("Park shared hollow terracotta pot",v,t);
        }
    }
}
