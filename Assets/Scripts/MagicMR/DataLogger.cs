using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace MagicMR
{
    /// <summary>
    /// Singleton CSV logger for HCI study sessions. Files are written under Application.persistentDataPath.
    /// </summary>
    public class DataLogger : MonoBehaviour
    {
        public static DataLogger Instance { get; private set; }

        const string EventHeader =
            "timestamp,subject_id,condition,trial_id,event_type,dimension,hand_distance,object_velocity,state_duration,hand_x,hand_y,hand_z,notes";

        const string TrajectoryHeader =
            "timestamp,subject_id,condition,trial_id,hand_x,hand_y,hand_z,target_x,target_y,target_z,distance";

        [SerializeField]
        string m_FilePrefix = "study";

        [SerializeField]
        bool m_LogHandTrajectory = StudySpec.LogHandTrajectory;

        [SerializeField]
        float m_TrajectorySampleInterval = StudySpec.TrajectorySampleInterval;

        string m_SubjectId = "S00";
        string m_Condition = "All";
        int m_TrialId;
        string m_EventFilePath;
        string m_TrajectoryFilePath;
        float m_NextTrajectorySampleTime;
        bool m_SessionActive;

        public string EventFilePath => m_EventFilePath;
        public bool SessionActive => m_SessionActive;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void ConfigureLogging(bool logHandTrajectory, float trajectorySampleInterval)
        {
            m_LogHandTrajectory = logHandTrajectory;
            m_TrajectorySampleInterval = trajectorySampleInterval;
        }

        public void StartSession(string subjectId, string condition, int trialId = 1)
        {
            m_SubjectId = string.IsNullOrWhiteSpace(subjectId) ? "S00" : subjectId.Trim();
            m_Condition = string.IsNullOrWhiteSpace(condition) ? "All" : condition.Trim();
            m_TrialId = Mathf.Max(1, trialId);

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var folder = Path.Combine(Application.persistentDataPath, "StudyLogs");
            Directory.CreateDirectory(folder);

            m_EventFilePath = Path.Combine(folder, $"{m_FilePrefix}_{m_SubjectId}_{stamp}_events.csv");
            m_TrajectoryFilePath = Path.Combine(folder, $"{m_FilePrefix}_{m_SubjectId}_{stamp}_trajectory.csv");

            File.WriteAllText(m_EventFilePath, EventHeader + Environment.NewLine, Encoding.UTF8);
            if (m_LogHandTrajectory)
                File.WriteAllText(m_TrajectoryFilePath, TrajectoryHeader + Environment.NewLine, Encoding.UTF8);

            m_SessionActive = true;
            m_NextTrajectorySampleTime = Time.time;

            LogEvent("session_start", EditDimension.None, -1f, 0f, 0f, notes: m_EventFilePath);
            Debug.Log($"[DataLogger] Session started. Events: {m_EventFilePath}");
        }

        public void EndSession()
        {
            if (!m_SessionActive)
                return;

            LogEvent("session_end", EditDimension.None, -1f, 0f, 0f);
            m_SessionActive = false;
            Debug.Log($"[DataLogger] Session ended. File: {m_EventFilePath}");
        }

        public void SetTrial(int trialId)
        {
            m_TrialId = Mathf.Max(1, trialId);
            LogEvent("trial_start", EditDimension.None, -1f, 0f, 0f, notes: $"trial={m_TrialId}");
        }

        public void LogEvent(
            string eventType,
            EditDimension dimension,
            float handDistance,
            float objectVelocity,
            float stateDuration = 0f,
            Vector3? handPosition = null,
            string notes = "")
        {
            if (!m_SessionActive || string.IsNullOrEmpty(m_EventFilePath))
                return;

            var hand = handPosition ?? Vector3.zero;
            var hasHand = handPosition.HasValue;
            var line = string.Join(",",
                Format(Time.time),
                Escape(m_SubjectId),
                Escape(m_Condition),
                m_TrialId.ToString(CultureInfo.InvariantCulture),
                Escape(eventType),
                dimension.ToString(),
                Format(handDistance),
                Format(objectVelocity),
                Format(stateDuration),
                hasHand ? Format(hand.x) : "",
                hasHand ? Format(hand.y) : "",
                hasHand ? Format(hand.z) : "",
                Escape(notes));

            File.AppendAllText(m_EventFilePath, line + Environment.NewLine, Encoding.UTF8);
        }

        public void LogHandTrajectory(Vector3 handPosition, Vector3 targetPosition)
        {
            if (!m_SessionActive || !m_LogHandTrajectory || string.IsNullOrEmpty(m_TrajectoryFilePath))
                return;

            if (Time.time < m_NextTrajectorySampleTime)
                return;

            m_NextTrajectorySampleTime = Time.time + m_TrajectorySampleInterval;
            var distance = Vector3.Distance(handPosition, targetPosition);
            var line = string.Join(",",
                Format(Time.time),
                Escape(m_SubjectId),
                Escape(m_Condition),
                m_TrialId.ToString(CultureInfo.InvariantCulture),
                Format(handPosition.x),
                Format(handPosition.y),
                Format(handPosition.z),
                Format(targetPosition.x),
                Format(targetPosition.y),
                Format(targetPosition.z),
                Format(distance));

            File.AppendAllText(m_TrajectoryFilePath, line + Environment.NewLine, Encoding.UTF8);
        }

        static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            if (value.Contains(",") || value.Contains("\""))
                return "\"" + value.Replace("\"", "\"\"") + "\"";

            return value;
        }
    }
}
