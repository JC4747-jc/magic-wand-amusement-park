using UnityEngine;
using UnityEngine.Events;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    /// <summary>
    /// Rule: right-hand fast horizontal wave / swipe. Left hand ignored.
    /// </summary>
    public class SwipeGestureDetector : HandGestureDetectorBase
    {
        [SerializeField]
        float m_SwipeSpeedThreshold = StudySpec.SwipeSpeedThreshold;

        [SerializeField]
        float m_CooldownSeconds = StudySpec.SwipeCooldownSeconds;

        [SerializeField]
        float m_MinHorizontalRatio = StudySpec.SwipeMinHorizontalRatio;

        [SerializeField]
        UnityEvent m_SwipeDetected = new UnityEvent();

        public UnityEvent DetectedEvent => m_SwipeDetected ??= new UnityEvent();

#if XR_HANDS_1_1_OR_NEWER
        struct SwipeState
        {
            public Vector3 LastPalmPosition;
            public bool HasLastPosition;
        }

        SwipeState m_Right;
        float m_LastSwipeTime;

        void Awake()
        {
            m_SwipeSpeedThreshold = StudySpec.SwipeSpeedThreshold;
            m_CooldownSeconds = StudySpec.SwipeCooldownSeconds;
            m_MinHorizontalRatio = StudySpec.SwipeMinHorizontalRatio;
        }

        protected override void OnUpdatedHands(
            XRHandSubsystem subsystem,
            XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags,
            XRHandSubsystem.UpdateType updateType)
        {
            if ((updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0)
                UpdateSwipe(subsystem.rightHand, ref m_Right);
        }

        protected override void ProcessHand(XRHand hand)
        {
            if (hand.handedness != Handedness.Right)
                return;
            UpdateSwipe(hand, ref m_Right);
        }

        void UpdateSwipe(XRHand hand, ref SwipeState state)
        {
            if (!hand.isTracked || !TryGetJointPose(hand, XRHandJointID.Palm, out var palmPose))
                return;

            if (!state.HasLastPosition)
            {
                state.LastPalmPosition = palmPose.position;
                state.HasLastPosition = true;
                return;
            }

            var delta = palmPose.position - state.LastPalmPosition;
            state.LastPalmPosition = palmPose.position;

            var speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            if (speed < m_SwipeSpeedThreshold)
                return;

            // Prefer horizontal (XZ) waves over vertical flicks.
            var horizontal = new Vector3(delta.x, 0f, delta.z);
            var horizRatio = horizontal.magnitude / Mathf.Max(delta.magnitude, 0.0001f);
            if (horizRatio < m_MinHorizontalRatio)
                return;

            if (Time.time - m_LastSwipeTime < m_CooldownSeconds)
                return;

            m_LastSwipeTime = Time.time;
            Debug.Log($"[MagicMR] Swipe detected speed={speed:F2} horizRatio={horizRatio:F2}.");
            m_SwipeDetected?.Invoke();
            GestureManager.Notify(EditDimension.Rule, "swipe");
        }
#else
        protected override void ProcessHand(object hand) { }
#endif
    }
}
