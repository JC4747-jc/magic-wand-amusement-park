using System.Collections.Generic;
using UnityEngine;

namespace MagicMR
{
    // Original bounded relief geometry for the virtual window, not room reconstruction.
    internal static class RoomWindowLandscape
    {
        public static Mesh CreateMesh()
        {
            var vertices=new List<Vector3>();var colors=new List<Color>();
            var uv=new List<Vector2>();var kinds=new List<Vector2>();var indices=new List<int>();
            int Vertex(float x,float y,float z,Color color,float kind){
                int n=vertices.Count;vertices.Add(new Vector3(x,y,z));colors.Add(color);
                uv.Add(new Vector2(x/.32f+.5f,y/.516f));kinds.Add(new Vector2(kind,0));return n;
            }
            void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color){
                indices.Add(Vertex(a.x,a.y,a.z,color,1));indices.Add(Vertex(b.x,b.y,b.z,color,1));indices.Add(Vertex(c.x,c.y,c.z,color,1));
            }
            void Quad(float left,float right,float bottom,float top,float z,Color color,float kind){
                int a=Vertex(left,bottom,z,color,kind),b=Vertex(left,top,z,color,kind);
                int c=Vertex(right,top,z,color,kind),d=Vertex(right,bottom,z,color,kind);
                indices.AddRange(new[]{a,b,c,a,c,d});
            }
            void Ridge(System.Func<float,float> height,float bottom,float z,Color low,Color high){
                const int segments=48;int first=vertices.Count;
                for(int i=0;i<=segments;i++){
                    float x=-.16f+.32f*i/segments;
                    Vertex(x,bottom,z,low,1);Vertex(x,height(x),z,high,1);
                    if(i>0){int a=first+(i-1)*2,b=a+1,c=a+3,d=a+2;indices.AddRange(new[]{a,b,c,a,c,d});}
                }
            }
            float Mountain(float x){
                float a=(x+.045f)/.054f,b=(x-.108f)/.035f;
                return .278f+.086f*Mathf.Exp(-a*a)+.051f*Mathf.Exp(-b*b)+.009f*Mathf.Sin(x*78);
            }
            Quad(-.16f,.16f,.002f,.516f,.012f,Color.white,0);
            Ridge(Mountain,.145f,.006f,new Color(.28f,.43f,.43f),new Color(.20f,.34f,.37f));
            // Snow follows the actual mountain silhouette, with a broken lower edge.
            for(int i=0;i<48;i++){
                float x=-.16f+.32f*i/48,x1=-.16f+.32f*(i+1)/48;
                float y=Mountain(x),y1=Mountain(x1);
                if(y>.329f&&y1>.329f){
                    float snow=.011f+.007f*(.5f+.5f*Mathf.Sin(i*2.7f));
                    var color=new Color(.76f,.82f,.77f);
                    Triangle(new Vector3(x,y,.005f),new Vector3(x1,y1,.005f),new Vector3(x1,y1-snow,.005f),color);
                    Triangle(new Vector3(x,y,.005f),new Vector3(x1,y1-snow,.005f),new Vector3(x,y-snow*.7f,.005f),color);
                }
            }
            Ridge(x=>.213f+.024f*Mathf.Sin(x*19+1)+.012f*Mathf.Cos(x*43),.125f,.002f,
                new Color(.11f,.29f,.29f),new Color(.14f,.33f,.31f));
            Quad(-.16f,.16f,.002f,.192f,.001f,Color.white,2);
            // Near banks and trees have real local depth, within the original inset budget.
            Ridge(x=>.045f+.112f*Mathf.Pow(Mathf.Abs(x)/.16f,2.1f),.002f,-.004f,
                new Color(.035f,.13f,.12f),new Color(.08f,.23f,.18f));
            foreach(float x in new[]{-.147f,-.121f,.125f,.148f}){
                float h=.12f+.055f*(.5f+.5f*Mathf.Sin(x*93));float bottom=.112f;
                Quad(x-.002f,x+.002f,bottom,bottom+h,-.006f,new Color(.10f,.13f,.09f),1);
                for(int tier=0;tier<3;tier++){
                    float y=bottom+h*(.24f+tier*.23f),width=.022f*(1-tier*.21f);
                    Triangle(new Vector3(x-width,y,-.008f),new Vector3(x,y+h*.40f,-.008f),new Vector3(x+width,y,-.008f),
                        new Color(.035f+tier*.009f,.15f+tier*.022f,.12f+tier*.01f));
                }
            }
            var mesh=new Mesh{name="Owned room window mountain-lake relief"};
            mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetUVs(1,kinds);
            mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
            return mesh;
        }
    }
}
