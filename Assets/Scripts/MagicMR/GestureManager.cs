using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace MagicMR
{
    /// <summary>
    /// Central gesture dispatch, experiment condition gating, and hand sampling for logging.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class GestureManager : MonoBehaviour
    {
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
        RealityEditor m_RealityEditor;

        float m_LastGestureTime = -999f;
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
            ResolveReferences();
        }

        void OnEnable()
        {
            BindDetectors();
#if XR_HANDS_1_1_OR_NEWER
            SubsystemManager.GetSubsystems(s_HandSubsystems);
            if (s_HandSubsystems.Count > 0)
                m_HandSubsystem = s_HandSubsystems[0];
#endif
        }

        void Start()
        {
            if (m_AutoStartSession && DataLogger.Instance != null)
                DataLogger.Instance.StartSession(m_SubjectId, m_Condition, m_TrialId);
        }

        void OnDisable()
        {
            UnbindDetectors();
        }

        void OnDestroy()
        {
            if (m_AutoStartSession && DataLogger.Instance != null && DataLogger.Instance.SessionActive)
                DataLogger.Instance.EndSession();
        }

        void Update()
        {
            SampleHandForLogging();
        }

        void ResolveReferences()
        {
            if (m_PinchDetector == null || m_CircleDetector == null || m_SwipeDetector == null || m_SnapDetector == null)
            {
                var root = GameObject.Find("GestureDetectors");
                if (root != null)
                {
                    m_PinchDetector ??= root.GetComponent<PinchGestureDetector>();
                    m_CircleDetector ??= root.GetComponent<CircleGestureDetector>();
                    m_SwipeDetector ??= root.GetComponent<SwipeGestureDetector>();
                    m_SnapDetector ??= root.GetComponent<SnapGestureDetector>();
                }
            }

            if (m_RealityEditor == null)
            {
                var lighter = GameObject.Find("Lighter");
                if (lighter != null)
                    m_RealityEditor = lighter.GetComponent<RealityEditor>();
            }
        }

        void BindDetectors()
        {
            if (m_PinchDetector != null)
            {
                m_PinchDetector.DetectedEvent.RemoveAllListeners();
                m_PinchDetector.DetectedEvent.AddListener(() => HandleGesture(EditDimension.Appearance, "pinch"));
            }

            if (m_CircleDetector != null)
            {
                m_CircleDetector.DetectedEvent.RemoveAllListeners();
                m_CircleDetector.DetectedEvent.AddListener(() => HandleGesture(EditDimension.Agency, "circle"));
            }

            if (m_SwipeDetector != null)
            {
                m_SwipeDetector.DetectedEvent.RemoveAllListeners();
                m_SwipeDetector.DetectedEvent.AddListener(() => HandleGesture(EditDimension.Rule, "swipe"));
            }

            if (m_SnapDetector != null)
            {
                m_SnapDetector.DetectedEvent.RemoveAllListeners();
                m_SnapDetector.DetectedEvent.AddListener(() => HandleGesture(EditDimension.Deconstruction, "snap"));
            }
        }

        void UnbindDetectors()
        {
            m_PinchDetector?.DetectedEvent.RemoveAllListeners();
            m_CircleDetector?.DetectedEvent.RemoveAllListeners();
            m_SwipeDetector?.DetectedEvent.RemoveAllListeners();
            m_SnapDetector?.DetectedEvent.RemoveAllListeners();
        }

        void HandleGesture(EditDimension dimension, string gestureName)
        {
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
                Debug.LogWarning("[GestureManager] RealityEditor not found.", this);
                return;
            }

            m_LastGestureTime = Time.time;
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

        public void BeginTrial(int trialId)
        {
            m_TrialId = trialId;
            DataLogger.Instance?.SetTrial(trialId);
            m_RealityEditor?.ResetTarget();
        }
    }
}
