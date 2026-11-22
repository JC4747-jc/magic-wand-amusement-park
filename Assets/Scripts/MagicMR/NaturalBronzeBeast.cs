using System;
using UnityEngine;

namespace MagicMR
{
    // Authored virtual head motion; the pedestal and tracking transforms stay fixed.
    public sealed class NaturalBronzeBeast : MonoBehaviour
    {
        Transform head;
        Quaternion rest;
        float blend;
        public Transform Head {get {Initialize();return head;}}
        public float Blend=>blend;
        public void Initialize()
        {
            if(head)return;
            foreach(var child in GetComponentsInChildren<Transform>(true))if(child.name=="BronzeHead"){head=child;break;}
            if(!head)throw new InvalidOperationException("Bronze beast head bone missing");
            rest=head.localRotation;
        }
        public void Step(float delta,float time,bool awake)
        {
            Initialize();
            if(!float.IsFinite(delta)||!float.IsFinite(time)||delta<0){ResetPose();return;}
            blend=Mathf.MoveTowards(blend,awake?1:0,Mathf.Min(delta,.1f)*3.5f);
            float phase=Mathf.Repeat(time,4)*Mathf.PI*.5f;
            head.localRotation=rest*Quaternion.Euler(Mathf.Sin(phase)*1.8f*blend,Mathf.Sin(phase*2)*1.2f*blend,0);
        }
        public void ResetPose(){Initialize();blend=0;head.localRotation=rest;}
        void OnDisable(){if(head){blend=0;head.localRotation=rest;}}
    }
}
