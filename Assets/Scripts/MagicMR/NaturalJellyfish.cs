using System;
using UnityEngine;

namespace MagicMR
{
    // All geometry is shared and static. A bounded GPU deformation animates the
    // single transparent surface; no Update, mesh uploads, lights or spawning.
    public sealed class NaturalJellyfish : MonoBehaviour
    {
        MeshRenderer surface;
        MaterialPropertyBlock properties;
        static readonly int PhaseId=Shader.PropertyToID("_Phase"),OpacityId=Shader.PropertyToID("_Opacity");
        public float Visibility {get;private set;}
        public float Phase {get;private set;}
        public MeshRenderer Surface {get {Initialize();return surface;}}
        public void Initialize()
        {
            if(surface)return;
            surface=GetComponent<MeshRenderer>();
            if(!surface||!surface.sharedMaterial||surface.sharedMaterial.shader.name!="MagicMR/NaturalJellyfish")throw new InvalidOperationException("Natural jellyfish surface/material missing");
            properties=new MaterialPropertyBlock();surface.GetPropertyBlock(properties);
        }
        public void Step(float dt,float time,bool shown)
        {
            Initialize();
            if(!float.IsFinite(dt)||dt<0||!float.IsFinite(time)){ResetPose();return;}
            Phase=Mathf.Repeat(time,2.8f)*(Mathf.PI*2/2.8f);
            Visibility=Mathf.MoveTowards(Visibility,shown?1:0,Mathf.Min(dt,.5f)/.35f);Apply();
        }
        public void SetVisibility(float value)
        {
            Initialize();Visibility=float.IsFinite(value)?Mathf.Clamp01(value):0;Apply();
        }
        public void ResetPose(){Initialize();Visibility=Phase=0;Apply();}
        void Apply(){properties.SetFloat(PhaseId,Phase);properties.SetFloat(OpacityId,Visibility);surface.SetPropertyBlock(properties);}
    }
}
