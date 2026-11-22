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

public static class StatueCalibratedDepthChecks
{
    const string Input = ".codex-tmp/statue-calibrated-depth-fixture";
    const string Output = ".codex-tmp/statue-calibrated-depth-checks";

    [Serializable] sealed class Setup
    {
        public float[] intrinsics;
        public float baselineMetres;
        public float expectedDepth;
        public string status;
    }

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        var trace = new List<string>();
        try
        {
            foreach (string path in Directory.GetDirectories(Input).OrderBy(path => path))
                Check(path, trace);
            File.WriteAllLines(Path.Combine(Output, "trace.txt"), trace);
            File.WriteAllText(Path.Combine(Output, "result.txt"), "PASS: saved ParkStatue calibrated PICF depth and selection.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            trace.Add(error.ToString());
            File.WriteAllLines(Path.Combine(Output, "trace.txt"), trace);
            File.WriteAllText(Path.Combine(Output, "result.txt"), "FAIL: " + error.Message);
            Debug.LogException(error);
            EditorApplication.Exit(2);
        }
    }

    static void Check(string input, List<string> trace)
    {
        string pair = Path.GetFileName(input);
        var config = JsonUtility.FromJson<Setup>(File.ReadAllText(Path.Combine(input, "input.json")));
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/ParkStatue.unity");
        var controller = scene.GetRootGameObjects()
            .SelectMany(obj => obj.GetComponentsInChildren<ParkController>(true)).Single();
        var receiver = controller.receiver as ParkStereoReceiver;
        Require(receiver && receiver.controller == controller && receiver.sender && receiver.transport
            && receiver.sender.FlipVerticallyBeforeEncode && controller.Profile == "statue",
            "ParkStatue no longer binds to its saved stereo receiver");
        Require(receiver.minDepth == .2f && receiver.maxDepth == 3f, "ParkStatue interaction range changed");
        Require(controller.Allows("Statue") && controller.Allows("BronzeBeast"), "ParkStatue class policy changed");
        controller.simulation = false;
        receiver.enabled = false;
        receiver.discoveryMode = false;
        Set(receiver, "left", File.ReadAllBytes(Path.Combine(input, "left.gray")));
        Set(receiver, "right", File.ReadAllBytes(Path.Combine(input, "right.gray")));
        float[] k = config.intrinsics;
        Set(receiver, "intrinsics", new Vector4(k[0], k[1], k[2], k[3]));
        Set(receiver, "baseline", config.baselineMetres);
        Set(receiver, "capturePose", Pose.identity);
        Set(receiver, "cameraPose", Pose.identity);
        Set(receiver, "capturePoseValid", true);
        Set(receiver, "headTracked", true);

        int accepted = 0;
        string id = null;
        ParkActor actor = null;
        receiver.RecognitionAccepted += _ => accepted++;
        for (int frameId = 1; frameId <= 3; frameId++)
        {
            string raw = File.ReadAllText(Path.Combine(input, $"reply-{frameId}.json"));
            var frame = JsonUtility.FromJson<DetectionFrameMessage>(raw);
            Require(frame.frame_id == frameId && frame.scene == "statue" && frame.status == "OK",
                $"{pair} invalid saved PICF packet");
            Set(receiver, "expected", frame.frame_id);
            Set(receiver, "waiting", true);
            Set(receiver, "capturedAt", Time.realtimeSinceStartup);
            receiver.Receive(raw);
            actor = scene.GetRootGameObjects().SelectMany(obj => obj.GetComponentsInChildren<ParkActor>(true))
                .SingleOrDefault(target => target.tracked && !target.virtualTarget);
            trace.Add($"{pair} frame={frame.frame_id} status={receiver.Status} tracked={(actor != null)}");
            Require(receiver.Status.StartsWith(config.status, StringComparison.Ordinal), $"{pair} unexpected depth status");
            Require((actor != null) == (config.expectedDepth > 0 && frameId == 3) && !controller.selected,
                $"{pair} target confirmation or selection changed");
            if (config.expectedDepth > 0)
            {
                string current = frame.detections.Single().instance_id;
                id ??= current;
                Require(id == current, $"{pair} raw PICF identity changed");
            }
        }
        Require(accepted == 3, $"{pair} receiver rejected the original reply");
        if (!actor) return;
        Vector3 point = actor.transform.position;
        Vector3 optical = StereoFusionGeometry.HeadToOptical(point, Pose.identity);
        Require(actor.instanceId == id && Mathf.Abs(optical.z - config.expectedDepth) < .003f,
            $"{pair} actor identity or depth disagrees with the production matcher");
        Vector3 direction = point.normalized;
        Vector3 tip = point - direction * .09f;
        var hand = TabletopHandSample.Create(1f, tip - direction * .06f, tip, tip + Vector3.right * .02f,
            tip - direction * .04f, new Vector4(.95f, .55f, .55f, .55f), Vector3.right, .02f);
        Require(controller.ProcessHandSample(hand, true) == EditDimension.None && controller.selected == actor,
            $"{pair} synthetic point and pinch did not select the recognized statue");
        trace.Add(FormattableString.Invariant(
            $"{pair} selected={actor.instanceId} opticalZ={optical.z:F4} worldZ={point.z:F4} syntheticHand=True"));
    }

    static void Set(ParkStereoReceiver receiver, string name, object value)
    {
        var field = typeof(ParkStereoReceiver).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Require(field != null, "Missing receiver field " + name);
        field.SetValue(receiver, value);
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
