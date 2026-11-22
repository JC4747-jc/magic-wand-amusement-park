using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagicMR
{
    // Authored virtual garden. Geometry is created once; animation uses GPU petal morphs and transforms.
    public sealed class StatueWishGarden:MonoBehaviour
    {
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly List<Renderer> flowers=new List<Renderer>();
        readonly List<Transform> cards=new List<Transform>();
        readonly List<Quaternion> cardRest=new List<Quaternion>();
        readonly List<Vector3> cardSites=new List<Vector3>();
        Material material;MaterialPropertyBlock block;Transform tree;
        bool requested;float progress;
        public float Progress=>progress;
        public int FlowerCount=>flowers.Count;
        public int CardCount=>cards.Count;
        public int VertexCount {get{int n=0;foreach(var m in meshes)n+=m.vertexCount;return n;}}
        public int TriangleCount {get{int n=0;foreach(var m in meshes)n+=(int)m.GetIndexCount(0)/3;return n;}}
        public void Initialize()
        {
            if(material)return;
            var shader=Resources.Load<Shader>("MagicMR/WishGarden");if(!shader)throw new InvalidOperationException("Wish garden shader missing");
            material=new Material(shader){name="Virtual lotus and wish tree (owned)"};block=new MaterialPropertyBlock();
            var lotus=BuildLotus();
            for(int i=0;i<6;i++)
            {
                float a=(i*55+20)*Mathf.Deg2Rad;
                var r=Draw("Lotus "+i,lotus,transform);r.transform.localPosition=new Vector3(Mathf.Cos(a)*.65f,0,Mathf.Sin(a)*.65f);
                r.transform.localRotation=Quaternion.Euler(0,i*137.5f,0);flowers.Add(r);
            }
            tree=new GameObject("Wish tree growth").transform;tree.SetParent(transform,false);tree.localPosition=new Vector3(.30f,0,.35f);
            Draw("Tapered branches and leaves",BuildTree(),tree);
            var tag=BuildCard();
            for(int i=0;i<cardSites.Count;i++)
            {
                var pivot=new GameObject("Wish ribbon hinge "+i).transform;pivot.SetParent(tree,false);pivot.localPosition=cardSites[i];
                pivot.localRotation=Quaternion.Euler(0,i*61,0);cards.Add(pivot);cardRest.Add(pivot.localRotation);Draw("Ivory wish ribbon",tag,pivot);
            }
            Clear();
        }
        Renderer Draw(string name,Mesh mesh,Transform parent)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;return r;
        }
        public void SetRequested(bool value){requested=value;}
        public void Step(float dt,float now)
        {
            if(!material)return;
            if(!float.IsFinite(dt)||dt<0||!float.IsFinite(now)){Clear();return;}
            progress=Mathf.MoveTowards(progress,requested?1:0,Mathf.Min(dt,.1f)/1.2f);
            for(int i=0;i<flowers.Count;i++)
            {
                float open=Mathf.SmoothStep(0,1,Mathf.InverseLerp(i*.045f,.7f+i*.045f,progress));
                block.Clear();block.SetFloat("_Bloom",open);flowers[i].SetPropertyBlock(block);
                flowers[i].transform.localScale=Vector3.one*Mathf.SmoothStep(0,1,Mathf.Clamp01(progress*4));
            }
            float grow=Mathf.SmoothStep(0,1,progress);tree.localScale=new Vector3(grow,grow,grow);
            for(int i=0;i<cards.Count;i++)cards[i].localRotation=cardRest[i]*Quaternion.Euler(Mathf.Sin(now*1.4f+i*.8f)*6*grow,0,Mathf.Sin(now*.9f+i)*3*grow);
        }
        public void Clear(){requested=false;progress=0;if(tree)tree.localScale=Vector3.zero;foreach(var r in flowers)if(r)r.transform.localScale=Vector3.zero;for(int i=0;i<cards.Count;i++)cards[i].localRotation=cardRest[i];}
        void OnDisable(){Clear();}
        void OnDestroy(){if(material)Destroy(material);foreach(var m in meshes)if(m)Destroy(m);}

        sealed class Geometry
        {
            public readonly List<Vector3> v=new List<Vector3>(),closed=new List<Vector3>();
            public readonly List<Color> colors=new List<Color>();public readonly List<int> triangles=new List<int>();
            public int Add(Vector3 p,Color c,Vector3? start=null){int n=v.Count;v.Add(p);closed.Add(start??p);colors.Add(c);return n;}
            public void Quad(int a,int b,int c,int d){triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(a);triangles.Add(c);triangles.Add(d);}
            public void Tube(Vector3 from,Vector3 to,float r0,float r1,Color color)
            {
                var direction=(to-from).normalized;var x=Vector3.Cross(direction,Mathf.Abs(direction.y)>.9f?Vector3.right:Vector3.up).normalized;var z=Vector3.Cross(direction,x);int first=v.Count;
                for(int row=0;row<=4;row++)for(int side=0;side<8;side++){float t=row/4f,a=side*Mathf.PI/4;var p=Vector3.Lerp(from,to,t)+x*Mathf.Sin(t*Mathf.PI)*.012f+(x*Mathf.Cos(a)+z*Mathf.Sin(a))*Mathf.Lerp(r0,r1,t);Add(p,color);}
                for(int row=0;row<4;row++)for(int side=0;side<8;side++)Quad(first+row*8+side,first+row*8+(side+1)%8,first+(row+1)*8+(side+1)%8,first+(row+1)*8+side);
            }
            public void Leaf(Vector3 root,Vector3 tip,float width,Color color)
            {
                var d=tip-root;var side=Vector3.Cross(d,Vector3.up).normalized*width;int first=v.Count;
                Add(root,color);Add(root+d*.5f-side,color);Add(root+d*.52f+Vector3.up*.012f,color*.9f);Add(root+d*.5f+side,color);Add(tip,color);
                triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3,first+1,first+4,first+2,first+2,first+4,first+3});
            }
        }
        Mesh Finish(string name,Geometry g)
        {
            var mesh=new Mesh{name=name};mesh.SetVertices(g.closed);mesh.SetTriangles(g.triangles,0);mesh.RecalculateNormals();var closedNormals=new List<Vector3>(mesh.normals);
            mesh.SetVertices(g.v);mesh.SetColors(g.colors);mesh.SetUVs(1,g.closed);mesh.SetUVs(2,closedNormals);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var bounds=mesh.bounds;foreach(var p in g.closed)bounds.Encapsulate(p);mesh.bounds=bounds;mesh.UploadMeshData(true);meshes.Add(mesh);return mesh;
        }
        Mesh BuildLotus()
        {
            var g=new Geometry();var green=new Color(.20f,.33f,.20f);
            // A notched lily pad rests on the confirmed plane, rather than a flattened sphere.
            int center=g.Add(new Vector3(0,.005f,0),green);
            for(int i=0;i<=28;i++){float a=Mathf.Lerp(.18f,Mathf.PI*2-.18f,i/28f);g.Add(new Vector3(Mathf.Sin(a)*.21f,.006f+Mathf.Sin(a*3)*.002f,Mathf.Cos(a)*.18f),green);if(i>0){g.triangles.Add(center);g.triangles.Add(center+i+1);g.triangles.Add(center+i);}}
            for(int ring=0;ring<3;ring++)
            {
                int count=ring==0?10:ring==1?8:6;float length=.17f-ring*.032f;float baseRadius=.028f-ring*.006f;
                for(int petal=0;petal<count;petal++)
                {
                    float a=(petal/(float)count+ring*.065f)*Mathf.PI*2;var radial=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));var side=new Vector3(radial.z,0,-radial.x);int first=g.v.Count;
                    for(int row=0;row<=8;row++)for(int col=0;col<=4;col++)
                    {
                        float t=row/8f,w=(col/4f*2-1)*Mathf.Sin(t*Mathf.PI)*(.060f-ring*.009f);
                        Vector3 Shape(float angle){float q=angle*Mathf.Deg2Rad;return radial*(baseRadius+length*t*Mathf.Cos(q))+side*w+Vector3.up*(.045f+ring*.009f+length*t*Mathf.Sin(q)+.028f*Mathf.Sin(t*Mathf.PI)*(1-Mathf.Abs(col/4f*2-1)));}
                        var color=Color.Lerp(new Color(.96f,.87f,.78f),new Color(.73f,.34f,.45f),Mathf.Pow(t,3)*.65f+ring*.07f);
                        g.Add(Shape(8+ring*19),color,Shape(78+ring*4));
                    }
                    for(int row=0;row<8;row++)for(int col=0;col<4;col++)g.Quad(first+row*5+col,first+(row+1)*5+col,first+(row+1)*5+col+1,first+row*5+col+1);
                }
            }
            g.Tube(new Vector3(0,.03f,0),new Vector3(0,.095f,0),.024f,.018f,new Color(.79f,.57f,.18f));
            for(int i=0;i<12;i++){float a=i*Mathf.PI/6;var p=new Vector3(Mathf.Sin(a)*.029f,.055f,Mathf.Cos(a)*.029f);g.Tube(p,p+Vector3.up*.045f,.002f,.003f,new Color(.93f,.72f,.28f));}
            return Finish("Layered lotus petals and notched pad",g);
        }
        Mesh BuildTree()
        {
            var g=new Geometry();var bark=new Color(.24f,.16f,.10f);var leaves=new Color(.26f,.36f,.21f);
            g.Tube(Vector3.zero,new Vector3(.015f,.65f,.025f),.043f,.021f,bark);g.Tube(new Vector3(.015f,.65f,.025f),new Vector3(-.025f,1.12f,0),.021f,.003f,bark);
            for(int i=0;i<7;i++)
            {
                float a=i*137.5f*Mathf.Deg2Rad;var radial=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));var root=new Vector3(.01f,.38f+i*.075f,.02f);var end=root+radial*(.33f-i*.022f)+Vector3.up*.23f;
                g.Tube(root,end,.014f-i*.001f,.002f,bark);cardSites.Add(Vector3.Lerp(root,end,.80f));
                for(int n=0;n<7;n++){float t=.25f+n*.105f;var at=Vector3.Lerp(root,end,t);var direction=Quaternion.AngleAxis(n%2==0?58:-58,Vector3.up)*radial;g.Leaf(at,at+direction*.13f+Vector3.up*.045f,.035f,leaves);}
            }
            return Finish("Curved tapering wish tree with individual leaves",g);
        }
        Mesh BuildCard()
        {
            var g=new Geometry();var ivory=new Color(.89f,.82f,.65f);var red=new Color(.52f,.16f,.13f);
            g.Tube(Vector3.zero,Vector3.down*.07f,.0015f,.0015f,red);
            int a=g.Add(new Vector3(-.024f,-.07f,0),ivory),b=g.Add(new Vector3(-.024f,-.16f,0),ivory),c=g.Add(new Vector3(.024f,-.15f,0),ivory),d=g.Add(new Vector3(.024f,-.07f,0),ivory);g.Quad(a,b,c,d);
            a=g.Add(new Vector3(-.015f,-.085f,-.0005f),red);b=g.Add(new Vector3(-.015f,-.105f,-.0005f),red);c=g.Add(new Vector3(.015f,-.105f,-.0005f),red);d=g.Add(new Vector3(.015f,-.085f,-.0005f),red);g.Quad(a,b,c,d);
            return Finish("Hanging wish ribbon with red seal",g);
        }
    }
}
