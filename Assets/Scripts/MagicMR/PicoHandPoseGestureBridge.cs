using UnityEngine;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
using HandFinger = UnityEngine.XR.Hands.XRHandFingerID;
#endif

namespace MagicMR
{
    /// <summary>
    /// Evaluates three PICO-style HandPose configs and forwards them into
    /// <see cref="GestureManager.Notify"/>. Does not replace Circle / Swipe /
    /// Pinch tap / FollowHand.
    ///
    /// Config 1 RightFist + Config 2 RightOpen → Deconstruction (fist then burst).
    /// Config 3 LeftReset (pinch + palm towards HMD) → <see cref="MRGestureController.PerformFullReset"/>.
    ///
    /// Optional: assign exported <c>PXR_HandPoseConfig</c> assets for documentation
    /// in the Inspector. Runtime matching uses XR Hands joint angles with the
    /// same Open/Close windows as PICO <c>ShapesRecognizer</c>.
    /// </summary>
    [DefaultExecutionOrder(-45)]
    public class PicoHandPoseGestureBridge : HandGestureDetectorBase
    {
        public static PicoHandPoseGestureBridge Instance { get; private set; }

        [Header("Enable")]
        [SerializeField]
        bool m_Enabled = PicoHandPoseSpec.Enabled;

        [Tooltip("When on, FistBurstGestureDetector is disabled to avoid double fire.")]
        [SerializeField]
        bool m_OwnFistBurst = true;

        [Tooltip("Off by default: palm-towards-face + pinch can click PICO system UI and quit the app.")]
        [SerializeField]
        bool m_OwnLeftReset = false;

        [Header("PXR_HandPoseConfig assets (optional, documentation)")]
        [SerializeField]
        ScriptableObject m_FistConfig;

        [SerializeField]
        ScriptableObject m_OpenConfig;

        [SerializeField]
        ScriptableObject m_LeftResetConfig;

        [Header("Fist → Open timing")]
        [SerializeField]
        float m_MinFistHoldSeconds = StudySpec.FistMinHoldSeconds;

        [SerializeField]
        float m_MaxBurstSeconds = StudySpec.FistMaxBurstSeconds;

        [Header("Left Reset")]
        [SerializeField]
        float m_LeftResetHoldSeconds = StudySpec.LeftResetHoldSeconds;

        float m_FistStartTime = -1f;
        bool m_InFist;
        float m_LastDeconstructionTime;
        float m_LeftResetAccumulated;
        float m_NextDiagTime;

        public bool IsActive => m_Enabled && isActiveAndEnabled;
        public bool OwnsFistBurst => IsActive && m_OwnFistBurst;
        public bool OwnsLeftReset => IsActive && m_OwnLeftReset;

