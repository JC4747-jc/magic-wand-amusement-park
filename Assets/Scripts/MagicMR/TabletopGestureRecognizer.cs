using UnityEngine;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    public sealed class TabletopGestureRecognizer : HandGestureDetectorBase
    {
        readonly TabletopGestureRules rules = new TabletopGestureRules();
        readonly GestureReachWindow reach = new GestureReachWindow();
        readonly TabletopSummonGesture summon = new TabletopSummonGesture();
        TabletopInteractionBase tabletop;
        float sampledAt = -100;
        float nextDiagnostic;
        string feedback = "Show right hand";
        RightHandSpellVfx spellVfx;
        RightHandSpellVfx SpellVfx => spellVfx != null ? spellVfx :
            (spellVfx = RightHandSpellVfx.GetOrCreate(GestureManager.Instance != null ? GestureManager.Instance.gameObject : gameObject));
        public float TargetDistance { get; private set; }
        public Vector3 QueryPosition { get; private set; }
        public bool HasFreshHand => Time.unscaledTime - sampledAt <= TabletopGestureRules.MaximumSampleGapSeconds;
        public bool CanReach => HasFreshHand && reach.Allowed;
        float visionHoldUntil;
        public bool HoldVisualFollowing => Time.unscaledTime < visionHoldUntil;
        public string Feedback => HasFreshHand ? feedback : "Show right hand to headset";
        public string ReachFeedback => HasFreshHand ?
            tabletop != null && !tabletop.InteractionReady ? "Right thumbs-up within 60 cm" :
            $"Hand: {TargetDistance * 100:F0} cm / enter 20 cm" + (CanReach ? " [IN RANGE]" : " [MOVE CLOSER]") : "Hand: not tracked";

        public void ResetRecognition() { rules.Reset(); reach.Reset(); summon.CancelDwell(); sampledAt = -100; if (spellVfx != null) spellVfx.Clear(); }
        public void ResetSummoning() { ResetRecognition(); summon.Reset(); visionHoldUntil = 0; }
        void OnApplicationPause(bool paused) { ResetRecognition(); }
#if XR_HANDS_1_1_OR_NEWER
        protected override void OnUpdatedHands(XRHandSubsystem subsystem, XRHandSubsystem.UpdateSuccessFlags flags,
            XRHandSubsystem.UpdateType type)
        {
            if (tabletop == null) tabletop = FindFirstObjectByType<TabletopInteractionBase>();
            if (tabletop != null && tabletop.TargetLocated && NearTarget(subsystem.rightHand))
                visionHoldUntil = Time.unscaledTime + .45f;
            UpdateLeftGrip(subsystem.leftHand, (flags & XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints) != 0,
                type == XRHandSubsystem.UpdateType.BeforeRender);
            // Casting state advances only once; BeforeRender updates position only.
            if (type != XRHandSubsystem.UpdateType.Dynamic) return;
            if ((flags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) == 0)
            { ResetRecognition(); return; }
            ProcessHand(subsystem.rightHand);
        }
        protected override void ProcessHand(XRHand hand)
        {
            if (tabletop == null) tabletop = FindFirstObjectByType<TabletopInteractionBase>();
            var root = Camera.main != null ? Camera.main.transform.parent : null;
            if (!TabletopHandSample.TryRead(hand,root,Camera.main?Camera.main.transform.right:Vector3.right,
                Time.unscaledTime,out var frame)) { ResetRecognition(); return; }
            sampledAt = frame.gesture.time;
            if (tabletop == null || !tabletop.TargetLocated)
            { rules.Reset(); reach.Reset(); summon.Reset(); feedback = "Keep real lighter visible and still"; return; }
            Vector3 worldPalm=frame.gesture.palm,worldIndex=frame.gesture.index;
            float i=frame.extension.x,m=frame.extension.y,r=frame.extension.z,l=frame.extension.w;
            QueryPosition=frame.ClosestTo(tabletop.InteractionPosition);
            TargetDistance=Vector3.Distance(QueryPosition,tabletop.InteractionPosition);
            bool openHand=frame.gesture.open&&frame.gesture.pinch>TabletopGestureRules.PinchReleaseMeters;
            if (!tabletop.InteractionReady)
            {
                rules.Reset(); reach.Reset();
                if (!Read(hand, XRHandJointID.ThumbProximal, out var thumbBase) ||
                    !Read(hand, XRHandJointID.ThumbDistal, out var thumbJoint))
                { ResetRecognition(); return; }
                Vector3 worldThumb = frame.thumb;
                bool thumbsUp = TabletopSummonGesture.IsThumbsUp(i, m, r, l,
                    root != null ? root.TransformPoint(thumbBase) : thumbBase,
                    root != null ? root.TransformPoint(thumbJoint) : thumbJoint, worldThumb, worldIndex);
                bool eligible = tabletop.CanSummon && Vector3.Distance(worldPalm, tabletop.InteractionPosition) <= .60f;
                if (summon.Step(eligible, thumbsUp, worldPalm, sampledAt))
                {
                    if (tabletop.TrySummon())
                    {
                        SpellVfx.Summon(worldThumb, tabletop.VisualPosition);
                        feedback = "Summoned! Open hand to continue";
                        Debug.Log("[TabletopGesture] Summon: right thumbs-up");
                    }
                    else summon.Reset();
                }
                else feedback = !tabletop.CanSummon ? "Locate lighter before summoning" :
                    !eligible ? "Move hand within 60 cm" :
                    !thumbsUp ? "Thumb up, curl four fingers" :
                    $"Thumbs-up - hold: {summon.Progress * 100:F0}%";
                SpellVfx.SummonPreview(worldThumb, summon.Progress);
                if (sampledAt >= nextDiagnostic)
                {
                    nextDiagnostic = sampledAt + 2f;
                    Debug.Log($"[TabletopThumb] pose={thumbsUp} eligible={eligible} extension={i:F2},{m:F2},{r:F2},{l:F2} " +
                        $"progress={summon.Progress:F2}");
                }
                return;
            }
            if (summon.BlockTransforms(!openHand, sampledAt))
            { rules.Reset(); reach.Reset(); feedback = "Summoned! Open hand to continue"; return; }
            if (!reach.Observe(true, TargetDistance, sampledAt))
            { rules.Reset(); feedback = "Move hand closer to real lighter"; return; }
            var sample = frame.gesture;
            var dimension = rules.Step(sample);
            SpellVfx.Preview(worldPalm, worldIndex, frame.thumb,
                sample.pointing, sample.fist, sample.pinch <= TabletopGestureRules.PinchEnterMeters, sample.open);
            feedback = rules.Status;
            if (sampledAt >= nextDiagnostic)
            {
                nextDiagnostic = sampledAt + 2f;
                Debug.Log($"[TabletopHand] distance={TargetDistance:F3} pinch={sample.pinch:F3} " +
                    $"extension={i:F2},{m:F2},{r:F2},{l:F2} fist={sample.fist} open={sample.open} point={sample.pointing} state={feedback}");
            }
            if (dimension != EditDimension.None)
            {
                Debug.Log($"[TabletopGesture] {dimension} distance={TargetDistance:F3} extension={i:F2},{m:F2},{r:F2},{l:F2}");
                GestureManager.Notify(dimension, "tabletop_" + dimension);
            }
        }
        static bool Read(XRHand hand, XRHandJointID id, out Vector3 p)
        { bool valid = hand.GetJoint(id).TryGetPose(out var pose); p = pose.position; return valid; }
        void UpdateLeftGrip(XRHand hand, bool jointsFresh, bool beforeRender)
        {
            if (tabletop == null) return;
            if (!jointsFresh || !hand.isTracked || !hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palm))
            {
                if(!beforeRender)tabletop.UpdateGripContact(false,default,default,Time.realtimeSinceStartup);
                tabletop.UpdatePinchHand(false,default,false,false,Vector3.zero,Time.realtimeSinceStartup,beforeRender); return;
            }
            var root = Camera.main != null ? Camera.main.transform.parent : null;
            var worldPalm = new Pose(root != null ? root.TransformPoint(palm.position) : palm.position,
                root != null ? root.rotation*palm.rotation : palm.rotation);
            bool indexValid=Read(hand,XRHandJointID.IndexTip,out var index);
            bool thumbValid=Read(hand,XRHandJointID.ThumbTip,out var thumb);
            bool tipsValid=indexValid&&thumbValid;
            Vector3 pinch=(index+thumb)*.5f;
            if(root!=null)pinch=root.TransformPoint(pinch);
            if(!beforeRender)tabletop.UpdateGripContact(tipsValid,root!=null?root.TransformPoint(thumb):thumb,
                root!=null?root.TransformPoint(index):index,Time.realtimeSinceStartup);
            bool closed=tipsValid&&Vector3.Distance(index,thumb)<(tabletop.HasGripCandidate?.07f:.055f);
            tabletop.UpdatePinchHand(true,worldPalm,tipsValid,closed,pinch,Time.realtimeSinceStartup,beforeRender);
        }
        bool NearTarget(XRHand hand)
        {
            if (!hand.isTracked) return false;
            var root = Camera.main != null ? Camera.main.transform.parent : null;
            return NearJoint(hand, XRHandJointID.Palm, root) ||
                NearJoint(hand, XRHandJointID.Wrist, root) || NearJoint(hand, XRHandJointID.MiddleTip, root) ||
                NearJoint(hand, XRHandJointID.RingTip, root) || NearJoint(hand, XRHandJointID.LittleTip, root) ||
                NearJoint(hand, XRHandJointID.IndexTip, root) || NearJoint(hand, XRHandJointID.ThumbTip, root);
        }
        bool NearJoint(XRHand hand, XRHandJointID id, Transform root)
        {
            if (!Read(hand, id, out var point)) return false;
            var world=root != null ? root.TransformPoint(point) : point;
            return Vector3.Distance(world, tabletop.InteractionPosition) < .18f || Vector3.Distance(world, tabletop.VisualPosition)<.18f;
        }
#else
        protected override void ProcessHand(object hand) { }
#endif
    }
}
