using System;
using System.Collections.Generic;
using Perception;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using UnityEngine.XR.Hands;


namespace MagicMR {
    /// <summary>Shared recognition UI; detection never implies a spatial anchor.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class ParkSharedInterface:MonoBehaviour {
        public ParkController controller;
        public ParkStereoReceiver receiver;
        public Perception.TcpClient transport;
        public TextAsset replay;
        public bool replayMode;
        public float staleAfter=1.5f;
        public DetectionFrameMessage CurrentFrame {get;private set;}
        public string Feedback {get;private set;}="Waiting for recognition";
        static bool enterDiscovery;
        static bool startReplay;
        static bool recordedReplay;
        static bool automaticSwitching=true;
        readonly DiscoverySelection replaySelection=new DiscoverySelection();
        readonly List<GameObject> boxes=new List<GameObject>();
        readonly List<Button> buttons=new List<Button>();
        readonly List<Button> plantSimulationButtons=new List<Button>();
        public static bool DesktopPointerCaptured {get=>ParkDesktopInput.PointerCapturedByUi;private set=>ParkDesktopInput.PointerCapturedByUi=value;}
        Text status;
        RectTransform detectionPanel;
        PicoYoloFrameSender previewSender;
        RawImage matchedPreview;
        Texture2D matchedTexture;
        readonly Dictionary<long,byte[]> sentImages=new Dictionary<long,byte[]>();
        readonly Queue<long> sentImageOrder=new Queue<long>();
        GameObject statusRoot,menuRoot;
        GameObject candidatesRoot;
        readonly List<Button> candidateButtons=new List<Button>();
        string candidateSignature;
        string[] replayLines;
        int replayIndex;
        float receivedAt=-100,nextReplay,nextTouch;
        bool roomAcknowledged;
        bool uiPinchArmed;
        bool rightUiCapture;
        bool leftUiCapture,leftUiPinchArmed;
        Button hoveredButton;
        Color hoverOriginal;
        string actionFeedback;
        float actionFeedbackUntil;

        XRHandSubsystem hands;

