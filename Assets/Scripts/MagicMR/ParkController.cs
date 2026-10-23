using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    public interface IParkTrackingStatus { string Status { get; } }
    /// <summary>Six independent scenes, manual-plane fallback, explicit virtual test mode.</summary>
    public sealed class ParkController : MonoBehaviour
    {
        public ParkScenario scenario;
        public ParkActor actorPrefab;
        public ParkActor[] registeredActors;
        public string recognitionLock;
        public bool SwitchContent(string profile,string instance) {
            int index=System.Array.IndexOf(new[]{"tree","bugs","statue","room","plant","pet"},profile);
            if(index<0 || registeredActors==null || registeredActors.Length!=6 || !registeredActors[index])return false;
            if(AnimationBusy)return false;
            Clear();scenario=(ParkScenario)index;actorPrefab=registeredActors[index];recognitionLock=instance;
            status="CLASS RECOGNIZED: "+profile+"; waiting for reliable stereo location";return true;
        }
        public bool simulation=false;
        public int maxInstances=8;
        public float lostAfter=.65f;
        [Header("Shared gesture timing (PICO validation pending)")]
        [Min(.1f)] public float openPalmStableSeconds=.3f;
        [Min(.005f)] public float openPalmMaximumDriftMeters=.035f;
        [Min(.05f)] public float handSampleMaximumGapSeconds=.25f;
        [Min(.02f)] public float pinchMinimumSeconds=.08f;
        [Min(.1f)] public float pinchMaximumSeconds=.8f;
        [Min(.3f)] public float fistHoldSeconds=.7f;
        [Min(.05f)] public float motionHeadMaximumSpeed=.25f;
        [Min(1f)] public float motionHeadMaximumAngularSpeed=45f;
        [Min(.1f)] public float motionHeadSettlingSeconds=.2f;
        public MonoBehaviour receiver;
        public ParkActor selected;
        public TextMesh hud;
        public bool sharedInterface;
        public string InteractionStatus=>status;
        public string GestureHint=>Hint().Replace("0.3s",openPalmStableSeconds.ToString("0.##")+"s").Replace("0.7s",fistHoldSeconds.ToString("0.##")+"s")+(scenario==ParkScenario.Plant?"\nSample hand shapes: "+(ParkHandTracking.SampleShapeAssetsAvailable?"PICO validation pending":"MISSING; open/fist disabled"):"");
        public bool AnimationBusy=>(selected && selected.Busy) || FitConfirmationPending || PlaneConfirmationPending ||
            (!rightHandUiCapture && (pinchHeld || fistHeld || openPalmSince>=0));
        public void ResetScene() { Clear();status="RESET: targets and effects cleared"; }
        readonly Dictionary<string,ParkActor> actors=new Dictionary<string,ParkActor>();
        readonly Dictionary<string,float> seen=new Dictionary<string,float>();
        readonly Dictionary<string,int> confirmations=new Dictionary<string,int>();
        readonly List<string> expired=new List<string>();
        readonly List<Transform> buttons=new List<Transform>();
        readonly List<string> buttonActions=new List<string>();
        readonly TabletopGestureRules rules=new TabletopGestureRules();
        Vector3 lastPalm;
        float lastPalmAt, pinchAt, fistAt, leftFistAt, spreadStart, nextPull, menuAt;
        bool pinchHeld, fistHeld,leftFistHeld,pullArmed,hasPalm,pointHeld,undoFired;
        bool clapArmed;
        bool bothFistsResetFired;
        float openPalmSince=-1;
        readonly ParkStableHold openPalmHold=new ParkStableHold();
        readonly ParkPinchLifecycle pinchLifecycle=new ParkPinchLifecycle();
        bool openPalmFired;
        ParkActor pinchTarget;
        ParkActor pointingTarget;
        bool requireGestureRelease;
        float lastHandSampleAt=-1;
        float headSampleAt=-1,headMotionBlockedUntil;
        Vector3 previousHeadPosition;
        Quaternion previousHeadRotation;
        public string MotionGestureStatus {get;private set;}="Motion: waiting for head samples";
        bool AcceptMotionHeadPose(Vector3 position,Quaternion rotation,float now) {
            float norm=rotation.x*rotation.x+rotation.y*rotation.y+rotation.z*rotation.z+rotation.w*rotation.w;
            bool finite=float.IsFinite(position.x)&&float.IsFinite(position.y)&&float.IsFinite(position.z)&&
                float.IsFinite(rotation.x)&&float.IsFinite(rotation.y)&&float.IsFinite(rotation.z)&&float.IsFinite(rotation.w)&&float.IsFinite(now)&&float.IsFinite(norm)&&Mathf.Abs(norm-1)<.02f;
            float dt=now-headSampleAt;
            bool continuous=finite && headSampleAt>=0 && dt>0 && dt<=handSampleMaximumGapSeconds;
            bool moving=!continuous || !float.IsFinite(motionHeadMaximumSpeed) || motionHeadMaximumSpeed<=0 ||
                !float.IsFinite(motionHeadMaximumAngularSpeed) || motionHeadMaximumAngularSpeed<=0 ||
                !float.IsFinite(motionHeadSettlingSeconds) || motionHeadSettlingSeconds<.1f;
            if(continuous)moving|=Vector3.Distance(position,previousHeadPosition)/dt>motionHeadMaximumSpeed || Quaternion.Angle(rotation,previousHeadRotation)/dt>motionHeadMaximumAngularSpeed;
            if(moving)headMotionBlockedUntil=now+Mathf.Max(.1f,float.IsFinite(motionHeadSettlingSeconds)?motionHeadSettlingSeconds:.2f);
            headSampleAt=finite?now:-1;previousHeadPosition=position;previousHeadRotation=rotation;
            bool accepted=finite && !moving && now>=headMotionBlockedUntil;
            MotionGestureStatus=!finite?"Motion: invalid head pose":moving?"Motion: paused; keep head still":accepted?"Motion: ready (PICO unverified)":"Motion: settling";
            return accepted;
        }
        bool AcceptHandSample(float now) {
            bool valid=!float.IsNaN(now) && !float.IsInfinity(now) &&
                !float.IsNaN(handSampleMaximumGapSeconds) && !float.IsInfinity(handSampleMaximumGapSeconds) && handSampleMaximumGapSeconds>0;
            bool continuous=valid && (lastHandSampleAt<0 || (now>=lastHandSampleAt && now-lastHandSampleAt<=handSampleMaximumGapSeconds));
            lastHandSampleAt=valid?now:-1;
            if(!continuous)ResetHandInteraction();
            return continuous;
        }
        float lastSpread,lastSpreadAt,clapArmedAt;
        readonly Dictionary<ParkActor,Transform> surfaceAnchors=new Dictionary<ParkActor,Transform>();
        ParkActor planeTarget,fitTarget;
        Vector3 fitBottom,fitStart;
        bool fitHasBottom;
        public bool FitConfirmationPending=>fitTarget!=null;
        bool suppressPinchRelease;
        bool rightHandUiCapture,leftHandUiCapture;
        public void SetLeftHandUiCapture(bool captured) { leftHandUiCapture=captured; }
        public void SetRightHandUiCapture(bool captured) { rightHandUiCapture=captured; }
        bool StepFists(bool rightClosed,bool leftClosed,float now,out ParkAction action) {
            action=ParkAction.Undo;
            if(rightClosed && !fistHeld) {fistAt=now;undoFired=false;}
            if(leftClosed && !leftFistHeld)leftFistAt=now;
            if(!rightClosed && !leftClosed)bothFistsResetFired=false;
            // A pair owns this hold even if the left hand releases, is lost or enters UI.
            // A fresh right-hand release/press is required before a solo command.
            if(rightClosed && leftClosed)undoFired=true;
            bool fire=false;
            if(rightClosed && leftClosed && !bothFistsResetFired && now-Mathf.Max(fistAt,leftFistAt)>.5f) {
                action=ParkAction.Reset;bothFistsResetFired=true;fire=true;
            } else if(rightClosed && !leftClosed && !bothFistsResetFired && !undoFired && now-fistAt>=fistHoldSeconds) {
                action=scenario==ParkScenario.Room?ParkAction.Stop:ParkAction.Undo;undoFired=true;fire=true;
            }
            fistHeld=rightClosed;leftFistHeld=leftClosed;
            return fire;
        }
        void ResetHandInteraction() {
            ResetHandState(true);
        }
        void ResetHandState(bool interruptEffects) {
            if(interruptEffects)selected?.EndHandInteraction();
            rules.Reset();hasPalm=false;
            pinchHeld=fistHeld=leftFistHeld=pullArmed=clapArmed=false;
            undoFired=false;bothFistsResetFired=false;suppressPinchRelease=true;
            lastSpread=lastSpreadAt=0;
            openPalmSince=-1;openPalmFired=false;
            openPalmHold.Reset();
            pinchLifecycle.Cancel();
            pinchTarget=null;
            if(pointingTarget)pointingTarget.SetHighlighted(pointingTarget==selected);
            pointingTarget=null;
            requireGestureRelease=true;
        }
        public bool PlaneConfirmationPending=>planeTarget!=null;
        string status="VIRTUAL TEST - no real object removed";
#if XR_HANDS_1_1_OR_NEWER
        XRHandSubsystem hands;
#endif
        static readonly string[] SceneNames={"ParkTree","ParkBugs","ParkStatue","ParkRoom","ParkPlant","ParkPet"};
        public string Profile => new[]{"tree","bugs","statue","room","plant","pet"}[(int)scenario];
        public string TargetClasses=>new[]{"Tree","Cockroach / Spider","Statue / BronzeBeast","Room: manual space confirmation","PottedPlant","Cat / Dog"}[(int)scenario];
        public int InstanceCount=>actors.Count;
        void Start()
        {
            var camera=Camera.main; if(!camera)return;
            if(!sharedInterface && !hud) { hud=new GameObject("Park operating instructions").AddComponent<TextMesh>(); hud.transform.SetParent(camera.transform,false); hud.transform.localPosition=new Vector3(0,.24f,.85f); hud.characterSize=.008f; hud.fontSize=40; hud.anchor=TextAnchor.MiddleCenter; }
            var actions=sharedInterface?System.Array.Empty<string>():new[]{"Tree","Bugs","Statue","Room","Plant","Pet","Mode","Place","Plane","Reset","Lighter","TreeMode","Fit"};
            for(int i=0;i<actions.Length;i++) {
                var b=GameObject.CreatePrimitive(PrimitiveType.Cube); b.name=actions[i]; b.transform.SetParent(camera.transform,false); b.transform.localPosition=new Vector3((i%6-2.5f)*.11f,-.23f-i/6*.07f,.7f); b.transform.localScale=new Vector3(.095f,.045f,.01f);
                Destroy(b.GetComponent<Collider>()); b.GetComponent<Renderer>().sharedMaterial=actorPrefab.sharedMaterial;
                var text=new GameObject("Label").AddComponent<TextMesh>(); text.transform.SetParent(b.transform,false); text.transform.localScale=new Vector3(1/.095f,1/.045f,100); text.transform.localPosition=new Vector3(0,0,-.6f); text.characterSize=.005f; text.fontSize=32; text.anchor=TextAnchor.MiddleCenter; text.text=actions[i];
                buttons.Add(b.transform); buttonActions.Add(actions[i]);
            }
            if(simulation)SpawnVirtual();
        }
        public bool Allows(string cls)
        {
            switch(scenario) {
                case ParkScenario.Tree:return cls=="Tree";
                case ParkScenario.Bugs:return cls=="Cockroach" || cls=="Spider";
                case ParkScenario.Statue:return cls=="Statue" || cls=="BronzeBeast";
                case ParkScenario.Room:return cls=="chair" || cls=="couch" || cls=="bed" || cls=="dining table" || cls=="Lamp";
                case ParkScenario.Plant:return cls=="PottedPlant";
                case ParkScenario.Pet:return cls=="Cat" || cls=="Dog";
                default:return false;
            }
        }
        ParkActor Create(string id,string cls,Vector3 position,bool virtualTarget)
        {
            var actor=Instantiate(actorPrefab,position,Quaternion.identity); actor.name="Anchor_"+id; actor.species=cls; actor.instanceId=id; actor.virtualTarget=virtualTarget; actor.gameObject.SetActive(true); actor.Build();
            if(sharedInterface && actor.label)actor.label.gameObject.SetActive(false);
            actors.Add(id,actor); seen[id]=Time.unscaledTime; confirmations[id]=0; if(!virtualTarget)actor.SetTracked(false); return actor;
        }
        public void SpawnVirtual() {SpawnVirtual(null);}
        public void SpawnVirtual(string recognizedClass)
        {
            if(!Camera.main || !actorPrefab)return;
            var cam=Camera.main.transform; Vector3 point=cam.position+cam.forward*1.35f+cam.right*.85f-cam.up*.2f;
            string species=scenario==ParkScenario.Pet?(string.Equals(recognizedClass?.Trim(),"Dog",System.StringComparison.OrdinalIgnoreCase)?"Dog":"Cat"):scenario==ParkScenario.Bugs?(string.Equals(recognizedClass?.Trim(),"Spider",System.StringComparison.OrdinalIgnoreCase)?"Spider":"Cockroach"):scenario.ToString();
            selected=Create("virtual:"+actors.Count,species,point,true);
            selected.SetVirtualPreviewSize(.35f);
            status="VIRTUAL TEST: point/pinch to select; no YOLO/depth claim";
        }
        public void Observe(string id,string cls,Vector3 point)
        {
            if(!string.IsNullOrEmpty(recognitionLock) && id!=recognitionLock)return;
            if(simulation || !Allows(cls))return;
            if(float.IsNaN(point.x)||float.IsNaN(point.y)||float.IsNaN(point.z)||float.IsInfinity(point.x)||float.IsInfinity(point.y)||float.IsInfinity(point.z))return;
            if(!actors.TryGetValue(id,out var actor)) { if(actors.Count>=maxInstances)return; actor=Create(id,cls,point,false); }
            // Large jumps require re-selection rather than animating a false tracked path.
            if(Vector3.Distance(actor.transform.position,point)>.45f) { actor.SetTracked(false); confirmations[id]=0; status="Depth jump rejected; select a newly verified target"; return; }
            if(!actor.tracked) {
                if(Vector3.Distance(actor.transform.position,point)>.12f)confirmations[id]=0;
                actor.transform.position=point; seen[id]=Time.unscaledTime;
                if(++confirmations[id]<3) { status="CONFIRMING_DEPTH: keep target visible"; return; }
            }
            float dt=Mathf.Max(0,Time.unscaledTime-seen[id]);
            float blend=1-Mathf.Exp(-dt/.08f);
            actor.transform.position=Vector3.Lerp(actor.transform.position,point,blend); actor.SetTracked(true); seen[id]=Time.unscaledTime;
            // Never silently switch the selected instance on each frame.
            status="2D class + stereo translation. Point to select; Place confirms virtual orientation.";
        }
        public void ExpirePhysicalTargets() { foreach(var pair in actors)if(!pair.Value.virtualTarget) { pair.Value.SetTracked(false);ReleaseSurface(pair.Value);confirmations[pair.Key]=0; } }
        public bool RequestFit() {
            if(!selected || !selected.tracked || selected.Busy)return false;
            bool oriented=selected.orientationConfirmed;float yaw=selected.transform.eulerAngles.y;
            ReleaseSurface(selected);selected.ResetEffects();if(oriented)selected.ConfirmOrientation(yaw);
            fitTarget=selected;fitHasBottom=false;status="FIT: pinch at target bottom, then pinch at top; hold target still";return true;
        }
        public bool ConfirmFit(Vector3 point,bool valid) {
            if(!fitTarget || fitTarget!=selected || !fitTarget.tracked || !valid || !float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))return false;
            if(!fitHasBottom) {
                if(Vector3.Distance(point,fitTarget.transform.position)>1.2f) { status="FIT: bottom must be near the selected target";return false; }
                fitBottom=point;fitStart=fitTarget.transform.position;fitHasBottom=true;status="FIT: bottom recorded; pinch at top";return true;
            }
            if(Vector3.Distance(fitTarget.transform.position,fitStart)>.08f) { fitTarget=null;fitHasBottom=false;status="FIT cancelled: target moved; keep it still and retry";return false; }
            if(!fitTarget.FitRepresentation(fitBottom,point,true)) { status="FIT: confirm a vertical height of 0.005 to 3 metres";return false; }
            status=fitTarget.Feedback;fitTarget=null;fitHasBottom=false;return true;
        }
        public bool RequestManualPlane() {
            if(!selected || !selected.tracked) { status="Select a tracked target before confirming a surface";return false; }
            fitTarget=null;fitHasBottom=false;planeTarget=selected;status="Touch the intended surface with index tip, then pinch to confirm a horizontal manual anchor";return true;
        }
        public bool ConfirmManualPlane(Vector3 point,bool valid) {
            if(!planeTarget || planeTarget!=selected || !planeTarget.tracked || !valid || !float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))return false;
            if(!surfaceAnchors.TryGetValue(planeTarget,out var anchor)) {
                anchor=new GameObject("Manual horizontal surface - "+planeTarget.instanceId).transform;anchor.SetParent(transform,true);surfaceAnchors.Add(planeTarget,anchor);
            }
            anchor.SetPositionAndRotation(point,Quaternion.identity);planeTarget.environmentAnchor=anchor;planeTarget.confirmedPlane=true;
            planeTarget=null;status="Horizontal surface confirmed manually; no scene reconstruction";return true;
        }
        void ReleaseSurface(ParkActor actor) {
            if(planeTarget==actor)planeTarget=null;
            if(fitTarget==actor) { fitTarget=null;fitHasBottom=false; }
            if(surfaceAnchors.TryGetValue(actor,out var anchor)) { if(anchor)Destroy(anchor.gameObject);surfaceAnchors.Remove(actor); }
            if(actor) { actor.environmentAnchor=null;actor.confirmedPlane=false; }
        }
        public bool Dispatch(ParkAction action,Vector3 hand,bool valid)
        {
            if(!selected) { status="Select an instance first"; return false; }
            if(action==ParkAction.Reset)ReleaseSurface(selected);
            else if(FitConfirmationPending) { status="Finish Fit or Reset before effects";return false; }
            bool accepted=selected.TryAction(action,hand,valid); status=selected.Feedback; return accepted;
        }
        public bool ConfirmVirtualAttack(ParkActor pointedTarget,Vector3 hand,bool valid)
        {
            if(scenario!=ParkScenario.Bugs || !pointedTarget || pointedTarget!=selected || !pointedTarget.tracked || !actors.ContainsValue(pointedTarget)) {
                status="Attack cancelled: point at a valid virtual enemy before pinching";return false;
            }
            return Dispatch(ParkAction.Reward,hand,valid);
        }
        void Clear(bool animateExit=true)
        {
            ResetHandInteraction();
            planeTarget=null;fitTarget=null;fitHasBottom=false;suppressPinchRelease=false;
            foreach(var pair in surfaceAnchors) { if(pair.Key)pair.Key.ResetEffects();if(pair.Value)Destroy(pair.Value.gameObject); }surfaceAnchors.Clear();
            foreach(var actor in actors.Values) { if(actor) { if(animateExit && Application.isPlaying && actor.isActiveAndEnabled)actor.ExitAndDestroy();else {actor.ResetEffects();Destroy(actor.gameObject);} } }
            actors.Clear(); seen.Clear(); confirmations.Clear(); selected=null; rules.Reset(); hasPalm=false; pullArmed=false; clapArmed=false;
        }
        void Menu(string command,Vector3 hand)
        {
            if(Time.unscaledTime-menuAt<.6f)return; menuAt=Time.unscaledTime;
            int scene=System.Array.IndexOf(new[]{"Tree","Bugs","Statue","Room","Plant","Pet"},command);
            if(scene>=0) { Clear(); SceneManager.LoadScene(SceneNames[scene]); return; }
            switch(command) {
                case "Mode": Clear(); simulation=!simulation; if(simulation)SpawnVirtual(); else status="REAL DETECTION: PC profile must match "+Profile; break;
                case "Place":
                    if(simulation) { if(actors.Count<maxInstances)SpawnVirtual(); }
                    else if(selected && selected.ConfirmOrientation(Camera.main.transform.eulerAngles.y)) { status="Orientation confirmed manually; virtual size and bottom remain uncalibrated"; }
                    break;
                case "Plane": RequestManualPlane(); break;
                case "Fit": RequestFit(); break;
                case "Reset": foreach(var a in actors.Values) { ReleaseSurface(a);a.ResetEffects(); } break;
                case "TreeMode": if(selected && scenario==ParkScenario.Tree) { ReleaseSurface(selected);selected.ResetEffects(); selected.magicTree=!selected.magicTree; status=selected.magicTree?"MAGIC TREE: awaken, wind, soothe scar, seal":"SEASON TREE: palm up/down timeline"; } break;
                case "Lighter": Clear(); SceneManager.LoadScene("BridgeStereoFusion"); break;
            }
        }
        ParkActor Point(Vector3 origin,Vector3 direction,bool select)
        {
            if(!float.IsFinite(origin.x)||!float.IsFinite(origin.y)||!float.IsFinite(origin.z)||!float.IsFinite(direction.x)||!float.IsFinite(direction.y)||!float.IsFinite(direction.z)||direction.sqrMagnitude<.001f)return null; direction.Normalize(); float best=.06f; int button=-1;
            for(int i=0;i<buttons.Count;i++) { Vector3 delta=buttons[i].position-origin; float along=Vector3.Dot(delta,direction); float distance=(delta-direction*along).magnitude; if(along>0 && along<2 && distance<best) { best=distance;button=i; } }
            if(button>=0 && select) { Menu(buttonActions[button],origin); return null; }
            ParkActor candidate=null; best=3;
            foreach(var actor in actors.Values) { if(!actor || !actor.tracked)continue; var delta=actor.transform.position-origin; float along=Vector3.Dot(delta,direction); float distance=(delta-direction*along).magnitude; if(along>0 && along<best && distance<.17f) { best=along;candidate=actor; } }
            if(candidate && select) {
                if(selected!=candidate) {
                    selected?.EndHandInteraction();
                    rules.Reset();hasPalm=false;pullArmed=clapArmed=false;
                    openPalmHold.Reset();openPalmSince=-1;openPalmFired=false;
                }
                selected=candidate; status="Locked instance "+selected.instanceId;
            }
            return candidate;
        }
        void Update()
        {
            if(planeTarget && (planeTarget!=selected || !planeTarget.tracked))planeTarget=null;
            if(fitTarget && (fitTarget!=selected || !fitTarget.tracked)) { fitTarget=null;fitHasBottom=false; }
            expired.Clear(); foreach(var pair in actors)if(!pair.Value.virtualTarget && Time.unscaledTime-seen[pair.Key]>lostAfter) { pair.Value.SetTracked(false);ReleaseSurface(pair.Value);confirmations[pair.Key]=0; if(Time.unscaledTime-seen[pair.Key]>5)expired.Add(pair.Key); }
            foreach(var id in expired) { if(selected==actors[id]) {ResetHandInteraction();selected=null;} Destroy(actors[id].gameObject); actors.Remove(id); seen.Remove(id); confirmations.Remove(id); }
            foreach(var actor in actors.Values)actor.SetHighlighted(actor==selected || actor==pointingTarget);
#if XR_HANDS_1_1_OR_NEWER
            hands=ParkHandTracking.Current;
            if(hands!=null && (!simulation || hands.rightHand.isTracked || hands.leftHand.isTracked))SampleHands();
            else {
                // Desktop simulation has no hand source. Only an actual tracking loss interrupts its effects.
                if(!simulation || lastHandSampleAt>=0)ResetHandInteraction();else ResetHandState(false);
                lastHandSampleAt=-1;
            }
#endif
            if(hud)hud.text=scenario+" | "+(simulation?"VIRTUAL TEST":"REAL: "+Profile)+"\n"+(FitConfirmationPending?(fitHasBottom?"FIT: bottom captured; pinch at top vertically (0.005-3m); hold still":"FIT: pinch at target bottom; hold still"):PlaneConfirmationPending?"PENDING SURFACE: touch it with index tip, then pinch":status)+"\n"+(receiver && !simulation?(receiver as IParkTrackingStatus)?.Status+"\n":"")+Hint();
#if UNITY_EDITOR || UNITY_STANDALONE
            if(simulation && selected) {
                var hand=selected.transform.position+Vector3.up*.1f;
                if(ParkDesktopInput.KeyDown(KeyCode.Alpha1))Dispatch(ParkAction.Style,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.Alpha2))Dispatch(ParkAction.Awaken,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.Alpha3))Dispatch(ParkAction.Wind,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.Alpha4))Dispatch(ParkAction.Reward,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.B))Dispatch(ParkAction.Pull,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.F))Dispatch(ParkAction.Feed,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.O))Dispatch(ParkAction.Poke,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.UpArrow))Dispatch(ParkAction.GrowUp,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.DownArrow))Dispatch(ParkAction.GrowDown,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.Z))Dispatch(ParkAction.Undo,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.R))Dispatch(ParkAction.Reset,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.G))Menu("Plane",hand);
                if(ParkDesktopInput.KeyDown(KeyCode.T))selected.magicTree=!selected.magicTree;
                if(ParkDesktopInput.KeyDown(KeyCode.N))SceneManager.LoadScene(SceneNames[((int)scenario+1)%6]);
            }
            if(ParkDesktopInput.ClickDown && !ParkDesktopInput.PointerCapturedByUi && Camera.main) { var ray=Camera.main.ScreenPointToRay(ParkDesktopInput.Pointer); Point(ray.origin,ray.direction,true); }
