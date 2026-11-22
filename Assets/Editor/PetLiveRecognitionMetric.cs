using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicMR;
using Perception;
using UnityEngine;

public static partial class PetLiveRecognitionChecks
{
    const string CandidateWeights = "85a76fe86dd8afe384648546b56a7a78580c7cb7b404fc595f97969322d502d5";
    const float HandCadence = .03f;
    static MetricFixture optics;
    static Texture2D[] metricPhotos;
    static byte[][] leftGray, rightGray;
    static ParkActor metricActor;
    static Transform dream;
    static readonly Dictionary<string, Texture2D> metricCaptures = new();
    static readonly EditDimension[] spells = { EditDimension.Appearance, EditDimension.Agency,
        EditDimension.Rule, EditDimension.Deconstruction };
    static TabletopHandSample[] handSamples;
    static TabletopHandSample heldMetricHand;
    static float nextHand, nextSpell, lossStart = -1;
    static int spell, handIndex, gestureEvents, recoveryStart;
    static bool withholdMetricFrames, recovering;
    static readonly Vector4 OpenHand = Vector4.one * .95f;
    static readonly Vector4 Point = new(.95f, .55f, .55f, .55f);

    [Serializable]
    sealed class MetricFixture
    {
        public float[] intrinsics;
        public float baseline;
        public int[] sourceFrames;
        public float[] lidarM;
    }

    static void LoadMetricFixture()
    {
        optics = JsonUtility.FromJson<MetricFixture>(File.ReadAllText(Path.Combine(Input, "input.json")));
        Require(optics.sourceFrames.Length == 3 && optics.lidarM.Length == 3 && optics.intrinsics.Length == 4 &&
                optics.baseline > 0f, "Expected three calibrated source frames and independent LiDAR readings.");
        metricPhotos = new Texture2D[3];
        leftGray = new byte[3][];
        rightGray = new byte[3][];
        for (int i = 0; i < 3; i++)
        {
            var dir = Path.Combine(Input, optics.sourceFrames[i].ToString("D5"));
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            metricPhotos[i] = texture;
            Require(texture.LoadImage(File.ReadAllBytes(Path.Combine(dir, "sender.png"))) &&
                    texture.width == 640 && texture.height == 480, "Expected a rectified colour frame.");
            leftGray[i] = File.ReadAllBytes(Path.Combine(dir, "left.gray"));
            rightGray[i] = File.ReadAllBytes(Path.Combine(dir, "right.gray"));
        }
        var k = optics.intrinsics;
        CalibratedParkReplayChecks.Set(receiver, "intrinsics", new Vector4(k[0],k[1],k[2],k[3]));
        CalibratedParkReplayChecks.Set(receiver, "baseline", optics.baseline);
        CalibratedParkReplayChecks.Set(receiver, "capturePose", new Pose(Vector3.zero, Quaternion.identity));
        CalibratedParkReplayChecks.Set(receiver, "cameraPose", new Pose(Vector3.zero, Quaternion.Euler(180,0,0)));
        trace.Add("Calibrated offline stereo with independent LiDAR reference and supplied desktop pose.");
    }

    static void SetMetricPair()
    {
        int i = frame % 3;
        CalibratedParkReplayChecks.Set(receiver, "left", leftGray[i]);
        CalibratedParkReplayChecks.Set(receiver, "right", rightGray[i]);
    }

    static void ObserveMetricReply(DetectionFrameMessage packet, string raw)
    {
        var hit = packet.detections.Single(d => d.class_name == MetricSpecies && d.confidence >= .5f);
        Require(packet.detections.All(d => d.class_name == MetricSpecies || d.confidence < .5f),
            "The candidate emitted a competing high-confidence wrong species.");
        if (frame == 0) id = hit.instance_id;
        Require(!string.IsNullOrEmpty(id) && hit.instance_id == id, "Live metric pet ID changed.");
        int i = frame % 3;
        int x = Mathf.RoundToInt((hit.x1 + hit.x2) * .5f);
        int y = Mathf.RoundToInt((hit.y1 + hit.y2) * .5f);
        int spreadX = Mathf.Min(24, Mathf.FloorToInt((hit.x2 - hit.x1) * .3f) - 5);
        int spreadY = Mathf.Min(16, Mathf.FloorToInt((hit.y2 - hit.y1) * .3f) - 4);
        var measured = StereoPatchMatcher.EstimateDepth(leftGray[i], rightGray[i], 640, 480,
            x, y, optics.intrinsics[0], optics.baseline, spreadX, spreadY);
        Require(measured.valid && measured.acceptedSamples >= 5 && measured.confidence >= .04f &&
                Mathf.Abs(measured.depthMeters - optics.lidarM[i]) <= .20f, "Stereo centre differs from the LiDAR reference.");
        int before = accepted;
        receiver.Receive(raw);
        Require(accepted == before + 1 && receiver.Status.StartsWith("TRANSLATION_ONLY:", StringComparison.Ordinal),
            "Saved receiver rejected the metric frame: " + receiver.Status);
        var actor = UnityEngine.Object.FindObjectsByType<ParkActor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(a => a.instanceId == id);
        Require(!actor.virtualTarget && actor.species == MetricSpecies && Mathf.Abs(actor.transform.position.z - measured.depthMeters) < .16f,
            "Original receiver did not deliver the expected physical translation.");
        if (frame < 3)
        {
            Require(actor.tracked == (frame == 2) && !controller.selected, "Target bypassed the three-observation or user selection gate.");
            trace.Add(FormattableString.Invariant(
                $"Source {optics.sourceFrames[i]} sent as PICF {packet.frame_id}: {hit.instance_id}, patches={measured.acceptedSamples}/9, stereo={measured.depthMeters:F3}m, LiDAR={optics.lidarM[i]:F3}m."));
        }
        metricActor = actor;
    }

