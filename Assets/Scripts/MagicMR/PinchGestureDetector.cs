using UnityEngine;
using UnityEngine.Events;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    /// <summary>
    /// Gesture A: thumb + index pinch (select).
    /// </summary>
    public class PinchGestureDetector : HandGestureDetectorBase
    {
        [SerializeField]
        float m_PinchDistanceThreshold = 0.025f;

        [SerializeField]
        UnityEvent m_PinchStarted;

        [SerializeField]
        UnityEvent m_PinchEnded;

#if XR_HANDS_1_1_OR_NEWER
        bool m_IsPinching;

        protected override void ProcessHand(XRHand hand)
        {
            var isPinching = IsPinching(hand);
            if (isPinching && !m_IsPinching)
                m_PinchStarted.Invoke();
            else if (!isPinching && m_IsPinching)
                m_PinchEnded.Invoke();

            m_IsPinching = isPinching;
        }

        bool IsPinching(XRHand hand)
        {
            if (!TryGetJointPose(hand, XRHandJointID.ThumbTip, out var thumbTip) ||
                !TryGetJointPose(hand, XRHandJointID.IndexTip, out var indexTip))
            {
                return false;
            }

            return Vector3.Distance(thumbTip.position, indexTip.position) <= m_PinchDistanceThreshold;
        }
#else
        protected override void ProcessHand(object hand) { }
#endif
    }
}
