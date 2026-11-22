using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagicMR
{
    // One shared mesh/bone binding per source renderer; no mesh baking or per-frame allocation.
    // Hull is presentation-only: never contributes to the actor's fit bounds or physics.
    public sealed class NaturalPetOutline : MonoBehaviour
    {
        readonly List<Renderer> sources=new List<Renderer>();
        readonly List<Renderer> shells=new List<Renderer>();
        Material ink;
        bool visible;
        public int ShellCount=>shells.Count;
        public void Initialize(bool inwardWinding=false)
        {
            if(ink)return;
            var shader=Resources.Load<Shader>("MagicMR/PetOutline");
            if(!shader)throw new InvalidOperationException("Pet outline shader missing");
            ink=new Material(shader){name="Pet manga ink (owned)"};
            // The accepted double-sided BicolorCat body has inward winding/normals.
            // Adjust only its presentation hull; preserve the licensed mesh and rig.
            ink.SetFloat("_NormalSign",inwardWinding?-1:1);
            ink.SetFloat("_HullCull",(float)(inwardWinding?CullMode.Back:CullMode.Front));
            var renderers=GetComponentsInChildren<Renderer>(true);
            float largest=0;foreach(var source in renderers)largest=Mathf.Max(largest,source.bounds.size.sqrMagnitude);
            foreach(var source in renderers)
            {
                // Tiny eyes and whiskers need no expanded hull: it obscures their detail.
                if(source.bounds.size.sqrMagnitude<largest*.25f)continue;
                var skin=source as SkinnedMeshRenderer;
                var filter=source.GetComponent<MeshFilter>();
                Mesh mesh=skin?skin.sharedMesh:filter?filter.sharedMesh:null;
                if(!mesh)continue;
                var go=new GameObject("Manga silhouette");go.layer=source.gameObject.layer;
                go.transform.SetParent(source.transform,false);
                Renderer shell;
                if(skin)
                {
                    var copy=go.AddComponent<SkinnedMeshRenderer>();
                    copy.sharedMesh=mesh;copy.rootBone=skin.rootBone;copy.bones=skin.bones;
                    copy.quality=skin.quality;copy.updateWhenOffscreen=skin.updateWhenOffscreen;
                    var bounds=skin.localBounds;
                    var scale=skin.transform.lossyScale;float minimumScale=Mathf.Max(.0001f,Mathf.Min(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
                    bounds.Expand(.008f/minimumScale);copy.localBounds=bounds;
                    shell=copy;
                }
                else {go.AddComponent<MeshFilter>().sharedMesh=mesh;shell=go.AddComponent<MeshRenderer>();}
                var materials=new Material[mesh.subMeshCount];for(int i=0;i<materials.Length;i++)materials[i]=ink;
                shell.sharedMaterials=materials;shell.shadowCastingMode=ShadowCastingMode.Off;
                shell.receiveShadows=false;shell.lightProbeUsage=LightProbeUsage.Off;
                shell.reflectionProbeUsage=ReflectionProbeUsage.Off;shell.enabled=false;
                sources.Add(source);shells.Add(shell);
            }
            if(shells.Count==0)throw new InvalidOperationException("Pet has no drawable outline meshes");
        }
        public void SetVisible(bool value){visible=value;Sync();}
        void LateUpdate(){Sync();}
        void Sync()
        {
            for(int i=0;i<shells.Count;i++)
            {
                var source=sources[i];var shell=shells[i];if(!shell)continue;
                shell.enabled=isActiveAndEnabled&&visible&&source&&source.enabled&&source.gameObject.activeInHierarchy;
                if(!shell.enabled)continue;
                if(source is SkinnedMeshRenderer skin && shell is SkinnedMeshRenderer copy)
                    for(int n=0;n<skin.sharedMesh.blendShapeCount;n++)copy.SetBlendShapeWeight(n,skin.GetBlendShapeWeight(n));
            }
        }
        void OnDisable(){foreach(var shell in shells)if(shell)shell.enabled=false;}
        void OnDestroy(){if(ink)Destroy(ink);}
    }
}
