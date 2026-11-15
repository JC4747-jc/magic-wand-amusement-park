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
        EnabledDimensions studyDimensions=EnabledDimensions.All;
        string dispatchSource="controller_api";
        public EnabledDimensions StudyDimensions=>studyDimensions;
        // Explicit opt-in: ordinary preview sessions must not become study data.
        public bool StartStudySession(string subjectId,int trialId=1) {
            if(string.IsNullOrWhiteSpace(subjectId)||trialId<1)return false;
            var logger=DataLogger.Instance;
            if(!logger)logger=new GameObject("Study DataLogger").AddComponent<DataLogger>();
            if(logger.SessionActive)return false;
            var study=FindFirstObjectByType<ExperimentConditionSwitcher>();
            if(!study)return false;
            string label="C"+study.CurrentConditionId;
            logger.StartSession(simulation?"SIM_"+subjectId:subjectId,label,trialId);
            study.SetCondition(study.CurrentConditionId);
            return true;
        }
        void Awake() {
            if(!FindFirstObjectByType<ExperimentConditionSwitcher>())
                gameObject.AddComponent<ExperimentConditionSwitcher>();
        }
        public void ApplyStudyCondition(EnabledDimensions dimensions) {
            studyDimensions=dimensions;ResetHandInteraction();
            foreach(var actor in actors.Values)if(actor) {ReleaseSurface(actor);actor.ResetEffects();}
            status="Study condition: "+dimensions+"; open hand before next action";
        }
        static EditDimension ActionDimension(ParkAction action) {
            switch(action) {
                case ParkAction.Style:return EditDimension.Appearance;
                case ParkAction.Awaken:return EditDimension.Agency;
                case ParkAction.Wind:return EditDimension.Rule;
                case ParkAction.Reward:return EditDimension.Deconstruction;
                default:return EditDimension.None;
            }
        }
        bool StudyAllows(ParkAction action) {
            if(action==ParkAction.Reset||action==ParkAction.Stop||action==ParkAction.Undo)return true;
            if(action==ParkAction.Awaken&&scenario==ParkScenario.Room&&selected&&selected.RoomCandySelected&&
                (studyDimensions&EnabledDimensions.Deconstruction)==0)return false;
            var dimension=ActionDimension(action);
            // Extra debug actions are not part of the four study dimensions.
            if(dimension==EditDimension.None)return studyDimensions==EnabledDimensions.All;
            var flag=(EnabledDimensions)(int)dimension;
            return (studyDimensions&flag)==flag;
        }
        void LogAction(ParkAction action,Vector3 hand,bool valid,bool accepted,string reason) {
            var logger=DataLogger.Instance;if(!logger||!logger.SessionActive)return;
            bool finite=valid&&TabletopHandSample.Finite(hand);
            logger.LogEvent(accepted?"park_action_accepted":"park_action_rejected",ActionDimension(action),
                finite&&selected?Vector3.Distance(hand,selected.transform.position):-1,-1,-1,
                finite?(Vector3?)hand:null,
                $"profile={Profile};target={(selected?selected.instanceId:"none")};mode={(simulation?"virtual_test":"physical")};input={dispatchSource};action={action};reason={reason};velocity_duration=unavailable");
        }
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
        public string GestureHint=>Hint();
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
        readonly GestureReachWindow gestureReach=new GestureReachWindow();
        ParkActor gestureTarget;
        public EditDimension LastRecognizedDimension {get;private set;}
        public bool LastMagicAccepted {get;private set;}
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
            if(interruptEffects&&selected)selected.EndHandInteraction();
            rules.Reset();gestureReach.Reset();gestureTarget=null;hasPalm=false;
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
            var study=FindFirstObjectByType<ExperimentConditionSwitcher>();
            if(study)studyDimensions=study.CurrentDimensions;
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
        public bool SelectVirtualPet(string species)
        {
            return (species=="Cat"||species=="Dog")&&SelectVirtualSpecies(ParkScenario.Pet,species);
        }
        public bool SelectVirtualBug(string species)
        {
            return (species=="Cockroach"||species=="Spider")&&SelectVirtualSpecies(ParkScenario.Bugs,species);
        }
        public bool SelectVirtualStatue(string species)
        {
            return (species=="Statue"||species=="BronzeBeast")&&SelectVirtualSpecies(ParkScenario.Statue,species);
        }
        bool SelectVirtualSpecies(ParkScenario expected,string species)
        {
            if(!simulation||scenario!=expected||AnimationBusy||(selected&&!selected.virtualTarget))return false;
            if(selected&&selected.species==species)return true;
            Clear();recognitionLock=null;SpawnVirtual(species);return selected&&selected.species==species;
        }
        public bool SetVirtualTreeMode(bool fantasy)
        {
            if(!simulation||scenario!=ParkScenario.Tree||FitConfirmationPending||PlaneConfirmationPending)return false;
            if(!selected){SpawnVirtual();if(!selected)return false;selected.magicTree=fantasy;status=fantasy?"VIRTUAL fantasy tree":"VIRTUAL natural tree";return true;}
            if(!selected.virtualTarget||!selected.tracked||!selected.isActiveAndEnabled||selected.IsExiting)return false;
            if(selected.magicTree==fantasy)return true;
            ResetHandInteraction();ReleaseSurface(selected);
            bool accepted=selected.SetVirtualTreeMode(fantasy);status=selected.Feedback;return accepted;
        }
        public void SpawnVirtual(string recognizedClass)
        {
            if(!Camera.main || !actorPrefab)return;
            var cam=Camera.main.transform; Vector3 point=cam.position+cam.forward*1.35f+cam.right*.5f-cam.up*.2f;
            string species=scenario==ParkScenario.Pet?(string.Equals(recognizedClass?.Trim(),"Dog",System.StringComparison.OrdinalIgnoreCase)?"Dog":"Cat"):scenario==ParkScenario.Bugs?(string.Equals(recognizedClass?.Trim(),"Spider",System.StringComparison.OrdinalIgnoreCase)?"Spider":"Cockroach"):scenario==ParkScenario.Statue?(string.Equals(recognizedClass?.Trim(),"BronzeBeast",System.StringComparison.OrdinalIgnoreCase)?"BronzeBeast":"Statue"):scenario.ToString();
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
            if(!StudyAllows(action)) {status="Disabled by study condition: "+action;LogAction(action,hand,valid,false,"dimension_disabled");return false;}
            if(!selected) { status="Select an instance first";LogAction(action,hand,valid,false,"no_target"); return false; }
            if(action==ParkAction.Reset)ReleaseSurface(selected);
            else if(FitConfirmationPending||PlaneConfirmationPending) { status="Finish Fit/Plane or Reset before effects";LogAction(action,hand,valid,false,"calibration_pending");return false; }
            bool accepted=selected.TryAction(action,hand,valid); status=selected.Feedback;
            LogAction(action,hand,valid,accepted,status);return accepted;
        }
        // Shared by desktop previews and recognized four-dimensional gestures.
        // Scenario-specific meaning is implemented by the actor, not by a second input consumer.
        public bool DispatchDimension(EditDimension dimension,Vector3 hand,bool valid)
        {
            switch(dimension) {
                case EditDimension.Appearance:return Dispatch(ParkAction.Style,hand,valid);
                case EditDimension.Agency:return Dispatch(ParkAction.Awaken,hand,valid);
                case EditDimension.Rule:return Dispatch(ParkAction.Wind,hand,valid);
                case EditDimension.Deconstruction:return Dispatch(ParkAction.Reward,hand,valid);
                default:status="Unsupported Park dimension";return false;
            }
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
            foreach(var pair in surfaceAnchors) {
                if(pair.Key) {pair.Key.environmentAnchor=null;pair.Key.confirmedPlane=false;}
                if(pair.Value)Destroy(pair.Value.gameObject);
            }surfaceAnchors.Clear();
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
                case "TreeMode": if(selected && scenario==ParkScenario.Tree) { ReleaseSurface(selected);selected.ResetEffects(); selected.magicTree=!selected.magicTree; status=selected.magicTree?"MAGIC TREE: A dark | B lash | C breathing scar | D seal/chest":"SEASON TREE: A anime | B awaken | C seasons | D falling foliage"; } break;
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
                    if(selected)selected.EndHandInteraction();
                    rules.Reset();gestureReach.Reset();gestureTarget=null;hasPalm=false;pullArmed=clapArmed=false;
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
            var currentHands=ParkHandTracking.Current;
            if(hands!=currentHands) {
                if(hands!=null)hands.updatedHands-=OnUpdatedHands;
                hands=currentHands;
                if(hands!=null)hands.updatedHands+=OnUpdatedHands;
                ResetHandState(lastHandSampleAt>=0);lastHandSampleAt=-1;
            }
            // Do not advance gesture time using cached joints from Update/BeforeRender.
            if(lastHandSampleAt>=0&&Time.unscaledTime-lastHandSampleAt>TabletopGestureRules.MaximumSampleGapSeconds) {
                ResetHandInteraction();lastHandSampleAt=-1;
            }
#endif
            if(hud)hud.text=scenario+" | "+(simulation?"VIRTUAL TEST":"REAL: "+Profile)+"\n"+(FitConfirmationPending?(fitHasBottom?"FIT: bottom captured; pinch at top vertically (0.005-3m); hold still":"FIT: pinch at target bottom; hold still"):PlaneConfirmationPending?"PENDING SURFACE: touch it with index tip, then pinch":status)+"\n"+(receiver && !simulation?(receiver as IParkTrackingStatus)?.Status+"\n":"")+Hint();
#if UNITY_EDITOR || UNITY_STANDALONE
            if(simulation && selected) {
                var hand=selected.transform.position+Vector3.up*.1f;
                if(ParkDesktopInput.KeyDown(KeyCode.Alpha1))DispatchDimension(EditDimension.Appearance,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.Alpha2))DispatchDimension(EditDimension.Agency,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.Alpha3))DispatchDimension(EditDimension.Rule,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.Alpha4))DispatchDimension(EditDimension.Deconstruction,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.B))Dispatch(ParkAction.Pull,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.F))Dispatch(ParkAction.Feed,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.O))Dispatch(ParkAction.Poke,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.UpArrow))Dispatch(ParkAction.GrowUp,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.DownArrow))Dispatch(ParkAction.GrowDown,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.Z))Dispatch(ParkAction.Undo,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.R))Dispatch(ParkAction.Reset,hand,true);
                if(ParkDesktopInput.KeyDown(KeyCode.G))Menu("Plane",hand);
                if(ParkDesktopInput.KeyDown(KeyCode.T)&&scenario==ParkScenario.Tree)SetVirtualTreeMode(!selected.magicTree);
                if(ParkDesktopInput.KeyDown(KeyCode.N))SceneManager.LoadScene(SceneNames[((int)scenario+1)%6]);
            }
            if(ParkDesktopInput.ClickDown && !ParkDesktopInput.PointerCapturedByUi && Camera.main) { var ray=Camera.main.ScreenPointToRay(ParkDesktopInput.Pointer); Point(ray.origin,ray.direction,true); }
