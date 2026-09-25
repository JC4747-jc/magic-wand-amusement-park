using System;
using System.Reflection;
using Perception;
using UnityEngine;

public static class VisualAverageChecks
{
    static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Near(Vector3 actual, Vector3 expected, string message)
        => Require(Vector3.Distance(actual, expected) < .0001f, message);
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Fields).SetValue(obj, value);
    static void Invoke(object obj, string method) => obj.GetType().GetMethod(method, Fields).Invoke(obj, null);

    public static void Run()
    {
        var follow = new TabletopVisualFollow { AverageMeasurements = true };
        follow.Reset(Vector3.zero);
        Require(follow.Observe(Vector3.one * .01f, 1, 1.02f), "First visual sample rejected");
        Require(follow.Observe(Vector3.one * .02f, 1.1f, 1.12f), "Second visual sample rejected");
        Require(follow.Observe(Vector3.one * .03f, 1.2f, 1.22f), "Third visual sample rejected");
        Near(follow.Position, Vector3.one * .023f, "Three-axis weighted average incorrect");
        Require(!follow.Observe(Vector3.zero, 1.2f, 1.23f), "Duplicate capture changed average");
        Require(!follow.Observe(Vector3.zero, 1.19f, 1.23f), "Out-of-order capture changed average");
        Require(!follow.Observe(Vector3.zero, 1.3f, 1.7f), "Stale reply accepted");
        Near(follow.Position, Vector3.one * .023f, "Rejected frames moved the target");
        Require(!follow.Observe(Vector3.one, 1.3f, 1.32f), "Single outlier entered average");
        Require(follow.Observe(Vector3.one * .04f, 1.4f, 1.42f), "Normal trajectory failed to recover");
        Require(follow.Position.x < .04f && follow.Position.x > .03f, "Outlier polluted averaging window");
        Vector3 held = follow.Position;
        Require(!follow.Observe(Vector3.one * .04f, 2, 2.02f), "Long visual loss bypassed reacquisition");
        Near(follow.Position, held, "First returning frame moved the target");
        Require(!follow.Observe(Vector3.one * .045f, 2.1f, 2.12f), "Only two recovery samples accepted");
        Require(follow.Observe(Vector3.one * .05f, 2.2f, 2.22f), "Third recovery sample rejected");
        Near(follow.Position, Vector3.one * .05f, "Expired samples pulled recovered target backward");
        Require(follow.SampleCount == 1, "Recovery retained old averaging history");
        follow.Reset(Vector3.zero);
        Require(!follow.Observe(Vector3.right * .2f, 3, 3.02f) &&
            !follow.Observe(Vector3.right * .245f, 3.15f, 3.17f) &&
            follow.Observe(Vector3.right * .29f, 3.3f, 3.32f), "Moving jump never confirmed");
        Near(follow.Position, Vector3.right * .29f, "Confirmed jump blended old location");
        follow.Reset(Vector3.zero);
        for (int i = 0; i < 20; i++) follow.Observe(Vector3.right * (i % 2 == 0 ? .001f : -.001f), 4+i*.1f, 4+i*.1f);
        Near(follow.Position, Vector3.zero, "Static noise moved visual target");
        CheckRenderedVision();
        Debug.Log("VISUAL_AVERAGE_CHECKS_PASS");
    }

    // Exercise actual stereo projection, quality gates and displayed anchor with right hand nearby.
    static void CheckRenderedVision()
    {
        var host = new GameObject("Visual average regression"); host.SetActive(false);
        var displayed = new GameObject("Visual average displayed anchor");
        try
        {
            var locator = host.AddComponent<StereoYoloLocator>();
            var anchor = host.AddComponent<AnchorManager>();
            var hand = host.AddComponent<MagicMR.TabletopGestureRecognizer>();
            locator.detections = host.AddComponent<DetectionManager>();
            locator.sender = host.AddComponent<PicoYoloFrameSender>();
            Set(locator, "tracking", true); Set(locator, "anchorManager", anchor);
            Set(locator, "gestureRecognizer", hand);
            Set(hand, "visionHoldUntil", Time.unscaledTime + 10);
            Set(anchor, "m_Anchor", displayed.transform); Set(anchor, "m_HasTarget", true);
            Set(anchor, "m_ProjectionMode", AnchorProjectionMode.StereoDepth); Set(anchor, "m_StereoLocator", locator);
            Set(locator.sender, "m_FlipVerticallyBeforeEncode", true);
            Set(locator.sender, "<LastSentWidth>k__BackingField", 640);
            Set(locator.sender, "<LastSentHeight>k__BackingField", 480);
            Set(locator.detections, "<LastFrameWidth>k__BackingField", 640);
            Set(locator.detections, "<LastFrameHeight>k__BackingField", 480);
            Set(locator, "registeredWidth", .04f); Set(locator, "registeredHeight", .09f);
            Set(locator, "intrinsics", new Vector4(400, 400, 320, 240));
            Set(locator, "baseline", .036f);
            Set(locator, "cameraPose", new Pose(Vector3.zero, Quaternion.Euler(180,0,0)));
            Vector3 origin = Vector3.forward * .6f;
            displayed.transform.position = origin;
            var acquired = (TabletopTargetLock)typeof(StereoYoloLocator).GetField("tabletopLock", Fields).GetValue(locator);
            for (int i=0;i<5;i++) acquired.Observe(origin,i*.2f);
            var follow = (TabletopVisualFollow)typeof(StereoYoloLocator).GetField("follow", Fields).GetValue(locator);
            follow.Reset(origin);
            var left = new byte[640*480]; var right = new byte[left.Length]; new System.Random(42).NextBytes(left);
            for(int y=0;y<480;y++) for(int x=0;x<616;x++) right[y*640+x]=left[y*640+x+24];
            Set(locator, "left", left); Set(locator, "right", right);
            var det = new ObjectDetectionEvent("Lighter", .9f, 320, 240, 1, 306, 210, 334, 270);
            float start = Time.realtimeSinceStartup;
            Set(locator, "resumeCaptureAfter", start-1);
            for (int i=0;i<3;i++)
            {
                Set(locator, "pendingId", (long)i+1);
                Set(locator.detections, "<LastFrameId>k__BackingField", (long)i+1);
                Set(locator, "capturedAt", start-.2f+i*.05f);
                Set(locator, "capturePose", new Pose(Vector3.right * ((i+1)*.01f), Quaternion.identity));
                Require(locator.TryLocate(det, out var p, out string reason), "Visual projection rejected with right hand nearby: "+reason);
                Set(anchor, "m_TargetPosition", p); Invoke(anchor, "Update");
                Near(displayed.transform.position, p, "Anchor added extra smoothing to averaged vision");
            }
            Require(Mathf.Abs(displayed.transform.position.x-.023f)<.001f, "Stereo measurements did not reach displayed averaged position");
            Vector3 before = displayed.transform.position;
            for(int i=0;i<40;i++)
            {
                float now = start+i*.01f;
                locator.UpdateGripContact(true,before+Vector3.left*.02f,before+Vector3.right*.02f,now);
                locator.UpdatePinchHand(true,new Pose(before+Vector3.up*.1f,Quaternion.identity),true,true,before,now,false);
                locator.UpdatePinchHand(true,new Pose(before+Vector3.up*.2f,Quaternion.identity),true,true,before,now,true);
            }
            Require(!locator.IsHeld&&!locator.HasGripCandidate,"Visual mode gave motion ownership to hand");
            locator.Invalidate("NO_LIGHTER"); Invoke(anchor,"Update");
            Near(displayed.transform.position,before,"Visual loss or hand motion moved the displayed anchor");
            Set(locator,"capturedAt",Time.realtimeSinceStartup-.02f);
            Require(!locator.TryLocate(new ObjectDetectionEvent("Lighter",.2f,320,240,2,306,210,334,270),out _,out var rejected)&&
                rejected=="VISUAL_QUALITY_GATE","Low-confidence measurement bypassed quality gate");
            locator.followMode=LighterFollowMode.HybridGrip; Invoke(locator,"UpdateFollowingHold");
            Require(!locator.TryLocate(det,out _,out rejected)&&rejected=="RIGHT_HAND_PROTECTION","Hybrid protection lost after switching modes");
            locator.followMode=LighterFollowMode.VisualAverage; Invoke(locator,"UpdateFollowingHold");
            Near(displayed.transform.position,before,"Switching follow mode moved the displayed anchor");
            Debug.Log("VISUAL_AVERAGE_RENDERED_ANCHOR_CHECK_PASS");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(displayed); }
    }
}
