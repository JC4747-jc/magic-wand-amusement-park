using System.Collections.Generic;
using UnityEngine;

namespace MagicMR
{
    public sealed partial class ParkActor
    {
        Transform naturalTreeGrowth;
        Renderer naturalTreeLeaves;
        readonly List<Quaternion> treeBranchRest=new List<Quaternion>();
        public Vector3 TreeScarWorldPosition=>scar?scar.position:transform.position;
        void BuildNaturalTreeMark()
        {
            var anchor=naturalTreeGrowth.Find("FantasyScarAnchor");var prefab=Resources.Load<GameObject>("MagicMR/NaturalModels/TreeScar");
            if(!anchor||!prefab)throw new System.InvalidOperationException("Missing surface-fitted fantasy bark mark; run NaturalTreeMarkAssetBuilder.Run");
            scar=Instantiate(prefab,anchor,false).transform;scar.name="Virtual scar";
        }
        void BuildNaturalTree()
        {
            var prefab=Resources.Load<GameObject>("MagicMR/NaturalModels/SmallTree");
            if(!prefab)throw new System.InvalidOperationException("Missing CC0 small tree; run NaturalTreeAssetBuilder.Run");
            var model=Instantiate(prefab,visual,false);naturalTreeGrowth=model.transform.Find("TreeGrowth");
            var transforms=model.GetComponentsInChildren<Transform>();
            for(int i=0;i<5;i++)
            {
                Transform bone=null;foreach(var t in transforms)if(t.name=="Branch_"+i.ToString("D2")){bone=t;break;}
                if(!bone)throw new System.InvalidOperationException("Tree wind sector missing");
                branches.Add(bone);treeBranchRest.Add(bone.localRotation);
            }
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                body.Add(renderer);componentColors[renderer]=Color.white;
                if(renderer.sharedMaterial.shader.name=="MagicMR/NaturalTreeLeaves")naturalTreeLeaves=renderer;
            }
            if(!naturalTreeGrowth||!naturalTreeLeaves)throw new System.InvalidOperationException("Tree growth/season representation missing");
        }
        void ApplyNaturalTreeGrowth()
        {
            if(naturalTreeGrowth)naturalTreeGrowth.localScale=Vector3.one*(Mathf.Lerp(.78f,1.26f,growthTime)/.996f);
        }
        void ApplyNaturalTreeSeason()
        {
            if(!naturalTreeLeaves)return;
            float season=Mathf.Repeat(seasonTime,1)*4;
            // Retain original albedo in summer. Autumn changes hue through luminance, preserving photographed detail.
            float fall=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.7f,2.7f,season));
            float spring=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.8f,season));
            float winter=Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.5f,3.1f,season));
            float recovery=Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.5f,4,season));
            float coverage=Mathf.Lerp(Mathf.Lerp(1,.025f,winter),1,recovery);
            naturalTreeLeaves.GetPropertyBlock(block);
            block.SetColor("_SeasonTint",Color.Lerp(new Color(.33f,.49f,.14f),new Color(.95f,.48f,.08f),fall));
            block.SetFloat("_SeasonMix",Mathf.Max(spring*.45f,fall*.92f)*(1-recovery));
            block.SetFloat("_LeafCoverage",coverage);naturalTreeLeaves.SetPropertyBlock(block);
        }
        void ResetNaturalTreePose()
        {
            for(int i=0;i<branches.Count&&i<treeBranchRest.Count;i++)branches[i].localRotation=treeBranchRest[i];
            ApplyNaturalTreeGrowth();ApplyNaturalTreeSeason();
        }
    }
}
