using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicMR
{
    // Reuses the accepted geometry/textures. Original shared materials are restored exactly.
    public sealed class NaturalCartoonStyle:MonoBehaviour
    {
        Renderer[] renderers;
        Material[][] originals,cartoons;
        readonly List<Material> owned=new List<Material>();
        bool active;
        public bool Active=>active;
        public int OwnedMaterialCount=>owned.Count;
        public void Initialize()
        {
            if(renderers!=null)return;
            var shader=Resources.Load<Shader>("MagicMR/StatueCartoon");
            if(!shader)throw new InvalidOperationException("Statue cartoon shader missing");
            renderers=GetComponentsInChildren<Renderer>(true);originals=new Material[renderers.Length][];cartoons=new Material[renderers.Length][];
            var converted=new Dictionary<Material,Material>();
            for(int i=0;i<renderers.Length;i++)
            {
                originals[i]=renderers[i].sharedMaterials;cartoons[i]=new Material[originals[i].Length];
                for(int j=0;j<originals[i].Length;j++)
                {
                    var source=originals[i][j];if(!source)throw new InvalidOperationException("Statue material missing");
                    if(!converted.TryGetValue(source,out var material))
                    {
                        material=new Material(shader){name=source.name+" (owned cartoon)"};owned.Add(material);converted.Add(source,material);
                        material.SetColor("_BaseColor",source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):Color.white);
                        if(source.HasProperty("_BaseMap")){material.SetTexture("_BaseMap",source.GetTexture("_BaseMap"));material.SetTextureScale("_BaseMap",source.GetTextureScale("_BaseMap"));material.SetTextureOffset("_BaseMap",source.GetTextureOffset("_BaseMap"));}
                        if(source.HasProperty("_Cull"))material.SetFloat("_Cull",source.GetFloat("_Cull"));
                    }
                    cartoons[i][j]=material;
                }
            }
        }
        public void SetActive(bool value)
        {
            Initialize();if(active==value)return;active=value;
            for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].sharedMaterials=value?cartoons[i]:originals[i];
        }
        void OnDisable(){if(renderers!=null)SetActive(false);}
        void OnDestroy(){foreach(var material in owned)if(material)Destroy(material);}
    }
}
