using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using Unity.XR.PXR;

namespace Perception
{
    public enum LighterFollowMode { VisualAverage = 0, HybridGrip = 1 }
    // Keeps the exact stereo pair and capture pose until its PICF frame_id is answered.
    [DefaultExecutionOrder(-40)]
    public sealed class StereoYoloLocator : MagicMR.TabletopInteractionBase
    {
        public PicoYoloFrameSender sender;
        public DetectionManager detections;
        public TcpClient transport;
        public Transform trackedCamera;
        public Transform lighter;
        public Text debugText;
        public float maxResultAgeSeconds = 1.5f;
        [Tooltip("VisualAverage follows only YOLO + stereo measurements. HybridGrip enables left-hand continuation.")]
        public LighterFollowMode followMode = LighterFollowMode.VisualAverage;
        public bool UsesVisualAverage => followMode == LighterFollowMode.VisualAverage;
        bool ProtectVision => !UsesVisualAverage && gestureRecognizer != null && gestureRecognizer.HoldVisualFollowing;
        LighterFollowMode appliedFollowMode = LighterFollowMode.VisualAverage;
        struct TimedPose { public long time; public Pose pose; }
        readonly List<TimedPose> poses = new List<TimedPose>();
        readonly StereoPositionGuard positionGuard = new StereoPositionGuard();
        readonly TabletopTargetLock tabletopLock = new TabletopTargetLock();
        readonly TabletopVisualFollow follow = new TabletopVisualFollow();
        readonly PinchGripFollow grip = new PinchGripFollow();
        readonly LighterHoldEvidence ownership = new LighterHoldEvidence();
        readonly LighterGripContact contact = new LighterGripContact();
        readonly LighterAppearanceTrack appearance = new LighterAppearanceTrack();
        float objectSeenAt=-100, objectTrackAt;
        string objectTrackStatus="NO_TEMPLATE";
        bool ObjectRecent => Time.realtimeSinceStartup-objectSeenAt<.9f;
        public override Vector3 VisualPosition => lighter!=null?lighter.position:InteractionPosition;
        bool handDriving;
        bool reacquiringVision;
        float diagnosticsAt,lastReplyAt=-100,lastReplyAgeMs=-1;
        public override bool IsHeld => !UsesVisualAverage && TargetLocated && grip.IsHolding && ownership.Confirmed;
        public override bool HasGripCandidate => !UsesVisualAverage && TargetLocated && grip.IsHolding;
        public override void UpdateGripContact(bool valid,Vector3 thumb,Vector3 index,float now)
        { if (!UsesVisualAverage) contact.Observe(valid&&TargetLocated,thumb,index,InteractionPosition,registeredWidth,registeredHeight,now); }
        public override void UpdatePinchHand(bool palmValid, Pose palm, bool tipsValid, bool closed, Vector3 pinch, float now, bool beforeRender)
        {
            if (UsesVisualAverage) return;
            if (!TargetLocated) { grip.Reset(); ownership.Reset(); contact.Reset(); return; }
            if(beforeRender)
            {
                if(IsHeld && grip.Preview(palmValid,palm,tipsValid,closed,pinch,now,out var position))
                { anchorManager?.RenderHandPlacement(position); FindFirstObjectByType<LighterUnderAnchorBinder>()?.RefreshFollowingEffects(); }
                return;
            }
            bool candidateBefore=grip.IsHolding;
            grip.Observe(palmValid,palm,tipsValid,closed,pinch,InteractionPosition,now);
            if(!candidateBefore&&grip.IsHolding)
            {
                ownership.Begin(InteractionPosition,now);
                Debug.Log("[ObjectGrip] CANDIDATE - waiting for real lighter motion");
            }
            if(!grip.IsHolding)ownership.Reset();
            if(candidateBefore&&!grip.IsHolding)contact.Reset();
            if(grip.IsHolding&&!ownership.Confirmed&&contact.Recent(now))
            {
                ownership.ConfirmContact();
                Debug.Log("[ObjectGrip] CONTACT_CONFIRMED - thumb/index bracket the registered lighter");
            }
            UpdateFollowingHold();
        }
        AnchorManager anchorManager;
        MagicMR.TabletopGestureRecognizer gestureRecognizer;
        bool holdFollowing;
        float resumeCaptureAfter;
        public int RegistrationRevision { get; private set; }
        bool summoned;
        public override bool TargetLocated => tabletopLock.IsLocked && tracking && !paused;
        public override bool InteractionReady => TargetLocated && summoned;
        // A verified tabletop anchor remains usable while the hand occludes vision.
        // Reset / head-tracking loss clear it; rejected frames must not revoke summon permission.
        public override bool CanSummon => TargetLocated && !summoned;
        public override Vector3 InteractionPosition => TargetLocated && anchorManager != null ? anchorManager.PlacementPosition : tabletopLock.Position;
        public override bool TrySummon()
        {
            UpdateFollowingHold();
            if (!CanSummon) return false;
            var binder = FindFirstObjectByType<LighterUnderAnchorBinder>();
            if (binder == null || !binder.PrepareSummon(this)) return false;
            summoned = true;
            foreach (var r in renderers) if (r != null) r.forceRenderingOff = false;
            Debug.Log($"[Tabletop] SUMMONED world={InteractionPosition:F4} bottom={CurrentBottom:F4} height={registeredHeight:F4} " +
                $"root={lighter.position:F4} yaw={lighter.eulerAngles.y:F1} protected={holdFollowing}");
            return true;
        }
        float registeredHeight, registeredBottomOffset, registeredWidth;
        public float RegisteredHeight => registeredHeight;
        public Vector3 RegisteredBottom => tabletopLock.Position + Vector3.up * registeredBottomOffset;
        public Vector3 CurrentBottom => InteractionPosition + Vector3.up * registeredBottomOffset;

