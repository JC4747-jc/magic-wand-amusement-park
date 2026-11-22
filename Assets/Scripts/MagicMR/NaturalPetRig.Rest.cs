using System.Collections.Generic;
using UnityEngine;

namespace MagicMR
{
    public sealed partial class NaturalPetRig
    {
        const int GroundSamples = 25;
        readonly float[] restGround = new float[GroundSamples];
        readonly List<LocalBend> restBends = new List<LocalBend>();
        Transform restBody;
        Vector3 restBodyPosition, restBodyInRig, restPivot;
        Quaternion restBodyRotation, restBodyInRigRotation;
        float restTime, restBreath;
        bool restPoseActive, keepStateBeforeRest, restGroundReady;
        public bool RestRequested { get; private set; }
        public float RestAmount { get; private set; }

        sealed class LocalBend
        {
            public Transform Bone;
            public float Pitch;
            public Quaternion Neutral;
        }

        sealed class GroundSkin
        {
            readonly Vector3[] vertices;
            readonly BoneWeight[] weights;
            readonly Transform[] bones;
            readonly Matrix4x4[] bind, matrices;

            public GroundSkin(SkinnedMeshRenderer skin)
            {
                vertices=skin.sharedMesh.vertices;
                weights=skin.sharedMesh.boneWeights;
                bind=skin.sharedMesh.bindposes;
                bones=skin.bones;
                matrices=new Matrix4x4[bones.Length];
            }

            public float Floor(Matrix4x4 worldToRig)
            {
                for(int i=0;i<bones.Length;i++)matrices[i]=worldToRig*bones[i].localToWorldMatrix*bind[i];
                float min=float.PositiveInfinity;
                for(int i=0;i<vertices.Length;i++)
                {
                    Vector3 v=vertices[i];
                    var w=weights[i];
                    float y=matrices[w.boneIndex0].MultiplyPoint3x4(v).y*w.weight0;
                    if(w.weight1>0)y+=matrices[w.boneIndex1].MultiplyPoint3x4(v).y*w.weight1;
                    if(w.weight2>0)y+=matrices[w.boneIndex2].MultiplyPoint3x4(v).y*w.weight2;
                    if(w.weight3>0)y+=matrices[w.boneIndex3].MultiplyPoint3x4(v).y*w.weight3;
                    min=Mathf.Min(min,y);
                }
                return min;
            }
        }

        public bool SetResting(bool rest)
        {
            Initialize();
            if(!Application.isPlaying||!gameObject.activeInHierarchy||IsFeeding)return false;
            if(rest&&!restPoseActive)
            {
                if(!animator.isActiveAndEnabled||!BindRestRig())return false;
                ResetPose();
                restBodyPosition=restBody.localPosition;
                restBodyRotation=restBody.localRotation;
                restBodyInRig=transform.InverseTransformPoint(restBody.position);
                restBodyInRigRotation=Quaternion.Inverse(transform.rotation)*restBody.rotation;
                // Roll around a point above the front-to-rear centerline, not around the tracking anchor.
                restPivot=Vector3.Lerp(restBodyInRig,transform.InverseTransformPoint(lowerNeck.position),.42f);
                foreach(var bend in restBends)bend.Neutral=bend.Bone.localRotation;
                if(!restGroundReady&&!PrepareGroundPath())return false;
                restTime=restBreath=0;
                restPoseActive=true;
                keepStateBeforeRest=animator.keepAnimatorStateOnDisable;
                // A zero-speed Animator still evaluates the neutral pose.
                animator.keepAnimatorStateOnDisable=true;
                animator.enabled=false;
            }
            RestRequested=rest;
            return true;
        }

