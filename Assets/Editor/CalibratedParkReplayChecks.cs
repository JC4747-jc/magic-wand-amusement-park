using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicMR;
using Perception;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Calls the saved scene's receiver with real replies and offline calibration.
// Pending-frame state and head/camera poses are explicit test fixtures.
[InitializeOnLoad]
public static class CalibratedParkReplayChecks
{
    const string Running = "MagicMR.CalibratedParkReplay";
    const string Furniture = "MagicMR.CalibratedParkFurnitureReplay";
    const int Width = 640, Height = 480;
    static readonly string[] Datasets = { "Playroom", "Jadeplant" };
    static readonly string[] Scenes = { "ParkRoom", "ParkPlant" };
    static readonly string[] FurnitureDatasets = { "Couch-detail", "Couch-depth", "Classroom1" };
    static readonly Pose HeadPose = new Pose(new Vector3(.12f, -.04f, .3f), Quaternion.Euler(0f, 20f, 0f));
    static readonly Pose CameraPose = new Pose(new Vector3(.03f, .01f, 0f), Quaternion.identity);
    static bool IsFurniture => SessionState.GetBool(Furniture, false);
    static string Input => Path.GetFullPath(IsFurniture ? ".codex-tmp/park-furniture-fixture" : ".codex-tmp/park-calibrated-fixture");
    static string Output => Path.GetFullPath(IsFurniture ? ".codex-tmp/park-furniture-checks" : ".codex-tmp/park-calibrated-checks");
    static int sceneIndex;
    static double settle;

    [Serializable]
    sealed class InputConfig
    {
        public float[] intrinsics;
        public float baselineMetres;
        public float[] hostSeconds;
    }

    static CalibratedParkReplayChecks() => EditorApplication.update += Tick;

    public static void Run() => StartReplay(false);
    public static void RunFurniture() => StartReplay(true);

