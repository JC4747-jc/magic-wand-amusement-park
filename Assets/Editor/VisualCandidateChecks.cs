using System;
using System.Reflection;
using Perception;
using UnityEngine;

public static class VisualCandidateChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Set(object instance, string field, object value)
        => instance.GetType().GetField(field, Private).SetValue(instance, value);
    static void Invoke(object instance, string method, params object[] args)
        => instance.GetType().GetMethod(method, Private).Invoke(instance, args);
    static ObjectDetectionEvent Box(float confidence, float center = 320, float width = 40)
        => new ObjectDetectionEvent("Lighter", confidence, center, 240, 1,
            center - width / 2, 200, center + width / 2, 280);

    public static void Run()
    {
        var host = new GameObject("Visual candidate association regression");
        host.SetActive(false);
        try
        {
            var locator = host.AddComponent<StereoYoloLocator>();
            var manager = host.AddComponent<SingleTargetManager>();
            Set(manager, "m_StereoLocator", locator);
            manager.SetFrameSize(640, 480);
            Invoke(manager, "RebuildStrategies");
            long frame = 0;
            void Send(params ObjectDetectionEvent[] detections)
                => Invoke(manager, "OnDetectionFrame", new DetectionFrame(detections, ++frame, frame, 640, 480));
            void PrepareContinuation(ObjectDetectionEvent previous, float elapsed = .15f)
            {
                float captured = Time.realtimeSinceStartup - .02f;
                Set(locator, "capturedAt", captured);
                Set(locator, "verifiedCaptureAt", captured - elapsed);
                Set(locator, "verifiedDetection", previous);
            }

            Send(Box(.35f));
            Require(manager.State == TargetLockState.NO_TARGET, "Weak candidate started acquisition");
            Send(Box(.9f));
            Send(Box(.35f));
            Require(manager.State == TargetLockState.NO_TARGET, "Weak candidate completed initial lock");
            Send(Box(.9f)); Send(Box(.9f));
            Require(manager.State == TargetLockState.TARGET_LOCKED, "Strong candidate did not acquire");

            Set(locator, "tracking", true);
            var tabletop = (TabletopTargetLock)typeof(StereoYoloLocator).GetField("tabletopLock", Private).GetValue(locator);
            for (int i = 0; i < 5; i++) tabletop.Observe(Vector3.forward, i * .2f);
            // Model the synchronous Anchor -> stereo validation contract here. The
            // rendered-anchor checks exercise the actual stereo projection separately.
            bool stereoPassed = true;
            ObjectDetectionEvent emitted = default;
            manager.LockedTargetUpdated += evt =>
            {
                if (evt.state != TargetLockState.TARGET_LOCKED) return;
                emitted = evt.detection;
                if (stereoPassed)
                    Invoke(locator, "RememberVerifiedDetection", evt.detection, Vector3.forward);
            };

            PrepareContinuation(Box(.9f));
            Send(Box(.99f, 520), Box(.35f, 324));
            Require(manager.State == TargetLockState.TARGET_LOCKED && manager.LockedDetection.centerX == 324 &&
                emitted.confidence == .35f, "Higher-score distant box displaced associated weak target");

            PrepareContinuation(manager.LockedDetection);
            stereoPassed = false;
            Send(Box(.35f, 328));
            Require(manager.State == TargetLockState.TARGET_LOST && manager.LockedDetection.centerX == 324,
                "Stereo-rejected weak box poisoned matching identity");
            Set(manager, "m_LostElapsed", .25f);
            PrepareContinuation(manager.LockedDetection);
            Send(Box(.35f, 328));
            Require(manager.State == TargetLockState.TARGET_LOST && manager.LostElapsed == .25f &&
                manager.LockedDetection.centerX == 324, "Rejected weak reacquisition reset loss grace or identity");

            stereoPassed = true;
            PrepareContinuation(manager.LockedDetection);
            Send(Box(.99f, 520), Box(.35f, 328));
            Require(manager.State == TargetLockState.TARGET_LOCKED && manager.LostElapsed == 0 &&
                manager.LockedDetection.centerX == 328, "Verified weak continuation did not recover lost track");

            PrepareContinuation(manager.LockedDetection);
            Send(Box(.35f, 328, 120));
            Require(manager.State == TargetLockState.TARGET_LOST, "Wrong-size weak candidate continued target");
            PrepareContinuation(manager.LockedDetection, 1f);
            Send(Box(.35f, 328));
            Require(manager.State == TargetLockState.TARGET_LOST, "Old stereo evidence revived weak target");
            PrepareContinuation(manager.LockedDetection);
            tabletop.Reset();
            Send(Box(.35f, 328));
            Require(manager.State == TargetLockState.TARGET_LOST, "Registration reset retained weak continuation permission");

            Invoke(manager, "ClearToNoTarget");
            Send(Box(.35f, 328));
            Require(manager.State == TargetLockState.NO_TARGET, "Expired image lock acquired a weak candidate");
            locator.followMode = LighterFollowMode.HybridGrip;
            Send(Box(.35f)); Send(Box(.35f));
            Require(manager.State == TargetLockState.TARGET_LOCKED, "Visual candidate policy changed legacy hybrid acquisition");
            Debug.Log("VISUAL_CANDIDATE_CHECKS_PASS");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }
}