        bool BindRestRig()
        {
            if(restBends.Count>0)return true;
            bool dog=headBoneName=="Head";
            var bones=new Dictionary<string,Transform>();
            foreach(var bone in animator.GetComponentsInChildren<Transform>(true))bones.TryAdd(bone.name,bone);
            if(!bones.TryGetValue(dog?"wolfhound":"Wolf_ROOTSHJnt_38",out var skeleton)||!lowerNeck||!jaw)return false;
            // Move every imported skin and bone together around the torso. The
            // prefab's own tracking/fitting transform remains outside this move.
            restBody=animator.transform;
            if(restBody==transform||!skeleton.IsChildOf(restBody))return false;
            var legBends=new List<LocalBend>();
            foreach(string side in new[]{"Left","Right"})
            {
                bool left=side=="Left";
                string suffix=left?"_L":"_R";
                string prefix=left?"Wolf_l_":"Wolf_r_";
                string[] names=dog
                    ?new[]{"FrontLeg1"+suffix,"FrontLeg2"+suffix,"BackLeg1"+suffix,"BackLeg2"+suffix}
                    :new[]{prefix+"FrontLeg_HipSHJnt_"+(left?4:10),prefix+"FrontLeg_KneeSHJnt_"+(left?3:9),
                           prefix+"HindLeg_HipSHJnt_"+(left?27:33),prefix+"HindLeg_Knee1SHJnt_"+(left?26:32)};
                float[] angles={18,-24,24,-32};
                for(int i=0;i<names.Length;i++)
                {
                    if(!bones.TryGetValue(names[i],out var bone)||!bone.IsChildOf(skeleton))return false;
                    legBends.Add(new LocalBend{Bone=bone,Pitch=angles[i]});
                }
            }
            restBends.AddRange(legBends);
            restBends.Add(new LocalBend{Bone=lowerNeck,Pitch=-7});
            restBends.Add(new LocalBend{Bone=neck,Pitch=-13});
            restBends.Add(new LocalBend{Bone=head,Pitch=-9});
            restBends.Add(new LocalBend{Bone=jaw,Pitch=dog?6:0});
            return true;
        }

        bool PrepareGroundPath()
        {
            var skins=new List<GroundSkin>();
            foreach(var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if(skin.sharedMesh&&skin.name!="Manga silhouette")skins.Add(new GroundSkin(skin));
            if(skins.Count==0)return false;
            float standing=RestFloor(skins);
            for(int i=0;i<restGround.Length;i++)
            {
                ApplyRestBones(i/(float)(restGround.Length-1),0,0);
                restGround[i]=standing-RestFloor(skins);
            }
            RestoreRestBones();
            restGroundReady=float.IsFinite(standing)&&float.IsFinite(restGround[restGround.Length-1]);
            return restGroundReady;
        }

        float RestFloor(List<GroundSkin> skins)
        {
            var inverse=transform.worldToLocalMatrix;
            float floor=float.PositiveInfinity;
            foreach(var skin in skins)floor=Mathf.Min(floor,skin.Floor(inverse));
            return floor;
        }

        void ApplyRestBones(float amount,float elevation,float inhale)
        {
            restBody.position=transform.TransformPoint(restBodyInRig);
            restBody.rotation=transform.rotation*restBodyInRigRotation;
            foreach(var bend in restBends)Bend(bend.Bone,bend.Neutral,bend.Pitch*amount);
            var roll=Quaternion.AngleAxis(-83f*Ease((amount-.08f)/.92f),Vector3.forward);
            restBody.position=transform.TransformPoint(restPivot+roll*(restBodyInRig-restPivot)+Vector3.up*(elevation+inhale));
            restBody.rotation=transform.rotation*roll*restBodyInRigRotation;
        }

        void StepRestPose(float dt)
        {
            restTime=Mathf.MoveTowards(restTime,RestRequested?1:0,dt/(RestRequested?1.35f:.9f));
            if(!RestRequested&&restTime<=0){RestoreRestPose();return;}
            RestAmount=Ease(restTime);
            restBreath=Mathf.Repeat(restBreath+dt/2.7f,1);
            float inhale=(1-Mathf.Cos(restBreath*2*Mathf.PI))*.5f;
            float sample=RestAmount*(restGround.Length-1);
            int left=Mathf.Min(Mathf.FloorToInt(sample),restGround.Length-2);
            float elevation=Mathf.Lerp(restGround[left],restGround[left+1],sample-left)+.0019f*RestAmount;
            ApplyRestBones(RestAmount,elevation,inhale*.0034f*RestAmount);
        }

        void RestoreRestBones()
        {
            restBody.localPosition=restBodyPosition;
            restBody.localRotation=restBodyRotation;
            foreach(var bend in restBends)bend.Bone.localRotation=bend.Neutral;
        }

        void RestoreRestPose()
        {
            RestoreRestBones();
            animator.enabled=true;
            animator.keepAnimatorStateOnDisable=keepStateBeforeRest;
            restPoseActive=false;
            RestRequested=false;
            RestAmount=0;
        }
    }
}
