using System;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEngine;

public static class NaturalPetModelChecks
{
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static SkinnedMeshRenderer[] SourceSkins(NaturalPetRig rig)
    {
        var shader=Resources.Load<Shader>("MagicMR/PetOutline");
        Require(shader,"Pet outline shader missing");
        var all=rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var shells=all.Where(s=>s.sharedMaterials.Length>0&&s.sharedMaterials.All(m=>m&&m.shader==shader)).ToArray();
        var sources=all.Except(shells).ToArray();
        Require(shells.Length>0&&shells.Length<=sources.Length,"Pet outline renderer budget invalid");
        foreach(var shell in shells)
        {
            var source=shell.transform.parent.GetComponent<SkinnedMeshRenderer>();
            Require(source&&sources.Contains(source),"Outline lacks its source renderer");
            Require(shell.sharedMesh==source.sharedMesh&&shell.rootBone==source.rootBone&&shell.bones.SequenceEqual(source.bones),"Outline duplicated mesh or changed bone binding");
            Require(shell.sharedMaterials.Length==source.sharedMesh.subMeshCount&&!shell.enabled,"Idle outline must be hidden with one material per submesh");
        }
        return sources;
    }
    static Bounds Geometry(ParkActor actor)
    {
        bool first=true;var bounds=new Bounds();
        foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;
            var matrices=skin.bones.Select((bone,i)=>bone.localToWorldMatrix*mesh.bindposes[i]).ToArray();
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];var p=matrices[w.boneIndex0].MultiplyPoint3x4(vertices[i])*w.weight0;
                if(w.weight1>0)p+=matrices[w.boneIndex1].MultiplyPoint3x4(vertices[i])*w.weight1;
                if(w.weight2>0)p+=matrices[w.boneIndex2].MultiplyPoint3x4(vertices[i])*w.weight2;
                if(w.weight3>0)p+=matrices[w.boneIndex3].MultiplyPoint3x4(vertices[i])*w.weight3;
                Require(float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z),"Nonfinite pet skin");
                if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
            }
        }
        Require(!first,"No natural cat geometry");return bounds;
    }
    public static void Run()
    {
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/ParkPet.prefab"));
        try
        {
            var actor=go.GetComponent<ParkActor>();actor.species="Cat";actor.virtualTarget=true;go.SetActive(true);actor.Build();
            var rig=go.GetComponentInChildren<NaturalPetRig>();Require(rig,"Natural cat not installed");rig.Initialize();
            var skins=NaturalPetModelChecks.SourceSkins(rig);
            Require(skins.Length==3&&skins.All(s=>s.bones.Length<=40),"Cat skin/bone budget exceeded");
            long triangles=skins.Sum(s=>Enumerable.Range(0,s.sharedMesh.subMeshCount).Sum(i=>(long)s.sharedMesh.GetIndexCount(i)/3));
            Require(triangles==8646,"Source cat topology unexpectedly changed");
            var coat=skins.SelectMany(s=>s.sharedMaterials).First(m=>m.name=="cat_diffuse");
            Require(coat.GetTexture("_BaseMap")&&coat.GetTexture("_BaseMap").name=="Image_0","Original pet albedo lost");
            Require(coat.IsKeywordEnabled("_NORMALMAP")&&coat.GetTexture("_BumpMap")&&coat.GetTexture("_BumpMap").name=="BicolorCat_UV0_Normal","UV2 source normal was not reprojected for URP UV0");
            go.transform.position=new Vector3(.6f,.05f,-.4f);Require(actor.ConfirmOrientation(37),"Pet orientation confirmation failed");
            var bottom=go.transform.position+Vector3.up*.015f;
            Require(actor.FitRepresentation(bottom,bottom+Vector3.up*.35f,true),"Pet fit rejected");
            var position=go.transform.position;var rotation=go.transform.rotation;
            float minimum=float.PositiveInfinity,maximumError=0;var originalHead=rig.Head.position;
            for(int i=0;i<5;i++)
            {
                rig.Animator.Play("Restrained neutral idle",0,i/4f);rig.Animator.Update(0);
                var bounds=Geometry(actor);minimum=Mathf.Min(minimum,bounds.min.y-bottom.y);
                maximumError=Mathf.Max(maximumError,Mathf.Abs(bounds.min.y-bottom.y));
                Require(Mathf.Abs(bounds.size.y-.35f)<.003f,"Idle pet height drifted beyond 3 mm");
                Require(go.transform.position==position&&go.transform.rotation==rotation,"Pet clip moved confirmed tracking root");
            }
            Require(maximumError<.001f,"Pet feet do not match confirmed bottom: "+maximumError);
            rig.Animator.Play("Brief sniff",0,.5f);rig.Animator.Update(0);Require(Vector3.Distance(rig.Head.position,originalHead)<.025f,"Sniff detached head from neck");
            rig.ResetPose();
            Debug.Log("NATURAL_PET_MODEL_PASS: 8646 triangles, three skins, original 40-bone rig; UV0 reprojected normals; five independently skinned placement poses, max bottom error="+maximumError+"; yaw/tracking anchor fixed");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
}
