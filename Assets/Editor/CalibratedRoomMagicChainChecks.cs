using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MagicMR;
using Perception;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

[InitializeOnLoad]
public static partial class CalibratedRoomMagicChainChecks
{
    const string Running = "MagicMR.CalibratedRoomMagicChain";
    const string PlantRun = Running + ".Plant";
    const string VariantRun = Running + ".Variants";
    const string BridgeRun = Running + ".Bridge";
    const float HandStep = .03f;
    static bool Plant => SessionState.GetBool(PlantRun, false);
    static bool Variants => SessionState.GetBool(VariantRun, false);
    static bool FromBridge => SessionState.GetBool(BridgeRun, false);
    static string Input => Path.GetFullPath(Plant ? ".codex-tmp/park-plant-chain-fixture" : ".codex-tmp/park-room-chain-fixture");
    static string Output => Path.GetFullPath(Plant ? ".codex-tmp/park-plant-chain-checks" :
        Variants ? ".codex-tmp/park-room-variants-checks" :
        FromBridge ? ".codex-tmp/bridge-room-chain-checks" : ".codex-tmp/park-room-chain-checks");
    static ParkController controller;
    static ParkStereoReceiver receiver;
    static ParkActor target;
    static GameObject roomDetectionPanel;
    static string[] replies;
    static ReplayConfig config;
    static int replyIndex, recognized, spellIndex, spellEvents, handIndex, errors;
    static int recoveryReplyCount = -1;
    static bool capturedPalm, capturedRelease;
    static readonly Dictionary<string, Texture2D> stagedPhotoCaptures = new();
    static bool initialized, selectedByPinch, withholdingReplies;
    static bool recoveryCasting;
    static bool needsRoomWall;
    static Vector3 wallWindowPosition;
    static Quaternion wallWindowRotation;
    static PointFlowerMorph pointFlower;
    static ParkSharedInterface plantUi;
    static TcpListener loopback;
    static System.Net.Sockets.TcpClient loopbackPeer;
    static string pendingId;
    static int recognizedBeforePending;
    static float entryStarted = -1;
    static bool bridgeRoomRequested;
    static float nextReply, nextHand, readyAt, started;
    static float previousHand = -1;
    static bool lastWindowRequest;
    static Vector3 anchor;
    static float maximumMouth;
    static TabletopHandSample[] handTrace;
    static TabletopHandSample heldHand;
    static readonly List<string> transitions = new List<string>();
    static readonly EditDimension[] Spells =
        { EditDimension.Appearance, EditDimension.Agency, EditDimension.Rule, EditDimension.Deconstruction };

    [Serializable]
    sealed class ReplayConfig
    {
        public float[] intrinsics, hostSeconds;
        public float baselineMetres;
        public string targetId;
    }

