using System;
using System.Linq;
using System.Reflection;
using MagicMR;
using UnityEditor;
using UnityEngine;

public static class NaturalTreeChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static void Call(ParkActor actor,string method,params object[] args){typeof(ParkActor).GetMethod(method,Private).Invoke(actor,args);}
    public static void Run()
    {
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/ParkTree.prefab"));var baked=new Mesh();
        try
        {
            go.SetActive(true);var actor=go.GetComponent<ParkActor>();actor.Build();actor.ResetEffects();actor.cooldown=0;
            var leaves=actor.visual.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name=="Leaves");
            var trunk=actor.visual.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name=="Trunk");
            var growth=(Transform)typeof(ParkActor).GetField("naturalTreeGrowth",Private).GetValue(actor);
            var colours=leaves.sharedMesh.colors32;
            Require(colours.Length==leaves.sharedMesh.vertexCount&&colours.Select(c=>c.a).Distinct().Count()>200,"Leaf seasonal IDs were lost during FBX import");
            Require(leaves.sharedMaterial.GetTexture("_AlphaMap")&&leaves.sharedMaterial.shader.name=="MagicMR/NaturalTreeLeaves"&&!ShaderUtil.ShaderHasError(leaves.sharedMaterial.shader),"Tree silhouette/season shader is invalid");
            var trunkMatrix=trunk.localToWorldMatrix;var anchor=go.transform.localToWorldMatrix;
            leaves.BakeMesh(baked);var before=baked.vertices;
            var stateField=typeof(ParkActor).GetField("state",Private);var state=stateField.GetValue(actor);state.GetType().GetField("wind").SetValue(state,true);stateField.SetValue(actor,state);
            actor.windStrength=.6f;actor.windDirection=Vector3.right;Call(actor,"StepTreeWind",.25f,1f);
            leaves.BakeMesh(baked);var after=baked.vertices;int moved=0;
            for(int i=0;i<after.Length;i++)
            {
                Require(float.IsFinite(after[i].x)&&float.IsFinite(after[i].y)&&float.IsFinite(after[i].z),"Wind produced nonfinite leaf skin");
                if((before[i]-after[i]).sqrMagnitude>1e-9f)moved++;
            }
            Require(moved>after.Length*.1f,"Tree bone motion did not deform its actual leaf mesh");
            Require(trunk.localToWorldMatrix==trunkMatrix&&go.transform.localToWorldMatrix==anchor,"Wind moved trunk/tracking anchor");
            actor.windDirection=new Vector3(float.NaN,0,0);Call(actor,"StepTreeWind",.25f,2f);
            leaves.BakeMesh(baked);var settled=baked.vertices;
            Require(settled.Zip(before,(a,b)=>(a-b).sqrMagnitude).Max()<1e-9f,"Invalid wind did not restore the rest skin");
            actor.ResetEffects();var bottom=trunk.bounds.min.y;var height=trunk.bounds.size.y;var pivot=growth.position;
            Require(actor.TryAction(ParkAction.GrowUp,actor.transform.position,true),"Tree growth rejected");
            actor.growthTime=.95f;Call(actor,"ApplyGrowthPose");
            Require(trunk.bounds.size.y>height*1.15f&&Mathf.Abs(trunk.bounds.min.y-bottom)<.0001f&&growth.position==pivot,"Tree growth floated its base or did not grow");
            actor.Undo();Require(Mathf.Abs(actor.growthTime-.45f)<.001f&&Mathf.Abs(trunk.bounds.size.y-height)<.0001f,"Tree Undo did not restore growth immediately");
            var block=new MaterialPropertyBlock();
            actor.seasonTime=.8f;Call(actor,"ApplyNaturalTreeSeason");leaves.GetPropertyBlock(block);Require(block.GetFloat("_LeafCoverage")<.03f,"Winter retained a full canopy");
            actor.seasonTime=.625f;Call(actor,"ApplyNaturalTreeSeason");leaves.GetPropertyBlock(block);Require(block.GetColor("_SeasonTint").r>block.GetColor("_SeasonTint").g,"Mid-autumn hue did not change");
            actor.ResetEffects();leaves.GetPropertyBlock(block);Require(block.GetFloat("_LeafCoverage")>.99f&&go.transform.localToWorldMatrix==anchor,"Tree Reset did not restore canopy/anchor");
            Debug.Log("NATURAL_TREE_RUNTIME_PASS: "+moved+"/"+after.Length+" leaf vertices deformed; fixed trunk/anchor, invalid-wind recovery, base-fixed growth/Undo and seasonal Reset");
        }
        finally{UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(go);}
    }
}
