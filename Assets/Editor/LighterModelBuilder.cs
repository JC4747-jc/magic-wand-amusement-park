using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class LighterModelBuilder
{
    const string Folder="Assets/MagicMR/LighterGeometry";
    static readonly Vector3 Units=new Vector3(.025f,.08f,.012f);
    static Vector3 Local(Vector3 p)=>new Vector3(p.x/Units.x,p.y/Units.y,p.z/Units.z);
    static Mesh Save(string name,List<Vector3> vertices,List<int> triangles)
    {
        string path=Folder+"/"+name+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}
        mesh.Clear(); mesh.name=name; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh); return mesh;
    }
    static Mesh Box(string name,Vector3 center,Vector3 size,float radius)
    {
        var vertices=new List<Vector3>(); var triangles=new List<int>();
        int segments=48;
        // Bevel the upper/lower rims as well as the four vertical corners.
        for(int ring=0;ring<6;ring++)
        {
            float[] levels={-.5f,-.49f,-.46f,.46f,.49f,.5f};
            float inset=ring==0||ring==5?radius*.5f:ring==1||ring==4?radius*.15f:0;
            float halfX=size.x*.5f-inset,halfZ=size.z*.5f-inset;
            float r=Mathf.Min(radius,Mathf.Min(halfX,halfZ));
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments;
                float x=Mathf.Sign(Mathf.Cos(a))*(halfX-r)+Mathf.Cos(a)*r;
                float z=Mathf.Sign(Mathf.Sin(a))*(halfZ-r)+Mathf.Sin(a)*r;
                vertices.Add(Local(center+new Vector3(x,levels[ring]*size.y,z)));
            }
        }
        for(int row=0;row<5;row++) for(int i=0;i<segments;i++)
        {int a=row*segments+i,b=row*segments+(i+1)%segments; triangles.AddRange(new[]{a,a+segments,b,b,a+segments,b+segments});}
        int bottom=vertices.Count; vertices.Add(Local(center-Vector3.up*size.y*.5f));
        int top=vertices.Count; vertices.Add(Local(center+Vector3.up*size.y*.5f));
        for(int i=0;i<segments;i++)
        {int next=(i+1)%segments;triangles.AddRange(new[]{bottom,i,next,top,5*segments+next,5*segments+i});}
        return Save(name,vertices,triangles);
    }
    static Mesh Wheel()
    {
        var v=new List<Vector3>(); var tr=new List<int>(); const int count=96;
        for(int side=0;side<2;side++) for(int i=0;i<count;i++)
        {
            float a=i*Mathf.PI*2/count,r=i%2==0?.0048f:.0043f;
            v.Add(Local(new Vector3((side==0?-1:1)*.0045f,.034f+Mathf.Cos(a)*r,Mathf.Sin(a)*r)));
        }
        for(int i=0;i<count;i++) {int n=(i+1)%count; tr.AddRange(new[]{i,n,i+count,n,n+count,i+count});}
        for(int side=0;side<2;side++)
        {
            int c=v.Count; v.Add(Local(new Vector3((side==0?-1:1)*.0045f,.034f,0)));
            for(int i=0;i<count;i++){int a=side*count+i,b=side*count+(i+1)%count;tr.AddRange(side==0?new[]{c,b,a}:new[]{c,a,b});}
        }
        return Save("SerratedWheel",v,tr);
    }
    static Material Material(string name,Color color,float metallic,float smoothness)
    {
        string path=Folder+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smoothness);
        EditorUtility.SetDirty(m); return m;
    }
    static void Part(Transform parent,string name,Mesh mesh,Material material)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);
        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    public static void Apply(GameObject root)
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        foreach(string old in new[]{"LighterCap","LighterWheel","RealisticDetails"})
        {var child=root.transform.Find(old);if(child!=null)UnityEngine.Object.DestroyImmediate(child.gameObject);}
        var blue=Material("BlueGlossPlastic",new Color(.045f,.12f,.30f),0,.65f);
        var metal=Material("BrushedSteel",new Color(.64f,.67f,.70f),.72f,.58f);
        var dark=Material("DarkRecess",new Color(.025f,.028f,.033f),.1f,.28f);
        var red=Material("IgnitionLever",new Color(.38f,.035f,.025f),0,.42f);
        root.GetComponent<MeshFilter>().sharedMesh=Box("RoundedBody",new Vector3(0,-.0085f,0),new Vector3(.025f,.063f,.012f),.0028f);
        root.GetComponent<MeshRenderer>().sharedMaterial=blue;
        var group=new GameObject("RealisticDetails").transform;group.SetParent(root.transform,false);
        Part(group,"Collar",Box("Collar",new Vector3(0,.023f,0),new Vector3(.0247f,.002f,.0122f),.002f),dark);
        Part(group,"WindShield",Box("WindShield",new Vector3(0,.029f,.002f),new Vector3(.023f,.012f,.008f),.0015f),metal);
        Part(group,"FlintWheel",Wheel(),metal);
        Part(group,"ThumbLever",Box("ThumbLever",new Vector3(0,.027f,-.0045f),new Vector3(.011f,.004f,.0035f),.0008f),red);
        for(int i=0;i<5;i++)
            Part(group,"Vent"+i,Box("Vent"+i,new Vector3((i-2)*.0038f,.0295f,.00605f),new Vector3(.0014f,.0038f,.00015f),.0004f),dark);
        Part(group,"BaseSeam",Box("BaseSeam",new Vector3(0,-.0385f,0),new Vector3(.024f,.0012f,.0112f),.0023f),dark);
        var collider=root.GetComponent<BoxCollider>();
        if(collider!=null){collider.center=Vector3.zero;collider.size=Vector3.one;}
        AssetDatabase.SaveAssets();
    }
    public static void Preview()
    {
        FlowerModelBuilder.Preview();
        var old=UnityEngine.Object.FindFirstObjectByType<Camera>();
        var flower=GameObject.Find("Flower(Clone)"); if(flower!=null)UnityEngine.Object.DestroyImmediate(flower);
        var root=GameObject.CreatePrimitive(PrimitiveType.Cube);root.name="Lighter preview";root.transform.localScale=Units;
        Apply(root);root.transform.position=Vector3.up*.04f;
        UnityEngine.Object.FindFirstObjectByType<Light>().transform.rotation=Quaternion.Euler(35,145,0);
        old.transform.position=new Vector3(.085f,.075f,.20f);old.transform.LookAt(Vector3.up*.041f);old.orthographicSize=.052f;
        var rt=new RenderTexture(800,1000,24);old.targetTexture=rt;var previous=RenderTexture.active;
        try
        {
            old.Render();old.Render();RenderTexture.active=rt;
            var tex=new Texture2D(800,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,800,1000),0,0);tex.Apply();
            File.WriteAllBytes(".codex-tmp/realistic-lighter-preview.png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
        }
        finally{old.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(rt);}
        root.SetActive(false);
        var editor=root.AddComponent<MagicMR.RealityEditor>();
        var support=new Vector3(.2f,.7f,-.1f);
        editor.RegisterVisualBounds(support,.08f);
        var all=root.GetComponentsInChildren<MeshRenderer>(true);
        Bounds bounds=all[0].bounds;
        foreach(var renderer in all) bounds.Encapsulate(renderer.bounds);
        if(Mathf.Abs(bounds.min.y-support.y)>.0001f || Mathf.Abs(bounds.size.y-.08f)>.0001f)
            throw new Exception("Detailed lighter lost height or bottom registration");
        var bodyMaterial=root.GetComponent<MeshRenderer>().sharedMaterial;
        root.transform.position+=Vector3.right*.1f;
        editor.ResetTarget();
        if(root.GetComponent<MeshRenderer>().sharedMaterial!=bodyMaterial)
            throw new Exception("Detailed lighter reset lost its plastic material");
        Debug.Log("DETAILED_LIGHTER_REGISTRATION_PASS");
    }
    public static void PreviewAndBuild() { Preview(); BridgeStereoBuild.Build(); }
}
