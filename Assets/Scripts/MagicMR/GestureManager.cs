using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace MagicMR
{
    /// <summary>
    /// Central gesture dispatch. Detectors call <see cref="Notify"/> directly
    /// (UnityEvent wiring was silently dropping events on device).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class GestureManager : MonoBehaviour
    {
        public static GestureManager Instance { get; private set; }

        [Header("Study Session")]
        [SerializeField]
        string m_SubjectId = StudySpec.DefaultSubjectId;

        [SerializeField]
        string m_Condition = StudySpec.DefaultCondition;

        [SerializeField]
        int m_TrialId = StudySpec.DefaultTrialId;

        [SerializeField]
        bool m_AutoStartSession = true;

        [Header("Gesture Gating")]
        [SerializeField]
        EnabledDimensions m_EnabledDimensions = EnabledDimensions.All;

        [SerializeField]
        float m_GlobalCooldownSeconds = StudySpec.GlobalCooldownSeconds;

        [SerializeField]
        Handedness m_TrackedHand = Handedness.Right;

        [Header("References")]
        [SerializeField]
        PinchGestureDetector m_PinchDetector;

        [SerializeField]
        CircleGestureDetector m_CircleDetector;

        [SerializeField]
        SwipeGestureDetector m_SwipeDetector;

        [SerializeField]
        SnapGestureDetector m_SnapDetector;

        [SerializeField]
        FistBurstGestureDetector m_FistBurstDetector;

        [SerializeField]
        RealityEditor m_RealityEditor;

        float m_LastGestureTime = -999f;
        float m_BlockGesturesUntil;
        Vector3 m_LastHandPosition;
        bool m_HasHandPosition;

#if XR_HANDS_1_1_OR_NEWER
        XRHandSubsystem m_HandSubsystem;
        static readonly List<XRHandSubsystem> s_HandSubsystems = new List<XRHandSubsystem>();
#endif

        public string SubjectId => m_SubjectId;
        public string Condition => m_Condition;
        public int TrialId => m_TrialId;

        void Awake()
        {
            Instance = this;
            ResolveReferences();
            Debug.Log("[MagicMR] GestureManager awake (direct Notify dispatch).", this);
        }

        void OnEnable()
        {
            Instance = this;
            BindDetectors();
#if XR_HANDS_1_1_OR_NEWER
            SubsystemManager.GetSubsystems(s_HandSubsystems);
            if (s_HandSubsystems.Count > 0)
                m_HandSubsystem = s_HandSubsystems[0];
#endif
        }

        void Start()
        {
            ResolveReferences();
            BindDetectors();
            FindFirstObjectByType<LighterAnchorManager>()?.BindPinchHoldListener();

            if (m_AutoStartSession && DataLogger.Instance != null)
                DataLogger.Instance.StartSession(m_SubjectId, m_Condition, m_TrialId);

            Debug.Log(
                $"[MagicMR] GestureManager ready editor={(m_RealityEditor != null)} " +
                $"pinch={(m_PinchDetector != null)} circle={(m_CircleDetector != null)} " +
                $"swipe={(m_SwipeDetector != null)} fist={(m_FistBurstDetector != null)} dims={m_EnabledDimensions}",
                this);
        }

        void OnDisable()
        {
            UnbindDetectors();
            if (Instance == this)
                Instance = null;
        }

        void OnDestroy()
        {
            if (m_AutoStartSession && DataLogger.Instance != null && DataLogger.Instance.SessionActive)
                DataLogger.Instance.EndSession();
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
#if XR_HANDS_1_1_OR_NEWER
            if (m_HandSubsystem == null)
            {
                SubsystemManager.GetSubsystems(s_HandSubsystems);
                if (s_HandSubsystems.Count > 0)
                {
                    m_HandSubsystem = s_HandSubsystems[0];
                    if (!m_HandSubsystem.running)
                        m_HandSubsystem.Start();
                }
            }
#endif
            SampleHandForLogging();
        }

        /// <summary>Called directly by gesture detectors (preferred over UnityEvent).</summary>
        public static void Notify(EditDimension dimension, string gestureName)
        {
            if (Instance == null)
                Instance = FindFirstObjectByType<GestureManager>();
            if (Instance == null)
            {
                Debug.LogWarning($"[MagicMR] GestureManager missing; dropped {gestureName}.");
                // Last-resort: apply on RealityEditor directly.
                var lighter = GameObject.Find("Lighter");
                var editor = lighter != null ? lighter.GetComponent<RealityEditor>() : null;
                editor?.ApplyDimension(dimension, Vector3.zero, false);
                return;
            }

            Instance.HandleGesture(dimension, gestureName);
        }

        void ResolveReferences()
        {
            var root = GameObject.Find("GestureDetectors");
            if (root != null)
            {
                m_PinchDetector ??= root.GetComponent<PinchGestureDetector>();
                m_CircleDetector ??= root.GetComponent<CircleGestureDetector>();
                m_SwipeDetector ??= root.GetComponent<SwipeGestureDetector>();
                m_SnapDetector ??= root.GetComponent<SnapGestureDetector>();
                m_FistBurstDetector ??= root.GetComponent<FistBurstGestureDetector>();
                if (m_FistBurstDetector == null)
                    m_FistBurstDetector = root.AddComponent<FistBurstGestureDetector>();
            }

            if (m_RealityEditor == null)
            {
                var lighter = GameObject.Find("Lighter");
                if (lighter != null)
                    m_RealityEditor = lighter.GetComponent<RealityEditor>();
            }
        }

        public void RebindDetectors()
        {
            UnbindDetectors();
            ResolveReferences();
            BindDetectors();
            FindFirstObjectByType<LighterAnchorManager>()?.BindPinchHoldListener();
            Debug.Log("[MagicMR] GestureManager rebound.", this);
        }

        void BindDetectors()
        {
            // Detectors call Notify() directly. Clear stale UnityEvent wiring so
            // scene leftovers cannot double-fire. Calibration is FSM-owned.
            if (m_PinchDetector != null)
            {
                SafeClear(m_PinchDetector.DetectedEvent);
                SafeClear(m_PinchDetector.HeldEvent);
            }

            if (m_CircleDetector != null)
                SafeClear(m_CircleDetector.DetectedEvent);

            if (m_SwipeDetector != null)
                SafeClear(m_SwipeDetector.DetectedEvent);

            if (m_SnapDetector != null)
            {
                SafeClear(m_SnapDetector.DetectedEvent);
                m_SnapDetector.enabled = false;
            }

            if (m_FistBurstDetector != null)
            {
                m_FistBurstDetector.enabled = true;
                SafeClear(m_FistBurstDetector.DetectedEvent);
            }
        }

        void UnbindDetectors()
        {
            SafeClear(m_PinchDetector != null ? m_PinchDetector.DetectedEvent : null);
            SafeClear(m_PinchDetector != null ? m_PinchDetector.HeldEvent : null);
            SafeClear(m_CircleDetector != null ? m_CircleDetector.DetectedEvent : null);
            SafeClear(m_SwipeDetector != null ? m_SwipeDetector.DetectedEvent : null);
            SafeClear(m_SnapDetector != null ? m_SnapDetector.DetectedEvent : null);
            SafeClear(m_FistBurstDetector != null ? m_FistBurstDetector.DetectedEvent : null);
        }

        static void SafeClear(UnityEngine.Events.UnityEvent evt)
        {
            evt?.RemoveAllListeners();
        }

        void HandleGesture(EditDimension dimension, string gestureName)
        {
            if (Time.time < m_BlockGesturesUntil)
            {
                Debug.Log($"[MagicMR] Gesture {gestureName} blocked (guard).");
                return;
            }

            // Strict FSM gating — Cooldown / CalibrationReady / FollowingHand /
            // requireRightHandRelease all drop 4D edits here.
            var fsm = MRGestureController.Instance;
            if (fsm != null && !fsm.CanAcceptEditGesture(dimension))
            {
                Debug.Log(
                    $"[MagicMR] Gesture {gestureName} blocked (FSM state={fsm.State}, " +
                    $"requireRelease={fsm.RequireRightHandRelease}).");
                return;
            }

            if (fsm == null)
            {
                // Legacy fallback when FSM is missing from the scene.
                if (dimension == EditDimension.Appearance)
                {
                    var anchor = m_RealityEditor != null
                        ? m_RealityEditor.GetComponent<LighterAnchorManager>()
                        : FindFirstObjectByType<LighterAnchorManager>();
                    if (anchor != null && !anchor.IsCalibrated)
                    {
                        Debug.Log("[MagicMR] Appearance blocked (not calibrated yet).");
                        return;
                    }
                }

                if (dimension == EditDimension.Rule)
                {
                    var anchor = m_RealityEditor != null
                        ? m_RealityEditor.GetComponent<LighterAnchorManager>()
                        : null;
                    if (anchor != null && anchor.IsFollowingHand)
                    {
                        Debug.Log($"[MagicMR] Gesture {gestureName} blocked (following hand).");
                        return;
                    }
                }
            }

            Debug.Log($"[MagicMR] Gesture detected: {gestureName} -> {dimension}");

            if (!IsDimensionEnabled(dimension))
            {
                LogGesture($"{gestureName}_blocked", dimension, "dimension_disabled");
                return;
            }

            if (Time.time - m_LastGestureTime < m_GlobalCooldownSeconds)
            {
                LogGesture($"{gestureName}_blocked", dimension, "cooldown");
                return;
            }

            if (m_RealityEditor == null)
            {
                ResolveReferences();
                if (m_RealityEditor == null)
                {
                    Debug.LogWarning("[GestureManager] RealityEditor not found.", this);
                    return;
                }
            }

            m_LastGestureTime = Time.time;

            if (dimension == EditDimension.Deconstruction)
            {
                m_PinchDetector?.SuppressTapFor(1.0f);
                m_BlockGesturesUntil = Time.time + 0.5f;
            }

            fsm?.NotifyEditAccepted(dimension);

            var distance = m_RealityEditor.GetHandDistance(m_LastHandPosition, m_HasHandPosition);
            m_RealityEditor.ApplyDimension(dimension, m_LastHandPosition, m_HasHandPosition);
            LogGesture($"{gestureName}_triggered", dimension, distance: distance);
        }

        void LogGesture(string eventType, EditDimension dimension, string notes = "", float distance = -1f)
        {
            if (DataLogger.Instance == null)
                return;

            var velocity = m_RealityEditor != null ? m_RealityEditor.CurrentVelocity : 0f;
            var stateDuration = m_RealityEditor != null ? m_RealityEditor.CurrentStateDuration : 0f;
            DataLogger.Instance.LogEvent(
                eventType,
                dimension,
                distance,
                velocity,
                stateDuration,
                m_HasHandPosition ? m_LastHandPosition : (Vector3?)null,
                notes);
        }

        bool IsDimensionEnabled(EditDimension dimension)
        {
            var flag = (EnabledDimensions)(int)dimension;
            return (m_EnabledDimensions & flag) == flag;
        }

        void SampleHandForLogging()
        {
#if XR_HANDS_1_1_OR_NEWER
            if (m_HandSubsystem == null || m_RealityEditor == null || DataLogger.Instance == null)
                return;

            var hand = m_TrackedHand == Handedness.Left ? m_HandSubsystem.leftHand : m_HandSubsystem.rightHand;
            if (!hand.isTracked)
            {
                m_HasHandPosition = false;
                return;
            }

            if (!hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palmPose))
                return;

            m_LastHandPosition = palmPose.position;
            m_HasHandPosition = true;
            DataLogger.Instance.LogHandTrajectory(m_LastHandPosition, m_RealityEditor.transform.position);
#endif
        }

        public void ConfigureSession(string subjectId, string condition, int trialId, EnabledDimensions enabledDimensions)
        {
            m_SubjectId = subjectId;
            m_Condition = condition;
            m_TrialId = trialId;
            m_EnabledDimensions = enabledDimensions;
        }

        public void SetEnabledDimensions(EnabledDimensions enabledDimensions)
        {
            m_EnabledDimensions = enabledDimensions;
        }

        public void SetConditionLabel(string condition)
        {
            m_Condition = condition;
        }

        public void NotifyStudyReset()
        {
            m_BlockGesturesUntil = Time.time + 0.75f;
            m_LastGestureTime = Time.time;
        }

        public void BeginTrial(int trialId)
        {
            m_TrialId = trialId;
            DataLogger.Instance?.SetTrial(trialId);
            m_RealityEditor?.ResetTarget();
        }
    }
}