        void Awake()
        {
            Instance = this;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Instance = this;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Instance == this)
                Instance = null;
        }

#if XR_HANDS_1_1_OR_NEWER
        protected override void OnUpdatedHands(
            XRHandSubsystem subsystem,
            XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags,
            XRHandSubsystem.UpdateType updateType)
        {
            if (!m_Enabled)
                return;

            if ((updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0)
                UpdateRightFistOpen(subsystem.rightHand);

            if (m_OwnLeftReset &&
                (updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints) != 0)
                UpdateLeftReset(subsystem.leftHand);
        }

        protected override void ProcessHand(XRHand hand)
        {
            // Both hands are handled in OnUpdatedHands.
        }

        void UpdateRightFistOpen(XRHand hand)
        {
            if (!hand.isTracked)
                return;

            var now = Time.unscaledTime;
            var isFist = MatchesRightFist(hand);
            var isOpen = MatchesRightOpen(hand);

            if (now >= m_NextDiagTime && (isFist || m_InFist || isOpen))
            {
                m_NextDiagTime = now + 0.7f;
                TryFingerAngles(hand, HandFinger.Index, out var flex, out var curl);
                Debug.Log(
                    $"[MagicMR] HandPose right fist={isFist} open={isOpen} inFist={m_InFist} " +
                    $"indexFlex={flex:F0} indexCurl={curl:F0} configs=" +
                    $"{ConfigLabel(m_FistConfig, PicoHandPoseSpec.FistConfigName)}/" +
                    $"{ConfigLabel(m_OpenConfig, PicoHandPoseSpec.OpenConfigName)}");
            }

            if (isFist)
            {
                if (!m_InFist)
                {
                    m_InFist = true;
                    m_FistStartTime = now;
                }

                return;
            }

            if (!m_InFist)
                return;

            var held = now - m_FistStartTime;
            if (held > m_MaxBurstSeconds)
            {
                m_InFist = false;
                return;
            }

            if (!isOpen)
                return;

            if (held >= m_MinFistHoldSeconds && now - m_LastDeconstructionTime > 0.65f)
            {
                m_LastDeconstructionTime = now;
                Debug.Log($"[MagicMR] HandPose fist-burst held={held:F2}s → Notify(Deconstruction).");
                GestureManager.Notify(EditDimension.Deconstruction, "handpose_fist_burst");
            }

            m_InFist = false;
        }

        void UpdateLeftReset(XRHand hand)
        {
            if (!hand.isTracked)
            {
                m_LeftResetAccumulated = 0f;
                return;
            }

            if (!MatchesLeftReset(hand))
            {
                m_LeftResetAccumulated = 0f;
                return;
            }

            m_LeftResetAccumulated += Time.unscaledDeltaTime;
            if (m_LeftResetAccumulated < m_LeftResetHoldSeconds)
                return;

            m_LeftResetAccumulated = 0f;
            var fsm = MRGestureController.Instance ?? FindFirstObjectByType<MRGestureController>();
            if (fsm != null)
            {
                Debug.Log("[MagicMR] HandPose LeftReset matched → PerformFullReset.");
                fsm.PerformFullReset("handpose_left_reset");
            }
            else
            {
                Debug.LogWarning("[MagicMR] HandPose LeftReset matched but FSM missing.");
            }
        }

        // Flexion=Any in PXR Inspector; only Curl Open/Close is required so PICO
        // tracking noise does not miss the pose.
        static bool MatchesRightFist(XRHand hand)
        {
            return FingerCurlMatches(hand, HandFinger.Thumb, PicoHandPoseSpec.CurlThumbCloseMin, PicoHandPoseSpec.CurlThumbCloseMax) &&
                   FingerCurlMatches(hand, HandFinger.Index, PicoHandPoseSpec.CurlCloseMin, PicoHandPoseSpec.CurlCloseMax) &&
                   FingerCurlMatches(hand, HandFinger.Middle, PicoHandPoseSpec.CurlCloseMin, PicoHandPoseSpec.CurlCloseMax) &&
                   FingerCurlMatches(hand, HandFinger.Ring, PicoHandPoseSpec.CurlCloseMin, PicoHandPoseSpec.CurlCloseMax) &&
                   FingerCurlMatches(hand, HandFinger.Little, PicoHandPoseSpec.CurlCloseMin, PicoHandPoseSpec.CurlCloseMax);
        }

        static bool MatchesRightOpen(XRHand hand)
        {
            return FingerCurlMatches(hand, HandFinger.Thumb, PicoHandPoseSpec.CurlThumbOpenMin, PicoHandPoseSpec.CurlThumbOpenMax) &&
                   FingerCurlMatches(hand, HandFinger.Index, PicoHandPoseSpec.CurlOpenMin, PicoHandPoseSpec.CurlOpenMax) &&
                   FingerCurlMatches(hand, HandFinger.Middle, PicoHandPoseSpec.CurlOpenMin, PicoHandPoseSpec.CurlOpenMax) &&
                   FingerCurlMatches(hand, HandFinger.Ring, PicoHandPoseSpec.CurlOpenMin, PicoHandPoseSpec.CurlOpenMax) &&
                   FingerCurlMatches(hand, HandFinger.Little, PicoHandPoseSpec.CurlOpenMin, PicoHandPoseSpec.CurlOpenMax);
        }

        static bool MatchesLeftReset(XRHand hand)
        {
            if (!hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb) ||
                !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index) ||
                !hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palm))
                return false;

            var pinchDist = Vector3.Distance(thumb.position, index.position);
            var pinchOk = pinchDist <= PicoHandPoseSpec.LeftResetPinchDistance +
                           PicoHandPoseSpec.LeftResetPinchMargin;
            if (!pinchOk)
                return false;

            var cam = Camera.main;
            if (cam == null)
                return false;

            var toFace = cam.transform.position - palm.position;
            if (toFace.sqrMagnitude < 0.0001f)
                return false;

            var palmNormal = palm.up;
            var angle = Vector3.Angle(palmNormal, toFace);
            var alt = Vector3.Angle(-palm.forward, toFace);
            var best = Mathf.Min(angle, alt);
            var limit = PicoHandPoseSpec.TowardsFaceAngleThreshold +
                        PicoHandPoseSpec.TowardsFaceThresholdWidth;
            return best <= limit;
        }

        static bool FingerCurlMatches(XRHand hand, HandFinger finger, float curlMin, float curlMax)
        {
            if (!TryFingerAngles(hand, finger, out _, out var curl))
                return false;

            return curl >= curlMin && curl <= curlMax;
        }

        static bool TryFingerAngles(XRHand hand, HandFinger finger, out float flexion, out float curl)
        {
            flexion = 0f;
            curl = 0f;

            XRHandJointID metaId;
            XRHandJointID proximalId;
            XRHandJointID intermediateId;
            XRHandJointID distalId;
            XRHandJointID tipId;

            switch (finger)
            {
                case HandFinger.Thumb:
                    metaId = XRHandJointID.ThumbMetacarpal;
                    proximalId = XRHandJointID.ThumbProximal;
                    intermediateId = XRHandJointID.ThumbDistal;
                    distalId = XRHandJointID.ThumbTip;
                    tipId = XRHandJointID.ThumbTip;
                    break;
                case HandFinger.Index:
                    metaId = XRHandJointID.IndexMetacarpal;
                    proximalId = XRHandJointID.IndexProximal;
                    intermediateId = XRHandJointID.IndexIntermediate;
                    distalId = XRHandJointID.IndexDistal;
                    tipId = XRHandJointID.IndexTip;
                    break;
                case HandFinger.Middle:
                    metaId = XRHandJointID.MiddleMetacarpal;
                    proximalId = XRHandJointID.MiddleProximal;
                    intermediateId = XRHandJointID.MiddleIntermediate;
                    distalId = XRHandJointID.MiddleDistal;
                    tipId = XRHandJointID.MiddleTip;
                    break;
                case HandFinger.Ring:
                    metaId = XRHandJointID.RingMetacarpal;
                    proximalId = XRHandJointID.RingProximal;
                    intermediateId = XRHandJointID.RingIntermediate;
                    distalId = XRHandJointID.RingDistal;
                    tipId = XRHandJointID.RingTip;
                    break;
                default:
                    metaId = XRHandJointID.LittleMetacarpal;
                    proximalId = XRHandJointID.LittleProximal;
                    intermediateId = XRHandJointID.LittleIntermediate;
                    distalId = XRHandJointID.LittleDistal;
                    tipId = XRHandJointID.LittleTip;
                    break;
            }

            if (!hand.GetJoint(proximalId).TryGetPose(out var proximal) ||
                !hand.GetJoint(tipId).TryGetPose(out var tip))
                return false;

            if (hand.GetJoint(metaId).TryGetPose(out var meta) &&
                hand.GetJoint(intermediateId).TryGetPose(out var mid))
            {
                flexion = Vector3.Angle(proximal.position - meta.position, mid.position - proximal.position);
            }
            else if (hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palm))
            {
                flexion = Vector3.Angle(proximal.position - palm.position, tip.position - proximal.position);
            }
            else
            {
                return false;
            }

            if (finger == HandFinger.Thumb)
            {
                curl = flexion;
                return true;
            }

            if (hand.GetJoint(intermediateId).TryGetPose(out var pip) &&
                hand.GetJoint(distalId).TryGetPose(out var dip))
            {
                var pipAngle = Vector3.Angle(pip.position - proximal.position, dip.position - pip.position);
                var dipAngle = Vector3.Angle(dip.position - pip.position, tip.position - dip.position);
                curl = 0.5f * (pipAngle + dipAngle);
                return true;
            }

            if (hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palm2))
            {
                var dist = Vector3.Distance(tip.position, palm2.position);
                curl = Mathf.Lerp(PicoHandPoseSpec.CurlCloseMax, PicoHandPoseSpec.CurlOpenMin, Mathf.InverseLerp(0.04f, 0.12f, dist));
                return true;
            }

            return false;
        }

        static string ConfigLabel(ScriptableObject asset, string fallback)
        {
            return asset != null ? asset.name : fallback;
        }
#else
        protected override void ProcessHand(object hand) { }
#endif
    }
}
