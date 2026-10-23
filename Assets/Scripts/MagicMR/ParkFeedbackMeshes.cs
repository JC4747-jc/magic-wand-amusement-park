using System.Collections.Generic;
using UnityEngine;

namespace MagicMR
{
    public enum ParkFeedbackShape { Star, Heart, Coin, Candy, Leaf, Fish, Bone, Petal }

    /// <summary>Shared, bounded geometry for recognizable virtual feedback.</summary>
    public static class ParkFeedbackMeshes
    {
        static readonly Dictionary<ParkFeedbackShape,Mesh> cache=new Dictionary<ParkFeedbackShape,Mesh>();
        public static Mesh Get(ParkFeedbackShape shape)
        {
            if(cache.TryGetValue(shape,out var mesh) && mesh)return mesh;
            var outline=new List<Vector2>();
            if(shape==ParkFeedbackShape.Petal)outline.AddRange(new[]{new Vector2(0,-.5f),new Vector2(-.25f,-.1f),new Vector2(-.35f,.25f),new Vector2(-.2f,.5f),new Vector2(0,.38f),new Vector2(.2f,.5f),new Vector2(.35f,.25f),new Vector2(.25f,-.1f)});
            else if(shape==ParkFeedbackShape.Fish)outline.AddRange(new[]{new Vector2(-.5f,.22f),new Vector2(-.2f,.08f),new Vector2(0,.28f),new Vector2(.32f,.18f),new Vector2(.5f,0),new Vector2(.32f,-.18f),new Vector2(0,-.28f),new Vector2(-.2f,-.08f),new Vector2(-.5f,-.22f)});
            else if(shape==ParkFeedbackShape.Bone)outline.AddRange(new[]{new Vector2(-.5f,-.12f),new Vector2(-.42f,-.25f),new Vector2(-.27f,-.25f),new Vector2(-.2f,-.1f),new Vector2(.2f,-.1f),new Vector2(.27f,-.25f),new Vector2(.42f,-.25f),new Vector2(.5f,-.12f),new Vector2(.43f,0),new Vector2(.5f,.12f),new Vector2(.42f,.25f),new Vector2(.27f,.25f),new Vector2(.2f,.1f),new Vector2(-.2f,.1f),new Vector2(-.27f,.25f),new Vector2(-.42f,.25f),new Vector2(-.5f,.12f),new Vector2(-.43f,0)});
            else if(shape==ParkFeedbackShape.Candy)outline.AddRange(new[]{new Vector2(-.5f,-.22f),new Vector2(-.28f,-.10f),new Vector2(-.20f,-.25f),new Vector2(.20f,-.25f),new Vector2(.28f,-.10f),new Vector2(.5f,-.22f),new Vector2(.5f,.22f),new Vector2(.28f,.10f),new Vector2(.20f,.25f),new Vector2(-.20f,.25f),new Vector2(-.28f,.10f),new Vector2(-.5f,.22f)});
            else {
                int count=shape==ParkFeedbackShape.Star?10:40;
                for(int i=0;i<count;i++) {
                    float a=i*Mathf.PI*2/count;
                    if(shape==ParkFeedbackShape.Heart)outline.Add(new Vector2(16*Mathf.Pow(Mathf.Sin(a),3)/32,(13*Mathf.Cos(a)-5*Mathf.Cos(2*a)-2*Mathf.Cos(3*a)-Mathf.Cos(4*a))/32));
                    else if(shape==ParkFeedbackShape.Star) { float r=i%2==0?.5f:.22f; outline.Add(new Vector2(Mathf.Sin(a)*r,Mathf.Cos(a)*r)); }
                    else outline.Add(new Vector2(Mathf.Sin(a)*(shape==ParkFeedbackShape.Leaf?.22f:.5f),Mathf.Cos(a)*.5f));
                }
            }
            // Ear clipping supports the concave heart/star silhouettes.
            float area=0; for(int i=0;i<outline.Count;i++) { var a=outline[i];var b=outline[(i+1)%outline.Count]; area+=a.x*b.y-b.x*a.y; }
            if(area<0)outline.Reverse();
            int n=outline.Count;var vertices=new Vector3[n*2];
            for(int i=0;i<n;i++) { vertices[i]=new Vector3(outline[i].x,outline[i].y,-.08f);vertices[i+n]=new Vector3(outline[i].x,outline[i].y,.08f); }
            var indices=new List<int>();var remaining=new List<int>();for(int i=0;i<n;i++)remaining.Add(i);
            int budget=n*n;
            while(remaining.Count>2 && budget-->0) {
                bool clipped=false;
                for(int j=0;j<remaining.Count;j++) {
                    int a=remaining[(j+remaining.Count-1)%remaining.Count],b=remaining[j],c=remaining[(j+1)%remaining.Count];
                    if(Cross(outline[b]-outline[a],outline[c]-outline[b])<=0)continue;
                    bool inside=false;
                    foreach(int p in remaining)if(p!=a && p!=b && p!=c && Inside(outline[p],outline[a],outline[b],outline[c])) { inside=true;break; }
                    if(inside)continue;
                    indices.AddRange(new[]{a,c,b,a+n,b+n,c+n});remaining.RemoveAt(j);clipped=true;break;
                }
                if(!clipped)throw new System.InvalidOperationException("Feedback polygon could not be triangulated: "+shape);
            }
            for(int i=0;i<n;i++) { int j=(i+1)%n; indices.AddRange(new[]{i,j,j+n,i,j+n,i+n}); }
            mesh=new Mesh { name="ParkFeedback_"+shape };mesh.vertices=vertices;mesh.triangles=indices.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();cache[shape]=mesh;return mesh;
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static bool Inside(Vector2 p,Vector2 a,Vector2 b,Vector2 c)=>Cross(b-a,p-a)>=0 && Cross(c-b,p-b)>=0 && Cross(a-c,p-c)>=0;
    }
}
