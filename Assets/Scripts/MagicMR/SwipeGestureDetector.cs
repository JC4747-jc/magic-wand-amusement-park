using UnityEngine;
using UnityEngine.Events;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    /// <summary>
    /// Gesture B: fast palm movement within a short window.
    /// </summary>
    public class SwipeGestureDetector : HandGestureDetectorBase
    {
        [SerializeField]
        float m_SwipeSpeedThreshold = 1.5f;

        [SerializeField]
        float m_CooldownSeconds = 0.5f;

        [SerializeField]
        UnityEvent m_SwipeDetected;

#if XR_HANDS_1_1_OR_NEWER
        Vector3 m_LastPalmPosition;
        bool m_HasLastPosition;
        float m_LastSwipeTime;

        protected override void ProcessHand(XRHand hand)
        {
            if (!TryGetJointPose(hand, XRHandJointID.Palm, out var palmPose))
                return;

            if (!m_HasLastPosition)
            {
                m_LastPalmPosition = palmPose.position;
                m_HasLastPosition = true;
                return;
            }

            var speed = (palmPose.position - m_LastPalmPosition).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            m_LastPalmPosition = palmPose.position;

            if (speed >= m_SwipeSpeedThreshold && Time.time - m_LastSwipeTime >= m_CooldownSeconds)
            {
                m_LastSwipeTime = Time.time;
                m_SwipeDetected.Invoke();
            }
        }
#else
        protected override void ProcessHand(object hand) { }
#endif
    }
}
