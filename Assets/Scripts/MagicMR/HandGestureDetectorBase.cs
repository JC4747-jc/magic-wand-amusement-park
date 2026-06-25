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
            SubsystemManager.GetSubsystems(s_Subsystems);
            if (s_Subsystems.Count == 0)
            {
                Debug.LogWarning("XR Hand Subsystem not found. Enable Hand Tracking on PXR_Manager.", this);
                return;
            }

            m_Subsystem = s_Subsystems[0];
            m_Subsystem.updatedHands += OnUpdatedHands;
        }

        protected virtual void OnDisable()
        {
            if (m_Subsystem == null)
                return;

            m_Subsystem.updatedHands -= OnUpdatedHands;
            m_Subsystem = null;
        }

        void OnUpdatedHands(
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
