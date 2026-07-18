using UnityEngine;
using UnityEngine.Events;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    /// <summary>
    /// Deconstruction: right fist then burst-open within 0.3s. Snap disabled.
    /// Left hand ignored (Reset-exclusive).
    /// </summary>
    public class FistBurstGestureDetector : HandGestureDetectorBase
    {
        [SerializeField]
        float m_FistMaxTipDistance = StudySpec.FistMaxTipDistance;

        [SerializeField]
        float m_OpenMinTipDistance = StudySpec.FistOpenMinTipDistance;

        [SerializeField]
        float m_MinFistHoldSeconds = StudySpec.FistMinHoldSeconds;

        [SerializeField]
        float m_MaxBurstSeconds = StudySpec.FistMaxBurstSeconds;

        [SerializeField]
        UnityEvent m_FistBurstDetected = new UnityEvent();

        public UnityEvent DetectedEvent => m_FistBurstDetected ??= new UnityEvent();

#if XR_HANDS_1_1_OR_NEWER
        struct BurstState
        {
            public bool InFist;
            public float FistStartTime;
            public float LastAvgTipDistance;
        }

        BurstState m_Right;
        float m_LastFireTime;

        void Awake()
        {
            m_FistMaxTipDistance = StudySpec.FistMaxTipDistance;
            m_OpenMinTipDistance = StudySpec.FistOpenMinTipDistance;
            m_MinFistHoldSeconds = StudySpec.FistMinHoldSeconds;
            m_MaxBurstSeconds = StudySpec.FistMaxBurstSeconds;
        }

        protected override void OnUpdatedHands(
            XRHandSubsystem subsystem,
            XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags,
            XRHandSubsystem.UpdateType updateType)
        {
            if ((updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0)
                UpdateBurst(subsystem.rightHand, ref m_Right);
        }

        protected override void ProcessHand(XRHand hand)
        {
            if (hand.handedness != Handedness.Right)
                return;
            UpdateBurst(hand, ref m_Right);
        }

        void UpdateBurst(XRHand hand, ref BurstState state)
        {
            if (!hand.isTracked || !TryAverageTipDistance(hand, out var avgDist))
                return;

            var now = Time.time;
            var isFist = avgDist <= m_FistMaxTipDistance;
            var isOpen = avgDist >= m_OpenMinTipDistance;

            if (isFist)
            {
                if (!state.InFist)
                {
                    state.InFist = true;
                    state.FistStartTime = now;
                }
            }
            else if (state.InFist && isOpen)
            {
                var held = now - state.FistStartTime;
                if (held >= m_MinFistHoldSeconds &&
                    held <= m_MaxBurstSeconds &&
                    now - m_LastFireTime > 0.8f)
                {
                    m_LastFireTime = now;
                    Debug.Log(
                        $"[MagicMR] Fist-burst detected avgTip={avgDist:F3} held={held:F2}s.");
                    m_FistBurstDetected?.Invoke();
                    GestureManager.Notify(EditDimension.Deconstruction, "fist_burst");
                }

                state.InFist = false;
            }
            else if (!isFist)
            {
                state.InFist = false;
            }

            state.LastAvgTipDistance = avgDist;
        }

        static bool TryAverageTipDistance(XRHand hand, out float average)
        {
            average = 0f;
            if (!hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palm))
                return false;

            var tips = new[]
            {
                XRHandJointID.IndexTip,
                XRHandJointID.MiddleTip,
                XRHandJointID.RingTip,
                XRHandJointID.LittleTip
            };

            var sum = 0f;
            var count = 0;
            foreach (var tipId in tips)
            {
                if (!hand.GetJoint(tipId).TryGetPose(out var tip))
                    continue;
                sum += Vector3.Distance(tip.position, palm.position);
                count++;
            }

            if (count < 3)
                return false;

            average = sum / count;
            return true;
        }
#else
        protected override void ProcessHand(object hand) { }
#endif
    }
}