#endif
        }
        string Hint() {
            switch(scenario) {
                case ParkScenario.Tree:return "Pinch-release: season | hold open palm 0.3s: awaken\nPalm up/down: season | swipe: wind | fist-open: seal";
                case ParkScenario.Bugs:return "Point+pinch-release: select and attack virtual enemy\nCircle: dance | swipe: stun | defeated enemy requires Reset";
                case ParkScenario.Statue:return "Hold open palm 0.3s: reveal | pinch-release: chapter\nSwipe: story | confirmed plane + fist-open: lotus";
                case ParkScenario.Room:return "Open palm 0.3s: selected effect | pinch: effect type\nConfirm ceiling for candy | right fist 0.7s / Stop: exit";
                case ParkScenario.Plant:return "Open palm 0.3s: virtual growth | pinch-release: bloom\nFist-open: style | palm up/down: growth | fist: undo / both: Reset";
                case ParkScenario.Pet:return "Open palm 0.3s: toggle dream bubble | pinch: content\nFist-open: affection | distance interaction needs validation";
                default:return "";
            }
        }
#if XR_HANDS_1_1_OR_NEWER
        bool Read(XRHand hand,XRHandJointID id,out Vector3 world) { var root=Camera.main?Camera.main.transform.parent:null; bool ok=hand.GetJoint(id).TryGetPose(out var pose); world=root?root.TransformPoint(pose.position):pose.position; return ok && float.IsFinite(world.x) && float.IsFinite(world.y) && float.IsFinite(world.z); }
        bool Fist(XRHand h) { if(scenario==ParkScenario.Plant)return ParkHandTracking.MatchesSampleShape(h,false);if(!Read(h,XRHandJointID.Palm,out var p))return false; int count=0; foreach(var id in new[]{XRHandJointID.IndexTip,XRHandJointID.MiddleTip,XRHandJointID.RingTip,XRHandJointID.LittleTip})if(Read(h,id,out var tip) && Vector3.Distance(tip,p)<.085f)count++; return count==4; }
        bool OpenHand(XRHand h) {
            if(scenario==ParkScenario.Plant)return ParkHandTracking.MatchesSampleShape(h,true);
            if(!h.isTracked || !Read(h,XRHandJointID.Palm,out var p))return false;
            foreach(var id in new[]{XRHandJointID.IndexTip,XRHandJointID.MiddleTip,XRHandJointID.RingTip,XRHandJointID.LittleTip})
                if(!Read(h,id,out var tip) || Vector3.Distance(tip,p)<.10f)return false;
            return true;
        }
        void SampleHands()
        {
            if(rightHandUiCapture) { ResetHandInteraction();return; }
            var right=hands.rightHand; var left=hands.leftHand; float now=Time.unscaledTime;
            if(!AcceptHandSample(now))return;
            var head=Camera.main?Camera.main.transform:null;
            bool motionSafe=head && AcceptMotionHeadPose(head.position,head.rotation,now);
            if(!head)MotionGestureStatus="Motion: head camera unavailable";
            if(!motionSafe) {rules.Reset();hasPalm=false;pullArmed=clapArmed=false;}
            if(!right.isTracked || !Read(right,XRHandJointID.Palm,out var palm) || !Read(right,XRHandJointID.IndexTip,out var index) || !Read(right,XRHandJointID.ThumbTip,out var thumb) || !Read(right,XRHandJointID.IndexProximal,out var proximal)) {
                ResetHandInteraction(); return;
            }
            bool pinch=Vector3.Distance(index,thumb)<.035f; bool fist=Fist(right);
            bool rightOpenHand=OpenHand(right);
            // Plant motion and static holds share the same authored shape predicate.
            bool open=(scenario==ParkScenario.Plant?rightOpenHand:Vector3.Distance(index,palm)>.12f) && !fist;
            if(selected && !selected.tracked) {ResetHandInteraction();return;}
            if(requireGestureRelease) {
                bool leftClosed=!leftHandUiCapture && left.isTracked && Fist(left);
                if(pinch || fist || rightOpenHand || leftClosed) {
                    status="Release the held gesture before interacting again";return;
                }
                requireGestureRelease=false;rules.Reset();hasPalm=false;
            }
            selected?.Preview(index,true);
            if(scenario==ParkScenario.Bugs && !pinch && !fist && !rightOpenHand) {
                var previous=pointingTarget;pointingTarget=Point(index,index-proximal,false);
                if(previous && previous!=pointingTarget)previous.SetHighlighted(previous==selected);
                if(pointingTarget)pointingTarget.SetHighlighted(true);
            } else if(!pinch) {
                if(pointingTarget)pointingTarget.SetHighlighted(pointingTarget==selected);
                pointingTarget=null;
            }
            bool menuPinch=false;
            for(int n=0;n<buttons.Count;n++)if(Vector3.Distance(index,buttons[n].position)<.055f && pinch) { Menu(buttonActions[n],index); menuPinch=true; break; }
            if(pinch && !pinchHeld && !menuPinch && FitConfirmationPending) { ConfirmFit(index,true);pinchHeld=true;suppressPinchRelease=true;rules.Reset();return; }
            if(pinch && !pinchHeld && !menuPinch && PlaneConfirmationPending) { ConfirmManualPlane(index,true);pinchHeld=true;suppressPinchRelease=true;rules.Reset();return; }
            if(pinch && !pinchHeld) { pinchAt=now;pinchTarget=menuPinch?null:Point(index,index-proximal,true); }
            if(menuPinch) { pinchLifecycle.Cancel();pinchHeld=pinch;suppressPinchRelease=true;rules.Reset();return; }
            var pinchPhase=pinchLifecycle.Step(pinch,!suppressPinchRelease && !PlaneConfirmationPending && !FitConfirmationPending,now,pinchMinimumSeconds,pinchMaximumSeconds,handSampleMaximumGapSeconds);
            if(pinchPhase==ParkPinchPhase.Confirmed && selected) {
                if(scenario!=ParkScenario.Bugs)Dispatch(scenario==ParkScenario.Plant?ParkAction.Bloom:ParkAction.Style,palm,true);
                else ConfirmVirtualAttack(pinchTarget,palm,true);
            }
            pinchHeld=pinch;
            if(!pinch) {suppressPinchRelease=false;pinchTarget=null;}
            Vector3 leftPalm=Vector3.zero; bool leftValid=!leftHandUiCapture && left.isTracked && Read(left,XRHandJointID.Palm,out leftPalm);
            bool leftFist=leftValid && Fist(left);
            if(StepFists(fist,leftFist,now,out var fistAction))Dispatch(fistAction,palm,true);
            if(PlaneConfirmationPending || FitConfirmationPending) { rules.Reset();hasPalm=false;pullArmed=false;clapArmed=false;return; }
            // Motion and fist-open have priority over a stationary palm hold.
            // Once any open-hand action fires, release before another action.
            bool stableOpen=rightOpenHand && !pinch;
            var holdPhase=openPalmHold.Step(stableOpen,palm,now,openPalmStableSeconds,openPalmMaximumDriftMeters,handSampleMaximumGapSeconds);
            if(holdPhase==ParkHoldPhase.Ended && stableOpen) {ResetHandInteraction();return;}
            openPalmSince=openPalmHold.Began;openPalmFired=openPalmHold.Consumed;
            if(stableOpen && openPalmFired) {rules.Reset();return;}
            bool bothOpen=motionSafe && leftValid && OpenHand(left) && rightOpenHand;
            float spread=bothOpen?Vector3.Distance(palm,leftPalm):0;
            if(scenario==ParkScenario.Plant && bothOpen) {
                if(spread>.2f) { clapArmed=true;clapArmedAt=now; }
                float closing=now-lastSpreadAt>.001f && now-lastSpreadAt<.1f?(lastSpread-spread)/(now-lastSpreadAt):0;
                if(clapArmed && spread<.09f && closing>.35f && now-clapArmedAt<.7f) { Dispatch(ParkAction.Awaken,palm,true);openPalmFired=true;openPalmHold.Consume();clapArmed=false;pullArmed=false;rules.Reset();lastSpread=spread;lastSpreadAt=now;return; }
            } else clapArmed=false;
            lastSpread=spread;lastSpreadAt=now;
            if(bothOpen && !pullArmed) { spreadStart=Vector3.Distance(palm,leftPalm); pullArmed=true; }
            if(!bothOpen)pullArmed=false;
            if(bothOpen && Vector3.Distance(palm,leftPalm)-spreadStart>.13f && now>nextPull) { Dispatch(ParkAction.Pull,palm,true); openPalmFired=true;openPalmHold.Consume();nextPull=now+.8f; spreadStart=Vector3.Distance(palm,leftPalm); rules.Reset(); return; }
            Vector3 velocity=motionSafe && hasPalm && now-lastPalmAt>.001f?(palm-lastPalm)/(now-lastPalmAt):Vector3.zero; lastPalm=palm; lastPalmAt=now; hasPalm=motionSafe;
            if(open && Mathf.Abs(velocity.y)>.35f && (scenario==ParkScenario.Plant || scenario==ParkScenario.Tree)) { Dispatch(velocity.y>0?ParkAction.GrowUp:ParkAction.GrowDown,palm,true); openPalmFired=true;openPalmHold.Consume();rules.Reset(); return; }
            if(scenario==ParkScenario.Pet && selected && !pinch && !fist && Vector3.Distance(index,selected.transform.position+Vector3.up*selected.headOffset)<.08f)Dispatch(ParkAction.Poke,palm,true);
            if(scenario==ParkScenario.Tree && selected && selected.magicTree && !pinch && Vector3.Distance(index,selected.transform.TransformPoint(new Vector3(0,.29f,-.07f)))<.06f)Dispatch(ParkAction.Poke,palm,true);
            var sample=new TabletopGestureRules.Sample { time=now,pinch=Vector3.Distance(index,thumb),index=index,palm=palm,swipeAxis=Camera.main?Camera.main.transform.right:Vector3.right,fist=fist,open=open,pointing=!fist && !pinch && !open };
            var action=motionSafe?rules.Step(sample):EditDimension.None;
            if(action==EditDimension.Agency) {Dispatch(ParkAction.Awaken,palm,true);return;}
            if(action==EditDimension.Rule) {Dispatch(ParkAction.Wind,palm,true);openPalmFired=true;openPalmHold.Consume();return;}
            if(action==EditDimension.Deconstruction && !undoFired && scenario!=ParkScenario.Bugs) {
                Dispatch(scenario==ParkScenario.Plant?ParkAction.Style:ParkAction.Reward,palm,true);
                openPalmFired=true;openPalmHold.Consume();return;
            }
            if(holdPhase==ParkHoldPhase.Ready && !openPalmFired) {
                openPalmFired=true;openPalmHold.Consume();rules.Reset();
                if(scenario!=ParkScenario.Bugs)
                    Dispatch(scenario==ParkScenario.Pet?ParkAction.Pull:scenario==ParkScenario.Plant?ParkAction.GrowUp:scenario==ParkScenario.Room && selected && selected.RoomCandySelected?ParkAction.Reward:ParkAction.Awaken,palm,true);
            } else if(openPalmHold.Active && !openPalmFired && scenario!=ParkScenario.Bugs) {
                status="Open palm confirming: "+Mathf.Clamp01((now-openPalmSince)/Mathf.Max(.1f,openPalmStableSeconds)).ToString("P0")+"; hold still";
            }
        }
#endif
        void OnDisable() { Clear(false); }
    }
}