        public override void ResetInteraction()
        {
            FindFirstObjectByType<MagicMR.TabletopGestureRecognizer>()?.ResetSummoning();
            summoned = false;
            tabletopLock.Reset();
            follow.Reset(Vector3.zero);
            grip.Reset(); ownership.Reset(); contact.Reset(); handDriving = false; reacquiringVision=false;
            appearance.Reset();objectSeenAt=-100;
            anchorManager?.EndHandPlacement();
            holdFollowing = false;
            registeredHeight = registeredBottomOffset = registeredWidth = 0;
            positionGuard.Reset();
            waiting = false; left = null; validAt = -100;
            lighter.GetComponent<MagicMR.RealityEditor>()?.ResetTarget();
            if (lighter.parent != null && lighter.parent.name == "Anchor_Lighter")
                lighter.localPosition = Vector3.zero;
            FindFirstObjectByType<AnchorManager>()?.ResetPlacement();
            foreach (var r in renderers) if (r != null) r.forceRenderingOff = true;
            state = "SEARCHING - keep lighter still";
            Debug.Log("[Tabletop] Reset: reacquiring real lighter.");
        }
        AndroidJavaClass clock;
        Texture2D texture;
        byte[] left, right;
        Pose capturePose, cameraPose;
        Vector4 intrinsics;
        float baseline, sentAt, capturedAt, validAt = -100, depth, quality, distance, lastLogAt;
        int acceptedCount, rejectedCount;
        long pendingId;
        bool waiting, tracking, paused;
        Vector3 world;
        string state = "WAITING_FOR_STEREO";
        Renderer[] renderers;

        void Awake()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            clock = new AndroidJavaClass("java.lang.System");
#endif
            texture = new Texture2D(640, 480, TextureFormat.RGBA32, false);
            anchorManager = FindFirstObjectByType<AnchorManager>();
            gestureRecognizer = FindFirstObjectByType<MagicMR.TabletopGestureRecognizer>();
            renderers = lighter.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) r.forceRenderingOff = true;
        }
        void OnEnable() { detections.AcceptFrame = AcceptReply; detections.DetectionFrameReceived += OnDetection; }
        void OnDisable() { detections.AcceptFrame = null; detections.DetectionFrameReceived -= OnDetection; }
        bool AcceptReply(DetectionFrame frame) => waiting && tracking && IsMatchingReply(
            frame.frameId, pendingId, frame.width, frame.height, Time.realtimeSinceStartup - capturedAt, maxResultAgeSeconds);
        public static bool IsMatchingReply(long id, long expected, int width, int height, float age, float maxAge)
            => id > 0 && id == expected && width == 640 && height == 480 && age >= 0 && age <= maxAge;
        long NowNs()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return clock.CallStatic<long>("nanoTime");
