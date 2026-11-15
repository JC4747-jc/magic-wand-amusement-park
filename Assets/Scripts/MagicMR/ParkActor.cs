using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MagicMR
{
    public enum ParkScenario { Tree, Bugs, Statue, Room, Plant, Pet }
    public enum ParkAction { Style, Awaken, Wind, Reward, Pull, Poke, Feed, GrowUp, GrowDown, Undo, Reset, Stop, Bloom }

    /// <summary>Virtual representation only. Root is a tracking/confirmed-placement anchor.</summary>
    public sealed partial class ParkActor : MonoBehaviour
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
            if(height<.005f || height>3 || new Vector2(top.x-bottom.x,top.z-bottom.z).magnitude>.1f ||
                !FinitePoint(neutralBounds.center) || !FinitePoint(neutralBounds.size) || neutralBounds.size.y<.001f)return false;
            float scale=height/neutralBounds.size.y;
            representationFit.localScale=Vector3.one*scale;
            representationFit.localPosition=transform.InverseTransformPoint(bottom)-new Vector3(neutralBounds.center.x,neutralBounds.min.y,neutralBounds.center.z)*scale;
            sizeConfirmed=true;Feedback="Manual nominal height / bottom confirmed; no 3D pose inference";return true;
        }
        static bool FinitePoint(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
        internal bool SetVirtualTreeMode(bool fantasy)
        {
            if(!built||scenario!=ParkScenario.Tree||!virtualTarget||!tracked||!isActiveAndEnabled||IsExiting)return false;
            var fitPosition=representationFit.localPosition;var fitScale=representationFit.localScale;bool fitted=sizeConfirmed,oriented=orientationConfirmed;
            magicTree=fantasy;ResetEffects();representationFit.localPosition=fitPosition;representationFit.localScale=fitScale;sizeConfirmed=fitted;orientationConfirmed=oriented;
            Feedback=fantasy?"VIRTUAL fantasy tree; effects reset":"VIRTUAL natural tree; effects reset";return true;
        }
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

        [Serializable] struct State { public int style, affection; public bool awake, rewarded, bubble, sealedTree, scar, wind, bloom, petComic; public float growth, season; }
        State state;
        readonly Stack<State> history = new Stack<State>();
        readonly List<Renderer> body = new List<Renderer>();
        readonly Dictionary<Renderer,Color> componentColors = new Dictionary<Renderer,Color>();
        readonly List<Transform> branches = new List<Transform>();
        readonly List<Transform> pool = new List<Transform>();
        Transform eyes, bubble, portal, jellyfish, scar, reward, environment, branchRoot, preview, spell, ears;
        readonly List<Transform> dreamContents=new List<Transform>();
        Transform[] stoppingEffects;
        Transform statueScan;
        NaturalBronzeBeast naturalBronze;
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
        static readonly Color[] SeasonColors={new Color(.38f,.52f,.24f),new Color(.19f,.36f,.13f),new Color(.62f,.39f,.15f),new Color(.37f,.39f,.32f)};

        public void Build()
        {
            if (built) return; built = true;
            block = new MaterialPropertyBlock();
            if (sharedMaterial == null) throw new InvalidOperationException("ParkActor requires a shared URP material");
            if (visual == null) { visual = new GameObject("Visual - animation only").transform; visual.SetParent(transform,false); }
            representationFit=new GameObject("Manual fit - separate from tracking and animation").transform;representationFit.SetParent(transform,false);visual.SetParent(representationFit,false);
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.spatialBlend=1; audioSource.volume=.18f; audioSource.playOnAwake=false;
            eyes = new GameObject("Eyes").transform; eyes.SetParent(visual,false);
            switch(scenario)
            {
                case ParkScenario.Tree:
                    BuildNaturalTree();
                    BuildNaturalTreeMark();
                    BuildSeasonTreeFace();
                    break;
                case ParkScenario.Bugs:
                    BuildNaturalBug();
                    break;
                case ParkScenario.Statue:
                    var scan=new GameObject("Virtual statue scan ring").AddComponent<LineRenderer>();
                    statueScan=scan.transform;statueScan.SetParent(visual,false);
                    scan.useWorldSpace=false;scan.loop=true;scan.positionCount=48;scan.widthMultiplier=.006f;scan.sharedMaterial=sharedMaterial;
                    var scanColor=new MaterialPropertyBlock();scanColor.SetColor("_BaseColor",Color.cyan);scan.SetPropertyBlock(scanColor);
                    for(int i=0;i<48;i++) {float a=i*Mathf.PI*2/48;scan.SetPosition(i,new Vector3(Mathf.Cos(a)*.25f,0,Mathf.Sin(a)*.25f));}
                    statueScan.gameObject.SetActive(false);
                    BuildNaturalStatue();
                    break;
                case ParkScenario.Room:
                    BuildNaturalRoom();
                    portal=Part(visual,"Virtual window",PrimitiveType.Cube,new Vector3(0,.3f,.2f),new Vector3(.01f,.45f,.02f),new Color(.025f,.07f,.22f),false);
                    BuildNaturalJellyfish();
                    break;
                case ParkScenario.Plant:
                    BuildNaturalPlant();
                    BuildNaturalBlossom();
                    break;
                case ParkScenario.Pet:
                    BuildNaturalPet();
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
            reward=scenario==ParkScenario.Tree?BuildTreeChest():Part(visual,"Reward chest",PrimitiveType.Cube,new Vector3(.2f,.06f,0),new Vector3(.12f,.08f,.1f),new Color(1,.7f,.1f),false);
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
            foreach(var renderer in body) { var bounds=NeutralGeometryBounds(renderer);for(int corner=0;corner<8;corner++) {
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
            if(scenario==ParkScenario.Plant)BuildPlantDialogue(neutralBounds);
            if(statueScan) {
                float radius=Mathf.Max(neutralBounds.extents.x,neutralBounds.extents.z)+.015f;
                var line=statueScan.GetComponent<LineRenderer>();line.widthMultiplier=.003f;
                for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;line.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
            }
            entryProgress=Application.isPlaying?0:1;
            if(Application.isPlaying)visual.localScale=Vector3.zero;
        }
        static Bounds NeutralGeometryBounds(Renderer renderer)
        {
            // Nominal fitting uses the bind-pose mesh, not the skin's expanded culling box.
            // Bounds remain readable in a player without retaining a CPU copy of all vertices or baking a temporary mesh.
            return renderer is SkinnedMeshRenderer skin&&skin.sharedMesh?skin.sharedMesh.bounds:renderer.localBounds;
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
            var b=new MaterialPropertyBlock(); b.SetColor("_BaseColor",color); r.SetPropertyBlock(b); if(tint) {body.Add(r);componentColors[r]=color;}
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
                if(!isActiveAndEnabled || !tracked || !valid || (scenario!=ParkScenario.Room && scenario!=ParkScenario.Pet && scenario!=ParkScenario.Statue)) {Feedback="Blocked: stop requires an active tracked fantasy target";return false;}
                if(stoppingEffects!=null)return true;
                var visibleParticles=new List<Transform>();var scales=new List<Vector3>();
                foreach(var p in pool)if(p.gameObject.activeSelf) {visibleParticles.Add(p);scales.Add(p.localScale);}
                Cancel();
                for(int i=0;i<visibleParticles.Count;i++) {visibleParticles[i].localScale=scales[i];visibleParticles[i].gameObject.SetActive(true);}
                motion=StartCoroutine(StopFantasy());return true;
            }
            float radius=virtualTarget?virtualInteractionRadius:interactionRadius;
            if(!isActiveAndEnabled || !tracked || !valid || !float.IsFinite(radius) || radius<=0 || Vector3.Distance(hand,transform.position)>radius) { Feedback="Blocked: target / tracking / distance"; return false; }
            if(scenario==ParkScenario.Pet && !virtualTarget && (!reliableHeadPose || !headAnchor || !FinitePoint(headAnchor.position)) && (action==ParkAction.Poke || action==ParkAction.Feed)) {
                Feedback="Blocked: pet head position is not verified; use virtual dream controls";return false;
            }
            if(Busy || Time.unscaledTime-acceptedAt<cooldown) { Feedback="Blocked: transition / cooldown"; return false; }
            if(action==ParkAction.Undo) { Undo(); return true; }
            if(scenario==ParkScenario.Bugs && action==ParkAction.Reward && state.rewarded) { Feedback="Already defeated; Reset starts a new virtual round";return false; }
            if((scenario==ParkScenario.Statue || scenario==ParkScenario.Room) && action==ParkAction.Reward && (!confirmedPlane || environmentAnchor==null)) { Feedback="Confirm a ground/ceiling plane first"; return false; }
            if(scenario==ParkScenario.Room && action==ParkAction.Reward && !RoomCandySelected) {Feedback="Select Candy rain before summoning it";return false;}
            if(scenario==ParkScenario.Pet && (action==ParkAction.Feed || action==ParkAction.Poke) && !state.bubble) { Feedback="Pull open dream bubble first"; return false; }
            if(scenario==ParkScenario.Pet && action==ParkAction.Wind && !state.bubble) { Feedback="Open a dream bubble before swiping to pop it"; return false; }
            if(scenario==ParkScenario.Tree && magicTree && action==ParkAction.Reward && !state.scar) { Feedback="Swipe C to reveal the breathing scar before sealing"; return false; }
            if(scenario==ParkScenario.Pet&&naturalPetRig)naturalPetRig.SetWalking(false);
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
            float jellyOpacity=naturalJellyfish?naturalJellyfish.Visibility:0;
            for(int i=0;i<stoppingEffects.Length;i++)if(stoppingEffects[i])stoppingScales[i]=stoppingEffects[i].localScale;
            float elapsed=0;
            while(elapsed<.3f) {
                elapsed+=Time.unscaledDeltaTime;
                for(int i=0;i<stoppingEffects.Length;i++)if(stoppingEffects[i])stoppingEffects[i].localScale=stoppingScales[i]*(1-Mathf.SmoothStep(0,1,elapsed/.3f));
                if(naturalJellyfish)naturalJellyfish.SetVisibility(jellyOpacity*(1-Mathf.SmoothStep(0,1,elapsed/.3f)));
                yield return null;
            }
            FinishFantasyExit();
        }
        void FinishFantasyExit() {
            state.awake=state.bubble=state.wind=state.rewarded=false;
            if(naturalBronze)naturalBronze.ResetPose();
            if(naturalJellyfish)naturalJellyfish.ResetPose();
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
                    if(scenario==ParkScenario.Plant) {SetHighlighted(true);Feedback="Plant selected";break;}
                    if(scenario==ParkScenario.Pet) {state.petComic=!state.petComic;Feedback=state.petComic?"Pet: manga outline":"Pet: natural appearance";break;}
                    if(scenario==ParkScenario.Room && (state.awake || state.rewarded || state.bubble))
                        yield return FadeFantasyEffects();
                    if(scenario==ParkScenario.Tree && !magicTree) {state.style=1-state.style;Feedback=state.style==1?"Season tree: anime canopy":"Season tree: natural canopy";}
                    else if(scenario==ParkScenario.Tree) {state.style=1-state.style;Feedback=state.style==1?"Willow: dark woodland":"Willow: natural woodland";}
                    else {
                        int variants=scenario==ParkScenario.Room?2:scenario==ParkScenario.Statue?Mathf.Max(1,story.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).Length):3;
                        state.style=(state.style+1)%variants;
                        targetColor=state.style==1?new Color(.15f,.16f,.2f):state.style==2?new Color(.9f,.3f,.5f):new Color(.25f,.65f,.4f);
                    }
                    if(scenario==ParkScenario.Room)Feedback="Selected effect: "+(state.style==0?"Jellyfish":"Candy rain; confirm ceiling first");
                    if(scenario==ParkScenario.Statue) {
                        var chapters=story.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries);
                        Feedback=chapters.Length>0?"Fantasy chapter "+(state.style+1)+": "+chapters[state.style%chapters.Length]+"\nSource: "+storySource:"No story chapters configured";
                    }
                    break;
                case ParkAction.Awaken:
                    state.awake=true;Emit(ParkAction.Awaken,16);
                    if(scenario==ParkScenario.Tree&&magicTree){state.wind=true;state.sealedTree=false;state.rewarded=false;treeGustUntil=Time.unscaledTime+2.4f;Feedback="Willow: branch lash and recovery";}
                    // HCI MangaPet B (circle): open the dream, including on repeated casts.
                    if(scenario==ParkScenario.Pet) { state.bubble=true; Feedback="Dream opened; swipe to pop"; }
                    if(scenario==ParkScenario.Plant)Feedback="Virtual plant: A little light helps me grow.";
                    if(scenario==ParkScenario.Tree&&!magicTree)Feedback="Season tree awakened";
                    if(scenario==ParkScenario.Statue) {
                        var firstChapters=story.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries);
                        state.style=0;
                        Feedback=firstChapters.Length>0?"Fantasy chapter 1: "+firstChapters[0]+"\nSource: "+storySource:"No story configured";
                    }
                    break;
                case ParkAction.Pull: state.bubble=scenario==ParkScenario.Pet?!state.bubble:true; break;
                case ParkAction.Bloom: state.bloom=!state.bloom;Feedback=state.bloom?"Virtual blossom opening":"Virtual blossom closing";break;
                case ParkAction.Poke: state.bubble=false; if(magicTree) { state.sealedTree=true; Feedback="Scar soothed; decorative branches rest"; } Emit(ParkAction.Poke,20); break;
                case ParkAction.Feed: state.affection++;if(naturalPetRig)naturalPetRig.Sniff();Emit(ParkAction.Feed,12);break;
                case ParkAction.GrowUp: targetGrowth=Mathf.Clamp01(growthTime+.2f); targetSeason=seasonTime+.25f; break;
                case ParkAction.GrowDown: targetGrowth=Mathf.Clamp01(growthTime-.2f); targetSeason=seasonTime-.25f; break;
                case ParkAction.Reward:
                    if(scenario==ParkScenario.Tree&&!magicTree) {
                        state.wind=true;treeGustUntil=Time.unscaledTime+2.4f;
                        windDirection=(hand-transform.position).sqrMagnitude>.001f?(transform.position-hand).normalized:Vector3.right;
                        Emit(ParkAction.Wind,24);Feedback="Seasonal gust: falling foliage";break;
                    }
                    if(scenario==ParkScenario.Plant) {
                        // Clear the magic, not the confirmed real-world placement or undo snapshot.
                        state=default;targetGrowth=.45f;targetSeason=0;targetColor=new Color(.25f,.65f,.4f);
                        // Selection is controller navigation state, not a magic effect.
                        particleCount=0;foreach(var p in pool)p.gameObject.SetActive(false);
                        Feedback="Plant effects cleared; placement retained";break;
                    }
                    state.rewarded=true; state.sealedTree=magicTree; state.bubble=false; if(scenario==ParkScenario.Tree&&magicTree){state.wind=false;Feedback="Willow sealed; treasure revealed";} if(scenario==ParkScenario.Pet) { state.affection++; state.style=0; targetColor=new Color(.25f,.65f,.4f); } Emit(ParkAction.Reward,24); if(scenario==ParkScenario.Bugs)Feedback="+100 virtual EXP / coins; physical insect remains visible"; break;
                case ParkAction.Wind:
                    if(scenario==ParkScenario.Tree&&magicTree){state.scar=true;state.sealedTree=false;state.rewarded=false;state.wind=false;Feedback="Willow: breathing bark mark; fist-open to seal";break;}
                    if(scenario==ParkScenario.Tree&&!magicTree) {
                        targetSeason+=.25f;Feedback="Season: "+new[]{"Spring","Summer","Autumn","Winter"}[(int)(Mathf.Repeat(targetSeason,1)*4)%4];break;
                    }
                    if(scenario==ParkScenario.Plant) {targetGrowth=targetGrowth>.5f?0:1;Feedback=targetGrowth>.5f?"Plant growing":"Plant returning to seedling";break;}
                    // HCI MangaPet C is a remote spell on the bubble, not a physical head poke.
                    // Keep Poke/Feed's verified-head gates for direct-touch interactions.
                    if(scenario==ParkScenario.Pet) { state.bubble=false; Emit(ParkAction.Poke,20); Feedback="Dream popped"; break; }
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
                bend=0; // Willow B owns the bounded branch-only lash in StepTreeWind.
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
            if(visible)statueScan.localPosition=new Vector3(neutralBounds.center.x,Mathf.Lerp(neutralBounds.min.y,neutralBounds.max.y,progress),neutralBounds.center.z);
        }

        void Emit(ParkAction kind,int count) {
            particleKind=kind; particleCount=Mathf.Min(count,pool.Count); particleStart=Time.unscaledTime;
            if(scenario==ParkScenario.Room && kind==ParkAction.Awaken)particleCount=Mathf.Min(particleCount,6);
            if(scenario==ParkScenario.Tree && kind!=ParkAction.Wind)particleCount=Mathf.Min(particleCount,6);
            if(scenario==ParkScenario.Statue && kind==ParkAction.Awaken)particleCount=Mathf.Min(particleCount,6);
            int windSeason=(int)(Mathf.Repeat(seasonTime,1)*4)%4;
            if(scenario==ParkScenario.Tree && kind==ParkAction.Wind && windSeason==3)particleCount=0;
            for(int i=0;i<particleCount;i++) {
                var shape=ParkFeedbackShape.Star; var color=Color.yellow;
                if(scenario==ParkScenario.Room && kind==ParkAction.Awaken){shape=ParkFeedbackShape.Mote;color=new Color(.68f,.80f,.85f);}
                if(scenario==ParkScenario.Tree && kind!=ParkAction.Wind){shape=ParkFeedbackShape.Mote;color=new Color(.70f,.59f,.40f);}
                if(scenario==ParkScenario.Statue && kind==ParkAction.Awaken){shape=ParkFeedbackShape.Mote;color=new Color(.62f,.53f,.37f);}
                if(scenario==ParkScenario.Bugs && kind==ParkAction.Reward)shape=ParkFeedbackShape.Coin;
                if(scenario==ParkScenario.Pet && (kind==ParkAction.Feed || kind==ParkAction.Reward)) { shape=ParkFeedbackShape.Heart;color=new Color(1,.2f,.45f); }
                if(scenario==ParkScenario.Room && kind==ParkAction.Reward) { shape=ParkFeedbackShape.Candy;color=i%2==0?new Color(1,.3f,.65f):Color.cyan; }
                if(scenario==ParkScenario.Tree && kind==ParkAction.Wind) { shape=magicTree&&windSeason==0?ParkFeedbackShape.Petal:ParkFeedbackShape.Leaf;color=SeasonColors[windSeason]; }
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
                if(scenario==ParkScenario.Pet){Cancel();reliableHeadPose=false;headAnchor=null;}else ResetEffects();
                Feedback="TARGET_LOST - interaction paused";
            } else {if(!hasPresented)entryProgress=0;Feedback="Target recovered; waiting for a new gesture";}
            if(visual)visual.gameObject.SetActive(value);
            if(!value)SetHighlighted(false);
        }
        public void Undo() { Cancel(); if(history.Count>0) { state=history.Pop(); targetGrowth=growthTime=state.growth; targetSeason=seasonTime=state.season; targetColor=state.style==1?new Color(.15f,.16f,.2f):state.style==2?new Color(.9f,.3f,.5f):new Color(.25f,.65f,.4f); Revision++;ApplyGrowthPose();if(scenario==ParkScenario.Bugs){bugDefeatProgress=state.rewarded?1:0;ApplyBugDefeatPose();}ApplyVisibility(); } Feedback="Undo latest effect"; }
        void ApplyGrowthPose() {
            if(scenario==ParkScenario.Plant && branchRoot)branchRoot.localScale=new Vector3(1,Mathf.Lerp(.15f,1.4f,growthTime),1);
            if(scenario==ParkScenario.Tree){ApplyNaturalTreeGrowth();ApplyNaturalTreeSeason();}
        }
        public void ResetEffects()
        {
            entryProgress=1;
            orientationConfirmed=false;sizeConfirmed=false;
            if(representationFit) { representationFit.localPosition=Vector3.zero;representationFit.localScale=Vector3.one; }
            Cancel(); history.Clear(); state=default; targetGrowth=growthTime=.45f; targetSeason=seasonTime=0; targetColor=new Color(.25f,.65f,.4f); Revision++; Feedback="Reset";
            acceptedAt=-100;currentColor=targetColor;styleMix=0;
            ApplyComponentColors(0);
            StepNaturalBug(0,false);
            dreamVisibility=0;if(bubble && scenario==ParkScenario.Pet)bubble.localScale=dreamRestScale;
            if(naturalJellyfish)naturalJellyfish.ResetPose();
            bloomVisibility=0;if(plantFlower) {plantFlower.localScale=Vector3.zero;plantFlower.gameObject.SetActive(false);}
            if(naturalBronze)naturalBronze.ResetPose();
            if(scenario==ParkScenario.Plant && branchRoot) {branchRoot.localRotation=Quaternion.identity;StepPlantAnimation(0,0);branchRoot.localRotation=Quaternion.identity;}
            bugDefeatProgress=0;ApplyBugDefeatPose();
            windBlend=0;
            if(scenario==ParkScenario.Tree)ResetNaturalTreePose();
            confirmedPlane=false; environmentAnchor=null; reliableHeadPose=false; headAnchor=null;
            if(visual) { visual.localPosition=Vector3.zero; visual.localRotation=Quaternion.identity; visual.localScale=scenario==ParkScenario.Pet?Vector3.one*Mathf.Lerp(.6f,1.15f,.45f):Vector3.one; }
            if(scenario==ParkScenario.Plant && branchRoot)branchRoot.localScale=new Vector3(1,Mathf.Lerp(.15f,1.4f,.45f),1);
            if(built) ApplyVisibility();
        }
        void Cancel() { if(statueScan)statueScan.gameObject.SetActive(false);if(naturalPetRig)naturalPetRig.ResetPose();if(naturalBronze)naturalBronze.ResetPose();StopAllCoroutines(); RestoreStoppedScales();motion=null; if(audioSource)audioSource.Stop(); particleCount=0; foreach(var p in pool)if(p)p.gameObject.SetActive(false); if(preview)preview.gameObject.SetActive(false); if(spell)spell.gameObject.SetActive(false); bend=spin=stretch=0; dance=Vector3.zero; }
        public void ExitAndDestroy() {
            if(IsExiting)return;
            if(!Application.isPlaying || !isActiveAndEnabled || !built || !visual) {Destroy(gameObject);return;}
            Vector3 fromScale=visual.localScale;
            // Reset clears calibration for reuse. An exiting representation must
            // retain its displayed size/bottom until it has finished shrinking.
            Vector3 fitPosition=representationFit.localPosition,fitScale=representationFit.localScale;
            ResetEffects();IsExiting=true;tracked=false;SetHighlighted(false);
            representationFit.localPosition=fitPosition;representationFit.localScale=fitScale;
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
            if(petOutline)petOutline.SetVisible(state.petComic&&tracked);
            if(scenario==ParkScenario.Bugs)foreach(var r in body)if(r)r.enabled=!state.rewarded || bugDefeatProgress<1;
            eyes.gameObject.SetActive(state.awake && !(scenario==ParkScenario.Bugs && state.rewarded) && !(scenario==ParkScenario.Tree&&magicTree));
            if(plantSpeech)plantSpeech.gameObject.SetActive(state.awake);
            bubble.gameObject.SetActive(scenario==ParkScenario.Pet && (state.bubble || dreamVisibility>0));
            for(int i=0;i<dreamContents.Count;i++)dreamContents[i].gameObject.SetActive(i==state.style);
            if(scar)scar.gameObject.SetActive(state.scar && !state.sealedTree);
            reward.gameObject.SetActive(state.rewarded && scenario==ParkScenario.Tree);
            environment.gameObject.SetActive(state.rewarded && scenario==ParkScenario.Statue && confirmedPlane && environmentAnchor);
            if(jellyfish)jellyfish.gameObject.SetActive(state.awake || (naturalJellyfish && naturalJellyfish.Visibility>0));
            if(portal)portal.gameObject.SetActive(state.bubble);
        }
        void StepDreamTransition(float dt) {
            if(!bubble || scenario!=ParkScenario.Pet || !float.IsFinite(dt) || dt<0)return;
            dreamVisibility=Mathf.MoveTowards(dreamVisibility,state.bubble?1:0,dt/.3f);
            bubble.localScale=dreamRestScale*Mathf.SmoothStep(0,1,dreamVisibility);
            bubble.gameObject.SetActive(state.bubble || dreamVisibility>0);
        }
        void StepTreeWind(float dt,float now) {
            if(state.wind&&now>=treeGustUntil)state.wind=false;
            bool valid=float.IsFinite(dt) && dt>=0 && float.IsFinite(now) && float.IsFinite(windStrength) && FinitePoint(windDirection);
            if(valid)windBlend=Mathf.MoveTowards(windBlend,state.wind && !state.sealedTree?1:0,dt/.25f);
            else windBlend=0;
            float sway=valid?Mathf.Clamp01(windStrength)*10*windBlend:0;
            for(int i=0;i<branches.Count;i++) {
                float wave=valid?Mathf.Sin(now+i)*sway:0;
                if(valid&&magicTree&&state.wind&&!state.sealedTree&&float.IsFinite(treeGustUntil)){float cycle=Mathf.Repeat(now-(treeGustUntil-2.4f)+i*.08f,.8f)/.8f;wave=(cycle<.25f?Mathf.Lerp(-8,28,cycle/.25f):Mathf.Lerp(28,0,Mathf.SmoothStep(0,1,(cycle-.25f)/.75f)))*windBlend;}
                var branch=branches[i];
                var direction=valid?Vector3.ProjectOnPlane(windDirection,Vector3.up):Vector3.zero;
                var axis=Vector3.Cross(Vector3.up,direction);
                var localAxis=axis.sqrMagnitude>.00001f?branch.parent.InverseTransformDirection(axis.normalized):Vector3.right;
                branch.localRotation=Quaternion.AngleAxis(wave+bend,localAxis)*treeBranchRest[i];
            }
        }
        void ApplyBugDefeatPose() {
            if(scenario!=ParkScenario.Bugs)return;
            float t=Mathf.Clamp01(bugDefeatProgress);
            // Impact squash, then dissolve by shrinking virtual geometry; pooled rewards stay full size.
            float squash=Mathf.Sin(Mathf.Clamp01(t/.35f)*Mathf.PI);
            float shrink=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,1,t));
            if(naturalBugRig)
            {
                // Scale the mesh and its skeleton together; renderer-only scaling is cancelled by skin bind matrices.
                naturalBugRig.transform.localScale=Vector3.Scale(naturalBugRestScale,new Vector3(1+squash*.2f,1-squash*.35f,1+squash*.2f))*shrink;return;
            }
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
            StepNaturalPlant(now);
            StepPlantDialogue(now);
            if(!plantFlower)return;
            bloomVisibility=Mathf.MoveTowards(bloomVisibility,state.bloom?1:0,dt/.65f);
            float open=Mathf.SmoothStep(0,1,bloomVisibility);
            plantFlower.localScale=Vector3.one*open;
            plantFlower.gameObject.SetActive(state.bloom || bloomVisibility>0);
            for(int i=0;i<plantPetals.Count;i++) {
                float petalOpen=Mathf.SmoothStep(0,1,Mathf.InverseLerp(i*.045f,.8f+i*.045f,bloomVisibility));
                plantPetals[i].localScale=Vector3.one*.026f*Mathf.Lerp(.25f,1,petalOpen);
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
            ApplyComponentColors(styleMix*2);
            StepNaturalBug(Time.unscaledTime,state.awake && !state.rewarded);
            if(naturalBronze)naturalBronze.Step(dt,Time.unscaledTime,state.awake);
            float breathing=state.awake && !state.sealedTree && scenario!=ParkScenario.Room && scenario!=ParkScenario.Statue && scenario!=ParkScenario.Plant && scenario!=ParkScenario.Tree?Mathf.Sin(Time.unscaledTime*2)*.018f:0;
            visual.localPosition=dance; visual.localRotation=Quaternion.Euler(0,spin,breathing*100);
            visual.localScale=Vector3.one*(1+breathing)+(scenario==ParkScenario.Room||scenario==ParkScenario.Statue||scenario==ParkScenario.Plant||scenario==ParkScenario.Tree?Vector3.zero:Vector3.up*stretch*.15f);
            if(scenario==ParkScenario.Pet)visual.localScale*=Mathf.Lerp(.6f,1.15f,growthTime);
            entryProgress=Mathf.MoveTowards(entryProgress,1,dt/.28f);
            visual.localScale*=Mathf.SmoothStep(0,1,entryProgress);
            if(entryProgress>=1)hasPresented=true;
            if(scenario==ParkScenario.Plant)StepPlantAnimation(dt,Time.unscaledTime);
            if(scenario==ParkScenario.Tree) {
                ApplyNaturalTreeGrowth();ApplyNaturalTreeSeason();
                StepSeasonTreeFace(Time.unscaledTime);
                StepWillowEffects(dt,Time.unscaledTime);
                StepTreeWind(dt,Time.unscaledTime);
            }
            if(scenario==ParkScenario.Statue)for(int i=0;i<branches.Count;i++)branches[i].localRotation=Quaternion.Euler(0,0,(i==0?-1:1)*stretch*45);
            if(ears)ears.localRotation=Quaternion.Euler(0,0,state.awake?Mathf.Sin(Time.unscaledTime*3)*5:0);
            StepRoomJellyfish(dt,Time.unscaledTime);
            if(portal)portal.localScale=new Vector3(Mathf.Lerp(portal.localScale.x,state.bubble?.6f:.01f,dt*4),.45f,.02f);
            if(bubble && scenario==ParkScenario.Pet) {
                StepDreamTransition(dt);
                if(reliableHeadPose && headAnchor)bubble.position=headAnchor.position+Vector3.up*.12f;
                else if(virtualTarget && naturalPetRig)bubble.position=naturalPetRig.Head.position+Vector3.up*.12f;
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
                if(scenario==ParkScenario.Room && particleKind==ParkAction.Reward && confirmedPlane && environmentAnchor)p.position=environmentAnchor.position+new Vector3(direction.x,0,direction.z)*.25f+Vector3.down*(.02f+particleAge*.18f);
                p.localRotation=Quaternion.Euler(particleAge*80,i*30,particleAge*100); p.localScale=Vector3.one*.045f*Mathf.Clamp01((1.6f-particleAge)/.3f);
                if(scenario==ParkScenario.Room && particleKind==ParkAction.Awaken){p.localPosition=Vector3.up*(.6f+age*.025f)+direction*age*.035f;p.localScale=Vector3.one*.009f*Mathf.Clamp01((1.6f-particleAge)/.3f);}
                if(scenario==ParkScenario.Tree && particleKind!=ParkAction.Wind){p.position=TreeScarWorldPosition+visual.TransformVector(direction*age*.025f+Vector3.up*age*.02f);p.localScale=Vector3.one*.009f*Mathf.Clamp01((1.6f-particleAge)/.3f);}
            }
            ApplyVisibility();
            if(label) { label.text=scenario+" / "+(virtualTarget?"VIRTUAL TEST":"STEREO TRANSLATION")+"\n"+Feedback+"\n"+(scenario==ParkScenario.Plant?"Moisture: DEMO 55% | no sensor":scenario==ParkScenario.Pet?"Affection (virtual): "+state.affection+" | head "+(reliableHeadPose?"tracked":"approximate"):"Local effects")+"\nOrientation: "+(orientationConfirmed?"manually confirmed":"UNCONFIRMED")+" | "+(sizeConfirmed?"MANUAL nominal height/bottom":"virtual size; bottom uncalibrated"); if(Camera.main)label.transform.rotation=Quaternion.LookRotation(label.transform.position-Camera.main.transform.position); }
        }
    }
}