#endif
        }
        string Hint() => "Same as lighter: pinch-release A | point-circle B | palm-swipe C | fist-open D\nMove hand within 20 cm; UI Fit/Plane owns input while confirming. Undo/Reset: controls.";

        // This is the live recognition path, also replayable with synthetic joint-derived frames.
        public EditDimension ProcessMagicSample(TabletopHandSample frame,bool fresh) {
            LastRecognizedDimension=EditDimension.None;LastMagicAccepted=false;
            if(!fresh||!frame.Valid||rightHandUiCapture||FitConfirmationPending||PlaneConfirmationPending||
                !selected||!selected.tracked) {ResetHandState(false);return EditDimension.None;}
            if(gestureTarget!=selected) {rules.Reset();gestureReach.Reset();gestureTarget=selected;}
            if(requireGestureRelease) {
                rules.Reset();gestureReach.Reset();
                if(!frame.gesture.open||frame.gesture.pinch<TabletopGestureRules.PinchReleaseMeters) {status="Open hand after UI/selection before casting";return EditDimension.None;}
                requireGestureRelease=false;return EditDimension.None;
            }
            var query=frame.ClosestTo(selected.transform.position);
            if(!gestureReach.Observe(true,Vector3.Distance(query,selected.transform.position),frame.gesture.time)) {
                rules.Reset();status="Move hand within 20 cm of selected real target";return EditDimension.None;
            }
            var dimension=rules.Step(frame.gesture);status=rules.Status;
            DataLogger.Instance?.LogHandTrajectory(query,selected.transform.position);
            if(dimension!=EditDimension.None) {
                LastRecognizedDimension=dimension;
                dispatchSource="shared_hand_recognizer";
                try {LastMagicAccepted=DispatchDimension(dimension,query,true);}
                finally {dispatchSource="controller_api";}
            }
            return dimension;
        }
        public EditDimension ProcessHandSample(TabletopHandSample frame,bool fresh) {
            LastRecognizedDimension=EditDimension.None;LastMagicAccepted=false;
            if(!fresh||!frame.Valid||rightHandUiCapture) {ResetHandInteraction();lastHandSampleAt=-1;return EditDimension.None;}
            lastHandSampleAt=frame.gesture.time;
            var index=frame.gesture.index;
            bool pinch=frame.gesture.pinch<=TabletopGestureRules.PinchEnterMeters;
            bool began=pinch&&!pinchHeld;pinchHeld=pinch;fistHeld=frame.gesture.fist;
            if(selected&&!selected.tracked) {ResetHandInteraction();return EditDimension.None;}
            if(selected)selected.Preview(index,true);
            if(began) {
                for(int n=0;n<buttons.Count;n++)if(Vector3.Distance(index,buttons[n].position)<.055f) {
                    Menu(buttonActions[n],index);ResetHandState(false);pinchHeld=true;return EditDimension.None;
                }
                if(FitConfirmationPending) {ConfirmFit(index,true);ResetHandState(false);pinchHeld=true;return EditDimension.None;}
                if(PlaneConfirmationPending) {ConfirmManualPlane(index,true);ResetHandState(false);pinchHeld=true;return EditDimension.None;}
                var previous=selected;Point(index,index-frame.indexBase,true);
                if(selected!=previous) {ResetHandState(false);pinchHeld=true;return EditDimension.None;}
            }
            // Preserve the pinch edge while the user holds it during calibration.
            // ResetHandState here would turn one held pinch into repeated confirmations.
            if(FitConfirmationPending||PlaneConfirmationPending) {
                rules.Reset();gestureReach.Reset();requireGestureRelease=true;return EditDimension.None;
            }
            return ProcessMagicSample(frame,true);
        }
#if XR_HANDS_1_1_OR_NEWER
        void OnUpdatedHands(XRHandSubsystem subsystem,XRHandSubsystem.UpdateSuccessFlags flags,XRHandSubsystem.UpdateType type) {
            if(type!=XRHandSubsystem.UpdateType.Dynamic)return;
            if((flags&XRHandSubsystem.UpdateSuccessFlags.RightHandJoints)==0) {
                ResetHandInteraction();lastHandSampleAt=-1;return;
            }
            var camera=Camera.main;
            bool valid=TabletopHandSample.TryRead(subsystem.rightHand,camera?camera.transform.parent:null,
                camera?camera.transform.right:Vector3.right,Time.unscaledTime,out var frame);
            ProcessHandSample(frame,valid);
        }
#endif
        void OnDisable() {
#if XR_HANDS_1_1_OR_NEWER
            if(hands!=null)hands.updatedHands-=OnUpdatedHands;
            hands=null;
#endif
            Clear(false);
        }
    }
}

