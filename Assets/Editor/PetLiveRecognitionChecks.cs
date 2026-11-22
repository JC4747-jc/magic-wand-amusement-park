using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using MagicMR;
using Perception;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static partial class PetLiveRecognitionChecks
{
    const string Running = "MagicMR.PetLiveRecognitionChecks";
    const string Metric = Running + ".Metric";
    static bool Calibrated => SessionState.GetBool(Metric, false);
    static string Input => Calibrated ? ".codex-tmp/pet-live-metric-fixture" : ".codex-tmp/pet-live-recognition-fixture";
    static string Output => Calibrated ? ".codex-tmp/pet-live-metric-checks" : ".codex-tmp/pet-live-recognition-checks";
    static string Weights => Calibrated ? CandidateWeights : "f59b3d833e2ff32e194b5bb8e08d211dc7c5bdf144b90d2c8412c47ccfc83b36";
    static readonly List<string> replies = new();
    static readonly List<string> trace = new();
    static ParkStereoReceiver receiver;
    static ParkController controller;
    static Texture2D photo;
    static float started = -1, sentAt, nextFrame;
    static string id;
    static int accepted, frame, errors;
    static bool routed, waiting, warm, finished;

    static PetLiveRecognitionChecks()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (SessionState.GetBool(Running, false) && type is LogType.Error or LogType.Assert or LogType.Exception)
                errors++;
        };
    }

    public static void Run() => Begin(false);
    public static void RunCalibrated() => Begin(true);

    static void Begin(bool calibrated)
    {
        SessionState.SetBool(Metric, calibrated);
        Require(Directory.Exists(Input) && !Directory.Exists(Output), "Pet fixture or output path is invalid.");
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "result.txt"), "RUNNING");
        EditorSceneManager.OpenScene("Assets/Scenes/BridgeStereoFusion.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }

    static void Require(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException(description);
    }

    static void Finish(int code, string message)
    {
        finished = true;
        SessionState.SetBool(Running, false);
        if (receiver && receiver.transport) receiver.transport.LineReceived -= Receive;
        if (photo) UnityEngine.Object.DestroyImmediate(photo);
        if (Calibrated) SaveMetricCaptures();
        File.WriteAllLines(Path.Combine(Output, "live-picf-replies.jsonl"), replies);
        File.WriteAllLines(Path.Combine(Output, "trace.txt"), trace);
        File.WriteAllText(Path.Combine(Output, "result.txt"), message);
        EditorApplication.Exit(code);
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || finished) return;
        try
        {
            float now = Time.realtimeSinceStartup;
            if (started < 0) started = now;
            Require(now - started < 35f, "Pet live chain timed out.");
            if (!routed)
            {
                RouteFromBridge();
                return;
            }
            if (SceneManager.GetActiveScene().name != "ParkPet") return;
            if (!receiver) BindReceiver();
            if (Calibrated) StepMetric();
            if (finished) return;
            if (waiting)
            {
                Require(now - sentAt < (warm ? 2f : 10f), "Pet PICF reply timed out.");
                return;
            }
            if (receiver.transport.IsConnected && now >= nextFrame && (!Calibrated || !withholdMetricFrames)) Send();
        }
        catch (Exception ex)
        {
            Finish(1, "FAIL: " + ex);
        }
    }

    static void RouteFromBridge()
    {
        var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .SingleOrDefault(item => item.name == "Pet");
        if (!button) return;
        Require(SceneManager.GetActiveScene().name == "BridgeStereoFusion" &&
                UnityEngine.Object.FindFirstObjectByType<StereoYoloLocator>(), "Saved Bridge capture entry is missing.");
        button.onClick.Invoke();
        routed = true;
        trace.Add("Saved Bridge Pet button requested ParkPet.");
    }

    static void BindReceiver()
    {
        controller = UnityEngine.Object.FindFirstObjectByType<ParkController>();
        Require(controller && controller.scenario == ParkScenario.Pet && !controller.simulation, "Wrong saved Pet controller.");
        receiver = controller.receiver as ParkStereoReceiver;
        var ui = UnityEngine.Object.FindFirstObjectByType<ParkSharedInterface>();
        Require(receiver && receiver.sender && receiver.sender.FlipVerticallyBeforeEncode && receiver.transport &&
                ui && ui.controller == controller && ui.receiver == receiver && ui.transport == receiver.transport,
            "Saved Pet receiver does not match the Bridge sender and menu.");
        receiver.enabled = false; // Only the headset acquisition loop is replaced by the fixture.
        receiver.discoveryMode = false;
        receiver.RecognitionAccepted += _ => accepted++;
        receiver.sender.SceneProfile = (byte)((int)controller.scenario + 1);
        var transport = receiver.transport;
        transport.AutoConnect = false;
        transport.DisconnectSocket();
        SetTransportField(transport, "m_AutoReconnect", false);
        SetTransportField(transport, "m_Host", IPAddress.Loopback.ToString());
        SetTransportField(transport, "m_Port", 5005);
        transport.LineReceived += Receive;
        transport.ConnectIfNeeded();
        photo = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (Calibrated) LoadMetricFixture();
        trace.Add("Saved Pet scene bound its original sender, TCP transport, receiver and shared menu.");
    }

    static void SetTransportField(Perception.TcpClient transport, string field, object value)
    {
        var info = typeof(Perception.TcpClient).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        Require(info != null, "Missing saved TCP field " + field);
        info.SetValue(transport, value);
    }

    static void Send()
    {
        if (!Calibrated)
            Require(photo.LoadImage(File.ReadAllBytes(Path.Combine(Input, $"{frame + 1:D2}.png"))) &&
                    photo.width == 640 && photo.height == 480, "Pet optical fixture has the wrong dimensions.");
        var inputPhoto = Calibrated ? metricPhotos[frame % metricPhotos.Length] : photo;
        if (!receiver.sender.TrySendTexture(inputPhoto, respectSenderRateLimit: false)) return;
        waiting = true;
        sentAt = Time.realtimeSinceStartup;
        CalibratedParkReplayChecks.Set(receiver, "waiting", true);
        CalibratedParkReplayChecks.Set(receiver, "expected", receiver.sender.LastSentFrameId);
        CalibratedParkReplayChecks.Set(receiver, "capturedAt", sentAt);
        // Public stereo colour images lack a metric calibration. Exercise both recognition-only fallbacks.
        CalibratedParkReplayChecks.Set(receiver, "capturePoseValid", Calibrated || frame >= 6);
        CalibratedParkReplayChecks.Set(receiver, "headTracked", Calibrated || frame < 6);
        if (Calibrated) SetMetricPair();
    }

    static void Receive(string raw)
    {
        try
        {
            var packet = JsonUtility.FromJson<DetectionFrameMessage>(raw);
            replies.Add(raw);
            Require(waiting && packet.frame_id == receiver.sender.LastSentFrameId &&
                    packet.timestamp == receiver.sender.LastSentTimestampMs && packet.scene == "pet" &&
                    packet.status == "OK" && packet.width == 640 && packet.height == 480 &&
                    raw.Contains(Weights), "Live Pet reply does not match its sent frame and YOLO weights.");
            waiting = false;
            nextFrame = Time.realtimeSinceStartup + .12f;
            if (!warm)
            {
                warm = true;
                return;
            }
            if (Calibrated)
            {
                ObserveMetricReply(packet, raw);
                frame++;
                return;
            }

            string species = frame < 6 ? "Cat" : "Dog";
            var hit = packet.detections.Single(item => item.class_name == species);
            Require(!string.IsNullOrEmpty(hit.instance_id) && hit.confidence > .5f, "Pet positive missing a confident tracked instance.");
            if (frame is 0 or 6) id = hit.instance_id;
            Require(hit.instance_id == id, "The live detector changed identity within the optical sequence.");
            receiver.Receive(raw);
            var status = frame < 6 ? "CAPTURE_POSE_UNAVAILABLE: recognition only" : "HEAD_TRACKING_LOST: recognition only";
            Require(accepted == frame + 1 && receiver.Status == status && !controller.selected &&
                    !UnityEngine.Object.FindObjectsByType<ParkActor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .Any(actor => actor.gameObject.scene == controller.gameObject.scene && actor.instanceId == id),
                "A live reply was lost or promoted to an actor without a valid pose.");
            foreach (var effect in new[] { ParkAction.Style, ParkAction.Awaken, ParkAction.Poke, ParkAction.Reward })
                Require(!controller.Dispatch(effect, Vector3.forward, true), "Pet effect fired without a spatial target.");
            trace.Add(FormattableString.Invariant($"Frame {packet.frame_id}: {species} {id} {hit.confidence:F3}; {status}; four effects gated."));
            if (++frame == 12)
            {
                Require(errors == 0, "Unity logged errors during the saved Pet route.");
                Finish(0, "PASS: saved BridgeStereoFusion sent live Pet PICF, with stable Cat/Dog IDs and both spatial guards.");
            }
        }
        catch (Exception ex)
        {
            Finish(1, "FAIL: " + ex);
        }
    }
}
