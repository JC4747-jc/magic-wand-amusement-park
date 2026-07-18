using UnityEngine;
using UnityEngine.Events;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    /// <summary>
    /// Gesture D: thumb + middle finger snap (fast close). Both hands.
    /// </summary>
    public class SnapGestureDetector : HandGestureDetectorBase
    {
        [SerializeField]
        float m_CloseDistanceThreshold = StudySpec.SnapCloseDistanceThreshold;

        [SerializeField]
        float m_OpenDistanceThreshold = StudySpec.SnapOpenDistanceThreshold;

        [SerializeField]
        float m_MinCloseSpeed = StudySpec.SnapMinCloseSpeed;

        [SerializeField]
        UnityEvent m_SnapDetected = new UnityEvent();

        public UnityEvent DetectedEvent => m_SnapDetected ??= new UnityEvent();

#if XR_HANDS_1_1_OR_NEWER
        struct SnapState
        {
            public bool WasOpen;
            public bool FiredThisAttempt;
            public float LastDistance;
            public float MinDistanceThisAttempt;
            public float MaxSpeedThisAttempt;
            public bool HasLast;
        }

        SnapState m_Left = new SnapState { WasOpen = true, MinDistanceThisAttempt = float.MaxValue };
        SnapState m_Right = new SnapState { WasOpen = true, MinDistanceThisAttempt = float.MaxValue };
        float m_LastFireTime;

        void Awake()
        {
            m_CloseDistanceThreshold = StudySpec.SnapCloseDistanceThreshold;
            m_OpenDistanceThreshold = StudySpec.SnapOpenDistanceThreshold;
            m_MinCloseSpeed = StudySpec.SnapMinCloseSpeed;
            // Snap conflicts with pinch — keep disabled; use fist-burst instead.
            enabled = false;
        }

        protected override void OnEnable()
        {
            // Never subscribe / run — fist-burst replaces snap.
            enabled = false;
        }

        protected override void OnUpdatedHands(
            XRHandSubsystem subsystem,
            XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags,
            XRHandSubsystem.UpdateType updateType)
        {
            if ((updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints) != 0)
                UpdateSnap(subsystem.leftHand, ref m_Left);
            if ((updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0)
                UpdateSnap(subsystem.rightHand, ref m_Right);
        }

        protected override void ProcessHand(XRHand hand)
        {
            UpdateSnap(hand, ref m_Right);
        }

        void UpdateSnap(XRHand hand, ref SnapState state)
        {
            if (!TryGetJointPose(hand, XRHandJointID.ThumbTip, out var thumbTip) ||
                !TryGetJointPose(hand, XRHandJointID.MiddleTip, out var middleTip))
            {
                return;
            }

            var distance = Vector3.Distance(thumbTip.position, middleTip.position);
            var speed = 0f;
            if (state.HasLast)
                speed = (state.LastDistance - distance) / Mathf.Max(Time.deltaTime, 0.0001f);

            state.LastDistance = distance;
            state.HasLast = true;
            state.MinDistanceThisAttempt = Mathf.Min(state.MinDistanceThisAttempt, distance);
            state.MaxSpeedThisAttempt = Mathf.Max(state.MaxSpeedThisAttempt, speed);

            if (state.WasOpen &&
                distance <= m_CloseDistanceThreshold &&
                speed >= m_MinCloseSpeed &&
                Time.time - m_LastFireTime > 0.6f)
            {
                m_LastFireTime = Time.time;
                state.WasOpen = false;
                state.FiredThisAttempt = true;
                Debug.Log(
                    $"[MagicMR] Snap detected dist={distance:F3} speed={speed:F2} hand={hand.handedness}.");
                m_SnapDetected.Invoke();
            }
            else if (distance >= m_OpenDistanceThreshold)
            {
                if (!state.WasOpen && !state.FiredThisAttempt &&
                    state.MinDistanceThisAttempt < m_OpenDistanceThreshold * 0.9f)
                {
                    Debug.Log(
                        $"[MagicMR] Snap attempt missed: minDist={state.MinDistanceThisAttempt:F3} " +
                        $"(need<= {m_CloseDistanceThreshold:F3}) maxSpeed={state.MaxSpeedThisAttempt:F2} " +
                        $"(need>= {m_MinCloseSpeed:F2})");
                }

                state.WasOpen = true;
                state.FiredThisAttempt = false;
                state.MinDistanceThisAttempt = float.MaxValue;
                state.MaxSpeedThisAttempt = 0f;
            }
        }
#else
        protected override void ProcessHand(object hand) { }
#endif
    }
}
