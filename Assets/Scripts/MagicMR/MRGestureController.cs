using System.Collections.Generic;
using UnityEngine;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    public enum MRState
    {
        Cooldown = 0,
        CalibrationReady = 1,
        PinnedIdle = 2,
        FollowingHand = 3
    }

    /// <summary>
    /// Strict FSM for asymmetric bimanual MR editing (CHI/UIST-style gating).
    /// Right hand: calibration, 4D edits, drag. Left hand: reset only.
    /// All gesture acceptance is polled / gated here — detectors only propose signals.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class MRGestureController : MonoBehaviour
    {
        public static MRGestureController Instance { get; private set; }

        [Header("FSM Timing")]
        [SerializeField]
        float m_CooldownSeconds = StudySpec.StartupCooldownSeconds;

        [SerializeField]
        float m_CalibrationHoldSeconds = StudySpec.PinchHoldSeconds;

        [SerializeField]
        float m_CoyoteTimeSeconds = StudySpec.CoyoteTimeSeconds;

        [SerializeField]
        float m_LeftResetHoldSeconds = StudySpec.LeftResetHoldSeconds;

        [SerializeField]
        float m_LeftPalmFacingDot = StudySpec.LeftPalmFacingDot;

        [Header("Pinch Strength")]
        [SerializeField]
        float m_PinchOpenDistance = StudySpec.PinchOpenDistance;

        [SerializeField]
        float m_PinchClosedDistance = StudySpec.PinchClosedDistance;

        [SerializeField]
        float m_ReleaseStrengthMax = StudySpec.RequireReleasePinchStrength;

        [SerializeField]
        float m_FollowPinchStrengthMin = StudySpec.FollowPinchStrengthMin;

        [SerializeField]
        float m_CalibrationProximityMeters = StudySpec.CalibrationProximityMeters;

        [SerializeField]
        float m_FollowAttachDistance = StudySpec.GripAttachDistance;

        [Header("References")]
        [SerializeField]
        LighterAnchorManager m_Anchor;

        [SerializeField]
        RealityEditor m_RealityEditor;

        [SerializeField]
        CalibrationRitual m_Ritual;

        [SerializeField]
        PinchGestureDetector m_PinchDetector;

        [SerializeField]
        Transform m_WireframeAnchor;

        MRState m_State = MRState.Cooldown;
        float m_StateEnterTime;
        float m_CooldownEndsAt;

        // Calibration (right hand only) + coyote time
        float m_CalibAccumulated;
        float m_CalibCoyoteEndsAt = -1f;
        bool m_CalibCoyoteActive;

        // Post-calibration latch: block until right hand fully opens
        bool m_RequireRightHandRelease;

        // Left-hand reset charge
        float m_LeftResetAccumulated;

        // 4D mutual exclusion while a gesture is being applied
        float m_EditExclusiveUntil;
        EditDimension m_LastAcceptedEdit = EditDimension.None;

        Vector3 m_DeskPresetPosition;
        Quaternion m_DeskPresetRotation = Quaternion.identity;
        bool m_HasDeskPreset;

        Vector3 m_LastRightHandPosition;
        bool m_HasRightHandPosition;

#if XR_HANDS_1_1_OR_NEWER
        XRHandSubsystem m_HandSubsystem;
        static readonly List<XRHandSubsystem> s_Subsystems = new List<XRHandSubsystem>();
#endif

        public MRState State => m_State;
        public bool RequireRightHandRelease => m_RequireRightHandRelease;
        public float CalibrationHoldProgress =>
            Mathf.Clamp01(m_CalibAccumulated / Mathf.Max(0.01f, m_CalibrationHoldSeconds));
        public Vector3 DeskPresetPosition => m_DeskPresetPosition;
        public Quaternion DeskPresetRotation => m_DeskPresetRotation;
        public bool HasDeskPreset => m_HasDeskPreset;

        void Awake()
        {
            Instance = this;
            ResolveReferences();
        }

        void OnEnable()
        {
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            ResolveReferences();
            CaptureDeskPresetFromScene();
            EnterCooldown("boot");
        }

        void Update()
        {
            ResolveReferences();
            EnsureHandSubsystem();

            // Left reset is global — evaluated every frame regardless of state.
            PollLeftHandReset();

            switch (m_State)
            {
                case MRState.Cooldown:
                    UpdateCooldown();
                    break;
                case MRState.CalibrationReady:
                    UpdateCalibrationReady();
                    break;
                case MRState.PinnedIdle:
                    UpdatePinnedIdle();
                    break;
                case MRState.FollowingHand:
                    UpdateFollowingHand();
                    break;
            }
        }

        public void SetDeskPreset(Vector3 position, Quaternion rotation)
        {
            m_DeskPresetPosition = position;
            m_DeskPresetRotation = rotation;
            m_HasDeskPreset = true;
            m_Ritual?.SetWireframeWorldPose(position, rotation);
        }

        /// <summary>
        /// Gate for detector → GestureManager dispatch. Returns false to drop the gesture.
        /// </summary>
        public bool CanAcceptEditGesture(EditDimension dimension)
        {
            if (dimension == EditDimension.None)
                return false;

            if (m_State != MRState.PinnedIdle)
                return false;

            if (m_RequireRightHandRelease)
                return false;

            // Mutual exclusion window: one 4D gesture at a time (no overlap).
            if (Time.unscaledTime < m_EditExclusiveUntil)
                return false;

            return true;
        }

        public void NotifyEditAccepted(EditDimension dimension)
        {
            m_LastAcceptedEdit = dimension;
            m_EditExclusiveUntil = Time.unscaledTime + StudySpec.GlobalCooldownSeconds;
            if (dimension == EditDimension.Deconstruction)
                m_EditExclusiveUntil = Time.unscaledTime + 0.5f;
        }

        /// <summary>Full study reset → Cooldown + wireframe. Safe from any state.</summary>
        public void PerformFullReset(string reason = "left_reset")
        {
            Debug.Log($"[MagicMR] FSM reset ({reason}) → Cooldown.", this);

            m_CalibAccumulated = 0f;
            m_CalibCoyoteActive = false;
            m_CalibCoyoteEndsAt = -1f;
            m_RequireRightHandRelease = false;
            m_LeftResetAccumulated = 0f;
            m_EditExclusiveUntil = 0f;
            m_LastAcceptedEdit = EditDimension.None;

            m_PinchDetector?.ResetForNewTrial();
            m_PinchDetector?.SuppressTapFor(1.0f);

            if (m_RealityEditor != null)
            {
                m_RealityEditor.StopAllCoroutines();
                m_RealityEditor.ResetTarget();
            }

            m_Anchor?.BeginRecalibration();

            if (m_Ritual == null)
                m_Ritual = FindFirstObjectByType<CalibrationRitual>();
            m_Ritual?.RestartRitual();

            if (m_HasDeskPreset)
                m_Ritual?.SetWireframeWorldPose(m_DeskPresetPosition, m_DeskPresetRotation);

            FindFirstObjectByType<GestureManager>()?.NotifyStudyReset();
            DataLogger.Instance?.LogEvent(
                "study_reset",
                EditDimension.None,
                -1f,
                0f,
                0f,
                notes: reason);

            EnterCooldown(reason);
        }

        void EnterCooldown(string reason)
        {
            m_State = MRState.Cooldown;
            m_StateEnterTime = Time.unscaledTime;
            m_CooldownEndsAt = Time.unscaledTime + m_CooldownSeconds;
            m_RequireRightHandRelease = false;
            m_CalibAccumulated = 0f;
            m_CalibCoyoteActive = false;
            Debug.Log($"[MagicMR] FSM → Cooldown ({m_CooldownSeconds:F1}s) reason={reason}", this);
        }

        void EnterCalibrationReady()
        {
            m_State = MRState.CalibrationReady;
            m_StateEnterTime = Time.unscaledTime;
            m_CalibAccumulated = 0f;
            m_CalibCoyoteActive = false;
            m_RequireRightHandRelease = false;
            Debug.Log("[MagicMR] FSM → CalibrationReady (right pinch near wireframe).", this);
        }

        void EnterPinnedIdle(string reason)
        {
            m_State = MRState.PinnedIdle;
            m_StateEnterTime = Time.unscaledTime;
            Debug.Log($"[MagicMR] FSM → PinnedIdle ({reason})", this);
        }

        void EnterFollowingHand()
        {
            m_State = MRState.FollowingHand;
            m_StateEnterTime = Time.unscaledTime;
            Debug.Log("[MagicMR] FSM → FollowingHand (4D gated).", this);
        }

        void UpdateCooldown()
        {
            if (Time.unscaledTime >= m_CooldownEndsAt)
                EnterCalibrationReady();
        }

        void UpdateCalibrationReady()
        {
#if XR_HANDS_1_1_OR_NEWER
            if (!TrySampleRightHand(out var pinchStrength, out var pinchPos, out var tracked))
            {
                // Tracking loss: start / continue coyote, do not zero yet.
                if (m_CalibAccumulated > 0f)
                    TickCoyoteOrReset();
                return;
            }

            m_LastRightHandPosition = pinchPos;
            m_HasRightHandPosition = true;

            var nearWireframe = IsNearWireframe(pinchPos);
            var isPinching = pinchStrength >= StudySpec.CalibrationPinchStrengthMin;

            if (isPinching && nearWireframe && tracked)
            {
                m_CalibCoyoteActive = false;
                m_CalibAccumulated += Time.unscaledDeltaTime;

                if (m_CalibAccumulated >= m_CalibrationHoldSeconds)
                    CompleteCalibration(pinchPos);
            }
            else if (m_CalibAccumulated > 0f)
            {
                // Pinch lost or left proximity → coyote window before hard reset.
                TickCoyoteOrReset();
            }
#else
            // Editor / no XR Hands: idle in CalibrationReady.
#endif
        }

        void TickCoyoteOrReset()
        {
            if (!m_CalibCoyoteActive)
            {
                m_CalibCoyoteActive = true;
                m_CalibCoyoteEndsAt = Time.unscaledTime + m_CoyoteTimeSeconds;
                return;
            }

            if (Time.unscaledTime > m_CalibCoyoteEndsAt)
            {
                if (m_CalibAccumulated > 0.15f)
                {
                    Debug.Log(
                        $"[MagicMR] Calib coyote expired; held={m_CalibAccumulated:F2}s → reset.",
                        this);
                }

                m_CalibAccumulated = 0f;
                m_CalibCoyoteActive = false;
            }
            // else: still within coyote — keep accumulated time
        }

        void CompleteCalibration(Vector3 aimPoint)
        {
            m_CalibAccumulated = 0f;
            m_CalibCoyoteActive = false;

            if (m_Anchor == null)
                m_Anchor = FindFirstObjectByType<LighterAnchorManager>();

            m_Anchor?.CalibrateAt(aimPoint);
            m_PinchDetector?.ResetForNewTrial();
            m_PinchDetector?.SuppressTapFor(0.8f);

            m_RequireRightHandRelease = true;
            EnterPinnedIdle("calibrated");
            Debug.Log("[MagicMR] Calibration OK — requireRightHandRelease until open hand.", this);
        }

        void UpdatePinnedIdle()
        {
#if XR_HANDS_1_1_OR_NEWER
            if (!TrySampleRightHand(out var pinchStrength, out var pinchPos, out _))
                return;

            m_LastRightHandPosition = pinchPos;
            m_HasRightHandPosition = true;

            // Force open-hand after calibration before any 4D / follow.
            if (m_RequireRightHandRelease)
            {
                if (pinchStrength < m_ReleaseStrengthMax)
                {
                    m_RequireRightHandRelease = false;
                    Debug.Log("[MagicMR] Right hand released — 4D / Follow unlocked.", this);
                }

                return;
            }

            if (m_RealityEditor != null && m_RealityEditor.IsDeconstructed)
                return;

            // High-threshold tight pinch near pin → FollowingHand.
            if (pinchStrength >= m_FollowPinchStrengthMin &&
                m_Anchor != null &&
                m_Anchor.IsCalibrated)
            {
                var pin = m_Anchor.PinnedPosition;
                if (Vector3.Distance(pinchPos, pin) <= m_FollowAttachDistance)
                {
                    if (TryGetRightPalm(out var palmPos, out var palmRot))
                        m_Anchor.BeginFollowHand(palmPos, palmRot);
                    else
                        m_Anchor.BeginFollowHand(pinchPos, m_Anchor.PinnedRotation);

                    EnterFollowingHand();
                }
            }
#endif
        }

        void UpdateFollowingHand()
        {
#if XR_HANDS_1_1_OR_NEWER
            if (!TrySampleRightHand(out var pinchStrength, out var pinchPos, out _))
            {
                // Brief tracking loss: keep following last pose; release only on open.
                return;
            }

            m_LastRightHandPosition = pinchPos;
            m_HasRightHandPosition = true;

            if (pinchStrength >= m_FollowPinchStrengthMin * 0.85f)
            {
                if (TryGetRightPalm(out var palmPos, out var palmRot))
                    m_Anchor?.UpdateFollowHand(palmPos, palmRot);
                else
                    m_Anchor?.UpdateFollowHand(pinchPos, m_Anchor != null ? m_Anchor.PinnedRotation : Quaternion.identity);
                return;
            }

            // Released → re-pin at current pose.
            m_Anchor?.EndFollowHand("released");
            EnterPinnedIdle("follow_released");
#endif
        }

        void PollLeftHandReset()
        {
#if XR_HANDS_1_1_OR_NEWER
            if (!TrySampleLeftResetPose(out var pinching, out var palmFacingHmd))
            {
                m_LeftResetAccumulated = 0f;
                return;
            }

            if (pinching && palmFacingHmd)
            {
                m_LeftResetAccumulated += Time.unscaledDeltaTime;
                if (m_LeftResetAccumulated >= m_LeftResetHoldSeconds)
                {
                    m_LeftResetAccumulated = 0f;
                    PerformFullReset("left_palm_pinch");
                }
            }
            else
            {
                m_LeftResetAccumulated = 0f;
            }
#endif
        }

#if XR_HANDS_1_1_OR_NEWER
        bool TrySampleRightHand(out float pinchStrength, out Vector3 pinchPos, out bool jointsOk)
        {
            pinchStrength = 0f;
            pinchPos = default;
            jointsOk = false;

            if (m_HandSubsystem == null)
                return false;

            var hand = m_HandSubsystem.rightHand;
            if (!hand.isTracked)
                return false;

            if (!hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb) ||
                !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index))
                return false;

            jointsOk = true;
            var dist = Vector3.Distance(thumb.position, index.position);
            pinchStrength = DistanceToPinchStrength(dist);
            pinchPos = (thumb.position + index.position) * 0.5f;
            return true;
        }

        bool TryGetRightPalm(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (m_HandSubsystem == null)
                return false;

            var hand = m_HandSubsystem.rightHand;
            if (!hand.isTracked || !hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palm))
                return false;

            position = palm.position;
            rotation = palm.rotation;
            return true;
        }

        bool TrySampleLeftResetPose(out bool pinching, out bool palmFacingHmd)
        {
            pinching = false;
            palmFacingHmd = false;

            if (m_HandSubsystem == null)
                return false;

            var hand = m_HandSubsystem.leftHand;
            if (!hand.isTracked)
                return false;

            if (!hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb) ||
                !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index) ||
                !hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palm))
                return false;

            var dist = Vector3.Distance(thumb.position, index.position);
            var strength = DistanceToPinchStrength(dist);
            pinching = strength >= StudySpec.CalibrationPinchStrengthMin;

            var cam = Camera.main;
            if (cam == null)
                return pinching;

            // Palm.up toward HMD forward ≈ "watch" / glance-at-wrist pose.
            palmFacingHmd = Vector3.Dot(palm.up, cam.transform.forward) > m_LeftPalmFacingDot;
            return true;
        }

        float DistanceToPinchStrength(float tipDistance)
        {
            // 1 = closed, 0 = open.
            return 1f - Mathf.InverseLerp(m_PinchClosedDistance, m_PinchOpenDistance, tipDistance);
        }
