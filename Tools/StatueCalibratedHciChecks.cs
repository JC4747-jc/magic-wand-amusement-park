using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using MagicMR;
using Perception;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class StatueCalibratedHciChecks
{
    const string Key = "MagicMR.StatueCalibratedHci";
    const string Input = ".codex-tmp/statue-calibrated-hci-fixture";
    const string Output = ".codex-tmp/statue-calibrated-hci-checks";
    const float HandStep = .03f;
    const float PicfStep = .14f;
    static readonly List<string> Trace = new();
    static readonly List<TabletopHandSample> Hands = new();

    [Serializable] sealed class Config
    {
        public float[] intrinsics, missingIntrinsics;
        public float baselineMetres, missingBaselineMetres, depthMetres;
        public int present, missingShort, retained, missingLong, recovered;
        public string firstId, secondId;
    }

    enum Phase { Select, A, B, Return, C, D, ReopenStory, Loss, Retained, RetainedCast, LongLoss, Recovery, Recast, Finish }
    static Phase phase;
    static Config config;
    static ParkController controller;
    static ParkStereoReceiver receiver;
    static TcpListener loopback;
    static System.Net.Sockets.TcpClient peer;
    static ParkActor actor;
    static NaturalStatueStretch stretch;
    static StatueWishGarden garden;
    static TabletopGestureRules lighter;
    static EditDimension expected;
    static string[] replies;
    static float began, nextPacket, sentAt, nextHand, readyAt;
    static int accepted, sent, checkedReplies, handIndex, events, unityFrame = -1;
    static int renders, errors;
    static bool routeRequested, sampledStretch, showingMissing, holdValid;
    static TabletopHandSample held;
    static Vector3 initialAnchor, ground;
    static Transform[] restBones;
    static Quaternion[] restRotations;
    static float restHandY, maximumHandRise;

    static StatueCalibratedHciChecks()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (_, _, type) =>
        {
            if (SessionState.GetBool(Key, false) && type is LogType.Error or LogType.Exception or LogType.Assert)
                errors++;
        };
    }

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/BridgeStereoFusion.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || unityFrame == Time.frameCount) return;
        unityFrame = Time.frameCount;
        try
        {
            float now = Time.unscaledTime;
            if (began == 0) began = now;
            Require(now - began < 80, $"Timed out in {SceneManager.GetActiveScene().name}, {phase}, {checkedReplies}/{sent}");
            Require(errors == 0, "Unity logged a runtime error");
            if (!controller)
            {
                if (!EnterFromBridge()) return;
                Initialize();
            }
            if (peer == null && loopback.Pending())
            {
                peer = loopback.AcceptTcpClient();
                peer.NoDelay = true;
            }
            if (peer == null || !receiver.transport.IsConnected) return;
            VerifyReply(now);
            StepEffects(now);
            if (checkedReplies == sent && sent < replies.Length && now >= nextPacket) Send(now);
        }
        catch (Exception error)
        {
            Trace.Add(error.ToString());
            End(2, "FAIL: " + error.Message);
        }
    }

    static bool EnterFromBridge()
    {
        if (routeRequested) return SceneManager.GetActiveScene().name == "ParkStatue";
        var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .SingleOrDefault(candidate => candidate.name == "Statue" && candidate.isActiveAndEnabled && candidate.interactable);
        if (!button) return false;
        var menu = UnityEngine.Object.FindFirstObjectByType<ParkSharedInterface>();
        Require(SceneManager.GetActiveScene().name == "BridgeStereoFusion" && menu && !menu.controller &&
                UnityEngine.Object.FindFirstObjectByType<StereoYoloLocator>(), "Original Bridge capture route unavailable");
        routeRequested = true;
        button.onClick.Invoke();
        Trace.Add("BridgeStereoFusion.Statue -> ParkStatue");
        return false;
    }

    static void Initialize()
    {
        config = JsonUtility.FromJson<Config>(File.ReadAllText(Path.Combine(Input, "input.json")));
        replies = File.ReadAllLines(Path.Combine(Input, "replies.jsonl"));
        Require(replies.Length == config.present + config.missingShort + config.retained + config.missingLong + config.recovered,
            "Fixture packet count changed");
        controller = UnityEngine.Object.FindFirstObjectByType<ParkController>();
        var menu = UnityEngine.Object.FindFirstObjectByType<ParkSharedInterface>();
        Require(controller && controller.Profile == "statue", "Wrong saved scene");
        receiver = controller.receiver as ParkStereoReceiver;
        Require(receiver && menu && menu.controller == controller && menu.receiver == receiver &&
                menu.transport == receiver.transport && receiver.sender.FlipVerticallyBeforeEncode,
                "Saved Statue UI, stereo receiver, or original transport binding changed");
        receiver.enabled = false;
        receiver.discoveryMode = false;
        controller.simulation = false;
        receiver.RecognitionAccepted += _ => accepted++;
        SetImage(false);
        Set(receiver, "capturePose", Pose.identity);
        Set(receiver, "cameraPose", Pose.identity);
        var transport = receiver.transport;
        transport.AutoConnect = false;
        transport.DisconnectSocket();
        transport.LineReceived += receiver.Receive;
        loopback = new TcpListener(IPAddress.Loopback, 0);
        loopback.Start(1);
        Set(transport, "m_AutoReconnect", false);
        Set(transport, "m_Host", IPAddress.Loopback.ToString());
        Set(transport, "m_Port", ((IPEndPoint)loopback.LocalEndpoint).Port);
        transport.ConnectIfNeeded();
        new GameObject("Calibrated statue synthetic joint trace").AddComponent<HciSeasonTreeHandPump>().sample = DriveHand;
    }

    static void Send(float now)
    {
        int frameId = sent + 1;
        var frame = JsonUtility.FromJson<DetectionFrameMessage>(replies[sent]);
        Require(frame.frame_id == frameId && frame.scene == "statue" && frame.status == "OK", "Malformed original PICF");
        if (showingMissing != Missing(frameId)) SetImage(Missing(frameId));
        Set(receiver, "expected", frame.frame_id);
        Set(receiver, "waiting", true);
        Set(receiver, "capturedAt", Time.realtimeSinceStartup);
        Set(receiver, "capturePoseValid", true);
        Set(receiver, "headTracked", true);
        byte[] bytes = Encoding.UTF8.GetBytes(replies[sent] + "\n");
        peer.GetStream().Write(bytes, 0, bytes.Length);
        sent++;
        sentAt = now;
        nextPacket = now + PicfStep;
    }

    static int LongLossStart => config.present + config.missingShort + config.retained + 1;
    static bool Missing(int frame) =>
        (frame > config.present && frame <= config.present + config.missingShort) ||
        (frame >= LongLossStart && frame < LongLossStart + config.missingLong);

    static void SetImage(bool missing)
    {
        showingMissing = missing;
        string prefix = missing ? "missing-" : "";
        Set(receiver, "left", File.ReadAllBytes(Path.Combine(Input, prefix + "left.gray")));
        Set(receiver, "right", File.ReadAllBytes(Path.Combine(Input, prefix + "right.gray")));
        float[] k = missing ? config.missingIntrinsics : config.intrinsics;
        Set(receiver, "intrinsics", new Vector4(k[0], k[1], k[2], k[3]));
        Set(receiver, "baseline", missing ? config.missingBaselineMetres : config.baselineMetres);
    }

    static void VerifyReply(float now)
    {
        if (checkedReplies == sent) return;
        Require(now - sentAt < .6f, $"PICF frame {sent} timeout: {receiver.Status}");
        if (accepted == checkedReplies) return;
        Require(accepted == sent, "Unexpected PICF replies or duplicates");
        checkedReplies = sent;
        if (Missing(sent))
        {
            holdValid = false;
            Require(receiver.Status == "NO_TARGET" && actor && !actor.tracked, "Loss did not invalidate the selected statue");
            Require(!actor.StatueNarrativeVisible && !actor.confirmedPlane && !garden.gameObject.activeInHierarchy,
                    "A lost target retained its narrative, garden, or confirmed surface");
            if (sent == config.present + 1)
            {
                Require(phase == Phase.Loss, "Replay lost the target before completing A/B/C/D");
                Trace.Add($"short loss frame={sent}: story, garden and surface cleared");
                phase = Phase.Retained;
            }
            else if (sent == LongLossStart)
            {
                Require(phase == Phase.LongLoss, "Replay entered long loss before the retained-ID cast");
                Trace.Add($"long loss frame={sent}: target and A material cleared");
                Require(!actor.StatueCartoon, "A remained active during the second loss");
                phase = Phase.Recovery;
            }
            return;
        }
        Require(receiver.Status.StartsWith("TRANSLATION_ONLY", StringComparison.Ordinal), receiver.Status);
        if (actor && actor.tracked && phase < Phase.Recovery)
            Require(Vector3.Distance(actor.transform.position, initialAnchor) < .003f, "Effects moved the recognized tracking anchor");
    }

    static ParkActor Track(string id) =>
        UnityEngine.Object.FindObjectsByType<ParkActor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .SingleOrDefault(target => target.instanceId == id && target.tracked && !target.virtualTarget);

    static void Select(ParkActor target, float now)
    {
        actor = target;
        initialAnchor = actor.transform.position;
        Require(Mathf.Abs(StereoFusionGeometry.HeadToOptical(initialAnchor, Pose.identity).z - config.depthMetres) < .003f,
                "Tracked depth differed from the prior calibrated replay");
        Vector3 ray = initialAnchor.normalized;
        Vector3 tip = initialAnchor - ray * .09f;
        controller.ProcessHandSample(default, false);
        var pinch = TabletopHandSample.Create(now, tip - ray * .06f, tip, tip + Vector3.right * .02f,
            tip - ray * .04f, new Vector4(.95f,.55f,.55f,.55f), Vector3.right, .02f);
        Require(controller.ProcessHandSample(pinch, true) == EditDimension.None && controller.selected == actor,
            "Point and pinch did not select the live-path recognized anchor");
        Trace.Add($"selected {actor.instanceId} at PICF frame {checkedReplies}");
        Require(actor.ConfirmOrientation(0) && actor.FitRepresentation(initialAnchor, initialAnchor + Vector3.up * .44f, true),
            "Manual test model fit or orientation could not be confirmed");
        stretch = actor.GetComponentInChildren<NaturalStatueStretch>(true);
        garden = actor.GetComponentInChildren<StatueWishGarden>(true);
        Require(stretch && garden && !actor.StatueCartoon, "Recognition built the wrong statue or reused stale effects");
        string[] names = { "hand_l", "foot_l", "foot_r", "pelvis" };
        restBones = names.Select(name => stretch.GetComponentsInChildren<Transform>(true).Single(bone => bone.name == name)).ToArray();
        restRotations = restBones.Select(bone => bone.localRotation).ToArray();
        restHandY = restBones[0].position.y;
    }

    static void StepEffects(float now)
    {
        if (Hands.Count > 0) return;
        if (now < readyAt) return;
        switch (phase)
        {
            case Phase.Select:
                var first = Track(config.firstId);
                if (!first) return;
                Require(checkedReplies == 3, "Expected three PICF replies before first selection");
                Select(first, now);
                BeginCast(EditDimension.Appearance, Phase.A, now);
                break;
            case Phase.A:
                if (actor.Busy) return;
                Require(actor.StatueCartoon, "A did not apply the cartoon material: " + actor.Feedback);
                Capture("A-cartoon", actor.transform.position + Vector3.up * .22f);
                BeginCast(EditDimension.Agency, Phase.B, now);
                break;
            case Phase.B:
                Require(stretch.GraphValid && actor.Busy,
                    $"B did not start the stretch animation after the casting transfer: {actor.Feedback}");
                phase = Phase.Return;
                break;
            case Phase.Return:
                maximumHandRise = Mathf.Max(maximumHandRise, restBones[0].position.y - restHandY);
                if (!sampledStretch && stretch.Progress > .27f && stretch.Progress < .78f)
                {
                    Capture("B-stretch", actor.transform.position + Vector3.up * .27f);
                    sampledStretch = true;
                }
                if (actor.Busy) return;
                Require(sampledStretch && stretch.GraphValid && maximumHandRise > .025f &&
                        restBones.Select((bone, i) => Quaternion.Angle(bone.localRotation, restRotations[i])).All(angle => angle < .15f),
                    "B failed to move and return to its rest pose");
                Trace.Add(FormattableString.Invariant($"B returned to tracked rest, hand rise {maximumHandRise:F4} m"));
                BeginCast(EditDimension.Rule, Phase.C, now);
                break;
            case Phase.C:
                if (actor.Busy) return;
                Require(actor.StatueNarrativeVisible && actor.StatueNarrativePage == 0 && actor.StatueNarrativePageCount > 1,
                    "C did not open the first chapter of the story");
                Capture("C-story", actor.transform.position + Vector3.right * .16f + Vector3.up * .24f);
                Capture("C-story-detail", actor.transform.position);
                controller.ProcessHandSample(default, false);
                ground = actor.transform.position - Vector3.up * .18f;
                Require(controller.RequestManualPlane() && controller.ConfirmManualPlane(ground, true), "D manual test floor failed");
                BeginCast(EditDimension.Deconstruction, Phase.D, now);
                break;
            case Phase.D:
                if (actor.Busy || garden.Progress < .93f) return;
                Require(garden.FlowerCount == 6 && garden.CardCount == 7 &&
                        Vector3.Distance(garden.transform.position, ground) < .003f, "D garden did not unfold on the confirmed floor");
                Capture("D-wish-garden", ground + Vector3.up * .14f);
                BeginCast(EditDimension.Rule, Phase.ReopenStory, now);
                break;
            case Phase.ReopenStory:
                if (actor.Busy) return;
                Require(actor.StatueNarrativeVisible && garden.gameObject.activeInHierarchy,
                    "Expected both story and garden before the NO_TARGET test");
                phase = Phase.Loss;
                break;
            case Phase.Retained:
                if (!actor.tracked) return;
                Require(Track(config.firstId) == actor && controller.selected == actor && !actor.StatueCartoon &&
                        !actor.StatueNarrativeVisible && !actor.confirmedPlane && !garden.gameObject.activeInHierarchy,
                    "Retained ID did not restore the original selection cleanly");
                Trace.Add($"short recovery {actor.instanceId} at PICF frame {checkedReplies}; previous effects cleared");
                BeginCast(EditDimension.Appearance, Phase.RetainedCast, now);
                break;
            case Phase.RetainedCast:
                if (actor.Busy) return;
                Require(actor.StatueCartoon, "Retained ID did not accept a fresh A");
                phase = Phase.LongLoss;
                break;
            case Phase.Recovery:
                var target = Track(config.secondId);
                if (!target) return;
                if (controller.selected)
                {
                    Require(!controller.selected.tracked && controller.selected.instanceId == config.firstId,
                        "The new tracker ID silently inherited the selection");
                    return;
                }
                var oldActor = actor;
                Select(target, now);
                Require(oldActor != actor && !garden.gameObject.activeInHierarchy && !actor.StatueNarrativeVisible,
                    "Recovered ID inherited old effects");
                BeginCast(EditDimension.Appearance, Phase.Recast, now);
                break;
            case Phase.Recast:
                if (actor.Busy) return;
                Require(actor.StatueCartoon, "Recovered target rejected A");
                phase = Phase.Finish;
                break;
            case Phase.Finish:
                if (checkedReplies != replies.Length) return;
                Require(renders == 5 && actor.tracked && controller.selected == actor, "Incomplete visuals or lost recovered selection");
                End(0, "PASS: original Bridge Statue PICF A/B/C/D, loss and fresh re-selection.");
                break;
        }
    }

    static void DriveHand()
    {
        try
        {
            if (!SessionState.GetBool(Key, false) || !actor || !actor.tracked) return;
            float now = Time.unscaledTime;
            if (Hands.Count > 0)
            {
                PumpHands(now);
                return;
            }
            if (!holdValid) return;
            held.gesture.time = now;
            Require(controller.ProcessHandSample(held, true) == EditDimension.None, "A stationary held sample repeated a cast");
        }
        catch (Exception error)
        {
            Trace.Add(error.ToString());
            End(2, "FAIL: " + error.Message);
        }
    }

    static TabletopHandSample Frame(Vector4 extension, float pinch, Vector3 movement)
    {
        Vector3 palm = initialAnchor + Vector3.up * .05f + movement;
        return TabletopHandSample.Create(0, palm, palm + Vector3.up * .04f,
            palm + Vector3.up * .04f + Vector3.right * pinch, palm - Vector3.up * .02f,
            extension, Vector3.right, pinch);
    }

    static void BeginCast(EditDimension dimension, Phase nextPhase, float now)
    {
        var open = Vector4.one * .95f;
        controller.ProcessHandSample(default, false);
        holdValid = false;
        lighter = new TabletopGestureRules();
        expected = dimension;
        phase = nextPhase;
        nextHand = now;
        handIndex = events = 0;
        Hands.Add(Frame(open, .08f, Vector3.zero));
        Hands.Add(Frame(open, .08f, Vector3.zero));
        switch (dimension)
        {
            case EditDimension.Appearance:
                for (int i = 0; i < 8; i++) Hands.Add(Frame(open, .02f, Vector3.zero));
                Hands.Add(Frame(open, .08f, Vector3.zero));
                break;
            case EditDimension.Agency:
                for (int i = 0; i <= 32; i++)
                {
                    float angle = i * Mathf.PI * 2f / 32f;
                    Hands.Add(Frame(new Vector4(.95f,.55f,.55f,.55f), .08f,
                        new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0) * .04f));
                }
                break;
            case EditDimension.Rule:
                for (int i = 1; i <= 6; i++) Hands.Add(Frame(open, .08f, Vector3.right * (i * .03f)));
                break;
            case EditDimension.Deconstruction:
                int fist = Mathf.CeilToInt(TabletopGestureRules.FistMinimumSeconds / HandStep) + 1;
                for (int i = 0; i < fist; i++) Hands.Add(Frame(Vector4.one * .55f, .02f, Vector3.zero));
                Hands.Add(Frame(open, .08f, Vector3.zero));
                break;
        }
    }

    static void PumpHands(float now)
    {
        if (now < nextHand) return;
        var sample = Hands[handIndex++];
        sample.gesture.time = now;
        nextHand = now + HandStep;
        var comparison = handIndex == 1 ? EditDimension.None : lighter.Step(sample.gesture);
        var gesture = controller.ProcessHandSample(sample, true);
        Require(comparison == gesture, $"Shared hand recognition differed at {expected}: {comparison}/{gesture}");
        if (gesture != EditDimension.None)
        {
            Require(gesture == expected && controller.LastMagicAccepted, $"{expected} was recognized but not dispatched: {controller.InteractionStatus}");
            events++;
            Trace.Add($"{phase} {gesture} accepted, synthetic joints, PICF {checkedReplies}, id={actor.instanceId}");
        }
        if (handIndex != Hands.Count) return;
        Require(events == 1, $"Expected one {expected} cast, got {events}");
        held = sample;
        holdValid = true;
        Hands.Clear();
        readyAt = now + (phase == Phase.C ? .66f : phase == Phase.A ? .70f : phase == Phase.B ? .45f : .15f);
    }

    static void Capture(string name, Vector3 center)
    {
        const int Width = 760, Height = 620;
        var camera = new GameObject("Statue HCI render review camera").AddComponent<Camera>();
        camera.fieldOfView = 35;
        camera.nearClipPlane = .01f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.28f,.32f,.37f);
        if (name.StartsWith("C", StringComparison.Ordinal) || name.StartsWith("D", StringComparison.Ordinal))
            FrameEffect(camera, name, (float)Width / Height);
        else
        {
            camera.transform.position = center + new Vector3(.02f,.13f,-.94f);
            camera.transform.LookAt(center);
        }
        var target = new RenderTexture(Width, Height, 24);
        var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), pixels.EncodeToPNG());
            renders++;
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    static void FrameEffect(Camera camera, string name, float aspect)
    {
        bool art = name.StartsWith("D", StringComparison.Ordinal);
        var effect = art ? garden.transform : actor.GetComponentsInChildren<Transform>()
            .Single(item => item.name == "Statue C - anchored narrative scroll");
        var effectRenderers = effect.GetComponentsInChildren<Renderer>();
        Require(effectRenderers.Length > 0, "Could not frame the visible effect");
        var renderers = name == "C-story-detail" ? effectRenderers.AsEnumerable()
            : effectRenderers.Concat(stretch.GetComponentsInChildren<SkinnedMeshRenderer>());
        Bounds bounds = effectRenderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        Quaternion look = Quaternion.LookRotation(art ? new Vector3(-.43f,-.30f,1) : Vector3.forward);
        Quaternion inverse = Quaternion.Inverse(look);
        float tangent = Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad) * .87f;
        float distance = .10f;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 corner = inverse * Vector3.Scale(bounds.extents, new Vector3(x,y,z));
            distance = Mathf.Max(distance, Mathf.Max(Mathf.Abs(corner.x) / (tangent * aspect),
                Mathf.Abs(corner.y) / tangent) - corner.z);
        }
        camera.transform.SetPositionAndRotation(bounds.center - look * Vector3.forward * distance, look);
    }

    static void End(int code, string status)
    {
        if (receiver) receiver.transport.LineReceived -= receiver.Receive;
        peer?.Dispose();
        loopback?.Stop();
        File.WriteAllLines(Path.Combine(Output, "trace.txt"), Trace);
        File.WriteAllText(Path.Combine(Output, "result.txt"), status);
        SessionState.SetBool(Key, false);
        EditorApplication.Exit(code);
    }

    static void Set(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName,BindingFlags.Instance | BindingFlags.NonPublic);
        Require(field != null, $"Missing saved runtime field {fieldName}");
        field.SetValue(target, value);
    }

    static void Require(bool value, string reason)
    {
        if (!value) throw new InvalidOperationException(reason);
    }
}
