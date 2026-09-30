using System.Globalization;
using UnityEngine;

namespace MagicMR
{
    /// <summary>
    /// Records affective-narrative activations and self-reported emotion for an
    /// MR study without owning any gesture or visual implementation.
    /// </summary>
    [AddComponentMenu("MagicMR/Study/Affective Experience Logger")]
    [DisallowMultipleComponent]
    public sealed class AffectiveExperienceLogger : MonoBehaviour
    {
        [Header("Narrative")]
        [Tooltip("Stable identifier used to group responses, for example lighter_aversive.")]
        [SerializeField] string m_ConfiguredNarrativeId = "unassigned";

        [Tooltip("Human-readable description stored with the activation event.")]
        [TextArea]
        [SerializeField] string m_ConfiguredNarrativeLabel = "Unassigned narrative";

        [Tooltip("If enabled, records the configured narrative when this object becomes active.")]
        [SerializeField] bool m_RecordActivationOnEnable;

        [Header("Self-report scale")]
        [Tooltip("Minimum accepted value for valence, arousal, and perceived agency.")]
        [SerializeField] float m_MinRating = 1f;

        [Tooltip("Maximum accepted value for valence, arousal, and perceived agency.")]
        [SerializeField] float m_MaxRating = 9f;

        [Header("Pending UI values")]
        [SerializeField] float m_Valence = 5f;
        [SerializeField] float m_Arousal = 5f;
        [SerializeField] float m_Agency = 5f;
        [TextArea]
        [SerializeField] string m_ParticipantNote;

        string m_CurrentNarrativeId = "unassigned";
        string m_CurrentNarrativeLabel = "Unassigned narrative";
        int m_ActivationIndex;
        float m_ActivationTime = -1f;

        /// <summary>Gets the stable identifier for the most recently activated narrative.</summary>
        public string CurrentNarrativeId => m_CurrentNarrativeId;

        /// <summary>Gets the display label for the most recently activated narrative.</summary>
        public string CurrentNarrativeLabel => m_CurrentNarrativeLabel;

        /// <summary>Gets the number of narrative activations recorded by this component.</summary>
        public int ActivationIndex => m_ActivationIndex;

        void Awake()
        {
            NormalizeRatingRange();
            m_CurrentNarrativeId = NormalizedId(m_ConfiguredNarrativeId);
            m_CurrentNarrativeLabel = NormalizedLabel(m_ConfiguredNarrativeLabel);
            ClampPendingRatings();
        }

        void OnEnable()
        {
            if (m_RecordActivationOnEnable)
                ActivateConfiguredNarrative();
        }

        void OnValidate()
        {
            NormalizeRatingRange();
            ClampPendingRatings();
        }

        /// <summary>Activates and logs the narrative configured in this component's Inspector.</summary>
        public void ActivateConfiguredNarrative()
        {
            ActivateNarrative(m_ConfiguredNarrativeId, m_ConfiguredNarrativeLabel);
        }

        /// <summary>Activates a narrative using its identifier and the configured display label.</summary>
        public void ActivateNarrative(string narrativeId)
        {
            ActivateNarrative(narrativeId, m_ConfiguredNarrativeLabel);
        }

