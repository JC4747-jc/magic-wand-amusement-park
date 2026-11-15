using System;
using UnityEngine;

namespace MagicMR
{
    // Animator owns only the imported model; the tracking root stays outside it.
    public sealed class NaturalPetRig : MonoBehaviour
    {
        Animator animator;
        Transform head;
        public string headBoneName="Wolf_Neck_TopSHJnt_14";
        static readonly int SniffTrigger=Animator.StringToHash("Sniff");
        static readonly int IdleState=Animator.StringToHash("Base Layer.Restrained neutral idle");
        static readonly int Walking=Animator.StringToHash("Walking");
        public Transform Head {get {Initialize();return head;}}
        public Animator Animator {get {Initialize();return animator;}}
        public bool IsWalking {get {Initialize();return animator.GetBool(Walking);}}
        public void Initialize()
        {
            if(animator&&head)return;
            animator=GetComponentInChildren<Animator>();
            if(!animator||!animator.runtimeAnimatorController)throw new InvalidOperationException("Natural pet controller missing");
            foreach(var bone in animator.GetComponentsInChildren<Transform>())if(bone.name==headBoneName){head=bone;break;}
            if(!head)throw new InvalidOperationException("Natural pet head bone missing: "+headBoneName);
            animator.applyRootMotion=false;
        }
        public void Sniff()
        {
            Initialize();if(!Application.isPlaying||!animator.isActiveAndEnabled)return;
            animator.SetBool(Walking,false);animator.ResetTrigger(SniffTrigger);animator.SetTrigger(SniffTrigger);
        }
        public bool SetWalking(bool enabled)
        {
            Initialize();if(!Application.isPlaying||!animator.isActiveAndEnabled)return false;
            animator.ResetTrigger(SniffTrigger);animator.SetBool(Walking,enabled);return true;
        }
        public void ResetPose()
        {
            Initialize();if(!animator.isActiveAndEnabled)return;
            animator.SetBool(Walking,false);animator.ResetTrigger(SniffTrigger);animator.Play(IdleState,0,0);animator.Update(0);
        }
    }
}
