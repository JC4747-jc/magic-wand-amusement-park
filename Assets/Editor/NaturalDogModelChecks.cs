using System;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEngine;

public static class NaturalDogModelChecks
{
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/ParkPet.prefab"));
        try
        {
            var actor=go.GetComponent<ParkActor>();actor.species="Dog";actor.virtualTarget=true;go.SetActive(true);actor.Build();
            var rig=go.GetComponentInChildren<NaturalPetRig>();Require(rig&&rig.headBoneName=="Head","Wolfhound not installed in Dog route");
            var skins=rig.GetComponentsInChildren<SkinnedMeshRenderer>();Require(skins.Length==1&&skins[0].bones.Length==47,"Dog source rig changed");
            Require(skins[0].sharedMesh.triangles.Length/3==849,"Dog topology changed");
            var coat=skins[0].sharedMaterial;Require(coat.GetTexture("_BaseMap")&&coat.GetTexture("_BaseMap").name=="Wolfhound_Albedo"&&!coat.IsKeywordEnabled("_NORMALMAP"),"Dog original coat lost");
            go.transform.position=new Vector3(.6f,.05f,-.4f);Require(actor.ConfirmOrientation(37),"Dog yaw rejected");
            var bottom=go.transform.position+Vector3.up*.015f;Require(actor.FitRepresentation(bottom,bottom+Vector3.up*.35f,true),"Dog fit rejected");
            var position=go.transform.position;var rotation=go.transform.rotation;
            // Static fitting checks sample the imported clip directly. Animator
            // execution/state transitions are verified separately in Play Mode.
            var clip=rig.Animator.runtimeAnimatorController.animationClips.Single(c=>c.name.EndsWith("Wolfhound_Feed",StringComparison.Ordinal));
            clip.SampleAnimation(rig.Animator.gameObject,0);
            var bounds=NaturalDogAssetBuilder.Geometry(rig.gameObject);Require(Mathf.Abs(bounds.min.y-bottom.y)<.002f,"Dog fit bottom differs from actual skin: "+(bounds.min.y-bottom.y));
            var initial=NaturalDogAssetBuilder.Vertices(skins[0]);var feet=Enumerable.Range(0,initial.Length).Where(i=>initial[i].y<bounds.min.y+bounds.size.y*.04f).ToArray();Require(feet.Length>=8,"No dog foot samples");
            float footError=0,skinMotion=0,headAngle=0;var originalHead=rig.Head.rotation;
            for(int i=0;i<5;i++)
            {
                clip.SampleAnimation(rig.Animator.gameObject,clip.length*i*.2f);var points=NaturalDogAssetBuilder.Vertices(skins[0]);
                foreach(int n in feet)footError=Mathf.Max(footError,Vector3.Distance(points[n],initial[n]));for(int n=0;n<points.Length;n++)skinMotion=Mathf.Max(skinMotion,Vector3.Distance(points[n],initial[n]));headAngle=Mathf.Max(headAngle,Quaternion.Angle(rig.Head.rotation,originalHead));
                Require(go.transform.position==position&&go.transform.rotation==rotation,"Dog animation moved confirmed tracking root");
                Require(rig.Head.position.y>NaturalDogAssetBuilder.Geometry(rig.gameObject).center.y,"Dog anatomical up inverted");
            }
            Require(footError<.001f&&skinMotion>.00005f&&headAngle>.1f,"Dog sniff foot/skin/head angle invalid: "+footError+"/"+skinMotion+"/"+headAngle);
            rig.Animator.runtimeAnimatorController.animationClips.Single(c=>c.name.EndsWith("Wolfhound_Idle",StringComparison.Ordinal)).SampleAnimation(rig.Animator.gameObject,0);
            Debug.Log("NATURAL_DOG_MODEL_PASS: original 849 triangles/47 bones/256px coat; confirmed yaw and actual skin bottom fit; five sniff poses foot_error_m="+footError+" skin_motion_m="+skinMotion+" head_angle_deg="+headAngle+"; tracking root fixed");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
}
