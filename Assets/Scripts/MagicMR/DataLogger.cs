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
            "timestamp,subject_id,condition,condition_id,trial_id,event_type,dimension,hand_distance,object_velocity,state_duration,hand_x,hand_y,hand_z,notes";

        const string TrajectoryHeader =
            "timestamp,subject_id,condition,condition_id,trial_id,hand_x,hand_y,hand_z,target_x,target_y,target_z,distance";

        [SerializeField]
        string m_FilePrefix = "study";

        [SerializeField]
        bool m_LogHandTrajectory = StudySpec.LogHandTrajectory;

        [SerializeField]
        float m_TrajectorySampleInterval = StudySpec.TrajectorySampleInterval;

        string m_SubjectId = "S00";
        string m_Condition = "All";
        int m_ConditionId = 4;
        int m_TrialId;
        string m_EventFilePath;
        string m_TrajectoryFilePath;
        float m_NextTrajectorySampleTime;
        bool m_SessionActive;

        public string EventFilePath => m_EventFilePath;
        public bool SessionActive => m_SessionActive;
        public string Condition => m_Condition;
        public int ConditionId => m_ConditionId;
        public string SubjectId => m_SubjectId;
        public int TrialId => m_TrialId;
        public string TrajectoryFilePath => m_TrajectoryFilePath;

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
            {
                EndSession();
                Instance = null;
            }
        }
        void OnApplicationQuit() => EndSession();

        public void ConfigureLogging(bool logHandTrajectory, float trajectorySampleInterval)
        {
            m_LogHandTrajectory = logHandTrajectory;
            m_TrajectorySampleInterval = float.IsFinite(trajectorySampleInterval) ? Mathf.Clamp(trajectorySampleInterval,.05f,10f) : .1f;
        }

        public void StartSession(string subjectId, string condition, int trialId = 1)
        {
            // The persistent logger owns a session across content/scene changes.
            // A joining controller must not overwrite its identity or files.
            if(m_SessionActive)return;
            m_SubjectId = string.IsNullOrWhiteSpace(subjectId) ? "S00" : subjectId.Trim();
            m_Condition = string.IsNullOrWhiteSpace(condition) ? "All" : condition.Trim();
            m_ConditionId = InferConditionId(m_Condition);
            m_TrialId = Mathf.Max(1, trialId);

            var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture)+"_"+Guid.NewGuid().ToString("N");
            var folder = Path.Combine(Application.persistentDataPath, "StudyLogs");
            Directory.CreateDirectory(folder);

            var stem=SafeFilePart(m_FilePrefix)+"_"+SafeFilePart(m_SubjectId)+"_"+stamp;
            m_EventFilePath = Path.Combine(folder, stem+"_events.csv");
            m_TrajectoryFilePath = Path.Combine(folder, stem+"_trajectory.csv");

            WriteNewHeader(m_EventFilePath,EventHeader);
            if (m_LogHandTrajectory)
                WriteNewHeader(m_TrajectoryFilePath,TrajectoryHeader);

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

        public void SetCondition(string condition, int conditionId)
        {
            m_Condition = string.IsNullOrWhiteSpace(condition) ? "All" : condition.Trim();
            m_ConditionId = Mathf.Clamp(conditionId, 1, 4);
            if (m_SessionActive)
                LogEvent("condition_change", EditDimension.None, -1f, 0f, 0f, notes: $"id={m_ConditionId}");
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
                m_ConditionId.ToString(CultureInfo.InvariantCulture),
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
            if(!TabletopHandSample.Finite(handPosition)||!TabletopHandSample.Finite(targetPosition))return;

            if (Time.time < m_NextTrajectorySampleTime)
                return;

            float interval=float.IsFinite(m_TrajectorySampleInterval)?Mathf.Clamp(m_TrajectorySampleInterval,.05f,10f):.1f;
            m_NextTrajectorySampleTime = Time.time + interval;
            var distance = Vector3.Distance(handPosition, targetPosition);
            var line = string.Join(",",
                Format(Time.time),
                Escape(m_SubjectId),
                Escape(m_Condition),
                m_ConditionId.ToString(CultureInfo.InvariantCulture),
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

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\r") || value.Contains("\n"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";

            return value;
        }
        static string SafeFilePart(string value) {
            if(string.IsNullOrWhiteSpace(value))return "study";
            var invalid=Path.GetInvalidFileNameChars();var result=new StringBuilder();
            foreach(char c in value) {
                if(result.Length>=64)break;
                result.Append(char.IsControl(c)||c=='/'||c=='\\'||Array.IndexOf(invalid,c)>=0?'_':c);
            }
            return result.ToString().TrimEnd('.',' ');
        }
        static void WriteNewHeader(string path,string header) {
            using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read))
            using(var writer=new StreamWriter(stream,Encoding.UTF8))writer.WriteLine(header);
        }

        static int InferConditionId(string condition)
        {
            if (string.IsNullOrEmpty(condition))
                return 4;
            if (condition.StartsWith("C1") || condition.IndexOf("Baseline", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return 1;
            if (condition.StartsWith("C2") || condition.Equals("Appearance", System.StringComparison.OrdinalIgnoreCase))
                return 2;
            if (condition.StartsWith("C3") || condition.IndexOf("Agency", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return 3;
            return 4;
        }
    }
}