        public DiscoverySelection Selection=>replayMode?replaySelection:receiver?receiver.discovery:replaySelection;
        void ClearRecognitionPresentation() {
            CurrentFrame=null;receivedAt=-100;nextReplay=0;
            sentImages.Clear();sentImageOrder.Clear();
            if(matchedPreview) {matchedPreview.enabled=false;matchedPreview.texture=null;}
            if(matchedTexture)Destroy(matchedTexture);matchedTexture=null;
            foreach(var box in boxes)if(box) {box.SetActive(false);Destroy(box);}boxes.Clear();
            foreach(var button in candidateButtons)if(button) {buttons.Remove(button);button.gameObject.SetActive(false);Destroy(button.gameObject);}candidateButtons.Clear();
            candidateSignature=null;
            if(detectionPanel)detectionPanel.gameObject.SetActive(false);
        }
        void OnEnable() { if(receiver) {receiver.RecognitionAccepted+=Receive;receiver.DiscoverySceneRequested+=DiscoveryRoute;}else if(transport)transport.LineReceived+=Receive; }
        void DiscoveryRoute(string profile,string id) { if(profile=="lighter")Route(profile,id); }
        void OnDisable() {
            DesktopPointerCaptured=false;
            if(previewSender)previewSender.EncodedFrameSent-=CacheSentImage;
            sentImages.Clear();sentImageOrder.Clear();
            if(matchedTexture)Destroy(matchedTexture);matchedTexture=null;
            if(controller)controller.SetRightHandUiCapture(false);
            rightUiCapture=false;leftUiCapture=false;uiPinchArmed=false;leftUiPinchArmed=false;
            if(controller)controller.SetLeftHandUiCapture(false);
            if(receiver)receiver.RecognitionAccepted-=Receive;else if(transport)transport.LineReceived-=Receive;
            if(receiver)receiver.DiscoverySceneRequested-=DiscoveryRoute;
            if(statusRoot)Destroy(statusRoot);if(menuRoot)Destroy(menuRoot);
            if(candidatesRoot)Destroy(candidatesRoot);candidateButtons.Clear();
            if(detectionPanel)Destroy(detectionPanel.gameObject);
            CurrentFrame=null;boxes.Clear();buttons.Clear();
        }
        void Start() {
            if(!Camera.main)return;
            statusRoot=Panel("Shared status",new Vector3(-.5f,.12f,1.2f),new Vector2(470,300));
            // Main TargetLockDebugUI uses yellow 24px status; PicoCameraPreviewUI uses black .65 background.
            status=Label(statusRoot.transform,"Status",new Vector2(440,270),24);status.color=Color.yellow;
            status.resizeTextForBestFit=true;status.resizeTextMinSize=16;status.resizeTextMaxSize=24;
            menuRoot=Panel("Shared scene menu",new Vector3(-.5f,-.24f,1.2f),new Vector2(470,420));
            string[] commands={"Entry","Lighter","Tree","Bugs","Statue","Plant","Pet","Room","Auto","Reset","Virtual test","Replay","Confirm room","Fit","Plane","Stop"};
            for(int i=0;i<commands.Length;i++) {
                string command=commands[i];
                AddButton(menuRoot.transform,command,new Vector2((i%3-1)*150,170-i/3*65),()=>Command(command));
            }
            if(controller)foreach(string test in new[]{"Test grow","Test bloom"}) {
                string command=test;AddButton(menuRoot.transform,command,new Vector2(plantSimulationButtons.Count*150,-155),()=>Command(command));
                var button=buttons[buttons.Count-1];button.gameObject.SetActive(false);plantSimulationButtons.Add(button);
            }
            detectionPanel=Panel("Shared YOLO detection",new Vector3(.12f,.1f,1.2f),new Vector2(640,480)).GetComponent<RectTransform>();
            var previewObject=new GameObject("Exact sent frame preview",typeof(RectTransform),typeof(RawImage));
            previewObject.transform.SetParent(detectionPanel,false);previewObject.transform.SetAsFirstSibling();
            var previewRect=previewObject.GetComponent<RectTransform>();previewRect.sizeDelta=new Vector2(640,480);
            matchedPreview=previewObject.GetComponent<RawImage>();matchedPreview.raycastTarget=false;matchedPreview.enabled=false;
            previewSender=receiver?receiver.sender:FindFirstObjectByType<PicoYoloFrameSender>();
            if(previewSender)previewSender.EncodedFrameSent+=CacheSentImage;
            candidatesRoot=Panel("Choose recognized target",new Vector3(-.5f,-.57f,1.2f),new Vector2(470,180));
            if(receiver && enterDiscovery) { receiver.discoveryMode=true;enterDiscovery=false; }
            if(recordedReplay)replay=Resources.Load<TextAsset>("MagicMR/recorded-recognition-replay.jsonl");
            if(replay)replayLines=replay.text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);
            Selection.automatic=automaticSwitching;
            if(controller && startReplay) {startReplay=false;Command("Replay");}
        }
        GameObject Panel(string name,Vector3 position,Vector2 size) {
            var go=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(Image));
            go.transform.SetParent(Camera.main.transform,false);
            var rt=go.GetComponent<RectTransform>();rt.sizeDelta=size;rt.localPosition=position;rt.localScale=Vector3.one*.001f;
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=Camera.main;
            go.GetComponent<Image>().color=new Color(0,0,0,.65f);return go;
        }
        Text Label(Transform parent,string name,Vector2 size,int fontSize) {
            var text=new GameObject(name,typeof(RectTransform),typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent,false);text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color=Color.white;text.fontSize=fontSize;text.alignment=TextAnchor.UpperLeft;
            text.rectTransform.sizeDelta=size;text.raycastTarget=false;return text;
        }
        void AddButton(Transform parent,string caption,Vector2 position,Action action) {
            var go=new GameObject(caption,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>();rt.sizeDelta=new Vector2(140,55);rt.anchoredPosition=position;
            go.GetComponent<Image>().color=new Color(.08f,.16f,.24f,.95f);
            var button=go.GetComponent<Button>();button.onClick.AddListener(()=>action());buttons.Add(button);
            var label=Label(go.transform,"Label",rt.sizeDelta,19);label.alignment=TextAnchor.MiddleCenter;label.text=caption;
        }
        void Receive(string json) { if(!replayMode)ApplyJson(json,false); }
        public void ApplyJson(string json,bool simulated) {
            DetectionFrameMessage frame;
            try { frame=JsonUtility.FromJson<DetectionFrameMessage>(json); }catch { Feedback="Invalid recognition JSON";return; }
            if(frame==null || frame.width<=0 || frame.height<=0 || frame.width>8192 || frame.height>8192) { Feedback="Invalid image dimensions";return; }
            if(CurrentFrame!=null && !simulated && frame.frame_id<=CurrentFrame.frame_id && Time.unscaledTime-receivedAt<=staleAfter)return;
            CurrentFrame=frame;receivedAt=Time.unscaledTime-(!simulated && receiver?receiver.AcceptedResultAge:0);
            Feedback=frame.status=="OK"?"Recognition received; location is separate":frame.status;
            if(!simulated && (!receiver || !receiver.discoveryMode)) {
                var candidates=CandidateFrame(frame);
                bool automatic=Selection.automatic;Selection.automatic=false;
                Selection.Update(candidates,Time.unscaledTime,controller && controller.AnimationBusy);Selection.automatic=automatic;
            }
            if(simulated) {
                {
                    var route=replaySelection.Update(CandidateFrame(frame),Time.unscaledTime,controller && controller.AnimationBusy);
                    if(route!=null)Route(route,replaySelection.LockedId);
                }
            }
            BindMatchedImage(frame,simulated);
            DrawDetections(frame);
        }
        static DetectionFrameMessage CandidateFrame(DetectionFrameMessage frame) {
            var candidates=new DetectionFrameMessage {status=frame.status,scene="discovery",frame_id=frame.frame_id,width=frame.width,height=frame.height,detections=frame.detections,weights=frame.weights,model_validation=frame.model_validation};
            if(frame.scene!="discovery")foreach(var d in candidates.detections??Array.Empty<DetectionMessage>())if(d!=null)d.target_scene=frame.scene;
            return candidates;
        }
        void CacheSentImage(uint id,byte[] jpeg) {
            if(replayMode || jpeg==null || jpeg.Length>2*1024*1024)return;
            sentImages[id]=jpeg;sentImageOrder.Enqueue(id);
            while(sentImageOrder.Count>4)sentImages.Remove(sentImageOrder.Dequeue());
        }
        void BindMatchedImage(DetectionFrameMessage frame,bool simulated) {
            if(!matchedPreview)return;
            matchedPreview.enabled=false;matchedPreview.texture=null;
            byte[] jpeg;
            if(simulated) {
                if(string.IsNullOrEmpty(frame.replay_image_base64) || frame.replay_image_base64.Length>2800000)return;
                try {jpeg=Convert.FromBase64String(frame.replay_image_base64);}catch(FormatException) {return;}
            } else if(!sentImages.TryGetValue(frame.frame_id,out jpeg))return;
            if(!matchedTexture)matchedTexture=new Texture2D(2,2,TextureFormat.RGB24,false);
            if(!matchedTexture.LoadImage(jpeg) || matchedTexture.width!=frame.width || matchedTexture.height!=frame.height)return;
            matchedPreview.texture=matchedTexture;matchedPreview.enabled=true;
        }
        void DrawDetections(DetectionFrameMessage frame) {
            foreach(var go in boxes)if(go)Destroy(go);boxes.Clear();
            if(!detectionPanel)return;
            foreach(var d in frame.detections??Array.Empty<DetectionMessage>()) {
                if(d==null||!float.IsFinite(d.x1)||!float.IsFinite(d.y1)||!float.IsFinite(d.x2)||!float.IsFinite(d.y2)||!float.IsFinite(d.confidence)||d.confidence<0||d.confidence>1||d.x1<0||d.y1<0||d.x2>frame.width||d.y2>frame.height||d.x2<=d.x1||d.y2<=d.y1)continue;
                var go=new GameObject("Detection "+d.instance_id,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(detectionPanel,false);boxes.Add(go);
                var rt=go.GetComponent<RectTransform>();rt.sizeDelta=new Vector2((d.x2-d.x1)*640/frame.width,(d.y2-d.y1)*480/frame.height);
                rt.anchoredPosition=new Vector2((d.x1+d.x2)*320/frame.width-320,240-(d.y1+d.y2)*240/frame.height);
                go.GetComponent<Image>().color=new Color(1,.85f,0,.12f);
                Border(rt);
                var text=Label(go.transform,"Class",new Vector2(Mathf.Max(rt.sizeDelta.x,180),28),18);text.rectTransform.anchoredPosition=new Vector2(0,rt.sizeDelta.y/2+14);
                text.text=(string.IsNullOrEmpty(d.class_name)?d.@class:d.class_name)+" "+d.confidence.ToString("P0")+" "+d.instance_id;
                string id=d.instance_id;go.GetComponent<Button>().onClick.AddListener(()=>Choose(id));
            }
        }
        void Border(RectTransform parent) {
            for(int i=0;i<4;i++) {
                var edge=new GameObject("Box edge",typeof(RectTransform),typeof(Image));edge.transform.SetParent(parent,false);
                var rect=edge.GetComponent<RectTransform>();bool horizontal=i<2;
                rect.sizeDelta=horizontal?new Vector2(parent.sizeDelta.x,2):new Vector2(2,parent.sizeDelta.y);
                rect.anchoredPosition=horizontal?new Vector2(0,(i==0?1:-1)*parent.sizeDelta.y/2):new Vector2((i==2?1:-1)*parent.sizeDelta.x/2,0);
                edge.GetComponent<Image>().color=Color.yellow;edge.GetComponent<Image>().raycastTarget=false;
            }
        }
        void RefreshCandidates() {
            if(!candidatesRoot)return;
            string signature=string.Join("|",Selection.Candidates.ConvertAll(d=>d.instance_id+":"+d.confidence.ToString("P0")));
            if(signature==candidateSignature)return;candidateSignature=signature;
            foreach(var button in candidateButtons)if(button) {buttons.Remove(button);Destroy(button.gameObject);}candidateButtons.Clear();
            for(int i=0;i<Mathf.Min(8,Selection.Candidates.Count);i++) {
                var d=Selection.Candidates[i];string id=d.instance_id;
                string caption=(string.IsNullOrEmpty(d.class_name)?d.@class:d.class_name)+" "+d.confidence.ToString("P0")+"\n"+id;
                AddButton(candidatesRoot.transform,caption,new Vector2((i%2==0?-1:1)*115,65-i/2*42),()=>Choose(id));
                var button=buttons[buttons.Count-1];button.GetComponent<RectTransform>().sizeDelta=new Vector2(220,38);
                var label=button.GetComponentInChildren<Text>();label.rectTransform.sizeDelta=new Vector2(218,38);label.fontSize=15;candidateButtons.Add(button);
            }
            candidatesRoot.SetActive(Selection.Candidates.Count>0);
        }
        void ShowActionFeedback() {actionFeedback=Feedback;actionFeedbackUntil=Time.unscaledTime+3;}
        public void Choose(string id) {ExecuteChoice(id);ShowActionFeedback();}
        void ExecuteChoice(string id) {
            if(!replayMode && (!transport || !transport.IsConnected)) {Feedback="Disconnected: live candidate selection unavailable";return;}
            if(Time.unscaledTime-receivedAt>staleAfter) { Feedback="Candidate expired";return; }
            var route=Selection.Choose(id,Time.unscaledTime,controller && controller.AnimationBusy);
            if(route!=null)Route(route,id);else Feedback="Candidate unavailable, cooldown, or animation busy";
        }
        void Route(string profile,string id) {
            if(profile=="room" && !roomAcknowledged) { Feedback="Confirm room space manually first";return; }
            if(profile=="lighter") {
                if(!controller) { Feedback="Selected lighter; main tracking and calibration remain active";return; }
                SceneManager.LoadScene("BridgeStereoFusion");return;
            }
            if(controller && controller.SwitchContent(profile,id)) {
                Feedback="Selected "+profile+"; waiting for location";
                if(replayMode) {
                    var candidate=Selection.Candidates.Find(d=>d.instance_id==id);
                    string recognized=candidate==null?null:string.IsNullOrWhiteSpace(candidate.class_name)?candidate.@class:candidate.class_name;
                    controller.simulation=true;controller.SpawnVirtual(recognized);Feedback="REPLAY + VIRTUAL ANIMATION: no physical location";
                }
            }
        }
        public void Command(string command) {ExecuteCommand(command);ShowActionFeedback();}
        void ExecuteCommand(string command) {
            if(command=="Test grow" || command=="Test bloom") {
                if(!controller || !controller.simulation || controller.scenario!=ParkScenario.Plant || !controller.selected) {Feedback="Plant animation tests require a selected VIRTUAL SIMULATION target";return;}
                bool accepted=controller.Dispatch(command=="Test grow"?ParkAction.GrowUp:ParkAction.Bloom,controller.selected.transform.position,true);
                Feedback=accepted?"SIMULATED plant animation input; no gesture or location measurement":"SIMULATION: "+controller.InteractionStatus;return;
            }
            if(command=="Replay" && !controller) {startReplay=true;enterDiscovery=true;SceneManager.LoadScene("ParkTree");return;}
            if(command=="Auto") { automaticSwitching=!Selection.automatic;Selection.automatic=automaticSwitching;Feedback="Automatic switching "+(automaticSwitching?"ON":"OFF");return; }
            if(command=="Confirm room") { roomAcknowledged=true;Feedback="Manual room acknowledgement; no reconstruction claim";return; }
            if(command=="Entry") {
                if(controller) { controller.ResetScene();controller.recognitionLock=null;Selection.Return(Time.unscaledTime);if(receiver)receiver.discoveryMode=true; }
                else { enterDiscovery=true;SceneManager.LoadScene("ParkTree"); }return;
            }
            if(command=="Reset") { if(controller) {controller.ResetScene();controller.recognitionLock=null;}else { var reset=FindFirstObjectByType<StudyResetWristUi>();if(reset)reset.TriggerReset(); } Selection.Return(Time.unscaledTime);return; }
            if(command=="Fit") { if(controller)controller.RequestFit();else Feedback="Lighter: use its existing calibration gesture";return; }
            if(command=="Stop") { if(controller && controller.selected)controller.Dispatch(ParkAction.Stop,controller.selected.transform.position,true);return; }
            if(command=="Plane") { if(controller)controller.RequestManualPlane();else Feedback="Select a park scene for manual surface confirmation";return; }
            if(command=="Virtual test") { if(controller) { controller.ResetScene();controller.simulation=!controller.simulation;if(controller.simulation)controller.SpawnVirtual(); }else Feedback="Select a park scene to preview its own animations";return; }
            if(command=="Replay") { ClearRecognitionPresentation();replayMode=!replayMode;if(receiver) {receiver.discovery.Return(Time.unscaledTime);receiver.enabled=!replayMode;}if(controller) {controller.ResetScene();controller.recognitionLock=null;controller.simulation=replayMode;}replayIndex=0;replaySelection.ResetSession(Time.unscaledTime);Selection.automatic=automaticSwitching;Feedback=replayMode?"REPLAY: JSON frames; no physical location":"LIVE TCP";return; }
            if(command=="Recorded replay") {
                var recording=Resources.Load<TextAsset>("MagicMR/recorded-recognition-images.jsonl")??Resources.Load<TextAsset>("MagicMR/recorded-recognition-replay.jsonl");
                if(!recording) {Feedback="No recorded recognition resource; export offline inference first";return;}
                recordedReplay=true;replay=recording;
                replayLines=replay.text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);
                if(replayMode)Command("Replay");Command("Replay");return;
            }
            string profile=command.ToLowerInvariant();
            if(profile=="room" && !roomAcknowledged) { Feedback="Use Confirm room before entering Room";return; }
            if(controller && controller.AnimationBusy) { Feedback="Wait for animation or Reset";return; }
            if(profile=="lighter") { SceneManager.LoadScene("BridgeStereoFusion");return; }
            if(controller) { Selection.Return(Time.unscaledTime);if(receiver)receiver.discoveryMode=false;Route(profile,null); }
            else SceneManager.LoadScene("Park"+command);
        }
        void Update() {
            foreach(var button in plantSimulationButtons)if(button)button.gameObject.SetActive(controller && controller.simulation && controller.scenario==ParkScenario.Plant);
            Selection.Tick(Time.unscaledTime,controller && controller.AnimationBusy);
            RefreshCandidates();
            if(replayMode && replayLines!=null && replayLines.Length>0 && Time.unscaledTime>=nextReplay) {
                nextReplay=Time.unscaledTime+.4f;if(replayIndex<replayLines.Length)ApplyJson(replayLines[replayIndex++],true);
            }
            bool fresh=Time.unscaledTime-receivedAt<=staleAfter && (replayMode || (transport && transport.IsConnected));
            bool simulated=replayMode || (controller && controller.simulation);
            if(status)status.text=(replayMode?"REPLAY / SIMULATION":simulated?"VIRTUAL SIMULATION":"LIVE TCP")+" | "+(controller?controller.Profile:"lighter")+
                "\nConnection: "+(transport && transport.IsConnected?"connected":"disconnected")+
                "\nTarget class: "+(controller?controller.TargetClasses:"Lighter")+
                "\nRecognition: "+(fresh?Feedback:"stale / waiting")+
                "\nImage: "+(simulated?(matchedPreview && matchedPreview.enabled?"source JPEG replay":"simulation, no image"):fresh && matchedPreview && matchedPreview.enabled?"exact sent frame":"matching frame unavailable")+
                "\nLocation: "+(simulated?"NOT MEASURED":receiver?receiver.Status:LighterLocation())+
                "\nTargets: "+Selection.Candidates.Count+" | Selected: "+(Selection.LockedId??(controller && controller.selected?controller.selected.instanceId:"none"))+
                (controller && controller.scenario==ParkScenario.Bugs && controller.selected?" | Virtual score: "+controller.selected.VirtualScore:"")+
                "\nAuto: "+Selection.automatic+" | "+ShortModelStatus(CurrentFrame?.model_validation)+
                "\n"+ParkHandTracking.Status+(rightUiCapture?" | R: UI":"")+(leftUiCapture?" | L: UI":"")+" | Gestures: device validation pending"+
                (controller?"\n"+controller.MotionGestureStatus+" | "+CompactStatus(controller.InteractionStatus):"")+
                "\n"+(Time.unscaledTime<actionFeedbackUntil?actionFeedback:controller?controller.GestureHint:"Main lighter gestures + wrist Reset");
            if(!fresh && detectionPanel)detectionPanel.gameObject.SetActive(false);else if(detectionPanel)detectionPanel.gameObject.SetActive(true);

            hands=ParkHandTracking.Current;
            Button rightTarget=null,leftTarget=null;
            if(hands!=null) {
                rightTarget=ProcessUiHand(hands.rightHand,ref rightUiCapture,ref uiPinchArmed);
                leftTarget=ProcessUiHand(hands.leftHand,ref leftUiCapture,ref leftUiPinchArmed);
            } else {rightUiCapture=leftUiCapture=uiPinchArmed=leftUiPinchArmed=false;}
            if(controller) {
                controller.SetRightHandUiCapture(rightUiCapture);
                controller.SetLeftHandUiCapture(leftUiCapture);
            }
            // Right hand wins simultaneous hover/click contention; both keep ownership.
            Button desktopTarget=null;
#if UNITY_EDITOR || UNITY_STANDALONE
            desktopTarget=ProcessDesktopPointer();
#endif
            SetHover(rightTarget?rightTarget:leftTarget?leftTarget:desktopTarget);
        }
        Button ProcessUiHand(XRHand hand,ref bool captured,ref bool armed) {
            if(!hand.isTracked || !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index) ||
                !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb) ||
                !FiniteUiPoint(index.position) || !FiniteUiPoint(thumb.position) || !Camera.main) {captured=false;armed=false;return null;}
            var origin=Camera.main.transform.parent;
            Vector3 world=origin?origin.TransformPoint(index.position):index.position;
            if(!FiniteUiPoint(world)) {captured=false;armed=false;return null;}
            bool rayValid=hand.GetJoint(XRHandJointID.IndexMetacarpal).TryGetPose(out var metacarpal) &&
                hand.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out var proximal);
            Ray ray=default;
            if(rayValid) {
                // Read again to keep definite assignment valid for short-circuit joint lookup.
                hand.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out proximal);
                Vector3 direction=proximal.position-metacarpal.position;rayValid=FiniteUiPoint(proximal.position) && FiniteUiPoint(metacarpal.position) && direction.sqrMagnitude>.000025f;
                ray=new Ray(origin?origin.TransformPoint(proximal.position):proximal.position,origin?origin.TransformDirection(direction.normalized):direction.normalized);
            }
            Button target=null;float nearest=float.MaxValue;
            foreach(var button in buttons)ConsiderButton(button,world,ray,rayValid,ref target,ref nearest);
            foreach(var box in boxes)if(box)ConsiderButton(box.GetComponent<Button>(),world,ray,rayValid,ref target,ref nearest);
            float pinch=Vector3.Distance(index.position,thumb.position);
            if(pinch>.045f)captured=false;
            if(target)captured=true;
            // Apply ownership before a click invokes a scene command.
            if(controller) {
                if(hand.handedness==UnityEngine.XR.Hands.Handedness.Right)controller.SetRightHandUiCapture(captured);
                else controller.SetLeftHandUiCapture(captured);
            }
            if(pinch>.045f)armed=true;
            if(pinch<.035f && armed) {
                armed=false;
                if(target && Time.unscaledTime>=nextTouch) {nextTouch=Time.unscaledTime+.7f;target.onClick.Invoke();}
            }
            return target;
        }
        static bool FiniteUiPoint(Vector3 point)=>float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);
        public static bool TryButtonRayHit(RectTransform rect,Ray ray,out float distance) {
            distance=0;if(!rect || !rect.gameObject.activeInHierarchy)return false;
            var plane=new Plane(rect.forward,rect.position);
            return plane.Raycast(ray,out distance) && float.IsFinite(distance) && distance>=0 && distance<=3 && rect.rect.Contains(rect.InverseTransformPoint(ray.GetPoint(distance)));
        }
        static string CompactStatus(string value) {
            if(string.IsNullOrEmpty(value))return "waiting";
            value=value.Replace('\r',' ').Replace('\n',' ');
            return value.Length>100?value.Substring(0,97)+"...":value;
        }
        static string ShortModelStatus(string marker) {
            if(string.IsNullOrEmpty(marker))return "model pending";
            if(marker.StartsWith("REJECTED",StringComparison.OrdinalIgnoreCase))return "REJECTED / research only";
            if(marker.IndexOf("PENDING",StringComparison.OrdinalIgnoreCase)>=0)return "PENDING / PICO unverified";
            return marker.Length>40?marker.Substring(0,37)+"...":marker;
        }
        void ConsiderButton(Button button,Vector3 finger,Ray ray,bool rayValid,ref Button target,ref float nearest) {
            if(!button || !button.gameObject.activeInHierarchy || !button.interactable)return;
            var rect=button.GetComponent<RectTransform>();var local=rect.InverseTransformPoint(finger);
            float distance;
            if(rect.rect.Contains(local) && Mathf.Abs(local.z)<45)distance=0;
            else if(!rayValid || !TryButtonRayHit(rect,ray,out distance))return;
            if(distance<nearest) {nearest=distance;target=button;}
        }
        void SetHover(Button target) {
            if(hoveredButton==target)return;
            if(hoveredButton && hoveredButton.image)hoveredButton.image.color=hoverOriginal;
            hoveredButton=target;
            if(target && target.image) {hoverOriginal=target.image.color;target.image.color=new Color(.3f,.65f,.75f,.95f);}
        }
        string LighterLocation() {var locator=FindFirstObjectByType<Perception.StereoYoloLocator>();return locator && locator.TargetLocated?"LOCATED":"NOT LOCATED";}
#if UNITY_EDITOR || UNITY_STANDALONE
        Button ProcessDesktopPointer() {
            DesktopPointerCaptured=false;
            if(!Camera.main)return null;
            Ray ray=Camera.main.ScreenPointToRay(ParkDesktopInput.Pointer);Button target=null;float nearest=float.MaxValue;
            foreach(var button in buttons) {
                if(!button || !button.interactable)continue;
                if(TryButtonRayHit(button.GetComponent<RectTransform>(),ray,out float distance) && distance<nearest) {target=button;nearest=distance;}
            }
            foreach(var box in boxes)if(box) {
                var button=box.GetComponent<Button>();if(!button || !button.interactable)continue;
                if(TryButtonRayHit(button.GetComponent<RectTransform>(),ray,out float distance) && distance<nearest) {target=button;nearest=distance;}
            }
            DesktopPointerCaptured=target;
            if(target && ParkDesktopInput.ClickDown && Time.unscaledTime>=nextTouch) {nextTouch=Time.unscaledTime+.7f;target.onClick.Invoke();}
            return target;
        }
#endif
    }
}

