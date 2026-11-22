using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class WillowCanopyChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly string Output=".codex-tmp/willow-canopy";
    static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
    static object Call(ParkActor actor,string method,params object[] args)=>typeof(ParkActor).GetMethod(method,Private).Invoke(actor,args);
    static void ValidateSkin(Mesh mesh,int bones){
        Require(mesh.vertexCount<60000&&mesh.triangles.Length/3<40000,"Canopy mesh budget exceeded");
        Require(mesh.bindposes.Length==bones&&mesh.boneWeights.Length==mesh.vertexCount,"Skin binding incomplete");
        foreach(var w in mesh.boneWeights)Require(w.boneIndex0>=0&&w.boneIndex0<bones&&w.boneIndex1>=0&&w.boneIndex1<bones&&Mathf.Abs(w.weight0+w.weight1-1)<1e-5f,"Invalid skin weights");
        Require(mesh.normals.Length==mesh.vertexCount&&mesh.normals.All(n=>float.IsFinite(n.x)&&float.IsFinite(n.y)&&float.IsFinite(n.z)&&n.sqrMagnitude>.5f),"Nonfinite/zero normals");
        Require(mesh.tangents.Length==mesh.vertexCount&&mesh.tangents.All(t=>float.IsFinite(t.x)&&float.IsFinite(t.y)&&float.IsFinite(t.z)&&new Vector3(t.x,t.y,t.z).sqrMagnitude>.5f),"Nonfinite/zero tangents");
    }
    static void Capture(Camera camera,string name){
        var rt=new RenderTexture(1000,1000,24);var pixels=new Texture2D(1000,1000,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();camera.Render();camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1000,1000),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
    }
    public static void Run(){
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING\n");
        string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;bool async=ShaderUtil.allowAsyncCompilation;Mesh baked=null;Material floorMaterial=null;
        try{
            ShaderUtil.allowAsyncCompilation=false;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            baked=new Mesh();
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.43f,.46f,.50f);
            var key=new GameObject("Willow studio key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.5f;key.transform.rotation=Quaternion.Euler(38,-35,0);
            var fill=new GameObject("Willow studio fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.4f;fill.transform.rotation=Quaternion.Euler(55,145,0);
            floorMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));floorMaterial.SetColor("_BaseColor",new Color(.20f,.22f,.23f));
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Review ground";floor.transform.localScale=new Vector3(1.4f,.01f,1.4f);floor.transform.position=Vector3.down*.007f;floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/ParkTree.prefab"));go.SetActive(true);var actor=go.GetComponent<ParkActor>();actor.Build();actor.label.gameObject.SetActive(false);
            Require(actor.ConfirmOrientation(23)&&actor.FitRepresentation(Vector3.zero,Vector3.up*.6f,true),"Nominal fitting failed");
            var fit=go.transform.Find("Manual fit - separate from tracking and animation");var fitPosition=fit.localPosition;var fitScale=fit.localScale;var root=go.transform.localToWorldMatrix;
            var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();
            var seasonLeaves=skins.Single(s=>s.name=="Leaves");var seasonBranches=skins.Single(s=>s!=seasonLeaves&&!s.name.StartsWith("Willow "));
            var leaves=skins.Single(s=>s.name=="Willow leaves");var branches=skins.Single(s=>s.name=="Willow branches");
            var trunk=actor.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name=="Trunk");var trunkMatrix=trunk.localToWorldMatrix;var trunkMesh=trunk.GetComponent<MeshFilter>().sharedMesh;
            var originalLeaves=seasonLeaves.sharedMesh;var originalBranches=seasonBranches.sharedMesh;var originalMaterial=seasonLeaves.sharedMaterial;
            var camera=new GameObject("Willow model review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.40f;camera.nearClipPlane=.01f;camera.backgroundColor=new Color(.13f,.16f,.18f);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.transform.position=new Vector3(.65f,.42f,-1.2f);camera.transform.LookAt(Vector3.up*.30f);Capture(camera,"00-season-tree-reference");
            Require((bool)Call(actor,"SetTreeExperience",true),"Willow selection rejected");
            var data=Resources.Load<WillowCanopyData>("MagicMR/NaturalModels/WillowCanopy");Require(leaves.enabled&&branches.enabled&&!seasonLeaves.enabled&&!seasonBranches.enabled&&leaves.sharedMesh==data.leaves&&branches.sharedMesh==data.branches&&leaves.sharedMaterial==data.leafMaterial,"Willow canopy not applied");
            leaves.BakeMesh(baked);
            File.WriteAllText(Output+"/binding.txt","mesh="+data.leaves.bounds+"\nskin="+leaves.localBounds+"\nworldbounds="+leaves.bounds+"\nbaked="+baked.bounds+"\nrenderer="+leaves.transform.localToWorldMatrix+"\nroot="+leaves.rootBone.localToWorldMatrix+"\nraw="+string.Join(";",data.leaves.vertices.Take(3).Select(v=>v.ToString("F4")))+"\nbaked vertices="+string.Join(";",baked.vertices.Take(3).Select(v=>v.ToString("F4")))+"\nbones="+string.Join(";",leaves.bones.Select(b=>b.name+":"+b.localScale+":"+b.position))+"\nold bind="+originalLeaves.bindposes[0]+"\nnew bind="+data.leaves.bindposes[0]+"\n");
            Require(data.fronds==100&&data.leafCount==2400,"Authored frond/leaf set incomplete");ValidateSkin(data.leaves,leaves.bones.Length);ValidateSkin(data.branches,branches.bones.Length);
            Require(data.leaves.colors32.Select(c=>c.a).Distinct().Count()>200&&data.leafMaterial.GetFloat("_LeafVertexTint")==1&&!ShaderUtil.ShaderHasError(data.leafMaterial.shader),"Leaf shape/colour shader invalid");
            Require(go.transform.localToWorldMatrix==root&&trunk.localToWorldMatrix==trunkMatrix&&trunk.GetComponent<MeshFilter>().sharedMesh==trunkMesh&&fit.localPosition==fitPosition&&fit.localScale==fitScale&&actor.sizeConfirmed&&actor.orientationConfirmed,"Variant changed placement/trunk");
            Capture(camera,"01-willow-front");camera.transform.position=new Vector3(-1.1f,.40f,.75f);camera.transform.LookAt(Vector3.up*.30f);Capture(camera,"02-willow-reverse");
            camera.transform.position=new Vector3(.65f,.42f,-1.2f);camera.transform.LookAt(Vector3.up*.30f);Call(actor,"ApplyComponentColors",1f);Capture(camera,"03-willow-dark");Call(actor,"ApplyComponentColors",0f);
            leaves.BakeMesh(baked);var rest=baked.vertices;
            var stateField=typeof(ParkActor).GetField("state",Private);var state=stateField.GetValue(actor);state.GetType().GetField("wind").SetValue(state,true);stateField.SetValue(actor,state);
            typeof(ParkActor).GetField("treeGustUntil",Private).SetValue(actor,Time.unscaledTime+2.4f);actor.windStrength=.6f;actor.windDirection=Vector3.right;
            Call(actor,"StepTreeWind",.25f,Time.unscaledTime+.2f);leaves.BakeMesh(baked);var swung=baked.vertices;
            int moved=swung.Where((p,i)=>(p-rest[i]).sqrMagnitude>1e-7f).Count();Require(moved>rest.Length*.2f,"New willow skin did not deform");
            Require(swung.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)&&leaves.localBounds.Contains(p)),"Lash leaves escape finite culling envelope: "+baked.bounds+" vs "+leaves.localBounds);
            Require(trunk.localToWorldMatrix==trunkMatrix&&go.transform.localToWorldMatrix==root,"Lash moved fixed trunk/root");
            actor.windDirection=new Vector3(float.NaN,0,0);Call(actor,"StepTreeWind",.25f,Time.unscaledTime+.3f);leaves.BakeMesh(baked);Require(baked.vertices.Zip(rest,(a,b)=>(a-b).sqrMagnitude).Max()<1e-7f,"Invalid wind did not restore canopy bind pose");
            int meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length,materials=Resources.FindObjectsOfTypeAll<Material>().Length;
            for(int i=0;i<12;i++){
                bool fantasy=i%2==1;Require((bool)Call(actor,"SetTreeExperience",fantasy),"Repeated variant switch rejected");
                Require(leaves.enabled==fantasy&&branches.enabled==fantasy&&seasonLeaves.enabled!=fantasy&&seasonBranches.enabled!=fantasy,"Canopy visibility not switched");
                Require(leaves.sharedMesh==data.leaves&&branches.sharedMesh==data.branches&&leaves.sharedMaterial==data.leafMaterial&&seasonLeaves.sharedMesh==originalLeaves&&seasonBranches.sharedMesh==originalBranches&&seasonLeaves.sharedMaterial==originalMaterial,"Original or willow asset identity changed");
                Require(actor.sizeConfirmed&&actor.orientationConfirmed&&fit.localPosition==fitPosition&&fit.localScale==fitScale&&go.transform.localToWorldMatrix==root,"Repeated choice lost nominal fit");
            }
            Require(Resources.FindObjectsOfTypeAll<Mesh>().Length==meshes&&Resources.FindObjectsOfTypeAll<Material>().Length==materials,"Switch allocated mesh/material copies");
            actor.ResetEffects();Require(actor.magicTree&&leaves.enabled&&!seasonLeaves.enabled&&!actor.Busy,"Reset discarded chosen willow or left motion");
            Require(!actor.sizeConfirmed&&!actor.orientationConfirmed&&fit.localScale==Vector3.one,"Full Reset did not clear calibration");var resetTrunk=trunk.localToWorldMatrix;
            go.SetActive(false);go.SetActive(true);Require(leaves.enabled&&!seasonLeaves.enabled&&trunk.localToWorldMatrix==resetTrunk,"Disable/re-enable damaged shared willow/trunk");
            File.WriteAllText(Output+"/result.txt","PASS: authored willow 100 drooping fronds / 2400 narrow folded leaves; finite two-bone bindings/normals/tangents, three active / five cached body renderers using existing five bone sectors; actual leaf skin deformed "+moved+"/"+rest.Length+" vertices; trunk/root/fit stable during variant switches and lash; invalid-wind recovery; 12 mode switches preserve exact original and willow meshes/materials and allocate no copies; full Reset clears calibration as specified; Reset/disable retain variant. Four actual Unity studio renders; lash geometry verified by BakeMesh, timed Play Mode renders required separately. No real YOLO/PICO or Android acceptance.\n");
        }catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL: "+e);throw;}
        finally{UnityEngine.Object.DestroyImmediate(baked);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);if(floorMaterial)UnityEngine.Object.DestroyImmediate(floorMaterial);ShaderUtil.allowAsyncCompilation=async;if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
}
