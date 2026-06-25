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
        float m_CloseDistanceThreshold = 0.03f;

        [SerializeField]
        float m_OpenDistanceThreshold = 0.08f;

        [SerializeField]
        float m_MinCloseSpeed = 2f;

        [SerializeField]
        UnityEvent m_SnapDetected;

#if XR_HANDS_1_1_OR_NEWER
        bool m_WasOpen = true;
        float m_LastDistance;

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

            if (m_WasOpen && distance <= m_CloseDistanceThreshold && speed >= m_MinCloseSpeed)
            {
                m_SnapDetected.Invoke();
                m_WasOpen = false;
            }
            else if (distance >= m_OpenDistanceThreshold)
            {
                m_WasOpen = true;
            }
        }
#else
        protected override void ProcessHand(object hand) { }
#endif
    }
}
