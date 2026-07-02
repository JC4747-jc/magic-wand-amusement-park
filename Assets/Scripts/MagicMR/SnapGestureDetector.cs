using UnityEngine;
using UnityEngine.Events;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    /// <summary>
    /// Gesture D: thumb + middle finger snap (fast close).
    /// MVP fallback: use double pinch on PinchGestureDetector instead.
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
        UnityEvent m_SnapDetected;

        public UnityEvent DetectedEvent => m_SnapDetected;

#if XR_HANDS_1_1_OR_NEWER
        bool m_WasOpen = true;
        bool m_FiredThisAttempt;
        float m_LastDistance;
        float m_MinDistanceThisAttempt = float.MaxValue;
        float m_MaxSpeedThisAttempt;

        protected override void ProcessHand(XRHand hand)
        {
            if (!TryGetJointPose(hand, XRHandJointID.ThumbTip, out var thumbTip) ||
                !TryGetJointPose(hand, XRHandJointID.MiddleTip, out var middleTip))
            {
                return;
            }

            var distance = Vector3.Distance(thumbTip.position, middleTip.position);
            var speed = (m_LastDistance - distance) / Mathf.Max(Time.deltaTime, 0.0001f);
            m_LastDistance = distance;
            m_MinDistanceThisAttempt = Mathf.Min(m_MinDistanceThisAttempt, distance);
            m_MaxSpeedThisAttempt = Mathf.Max(m_MaxSpeedThisAttempt, speed);

            if (m_WasOpen && distance <= m_CloseDistanceThreshold && speed >= m_MinCloseSpeed)
            {
                m_SnapDetected.Invoke();
                m_WasOpen = false;
                m_FiredThisAttempt = true;
            }
            else if (distance >= m_OpenDistanceThreshold)
            {
                // Diagnostic: if the hand closed near the thresholds without
                // triggering, log the closest approach so thresholds can be
                // tuned from real on-device data.
                if (!m_WasOpen && !m_FiredThisAttempt && m_MinDistanceThisAttempt < m_OpenDistanceThreshold * 0.9f)
                {
                    Debug.Log(
                        $"[MagicMR] Snap attempt missed: minDist={m_MinDistanceThisAttempt:F3} " +
                        $"(need<= {m_CloseDistanceThreshold:F3}) maxSpeed={m_MaxSpeedThisAttempt:F2} " +
                        $"(need>= {m_MinCloseSpeed:F2})");
                }

                m_WasOpen = true;
                m_FiredThisAttempt = false;
                m_MinDistanceThisAttempt = float.MaxValue;
                m_MaxSpeedThisAttempt = 0f;
            }
        }
#else
        protected override void ProcessHand(object hand) { }
#endif
    }
}
