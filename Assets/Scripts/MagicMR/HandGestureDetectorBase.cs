using System.Collections.Generic;
using UnityEngine;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    /// <summary>
    /// Shared XR Hands subsystem wiring for custom gesture detectors.
    /// </summary>
    public abstract class HandGestureDetectorBase : MonoBehaviour
    {
#if XR_HANDS_1_1_OR_NEWER
        [SerializeField]
        Handedness m_Handedness = Handedness.Right;

        XRHandSubsystem m_Subsystem;

        static readonly List<XRHandSubsystem> s_Subsystems = new List<XRHandSubsystem>();

        protected Handedness Handedness => m_Handedness;

        protected virtual void OnEnable()
        {
            TrySubscribeToHands();
        }

        protected virtual void OnDisable()
        {
            UnsubscribeFromHands();
        }

        void Update()
        {
            // XR Hand Subsystem often appears a frame or two after scene
            // load (after XR init). If we only subscribe in OnEnable we can
            // miss it entirely and gestures never fire — VstTest doesn't
            // use detectors so this race only shows up in MagicMR.
            if (m_Subsystem == null)
                TrySubscribeToHands();
        }

        void TrySubscribeToHands()
        {
            if (m_Subsystem != null)
                return;

            SubsystemManager.GetSubsystems(s_Subsystems);
            if (s_Subsystems.Count == 0)
                return;

            m_Subsystem = s_Subsystems[0];
            if (!m_Subsystem.running)
                m_Subsystem.Start();

            m_Subsystem.updatedHands += OnUpdatedHands;
        }

        void UnsubscribeFromHands()
        {
            if (m_Subsystem == null)
                return;

            m_Subsystem.updatedHands -= OnUpdatedHands;
            m_Subsystem = null;
        }

        /// <summary>Called after bootstrap starts the hand subsystem.</summary>
        public void EnsureHandTrackingSubscribed()
        {
            if (isActiveAndEnabled) TrySubscribeToHands();
        }

        public static void EnsureAllSubscribed()
        {
            foreach (var detector in FindObjectsByType<HandGestureDetectorBase>(FindObjectsSortMode.None))
                detector.EnsureHandTrackingSubscribed();
        }

        protected virtual void OnUpdatedHands(
            XRHandSubsystem subsystem,
            XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags,
            XRHandSubsystem.UpdateType updateType)
        {
            var successFlag = m_Handedness == Handedness.Left
                ? XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints
                : XRHandSubsystem.UpdateSuccessFlags.RightHandJoints;

            if ((updateSuccessFlags & successFlag) != successFlag)
                return;

            var hand = m_Handedness == Handedness.Left ? subsystem.leftHand : subsystem.rightHand;
            if (!hand.isTracked)
                return;

            ProcessHand(hand);
        }

        protected abstract void ProcessHand(XRHand hand);

        protected static bool TryGetJointPose(XRHand hand, XRHandJointID jointId, out Pose pose)
        {
            return hand.GetJoint(jointId).TryGetPose(out pose);
        }
#else
        protected virtual void OnEnable()
        {
            Debug.LogError(
                "XR Hands package required. Install com.unity.xr.hands via Package Manager.",
                this);
        }

        protected abstract void ProcessHand(object hand);
#endif
    }
}
