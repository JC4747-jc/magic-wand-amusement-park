using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MagicMR
{
    public enum ParkScenario { Tree, Bugs, Statue, Room, Plant, Pet }
    public enum ParkAction { Style, Awaken, Wind, Reward, Pull, Poke, Feed, GrowUp, GrowDown, Undo, Reset, Stop, Bloom }

    /// <summary>Virtual representation only. Root is a tracking/confirmed-placement anchor.</summary>
    public sealed class ParkActor : MonoBehaviour
    {
        public ParkScenario scenario;
        public string species = "Cat";
        public bool magicTree;
        public float interactionRadius = .65f;
        [Tooltip("Gesture range for explicitly virtual test objects; does not change real target distance checks.")]
        public float virtualInteractionRadius = 2f;
        public float cooldown = .45f;
        public float transitionSeconds = .7f;
        public float environmentRadius = .6f;
        public float headOffset = .38f;
        public bool reliableHeadPose;
        public bool confirmedPlane;
        public bool sizeConfirmed { get; private set; }
        public void SetVirtualPreviewSize(float height) {
            if(!virtualTarget || !built || !float.IsFinite(height) || height<.05f || height>1 || neutralBounds.size.y<.001f)return;
            float scale=height/Mathf.Max(neutralBounds.size.x,neutralBounds.size.y,neutralBounds.size.z);
            representationFit.localScale=Vector3.one*scale;
            representationFit.localPosition=-new Vector3(neutralBounds.center.x,neutralBounds.min.y,neutralBounds.center.z)*scale;
            sizeConfirmed=false;
            if(label)label.transform.localPosition=Vector3.up*(neutralBounds.size.y*scale+.08f);
        }
        Transform representationFit;
        Bounds neutralBounds;
        public Vector3 ConfirmedBottomWorld=>representationFit?representationFit.TransformPoint(new Vector3(neutralBounds.center.x,neutralBounds.min.y,neutralBounds.center.z)):transform.position;
        public float ConfirmedHeight=>sizeConfirmed?neutralBounds.size.y*representationFit.localScale.y:0;
        public bool FitRepresentation(Vector3 bottom,Vector3 top,bool valid) {
            if(!built || !tracked || Busy || !valid || !FinitePoint(bottom) || !FinitePoint(top) || Vector3.Dot(transform.up,Vector3.up)<.99f)return false;
            float height=top.y-bottom.y;
            if(height<.005f || height>3 || new Vector2(top.x-bottom.x,top.z-bottom.z).magnitude>.1f || neutralBounds.size.y<.001f)return false;
            float scale=height/neutralBounds.size.y;
            representationFit.localScale=Vector3.one*scale;
            representationFit.localPosition=transform.InverseTransformPoint(bottom)-new Vector3(neutralBounds.center.x,neutralBounds.min.y,neutralBounds.center.z)*scale;
            sizeConfirmed=true;Feedback="Manual nominal height / bottom confirmed; no 3D pose inference";return true;
        }
        static bool FinitePoint(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
        public bool orientationConfirmed { get; private set; }
        public bool ConfirmOrientation(float yaw) {
            if(!tracked || !float.IsFinite(yaw))return false;
            if(sizeConfirmed) { sizeConfirmed=false;representationFit.localPosition=Vector3.zero;representationFit.localScale=Vector3.one; }
            transform.rotation=Quaternion.Euler(0,yaw,0);orientationConfirmed=true;return true;
        }
        public Transform environmentAnchor;
        public Transform headAnchor;
        public string story = "I watched the seasons from a quiet pedestal.\nOne night, a wind carried a seed to my feet.\nI woke to shelter its first leaf.\nNow each visitor helps my little garden grow.";
        public string storySource = "Original fantasy; this monument's history is not identified";
        public AudioClip storyAudio;
        public bool virtualTarget = true;
        public string instanceId;
        public bool tracked = true;
        public Transform visual;
        public Material sharedMaterial;
        public TextMesh label;
        public float seasonTime;
        public float growthTime = .45f;
        public Vector3 windDirection = Vector3.right;
        public float windStrength = .2f;
        public string Feedback { get; private set; } = "Ready";
        public bool Busy => motion != null;
        public bool IsExiting {get;private set;}
        float entryProgress=1;
        bool hasPresented;
        public float EntryProgress=>entryProgress;
        public int Affection => state.affection;
        // Each selected virtual enemy is one round; its defeated state is the score source of truth.
        public int VirtualScore=>scenario==ParkScenario.Bugs && state.rewarded?100:0;
        public bool RoomCandySelected=>scenario==ParkScenario.Room && state.style==1;
        public int Revision { get; private set; }

        [Serializable] struct State { public int style, affection; public bool awake, rewarded, bubble, sealedTree, scar, wind, bloom; public float growth, season; }
        State state;
        readonly Stack<State> history = new Stack<State>();
        readonly List<Renderer> body = new List<Renderer>();
        readonly List<Transform> foliage = new List<Transform>();
        readonly List<Transform> branches = new List<Transform>();
        readonly List<Transform> pool = new List<Transform>();
        Transform eyes, bubble, portal, jellyfish, scar, reward, environment, branchRoot, preview, spell, ears;
        readonly List<Transform> dreamContents=new List<Transform>();
        Transform[] stoppingEffects;
        Transform statueScan;
        Transform plantFlower;
        float bloomVisibility;
        float bugDefeatProgress;
        readonly List<Vector3> bugRestScales=new List<Vector3>();
        readonly List<Transform> plantPetals=new List<Transform>();
        public bool PlantBloomVisible=>plantFlower && plantFlower.gameObject.activeInHierarchy;
        float dreamVisibility;
        float windBlend;
        Vector3 dreamRestScale=Vector3.one*.2f;
        Vector3[] stoppingScales;
        Transform selectionHighlight;
        AudioSource audioSource;
        MaterialPropertyBlock block;
        Mesh fragmentMesh;
        Coroutine motion;
        float acceptedAt = -100, bend, spin, stretch, targetGrowth, targetSeason, styleMix;
        Vector3 dance;
        Color targetColor = new Color(.2f,.65f,.35f), currentColor;
        int particleCount;
        float particleStart;
        ParkAction particleKind;
        bool built;
        static readonly Color[] SeasonColors={new Color(1,.6f,.7f),new Color(.2f,.7f,.25f),new Color(1,.55f,.12f),new Color(.45f,.35f,.25f)};

        public void Build()
        {
            if (built) return; built = true;
            block = new MaterialPropertyBlock();
            if (sharedMaterial == null) throw new InvalidOperationException("ParkActor requires a shared URP material");
            if (visual == null) { visual = new GameObject("Visual - animation only").transform; visual.SetParent(transform,false); }
            representationFit=new GameObject("Manual fit - separate from tracking and animation").transform;representationFit.SetParent(transform,false);visual.SetParent(representationFit,false);
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.spatialBlend=1; audioSource.volume=.18f; audioSource.playOnAwake=false;
            eyes = new GameObject("Eyes").transform; eyes.SetParent(visual,false);
            Part(eyes,"LeftEye",PrimitiveType.Sphere,new Vector3(-.055f,.25f,-.105f),Vector3.one*.025f,Color.black,false);
            Part(eyes,"RightEye",PrimitiveType.Sphere,new Vector3(.055f,.25f,-.105f),Vector3.one*.025f,Color.black,false);
            switch(scenario)
            {
                case ParkScenario.Tree:
                    Part(visual,"Trunk",PrimitiveType.Cylinder,new Vector3(0,.28f,0),new Vector3(.13f,.28f,.13f),new Color(.3f,.16f,.08f));
                    branchRoot=new GameObject("Decorative virtual branches").transform; branchRoot.SetParent(visual,false);
                    for(int i=0;i<5;i++) {
                        var pivot=new GameObject("BranchPivot").transform; pivot.SetParent(branchRoot,false); pivot.localPosition=new Vector3(0,.32f+i*.03f,0); pivot.localRotation=Quaternion.Euler(0,i*72,40);
                        Part(pivot,"Branch",PrimitiveType.Cylinder,new Vector3(0,.12f,0),new Vector3(.025f,.14f,.025f),new Color(.3f,.16f,.08f)); branches.Add(pivot);
                        foliage.Add(Part(pivot,"Crown",PrimitiveType.Sphere,new Vector3(0,.28f,0),new Vector3(.3f,.25f,.28f),targetColor));
                    }
                    scar=Part(visual,"Virtual scar",PrimitiveType.Sphere,new Vector3(0,.29f,-.07f),new Vector3(.05f,.07f,.02f),Color.red,false);
                    break;
                case ParkScenario.Bugs:
                    Part(visual,"Pixel body",PrimitiveType.Cube,Vector3.zero,new Vector3(.13f,.025f,.08f),new Color(.5f,.16f,.1f));
                    Part(visual,"Pixel head",PrimitiveType.Cube,new Vector3(0,0,-.06f),new Vector3(.06f,.03f,.04f),Color.red);
                    for(int i=0;i<(species=="Spider"?8:6);i++) { float side=i%2==0?-1:1; var leg=Part(visual,"Pixel leg",PrimitiveType.Cube,new Vector3(side*.09f,0,(i/2-1)*.025f),new Vector3(.1f,.013f,.012f),Color.black); leg.localRotation=Quaternion.Euler(0,side*(i/2-1)*20,0); }
                    eyes.localPosition=new Vector3(0,-.25f,.02f); eyes.localScale=Vector3.one*.4f;
                    break;
                case ParkScenario.Statue:
                    var scan=new GameObject("Virtual statue scan ring").AddComponent<LineRenderer>();
                    statueScan=scan.transform;statueScan.SetParent(visual,false);
                    scan.useWorldSpace=false;scan.loop=true;scan.positionCount=48;scan.widthMultiplier=.006f;scan.sharedMaterial=sharedMaterial;
                    var scanColor=new MaterialPropertyBlock();scanColor.SetColor("_BaseColor",Color.cyan);scan.SetPropertyBlock(scanColor);
                    for(int i=0;i<48;i++) {float a=i*Mathf.PI*2/48;scan.SetPosition(i,new Vector3(Mathf.Cos(a)*.25f,0,Mathf.Sin(a)*.25f));}
                    statueScan.gameObject.SetActive(false);
                    Part(visual,"Pedestal",PrimitiveType.Cube,new Vector3(0,.06f,0),new Vector3(.35f,.12f,.28f),Color.gray);
                    Part(visual,"Torso",PrimitiveType.Capsule,new Vector3(0,.28f,0),new Vector3(.19f,.16f,.16f),Color.gray);
                    Part(visual,"Head",PrimitiveType.Sphere,new Vector3(0,.49f,0),Vector3.one*.16f,Color.gray);
                    for(int i=-1;i<=1;i+=2) branches.Add(Part(visual,"Arm",PrimitiveType.Capsule,new Vector3(i*.16f,.3f,0),new Vector3(.045f,.12f,.045f),Color.gray));
                    eyes.localPosition=Vector3.up*.23f;
                    break;
                case ParkScenario.Room:
                    for(int i=0;i<3;i++) Part(visual,"Local decoration",PrimitiveType.Cube,new Vector3((i-1)*.25f,.15f,.15f),new Vector3(.18f,.25f,.04f),new Color(.35f,.5f,.7f));
                    portal=Part(visual,"Virtual window",PrimitiveType.Cube,new Vector3(0,.3f,.2f),new Vector3(.01f,.45f,.02f),new Color(.025f,.07f,.22f),false);
                    jellyfish=new GameObject("Virtual jellyfish lamp").transform; jellyfish.SetParent(visual,false); jellyfish.localPosition=Vector3.up*.6f;
                    Part(jellyfish,"Bell",PrimitiveType.Sphere,Vector3.zero,new Vector3(.22f,.12f,.22f),new Color(.5f,.8f,1),false);
                    for(int i=0;i<6;i++) Part(jellyfish,"Tentacle",PrimitiveType.Cylinder,new Vector3(Mathf.Cos(i)*.06f,-.12f,Mathf.Sin(i)*.06f),new Vector3(.012f,.1f,.012f),Color.cyan,false);
                    break;
                case ParkScenario.Plant:
                    Part(visual,"Pot",PrimitiveType.Cylinder,new Vector3(0,.05f,0),new Vector3(.15f,.05f,.15f),new Color(.55f,.28f,.13f),false);
                    branchRoot=new GameObject("Growing virtual plant").transform; branchRoot.SetParent(visual,false); branchRoot.localPosition=Vector3.up*.1f;
                    Part(branchRoot,"Stem",PrimitiveType.Cylinder,Vector3.up*.12f,new Vector3(.018f,.12f,.018f),Color.green);
                    for(int i=0;i<5;i++) { var leaf=Part(branchRoot,"Leaf",PrimitiveType.Sphere,new Vector3((i%2==0?1:-1)*.06f,.04f+i*.035f,0),new Vector3(.12f,.035f,.055f),targetColor); foliage.Add(leaf); }
                    plantFlower=new GameObject("Virtual plant blossom").transform;plantFlower.SetParent(branchRoot,false);plantFlower.localPosition=Vector3.up*.29f;
                    for(int i=0;i<5;i++) {
                        float a=i*Mathf.PI*2/5;
                        var petal=Part(plantFlower,"Blossom petal",PrimitiveType.Cube,new Vector3(Mathf.Sin(a)*.035f,Mathf.Cos(a)*.035f,0),new Vector3(.055f,.08f,.025f),new Color(1,.35f,.65f),false);
                        petal.GetComponent<MeshFilter>().sharedMesh=ParkFeedbackMeshes.Get(ParkFeedbackShape.Petal);petal.localRotation=Quaternion.Euler(0,0,-i*72);plantPetals.Add(petal);
                    }
                    Part(plantFlower,"Blossom centre",PrimitiveType.Sphere,new Vector3(0,0,-.015f),Vector3.one*.03f,Color.yellow,false);
                    break;
                case ParkScenario.Pet:
                    Part(visual,"Virtual comic pet body",PrimitiveType.Capsule,new Vector3(0,.12f,0),new Vector3(.25f,.12f,.19f),new Color(.9f,.65f,.35f));
                    Part(visual,"Head",PrimitiveType.Sphere,new Vector3(0,.24f,-.08f),Vector3.one*.18f,targetColor);
                    ears=new GameObject("Virtual ears only").transform; ears.SetParent(visual,false);
                    for(int i=-1;i<=1;i+=2) Part(ears,"Ear",PrimitiveType.Cube,new Vector3(i*.06f,.34f,-.08f),new Vector3(.04f,species=="Dog"?.12f:.07f,.04f),targetColor);
                    break;
            }
            bubble=Part(visual,"Dream bubble",PrimitiveType.Sphere,new Vector3(0,headOffset,0),Vector3.one*.2f,new Color(.7f,.85f,1),false);
            dreamRestScale=bubble.localScale;
            // An opaque shell hid the selectable dream content inside it.
            if(scenario==ParkScenario.Pet) {
                bubble.GetComponent<Renderer>().enabled=false;
                var outline=new GameObject("Dream bubble outline").AddComponent<LineRenderer>();
                outline.transform.SetParent(bubble,false);outline.useWorldSpace=false;outline.loop=true;
                outline.positionCount=48;outline.widthMultiplier=.003f;outline.sharedMaterial=sharedMaterial;
                var tint=new MaterialPropertyBlock();tint.SetColor("_BaseColor",new Color(.7f,.85f,1));outline.SetPropertyBlock(tint);
                for(int i=0;i<48;i++) {float angle=i*Mathf.PI*2/48;outline.SetPosition(i,new Vector3(Mathf.Cos(angle)*.5f,Mathf.Sin(angle)*.5f,0));}
            }
            var food=Part(bubble,species=="Dog"?"Virtual bone":"Virtual fish",PrimitiveType.Cube,Vector3.zero,new Vector3(.18f,.18f,.08f),species=="Dog"?Color.white:new Color(1,.75f,.2f),false);
            food.GetComponent<MeshFilter>().sharedMesh=ParkFeedbackMeshes.Get(species=="Dog"?ParkFeedbackShape.Bone:ParkFeedbackShape.Fish);dreamContents.Add(food);
            foreach(var shape in new[]{ParkFeedbackShape.Star,ParkFeedbackShape.Heart}) {
                var content=Part(bubble,"Dream "+shape,PrimitiveType.Cube,Vector3.zero,Vector3.one*.12f,shape==ParkFeedbackShape.Heart?new Color(1,.2f,.45f):Color.yellow,false);
                content.GetComponent<MeshFilter>().sharedMesh=ParkFeedbackMeshes.Get(shape);dreamContents.Add(content);
            }
            if(scenario==ParkScenario.Pet)foreach(var content in dreamContents)content.localScale*=3;
            reward=Part(visual,"Reward chest",PrimitiveType.Cube,new Vector3(.2f,.06f,0),new Vector3(.12f,.08f,.1f),new Color(1,.7f,.1f),false);
            environment=new GameObject("Confirmed-plane environment only").transform; environment.SetParent(transform,false);
            for(int i=0;i<12;i++) { var lotus=Part(environment,"Virtual lotus",PrimitiveType.Sphere,new Vector3(Mathf.Cos(i)*.5f,0,Mathf.Sin(i)*.5f),new Vector3(.1f,.025f,.1f),new Color(.95f,.55f,.7f),false); }
            preview=Part(transform,"Gesture preview",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.012f,Color.cyan,false);
            spell=Part(transform,"Spell transfer",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.018f,Color.yellow,false);
            for(int i=0;i<32;i++) { var p=Part(visual,"Pooled feedback "+i,PrimitiveType.Cube,Vector3.zero,Vector3.one*.018f,Color.yellow,false); p.gameObject.SetActive(false); pool.Add(p); }
            fragmentMesh=pool[0].GetComponent<MeshFilter>().sharedMesh;
            if(label==null) { label=new GameObject("Local instructions").AddComponent<TextMesh>(); label.transform.SetParent(transform,false); label.transform.localPosition=Vector3.up*.72f; label.characterSize=.014f; label.fontSize=38; label.anchor=TextAnchor.MiddleCenter; }
            if(scenario==ParkScenario.Bugs)foreach(var renderer in body)bugRestScales.Add(renderer.transform.localScale);
            ResetEffects(); currentColor=targetColor;
            bool first=true;
            foreach(var renderer in body) { var bounds=renderer.localBounds;for(int corner=0;corner<8;corner++) {
                var local=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                var point=representationFit.InverseTransformPoint(renderer.transform.TransformPoint(local));
                if(first) { neutralBounds=new Bounds(point,Vector3.zero);first=false; } else neutralBounds.Encapsulate(point);
            } }
            selectionHighlight=new GameObject("Selected virtual target highlight").transform;
            selectionHighlight.SetParent(representationFit,false);
            for(int i=0;i<4;i++) {
                var point=new Vector3((i%2==0?neutralBounds.min.x:neutralBounds.max.x),neutralBounds.min.y,(i<2?neutralBounds.min.z:neutralBounds.max.z));
                Part(selectionHighlight,"Selection corner",PrimitiveType.Cube,point,Vector3.one*.018f,Color.cyan,false);
            }
            selectionHighlight.gameObject.SetActive(false);
            entryProgress=Application.isPlaying?0:1;
            if(Application.isPlaying)visual.localScale=Vector3.zero;
        }
        public void SetHighlighted(bool value) {
            value=value && tracked;
            if(selectionHighlight)selectionHighlight.gameObject.SetActive(value);
            if(label)label.color=value?Color.cyan:Color.white;
        }

        Transform Part(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 size,Color color,bool tint=true)
        {
            var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent,false); go.transform.localPosition=position; go.transform.localScale=size;
            var collider=go.GetComponent<Collider>(); if(Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            var r=go.GetComponent<Renderer>(); r.sharedMaterial=sharedMaterial;
            var b=new MaterialPropertyBlock(); b.SetColor("_BaseColor",color); r.SetPropertyBlock(b); if(tint) body.Add(r);
            return go.transform;
        }

        public bool TryAction(ParkAction action,Vector3 hand,bool valid)
        {
            if(IsExiting) {Feedback="Scene exit in progress";return false;}
            if(!built) Build();
            if(action==ParkAction.Reset) { ResetEffects(); return true; }
            if(action==ParkAction.Bloom && scenario!=ParkScenario.Plant) {Feedback="Bloom is a virtual plant action";return false;}
            if(scenario==ParkScenario.Room && action==ParkAction.Awaken && RoomCandySelected)action=ParkAction.Reward;
            if(!FinitePoint(hand)) {Feedback="Blocked: invalid hand position";return false;}
            if(action==ParkAction.Stop) {
                if(!isActiveAndEnabled || !tracked || !valid || (scenario!=ParkScenario.Room && scenario!=ParkScenario.Pet)) {Feedback="Blocked: stop requires an active tracked fantasy target";return false;}
                if(stoppingEffects!=null)return true;
                var visibleParticles=new List<Transform>();var scales=new List<Vector3>();
                foreach(var p in pool)if(p.gameObject.activeSelf) {visibleParticles.Add(p);scales.Add(p.localScale);}
                Cancel();
                for(int i=0;i<visibleParticles.Count;i++) {visibleParticles[i].localScale=scales[i];visibleParticles[i].gameObject.SetActive(true);}
                motion=StartCoroutine(StopFantasy());return true;
            }
            float radius=virtualTarget?virtualInteractionRadius:interactionRadius;
            if(!isActiveAndEnabled || !tracked || !valid || !float.IsFinite(radius) || radius<=0 || Vector3.Distance(hand,transform.position)>radius) { Feedback="Blocked: target / tracking / distance"; return false; }
            if(scenario==ParkScenario.Pet && !virtualTarget && !reliableHeadPose && (action==ParkAction.Poke || action==ParkAction.Feed)) {
                Feedback="Blocked: pet head position is not verified; use virtual dream controls";return false;
            }
            if(Busy || Time.unscaledTime-acceptedAt<cooldown) { Feedback="Blocked: transition / cooldown"; return false; }
            if(action==ParkAction.Undo) { Undo(); return true; }
            if(scenario==ParkScenario.Bugs && action==ParkAction.Reward && state.rewarded) { Feedback="Already defeated; Reset starts a new virtual round";return false; }
            if((scenario==ParkScenario.Statue || scenario==ParkScenario.Room) && action==ParkAction.Reward && (!confirmedPlane || environmentAnchor==null)) { Feedback="Confirm a ground/ceiling plane first"; return false; }
            if(scenario==ParkScenario.Room && action==ParkAction.Reward && !RoomCandySelected) {Feedback="Select Candy rain before summoning it";return false;}
            if(scenario==ParkScenario.Pet && (action==ParkAction.Feed || action==ParkAction.Poke) && !state.bubble) { Feedback="Pull open dream bubble first"; return false; }
            if(scenario==ParkScenario.Tree && magicTree && action==ParkAction.Reward && !state.scar) { Feedback="Awaken and reveal scar before sealing"; return false; }
            state.growth=growthTime; state.season=seasonTime; history.Push(state); acceptedAt=Time.unscaledTime;
            motion=StartCoroutine(Perform(action,hand)); return true;
        }

        IEnumerator StopFantasy() {
            yield return FadeFantasyEffects();motion=null;
        }
        IEnumerator FadeFantasyEffects() {
            Feedback="Stopping virtual effects";
            var exiting=new List<Transform>{bubble,portal,jellyfish};
            foreach(var p in pool)if(p.gameObject.activeSelf)exiting.Add(p);
            stoppingEffects=exiting.ToArray();stoppingScales=new Vector3[stoppingEffects.Length];
            for(int i=0;i<stoppingEffects.Length;i++)if(stoppingEffects[i])stoppingScales[i]=stoppingEffects[i].localScale;
            float elapsed=0;
            while(elapsed<.3f) {
                elapsed+=Time.unscaledDeltaTime;
                for(int i=0;i<stoppingEffects.Length;i++)if(stoppingEffects[i])stoppingEffects[i].localScale=stoppingScales[i]*(1-Mathf.SmoothStep(0,1,elapsed/.3f));
                yield return null;
            }
            FinishFantasyExit();
        }
        void FinishFantasyExit() {
            state.awake=state.bubble=state.wind=state.rewarded=false;
            dreamVisibility=0;
            RestoreStoppedScales();particleCount=0;foreach(var p in pool)p.gameObject.SetActive(false);
            ApplyVisibility();Feedback="Virtual effects stopped";
        }
        void RestoreStoppedScales() {
            if(stoppingEffects==null)return;
            for(int i=0;i<stoppingEffects.Length;i++)if(stoppingEffects[i])stoppingEffects[i].localScale=stoppingScales[i];
            stoppingEffects=null;stoppingScales=null;
        }
        IEnumerator Perform(ParkAction action,Vector3 hand)
        {
            Feedback="Accepted: "+action; preview.gameObject.SetActive(false); spell.gameObject.SetActive(true);
            Vector3 from=transform.InverseTransformPoint(hand); float elapsed=0;
            audioSource.PlayOneShot(MagicMRAudioFactory.Whoosh,.6f);
            while(elapsed<.3f) { elapsed+=Time.unscaledDeltaTime; spell.localPosition=Vector3.Lerp(from,Vector3.up*.15f,Mathf.SmoothStep(0,1,elapsed/.3f)); yield return null; }
            spell.gameObject.SetActive(false); Revision++;
            switch(action)
            {
                case ParkAction.Style:
                    if(scenario==ParkScenario.Room && (state.awake || state.rewarded || state.bubble))
                        yield return FadeFantasyEffects();
                    if(scenario==ParkScenario.Tree) {targetSeason+=.25f;Feedback="Season: "+new[]{"Spring","Summer","Autumn","Winter"}[(int)(Mathf.Repeat(targetSeason,1)*4)%4];}
                    else {
                        int variants=scenario==ParkScenario.Room?2:scenario==ParkScenario.Statue?Mathf.Max(1,story.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).Length):3;
                        state.style=(state.style+1)%variants;
                        targetColor=state.style==1?new Color(.15f,.16f,.2f):state.style==2?new Color(.9f,.3f,.5f):new Color(.25f,.65f,.4f);
                    }
                    if(scenario==ParkScenario.Pet)Feedback="Dream content: "+new[]{"Virtual food","Star","Heart"}[state.style];
                    if(scenario==ParkScenario.Room)Feedback="Selected effect: "+(state.style==0?"Jellyfish":"Candy rain; confirm ceiling first");
                    if(scenario==ParkScenario.Statue) {
                        var chapters=story.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries);
                        Feedback=chapters.Length>0?"Fantasy chapter "+(state.style+1)+": "+chapters[state.style%chapters.Length]+"\nSource: "+storySource:"No story chapters configured";
                    }
                    break;
                case ParkAction.Awaken:
                    state.awake=true;state.scar=magicTree;Emit(ParkAction.Awaken,16);
                    if(scenario==ParkScenario.Statue) {
                        var firstChapters=story.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries);
                        state.style=0;
                        Feedback=firstChapters.Length>0?"Fantasy chapter 1: "+firstChapters[0]+"\nSource: "+storySource:"No story configured";
                    }
                    break;
                case ParkAction.Pull: state.bubble=scenario==ParkScenario.Pet?!state.bubble:true; break;
                case ParkAction.Bloom: state.bloom=!state.bloom;Feedback=state.bloom?"Virtual blossom opening":"Virtual blossom closing";break;
                case ParkAction.Poke: state.bubble=false; if(magicTree) { state.sealedTree=true; Feedback="Scar soothed; decorative branches rest"; } Emit(ParkAction.Poke,20); break;
                case ParkAction.Feed: state.affection++; Emit(ParkAction.Feed,12); break;
                case ParkAction.GrowUp: targetGrowth=Mathf.Clamp01(growthTime+.2f); targetSeason=seasonTime+.25f; break;
                case ParkAction.GrowDown: targetGrowth=Mathf.Clamp01(growthTime-.2f); targetSeason=seasonTime-.25f; break;
                case ParkAction.Reward: state.rewarded=true; state.sealedTree=magicTree; state.bubble=false; if(scenario==ParkScenario.Pet) { state.affection++; state.style=0; targetColor=new Color(.25f,.65f,.4f); } Emit(ParkAction.Reward,24); if(scenario==ParkScenario.Bugs)Feedback="+100 virtual EXP / coins; physical insect remains visible"; break;
                case ParkAction.Wind:
                    state.wind=true;
                    windDirection=(hand-transform.position).sqrMagnitude>.001f?(transform.position-hand).normalized:Vector3.right;
                    Emit(ParkAction.Wind,24); if(scenario==ParkScenario.Statue) { Feedback=story+"\nSource: "+storySource; if(storyAudio)audioSource.PlayOneShot(storyAudio); } break;
            }
            audioSource.PlayOneShot(action==ParkAction.Reward?MagicMRAudioFactory.Ding:MagicMRAudioFactory.ResetChime,.5f);
            elapsed=0; float duration=Mathf.Max(.3f,transitionSeconds);
            while(elapsed<duration)
            {
                elapsed+=Time.unscaledDeltaTime; float t=Mathf.Clamp01(elapsed/duration); float pulse=Mathf.Sin(t*Mathf.PI);
                if(action==ParkAction.Awaken && scenario==ParkScenario.Statue)SetScanProgress(t);
                stretch=action==ParkAction.Awaken?pulse:0;
                // Willow windup -> bounded side sweep -> recovery; never moves the anchor.
                bend=action==ParkAction.Wind && magicTree?Mathf.Sin(t*Mathf.PI*2)*18:0;
                spin=action==ParkAction.Wind && scenario==ParkScenario.Bugs?Mathf.SmoothStep(0,1,t)*720:0;
                dance=action==ParkAction.Awaken && scenario==ParkScenario.Bugs?Vector3.right*(Mathf.Floor(t*6)%2==0?.04f:-.04f):Vector3.zero;
                yield return null;
            }
            if(statueScan)statueScan.gameObject.SetActive(false);
            bend=spin=stretch=0; dance=Vector3.zero; motion=null;
        }
        void SetScanProgress(float progress) {
            if(!statueScan)return;
            bool visible=tracked && float.IsFinite(progress) && progress>=0 && progress<1;
            statueScan.gameObject.SetActive(visible);
            if(visible)statueScan.localPosition=Vector3.up*Mathf.Lerp(.04f,.6f,progress);
        }

        void Emit(ParkAction kind,int count) {
            particleKind=kind; particleCount=Mathf.Min(count,pool.Count); particleStart=Time.unscaledTime;
            int windSeason=(int)(Mathf.Repeat(seasonTime,1)*4)%4;
            if(scenario==ParkScenario.Tree && kind==ParkAction.Wind && windSeason==3)particleCount=0;
            for(int i=0;i<particleCount;i++) {
                var shape=ParkFeedbackShape.Star; var color=Color.yellow;
                if(scenario==ParkScenario.Bugs && kind==ParkAction.Reward)shape=ParkFeedbackShape.Coin;
                if(scenario==ParkScenario.Pet && (kind==ParkAction.Feed || kind==ParkAction.Reward)) { shape=ParkFeedbackShape.Heart;color=new Color(1,.2f,.45f); }
                if(scenario==ParkScenario.Room && kind==ParkAction.Reward) { shape=ParkFeedbackShape.Candy;color=i%2==0?new Color(1,.3f,.65f):Color.cyan; }
                if(scenario==ParkScenario.Tree && kind==ParkAction.Wind) { shape=windSeason==0?ParkFeedbackShape.Petal:ParkFeedbackShape.Leaf;color=SeasonColors[windSeason]; }
                bool fragment=scenario==ParkScenario.Bugs && kind==ParkAction.Reward && i<8;
                pool[i].GetComponent<MeshFilter>().sharedMesh=fragment?fragmentMesh:ParkFeedbackMeshes.Get(shape);
                if(fragment)color=new Color(.5f,.16f,.1f);
                var r=pool[i].GetComponent<Renderer>();r.GetPropertyBlock(block);block.SetColor("_BaseColor",color);r.SetPropertyBlock(block);
            }
        }
        public void Preview(Vector3 world,bool valid) { if(!built)return; preview.gameObject.SetActive(valid && tracked && !Busy); if(valid)preview.position=world; }
        public void EndHandInteraction() {
            if(!built)return;
            // Interrupt transient animation and continuous emissions even after the spell coroutine ends.
            // Keep discrete scene choices (including the pet's dream content) for recovery.
            Cancel();state.wind=false;
            if(scenario==ParkScenario.Room)FinishFantasyExit();
            else ApplyVisibility();
            Preview(Vector3.zero,false);
        }
        public void SetTracked(bool value) {
            if(tracked==value)return;tracked=value;
            if(!value) {
                // Brief pet occlusion ends motion but keeps the chosen dream and affection.
                // Full Reset/scene exit still clears everything through ResetEffects.
                if(scenario==ParkScenario.Pet)Cancel();else ResetEffects();
                Feedback="TARGET_LOST - interaction paused";
            } else {if(!hasPresented)entryProgress=0;Feedback="Target recovered; waiting for a new gesture";}
            if(visual)visual.gameObject.SetActive(value);
            if(!value)SetHighlighted(false);
        }
        public void Undo() { Cancel(); if(history.Count>0) { state=history.Pop(); targetGrowth=growthTime=state.growth; targetSeason=seasonTime=state.season; targetColor=state.style==1?new Color(.15f,.16f,.2f):state.style==2?new Color(.9f,.3f,.5f):new Color(.25f,.65f,.4f); Revision++;ApplyGrowthPose();ApplyVisibility(); } Feedback="Undo latest effect"; }
        void ApplyGrowthPose() {
            if(scenario==ParkScenario.Plant && branchRoot)branchRoot.localScale=new Vector3(1,Mathf.Lerp(.15f,1.4f,growthTime),1);
        }
        public void ResetEffects()
        {
            entryProgress=1;
            orientationConfirmed=false;sizeConfirmed=false;
            if(representationFit) { representationFit.localPosition=Vector3.zero;representationFit.localScale=Vector3.one; }
            Cancel(); history.Clear(); state=default; targetGrowth=growthTime=.45f; targetSeason=seasonTime=0; targetColor=new Color(.25f,.65f,.4f); Revision++; Feedback="Reset";
            acceptedAt=-100;currentColor=targetColor;styleMix=0;
            foreach(var r in body)if(r) {r.GetPropertyBlock(block);block.SetColor("_BaseColor",currentColor);block.SetFloat("_Style",0);r.SetPropertyBlock(block);}
            dreamVisibility=0;if(bubble && scenario==ParkScenario.Pet)bubble.localScale=dreamRestScale;
            bloomVisibility=0;if(plantFlower) {plantFlower.localScale=Vector3.zero;plantFlower.gameObject.SetActive(false);}
            if(scenario==ParkScenario.Plant && branchRoot) {branchRoot.localRotation=Quaternion.identity;StepPlantAnimation(0,0);branchRoot.localRotation=Quaternion.identity;}
            bugDefeatProgress=0;ApplyBugDefeatPose();
            windBlend=0;
            if(scenario==ParkScenario.Tree)for(int i=0;i<branches.Count;i++)branches[i].localRotation=Quaternion.Euler(0,i*72,40);
            confirmedPlane=false; environmentAnchor=null; reliableHeadPose=false; headAnchor=null;
            if(visual) { visual.localPosition=Vector3.zero; visual.localRotation=Quaternion.identity; visual.localScale=scenario==ParkScenario.Pet?Vector3.one*Mathf.Lerp(.6f,1.15f,.45f):Vector3.one; }
            if(scenario==ParkScenario.Plant && branchRoot)branchRoot.localScale=new Vector3(1,Mathf.Lerp(.15f,1.4f,.45f),1);
            if(built) ApplyVisibility();
        }
        void Cancel() { if(statueScan)statueScan.gameObject.SetActive(false);StopAllCoroutines(); RestoreStoppedScales();motion=null; if(audioSource)audioSource.Stop(); particleCount=0; foreach(var p in pool)if(p)p.gameObject.SetActive(false); if(preview)preview.gameObject.SetActive(false); if(spell)spell.gameObject.SetActive(false); bend=spin=stretch=0; dance=Vector3.zero; }
        public void ExitAndDestroy() {
            if(IsExiting)return;
            if(!Application.isPlaying || !isActiveAndEnabled || !built || !visual) {Destroy(gameObject);return;}
            Vector3 fromScale=visual.localScale;
            ResetEffects();IsExiting=true;tracked=false;SetHighlighted(false);
            if(label)label.gameObject.SetActive(false);
            foreach(var collider in GetComponentsInChildren<Collider>())collider.enabled=false;
            StartCoroutine(ExitPresentation(fromScale));
        }
        IEnumerator ExitPresentation(Vector3 fromScale) {
            float elapsed=0;
            while(elapsed<.22f) {
                elapsed+=Time.unscaledDeltaTime;
                visual.localScale=fromScale*(1-Mathf.SmoothStep(0,1,elapsed/.22f));
                yield return null;
            }
            Destroy(gameObject);
        }
        void OnDisable() { ResetEffects(); }

        void ApplyVisibility()
        {
            if(scenario==ParkScenario.Bugs)foreach(var r in body)if(r)r.enabled=!state.rewarded || bugDefeatProgress<1;
            eyes.gameObject.SetActive(state.awake && !(scenario==ParkScenario.Bugs && state.rewarded));
            bubble.gameObject.SetActive(scenario==ParkScenario.Pet && (state.bubble || dreamVisibility>0));
            for(int i=0;i<dreamContents.Count;i++)dreamContents[i].gameObject.SetActive(i==state.style);
            if(scar)scar.gameObject.SetActive(state.scar && !state.sealedTree);
            reward.gameObject.SetActive(state.rewarded && scenario==ParkScenario.Tree);
            environment.gameObject.SetActive(state.rewarded && scenario==ParkScenario.Statue && confirmedPlane && environmentAnchor);
            if(jellyfish)jellyfish.gameObject.SetActive(state.awake);
            if(portal)portal.gameObject.SetActive(state.bubble);
        }
        void StepDreamTransition(float dt) {
            if(!bubble || scenario!=ParkScenario.Pet || !float.IsFinite(dt) || dt<0)return;
            dreamVisibility=Mathf.MoveTowards(dreamVisibility,state.bubble?1:0,dt/.3f);
            bubble.localScale=dreamRestScale*Mathf.SmoothStep(0,1,dreamVisibility);
            bubble.gameObject.SetActive(state.bubble || dreamVisibility>0);
        }
        void StepTreeWind(float dt,float now) {
            bool valid=float.IsFinite(dt) && dt>=0 && float.IsFinite(now) && float.IsFinite(windStrength) && FinitePoint(windDirection);
            if(valid)windBlend=Mathf.MoveTowards(windBlend,state.wind && !state.sealedTree?1:0,dt/.25f);
            else windBlend=0;
            float sway=valid?Mathf.Clamp01(windStrength)*10*windBlend:0;
            for(int i=0;i<branches.Count;i++) {
                float wave=valid?Mathf.Sin(now+i)*sway:0;
                branches[i].localRotation=Quaternion.Euler(valid?windDirection.z*wave:0,i*72,40+bend+(valid?windDirection.x*wave:0));
            }
        }
        void ApplyBugDefeatPose() {
            if(scenario!=ParkScenario.Bugs)return;
            float t=Mathf.Clamp01(bugDefeatProgress);
            // Impact squash, then dissolve by shrinking virtual geometry; pooled rewards stay full size.
            float squash=Mathf.Sin(Mathf.Clamp01(t/.35f)*Mathf.PI);
            float shrink=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,1,t));
            for(int i=0;i<body.Count && i<bugRestScales.Count;i++)if(body[i])
                body[i].transform.localScale=Vector3.Scale(bugRestScales[i],new Vector3(1+squash*.2f,1-squash*.35f,1+squash*.2f))*shrink;
        }
        // Presentation only: keep the pot, fitted representation and tracking anchor fixed.
        void StepPlantAnimation(float dt,float now) {
            if(!branchRoot || !float.IsFinite(dt) || dt<0 || !float.IsFinite(now))return;
            ApplyGrowthPose();
            float growing=Mathf.Clamp01(Mathf.Abs(targetGrowth-growthTime)/.12f);
            float sway=Mathf.Sin(now*1.6f)*(.65f+growing*1.8f);
            branchRoot.localRotation=Quaternion.Euler(0,0,sway);
            for(int i=0;i<foliage.Count;i++) {
                float unfold=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f+i*.055f,.45f+i*.055f,growthTime));
                foliage[i].localScale=new Vector3(.12f,.035f,.055f)*Mathf.Lerp(.25f,1,unfold);
                foliage[i].localRotation=Quaternion.Euler(0,0,(i%2==0?1:-1)*(Mathf.Lerp(55,8,unfold)+Mathf.Sin(now*2+i)*2));
            }
            if(!plantFlower)return;
            bloomVisibility=Mathf.MoveTowards(bloomVisibility,state.bloom?1:0,dt/.65f);
            float open=Mathf.SmoothStep(0,1,bloomVisibility);
            plantFlower.localScale=Vector3.one*open;
            plantFlower.gameObject.SetActive(state.bloom || bloomVisibility>0);
            for(int i=0;i<plantPetals.Count;i++) {
                float petalOpen=Mathf.SmoothStep(0,1,Mathf.InverseLerp(i*.045f,.8f+i*.045f,bloomVisibility));
                float a=i*Mathf.PI*2/5;
                plantPetals[i].localPosition=new Vector3(Mathf.Sin(a),Mathf.Cos(a),0)*Mathf.Lerp(.008f,.035f,petalOpen);
                plantPetals[i].localRotation=Quaternion.Euler(Mathf.Lerp(70,0,petalOpen),0,-i*72);
                plantPetals[i].localScale=new Vector3(.055f,.08f,.025f)*Mathf.Lerp(.35f,1,petalOpen);
            }
        }
        void Update()
        {
            if(!built || !tracked || IsExiting || stoppingEffects!=null)return;
            float dt=Time.unscaledDeltaTime;
            if(scenario==ParkScenario.Bugs) {bugDefeatProgress=Mathf.MoveTowards(bugDefeatProgress,state.rewarded?1:0,dt/.3f);ApplyBugDefeatPose();}
            growthTime=Mathf.MoveTowards(growthTime,targetGrowth,dt*.35f); seasonTime=Mathf.MoveTowards(seasonTime,targetSeason,dt*.2f);
            styleMix=Mathf.MoveTowards(styleMix,state.style/2f,dt);
            currentColor=Color.Lerp(currentColor,targetColor,1-Mathf.Exp(-dt*4));
            foreach(var r in body) { if(!r)continue; r.GetPropertyBlock(block); block.SetColor("_BaseColor",currentColor); block.SetFloat("_Style",styleMix*2); r.SetPropertyBlock(block); }
            float breathing=state.awake && !state.sealedTree?Mathf.Sin(Time.unscaledTime*2)*.018f:0;
            visual.localPosition=dance; visual.localRotation=Quaternion.Euler(0,spin,breathing*100);
            visual.localScale=Vector3.one*(1+breathing)+Vector3.up*stretch*.15f;
            if(scenario==ParkScenario.Pet)visual.localScale*=Mathf.Lerp(.6f,1.15f,growthTime);
            entryProgress=Mathf.MoveTowards(entryProgress,1,dt/.28f);
            visual.localScale*=Mathf.SmoothStep(0,1,entryProgress);
            if(entryProgress>=1)hasPresented=true;
            if(scenario==ParkScenario.Plant)StepPlantAnimation(dt,Time.unscaledTime);
            float season=Mathf.Repeat(seasonTime,1)*4;
            var colors=SeasonColors;
            if(scenario==ParkScenario.Tree) {
                float canopy=season<2?1:Mathf.Lerp(1,.05f,Mathf.SmoothStep(0,1,(season-2)/1.1f)); if(season>3.5f)canopy=Mathf.Lerp(.05f,1,(season-3.5f)*2);
                for(int i=0;i<foliage.Count;i++) { foliage[i].localScale=new Vector3(.3f,.25f,.28f)*canopy; var r=foliage[i].GetComponent<Renderer>(); r.GetPropertyBlock(block); block.SetColor("_BaseColor",Color.Lerp(colors[(int)season%4],colors[((int)season+1)%4],Mathf.SmoothStep(0,1,season%1))); r.SetPropertyBlock(block); }
                StepTreeWind(dt,Time.unscaledTime);
            }
            if(scenario==ParkScenario.Statue)for(int i=0;i<branches.Count;i++)branches[i].localRotation=Quaternion.Euler(0,0,(i==0?-1:1)*stretch*45);
            if(ears)ears.localRotation=Quaternion.Euler(0,0,state.awake?Mathf.Sin(Time.unscaledTime*3)*5:0);
            if(jellyfish)jellyfish.localPosition=Vector3.up*(.6f+Mathf.Sin(Time.unscaledTime)*.04f);
            if(portal)portal.localScale=new Vector3(Mathf.Lerp(portal.localScale.x,state.bubble?.6f:.01f,dt*4),.45f,.02f);
            if(bubble && scenario==ParkScenario.Pet) {
                StepDreamTransition(dt);
                if(reliableHeadPose && headAnchor)bubble.position=headAnchor.position+Vector3.up*.12f;
                else bubble.localPosition=Vector3.up*headOffset;
                // Face only the flat dream content toward the viewer; never rotate the tracking anchor.
                var viewer=Camera.main;
                if(viewer) {
                    Vector3 facing=bubble.position-viewer.transform.position;
                    if(FinitePoint(facing) && facing.sqrMagnitude>.0001f)
                        bubble.rotation=Quaternion.LookRotation(facing,viewer.transform.up);
                }
            }
            if(environmentAnchor && confirmedPlane) { environment.position=environmentAnchor.position; environment.rotation=environmentAnchor.rotation; environment.localScale=Vector3.one*Mathf.Clamp(environmentRadius,.1f,2); }
            float age=Time.unscaledTime-particleStart;
            for(int i=0;i<pool.Count;i++) {
                // Reuse the existing bounded pool for sustained candy rain. No per-frame spawning.
                bool candyRain=scenario==ParkScenario.Room && state.rewarded && particleKind==ParkAction.Reward && confirmedPlane && environmentAnchor;
                float particleAge=candyRain?Mathf.Repeat(age+i*.065f,1.6f):age;
                bool on=i<particleCount && particleAge<1.6f; var p=pool[i]; p.gameObject.SetActive(on); if(!on)continue;
                float angle=i*2.39996f; Vector3 direction=new Vector3(Mathf.Cos(angle),.5f,Mathf.Sin(angle));
                p.localPosition=particleKind==ParkAction.Wind?Vector3.up*.4f+windDirection*age*.25f+direction*.05f:new Vector3(0,.2f,0)+direction*age*.15f+Vector3.down*age*age*.07f;
                if(scenario==ParkScenario.Room && particleKind==ParkAction.Reward && confirmedPlane && environmentAnchor)p.position=environmentAnchor.position+direction*.25f+Vector3.down*particleAge*.18f;
                p.localRotation=Quaternion.Euler(particleAge*80,i*30,particleAge*100); p.localScale=Vector3.one*.045f*Mathf.Clamp01((1.6f-particleAge)/.3f);
            }
            ApplyVisibility();
            if(label) { label.text=scenario+" / "+(virtualTarget?"VIRTUAL TEST":"STEREO TRANSLATION")+"\n"+Feedback+"\n"+(scenario==ParkScenario.Plant?"Moisture: DEMO 55% | no sensor":scenario==ParkScenario.Pet?"Affection (virtual): "+state.affection+" | head "+(reliableHeadPose?"tracked":"approximate"):"Local effects")+"\nOrientation: "+(orientationConfirmed?"manually confirmed":"UNCONFIRMED")+" | "+(sizeConfirmed?"MANUAL nominal height/bottom":"virtual size; bottom uncalibrated"); if(Camera.main)label.transform.rotation=Quaternion.LookRotation(label.transform.position-Camera.main.transform.position); }
        }
    }
}