        /// <summary>Activates a narrative and records its start in the shared study event log.</summary>
        public void ActivateNarrative(string narrativeId, string narrativeLabel)
        {
            m_CurrentNarrativeId = NormalizedId(narrativeId);
            m_CurrentNarrativeLabel = NormalizedLabel(narrativeLabel);
            m_ActivationIndex++;
            m_ActivationTime = Time.realtimeSinceStartup;
            m_ParticipantNote = string.Empty;
            ClampPendingRatings();

            Log("affect_narrative_activated", 0f,
                "narrative_id=" + NoteValue(m_CurrentNarrativeId) +
                ";narrative_label=" + NoteValue(m_CurrentNarrativeLabel) +
                ";activation_index=" + m_ActivationIndex.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Alias for code that describes the action as recording rather than activating.</summary>
        public void RecordNarrativeActivation(string narrativeId, string narrativeLabel)
        {
            ActivateNarrative(narrativeId, narrativeLabel);
        }

        /// <summary>Sets the pending valence rating, such as a value from a UI slider.</summary>
        public void SetValence(float value) => m_Valence = ClampRating(value);

        /// <summary>Sets the pending arousal rating, such as a value from a UI slider.</summary>
        public void SetArousal(float value) => m_Arousal = ClampRating(value);

        /// <summary>Sets the pending perceived-agency rating, such as a value from a UI slider.</summary>
        public void SetAgency(float value) => m_Agency = ClampRating(value);

        /// <summary>Sets an optional participant note that is included with the next submitted rating.</summary>
        public void SetParticipantNote(string value) => m_ParticipantNote = value ?? string.Empty;

        /// <summary>Submits the Inspector or UI-bound pending self-report values.</summary>
        public void SubmitPendingRatings()
        {
            SubmitRatings(m_Valence, m_Arousal, m_Agency, m_ParticipantNote);
        }

        /// <summary>Submits valence, arousal, and perceived agency for the current narrative.</summary>
        public void SubmitRatings(float valence, float arousal, float agency)
        {
            SubmitRatings(valence, arousal, agency, string.Empty);
        }

        /// <summary>
        /// Submits valence, arousal, perceived agency, and an optional participant note.
        /// Values are clamped to the configured study scale before logging.
        /// </summary>
        public void SubmitRatings(float valence, float arousal, float agency, string participantNote)
        {
            if (m_ActivationTime < 0f || !float.IsFinite(valence) || !float.IsFinite(arousal) || !float.IsFinite(agency))
                return;
            var logger = DataLogger.Instance;
            if (logger == null || !logger.SessionActive) return;
            m_Valence = ClampRating(valence);
            m_Arousal = ClampRating(arousal);
            m_Agency = ClampRating(agency);
            m_ParticipantNote = participantNote ?? string.Empty;

            var responseLatency = m_ActivationTime < 0f
                ? 0f
                : Mathf.Max(0f, Time.realtimeSinceStartup - m_ActivationTime);
            var notes = "narrative_id=" + NoteValue(m_CurrentNarrativeId) +
                        ";narrative_label=" + NoteValue(m_CurrentNarrativeLabel) +
                        ";activation_index=" + m_ActivationIndex.ToString(CultureInfo.InvariantCulture) +
                        ";valence=" + Format(m_Valence) +
                        ";arousal=" + Format(m_Arousal) +
                        ";agency=" + Format(m_Agency) +
                        ";scale_min=" + Format(m_MinRating) +
                        ";scale_max=" + Format(m_MaxRating) +
                        ";participant_note=" + NoteValue(m_ParticipantNote);

            // The legacy CSV schema has no dedicated affect columns. Preserve its
            // numeric meanings and encode named ratings in notes for unambiguous export.
            Log("affect_rating_submitted", responseLatency, notes);
        }

        /// <summary>Prevents a response from being assigned to a previous scene after reset.</summary>
        public void ClearNarrative()
        {
            m_ActivationTime = -1f;
            m_CurrentNarrativeId = "unassigned";
            m_ParticipantNote = string.Empty;
        }

        void Log(string eventType, float stateDuration, string notes)
        {
            // DataLogger also safely ignores events outside an active session.
            DataLogger.Instance?.LogEvent(eventType, EditDimension.None, -1f, 0f, stateDuration, notes: notes);
        }

        void NormalizeRatingRange()
        {
            if (m_MaxRating < m_MinRating)
            {
                var previousMin = m_MinRating;
                m_MinRating = m_MaxRating;
                m_MaxRating = previousMin;
            }
        }

        void ClampPendingRatings()
        {
            m_Valence = ClampRating(m_Valence);
            m_Arousal = ClampRating(m_Arousal);
            m_Agency = ClampRating(m_Agency);
        }

        float ClampRating(float value) => Mathf.Clamp(value, m_MinRating, m_MaxRating);

        static string NormalizedId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unassigned" : value.Trim();
        }

        static string NormalizedLabel(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Unassigned narrative" : value.Trim();
        }

        static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        static string NoteValue(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace(";", "/")
                .Replace("=", "-");
        }
    }
}