    static void StartReplay(bool furniture)
    {
        SessionState.SetBool(Furniture, furniture);
        sceneIndex = 0;
        settle = 0;
        if (!Directory.Exists(Input) || Directory.Exists(Output))
            throw new InvalidOperationException("Expected a calibrated input fixture and a fresh output directory.");
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "result.txt"), "RUNNING");
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene(ScenePath(Scenes[0]));
        EditorApplication.EnterPlaymode();
    }

    static string ScenePath(string name) => $"Assets/Scenes/{name}.unity";

    static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying ||
            !Application.isPlaying || EditorApplication.timeSinceStartup < settle) return;
        try
        {
            var scene = SceneManager.GetActiveScene();
            int count = IsFurniture ? FurnitureDatasets.Length : Scenes.Length;
            Require(scene.path == ScenePath(IsFurniture ? Scenes[0] : Scenes[sceneIndex]), "Unexpected saved scene.");
            if (IsFurniture) CheckFurniture(scene, FurnitureDatasets[sceneIndex]);
            else CheckScene(scene, Datasets[sceneIndex]);
            if (++sceneIndex < count)
            {
                settle = EditorApplication.timeSinceStartup + .3;
                EditorSceneManager.LoadSceneInPlayMode(ScenePath(IsFurniture ? Scenes[0] : Scenes[sceneIndex]), new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
            Finish(0, IsFurniture
                ? "PASS: saved ParkRoom furniture holdout PICF replay; see measured target and matcher outcomes."
                : "PASS: saved ParkRoom and ParkPlant public-image PICF replay; see both measured outcomes.");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            Finish(2, "FAIL: " + error);
        }
    }

    static void Finish(int code, string message)
    {
        File.WriteAllText(Path.Combine(Output, "result.txt"), message);
        SessionState.SetBool(Running, false);
        SessionState.SetBool(Furniture, false);
        EditorApplication.Exit(code);
    }

    static void Require(bool state, string message)
    {
        if (!state) throw new InvalidOperationException(message);
    }

    internal static void Set(ParkStereoReceiver receiver, string name, object value)
    {
        var field = typeof(ParkStereoReceiver).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Require(field != null, "Receiver test field missing: " + name);
        field.SetValue(receiver, value);
    }

    internal static void Supply(ParkStereoReceiver receiver, string raw, float age, bool hasPose = true)
    {
        var reply = JsonUtility.FromJson<DetectionFrameMessage>(raw);
        Set(receiver, "waiting", true);
        Set(receiver, "expected", reply.frame_id);
        Set(receiver, "capturedAt", Time.realtimeSinceStartup - age);
        Set(receiver, "capturePoseValid", hasPose);
        Set(receiver, "headTracked", true);
        receiver.Receive(raw);
    }

    static ParkActor FindActor(ParkController controller, string id) =>
        controller.gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ParkActor>(true))
            .FirstOrDefault(a => a.instanceId == id);

    static (ParkController controller, ParkStereoReceiver receiver) ConfigureRoomReceiver(Scene scene)
    {
        var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ParkController>(true)).Single();
        var receiver = controller.receiver as ParkStereoReceiver;
        Require(receiver && receiver.controller == controller && receiver.sender && receiver.transport,
            "Saved scene receiver wiring is incomplete.");
        Require(receiver.sender.FlipVerticallyBeforeEncode, "Fixture's raw byte-row convention differs from saved sender.");
        receiver.enabled = false; // Prevent disconnected/live headset Update from expiring the replay midway.
        receiver.discoveryMode = false; // Exercise the saved per-scene Room/Plant profile.
        controller.simulation = false;
        return (controller, receiver);
    }

    static void ConfigureStereo(ParkStereoReceiver receiver, byte[] left, byte[] right, Vector4 k, float baseline)
    {
        Require(left.Length == Width * Height && right.Length == left.Length, "Replay image sizes differ from the saved input size.");
        Set(receiver, "left", left);
        Set(receiver, "right", right);
        Set(receiver, "intrinsics", k);
        Set(receiver, "baseline", baseline);
        Set(receiver, "capturePose", HeadPose);
        Set(receiver, "cameraPose", CameraPose);
    }

    static void CheckFurniture(Scene scene, string dataset)
    {
        var (controller, receiver) = ConfigureRoomReceiver(scene);
        var path = Path.Combine(Input, dataset);
        var cfg = JsonUtility.FromJson<InputConfig>(File.ReadAllText(Path.Combine(path, "input.json")));
        var k = new Vector4(cfg.intrinsics[0], cfg.intrinsics[1], cfg.intrinsics[2], cfg.intrinsics[3]);
        var left = File.ReadAllBytes(Path.Combine(path, "left.gray"));
        var right = File.ReadAllBytes(Path.Combine(path, "right.gray"));
        var reference = File.ReadAllBytes(Path.Combine(path, "reference-depth.f32"));
        Require(reference.Length == Width * Height * sizeof(float), "Invalid holdout depth map.");
        ConfigureStereo(receiver, left, right, k, cfg.baselineMetres);
        var lines = new List<string>();
        int recognized = 0;
        receiver.RecognitionAccepted += _ => recognized++;
        for (int i = 1; i <= 4; i++)
        {
            var raw = File.ReadAllText(Path.Combine(path, $"{i}.json"));
            var reply = JsonUtility.FromJson<DetectionFrameMessage>(raw);
            Require(reply.scene == controller.Profile && reply.frame_id == i, "Holdout reply source does not match the saved room.");
            if (i == 1) Supply(receiver, raw, .04f, false);
            else
            {
                Require(cfg.hostSeconds[i-1] > 0 && cfg.hostSeconds[i-1] < receiver.maxAge, "Warm holdout reply is stale.");
                Supply(receiver, raw, cfg.hostSeconds[i-1]);
            }
            Require(!controller.selected, "Holdout replay bypassed user selection.");
            lines.Add($"reply={i} status={receiver.Status}");
            foreach (var target in reply.detections)
            {
                string label = string.IsNullOrEmpty(target.class_name) ? target.@class : target.class_name;
                Require(!string.IsNullOrEmpty(target.instance_id), "Holdout proposal is missing its original ID.");
                var actor = FindActor(controller, target.instance_id);
                Require(!actor || !actor.virtualTarget, "Holdout reply created a virtual actor.");
                lines.Add(FormattableString.Invariant($"target={label} confidence={target.confidence:R} tracked={(actor && actor.tracked)}"));
                if (i != 4) continue;
                int u = Mathf.RoundToInt((target.x1 + target.x2) * .5f);
                int v = Mathf.RoundToInt((target.y1 + target.y2) * .5f);
                int sx = Mathf.Min(24, Mathf.FloorToInt((target.x2 - target.x1) * .3f) - 5);
                int sy = Mathf.Min(16, Mathf.FloorToInt((target.y2 - target.y1) * .3f) - 4);
                var samples = new StereoPatchMatcher.Sample[9];
                var estimate = StereoPatchMatcher.EstimateDepth(left, right, Width, Height, u, v, k.x, cfg.baselineMetres, sx, sy, samples);
                lines.Add(FormattableString.Invariant($"matcherValid={estimate.valid} inliers={estimate.acceptedSamples} metres={estimate.depthMeters:R} minAccepted={receiver.minDepth:R} maxAccepted={receiver.maxDepth:R}"));
                foreach (var sample in samples.Where(s => s.inlier))
                {
                    float truth = BitConverter.ToSingle(reference, (sample.y * Width + sample.x) * sizeof(float));
                    lines.Add(FormattableString.Invariant($"sample={sample.x},{sample.y} estimateMetres={k.x * cfg.baselineMetres / sample.disparity:R} referenceMetres={truth:R}"));
                }
            }
        }
        Require(recognized == 4, "Saved Room receiver did not see every original holdout reply.");
        File.WriteAllLines(Path.Combine(Output, dataset + ".txt"), lines);
    }

    static void CheckScene(Scene scene, string dataset)
    {
        var (controller, receiver) = ConfigureRoomReceiver(scene);
        var path = Path.Combine(Input, dataset);
        var cfg = JsonUtility.FromJson<InputConfig>(File.ReadAllText(Path.Combine(path, "input.json")));
        var raw = Enumerable.Range(1, 4).Select(i => File.ReadAllText(Path.Combine(path, $"{i}.json"))).ToArray();
        var first = JsonUtility.FromJson<DetectionFrameMessage>(raw[0]);
        var last = JsonUtility.FromJson<DetectionFrameMessage>(raw[3]);
        Require(first.scene == controller.Profile && last.scene == controller.Profile && last.detections.Length == 1,
            "Actual reply is outside the intended scene or target scope.");
        var target = last.detections[0];
        string id = target.instance_id;
        Require(!string.IsNullOrEmpty(id), "Detector reply did not provide an instance ID.");
        var k = new Vector4(cfg.intrinsics[0], cfg.intrinsics[1], cfg.intrinsics[2], cfg.intrinsics[3]);
        var left = File.ReadAllBytes(Path.Combine(path, "left.gray"));
        var right = File.ReadAllBytes(Path.Combine(path, "right.gray"));
        ConfigureStereo(receiver, left, right, k, cfg.baselineMetres);

        int recognized = 0;
        receiver.RecognitionAccepted += _ => recognized++;
        // Use the original public source response that does not have the runtime frame size.
        Supply(receiver, File.ReadAllText(Path.Combine(path, "original-size.json")), .04f);
        Require(recognized == 0 && !FindActor(controller, id), "Non-native frame entered recognition.");
        Supply(receiver, raw[0], cfg.hostSeconds[0], false);
        Require(recognized == 1 && receiver.Status.StartsWith("CAPTURE_POSE_UNAVAILABLE") && !FindActor(controller, id),
            "Without a capture pose, a fresh reply must remain recognition only.");
        Supply(receiver, raw[1], receiver.maxRecognitionAge + .1f);
        Require(recognized == 1 && !FindActor(controller, id),
            "An expired reply entered recognition.");

        var samples = new StereoPatchMatcher.Sample[9];
        int u = Mathf.RoundToInt((target.x1 + target.x2) * .5f);
        int v = Mathf.RoundToInt((target.y1 + target.y2) * .5f);
        int sx = Mathf.Min(24, Mathf.FloorToInt((target.x2 - target.x1) * .3f) - 5);
        int sy = Mathf.Min(16, Mathf.FloorToInt((target.y2 - target.y1) * .3f) - 4);
        var estimate = StereoPatchMatcher.EstimateDepth(left, right, Width, Height, u, v, k.x,
            cfg.baselineMetres, sx, sy, samples);
        var statuses = new List<string>();
        var tracked = new List<bool>();
        for (int i = 1; i < raw.Length; i++)
        {
            Require(cfg.hostSeconds[i] > 0 && cfg.hostSeconds[i] < receiver.maxAge,
                "Warm host reply exceeded the spatial freshness gate.");
            Supply(receiver, raw[i], cfg.hostSeconds[i]);
            statuses.Add(receiver.Status);
            var observed = FindActor(controller, id);
            tracked.Add(observed && observed.tracked);
        }
        Require(recognized == 4 && !tracked[0] && !tracked[1], "Recognition or three-frame target confirmation regressed.");
        var actor = FindActor(controller, id);
        var reference = File.ReadAllBytes(Path.Combine(path, "reference-depth.f32"));
        Require(reference.Length == Width * Height * sizeof(float), "Invalid benchmark depth map.");
        var sampleLines = new List<string>();
        var errors = new List<float>();
        foreach (var sample in samples.Where(s => s.inlier))
        {
            float z = k.x * cfg.baselineMetres / sample.disparity;
            float truth = BitConverter.ToSingle(reference, (sample.y * Width + sample.x) * sizeof(float));
            if (float.IsFinite(truth) && truth >= .2f && truth <= 3f) errors.Add(Mathf.Abs(z - truth));
            sampleLines.Add(FormattableString.Invariant($"{sample.x},{sample.y},{z:R},{truth:R}"));
        }
        if (tracked[2])
        {
            Require(actor && !actor.virtualTarget && !controller.selected && estimate.valid, "Tracked actor bypassed the selection boundary.");
            var optical = new Vector3((u-k.z)*estimate.depthMeters/k.x, (v-k.w)*estimate.depthMeters/k.y, estimate.depthMeters);
            var expected = HeadPose.position + HeadPose.rotation * StereoFusionGeometry.OpticalToHead(optical, CameraPose);
            Require(Vector3.Distance(expected, actor.transform.position) < .001f, "Receiver and calibrated world projection disagree.");
        }
        else
        {
            Require(!actor && statuses.All(s => s.StartsWith("DEPTH_UNRELIABLE") || s == "LOW_DETECTION_CONFIDENCE"),
                "A rejected public case reached Observe or an unanticipated rejection.");
        }
        if (dataset == "Jadeplant") Require(statuses.All(s => s == "LOW_DETECTION_CONFIDENCE"),
            "The replay's recorded plant confidence should be rejected by the spatial .5 gate.");
        Supply(receiver, raw[3], .04f);
        Require(recognized == 4, "A duplicate reply was accepted.");

        File.WriteAllLines(Path.Combine(Output, dataset + ".txt"), new[]
        {
            $"actualRecognitionEvents={recognized}",
            "rejections=original image size, missing capture pose, expired recognition, duplicate reply",
            "warmStatuses=" + string.Join(" | ", statuses),
            "trackedAfterEachWarmReply=" + string.Join(",", tracked),
            "confidence=" + target.confidence.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            FormattableString.Invariant($"matcherAcceptedSamples={estimate.acceptedSamples}; matcherDepthMetres={estimate.depthMeters:R}; comparableSamples={errors.Count}; meanAbsoluteSampleErrorMetres={(errors.Count>0?errors.Average():-1):R}"),
            "samples=x,y,estimatedMetres,referenceMetres",
        }.Concat(sampleLines));
    }
}
