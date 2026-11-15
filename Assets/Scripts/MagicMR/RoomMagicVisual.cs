using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicMR
{
    // Applies only to the virtual room representation, never the camera passthrough.
    public sealed class RoomMagicVisual : MonoBehaviour
    {
        Renderer[] surfaces;Material[][] original,painted;readonly List<Material> owned=new List<Material>();
        Transform window;float opening;int style;
        public int Style=>style;
        public float WindowProgress=>opening;
        public Transform Window=>window;
        public int OwnedMaterialCount=>owned.Count;
        public void Initialize(Renderer[] roomSurfaces,Transform parent)
        {
            if(surfaces!=null)return;
            surfaces=roomSurfaces;original=new Material[surfaces.Length][];painted=new Material[surfaces.Length][];
            var shader=Resources.Load<Shader>("MagicMR/RoomStyle");if(!shader)throw new InvalidOperationException("RoomStyle shader missing");
            var cache=new Dictionary<Material,Material>();
            for(int i=0;i<surfaces.Length;i++){
                original[i]=surfaces[i].sharedMaterials;painted[i]=new Material[original[i].Length];
                for(int j=0;j<original[i].Length;j++){
                    var source=original[i][j];if(!source)throw new InvalidOperationException("Room material missing");
                    if(!cache.TryGetValue(source,out var m)){
                        m=new Material(shader){name=source.name+" (owned room magic)"};owned.Add(m);cache.Add(source,m);
                        m.SetColor("_RoomBaseTint",source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):Color.white);
                        if(source.HasProperty("_BaseMap")){m.SetTexture("_BaseMap",source.GetTexture("_BaseMap"));m.SetTextureScale("_BaseMap",source.GetTextureScale("_BaseMap"));m.SetTextureOffset("_BaseMap",source.GetTextureOffset("_BaseMap"));}
                    }
                    painted[i][j]=m;
                }
            }
            window=new GameObject("Virtual floor-to-ceiling window - illustrated landscape").transform;window.SetParent(parent,false);window.localPosition=new Vector3(-.2f,.012f,.242f);
            var lit=Shader.Find("Universal Render Pipeline/Lit");if(!lit)throw new InvalidOperationException("Room window material missing");
            Material Make(string name,Color color){var m=new Material(lit){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.25f);owned.Add(m);return m;}
            var frame=Make("Owned window warm brass",new Color(.20f,.17f,.12f));var sky=Make("Owned window blue sky",new Color(.47f,.70f,.77f));
            // Bounded opaque inset illustration: no claim of real room reconstruction or a portal.
            Box("Landscape inset",new Vector3(0,.258f,.012f),new Vector3(.32f,.516f,.008f),sky);
            var distant=Make("Owned window distant hills",new Color(.23f,.40f,.43f));
            var near=Make("Owned window near hills",new Color(.12f,.26f,.25f));
            for(int i=0;i<7;i++){
                float h=.08f+.07f*(.5f+.5f*Mathf.Sin(i*1.7f));
                Box("Distant ridge "+i,new Vector3(-.135f+i*.045f,h*.5f,.005f),new Vector3(.05f,h,.006f),distant);
                Box("Near ridge "+i,new Vector3(-.135f+i*.045f,.025f+.012f*Mathf.Cos(i),-.001f),new Vector3(.05f,.07f,.006f),near);
            }
            foreach(float x in new[]{-.17f,0,.17f})Box("Vertical mullion",new Vector3(x,.26f,-.014f),new Vector3(.012f,.52f,.024f),frame);
            foreach(float y in new[]{0f,.26f,.52f})Box("Horizontal rail",new Vector3(0,y,-.014f),new Vector3(.352f,.012f,.024f),frame);
            SetWindow(0);
        }
        void Box(string name,Vector3 position,Vector3 size,Material material){
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(window,false);go.transform.localPosition=position;go.transform.localScale=size;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);go.GetComponent<Renderer>().sharedMaterial=material;
        }
        public void SetStyle(int value){
            value=Mathf.Clamp(value,0,2);if(surfaces==null||style==value)return;style=value;
            for(int i=0;i<surfaces.Length;i++)if(surfaces[i])surfaces[i].sharedMaterials=value==0?original[i]:painted[i];
            foreach(var m in owned)if(m&&m.HasProperty("_RoomStyle"))m.SetFloat("_RoomStyle",value);
        }
        public void Step(float dt,bool requested){if(float.IsFinite(dt)&&dt>=0)SetWindow(Mathf.MoveTowards(opening,requested?1:0,Mathf.Min(dt,.1f)/.65f));}
        public void SetWindow(float value){opening=float.IsFinite(value)?Mathf.Clamp01(value):0;if(window){window.localScale=new Vector3(1,Mathf.SmoothStep(0,1,opening),1);window.gameObject.SetActive(opening>0);}}
        void OnDisable(){SetStyle(0);SetWindow(0);}
        void OnDestroy(){foreach(var m in owned)if(m)Destroy(m);}
    }
}
