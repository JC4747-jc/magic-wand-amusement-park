using System;
using UnityEngine;

namespace MagicMR
{
    // Animator owns only the imported model; the tracking root stays outside it.
    public sealed partial class NaturalPetRig : MonoBehaviour
    {
        Animator animator;
        Transform head;
        Transform neck, lowerNeck, jaw;
        Quaternion neckAtRest, lowerNeckAtRest, jawAtRest;
        float speedBeforeMeal, mealLean, mealBite;
        Vector3 mealMuzzleInJaw;
        public string headBoneName="Wolf_Neck_TopSHJnt_14";
        public const float MealDuration = 2.25f;
        static readonly int SniffTrigger=Animator.StringToHash("Sniff");
        static readonly int IdleState=Animator.StringToHash("Base Layer.Restrained neutral idle");
        static readonly int Walking=Animator.StringToHash("Walking");
        public Transform Head {get {Initialize();return head;}}
        public Animator Animator {get {Initialize();return animator;}}
        public bool IsWalking {get {Initialize();return !restPoseActive&&animator.GetBool(Walking);}}
        public bool IsFeeding { get; private set; }
        public float MealJawAngle { get; private set; }
        public int ConsumedMorsels { get; private set; }
        public Vector3 MealTrayPosition { get; private set; }
        public Vector3 MealMuzzlePosition => jaw.TransformPoint(mealMuzzleInJaw);
        public void Initialize()
        {
            if(animator&&head)return;
            animator=GetComponentInChildren<Animator>();
            if(!animator||!animator.runtimeAnimatorController)throw new InvalidOperationException("Natural pet controller missing");
            foreach(var bone in animator.GetComponentsInChildren<Transform>())
            {
                if(bone.name==headBoneName)head=bone;
                if(bone.name=="Jaw1"||bone.name=="Wolf_Head_JawSHJnt_12")jaw=bone;
            }
            if(!head)throw new InvalidOperationException("Natural pet head bone missing: "+headBoneName);
            neck=head.parent;
            lowerNeck=neck.parent;
            animator.applyRootMotion=false;
        }
        public void Sniff()
        {
            Initialize();if(restPoseActive)RestoreRestPose();
            if(!Application.isPlaying||!animator.isActiveAndEnabled)return;
            animator.SetBool(Walking,false);animator.ResetTrigger(SniffTrigger);animator.SetTrigger(SniffTrigger);
        }
        public bool SetWalking(bool enabled)
        {
            Initialize();if(IsFeeding||!Application.isPlaying)return false;
            if(restPoseActive)
            {
                if(!enabled)return true;
                RestoreRestPose();
            }
            if(!animator.isActiveAndEnabled)return false;
            animator.ResetTrigger(SniffTrigger);animator.SetBool(Walking,enabled);return true;
        }
        public bool BeginMeal()
        {
            Initialize();
            if(restPoseActive)RestoreRestPose();
            if(!Application.isPlaying||!animator.isActiveAndEnabled||!jaw||!lowerNeck)return false;
            ResetPose();
            speedBeforeMeal=animator.speed;
            animator.speed=0;
            IsFeeding=true;
            neckAtRest=neck.localRotation;
            lowerNeckAtRest=lowerNeck.localRotation;
            jawAtRest=jaw.localRotation;

            PoseMeal(1,0);
            // The imported jaw bones pivot behind the visible muzzle. Put the
            // dish below the bite point on the low pose of each rig.
            float muzzleLength=headBoneName=="Head"?.070f:.041f;
            Vector3 muzzle=jaw.position+transform.TransformVector(new Vector3(0,.003f,-muzzleLength));
            mealMuzzleInJaw=jaw.InverseTransformPoint(muzzle);
            MealTrayPosition=transform.InverseTransformPoint(muzzle)+new Vector3(0,-.037f,-.008f);
            SampleMeal(0);
            PoseMeal(0,0);
            return true;
        }
        public void SampleMeal(float elapsed)
        {
            if(!IsFeeding)return;
            float lean=Ease(elapsed/.48f)*(1-Ease((elapsed-1.72f)/.48f));
            float bite=0;
            ConsumedMorsels=0;
            for(int i=0;i<3;i++)
            {
                float center=MealBiteCenter(i);
                float phase=Mathf.Clamp01((elapsed-center+.145f)/.29f);
                bite=Mathf.Max(bite,Mathf.Sin(phase*Mathf.PI));
                if(elapsed>=center+.10f)ConsumedMorsels++;
            }
            mealLean=lean;
            mealBite=bite;
        }
        static float MealBiteCenter(int index)=>.63f+index*.39f;
        public static float MealPickupProgress(float elapsed,int index)=>Ease((elapsed-MealBiteCenter(index)+.08f)/.18f);
        void LateUpdate()
        {
            if(IsFeeding)PoseMeal(mealLean,mealBite);
            else if(restPoseActive)StepRestPose(Time.unscaledDeltaTime);
        }
        static float Ease(float value)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(value));
        void PoseMeal(float lean,float bite)
        {
            Bend(lowerNeck,lowerNeckAtRest,-14f*lean);
            Bend(neck,neckAtRest,-(19f+bite*3f)*lean);
            MealJawAngle=11f*bite;
            Bend(jaw,jawAtRest,-MealJawAngle);
        }
        void Bend(Transform bone,Quaternion neutral,float angle)
        {
            Vector3 axis=bone.parent.InverseTransformDirection(transform.right);
            bone.localRotation=Quaternion.AngleAxis(angle,axis)*neutral;
        }
        public void ResetPose()
        {
            Initialize();
            if(restPoseActive)RestoreRestPose();
            if(IsFeeding)
            {
                PoseMeal(0,0);
                animator.speed=speedBeforeMeal;
                IsFeeding=false;
                ConsumedMorsels=0;
            }
            if(!animator.isActiveAndEnabled)return;
            animator.SetBool(Walking,false);animator.ResetTrigger(SniffTrigger);animator.Play(IdleState,0,0);animator.Update(0);
        }
    }
}
