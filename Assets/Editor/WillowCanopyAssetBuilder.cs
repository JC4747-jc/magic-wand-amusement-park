using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class WillowCanopyAssetBuilder
{
    public const string Folder="Assets/MagicMR/Willow";
    public const string DataPath="Assets/Resources/MagicMR/NaturalModels/WillowCanopy.asset";
    sealed class Batch
    {
        public readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Vector2> uv=new List<Vector2>();readonly List<Color32> colors=new List<Color32>();
        readonly List<BoneWeight> weights=new List<BoneWeight>();readonly List<int> triangles=new List<int>();
        readonly SkinnedMeshRenderer skin;readonly Transform growth;readonly int root;
        public Batch(SkinnedMeshRenderer source,Transform parent){skin=source;growth=parent;root=Array.FindIndex(skin.bones,b=>!b.name.StartsWith("Branch_"));if(root<0)throw new Exception("Tree root bone absent");}
        public int Bone(int sector){int value=Array.FindIndex(skin.bones,b=>b.name=="Branch_"+sector.ToString("D2"));if(value<0)throw new Exception("Tree sector absent");return value;}
        int Vertex(Vector3 p,Vector2 tex,Color32 color,int bone,float weight){int index=vertices.Count;vertices.Add(p);uv.Add(tex);colors.Add(color);weights.Add(new BoneWeight{boneIndex0=bone,weight0=weight,boneIndex1=root,weight1=1-weight});return index;}
        void Tri(int a,int b,int c){triangles.Add(a);triangles.Add(b);triangles.Add(c);}
        public void Tube(Func<float,Vector3> curve,float radius,int segments,int sides,int sector,float support)
        {
            int start=vertices.Count,bone=Bone(sector);
            for(int j=0;j<=segments;j++){
                float t=j/(float)segments;var p=curve(t);var tangent=(curve(Mathf.Min(1,t+.005f))-curve(Mathf.Max(0,t-.005f))).normalized;
                var normal=Vector3.Cross(tangent,Vector3.right).normalized;if(normal.sqrMagnitude<.5f)normal=Vector3.Cross(tangent,Vector3.forward).normalized;
                var side=Vector3.Cross(tangent,normal).normalized;float width=radius*Mathf.Lerp(1,.08f,t);
                for(int k=0;k<sides;k++){float a=k/(float)sides*Mathf.PI*2;Vertex(p+(normal*Mathf.Cos(a)+side*Mathf.Sin(a))*width,new Vector2(k/(float)sides,t*8),Color.white,bone,Mathf.Lerp(support,1,t));}
            }
            for(int j=0;j<segments;j++)for(int k=0;k<sides;k++){int a=start+j*sides+k,b=start+j*sides+(k+1)%sides,c=a+sides,d=b+sides;Tri(a,c,b);Tri(b,c,d);}
        }
        public void Leaf(Vector3 origin,Vector3 direction,Vector3 normal,float length,float width,int sector,float weight,Color32 tint)
        {
            direction.Normalize();normal=Vector3.ProjectOnPlane(normal,direction).normalized;var across=Vector3.Cross(direction,normal).normalized;
            int start=vertices.Count,bone=Bone(sector);
            Vertex(origin,Vector2.zero,tint,bone,weight);
            foreach(float t in new[]{.32f,.72f}){
                var center=origin+direction*(length*t)+normal*(length*.05f*Mathf.Sin(t*Mathf.PI));float half=width*(t<.5f?.5f:.32f);
                Vertex(center-across*half-normal*(width*.10f),new Vector2(0,t),tint,bone,weight);
                Vertex(center+normal*(width*.10f),new Vector2(.5f,t),tint,bone,weight);
                Vertex(center+across*half-normal*(width*.10f),new Vector2(1,t),tint,bone,weight);
            }
            Vertex(origin+direction*length-normal*length*.025f,Vector2.one,tint,bone,weight);
            foreach(var tri in new[]{new[]{0,1,2},new[]{0,2,3},new[]{1,4,2},new[]{2,4,5},new[]{2,5,3},new[]{3,5,6},new[]{4,7,5},new[]{5,7,6}})Tri(start+tri[0],start+tri[1],start+tri[2]);
        }
        public Mesh Save(string name)
        {
            if(vertices.Count>60000||triangles.Count/3>40000)throw new Exception("Willow per-mesh geometry budget exceeded");
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt16};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.boneWeights=weights.ToArray();
            // New renderers use TreeGrowth's metric frame, independently of the FBX renderer's axis/scale bake.
            mesh.bindposes=skin.bones.Select(b=>b.worldToLocalMatrix*growth.localToWorldMatrix).ToArray();
            mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            var bounds=mesh.bounds;var source=skin.sharedMesh.bounds;
            for(int corner=0;corner<8;corner++){var p=source.center+Vector3.Scale(source.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));bounds.Encapsulate(growth.InverseTransformPoint(skin.transform.TransformPoint(p)));}
            mesh.bounds=bounds;
            AssetDatabase.CreateAsset(mesh,Folder+"/"+name+".asset");return mesh;
        }
    }
    public static void Run()
    {
        if(AssetDatabase.LoadAssetAtPath<WillowCanopyData>(DataPath))throw new Exception("Willow assets already exist; preserve reviewed revision before rebuilding");
        Directory.CreateDirectory(Folder);Directory.CreateDirectory(".codex-tmp/willow-canopy");AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewPreviewScene();var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/MagicMR/NaturalModels/SmallTree.prefab"));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(source,scene);
        try{
            var growth=source.transform.Find("TreeGrowth");var skins=source.GetComponentsInChildren<SkinnedMeshRenderer>();var leaves=skins.Single(s=>s.name=="Leaves");var branches=skins.Single(s=>s!=leaves);
            var bounds=new Bounds();bool first=true;
            foreach(var renderer in source.GetComponentsInChildren<Renderer>()){
                var mesh=renderer is SkinnedMeshRenderer sk?sk.sharedMesh:renderer.GetComponent<MeshFilter>().sharedMesh;var box=mesh.bounds;
                for(int corner=0;corner<8;corner++){var v=box.center+Vector3.Scale(box.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));var p=growth.InverseTransformPoint(renderer.transform.TransformPoint(v));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
            }
            float height=bounds.size.y,radius=Mathf.Min(bounds.extents.x,bounds.extents.z)*.83f;var center=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            var stemBatch=new Batch(branches,growth);var leafBatch=new Batch(leaves,growth);var random=new System.Random(4747);int leafCount=0,fronds=0;
            float Range(float a,float b)=>(float)(a+random.NextDouble()*(b-a));
            for(int sector=0;sector<5;sector++){
                float mainAngle=sector*Mathf.PI*2/5+.2f;var radial=new Vector3(Mathf.Cos(mainAngle),0,Mathf.Sin(mainAngle));
                Vector3 Main(float t)=>center+radial*(radius*.88f*t)+Vector3.up*height*(.46f+.30f*t+.06f*Mathf.Sin(t*Mathf.PI));
                stemBatch.Tube(Main,height*.006f,12,7,sector,0);
                for(int f=0;f<20;f++){
                    float angle=mainAngle+Range(-.60f,.60f),r=radius*Range(.30f,.92f),top=height*Range(.69f,.91f),drop=height*Range(.30f,.62f),curl=Range(-.08f,.08f)*radius;
                    var outward=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var around=Vector3.Cross(Vector3.up,outward);
                    var origin=center+outward*r+Vector3.up*top;
                    var start=Main(Mathf.Clamp01(r/(radius*.88f)));
                    Vector3 Connector(float t)=>Vector3.Lerp(start,origin,t)+Vector3.up*height*.015f*Mathf.Sin(t*Mathf.PI);
                    stemBatch.Tube(Connector,height*.0018f,5,5,sector,.5f);
                    Vector3 Frond(float t)=>origin+outward*(radius*.05f*Mathf.Sin(t*Mathf.PI))+around*(curl*t*t)-Vector3.up*(drop*Mathf.Pow(t,1.15f));
                    stemBatch.Tube(Frond,height*.0010f,16,5,sector,.60f);fronds++;
                    for(int n=0;n<24;n++){
                        float t=(n+.6f)/25f;var position=Frond(t);float ph=n*2.399963f+f*.47f;
                        var side=(outward*Mathf.Cos(ph)+around*Mathf.Sin(ph)).normalized;
                        var direction=(side*.75f+Vector3.down*.65f).normalized;
                        float length=height*Range(.032f,.061f)*Mathf.Lerp(1,.65f,t),width=length*Range(.115f,.17f);
                        float variation=Range(.82f,1.18f);var colour=new Color(.35f*variation,.51f*variation,.20f*variation,Range(.005f,.995f));
                        leafBatch.Leaf(position,direction,side+Vector3.up*.25f,length,width,sector,Mathf.Lerp(.60f,1,t),colour);leafCount++;
                    }
                }
            }
            var stemMesh=stemBatch.Save("WillowBranches");var leafMesh=leafBatch.Save("WillowLeaves");
            var shader=Shader.Find("MagicMR/NaturalTreeLeaves");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Willow leaf shader failed");
            var material=new Material(shader){name="Willow narrow leaves"};material.SetColor("_BaseColor",Color.white);material.SetFloat("_LeafVertexTint",1);material.SetFloat("_BumpScale",0);material.SetFloat("_LeafCoverage",1);material.SetFloat("_Cutoff",.4f);
            AssetDatabase.CreateAsset(material,Folder+"/WillowLeaves.mat");
            var data=ScriptableObject.CreateInstance<WillowCanopyData>();data.leaves=leafMesh;data.branches=stemMesh;data.leafMaterial=material;data.fronds=fronds;data.leafCount=leafCount;AssetDatabase.CreateAsset(data,DataPath);AssetDatabase.SaveAssets();
            File.WriteAllText(".codex-tmp/willow-canopy/build.json",JsonUtility.ToJson(new BuildInfo{fronds=fronds,leaves=leafCount,leafVertices=leafMesh.vertexCount,branchVertices=stemMesh.vertexCount,leafTriangles=leafMesh.triangles.Length/3,branchTriangles=stemMesh.triangles.Length/3,sourceNominalBounds=bounds,seed=4747},true));
            Debug.Log("WILLOW_CANOPY_BUILD_PASS: "+fronds+" drooping fronds, "+leafCount+" lanceolate leaves, existing 5-sector skin/trunk; original authored geometry, no texture download");
        }finally{UnityEngine.Object.DestroyImmediate(source);EditorSceneManager.ClosePreviewScene(scene);}
    }
    public static void ReviseCandidate()
    {
        var data=AssetDatabase.LoadAssetAtPath<WillowCanopyData>(DataPath);
        if(!data||data.fronds!=100||data.leafCount!=2400||data.leaves.name!="WillowLeaves"||data.branches.name!="WillowBranches")throw new Exception("Refuse to replace an unrecognized candidate");
        string backup=".codex-tmp/willow-canopy/revision-02";if(Directory.Exists(backup))throw new Exception("Revision backup already exists; preserve it and use a new revision name");Directory.CreateDirectory(backup);
        string[] owned={DataPath,Folder+"/WillowBranches.asset",Folder+"/WillowLeaves.asset",Folder+"/WillowLeaves.mat"};
        foreach(string path in owned){if(!File.Exists(path)||!File.Exists(path+".meta"))throw new Exception("Candidate file/meta missing: "+path);File.Copy(path,backup+"/"+Path.GetFileName(path));File.Copy(path+".meta",backup+"/"+Path.GetFileName(path)+".meta");}
        foreach(string path in owned)if(!AssetDatabase.DeleteAsset(path))throw new Exception("Unable to replace owned candidate: "+path);
        Run();
    }
    [Serializable]sealed class BuildInfo {public int fronds,leaves,leafVertices,branchVertices,leafTriangles,branchTriangles,seed;public Bounds sourceNominalBounds;}
}