    static TabletopHandSample MetricHand(Vector4 fingers, float pinch, Vector3 offset)
    {
        Vector3 point = metricActor.InteractionCenter + offset;
        return TabletopHandSample.Create(Time.unscaledTime, point - Vector3.up * .03f, point,
            point + Vector3.right * pinch, point - Vector3.forward * .04f, fingers, Vector3.right, pinch);
    }

    static void StepMetric()
    {
        if (!metricActor || frame < 3) return;
        float now = Time.unscaledTime;
        if (!controller.selected && lossStart < 0)
        {
            Require(metricActor.tracked && !metricActor.virtualTarget, "Only a tracked physical actor can be selected.");
            Require(controller.ProcessHandSample(MetricHand(Point,.02f,-Vector3.forward*.09f), true) == EditDimension.None &&
                    controller.selected == metricActor, "Shared point/pinch did not select the live pet.");
            dream = metricActor.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Dream bubble");
            Require(metricActor.ConfirmOrientation(0f), "Manual orientation confirmation failed.");
            nextSpell = now + .35f;
            trace.Add($"Selected the actual tracked {MetricSpecies} with desktop point/pinch.");
            return;
        }
        if (lossStart >= 0)
        {
            if (now - lossStart < .9f) return;
            if (!recovering)
            {
                Require(!metricActor.tracked && !metricActor.visual.gameObject.activeInHierarchy &&
                    metricActor.PetComic && metricActor.Affection == 1 && !dream.gameObject.activeInHierarchy &&
                    !controller.DispatchDimension(EditDimension.Appearance, metricActor.InteractionCenter, true),
                    "Tracking loss failed to hide the Pet, preserve its state or gate new input.");
                trace.Add("Withheld frames; virtual Pet became hidden and refused interaction while keeping affection and style.");
                recovering = true;
                recoveryStart = frame;
                withholdMetricFrames = false;
                return;
            }
            if (frame - recoveryStart < 3) return;
            Require(metricActor.tracked && controller.selected == metricActor && metricActor.PetComic && metricActor.Affection == 1,
                "The same Pet did not recover with its saved state.");
            Require(controller.Dispatch(ParkAction.Reset,metricActor.InteractionCenter,true) &&
                !metricActor.PetComic && metricActor.Affection == 0, "Pet Reset left stale effects.");
            Require(errors == 0, "Unity logged an error in the calibrated pet case.");
            trace.Add($"Same ID recovered after three fresh stereo frames; Reset cleared the effects after {frame} warm replies.");
            Finish(0, $"PASS: saved BridgeStereoFusion located the live calibrated {MetricSpecies} and completed Pet A/B/C/D.");
            return;
        }
        if (handSamples != null)
        {
            if (now < nextHand) return;
            var sample = handSamples[handIndex++];
            sample.gesture.time = now;
            var result = controller.ProcessHandSample(sample,true);
            heldMetricHand = sample;
            if (result != EditDimension.None)
            {
                Require(result == spells[spell] && controller.LastMagicAccepted,
                    $"Shared gesture {spells[spell]} failed: {controller.InteractionStatus}");
                trace.Add($"Recognized {result} at {now:F3}s, tracked={metricActor.tracked}, revision={metricActor.Revision}, feedback={metricActor.Feedback}.");
                gestureEvents++;
            }
            nextHand = now + HandCadence;
            if (handIndex != handSamples.Length) return;
            Require(gestureEvents == 1, "Expected one accepted event for " + spells[spell]);
            handSamples = null;
            spell++;
            nextSpell = now + .95f;
            return;
        }
        if (now < nextSpell || metricActor.Busy || waiting)
        {
            if (spell > 0 && now >= nextHand)
            {
                // Keep sending the stationary hand through the spell transition.
                // A gap is interpreted as hands lost and correctly cancels the animation.
                heldMetricHand.gesture.time = now;
                Require(controller.ProcessHandSample(heldMetricHand,true) == EditDimension.None,
                    "A stationary hand sent an extra gesture.");
                nextHand = now + HandCadence;
            }
            return;
        }
        Require(metricActor.tracked && controller.selected == metricActor, "The selected live pet left tracking during the interaction.");
        if (spell > 0) VerifyMetricEffect(spell - 1);
        if (spell == spells.Length)
        {
            withholdMetricFrames = true;
            lossStart = now;
            return;
        }
        BeginMetricSpell();
    }

