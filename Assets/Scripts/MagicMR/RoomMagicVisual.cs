using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicMR
{
    public sealed class RoomMagicVisual : MonoBehaviour
    {
        const float WindowWidth = .352f;
        static readonly Vector3 DefaultWindowPosition = new Vector3(-.2f,.012f,.242f);
        Renderer[] surfaces;Material[][] original,painted;readonly List<Material> owned=new List<Material>();
        Transform window,windowLayout,windowLayoutHome;float opening,landscapePhase;int style;
        public bool WallPlaced { get; private set; }
        Mesh landscapeMesh;Renderer landscapeRenderer;MaterialPropertyBlock landscapeProperties;
        GameObject styleOverlay;
        Material styleOverlayMaterial;
        float styleProgress;
        public int Style=>style;
        public float WindowProgress=>opening;
        public Transform Window=>window;
        public float LandscapePhase=>landscapePhase;
        public Renderer LandscapeRenderer=>landscapeRenderer;
        public int OwnedMaterialCount=>owned.Count;
        public RoomPhotoTransfer PhotoTransfer { get; private set; }
        public void Initialize(Renderer[] roomSurfaces,Transform parent,bool passthrough=false)
        {
            if(surfaces!=null)return;
            surfaces=roomSurfaces;original=new Material[surfaces.Length][];painted=new Material[surfaces.Length][];
            if(passthrough)BuildStyleOverlay();
            var shader=Resources.Load<Shader>("MagicMR/RoomStyle");
            if(surfaces.Length>0&&!shader)throw new InvalidOperationException("RoomStyle shader missing");
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
            if(passthrough)
            {
                windowLayoutHome=parent;
                windowLayout=new GameObject("Tracked room window layout").transform;
                windowLayout.SetParent(parent,false);
                parent=windowLayout;
            }
            window=new GameObject("Virtual floor-to-ceiling window - illustrated landscape").transform;
            window.SetParent(parent,false);
            window.localPosition=DefaultWindowPosition;
            var lit=Shader.Find("Universal Render Pipeline/Lit");if(!lit)throw new InvalidOperationException("Room window material missing");
            Material Make(string name,Color color){var m=new Material(lit){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.25f);owned.Add(m);return m;}
            var lake=passthrough?Resources.Load<Texture2D>("MagicMR/SwanLake"):null;
            if(passthrough&&!lake)throw new InvalidOperationException("Room swan lake artwork missing");
            var landscapeShader=Resources.Load<Shader>(passthrough?"MagicMR/SwanLakeWindow":"MagicMR/RoomWindowLandscape");if(!landscapeShader)throw new InvalidOperationException("Room window landscape shader missing");
            var landscapeMaterial=new Material(landscapeShader){name="Owned room window landscape"};owned.Add(landscapeMaterial);
            var landscape=new GameObject("Window mountain-lake relief");landscape.transform.SetParent(window,false);
            if(lake)landscapeMaterial.SetTexture("_BaseMap",lake);
            landscapeMesh=passthrough?RoomWindowLandscape.CreateLakeMesh():RoomWindowLandscape.CreateMesh();landscape.AddComponent<MeshFilter>().sharedMesh=landscapeMesh;
            landscapeRenderer=landscape.AddComponent<MeshRenderer>();landscapeRenderer.sharedMaterial=landscapeMaterial;
            landscapeRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;landscapeRenderer.receiveShadows=false;
            landscapeProperties=new MaterialPropertyBlock();
            if(!passthrough){
                var frame=Make("Owned window warm brass",new Color(.20f,.17f,.12f));
                foreach(float x in new[]{-.17f,0,.17f})Box("Vertical mullion",new Vector3(x,.26f,-.014f),new Vector3(.012f,.52f,.024f),frame);
                foreach(float y in new[]{0f,.26f,.52f})Box("Horizontal rail",new Vector3(0,y,-.014f),new Vector3(WindowWidth,.012f,.024f),frame);
            }
            SetWindow(0);
            PhotoTransfer=gameObject.AddComponent<RoomPhotoTransfer>();PhotoTransfer.Initialize(window,landscapeRenderer,lake);
        }
        void BuildStyleOverlay()
        {
            var shader=Resources.Load<Shader>("MagicMR/RoomPassthroughStyle");
            if(!shader)throw new InvalidOperationException("RoomPassthroughStyle shader missing");
            styleOverlayMaterial=new Material(shader){name="Owned room passthrough style"};
            owned.Add(styleOverlayMaterial);
            styleOverlay=GameObject.CreatePrimitive(PrimitiveType.Quad);
            styleOverlay.name="MR room style wash";
            styleOverlay.transform.SetParent(transform,false);
            styleOverlay.transform.localPosition=Vector3.up*.035f;
            styleOverlay.transform.localRotation=Quaternion.Euler(90,0,0);
            styleOverlay.transform.localScale=Vector3.one*.6f;
            RemoveCollider(styleOverlay);
            var renderer=styleOverlay.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=styleOverlayMaterial;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            styleOverlay.SetActive(false);
        }
        void Box(string name,Vector3 position,Vector3 size,Material material){
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(window,false);go.transform.localPosition=position;go.transform.localScale=size;
            RemoveCollider(go);
            go.GetComponent<Renderer>().sharedMaterial=material;
        }
        static void RemoveCollider(GameObject primitive)
        {
            var collider=primitive.GetComponent<Collider>();
            collider.enabled=false;
            if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);
        }
        public void SetStyle(int value){
            value=Mathf.Clamp(value,0,2);if(surfaces==null||style==value)return;style=value;
            if(styleOverlay)
            {
                styleProgress=0;
                styleOverlayMaterial.SetFloat("_Reveal",0);
                styleOverlayMaterial.SetFloat("_RoomStyle",value);
                styleOverlay.SetActive(value>0);
            }
            for(int i=0;i<surfaces.Length;i++)if(surfaces[i])surfaces[i].sharedMaterials=value==0?original[i]:painted[i];
            foreach(var m in owned)if(m&&m.HasProperty("_RoomStyle"))m.SetFloat("_RoomStyle",value);
        }
        public void Step(float dt,bool requested){
            if(!float.IsFinite(dt)||dt<0)return;
            float step=Mathf.Min(dt,.1f);
            if(styleOverlay&&style>0)
            {
                styleProgress=Mathf.MoveTowards(styleProgress,1,step/.55f);
                styleOverlayMaterial.SetFloat("_Reveal",styleProgress);
            }
            if(PhotoTransfer&&PhotoTransfer.Running){
                if(!requested)PhotoTransfer.Cancel();
                else {PhotoTransfer.Step(step);SetWindow(PhotoTransfer.Reveal);}
            } else SetWindow(Mathf.MoveTowards(opening,requested?1:0,step/.65f));
            if(window&&window.gameObject.activeSelf&&landscapeRenderer){
                landscapePhase=Mathf.Repeat(landscapePhase+step*.18f,Mathf.PI*2);
                landscapeProperties.SetFloat("_LandscapePhase",landscapePhase);landscapeRenderer.SetPropertyBlock(landscapeProperties);
            }
        }
        public void SetWindow(float value){
            opening=float.IsFinite(value)?Mathf.Clamp01(value):0;
            if(!window)return;
            if(windowLayout){
                landscapeProperties.SetFloat("_Opening",opening);
                landscapeRenderer.SetPropertyBlock(landscapeProperties);
            }else window.localScale=new Vector3(1,Mathf.SmoothStep(0,1,opening),1);
            window.gameObject.SetActive(opening>0);
        }
        public void BeginPhotoScene(Vector3 hand){SetWindow(0);PhotoTransfer.Begin(hand);}
        public void CancelPhotoScene(){if(PhotoTransfer)PhotoTransfer.Cancel();}
        internal void PlaceOnWall(Vector3 bottomCenter, Quaternion rotation, float width)
        {
            windowLayout.SetParent(null,false);
            windowLayout.SetPositionAndRotation(bottomCenter,rotation);
            windowLayout.localScale=Vector3.one*(width/WindowWidth);
            window.localPosition=Vector3.zero;
            WallPlaced=true;
        }
        internal void ClearWallPlacement()
        {
            if(!WallPlaced)return;
            WallPlaced=false;
            windowLayout.SetParent(windowLayoutHome,false);
            windowLayout.localPosition=Vector3.zero;
            windowLayout.localRotation=Quaternion.identity;
            windowLayout.localScale=Vector3.one;
            window.localPosition=DefaultWindowPosition;
        }
        void OnDisable(){CancelPhotoScene();SetStyle(0);SetWindow(0);ClearWallPlacement();}
        void OnDestroy(){if(PhotoTransfer)Destroy(PhotoTransfer);if(windowLayout)Destroy(windowLayout.gameObject);else if(window)Destroy(window.gameObject);if(styleOverlay)Destroy(styleOverlay);if(landscapeMesh)Destroy(landscapeMesh);foreach(var m in owned)if(m)Destroy(m);}
    }
}
