using UnityEngine;
using UnityEngine.Events;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    /// <summary>
    /// Right-hand pinch sensor for Appearance (quick tap).
    /// Calibration hold is owned by <see cref="MRGestureController"/> (coyote time).
    /// Left hand is reserved for Reset and never fires Appearance.
    /// </summary>
    public class PinchGestureDetector : HandGestureDetectorBase
    {
        [SerializeField]
        float m_PinchDistanceThreshold = StudySpec.PinchDistanceThreshold;

        [SerializeField]
        float m_HoldPinchDistanceThreshold = StudySpec.HoldPinchDistanceThreshold;

        [SerializeField]
        float m_HoldDurationSeconds = StudySpec.PinchHoldSeconds;

        [SerializeField]
        float m_TapMaxSeconds = StudySpec.AppearanceTapMaxSeconds;

        [SerializeField]
        float m_HoldDropoutGraceSeconds = StudySpec.CoyoteTimeSeconds;

        [SerializeField]
        float m_StartupGuardSeconds = 2.5f;

        [SerializeField]
        float m_FollowGripDistance = 0.07f;

        [SerializeField]
        UnityEvent m_PinchStarted = new UnityEvent();

        [SerializeField]
        UnityEvent m_PinchEnded = new UnityEvent();

        [SerializeField]
        UnityEvent m_PinchHeld = new UnityEvent();

        public UnityEvent DetectedEvent => m_PinchStarted ??= new UnityEvent();
        public UnityEvent HeldEvent => m_PinchHeld ??= new UnityEvent();

        public float CalibrationHoldProgress { get; private set; }

        public Vector3 LastPinchPosition { get; private set; }
        public Vector3 LastIndexTipPosition { get; private set; }

        /// <summary>Tight grip for FollowHand — not the loose calib threshold.</summary>
        public bool IsAnyHandHolding { get; private set; }

        float m_EarliestHoldTime;

        void Awake()
        {
            m_PinchDistanceThreshold = StudySpec.PinchDistanceThreshold;
            m_HoldPinchDistanceThreshold = StudySpec.HoldPinchDistanceThreshold;
            m_HoldDurationSeconds = StudySpec.PinchHoldSeconds;
            m_TapMaxSeconds = StudySpec.AppearanceTapMaxSeconds;
            m_HoldDropoutGraceSeconds = StudySpec.CoyoteTimeSeconds;
            m_EarliestHoldTime = Time.unscaledTime + m_StartupGuardSeconds;
        }

#if XR_HANDS_1_1_OR_NEWER
        struct PinchState
        {
            public bool IsPinching;
            public bool HoldFired;
            public float AccumulatedHold;
            public float PinchStartTime;
            public float LastHoldSeenTime;
            public float LastTipDist;
            public float NextDiagLogTime;
        }

        PinchState m_RightState;
        float m_SuppressTapUntil;

        public void SuppressTapFor(float seconds)
        {
            m_SuppressTapUntil = Time.unscaledTime + seconds;
        }

        public void ResetForNewTrial()
        {
            m_RightState = default;
            CalibrationHoldProgress = 0f;
            IsAnyHandHolding = false;
            m_SuppressTapUntil = 0f;
            m_EarliestHoldTime = Time.unscaledTime + 1.0f;
        }

        protected override void OnUpdatedHands(
            XRHandSubsystem subsystem,
            XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags,
            XRHandSubsystem.UpdateType updateType)
        {
            // Right hand only — left is Reset-exclusive (FSM).
            UpdatePinchHand(
                subsystem.rightHand,
                ref m_RightState,
                (updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0);

            var fsm = MRGestureController.Instance;
            CalibrationHoldProgress = fsm != null
                ? fsm.CalibrationHoldProgress
                : HoldProgress(ref m_RightState);

            IsAnyHandHolding = IsTightGrip(ref m_RightState);
        }

        protected override void ProcessHand(XRHand hand)
        {
            if (hand.handedness != Handedness.Right)
                return;

            UpdatePinchHand(hand, ref m_RightState, hand.isTracked);
            var fsm = MRGestureController.Instance;
            CalibrationHoldProgress = fsm != null
                ? fsm.CalibrationHoldProgress
                : HoldProgress(ref m_RightState);
            IsAnyHandHolding = IsTightGrip(ref m_RightState);
        }

        bool IsTightGrip(ref PinchState state)
        {
            return state.LastTipDist > 0f &&
                   state.LastTipDist <= m_FollowGripDistance &&
                   Time.unscaledTime - state.LastHoldSeenTime < 0.25f;
        }

        float HoldProgress(ref PinchState state)
        {
            if (state.HoldFired)
                return 1f;
            return Mathf.Clamp01(state.AccumulatedHold / Mathf.Max(0.01f, m_HoldDurationSeconds));
        }

        void UpdatePinchHand(XRHand hand, ref PinchState state, bool jointsUpdated)
        {
            var now = Time.unscaledTime;
            var dt = Time.unscaledDeltaTime;
            var fsmOwnsCalibration = MRGestureController.Instance != null;

            if (!hand.isTracked || !jointsUpdated)
            {
                if (state.AccumulatedHold > 0f &&
                    now - state.LastHoldSeenTime <= m_HoldDropoutGraceSeconds)
                    return;

                if (state.IsPinching || state.AccumulatedHold > 0f)
                    m_PinchEnded.Invoke();

                state.IsPinching = false;
                state.AccumulatedHold = 0f;
                state.HoldFired = false;
                state.LastTipDist = 999f;
                return;
            }

            if (!TryGetTips(hand, out var thumbIndexDist, out var indexTip))
            {
                if (state.AccumulatedHold > 0f &&
                    now - state.LastHoldSeenTime <= m_HoldDropoutGraceSeconds)
                    return;

                state.AccumulatedHold = 0f;
                state.LastTipDist = 999f;
                return;
            }

            LastIndexTipPosition = indexTip;
            state.LastTipDist = thumbIndexDist;

            var isPinching = thumbIndexDist <= m_PinchDistanceThreshold;
            var isHolding = thumbIndexDist <= m_HoldPinchDistanceThreshold;
            var needsCalibration = !IsAppearanceAllowed();

            if (now >= state.NextDiagLogTime)
            {
                state.NextDiagLogTime = now + 0.7f;
                Debug.Log(
                    $"[MagicMR] Right pinch tip={thumbIndexDist:F3} " +
                    $"(tap≤{m_PinchDistanceThreshold:F2}) " +
                    $"{(isPinching ? "PINCH" : "open")} fsmCalib={fsmOwnsCalibration}");
            }

            // Legacy HeldEvent path only when FSM is absent.
            if (!fsmOwnsCalibration && needsCalibration && isHolding && now >= m_EarliestHoldTime)
            {
                LastPinchPosition = indexTip;
                state.LastHoldSeenTime = now;
                state.AccumulatedHold += dt;

                if (!state.HoldFired && state.AccumulatedHold >= m_HoldDurationSeconds)
                {
                    state.HoldFired = true;
                    LastPinchPosition = indexTip;
                    Debug.Log($"[MagicMR] Right-hand pinch-and-hold (calib) at {indexTip}.");
                    m_PinchHeld.Invoke();
                    state.AccumulatedHold = 0f;
                }
            }
            else if (!fsmOwnsCalibration && needsCalibration && state.AccumulatedHold > 0f &&
                     now - state.LastHoldSeenTime > m_HoldDropoutGraceSeconds)
            {
                m_PinchEnded.Invoke();
                state.AccumulatedHold = 0f;
                state.HoldFired = false;
            }
            else
            {
                state.AccumulatedHold = 0f;
                state.HoldFired = false;
                if (isHolding || isPinching)
                    state.LastHoldSeenTime = now;
            }

            if (isPinching && !state.IsPinching)
                state.PinchStartTime = now;

            // Appearance tap: quick tight pinch release in PinnedIdle (FSM gates Notify).
            if (!isPinching && state.IsPinching && !needsCalibration)
            {
                var heldFor = now - state.PinchStartTime;
                if (heldFor < m_TapMaxSeconds && now >= m_SuppressTapUntil)
                {
                    Debug.Log("[MagicMR] Pinch tap detected (Appearance).");
                    m_PinchStarted?.Invoke();
                    GestureManager.Notify(EditDimension.Appearance, "pinch");
                }
            }

            state.IsPinching = isPinching;
        }

        static bool IsAppearanceAllowed()
        {
            var fsm = MRGestureController.Instance;
            if (fsm != null)
                return fsm.State == MRState.PinnedIdle || fsm.State == MRState.FollowingHand;

            var anchor = Object.FindFirstObjectByType<LighterAnchorManager>();
            return anchor == null || anchor.IsCalibrated;
        }

        static bool TryGetTips(XRHand hand, out float thumbIndexDist, out Vector3 indexTip)
        {
            thumbIndexDist = 999f;
            indexTip = default;

            if (!hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb) ||
                !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index))
            {
                return false;
            }

            thumbIndexDist = Vector3.Distance(thumb.position, index.position);
            indexTip = index.position;
            return true;
        }
#else
        protected override void ProcessHand(object hand) { }
#endif
    }
}
