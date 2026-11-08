using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// All dimensions in metres. Saved geometry is shared by runtime and previews.
public static class LighterInteriorBuilder
{
    const string Folder="Assets/MagicMR/InteriorGeometry/";
    const string Prefab="Assets/Resources/MagicMR/LighterInterior.prefab";
    static Transform assembly;
    static Material steel, gold, ceramic, red, green, dark, cyan, glass;
    static Vector3 P(float x,float y,float z=0)=>new Vector3(x,y,z)*.001f;
    static Material Mat(string name,Color color,float metal=0,float smooth=.4f,bool transparent=false)
    {
        string path=Folder+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader=Shader.Find(transparent?"MagicMR/TransparentCasing":"Universal Render Pipeline/Lit");
        if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}
        m.shader=shader;m.SetColor("_BaseColor",color);
        if(transparent)m.SetFloat("_EdgeAlpha",.36f);else {m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);}
        EditorUtility.SetDirty(m);return m;
    }
    static Transform Part(string name,PrimitiveType type,Vector3 pos,Vector3 size,Material mat)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(assembly,false);go.transform.localPosition=pos;go.transform.localScale=size;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;return go.transform;
    }
    static Transform Box(string name,Vector3 p,Vector3 size,Material m)=>Part(name,PrimitiveType.Cube,p,size,m);
    static Transform Cylinder(string name,float x,float y,float diameter,float height,Material m,float z=0)=>Part(name,PrimitiveType.Cylinder,P(x,y,z),P(diameter,height*.5f,diameter),m);
    static Transform MeshPart(string name,List<Vector3> vertices,List<int> triangles,Material mat)
    {
        string path=Folder+name+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}
        mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        var go=new GameObject(name);go.transform.SetParent(assembly,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;return go.transform;
    }
    static void Tube(string name,List<Vector3> path,float radius,Material mat)
    {
        var v=new List<Vector3>();var tri=new List<int>();const int sides=8;
        for(int i=0;i<path.Count;i++) {
            Vector3 tangent=(path[Mathf.Min(i+1,path.Count-1)]-path[Mathf.Max(0,i-1)]).normalized;
            var axis=Mathf.Abs(Vector3.Dot(tangent,Vector3.forward))>.95f?Vector3.up:Vector3.forward;
            var a=Vector3.Cross(tangent,axis).normalized;var b=Vector3.Cross(tangent,a).normalized;
            for(int j=0;j<sides;j++){float angle=j*Mathf.PI*2/sides;v.Add(path[i]+radius*(a*Mathf.Cos(angle)+b*Mathf.Sin(angle)));}
            if(i>0)for(int j=0;j<sides;j++){int s=(i-1)*sides+j,t=(i-1)*sides+(j+1)%sides;tri.AddRange(new[]{s,t,s+sides,t,t+sides,s+sides});}
        }
        MeshPart(name,v,tri,mat);
    }
    static void Spring(string name,float x,float bottom,float height,float radius,int turns,float wire,Material m)
    {
        var points=new List<Vector3>();int steps=turns*24;
        for(int i=0;i<=steps;i++){float t=(float)i/steps,a=t*turns*Mathf.PI*2;points.Add(P(x+radius*Mathf.Cos(a),bottom+t*height,radius*Mathf.Sin(a)));}
        Tube(name,points,wire*.001f,m);
    }
    static void Sleeve(string name,float x,float bottom,float height,float radius,Material m)
    {
        // Back half only: the front is an intentional cutaway, exposing the stack.
        var v=new List<Vector3>();var tr=new List<int>();const int n=32;
        for(int ring=0;ring<4;ring++)for(int i=0;i<=n;i++){
            float a=Mathf.PI+i*Mathf.PI/n,r=radius-(ring>=2?.5f:0);float y=bottom+((ring==1||ring==2)?height:0);
            v.Add(P(x+r*Mathf.Cos(a),y,r*Mathf.Sin(a)));
        }
        for(int ring=0;ring<4;ring++)for(int i=0;i<n;i++){int a=ring*(n+1)+i,b=((ring+1)%4)*(n+1)+i;tr.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
        MeshPart(name,v,tr,m);
    }
    public static void Build()
    {
        Directory.CreateDirectory(Folder);Directory.CreateDirectory("Assets/Resources/MagicMR");AssetDatabase.Refresh();
        var root=new GameObject("LighterInterior");assembly=new GameObject("Mechanism").transform;assembly.SetParent(root.transform,false);
        try {
            steel=Mat("Steel",new Color(.68f,.76f,.82f),.65f,.65f);gold=Mat("Brass",new Color(.92f,.56f,.16f),.6f,.6f);
            ceramic=Mat("PiezoCeramic",new Color(.91f,.83f,.55f),0,.3f);red=Mat("HighVoltage",new Color(.95f,.09f,.045f));green=Mat("Ground",new Color(.08f,.83f,.48f));
            dark=Mat("Frame",new Color(.14f,.20f,.27f),.4f);cyan=Mat("Hammer",new Color(.08f,.78f,.86f),.4f);
            glass=Mat("CasingGlass",new Color(.32f,.78f,1f,.045f),0,0,true);
            var fuel=Mat("Butane",new Color(.025f,.36f,.78f),.1f,.65f);
            var vapor=Mat("Vapor",new Color(.28f,.65f,.83f,.12f),0,0,true);
            // Reservoir: blue lower liquid region and separate headspace.
            Box("01_LiquidButane",P(-6,-15,0),P(8.8f,42,6.4f),fuel);
            Box("01_VaporHeadspace",P(-6,8,0),P(8.8f,4,6.4f),vapor);
            Box("ReservoirTop",P(-6,10.5f),P(9.4f,1,7),steel);
            Box("ReservoirBase",P(-6,-36.5f),P(9.4f,1,7),dark);
            Cylinder("02_ValveBody",-6,15,4,8,gold);
            Cylinder("02_ValveSeal",-6,12,4.7f,1,dark);
            Cylinder("02_ValveCollar",-6,19.5f,5.3f,1.5f,steel);
            Cylinder("02_BurnerNozzle",-6,25,2.4f,9,gold);
            Cylinder("NozzleOpening",-6,29.55f,1.4f,.15f,dark);
            Tube("FuelPickup",new List<Vector3>{P(-6,-30,3.4f),P(-6,10,3.4f),P(-6,14,0)},.00045f,gold);
            // Transparent reservoir skin leaves liquid visible from every side.
            Box("ReservoirSkin",P(-6,-12.5f),P(9.3f,47,7),vapor);
            // Actual 3D helical springs, split sleeve, hammer and ceramic stack.
            Sleeve("03_CutawayGuide",5,-32,51,3.7f,dark);
            Cylinder("04_LowerMetalCap",5,-31,6.4f,2,steel);
            Cylinder("05_LowerPiezoCeramic",5,-25,5.3f,10,ceramic);
            Cylinder("06_PhosphorBronzeElectrode",5,-19.4f,6.3f,1,gold);
            Cylinder("07_UpperPiezoCeramic",5,-13.8f,5.3f,10,ceramic);
            Cylinder("08_CeramicProtectiveCap",5,-7.8f,6,2,cyan);
            Spring("09_ReturnSpring",5,-6.2f,6,2.15f,4,.32f,steel);
            Cylinder("10_StrikingHammer",5,1.4f,5,2.8f,cyan);
            Cylinder("HammerShoulder",5,3.1f,6.2f,.7f,cyan);
            Spring("11_PowerSpring",5,3.6f,12.5f,2.2f,9,.33f,steel);
            Cylinder("12_Plunger",5,18,5.5f,3.6f,steel);
            Box("12_PushButton",P(5,21),P(7.4f,2.4f,7.4f),dark);
            var lever=Box("13_ActuatingLever",P(-.7f,23.2f,0),P(16,1.2f,3),steel);lever.localRotation=Quaternion.Euler(0,0,14);
            var pivot=Cylinder("LeverPivot",.5f,23.5f,2,5,gold);pivot.localRotation=Quaternion.Euler(90,0,0);
            // Red electrode lead exits between the two ceramic elements and ends at the nozzle gap.
            Tube("14_HighVoltageLead",new List<Vector3>{P(5,-19.4f,3),P(.2f,-19.4f,3),P(.2f,30,3),P(-3.7f,30,1)},.00042f,red);
            Tube("15_GroundReturn",new List<Vector3>{P(5,-32,0),P(8.8f,-32,0),P(8.8f,-34.5f,0),P(-.9f,-34.5f,0),P(-.9f,15,-1.6f),P(-6,15,-1.6f)},.00035f,green);
            Cylinder("IgnitionElectrodeTip",-3.7f,30,1,.8f,steel,1);
            // Ghost the exact original meshes, including the rounded silhouette.
            var shell=new GameObject("TransparentShell").transform;shell.SetParent(root.transform,false);
            foreach(string name in new[]{"RoundedBody","WindShield","SerratedWheel","Collar","ThumbLever","BaseSeam"}) {
                var go=new GameObject(name);go.transform.SetParent(shell,false);go.transform.localScale=P(25,80,12);
                go.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/MagicMR/LighterGeometry/"+name+".asset");
                var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=glass;r.shadowCastingMode=ShadowCastingMode.Off;
            }
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);AssetDatabase.SaveAssets();
        } finally {UnityEngine.Object.DestroyImmediate(root);}
        Debug.Log("PIEZOELECTRIC_INTERIOR_BUILT");
    }
    public static void Preview()
    {
        Build();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.7f,.7f,.7f);
        var key=new GameObject("Key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.5f;key.transform.rotation=Quaternion.Euler(30,-25,0);
        var camera=new GameObject("Preview").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.075f,.11f);camera.orthographic=true;camera.nearClipPlane=.001f;camera.farClipPlane=10;camera.orthographicSize=.135f;
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="VR Lighter";go.transform.localScale=P(25,80,12);LighterModelBuilder.Apply(go);
        var editor=go.AddComponent<MagicMR.RealityEditor>();typeof(MagicMR.RealityEditor).GetMethod("Awake",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(editor,null);
        for(int i=0;i<4;i++)editor.TriggerScale();
        if(editor.InteriorVisible)throw new Exception("Interior revealed before final action");
        editor.TriggerScale();if(!editor.InteriorVisible)throw new Exception("Interior not revealed");
        var original=go.GetComponent<Renderer>();if(original.enabled)throw new Exception("Opaque shell hides interior");
        Directory.CreateDirectory(".codex-tmp/lighter-interior");
        camera.transform.position=new Vector3(.24f,.10f,.5f);camera.transform.LookAt(Vector3.zero);Capture(camera,"transparent-perspective");
        camera.transform.position=new Vector3(0,0,.5f);camera.transform.LookAt(Vector3.zero);Capture(camera,"transparent-front");
        camera.transform.position=new Vector3(-.3f,.1f,-.5f);camera.transform.LookAt(Vector3.zero);Capture(camera,"transparent-back");
        editor.ResetTarget();if(editor.InteriorVisible||!original.enabled||original.sharedMaterial.GetColor("_BaseColor").r<.9f)throw new Exception("Reset did not restore original");
        for(int i=0;i<5;i++)editor.TriggerScale();if(!editor.InteriorVisible)throw new Exception("Interior failed to reopen");
        if(go.transform.Find("LighterInterior_Piezoelectric").GetComponentsInChildren<MeshFilter>().Length<30)throw new Exception("Interior parts missing");
        Debug.Log("INTERIOR_SCALE_REVEAL_RESET_REOPEN_PASS");
    }
    static void Capture(Camera c,string name)
    {
        var rt=new RenderTexture(1200,1500,24);rt.antiAliasing=4;c.targetTexture=rt;var old=RenderTexture.active;
        try{c.Render();c.Render();RenderTexture.active=rt;var t=new Texture2D(1200,1500,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1200,1500),0,0);t.Apply();File.WriteAllBytes(".codex-tmp/lighter-interior/"+name+".png",t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);}
        finally{c.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(rt);}
    }
}
