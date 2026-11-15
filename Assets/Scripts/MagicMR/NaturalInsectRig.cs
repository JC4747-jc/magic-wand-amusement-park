using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicMR
{
    // The owner advances this bounded rig. No Animator, per-frame object creation or network dependency.
    public sealed class NaturalInsectRig : MonoBehaviour
    {
        sealed class Leg
        {
            public Transform upper,lower,foot;
            public Vector3 hip,knee,ankle,target;
            public Quaternion upperRotation,lowerRotation,footRotation;
            public float upperLength,lowerLength;
        }
        Leg[] legs;
        Transform[] bones;
        Quaternion[] rest;
        readonly List<int> antennaIndices=new List<int>();
        bool ready;
        public int LegCount {get{Initialize();return legs.Length;}}
        public SkinnedMeshRenderer Skin {get;private set;}
        public Vector3 FootLocal(int index)=>transform.InverseTransformPoint(legs[index].foot.position);
        public Vector3 FootTarget(int index)=>legs[index].target;
        public void Initialize()
        {
            if(ready)return;
            Skin=GetComponentInChildren<SkinnedMeshRenderer>();if(!Skin)throw new InvalidOperationException("Insect skin missing");
            bones=Skin.bones;rest=new Quaternion[bones.Length];var names=new Dictionary<string,Transform>();
            for(int i=0;i<bones.Length;i++){names.Add(bones[i].name,bones[i]);rest[i]=bones[i].localRotation;if(bones[i].name.StartsWith("Antenna_",StringComparison.Ordinal))antennaIndices.Add(i);}
            var cache=new List<Leg>();
            for(int i=0;i<8;i++)
            {
                string prefix="Leg_"+i.ToString("D2");
                if(!names.TryGetValue(prefix+"_Upper",out var upper))break;
                if(!names.TryGetValue(prefix+"_Lower",out var lower)||!names.TryGetValue(prefix+"_Foot",out var foot))throw new InvalidOperationException("Incomplete insect leg chain");
                var leg=new Leg{upper=upper,lower=lower,foot=foot,hip=transform.InverseTransformPoint(upper.position),knee=transform.InverseTransformPoint(lower.position),ankle=transform.InverseTransformPoint(foot.position),
                    upperRotation=Quaternion.Inverse(transform.rotation)*upper.rotation,lowerRotation=Quaternion.Inverse(transform.rotation)*lower.rotation,footRotation=Quaternion.Inverse(transform.rotation)*foot.rotation};
                leg.upperLength=Vector3.Distance(leg.hip,leg.knee);leg.lowerLength=Vector3.Distance(leg.knee,leg.ankle);leg.target=leg.ankle;
                if(leg.upperLength<.00001f||leg.lowerLength<.00001f)throw new InvalidOperationException("Degenerate insect leg chain");cache.Add(leg);
            }
            legs=cache.ToArray();if(legs.Length!=6&&legs.Length!=8)throw new InvalidOperationException("Insect must have six or eight articulated legs");ready=true;
        }
        public void ResetPose()
        {
            Initialize();for(int i=0;i<bones.Length;i++)bones[i].localRotation=rest[i];foreach(var leg in legs)leg.target=leg.ankle;
        }
        public void Step(float time,bool active)
        {
            ResetPose();if(!float.IsFinite(time))return;
            if(active)
            {
                for(int i=0;i<legs.Length;i++)
                {
                    var leg=legs[i];float phase=Mathf.Repeat(time*1.6f+((i/2+i%2)%2)*.5f,1);
                    float stride=(leg.upperLength+leg.lowerLength)*.11f,offset,lift=0;
                    if(phase<.62f)offset=Mathf.Lerp(.5f,-.5f,phase/.62f);
                    else{float swing=(phase-.62f)/.38f;offset=Mathf.Lerp(-.5f,.5f,Mathf.SmoothStep(0,1,swing));lift=Mathf.Sin(swing*Mathf.PI)*(leg.upperLength+leg.lowerLength)*.10f;}
                    leg.target=leg.ankle+Vector3.back*(offset*stride)+Vector3.up*lift;Solve(leg);
                }
            }
            for(int i=0;i<antennaIndices.Count;i++)
            {
                int index=antennaIndices[i];float phase=i*.7f;
                float sweep=(Mathf.Sin(time*(active?2.3f:.7f)+phase)-Mathf.Sin(phase))*(active?1.5f:.35f);
                bones[index].localRotation=rest[index]*Quaternion.Euler(0,sweep,sweep*.4f);
            }
        }
        void Solve(Leg leg)
        {
            Vector3 delta=leg.target-leg.hip;float rawDistance=delta.magnitude;
            if(rawDistance<.000001f)return;
            float distance=Mathf.Clamp(rawDistance,Mathf.Abs(leg.upperLength-leg.lowerLength)+.000001f,leg.upperLength+leg.lowerLength-.000001f);
            Vector3 direction=delta/rawDistance,pole=Vector3.ProjectOnPlane(leg.knee-leg.hip,direction).normalized;
            if(pole.sqrMagnitude<.5f)pole=Vector3.up;
            float along=(leg.upperLength*leg.upperLength-leg.lowerLength*leg.lowerLength+distance*distance)/(2*distance);
            float height=Mathf.Sqrt(Mathf.Max(0,leg.upperLength*leg.upperLength-along*along));
            Vector3 knee=leg.hip+direction*along+pole*height,ankle=leg.hip+direction*distance;
            leg.upper.rotation=transform.rotation*Quaternion.FromToRotation((leg.knee-leg.hip).normalized,(knee-leg.hip).normalized)*leg.upperRotation;
            leg.lower.rotation=transform.rotation*Quaternion.FromToRotation((leg.ankle-leg.knee).normalized,(ankle-knee).normalized)*leg.lowerRotation;
            // Keep tarsi level while the femur/tibia solve the planted or lifted ankle target.
            leg.foot.rotation=transform.rotation*leg.footRotation;
        }
    }
}