#endif

        bool IsNearWireframe(Vector3 pinchPos)
        {
            Vector3 target;
            if (m_WireframeAnchor != null)
                target = m_WireframeAnchor.position;
            else if (m_Ritual != null && m_Ritual.TryGetWireframePosition(out var ghostPos))
                target = ghostPos;
            else if (m_HasDeskPreset)
                target = m_DeskPresetPosition;
            else if (m_RealityEditor != null)
                target = m_RealityEditor.transform.position;
            else
                return true;

            return Vector3.Distance(pinchPos, target) <= m_CalibrationProximityMeters;
        }

        void CaptureDeskPresetFromScene()
        {
            var lighter = GameObject.Find("Lighter");
            if (lighter == null)
                return;

            m_DeskPresetPosition = lighter.transform.position;
            m_DeskPresetRotation = lighter.transform.rotation;
            m_HasDeskPreset = true;
            m_Ritual?.SetWireframeWorldPose(m_DeskPresetPosition, m_DeskPresetRotation);
        }

        void ResolveReferences()
        {
            if (m_Anchor == null)
                m_Anchor = FindFirstObjectByType<LighterAnchorManager>();
            if (m_RealityEditor == null)
            {
                var lighter = GameObject.Find("Lighter");
                if (lighter != null)
                    m_RealityEditor = lighter.GetComponent<RealityEditor>();
            }

            if (m_Ritual == null)
                m_Ritual = FindFirstObjectByType<CalibrationRitual>();
            if (m_PinchDetector == null)
            {
                var root = GameObject.Find("GestureDetectors");
                if (root != null)
                    m_PinchDetector = root.GetComponent<PinchGestureDetector>();
            }
        }

        void EnsureHandSubsystem()
        {
#if XR_HANDS_1_1_OR_NEWER
            if (m_HandSubsystem != null)
                return;

            SubsystemManager.GetSubsystems(s_Subsystems);
            if (s_Subsystems.Count == 0)
                return;

            m_HandSubsystem = s_Subsystems[0];
            if (!m_HandSubsystem.running)
                m_HandSubsystem.Start();
#endif
        }
    }
}
