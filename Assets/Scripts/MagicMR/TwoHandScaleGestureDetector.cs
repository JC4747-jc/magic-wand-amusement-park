using UnityEngine;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    /// <summary>
    /// Two open hands pulling apart triggers one Scale edit. Moving them together
    /// only rearms recognition and never emits a shrink operation.
    /// </summary>
    public sealed class TwoHandScaleGestureDetector : HandGestureDetectorBase
    {
#if XR_HANDS_1_1_OR_NEWER
        bool m_Armed;
        float m_StartDistance;
        float m_StartedAt;
        float m_LastFireAt = -100f;

        protected override void OnUpdatedHands(XRHandSubsystem subsystem,
            XRHandSubsystem.UpdateSuccessFlags flags, XRHandSubsystem.UpdateType updateType)
        {
            if (updateType != XRHandSubsystem.UpdateType.Dynamic) return;
            bool bothFresh = (flags & XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints) != 0 &&
                             (flags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0;
            UpdateSpread(subsystem.leftHand, subsystem.rightHand, bothFresh);
        }

        protected override void ProcessHand(XRHand hand) { }

        void UpdateSpread(XRHand left, XRHand right, bool fresh)
        {
            if (!fresh || !TryOpenPalm(left, out var lp) || !TryOpenPalm(right, out var rp))
            {
                m_Armed = false;
                return;
            }

            float now = Time.time;
            float distance = Vector3.Distance(lp, rp);
            if (!m_Armed || now - m_StartedAt > StudySpec.ScaleGestureMaxSeconds)
            {
                m_Armed = true;
                m_StartDistance = distance;
                m_StartedAt = now;
                return;
            }

            // Hands coming closer may rearm, but there is deliberately no shrink event.
            if (distance <= StudySpec.ScaleGestureRearmMeters)
            {
                m_StartDistance = distance;
                m_StartedAt = now;
                return;
            }

            if (distance - m_StartDistance < StudySpec.ScaleGesturePullMeters || now - m_LastFireAt < .8f)
                return;

            m_LastFireAt = now;
            m_Armed = false;
            Debug.Log($"[MagicMR] Two-hand grow detected: {m_StartDistance:F2}m -> {distance:F2}m.");
            GestureManager.Notify(EditDimension.Scale, "two_hand_spread");
        }

        static bool TryOpenPalm(XRHand hand, out Vector3 palm)
        {
            palm = default;
            if (!hand.isTracked || !hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palmPose) ||
                !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index) ||
                !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb)) return false;
            palm = palmPose.position;
            return Vector3.Distance(index.position, thumb.position) >= .06f;
        }
#else
        protected override void ProcessHand(object hand) { }
#endif
    }
}
