using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagicMR
{
    // Original bounded object-portal interaction, not the Wormhole game or a
    // physical-hand/cable simulator. Uses the existing selected Room and recognizer.
    public sealed class RoomPortalPuzzle : MonoBehaviour
    {
        public Transform Entrance {get;private set;}
        public Transform Exit {get;private set;}
        public Transform Seed {get;private set;}
        public Transform Socket {get;private set;}
        public bool Open {get;private set;}
        public bool Held {get;private set;}
        public bool Mapped {get;private set;}
        public bool Powered {get;private set;}
        public bool Enabled {get;private set;}
        public bool IsTravelling=>travel>=0;
        public int OwnedMaterialCount=>materials.Count;
        public bool UsesConfirmedSurface => surfacePoint.HasValue;
        public Vector3 SurfaceInteractionCenter => surfacePoint.GetValueOrDefault() + Vector3.up * .085f;

        internal void PlaceOnConfirmedSurface(Vector3 point)
        {
            surfacePoint = point;
            root.SetParent(null, false);
            root.localScale = Vector3.one;
            root.SetPositionAndRotation(SurfaceInteractionCenter, Quaternion.Euler(0, owner.transform.eulerAngles.y, 0));
            owner.SetRoomReferenceVisible(false);
            Clear();
            Status = "Portals placed on the confirmed surface; B opens them";
        }

        internal void ResetPlacement()
        {
            if (UsesConfirmedSurface && owner) owner.SetRoomReferenceVisible(true);
            surfacePoint = null;
            RefreshReachLayout();
        }

        public void RefreshReachLayout()
        {
            if (!root || !owner || !owner.visual || UsesConfirmedSurface) return;
            if (root.parent != owner.visual)
            {
                if (!owner.visual.gameObject.activeInHierarchy)
                {
                    root.gameObject.SetActive(false);
                    return;
                }
                root.SetParent(owner.visual, false);
                root.gameObject.SetActive(Enabled);
            }
            var visualScale = owner.visual.lossyScale;
            float largestAxis = Mathf.Max(.001f, Mathf.Max(Mathf.Abs(visualScale.x), Mathf.Max(Mathf.Abs(visualScale.y), Mathf.Abs(visualScale.z))));
            float scale = Mathf.Min(1, .42f / largestAxis);
            root.localScale = Vector3.one * scale;
            root.localPosition = new Vector3(0, .21f, -.23f) * scale;
            root.localRotation = Quaternion.identity;
        }
        public string Status {get;private set;}="B opens portals; A picks up/releases the light seed; C sends it through; D celebrates a powered socket";
        readonly List<Material> materials=new List<Material>();
        Transform root,viewA,viewB;Renderer rimA,rimB,receiver;
        Transform apertureA,apertureB;Renderer mappedA,mappedB;
        Vector3? surfacePoint;
        MaterialPropertyBlock viewProperties,receiverProperties;
        Material gold;Mesh torus;ParkActor owner;Vector3 previousHand;float lastHandAt=-1,travel=-1;
        Vector3 travelStart,travelEnd;bool hasHand;float glow;
        readonly Stack<Snapshot> history=new Stack<Snapshot>();
        struct Snapshot {public bool open,powered;public Vector3 seed;}
        const float Radius=.065f;

        public static Vector3 MapPoint(Transform from,Transform to,Vector3 point)
        {return to.TransformPoint(Quaternion.Euler(0,180,0)*from.InverseTransformPoint(point));}
        public static bool CrossesOpening(Transform portal,Vector3 before,Vector3 after,float radius)
        {
            var a=portal.InverseTransformPoint(before);var b=portal.InverseTransformPoint(after);
            if(!TabletopHandSample.Finite(a)||!TabletopHandSample.Finite(b)||Mathf.Abs(a.z)<1e-6f||Mathf.Abs(b.z-a.z)<1e-7f||a.z*b.z>0)return false;
            float t=-a.z/(b.z-a.z);if(t<0||t>1)return false;
            var p=Vector3.Lerp(a,b,t);return p.x*p.x+p.y*p.y<radius*radius;
        }
        Material Make(Shader shader,string name,Color color,int stencil=0)
        {if(!shader)throw new InvalidOperationException("Portal shader missing: "+name);var m=new Material(shader){name=name};materials.Add(m);if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);if(stencil!=0)m.SetInt("_PortalId",stencil);return m;}
        Transform Part(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return go.transform;
        }
        public void Initialize(ParkActor actor)
        {
            if(root)return;owner=actor;viewProperties=new MaterialPropertyBlock();receiverProperties=new MaterialPropertyBlock();root=new GameObject("Owned room light-seed portal puzzle").transform;root.SetParent(actor.visual,false);
            RefreshReachLayout();
            var lit=Shader.Find("Universal Render Pipeline/Lit");
            var blue=Make(lit,"Owned portal blue rim",new Color(.06f,.65f,.9f));var orange=Make(lit,"Owned portal orange rim",new Color(1,.35f,.06f));
            gold=Make(lit,"Owned portal light seed",new Color(1,.78f,.18f));
            var baseMat=Make(lit,"Owned portal receiver",new Color(.14f,.22f,.25f));
            torus=RingMesh();
            Transform Portal(string name,float x,Material rim,int stencil,out Renderer ring)
            {
                var p=new GameObject(name).transform;p.SetParent(root,false);p.localPosition=new Vector3(x,0,0);
                var go=new GameObject("Portal rim");go.transform.SetParent(p,false);go.AddComponent<MeshFilter>().sharedMesh=torus;ring=go.AddComponent<MeshRenderer>();ring.sharedMaterial=rim;
                var mask=Make(Resources.Load<Shader>("MagicMR/PortalMask"),"Owned portal aperture "+stencil,Color.black,stencil);
                Part(p,"Stereo portal aperture",PrimitiveType.Quad,Vector3.zero,Vector3.one*Radius*2,mask);
                return p;
            }
            Entrance=Portal("Blue entrance",-.125f,blue,21,out rimA);Exit=Portal("Orange exit",.125f,orange,22,out rimB);
            apertureA=Entrance.Find("Stereo portal aperture");apertureB=Exit.Find("Stereo portal aperture");
            Seed=Part(root,"Light seed - tracked hand carry",PrimitiveType.Sphere,new Vector3(-.125f,0,-.055f),Vector3.one*.027f,gold);
            Socket=Part(root,"Light seed receiver",PrimitiveType.Cube,new Vector3(.125f,-.024f,-.055f),new Vector3(.065f,.012f,.065f),baseMat);receiver=Socket.GetComponent<Renderer>();
            // Only the owned seed/receiver are replicated through these windows.
            // No extra cameras, render features or mono render textures are used.
            Transform View(Transform p,int id)
            {
                var m=Make(Resources.Load<Shader>("MagicMR/PortalView"),"Owned stereo portal view "+id,new Color(1,.78f,.18f),id);
                return Part(root,"Mapped seed view "+id,PrimitiveType.Sphere,Vector3.zero,Vector3.one*.027f,m);
            }
            viewA=View(Entrance,21);viewB=View(Exit,22);mappedA=viewA.GetComponent<Renderer>();mappedB=viewB.GetComponent<Renderer>();SetEnabled(false);
        }
        static Mesh RingMesh()
        {
            const int around=40,tube=6;var v=new Vector3[around*tube];var t=new int[around*tube*6];
            for(int i=0;i<around;i++)for(int j=0;j<tube;j++){
                float a=i*Mathf.PI*2/around,b=j*Mathf.PI*2/tube;
                v[i*tube+j]=new Vector3(Mathf.Cos(a)*(Radius+.007f*Mathf.Cos(b)),Mathf.Sin(a)*(Radius+.007f*Mathf.Cos(b)),.007f*Mathf.Sin(b));
                int k=(i*tube+j)*6,n=(i+1)%around*tube+j,m=i*tube+(j+1)%tube,q=(i+1)%around*tube+(j+1)%tube;
                t[k]=i*tube+j;t[k+1]=n;t[k+2]=m;t[k+3]=m;t[k+4]=n;t[k+5]=q;
            }
            var mesh=new Mesh{name="Owned portal torus 480 triangles"};mesh.vertices=v;mesh.triangles=t;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public void SetEnabled(bool value){Enabled=value;if(!value)ResetPlacement();Clear();if(root)root.gameObject.SetActive(value);}
        public void Clear(){Held=Mapped=Powered=Open=false;hasHand=false;lastHandAt=-1;travel=-1;glow=0;history.Clear();if(Seed)Seed.localPosition=new Vector3(-.125f,0,-.055f);ApplyViews();}
        public void EndHand(){if(Held){Held=Mapped=false;Seed.localPosition=new Vector3(-.125f,0,-.055f);Status="Hand unavailable; seed returned safely";}hasHand=false;lastHandAt=-1;}
        public void ObserveHand(Vector3 hand,float time)
        {
            if(!Enabled||!TabletopHandSample.Finite(hand)||!float.IsFinite(time)){EndHand();return;}
            if(hasHand&&(time<=lastHandAt||time-lastHandAt>TabletopGestureRules.MaximumSampleGapSeconds)){EndHand();}
            if(Held){
                if(Open&&hasHand&&CrossesOpening(Entrance,previousHand,hand,Radius))Mapped=!Mapped;
                Seed.position=Mapped?MapPoint(Entrance,Exit,hand):hand;
            }
            previousHand=hand;lastHandAt=time;hasHand=true;ApplyViews();
        }
        void Remember(){history.Push(new Snapshot{open=Open,powered=Powered,seed=Seed.localPosition});while(history.Count>12){var a=history.ToArray();history.Clear();for(int i=10;i>=0;i--)history.Push(a[i]);}}
        public bool TryAction(ParkAction action,Vector3 hand)
        {
            if(!Enabled||!TabletopHandSample.Finite(hand))return false;
            // The controller's closest joint owns reach gating. Picking up an
            // object uses the fresh fingertip sample, rather than that palm joint.
            var commandPoint=hasHand&&Time.unscaledTime>=lastHandAt&&Time.unscaledTime-lastHandAt<=TabletopGestureRules.MaximumSampleGapSeconds?previousHand:hand;
            if(action==ParkAction.Undo){EndHand();travel=-1;if(history.Count==0)return false;var old=history.Pop();Open=old.open;Powered=old.powered;Seed.localPosition=old.seed;ApplyViews();Status="Portal interaction undone";return true;}
            if(travel>=0){Status="Wait for the seed to arrive";return false;}
            if(action==ParkAction.Awaken){Remember();Open=!Open;if(!Open)EndHand();Status=Open?"Portals linked: A picks up the seed; carry it through blue, then release on the orange socket":"Portals closed";ApplyViews();return true;}
            if(action==ParkAction.Style){
                if(Held){Remember();Held=false;hasHand=false;var target=Socket.position+Socket.up*.022f*root.lossyScale.y;Powered=Vector3.Distance(Seed.position,target)<.04f*root.lossyScale.y;if(Powered)Seed.position=target;Mapped=false;Status=Powered?"Socket powered! D celebrates":"Seed released";ApplyViews();return true;}
                if(Vector3.Distance(commandPoint,Seed.position)>Mathf.Max(.025f,.065f*root.lossyScale.y)){Status="Bring your fingertips to the light seed before A";return false;}
                Remember();Powered=false;Held=true;Mapped=false;hasHand=true;previousHand=commandPoint;lastHandAt=Time.unscaledTime;Status="Seed held: move through the blue opening; A releases";return true;
            }
            if(action==ParkAction.Wind){
                if(!Open){Status="Open the linked portals with B first";return false;}
                if(!Held&&Vector3.Distance(commandPoint,Seed.position)>Mathf.Max(.03f,.10f*root.lossyScale.y)){Status="Cast C near the seed to send it through";return false;}
                Remember();Held=Mapped=false;hasHand=false;travel=0;travelStart=Seed.position;travelEnd=Socket.position+Socket.up*.022f*root.lossyScale.y;Powered=false;Status="Wind sends the seed through the linked portals";return true;
            }
            if(action==ParkAction.Reward){if(!Powered){Status="Power the socket first";return false;}Remember();glow=1;Status="Light seed circuit completed";return true;}
            return false;
        }
        void ApplyViews()
        {
            if(!root)return;
            if(apertureA)apertureA.gameObject.SetActive(Enabled&&Open);if(apertureB)apertureB.gameObject.SetActive(Enabled&&Open);
            if(viewA&&viewB){
                viewA.gameObject.SetActive(Enabled&&Open);viewB.gameObject.SetActive(Enabled&&Open);
                viewA.position=MapPoint(Exit,Entrance,Seed.position);viewB.position=MapPoint(Entrance,Exit,Seed.position);
                Plane(Entrance,mappedA);Plane(Exit,mappedB);
            }
            if(receiver){receiverProperties.SetColor("_BaseColor",Powered?new Color(.9f,.7f,.12f):new Color(.14f,.22f,.25f));receiver.SetPropertyBlock(receiverProperties);}
        }
        void Plane(Transform portal,Renderer renderer)
        {var normal=portal.forward;viewProperties.SetVector("_ClipPlane",new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(normal,portal.position)));renderer.SetPropertyBlock(viewProperties);}
        void Update()
        {
            if(!Enabled||!owner||!owner.tracked)return;
            RefreshReachLayout();
            if(Held&&hasHand&&Time.unscaledTime-lastHandAt>TabletopGestureRules.MaximumSampleGapSeconds)EndHand();
            if(travel>=0){
                float dt=Time.unscaledDeltaTime;if(!float.IsFinite(dt)||dt<0)return;travel+=Mathf.Min(dt,.1f);
                if(travel<.45f)Seed.position=Vector3.Lerp(travelStart,Entrance.position,Mathf.SmoothStep(0,1,travel/.45f));
                else Seed.position=Vector3.Lerp(Exit.position,travelEnd,Mathf.SmoothStep(0,1,(travel-.45f)/.65f));
                if(travel>=1.1f){travel=-1;Powered=true;Seed.position=travelEnd;Status="Socket powered! D celebrates";}
            }
            glow=Mathf.MoveTowards(glow,0,Time.unscaledDeltaTime*.35f);
            if(Seed)Seed.localScale=Vector3.one*(.027f+Mathf.Sin(Time.unscaledTime*8)*.003f*glow);
            ApplyViews();
        }
        void OnEnable(){if(root){RefreshReachLayout();root.gameObject.SetActive(Enabled&&(UsesConfirmedSurface||root.parent==owner.visual));}}
        void OnDisable(){EndHand();if(root)root.gameObject.SetActive(false);}
        void OnDestroy(){if(root)Destroy(root.gameObject);if(torus)Destroy(torus);foreach(var m in materials)if(m)Destroy(m);}
    }
}
