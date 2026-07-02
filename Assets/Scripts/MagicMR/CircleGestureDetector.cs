using UnityEngine;
using UnityEngine.Events;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    /// <summary>
    /// Gesture C: index finger draws a rough circle (bounding box + path length).
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
        UnityEvent m_CircleDetected;

        public UnityEvent DetectedEvent => m_CircleDetected;

#if XR_HANDS_1_1_OR_NEWER
        float m_WindowStartTime;
        Vector3 m_LastIndexPosition;
        bool m_HasLastIndexPosition;
        float m_PathLength;
        float m_MinX;
        float m_MaxX;
        float m_MinY;
        float m_MaxY;
        bool m_Tracking;

        protected override void ProcessHand(XRHand hand)
        {
            if (!TryGetJointPose(hand, XRHandJointID.IndexTip, out var indexPose))
                return;

            var now = Time.time;
            if (!m_Tracking || now - m_WindowStartTime > m_WindowSeconds)
                ResetWindow(now, indexPose.position);

            if (m_HasLastIndexPosition)
                m_PathLength += Vector3.Distance(indexPose.position, m_LastIndexPosition);

            m_LastIndexPosition = indexPose.position;
            m_HasLastIndexPosition = true;

            m_MinX = Mathf.Min(m_MinX, indexPose.position.x);
            m_MaxX = Mathf.Max(m_MaxX, indexPose.position.x);
            m_MinY = Mathf.Min(m_MinY, indexPose.position.y);
            m_MaxY = Mathf.Max(m_MaxY, indexPose.position.y);

            var width = m_MaxX - m_MinX;
            var height = m_MaxY - m_MinY;
            if (width < 0.05f || height < 0.05f)
                return;

            var aspect = Mathf.Max(width, height) / Mathf.Min(width, height);
            if (m_PathLength >= m_MinPathLength && aspect <= m_MaxAspectRatio)
            {
                m_CircleDetected.Invoke();
                ResetWindow(now, indexPose.position);
            }
        }

        void ResetWindow(float now, Vector3 position)
        {
            m_WindowStartTime = now;
            m_Tracking = true;
            m_PathLength = 0f;
            m_MinX = m_MaxX = position.x;
            m_MinY = m_MaxY = position.y;
            m_LastIndexPosition = position;
            m_HasLastIndexPosition = false;
        }
#else
        protected override void ProcessHand(object hand) { }
#endif
    }
}