    static CalibratedRoomMagicChainChecks()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += CountErrors;
    }

    static void CountErrors(string message, string trace, LogType type)
    {
        if (SessionState.GetBool(Running, false) && (type == LogType.Error || type == LogType.Assert || type == LogType.Exception))
            errors++;
    }

    public static void Run() => Begin(false);
    public static void RunPlant() => Begin(true);
    public static void RunRoomVariants() => Begin(false, true);
    public static void RunFromBridge()
    {
        ParkRegressionChecks.RunRecorder();
        Begin(false, fromBridge:true);
    }

    static void Begin(bool plant, bool variants = false, bool fromBridge = false)
    {
        SessionState.SetBool(PlantRun, plant);
        SessionState.SetBool(VariantRun, variants);
        SessionState.SetBool(BridgeRun, fromBridge);
        if (!Directory.Exists(Input) || Directory.Exists(Output))
            throw new InvalidOperationException("Expected an existing replay fixture and an empty output path.");
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "result.txt"), "RUNNING");
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene(fromBridge ? "Assets/Scenes/BridgeStereoFusion.unity" :
            plant ? "Assets/Scenes/ParkPlant.unity" : "Assets/Scenes/ParkRoom.unity");
        EditorApplication.EnterPlaymode();
    }

    static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
    }

    static void Finish(int code, string message)
    {
        foreach (var capture in stagedPhotoCaptures)
        {
            File.WriteAllBytes(Path.Combine(Output,capture.Key),capture.Value.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(capture.Value);
        }
        stagedPhotoCaptures.Clear();
        File.WriteAllLines(Path.Combine(Output, "trace.txt"), transitions);
        File.WriteAllText(Path.Combine(Output, "result.txt"), message);
        SessionState.SetBool(Running, false);
        if (receiver && FromBridge) receiver.transport.LineReceived -= receiver.Receive;
        loopbackPeer?.Dispose();
        loopback?.Stop();
        EditorApplication.Exit(code);
    }

    static bool EnterRoomFromBridge()
    {
        if (entryStarted < 0) entryStarted = Time.unscaledTime;
        Require(Time.unscaledTime-entryStarted < 10, "The saved Bridge room menu did not load ParkRoom.");
        if (bridgeRoomRequested) return SceneManager.GetActiveScene().name == "ParkRoom";
        var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var room = buttons.SingleOrDefault(button => button.name == "Room");
        var confirm = buttons.SingleOrDefault(button => button.name == "Confirm room");
        if (!room || !confirm) return false;
        var ui = UnityEngine.Object.FindFirstObjectByType<ParkSharedInterface>();
        Require(SceneManager.GetActiveScene().name == "BridgeStereoFusion" && ui && !ui.controller &&
                UnityEngine.Object.FindFirstObjectByType<StereoYoloLocator>(),
            "Expected the existing Bridge capture and recognition entry.");
        CheckXrCamera();
        room.onClick.Invoke();
        Require(ui.Feedback.Contains("Confirm room") && SceneManager.GetActiveScene().name == "BridgeStereoFusion",
            "The saved Room button bypassed the room acknowledgement.");
        confirm.onClick.Invoke();
        room.onClick.Invoke();
        bridgeRoomRequested = true;
        transitions.Add("Saved Bridge Room button required Confirm room, then routed into ParkRoom.");
        return false;
    }

    static void Initialize()
    {
        controller = UnityEngine.Object.FindFirstObjectByType<ParkController>();
        Require(controller && controller.scenario == (Plant ? ParkScenario.Plant : ParkScenario.Room), "Wrong saved scene controller.");
        receiver = controller.receiver as ParkStereoReceiver;
        Require(receiver && receiver.sender && receiver.transport && receiver.sender.FlipVerticallyBeforeEncode,
            "Expected saved room receiver and matching byte-row convention.");
        // Only the hardware loop is disabled. All observed positions arrive via Receive.
        receiver.enabled = false;
        receiver.discoveryMode = false;
        controller.simulation = false;
        receiver.RecognitionAccepted += _ => recognized++;
        if (FromBridge)
        {
            CheckXrCamera();
            var ui = UnityEngine.Object.FindFirstObjectByType<ParkSharedInterface>();
            Require(ui && ui.controller == controller && ui.receiver == receiver && ui.transport == receiver.transport,
                "ParkRoom lost the shared menu/receiver/transport binding.");
            var detectionPanel = Camera.main.transform.Find("Shared YOLO detection");
            Require(detectionPanel, "ParkRoom did not create the normal detection preview panel.");
            roomDetectionPanel = detectionPanel.gameObject;
            var transport = receiver.transport;
            transport.LineReceived += receiver.Receive;
            transport.AutoConnect = false;
            transport.DisconnectSocket();
            SetTransportField(transport, "m_AutoReconnect", false);
            SetTransportField(transport, "m_Host", IPAddress.Loopback.ToString());
            loopback = new TcpListener(IPAddress.Loopback, 0);
            loopback.Start(1);
            SetTransportField(transport, "m_Port", ((IPEndPoint)loopback.LocalEndpoint).Port);
            transport.ConnectIfNeeded();
        }
        config = JsonUtility.FromJson<ReplayConfig>(File.ReadAllText(Path.Combine(Input, "input.json")));
        replies = Enumerable.Range(1, config.hostSeconds.Length)
            .Select(index => File.ReadAllText(Path.Combine(Input, $"reply-{index}.json"))).ToArray();
        var k = config.intrinsics;
        CalibratedParkReplayChecks.Set(receiver, "left", File.ReadAllBytes(Path.Combine(Input, "left.gray")));
        CalibratedParkReplayChecks.Set(receiver, "right", File.ReadAllBytes(Path.Combine(Input, "right.gray")));
        CalibratedParkReplayChecks.Set(receiver, "intrinsics", new Vector4(k[0], k[1], k[2], k[3]));
        CalibratedParkReplayChecks.Set(receiver, "baseline", config.baselineMetres);
        CalibratedParkReplayChecks.Set(receiver, "capturePose", new Pose(new Vector3(.12f,-.04f,.3f), Quaternion.Euler(0,20,0)));
        CalibratedParkReplayChecks.Set(receiver, "cameraPose", new Pose(new Vector3(.03f,.01f,0), Quaternion.identity));
        replyIndex = 1; // Cold request excluded; use the measured warm ages.
        started = nextReply = Time.unscaledTime;
        initialized = true;
    }

    static void SetTransportField(Perception.TcpClient transport, string name, object value)
    {
        var field = typeof(Perception.TcpClient).GetField(name,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Require(field != null, "Transport test field missing: " + name);
        field.SetValue(transport, value);
    }

    static void PumpRecognition(float now)
    {
        if (pendingId != null)
        {
            if (recognized == recognizedBeforePending) return;
            CheckRecognizedActor(pendingId, recognizedBeforePending);
            pendingId = null;
        }
        if (withholdingReplies || now < nextReply) return;
        if (FromBridge && loopbackPeer == null)
        {
            if (!receiver.transport.IsConnected || !loopback.Pending()) return;
            loopbackPeer = loopback.AcceptTcpClient();
            loopbackPeer.NoDelay = true;
            loopbackPeer.SendTimeout = 500;
            transitions.Add("ParkRoom received original PICF lines through the saved TCP component over local loopback.");
        }
        Require(replyIndex < replies.Length, "Exhausted recorded PICF responses.");
        float age = config.hostSeconds[replyIndex];
        Require(age > 0 && age < receiver.maxAge, "A recorded reply missed the spatial freshness gate.");
        var reply = JsonUtility.FromJson<DetectionFrameMessage>(replies[replyIndex]);
        Require(reply.detections.Length == (Plant ? 2 : 1) &&
                reply.detections.All(d => d.class_name == (Plant ? "PottedPlant" : "chair")), "Unexpected routed candidates.");
        string id = Plant ? config.targetId : reply.detections[0].instance_id;
        Require(reply.detections.Count(d => d.instance_id == id) == 1, "Requested detector instance missing.");
        if (target) Require(target.instanceId == id, "Detection changed target ID within one replay session.");
        if (Plant && replyIndex == 1)
            RecordPlantCandidates(reply);
        int before = recognized;
        string raw = replies[replyIndex++];
        if (FromBridge)
        {
            CalibratedParkReplayChecks.Set(receiver, "waiting", true);
            CalibratedParkReplayChecks.Set(receiver, "expected", reply.frame_id);
            CalibratedParkReplayChecks.Set(receiver, "capturedAt", Time.realtimeSinceStartup-age);
            CalibratedParkReplayChecks.Set(receiver, "capturePoseValid", true);
            CalibratedParkReplayChecks.Set(receiver, "headTracked", true);
            pendingId = id;
            recognizedBeforePending = before;
            byte[] line = Encoding.UTF8.GetBytes(raw+"\n");
            loopbackPeer.GetStream().Write(line,0,line.Length);
        }
        else
        {
            CalibratedParkReplayChecks.Supply(receiver, raw, age);
            CheckRecognizedActor(id, before);
        }
        nextReply = now + .14f;
    }

    static void CheckRecognizedActor(string id, int before)
    {
        Require(receiver.Status.StartsWith("TRANSLATION_ONLY") && recognized == before + 1,
            "An actual replay frame failed to refresh the room observation: " + receiver.Status);
        if (!target)
            target = UnityEngine.Object.FindObjectsByType<ParkActor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .SingleOrDefault(a => a.instanceId == id);
        Require(target && !target.virtualTarget, "The requested detector instance did not become a spatial actor.");
    }

    static void RecordPlantCandidates(DetectionFrameMessage reply)
    {
        var left = File.ReadAllBytes(Path.Combine(Input, "left.gray"));
        var right = File.ReadAllBytes(Path.Combine(Input, "right.gray"));
        foreach (var d in reply.detections)
        {
            int u=Mathf.RoundToInt((d.x1+d.x2)*.5f),v=Mathf.RoundToInt((d.y1+d.y2)*.5f);
            int sx=Mathf.Min(24,Mathf.FloorToInt((d.x2-d.x1)*.3f)-5),sy=Mathf.Min(16,Mathf.FloorToInt((d.y2-d.y1)*.3f)-4);
            var depth=StereoPatchMatcher.EstimateDepth(left,right,640,480,u,v,config.intrinsics[0],config.baselineMetres,sx,sy);
            transitions.Add(FormattableString.Invariant($"Plant candidate {d.instance_id}: confidence={d.confidence:R}, inliers={depth.acceptedSamples}/9, metres={depth.depthMeters:R}, quality={depth.confidence:R}, reason={depth.reason}."));
        }
    }

    static TabletopHandSample Hand(Vector4 fingers, float pinch, Vector3 offset)
    {
        var point = anchor + offset;
        return TabletopHandSample.Create(Time.unscaledTime, point - Vector3.up * .03f, point,
            point + Vector3.right * pinch, point - Vector3.forward * .04f, fingers, Vector3.right, pinch);
    }

    static void SelectAndCalibrate()
    {
        anchor = target.transform.position;
        Require(!controller.selected, "Recognition selected an actor without the user's gesture.");
        if (FromBridge)
            Require(roomDetectionPanel.activeInHierarchy, "The live Room preview was hidden before selection.");
        var point = new Vector4(.95f,.55f,.55f,.55f);
        Require(controller.ProcessHandSample(Hand(point,.02f,-Vector3.forward*.09f), true) == EditDimension.None &&
                controller.selected == target, "Point and pinch did not select the YOLO-tracked actor.");
        Require(target.ConfirmOrientation(20f), "Manual test orientation could not be confirmed.");
        Require(controller.RequestFit() && controller.ConfirmFit(anchor, true) &&
                controller.ConfirmFit(anchor + Vector3.up * (Plant ? .26f : .65f), true), "Manual test fit failed.");
        if (!Plant)
            Require(controller.RequestManualPlane() &&
                    controller.ConfirmManualPlane(target.visual.TransformPoint(Vector3.up*.9f), true), "Manual ceiling test anchor failed.");
        needsRoomWall = !Plant && !Variants;
        selectedByPinch = true;
        heldHand = Hand(Vector4.one*.95f,.08f,Vector3.up*.05f);
        readyAt = Time.unscaledTime + .3f;
        transitions.Add("Selected original detector instance " + target.instanceId +
            " by synthetic point/pinch after three stereo observations.");
    }

    static void CalibrateRoomWall()
    {
        var room = target.GetComponent<RoomMagicVisual>();
        CheckPassthroughRoom();
        var ceiling = target.environmentAnchor;
        Vector3 ceilingPosition = ceiling.position;
        Require(!controller.Dispatch(ParkAction.Awaken,anchor,true) &&
                !target.RoomWindowRequested && controller.InteractionStatus.Contains("Place window"),
            "Room B cast before the wall was marked.");
        var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .SingleOrDefault(item => item.name == "Place window");
        Require(button && button.interactable, "The selected Room did not expose the live wall placement button.");
        button.onClick.Invoke();
        Require(controller.PlaneConfirmationPending, "The wall button did not own the next two pinch edges.");
        Vector3 facing = Vector3.ProjectOnPlane(anchor-Camera.main.transform.position,Vector3.up);
        Quaternion wallFrame = facing.sqrMagnitude>.0001f ? Quaternion.LookRotation(facing) : target.transform.rotation;
        Vector3 right = wallFrame*Vector3.right;
        Vector3 center = anchor - right*.32f + wallFrame*Vector3.forward*.26f;
        Vector3 first = center-right*.20f;
        Vector3 second = center+right*.20f;
        var open = Vector4.one*.95f;
        controller.ProcessHandSample(Hand(open,.08f,first-anchor),true);
        controller.ProcessHandSample(Hand(open,.02f,first-anchor),true);
        controller.ProcessHandSample(Hand(open,.02f,second-anchor),true);
        Require(controller.PlaneConfirmationPending&&!target.RoomWindowWallPlaced&&!controller.LastMagicAccepted,
            "A held pinch placed or cast the window.");
        Require(!controller.ConfirmManualPlane(first+.02f*right,true) &&
                !controller.ConfirmManualPlane(second+.2f*Vector3.up,true), "An invalid wall edge was accepted.");
        controller.ProcessHandSample(Hand(open,.08f,second-anchor),true);
        controller.ProcessHandSample(Hand(open,.02f,second-anchor),true);
        Require(target.RoomWindowWallPlaced&&!target.RoomWindowRequested&&!controller.PlaneConfirmationPending,
            "Two wall corner pinches did not place the window without casting it.");
        Require(target.environmentAnchor==ceiling&&ceiling.position==ceilingPosition&&target.confirmedPlane,
            "Wall placement moved or invalidated the ceiling anchor.");
        wallWindowPosition=room.Window.position;
        wallWindowRotation=room.Window.rotation;
        Require(Vector3.Distance(wallWindowPosition,center)>.016f&&
                Vector3.Distance(wallWindowPosition,center)<.020f, "The window was not offset just in front of the wall.");
        target.transform.position+=right*.06f;
        Require(room.Window.position==wallWindowPosition&&room.Window.rotation==wallWindowRotation,
            "The placed window followed a change in the recognized target pose.");
        target.transform.position=anchor;
        if (FromBridge)
        {
            Require(!roomDetectionPanel.activeInHierarchy, "The live detection preview still covered the selected Room.");
            transitions.Add("The live detection preview was visible before selection and yielded to the tracked room; Place window remained usable.");
            Capture("passthrough-idle.png");
        }
        needsRoomWall=false;
        readyAt=Time.unscaledTime+.3f;
        transitions.Add("The live Place window menu used two synthetic corner pinches and held world position during a target shift. The ceiling anchor was unchanged.");
    }

    static EditDimension PlannedSpell => Variants && roomVariant == RoomVariant.Portal && spellIndex < 2
        ? (spellIndex == 0 ? EditDimension.Agency : EditDimension.Appearance)
        : Spells[spellIndex];

    static void StartSpell(float now)
    {
        controller.ProcessMagicSample(default, false);
        var open = Vector4.one * .95f;
        var point = new Vector4(.95f,.55f,.55f,.55f);
        var near = Variants ? VariantGestureOffset(PlannedSpell) : Vector3.up * .05f;
        var samples = new List<TabletopHandSample> { Hand(open,.08f,near), Hand(open,.08f,near) };
        switch (PlannedSpell)
        {
            case EditDimension.Appearance:
                for (int n=0;n<8;n++) samples.Add(Hand(open,.02f,near));
                samples.Add(Hand(open,.08f,near));
                break;
            case EditDimension.Agency:
                for (int n=0;n<=32;n++)
                {
                    float angle = n / 32f * Mathf.PI * 2;
                    samples.Add(Hand(point,.08f,near+new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*.04f));
                }
                break;
            case EditDimension.Rule:
                for (int n=1;n<=6;n++) samples.Add(Hand(open,.08f,near+Vector3.right*(n*.029f)));
                break;
            case EditDimension.Deconstruction:
                int count = Mathf.CeilToInt(TabletopGestureRules.FistMinimumSeconds / HandStep) + 1;
                for (int n=0;n<count;n++) samples.Add(Hand(Vector4.one*.55f,.02f,near));
                samples.Add(Hand(open,.08f,near));
                break;
        }
        handTrace = samples.ToArray();
        handIndex = spellEvents = 0;
        nextHand = now;
    }

    static void AdvanceHand(float now)
    {
        if (now < nextHand) return;
        var sample = handTrace[handIndex++];
        sample.gesture.time = now;
        var outcome = SendHand(sample, now);
        heldHand = sample;
        if (outcome != EditDimension.None)
        {
            Require(outcome == PlannedSpell && controller.LastMagicAccepted, "Gesture rejected: " + controller.InteractionStatus);
            spellEvents++;
        }
        nextHand = now + HandStep;
        if (handIndex < handTrace.Length) return;
        Require(spellEvents == 1, "Expected exactly one shared gesture event: " + PlannedSpell);
        transitions.Add($"Accepted {PlannedSpell} once using recorded frames through reply {replyIndex}.");
        handTrace = null;
        spellIndex++;
        readyAt = now + (Variants ? 1.2f : pointFlower ? 1.05f :
            Plant ? (spellIndex >= 3 ? 2.25f : 1.15f) : (spellIndex == 2 ? 2.65f : 1.05f));
    }

    static void MaintainHand(float now)
    {
        if (now < nextHand) return;
        heldHand.gesture.time = now;
        Require(SendHand(heldHand, now) == EditDimension.None,
            "A stationary held hand repeated a spell.");
        if(FromBridge && !Plant && !Variants && spellIndex==2 && target){
            var transfer=target.GetComponent<RoomPhotoTransfer>();
            if(transfer && transfer.Running && transfer.Stage=="held" && !capturedPalm){
                Require(Vector3.Distance(transfer.Focus,heldHand.gesture.palm+Vector3.up*.025f)<.002f,
                    "Gathered particles did not follow the current palm sample.");
                Capture("swan-lake-palm.png");capturedPalm=true;
            }else if(transfer && transfer.Running && transfer.Elapsed>1.33f && transfer.Elapsed<1.65f && !capturedRelease){
                Capture("swan-lake-release.png");capturedRelease=true;
            }
        }
        nextHand = now + HandStep;
    }

    static EditDimension SendHand(TabletopHandSample sample, float now)
    {
        if (previousHand >= 0 && now - previousHand > .1f)
            transitions.Add(FormattableString.Invariant($"Hand sample cadence gap {now-previousHand:R}s at room time {now-started:R}s, reply {replyIndex}."));
        previousHand = now;
        return controller.ProcessHandSample(sample, true);
    }

    static void CheckEffects()
    {
        var room = target.GetComponent<RoomMagicVisual>();
        var jelly = target.GetComponentInChildren<NaturalJellyfish>(true);
        transitions.Add(FormattableString.Invariant(
            $"Final effect state: style={target.RoomStyle}, window={target.RoomWindowRequested}/{(room ? room.WindowProgress : -1):R}, lamp={target.RoomLampRequested}/{(jelly ? jelly.Visibility : -1):R}, feedback={target.Feedback}."));
        Require(room && jelly && target.RoomStyle == 1 && target.RoomWindowRequested &&
                room.WindowProgress > .99f && target.RoomLampRequested && jelly.Visibility > .99f,
            "First three effects did not coexist on the recognized room.");
        Require(room.PhotoTransfer.Photo && room.PhotoTransfer.Photo==room.LandscapeRenderer.sharedMaterial.GetTexture("_BaseMap")
                && room.LandscapeRenderer.sharedMaterial.shader.name=="MagicMR/SwanLakeWindow"
                && room.LandscapeRenderer.sharedMaterial.shader.isSupported,
            "The photo particles and placed lake must use the same artwork.");
        if(FromBridge)Require(capturedPalm && capturedRelease,"Missing intermediate lake particles.");
        Capture("swan-lake-wall.png");
        CheckPassthroughRoom();
        Require(target.RoomWindowWallPlaced && room.Window.position==wallWindowPosition &&
                room.Window.rotation==wallWindowRotation,
            "Room B changed the manually placed wall pose when opening.");
        var overlay = target.transform.Find("MR room style wash");
        var overlayMaterial = overlay ? overlay.GetComponent<MeshRenderer>().sharedMaterial : null;
        Require(overlay && overlay.gameObject.activeInHierarchy && overlayMaterial &&
                overlayMaterial.shader.isSupported && overlayMaterial.GetFloat("_Reveal") > .9f,
            "The physical Room style wash did not animate on the tracked target.");
        var pool = (List<Transform>)typeof(ParkActor).GetField("pool",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(target);
        Require(pool.Count == 32 && pool.Count(x => x.gameObject.activeSelf) == 24 && target.confirmedPlane,
            "Ceiling-gated reward pool was not active.");
    }

    static void CheckPlantBeforeNextSpell()
    {
        if (spellIndex == 1)
        {
            var highlight=target.visual.parent.Find("Selected virtual target highlight");
            Require(highlight && highlight.gameObject.activeInHierarchy && !target.PlantBloomVisible,
                "A did not retain the selected plant highlight.");
        }
        if (spellIndex == 2)
        {
            var audio=target.GetComponent<AudioSource>();
            Require(target.PlantVoiceActive && audio && audio.clip && audio.clip.channels==1 && maximumMouth>.05f,
                "B did not run the original audio and mouth envelope.");
            transitions.Add(FormattableString.Invariant($"B voice active; observed mouth amplitude {maximumMouth:R}."));
        }
        if (spellIndex == 3)
        {
            Require(Mathf.Abs(target.growthTime-1f)<.001f, "C did not reach the grown state.");
            transitions.Add("C reached the grown state on the selected plant.");
        }
    }

    static void CheckPlantCleared()
    {
        var speech=target.GetComponentsInChildren<TextMesh>(true).Single(t=>t.name=="Virtual plant dialogue");
        var audio=target.GetComponent<AudioSource>();
        Require(!speech.gameObject.activeInHierarchy && !target.PlantBloomVisible && !target.PlantVoiceActive &&
                target.PlantVoiceMouth==0 && !audio.clip && Mathf.Abs(target.growthTime-.45f)<.001f &&
                target.sizeConfirmed && target.orientationConfirmed,
            "D did not clear plant effects while preserving manual placement.");
        transitions.Add("D cleared plant effects, restored base growth and kept fit/orientation.");
    }

    static void BeginPointFlower(float now)
    {
        plantUi=UnityEngine.Object.FindFirstObjectByType<ParkSharedInterface>();
        Require(plantUi, "Saved shared UI is missing.");
        plantUi.Command("Particle flower");
        pointFlower=target.PointFlower;
        Require(target.PointFlowerMode && pointFlower && pointFlower.Active && controller.selected==target,
            "Original menu did not enable the point flower on the same recognized plant.");
        transitions.Add("Shared menu opened Particle flower on the same detector instance.");
        CheckPointFlowerBrush();
        spellIndex=0;
        readyAt=Time.unscaledTime+.2f;
    }

    static void CheckPointFlowerBrush()
    {
        var root=target.visual.Find("Owned finite point flower");
        Require(root,"Tracked plant did not create its point cloud.");
        var cloud=root.GetComponent<ParticleSystem>();
        var points=pointFlower.CurrentPoints;
        var tip=points.Where(point=>Vector3.Distance(root.TransformPoint(point),root.position)<.18f)
                      .OrderByDescending(point=>point.y).First();
        Vector3 offset=root.TransformPoint(tip)-anchor;
        Require(tip.y>0,"The test could not reach a point on the flower crown.");
        var hand=Hand(Vector4.one*.95f,.08f,offset);
        Capture("point-flower-brush-rest.png");
        for(int i=0;i<2;i++)
            Require(controller.ProcessMagicSample(hand,true)==EditDimension.None&&!controller.LastMagicAccepted,
                "A stationary fingertip cast a spell.");
        pointFlower.Step(.10f);
        var particles=new ParticleSystem.Particle[PointFlowerMorph.PointBudget];
        Require(cloud.GetParticles(particles)==particles.Length,"Fingertip changed the point budget.");
        float maximum=0;
        int moved=0;
        for(int i=0;i<points.Length;i++)
        {
            float delta=Vector3.Distance(points[i],particles[i].position);
            if(delta>.0001f)moved++;
            if(Vector3.Distance(points[i],tip)>=.08f)
                Require(delta<.0001f,"Fingertip changed a distant point.");
            maximum=Mathf.Max(maximum,delta);
        }
        Require(moved>0&&moved<particles.Length&&maximum>.008f&&maximum<=.0351f,
            "The fingertip did not produce a bounded local displacement.");
        Capture("point-flower-brush-active.png");
        for(int i=0;i<4;i++)pointFlower.Step(.10f);
        cloud.GetParticles(particles);
        Require(points.Select((p,i)=>Vector3.Distance(p,particles[i].position)).Max()<.0001f,
            "Point displacement did not relax after hand samples stopped.");
        var far=Hand(Vector4.one*.95f,.08f,root.position-anchor+Vector3.forward*.5f);
        controller.ProcessMagicSample(far,true);
        pointFlower.Step(.10f);
        cloud.GetParticles(particles);
        Require(points.Select((p,i)=>Vector3.Distance(p,particles[i].position)).Max()<.0001f,
            "An out-of-range fingertip changed the flower.");
        transitions.Add(FormattableString.Invariant(
            $"A stationary fingertip at the crown displaced {moved} of {particles.Length} particles, maximum {maximum:R} local units; stopped hand samples restored the points and out-of-range motion left them still."));
    }

    static void CheckPointFlower()
    {
        Require(target.PointFlowerMode && pointFlower.LivePoints==PointFlowerMorph.PointBudget &&
                pointFlower.OwnedMaterialCount==1, "Point flower ownership or count changed.");
        if (spellIndex >= 1) Require(pointFlower.Palette==1,"A did not change the point palette.");
        if (spellIndex >= 2) Require(pointFlower.Alive,"B did not enable breathing.");
        if (spellIndex >= 3)
            Require(pointFlower.Shape==1 && pointFlower.Transition>=.999f &&
                    pointFlower.CurrentPoints.All(p=>Mathf.Abs(p.magnitude-.105f)<.0002f),
                "C did not form the sphere on the recognized plant.");
        if (spellIndex == 4)
            Require(pointFlower.Status.Contains("scatters and regroups"),"D did not request scatter/regroup.");
    }

    static void CheckXrCamera()
    {
        var scene = SceneManager.GetActiveScene();
        var camera = Camera.main;
        var origin = camera ? camera.GetComponentInParent<XROrigin>() : null;
        Require(camera && camera.isActiveAndEnabled && camera.gameObject.scene == scene &&
                origin && origin.isActiveAndEnabled && origin.Camera == camera,
            scene.name + " did not render through its own XR Origin camera.");
        var settings = camera.GetComponent<UniversalAdditionalCameraData>();
        Require(!camera.orthographic && camera.stereoTargetEye == StereoTargetEyeMask.Both && settings &&
                settings.allowXRRendering && settings.renderType == CameraRenderType.Base && settings.cameraStack.Count == 0,
            scene.name + " lost its perspective, both-eye XR routing or base camera ownership.");
        Require(camera.clearFlags == CameraClearFlags.SolidColor && camera.backgroundColor == Color.clear &&
                !camera.allowHDR && !camera.allowMSAA && !settings.allowHDROutput &&
                !settings.renderPostProcessing && settings.antialiasing == AntialiasingMode.None,
            scene.name + " used camera settings that can alter the passthrough alpha.");
        Require(Camera.allCameras.Count(other => other.cameraType == CameraType.Game && !other.targetTexture &&
                other.targetDisplay == camera.targetDisplay) == 1,
            scene.name + " had a second game camera rendering to the XR camera display.");
        transitions.Add(scene.name + " retained one active XR Origin perspective camera, Both eyes, RGBA clear=(0,0,0,0), no camera stack, HDR, MSAA or postprocessing.");
    }

    static void CheckPassthroughRoom()
    {
        var body = (List<Renderer>)typeof(ParkActor).GetField("body",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(target);
        Require(body.Count==0 && !target.transform.Find("MR room miniature") &&
                !target.visual.Find("Plaster walls") && !target.visual.Find("ReadingChair"),
            "The recognized Room added a virtual room shell or furnishings over passthrough.");
    }

    static void Capture(string filename = null)
    {
        var go = new GameObject("Room chain test camera");
        var camera = go.AddComponent<Camera>();
        var center = target.visual.TransformPoint(new Vector3(0,Plant?.28f:.38f,.025f));
        bool passthroughReview = !Plant && !Variants;
        var view = target.transform;
        camera.transform.position = center + view.TransformDirection(new Vector3(.7f,passthroughReview ? .62f : .3f,-1.5f));
        camera.transform.LookAt(center);
        camera.orthographic = !passthroughReview;
        camera.fieldOfView = 42;
        camera.orthographicSize = (Plant?.31f:.52f) * target.visual.lossyScale.y;
        bool particlesOnly=filename=="swan-lake-palm.png" || filename=="swan-lake-release.png";
        
        if(particlesOnly){
            camera.orthographic=true;
            var particles=target.GetComponentsInChildren<ParticleSystem>(true).Single(p=>p.name=="Room photo particles - bounded");
            var points=new ParticleSystem.Particle[particles.particleCount];
            int count=particles.GetParticles(points);
            Require(count==RoomPhotoTransfer.ParticleBudget,"Missing lake particles at capture.");
            var bounds=new Bounds(points[0].position,Vector3.zero);
            for(int i=1;i<count;i++)bounds.Encapsulate(points[i].position);
            camera.transform.position=bounds.center+new Vector3(.045f,.025f,-.5f);
            camera.transform.LookAt(bounds.center);
            camera.orthographicSize=Mathf.Max(.08f,bounds.extents.magnitude*1.24f);
        }
        bool lakeDetail=filename=="swan-lake-wall.png";
        var landscape=lakeDetail?target.GetComponent<RoomMagicVisual>().LandscapeRenderer:null;
        int priorLayer=lakeDetail?landscape.gameObject.layer:0;
        if(lakeDetail){
            var wall=landscape.transform;
            camera.orthographic=false;
            camera.fieldOfView=40;
            camera.transform.position=wall.TransformPoint(new Vector3(.035f,.26f,-.83f));
            camera.transform.LookAt(wall.TransformPoint(new Vector3(0,.26f,.012f)),wall.up);
            camera.cullingMask=1<<30;
            landscape.gameObject.layer=30;
        }
        camera.nearClipPlane = .01f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = passthroughReview ? Color.clear : new Color(.14f,.16f,.18f);
        var surface = RenderTexture.GetTemporary(960,800,24);
        var pixels = new Texture2D(960,800,passthroughReview ? TextureFormat.RGBA32 : TextureFormat.RGB24,false);
        var old = RenderTexture.active;
        try
        {
            camera.targetTexture = surface;
            camera.Render();
            RenderTexture.active = surface;
            pixels.ReadPixels(new Rect(0,0,960,800),0,0);
            pixels.Apply();
            if(particlesOnly)
            {
                // Compress after gesture replay so PNG encoding cannot starve the tracked samples.
                stagedPhotoCaptures.Add(filename,pixels);
                pixels=null;
            }
            else File.WriteAllBytes(Path.Combine(Output,filename??(Plant?"recognized-plant-point-flower.png":
                Variants?"recognized-room-snow.png":"recognized-room-effects.png")), pixels.EncodeToPNG());
            if(filename=="passthrough-idle.png")
                Require(pixels.GetPixel(480,400).a<.02f && pixels.GetPixel(8,8).a<.02f,
                    "The tracked room blocked passthrough before its effects were cast.");
            if(lakeDetail)Require(pixels.GetPixel(480,400).a>.98f && pixels.GetPixel(8,8).a<.02f,
                "The placed lake lost its solid center or clear exterior.");
        }
        finally
        {
            if(lakeDetail)landscape.gameObject.layer=priorLayer;
            camera.targetTexture = null;
            RenderTexture.active = old;
            RenderTexture.ReleaseTemporary(surface);
            UnityEngine.Object.DestroyImmediate(go);
            if(pixels)UnityEngine.Object.DestroyImmediate(pixels);
        }
    }


    static void CheckRoomRecovery(float now)
    {
        Require(target && !target.virtualTarget && controller.selected == target &&
                Vector3.Distance(target.transform.position,anchor)<.001f,
            "Resumed observations changed the selected target or its spatial anchor.");
        CheckPassthroughRoom();
        int restoredReplies = recognized - recoveryReplyCount;
        if (restoredReplies < 3)
        {
            Require(!target.tracked,
                "The room became visible before three stable observations returned.");
            MaintainHand(now);
            return;
        }

        Require(target.tracked, "Stable observations did not restore room tracking.");
        Require(!target.RoomWindowRequested && !target.RoomWindowWallPlaced && !target.RoomLampRequested &&
                !target.confirmedPlane, "Resumed observations restored expired effects or wall placement.");
        if (!recoveryCasting)
        {
            Require(target.RoomStyle == 0, "Room style returned before a new gesture.");
            if (roomDetectionPanel.activeInHierarchy) { MaintainHand(now); return; }
            Require(!controller.Dispatch(ParkAction.Awaken,anchor,true) &&
                    !target.RoomWindowRequested && controller.InteractionStatus.Contains("Place window"),
                "Recovered Room accepted B without a fresh wall placement.");
            transitions.Add($"Restored Room {target.instanceId} after {restoredReplies} acknowledged replies; detection preview yielded again and B still required wall placement.");
            recoveryCasting = true;
            spellIndex = 0;
            StartSpell(now);
            return;
        }
        if (handTrace != null) { AdvanceHand(now); return; }
        MaintainHand(now);
        if (now < readyAt) return;
        Require(target.RoomStyle == 1,
            "A fresh shared A gesture did not apply to the recovered Room.");
        transitions.Add("Fresh A changed the recovered Room style; old B/C/D and the manual plane remained cleared.");
        Finish(0,"PASS: saved BridgeStereoFusion acknowledged Room, routed to ParkRoom, replayed PICF over saved TCP, and verified four tracked effects, expiry and same-instance recovery.");
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Running,false) || !EditorApplication.isPlaying || !Application.isPlaying) return;
        try
        {
            if (!initialized)
            {
                if (!FromBridge || EnterRoomFromBridge()) Initialize();
                return;
            }
            float now = Time.unscaledTime;
            if (Plant && target) maximumMouth=Mathf.Max(maximumMouth,target.PlantVoiceMouth);
            Require(errors == 0 && now-started < 30f, "Room chain reported runtime errors or timed out.");
            PumpRecognition(now);
            if (recoveryReplyCount >= 0) { CheckRoomRecovery(now); return; }
            if (!selectedByPinch)
            {
                if (target && target.tracked) SelectAndCalibrate();
                return;
            }
            if (needsRoomWall)
            {
                if (Time.unscaledTime>=readyAt) CalibrateRoomWall();
                return;
            }
            if (withholdingReplies)
            {
                if (target.tracked) { MaintainHand(now); return; }
                if (Variants)
                {
                    FinishRoomVariants();
                    return;
                }
                if (Plant)
                {
                    Require(!target.PlantVoiceActive && !target.sizeConfirmed && !target.orientationConfirmed,
                        "Expired plant retained playback or fit.");
                    transitions.Add("Withheld replies; tracked plant expired.");
                    Finish(0,"PASS: saved ParkPlant actual PICF and stereo replay selected one tracked plant, verified shared A/B/C/D and target expiry.");
                    return;
                }
                Require(target.RoomStyle == 0 && !target.RoomWindowRequested && !target.RoomWindowWallPlaced && !target.RoomLampRequested &&
                        !target.confirmedPlane, "Timed-out real target retained effects or plane calibration.");
                var overlay = target.transform.Find("MR room style wash");
                Require(overlay && !overlay.gameObject.activeSelf, "Timed-out real target retained the MR room wash.");
                CheckPassthroughRoom();
                // The UI runs before the controller's expiry tick and refreshes on the next frame.
                if (FromBridge && !roomDetectionPanel.activeInHierarchy) { MaintainHand(now); return; }
                transitions.Add("Withheld replies; production target timeout cleared A/B/C/D and manual plane.");
                if (FromBridge)
                {
                    transitions.Add("While its last frame was still fresh, the live detection preview returned after the selected Room expired.");
                    recoveryReplyCount = recognized;
                    withholdingReplies = false;
                    return;
                }
                Finish(0,"PASS: saved ParkRoom actual PICF and stereo replay selected the tracked chair, A/B/C/D coexisted, and target expiry cleared effects.");
                return;
            }
            Require(target.tracked && controller.selected == target && Vector3.Distance(target.transform.position,anchor)<.001f,
                "Room magic changed target identity or tracking placement.");
            if (lastWindowRequest != target.RoomWindowRequested)
            {
                lastWindowRequest = target.RoomWindowRequested;
                transitions.Add(FormattableString.Invariant($"Window requested={lastWindowRequest} at room time {now-started:R}s, reply {replyIndex}, {target.Feedback}."));
            }
            if (handTrace != null) { AdvanceHand(now); return; }
            if (now < readyAt || target.Busy) { MaintainHand(now); return; }
            if (Variants)
            {
                CheckRoomVariants(now);
                return;
            }
            if (pointFlower) CheckPointFlower();
            else if (Plant) CheckPlantBeforeNextSpell();
            if (spellIndex < Spells.Length) { StartSpell(now); return; }
            if (Plant)
            {
                if (!pointFlower)
                {
                    CheckPlantCleared();
                    BeginPointFlower(now);
                    return;
                }
                transitions.Add("Point flower retained 2048 points and one material; A/B/C/D completed on the selected instance.");
                Capture();
                plantUi.Command("Plant effects");
                Require(!target.PointFlowerMode && !pointFlower.Active && controller.selected==target,
                    "Original menu did not restore default Plant on the same instance.");
                transitions.Add($"{recognized} recorded real PICF replies maintained the selected instance.");
                withholdingReplies = true;
                return;
            }
            Capture();
            CheckEffects();
            transitions.Add($"All four effects coexisted; {recognized} valid recorded responses refreshed exactly one target.");
            withholdingReplies = true;
        }
        catch (Exception failure)
        {
            Debug.LogException(failure);
            Finish(2, "FAIL: " + failure);
        }
    }
}