#else
            return (long)(Time.realtimeSinceStartupAsDouble * 1e9);
#endif
        }
        void Update()
        {
            UpdateFollowingHold();
            var head = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            tracking = !paused && head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked;
            if (tracking && head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 p) &&
                head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion q))
            {
                Transform root = trackedCamera.parent;
                poses.Add(new TimedPose { time = NowNs(), pose = new Pose(
                    root != null ? root.TransformPoint(p) : p, root != null ? root.rotation * q : q) });
                if (poses.Count > 180) poses.RemoveAt(0);
            }
            else
            {
                if (tabletopLock.IsLocked) ResetInteraction();
                tabletopLock.Reset();
                tracking = false; poses.Clear(); waiting = false; left = null;
                positionGuard.Reset(); Invalidate("TRACKING_LOST");
            }
            if (tracking && transport != null && !transport.IsConnected)
            { waiting = false; left = null; Invalidate("PC_DISCONNECTED"); }
            if (waiting && Time.realtimeSinceStartup - sentAt > maxResultAgeSeconds)
            { waiting = false; Invalidate("YOLO_TIMEOUT"); }
            bool fresh = TargetLocated;
            if (!fresh && state == "LIGHTER_LOCATED") state = "WAITING_FOR_FRESH_POSITION";
            foreach (var r in renderers) if (r != null) r.forceRenderingOff = !InteractionReady;
            var gesture = FindFirstObjectByType<MagicMR.TabletopGestureRecognizer>();
            RecordTrackingDiagnostics();
            if (debugText != null) debugText.text = (UsesVisualAverage ? "VISUAL FOLLOW 1.10.0 (3 frames)\n" : "HYBRID GRIP 1.10.0\n") +
                (TargetLocated && !summoned ? (CanSummon ? "READY - thumbs-up to summon" : "WAIT TO SUMMON - " + state) :
                InteractionReady ? (IsHeld ? (grip.UsingPalmFallback ? "FOLLOW - brief palm fallback" : !ObjectRecent ? "FOLLOW LEFT GRIP - vision pending" : "FOLLOW LEFT GRIP + vision") : grip.IsHolding ? "VERIFY PICKUP - keep lighter visible" : holdFollowing ? "HOLD - right hand casting" :
                    Time.realtimeSinceStartup - validAt < 1f ? "FOLLOW - right hand to cast" : "HOLD - " + state) : state) +
                "\nPC: " + (transport != null && transport.IsConnected ? "connected" : "disconnected") +
                "\nFrame: " + pendingId + (waiting ? " (waiting)" : "") +
                "\nDistance: " + (fresh ? distance.ToString("F3") + " m" : "--") +
                "   Z: " + (fresh ? depth.ToString("F3") + " m" : "--") +
                "\n" + (IsHeld ? $"Corr: {grip.Corrections} / filter gap: {grip.FilterLagMm:F1} mm" : CanSummon && Time.realtimeSinceStartup - validAt >= 1f ? "Using saved pose; moved? Left Reset" :
                    "Lighter height: " + (fresh ? (registeredHeight * 100).ToString("F1") + " cm" : "--")) +
                "\n" + (gesture != null ? gesture.ReachFeedback : "Hand: --") +
                "\n" + (gesture != null ? gesture.Feedback : "") +
                "\n" + (MagicMR.GestureManager.Instance != null ? MagicMR.GestureManager.Instance.LastGestureFeedback : "") +
                "\n" + (UsesVisualAverage ? "YOLO + stereo / lost vision: hold pose" : IsHeld ? "Separate left fingers to release" : "Left thumb/index to pick up / Reset");
        }
        bool PoseAt(long timestamp, out Pose result)
        {
            result = default;
            if (!tracking) return false;
            for (int i = poses.Count - 1; i > 0; i--)
            {
                var a = poses[i - 1]; var b = poses[i];
                if (timestamp < a.time || timestamp > b.time) continue;
                if (b.time - a.time > 100000000) return false;
                float t = (float)((double)(timestamp - a.time) / Math.Max(1, b.time - a.time));
                result = new Pose(Vector3.Lerp(a.pose.position, b.pose.position, t),
                    Quaternion.Slerp(a.pose.rotation, b.pose.rotation, t));
                return true;
            }
            return false;
        }
        public void AcceptPair(byte[] rgba, byte[] leftGray, byte[] rightGray,
            XrCameraIntrinsics k, XrCameraExtrinsics e, float separation, long timestamp)
        {
            UpdateFollowingHold();
            if (paused || (holdFollowing && !IsHeld)) return;
            double age = (NowNs() - timestamp) / 1e9;
            float captureTime = Time.realtimeSinceStartup - (float)age;
            if (tabletopLock.IsLocked && captureTime <= resumeCaptureAfter) return;
            if (age < 0 || age > .25 || !PoseAt(timestamp, out Pose headPose))
            { Invalidate("WAITING_FOR_CAPTURE_POSE"); return; }
            if(IsHeld && Time.realtimeSinceStartup>=objectTrackAt && (gestureRecognizer==null||!gestureRecognizer.HoldVisualFollowing))
            {
                objectTrackAt=Time.realtimeSinceStartup+.14f;
                TrackHeldObject(leftGray,rightGray,k,e,separation,headPose,captureTime);
            }
            if(waiting)return;
            // Raw storage is also our matching coordinate system. JPEG mapping is
            // explicit below, including Unity's bottom-up Texture2D convention.
            texture.LoadRawTextureData(rgba); texture.Apply(false, false);
            if (!sender.TrySendTexture(texture)) return;
            left = leftGray; right = rightGray; capturePose = headPose;
            cameraPose = new Pose(new Vector3(e.pose.Position.X, e.pose.Position.Y, e.pose.Position.Z),
                new Quaternion(e.pose.Orientation.X, e.pose.Orientation.Y, e.pose.Orientation.Z, e.pose.Orientation.W).normalized);
            intrinsics = new Vector4(k.focalLength.X, k.focalLength.Y, k.principalPoint.X, k.principalPoint.Y);
            baseline = separation;
            capturedAt = captureTime;
            pendingId = sender.LastSentFrameId; sentAt = Time.realtimeSinceStartup; waiting = true;
        }
        void OnDetection(DetectionFrame frame)
        {
            if (frame.frameId != pendingId) return;
            lastReplyAt=Time.realtimeSinceStartup;lastReplyAgeMs=(lastReplyAt-capturedAt)*1000;
            waiting = false;
            bool hasLighter = false;
            foreach (var d in frame.detections)
                if (string.Equals(d.className, "Lighter", StringComparison.OrdinalIgnoreCase)) hasLighter = true;
            if (!hasLighter) Invalidate("NO_LIGHTER");
            else if (Time.realtimeSinceStartup - validAt > .6f) state = "CONFIRMING_LIGHTER";
        }
        public bool TryLocate(ObjectDetectionEvent detection, out Vector3 position, out string reason)
        {
            UpdateFollowingHold();
            rejectedCount++;
            position = default; reason = "StereoWaiting";
            if(tabletopLock.IsLocked && ProtectVision)
            {reason="RIGHT_HAND_PROTECTION";return false;}
            if (holdFollowing && !IsHeld) { reason = "HAND_HOLD"; return false; }
            if (!tracking || left == null || detections.LastFrameId != pendingId ||
                Time.realtimeSinceStartup - capturedAt > maxResultAgeSeconds) return false;
            if (detections.LastFrameWidth != 640 || detections.LastFrameHeight != 480 ||
                sender.LastSentWidth != 640 || sender.LastSentHeight != 480)
            { reason = "StereoFrameSizeMismatch"; Invalidate(reason); return false; }
            if (!detection.HasBBox) return false;
            // EncodeToJPG exports top row first; Texture2D row zero is bottom.
            float x1 = detection.x1, x2 = detection.x2;
            float y1 = sender.FlipVerticallyBeforeEncode ? detection.y1 : 479 - detection.y2;
            float y2 = sender.FlipVerticallyBeforeEncode ? detection.y2 : 479 - detection.y1;
            if (!float.IsFinite(x1) || !float.IsFinite(x2) || !float.IsFinite(y1) || !float.IsFinite(y2) ||
                x1 < 0 || x2 > 640 || y1 < 0 || y2 > 480) return false;
            var box=Rect.MinMaxRect(x1,y1,x2,y2);
            // The fixed template is optional evidence, never a prerequisite for YOLO following.
            // Partial occlusion and a changed crop rejected the real lighter in 1.9.2.
            int u = Mathf.RoundToInt((x1 + x2) * .5f), v = Mathf.RoundToInt((y1 + y2) * .5f);
            // Keep complete 9x7 patches in the middle 60% of the detected object.
            int sx = Mathf.Min(24, Mathf.FloorToInt((x2 - x1) * .3f) - 5);
            int sy = Mathf.Min(16, Mathf.FloorToInt((y2 - y1) * .3f) - 4);
            if (sx < 2 || sy < 2) { reason = "LighterTooSmall"; Invalidate(reason); return false; }
            var estimate = StereoPatchMatcher.EstimateDepth(left, right, 640, 480, u, v,
                intrinsics.x, baseline, sx, sy);
            quality = estimate.confidence;
            if (!estimate.valid) { reason = "NoReliableLighterDepth"; Invalidate(reason); return false; }
            depth = estimate.depthMeters;
            Vector3 optical = new Vector3((u - intrinsics.z) * depth / intrinsics.x,
                (v - intrinsics.w) * depth / intrinsics.y, depth);
            position = capturePose.position + capturePose.rotation * StereoFusionGeometry.OpticalToHead(optical, cameraPose);
            float measuredWidth=(x2-x1)*depth/intrinsics.x;
            float objectHeight=(y2-y1)*depth/intrinsics.y;
            bool objectReliable=detection.confidence>=.5f && quality>=.04f && estimate.acceptedSamples>=5 &&
                measuredWidth>registeredWidth*.6f && measuredWidth<registeredWidth*1.5f &&
                objectHeight>registeredHeight*.65f && objectHeight<registeredHeight*1.35f &&
                x1>4 && x2<636 && y1>4 && y2<476;
            if(!UsesVisualAverage && grip.IsHolding && grip.TryPredictAt(capturedAt,out var predicted))
            {
                bool wasConfirmed=ownership.Confirmed;
                if(ownership.Observe(position,predicted,capturedAt,Time.realtimeSinceStartup,objectReliable))
                {
                    position=ownership.ReleasedPosition;
                    grip.ReleaseFromVision();contact.Reset();UpdateFollowingHold();
                    follow.Reset(position);reacquiringVision=false;
                    objectSeenAt=validAt=Time.realtimeSinceStartup;
                    state=reason="OBJECT_RETURNED_TO_VISION";
                    Debug.Log($"[ObjectGrip] RELEASED - real lighter stationary, hand departed; object={position:F4}");
                    return true;
                }
                if(!wasConfirmed&&ownership.Confirmed)
                    Debug.Log("[ObjectGrip] CONFIRMED - hand and real lighter moved together");
                if(objectReliable)objectSeenAt=Time.realtimeSinceStartup;
            }
            if(IsHeld)
            {
                float width=(x2-x1)*depth/intrinsics.x;
                float apparentHeight=(y2-y1)*depth/intrinsics.y;
                bool reliable=detection.confidence>=.6f && quality>=.08f && estimate.acceptedSamples>=6 &&
                    width>registeredWidth*.8f && width<registeredWidth*1.25f &&
                    apparentHeight>registeredHeight*.8f && apparentHeight<registeredHeight*1.2f &&
                    x1>4 && x2<636 && y1>4 && y2<476 &&
                    (gestureRecognizer==null || !gestureRecognizer.HoldVisualFollowing);
                bool corrected=grip.Correct(position,capturedAt,Time.realtimeSinceStartup,reliable);
                if(corrected){objectSeenAt=Time.realtimeSinceStartup;Debug.Log($"[PinchFollow] VISUAL_CORRECTION count={grip.Corrections} captureAge={Time.realtimeSinceStartup-capturedAt:F3}");}
                reason=corrected?"PINCH_VISUAL_CORRECTED":"PINCH_HAND_ONLY";
                // Vision corrects the attachment; never writes an old camera position to the anchor.
                return false;
            }
            if (tabletopLock.IsLocked)
            {
                if (Time.realtimeSinceStartup - capturedAt > (UsesVisualAverage ? .3f : .6f) || captureTimeInvalid())
                { reason = "WAITING_FOR_FRESH_POSITION"; Invalidate(reason); return false; }
                if (UsesVisualAverage && !objectReliable)
                { reason = "VISUAL_QUALITY_GATE"; Invalidate(reason); return false; }
                // Track the detected center in all three axes; keep initial model size and rotation.
                if (!(UsesVisualAverage ? follow.Observe(position, capturedAt, Time.realtimeSinceStartup) :
                    follow.Observe(position, Time.realtimeSinceStartup)))
                { reason = "CONFIRMING_MOVE"; Invalidate(reason); return false; }
                position = follow.Position;
                objectSeenAt=Time.realtimeSinceStartup;
                reacquiringVision=false;
                distance = Vector3.Distance(position, capturePose.position);
                validAt = Time.realtimeSinceStartup; state = "FOLLOWING_3D";
                reason = UsesVisualAverage ? "StereoVisualAverage3" : "StereoFollow3D";
                return true;
            }
            Vector3 cameraOrigin = capturePose.position + capturePose.rotation * cameraPose.position;
            Vector3 topOptical = new Vector3((u - intrinsics.z) / intrinsics.x, (y1 - intrinsics.w) / intrinsics.y, 1);
            Vector3 bottomOptical = new Vector3((u - intrinsics.z) / intrinsics.x, (y2 - intrinsics.w) / intrinsics.y, 1);
            Vector3 topRay = capturePose.rotation * (StereoFusionGeometry.OpticalToHead(topOptical, cameraPose) - cameraPose.position);
            Vector3 bottomRay = capturePose.rotation * (StereoFusionGeometry.OpticalToHead(bottomOptical, cameraPose) - cameraPose.position);
            if (!StereoFusionGeometry.TryUprightExtent(position, cameraOrigin, topRay.normalized, bottomRay.normalized,
                out Vector3 measuredBottom, out float measuredHeight))
            { reason = "WAITING_FOR_UPRIGHT_LIGHTER"; Invalidate(reason); return false; }
            if (!positionGuard.Accept(position, Time.realtimeSinceStartup))
            { reason = "CONFIRMING_POSITION_CHANGE"; Invalidate(reason); return false; }
            bool locked = tabletopLock.Observe(position, Time.realtimeSinceStartup);
            float blend = 1f / Mathf.Max(1, tabletopLock.SampleCount);
            registeredHeight = Mathf.Lerp(registeredHeight, measuredHeight, blend);
            registeredWidth = Mathf.Lerp(registeredWidth, (x2-x1)*depth/intrinsics.x, blend);
            registeredBottomOffset = Mathf.Lerp(registeredBottomOffset, measuredBottom.y - position.y, blend);
            if (!locked)
            { reason = "STABILIZING - keep lighter still"; state = reason; return false; }
            position = tabletopLock.Position;
            appearance.Capture(left,box);objectSeenAt=Time.realtimeSinceStartup;
            follow.Reset(position);
            RegistrationRevision++;
            distance = Vector3.Distance(position, capturePose.position);
            acceptedCount++;
            rejectedCount--;
            world = position; validAt = Time.realtimeSinceStartup; state = "LIGHTER_LOCATED"; reason = "StereoDepth";
            Debug.Log($"[Tabletop] READY world={world:F4}; visual translation following enabled.");
            Debug.Log($"[Registration] bbox=({x1:F1},{y1:F1})-({x2:F1},{y2:F1}) height={registeredHeight:F4} " +
                $"bottom={RegisteredBottom:F4} camera={cameraOrigin:F4} captureHead={capturePose.position:F4}");
            Debug.Log($"[StereoYOLO] frame={pendingId} pixel=({u},{v}) z={depth:F4} world={world:F4} quality={quality:F3}");
            return true;
        }
        public void Invalidate(string reason)
        {
            if (state != reason || Time.realtimeSinceStartup - lastLogAt > 2f)
            { Debug.Log($"[StereoYOLO] state={reason} frame={pendingId}"); lastLogAt = Time.realtimeSinceStartup; }
            state = reason; validAt = -100;
        }
        bool captureTimeInvalid() => capturedAt <= resumeCaptureAfter;
        void RecordTrackingDiagnostics()
        {
            float now=Time.realtimeSinceStartup;
            if(!TargetLocated||now<diagnosticsAt)return;
            diagnosticsAt=now+2;
            Debug.Log($"[FollowMetrics] held={IsHeld} fallback={grip.UsingPalmFallback} reacquire={reacquiringVision} " +
                $"mode={followMode} averageSamples={follow.SampleCount} " +
                $"handGapMs={grip.HandGapMs:F1} filterGapMm={grip.FilterLagMm:F2} " +
                $"replyAgeMs={lastReplyAgeMs:F0} replyAgoMs={(lastReplyAt<0?-1:(now-lastReplyAt)*1000):F0} " +
                $"visionResidualMm={grip.VisionResidualMm:F1} vision={grip.VisionStatus} " +
                $"object={objectTrackStatus} score={appearance.LastScore:F2} objectAgoMs={(now-objectSeenAt)*1000:F0} " +
                $"ownership={ownership.Status} candidate={grip.IsHolding} contact={contact.Touching} attachments={grip.Attachments} fallbacks={grip.Fallbacks} release={grip.ReleaseReason}");
        }
        void TrackHeldObject(byte[] gray,byte[] other,XrCameraIntrinsics k,XrCameraExtrinsics e,float separation,Pose head,float captured)
        {
            objectTrackStatus="NO_TEMPLATE";
            if(!appearance.Ready)return;
            var camera=new Pose(new Vector3(e.pose.Position.X,e.pose.Position.Y,e.pose.Position.Z),
                new Quaternion(e.pose.Orientation.X,e.pose.Orientation.Y,e.pose.Orientation.Z,e.pose.Orientation.W).normalized);
            Vector3 headPoint=Quaternion.Inverse(head.rotation)*(grip.Position-head.position);
            Vector3 optical=StereoFusionGeometry.HeadToOptical(headPoint,camera);
            if(optical.z<.15f||optical.z>2)return;
            float fx=k.focalLength.X,fy=k.focalLength.Y,cx=k.principalPoint.X,cy=k.principalPoint.Y;
            Vector2 center=new Vector2(cx+fx*optical.x/optical.z,cy+fy*optical.y/optical.z);
            Vector2 size=new Vector2(fx*registeredWidth/optical.z,fy*registeredHeight/optical.z);
            objectTrackStatus="NO_APPEARANCE_MATCH";
            if(!appearance.Find(gray,new Rect(center-size*.5f,size),out var box))return;
            int sx=Mathf.Min(24,Mathf.FloorToInt(box.width*.3f)-5),sy=Mathf.Min(16,Mathf.FloorToInt(box.height*.3f)-4);
            if(sx<2||sy<2)return;
            var estimate=StereoPatchMatcher.EstimateDepth(gray,other,640,480,Mathf.RoundToInt(box.center.x),Mathf.RoundToInt(box.center.y),
                fx,separation,sx,sy);
            objectTrackStatus="NO_RELIABLE_OBJECT_DEPTH";
            if(!estimate.valid||estimate.acceptedSamples<5||estimate.confidence<.05f)return;
            float measuredHeight=box.height*estimate.depthMeters/fy;
            if(measuredHeight<registeredHeight*.65f||measuredHeight>registeredHeight*1.4f)return;
            Vector3 p=new Vector3((box.center.x-cx)*estimate.depthMeters/fx,(box.center.y-cy)*estimate.depthMeters/fy,estimate.depthMeters);
            Vector3 worldPoint=head.position+head.rotation*StereoFusionGeometry.OpticalToHead(p,camera);
            if(Vector3.Distance(worldPoint,grip.Position)>.09f)return;
            objectSeenAt=Time.realtimeSinceStartup;
            bool corrected=grip.Correct(worldPoint,captured,Time.realtimeSinceStartup,true,true);
            objectTrackStatus=grip.VisionStatus;
            if(corrected)Debug.Log($"[ObjectTrack] CORRECTED score={appearance.LastScore:F2} residual={grip.VisionResidualMm:F1}mm");
        }
        void UpdateFollowingHold()
        {
            if (appliedFollowMode != followMode)
            {
                anchorManager?.EndHandPlacement();
                grip.Reset(); ownership.Reset(); contact.Reset();
                handDriving = holdFollowing = false;
                waiting = false; left = null;
                resumeCaptureAfter = Time.realtimeSinceStartup;
                follow.Reset(InteractionPosition, TargetLocated);
                appliedFollowMode = followMode;
            }
            follow.AverageMeasurements = UsesVisualAverage;
            if (UsesVisualAverage) return;
            grip.Tick(Time.realtimeSinceStartup);
            if (IsHeld)
            {
                if (!handDriving)
                {
                    Debug.Log($"[PinchFollow] ATTACHED position={grip.Position:F4}");
                    waiting=false;left=null;resumeCaptureAfter=Time.realtimeSinceStartup;reacquiringVision=false;
                }
                handDriving = true;
                // Reliable left-hand tracking remains the motion source during visual loss
                // and right-hand casting. Vision only corrects the captured grip offset.
                anchorManager?.SetHandPlacement(grip.Position);
                follow.Reset(grip.Position); holdFollowing = true;
                distance = trackedCamera != null ? Vector3.Distance(grip.Position,trackedCamera.position) : distance;
                return;
            }
            if (handDriving)
            {
                handDriving = false; anchorManager?.EndHandPlacement();
                reacquiringVision=true;
                follow.Reset(InteractionPosition,true);
                resumeCaptureAfter = Time.realtimeSinceStartup;
                Debug.Log($"[Handheld] RELEASE_OR_TRACKING_LOST reason={grip.ReleaseReason} position={InteractionPosition:F4}; confirming fresh vision.");
            }
            bool hold = tabletopLock.IsLocked && ProtectVision;
            if (hold && !holdFollowing)
            {
                Debug.Log($"[SummonPose] Hand hold bottom={CurrentBottom:F4} height={registeredHeight:F4} age={Time.realtimeSinceStartup-validAt:F2}");
            }
            if (hold)
            {
                waiting = false; left = null;
                anchorManager?.HoldPlacement();
                follow.Reset(InteractionPosition,reacquiringVision);
                resumeCaptureAfter = Time.realtimeSinceStartup;
            }
            if (holdFollowing && !hold) resumeCaptureAfter = Time.realtimeSinceStartup;
            holdFollowing = hold;
        }
        void OnApplicationPause(bool value) { paused = value; ResetInteraction(); poses.Clear(); Invalidate(value ? "PAUSED" : "WAITING_FOR_STEREO"); }
        void OnDestroy() { Destroy(texture); clock?.Dispose(); }
    }
}
