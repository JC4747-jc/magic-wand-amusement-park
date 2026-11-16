using System;
using System.Collections.Generic;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEngine;

public static class NaturalCatWalkChecks
{
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/ParkPet.prefab"));
        try
        {
            var actor=go.GetComponent<ParkActor>();actor.species="Cat";actor.virtualTarget=true;go.SetActive(true);actor.Build();
            var rig=go.GetComponentInChildren<NaturalPetRig>();Require(rig,"Cat rig missing");
            go.transform.position=new Vector3(.5f,.1f,-.3f);Require(actor.ConfirmOrientation(37),"Cat walk yaw rejected");
            var bottom=go.transform.position+Vector3.up*.02f;Require(actor.FitRepresentation(bottom,bottom+Vector3.up*.35f,true),"Cat walk fit rejected");
            var rootPosition=go.transform.position;var rootRotation=go.transform.rotation;
            var skins=NaturalPetModelChecks.SourceSkins(rig);Require(skins.Length==3,"Cat skins changed");
            var clips=rig.Animator.runtimeAnimatorController.animationClips;
            var walk=clips.Single(c=>c.name.EndsWith("BicolorCat_Walk",StringComparison.Ordinal));Require(Mathf.Abs(walk.length-2)<.002f&&walk.isLooping,"Missing 2s loop");
            clips.Single(c=>c.name.EndsWith("BicolorCat_Idle",StringComparison.Ordinal)).SampleAnimation(rig.Animator.gameObject,0);
            var neutral=skins.SelectMany(NaturalDogAssetBuilder.Vertices).ToArray();
            string[] prefixes={"Wolf_l_HindLeg_","Wolf_l_FrontLeg_","Wolf_r_HindLeg_","Wolf_r_FrontLeg_"};float[] offsets={0,.25f,.5f,.75f};
            var paws=prefixes.Select(_=>new List<int>()).ToArray();int vertexOffset=0;
            foreach(var skin in skins)
            {
                var weights=skin.sharedMesh.boneWeights;
                for(int i=0;i<weights.Length;i++)
                {
                    if(neutral[vertexOffset+i].y>=bottom.y+.025f)continue;
                    var w=weights[i];float[] scores=new float[4];
                    void Add(int bone,float weight){for(int n=0;n<4;n++)if(skin.bones[bone].name.StartsWith(prefixes[n],StringComparison.Ordinal))scores[n]+=weight;}
                    Add(w.boneIndex0,w.weight0);if(w.weight1>0)Add(w.boneIndex1,w.weight1);if(w.weight2>0)Add(w.boneIndex2,w.weight2);if(w.weight3>0)Add(w.boneIndex3,w.weight3);
                    int best=Array.IndexOf(scores,scores.Max());if(scores[best]>.8f)paws[best].Add(vertexOffset+i);
                }
                vertexOffset+=weights.Length;
            }
            Require(paws.All(p=>p.Count>=8),"Insufficient bone-weighted paw surface vertices");
            Vector3[] first=null,last=null;float minimum=float.PositiveInfinity,stanceError=0,motion=0;float[] lift=new float[4];
            for(int sample=0;sample<=192;sample++)
            {
                float phase=sample/192f;walk.SampleAnimation(rig.Animator.gameObject,phase*walk.length);
                var points=skins.SelectMany(NaturalDogAssetBuilder.Vertices).ToArray();minimum=Mathf.Min(minimum,points.Min(p=>p.y)-bottom.y);
                if(sample==0)first=points;else for(int i=0;i<points.Length;i++)motion=Mathf.Max(motion,Vector3.Distance(points[i],first[i]));last=points;
                for(int n=0;n<4;n++)
                {
                    float height=paws[n].Min(i=>points[i].y)-bottom.y;lift[n]=Mathf.Max(lift[n],height);
                    if(Mathf.Repeat(phase-offsets[n],1)<.64f)stanceError=Mathf.Max(stanceError,Mathf.Abs(height));
                }
                Require(go.transform.position==rootPosition&&go.transform.rotation==rootRotation,"Cat walk moved confirmed tracking root");
            }
            float seam=first.Zip(last,(a,b)=>Vector3.Distance(a,b)).Max();
            Require(minimum>-.0005f&&stanceError<.0006f,"Imported walk ground contact failed: "+minimum+"/"+stanceError);
            Require(seam<.00003f&&motion>.015f&&lift.All(h=>h>.009f),"Cat walk loop or four-paw movement invalid: "+seam+"/"+motion+"/"+string.Join(",",lift));
            Debug.Log("NATURAL_CAT_WALK_GEOMETRY_PASS: 193 imported-clip samples at 0.35m fit/37 degree yaw; minimum_ground_m="+minimum+" maximum_stance_error_m="+stanceError+" loop_seam_m="+seam+" four_paw_lifts_m="+string.Join(",",lift)+"; original topology/rig, fixed tracking anchor; authored in-place preview, no real animal/hardware claim");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
}