    static void BeginMetricSpell()
    {
        controller.ProcessHandSample(default, false);
        Vector3 near = Vector3.up * .035f;
        var samples = new List<TabletopHandSample> { MetricHand(OpenHand,.08f,near), MetricHand(OpenHand,.08f,near) };
        switch (spells[spell])
        {
            case EditDimension.Appearance:
                for (int n = 0; n < 8; n++) samples.Add(MetricHand(OpenHand,.02f,near));
                samples.Add(MetricHand(OpenHand,.08f,near));
                break;
            case EditDimension.Agency:
                for (int n = 0; n <= 32; n++)
                {
                    float angle = n / 32f * Mathf.PI * 2;
                    samples.Add(MetricHand(Point,.08f,near + new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*.04f));
                }
                break;
            case EditDimension.Rule:
                for (int n = 1; n <= 6; n++) samples.Add(MetricHand(OpenHand,.08f,near + Vector3.right * (n*.029f)));
                break;
            case EditDimension.Deconstruction:
                int count = Mathf.CeilToInt(TabletopGestureRules.FistMinimumSeconds / HandCadence) + 1;
                for (int n = 0; n < count; n++) samples.Add(MetricHand(Vector4.one*.55f,.02f,near));
                samples.Add(MetricHand(OpenHand,.08f,near));
                break;
        }
        handSamples = samples.ToArray();
        handIndex = gestureEvents = 0;
        nextHand = Time.unscaledTime;
    }

    static void VerifyMetricEffect(int completed)
    {
        string label = spells[completed].ToString();
        if (metricCaptures.ContainsKey(label)) return;
        var outline = metricActor.GetComponentInChildren<NaturalPetOutline>();
        bool bubble = dream.gameObject.activeInHierarchy && dream.localScale.sqrMagnitude > .001f;
        int enabledShells = outline ? outline.GetComponentsInChildren<Renderer>(true)
            .Count(r => r.name == "Manga silhouette" && r.enabled) : 0;
        CaptureMetric(label);
        Require(metricActor.PetComic && outline && enabledShells > 0,
            $"A did not enable the manga outline: comic={metricActor.PetComic}, active={metricActor.visual.gameObject.activeInHierarchy}, tracked={metricActor.tracked}, shells={enabledShells}/{(outline ? outline.ShellCount : 0)}, revision={metricActor.Revision}, feedback={metricActor.Feedback}.");
        Require(bubble == (completed == 1), $"Dream was {(bubble ? "visible" : "hidden")} after {label}.");
        Require(metricActor.Affection == (completed == 3 ? 1 : 0), "Expected one affection increment only after D.");
        trace.Add($"{label}: outline={metricActor.PetComic}, dream={bubble}, affection={metricActor.Affection}, live {MetricSpecies}={id}.");
    }

    static void CaptureMetric(string label)
    {
        var camera = Camera.main;
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        var originalPosition = camera.transform.position;
        var originalRotation = camera.transform.rotation;
        float originalFov = camera.fieldOfView;
        var canvases = camera.GetComponentsInChildren<Canvas>().Where(canvas => canvas.enabled).ToArray();
        var render = RenderTexture.GetTemporary(800,600,24,RenderTextureFormat.ARGB32);
        try
        {
            foreach (var canvas in canvases) canvas.enabled = false;
            Vector3 subject = metricActor.InteractionCenter + Vector3.up * .10f;
            camera.transform.position = subject + new Vector3(.48f,.22f,-.85f);
            camera.transform.LookAt(subject);
            camera.fieldOfView = 41f;
            camera.targetTexture = render;
            camera.Render();
            RenderTexture.active = render;
            var image = new Texture2D(800,600,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(0,0,800,600),0,0);
            metricCaptures.Add(label,image);
        }
        finally
        {
            camera.targetTexture = previous;
            camera.transform.SetPositionAndRotation(originalPosition,originalRotation);
            camera.fieldOfView = originalFov;
            foreach (var canvas in canvases) canvas.enabled = true;
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(render);
        }
    }

    static void SaveMetricCaptures()
    {
        if (metricPhotos != null)
            foreach (var texture in metricPhotos)
                if (texture) UnityEngine.Object.DestroyImmediate(texture);
        foreach (var capture in metricCaptures)
        {
            File.WriteAllBytes(Path.Combine(Output,capture.Key + ".png"),capture.Value.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(capture.Value);
        }
    }
}
