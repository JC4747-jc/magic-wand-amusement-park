using UnityEngine;
using UnityEngine.Events;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    /// <summary>
    /// Agency: right index finger draws a rough circle (3D AABB + path length).
    /// Left hand ignored (Reset-exclusive).
    /// </summary>
    public class CircleGestureDetector : HandGestureDetectorBase
    {
        [SerializeField]
        float m_WindowSeconds = StudySpec.CircleWindowSeconds;

        [SerializeField]
        float m_MinPathLength = StudySpec.CircleMinPathLength;

        [SerializeField]
        float m_MaxAspectRatio = StudySpec.CircleMaxAspectRatio;

        [SerializeField]
        float m_MinAxisSpan = StudySpec.CircleMinAxisSpan;

        [SerializeField]
        UnityEvent m_CircleDetected = new UnityEvent();

        public UnityEvent DetectedEvent => m_CircleDetected ??= new UnityEvent();

#if XR_HANDS_1_1_OR_NEWER
        struct CircleState
        {
            public float WindowStartTime;
            public Vector3 LastIndexPosition;
            public bool HasLastIndexPosition;
            public float PathLength;
            public Vector3 Min;
            public Vector3 Max;
            public bool Tracking;
        }

        CircleState m_Right;
        float m_LastFireTime;

        void Awake()
        {
            m_WindowSeconds = StudySpec.CircleWindowSeconds;
            m_MinPathLength = StudySpec.CircleMinPathLength;
            m_MaxAspectRatio = StudySpec.CircleMaxAspectRatio;
            m_MinAxisSpan = StudySpec.CircleMinAxisSpan;
        }

        protected override void OnUpdatedHands(
            XRHandSubsystem subsystem,
            XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags,
            XRHandSubsystem.UpdateType updateType)
        {
            if ((updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0)
                UpdateCircle(subsystem.rightHand, ref m_Right);
        }

        protected override void ProcessHand(XRHand hand)
        {
            if (hand.handedness != Handedness.Right)
                return;
            UpdateCircle(hand, ref m_Right);
        }

        void UpdateCircle(XRHand hand, ref CircleState state)
        {
            if (!hand.isTracked || !TryGetJointPose(hand, XRHandJointID.IndexTip, out var indexPose))
                return;

            var now = Time.time;
            if (!state.Tracking || now - state.WindowStartTime > m_WindowSeconds)
                ResetWindow(ref state, now, indexPose.position);

            if (state.HasLastIndexPosition)
                state.PathLength += Vector3.Distance(indexPose.position, state.LastIndexPosition);

            state.LastIndexPosition = indexPose.position;
            state.HasLastIndexPosition = true;

            state.Min = Vector3.Min(state.Min, indexPose.position);
            state.Max = Vector3.Max(state.Max, indexPose.position);

            var size = state.Max - state.Min;
            float a = size.x, b = size.y, c = size.z;
            if (a < b) (a, b) = (b, a);
            if (b < c) (b, c) = (c, b);
            if (a < b) (a, b) = (b, a);

            if (a < m_MinAxisSpan || b < m_MinAxisSpan)
                return;

            var aspect = a / Mathf.Max(b, 0.0001f);
            if (state.PathLength >= m_MinPathLength && aspect <= m_MaxAspectRatio)
            {
                if (now - m_LastFireTime < 0.8f)
                {
                    ResetWindow(ref state, now, indexPose.position);
                    return;
                }

                m_LastFireTime = now;
                Debug.Log(
                    $"[MagicMR] Circle detected path={state.PathLength:F2} aspect={aspect:F2} axes=({a:F2},{b:F2}).");
                m_CircleDetected?.Invoke();
                GestureManager.Notify(EditDimension.Agency, "circle");
                ResetWindow(ref state, now, indexPose.position);
            }
        }

        static void ResetWindow(ref CircleState state, float now, Vector3 position)
        {
            state.WindowStartTime = now;
            state.Tracking = true;
            state.PathLength = 0f;
            state.Min = state.Max = position;
            state.LastIndexPosition = position;
            state.HasLastIndexPosition = false;
        }
#else
        protected override void ProcessHand(object hand) { }
#endif
    }
}
