using System;
using System.Collections.Generic;
using UnityEngine;

namespace Perception
{
    /// <summary>
    /// Keeps exactly one locked lighter target across frames.
    /// Consumes DetectionFrame; never switches target solely because another
    /// detection has higher confidence while locked.
    /// </summary>
    public class SingleTargetManager : MonoBehaviour
    {
        [SerializeField]
        DetectionManager m_DetectionManager;

        [Header("Class filter")]
        [SerializeField]
        string m_TargetClassName = "Lighter";

        [Header("Frame size (matching / ClosestToCenter)")]
        [SerializeField]
        float m_FrameWidth = 1280f;

        [SerializeField]
        float m_FrameHeight = 720f;

        [Header("State machine")]
        [SerializeField]
        int m_AcquireConfirmFrames = 2;

        [SerializeField]
        float m_LostTargetGracePeriod = 0.5f;

        [SerializeField]
        float m_MatchMinScore = 0.3f;

        [Header("Matching weights")]
        [SerializeField]
        float m_MatchAlphaIoU = 0.7f;

        [SerializeField]
        float m_MatchBetaCenter = 0.3f;

        [Header("Selection")]
        [SerializeField]
        TargetSelectionMode m_SelectionMode = TargetSelectionMode.HighestConfidence;

        [Header("Debug")]
        [SerializeField]
        bool m_DrawGizmos = true;

        ITargetSelectionStrategy m_Selection;
        ITargetMatchingStrategy m_Matching;

        TargetLockState m_State = TargetLockState.NO_TARGET;
        ObjectDetectionEvent m_LockedDetection;
        ObjectDetectionEvent m_AcquireCandidate;
        int m_AcquireStreak;
        float m_LostElapsed;
        float m_LastMatchScore;
        readonly List<ObjectDetectionEvent> m_LastFrameLighters = new List<ObjectDetectionEvent>(8);
        readonly List<ObjectDetectionEvent> m_Scratch = new List<ObjectDetectionEvent>(8);

        public TargetLockState State => m_State;
        public bool HasLockedTarget =>
            m_State == TargetLockState.TARGET_LOCKED
            || m_State == TargetLockState.TARGET_LOST
            || m_State == TargetLockState.TARGET_ACQUIRE;

        public ObjectDetectionEvent LockedDetection => m_LockedDetection;
        public float LastMatchScore => m_LastMatchScore;
        public float LostElapsed => m_LostElapsed;
        public IReadOnlyList<ObjectDetectionEvent> LastFrameLighters => m_LastFrameLighters;
        public float FrameWidth => m_FrameWidth;
        public float FrameHeight => m_FrameHeight;

        /// <summary>
        /// Sync matching / selection frame size to PICO Camera JPEG pixel space.
        /// </summary>
        public void SetFrameSize(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return;
            m_FrameWidth = width;
            m_FrameHeight = height;
        }

        /// <summary>
        /// Fired when a locked target pose should drive Anchor_Lighter.
        /// Not raised on NO_TARGET. During TARGET_LOST, not raised (anchor holds last pose).
        /// </summary>
        public event Action<LockedTargetEvent> LockedTargetUpdated;

        /// <summary>Fired on any state / debug field change.</summary>
        public event Action StateChanged;

        void Awake()
        {
            if (m_DetectionManager == null)
                m_DetectionManager = FindFirstObjectByType<DetectionManager>();
            RebuildStrategies();
        }

        void OnValidate() => RebuildStrategies();

        void OnEnable()
        {
            if (m_DetectionManager != null)
                m_DetectionManager.DetectionFrameReceived += OnDetectionFrame;
            else
                Debug.LogError("[SingleTarget] DetectionManager missing.");
        }

        void OnDisable()
        {
            if (m_DetectionManager != null)
                m_DetectionManager.DetectionFrameReceived -= OnDetectionFrame;
        }

        void RebuildStrategies()
        {
            m_Matching = new IoUCenterMatching();
            switch (m_SelectionMode)
            {
                case TargetSelectionMode.LargestArea:
                    m_Selection = new LargestAreaSelection();
                    break;
                case TargetSelectionMode.ClosestToCenter:
                    m_Selection = new ClosestToCenterSelection();
                    break;
                default:
                    m_Selection = new HighestConfidenceSelection();
                    break;
            }
        }

        void OnDetectionFrame(DetectionFrame frame)
        {
            FilterLighters(frame.detections, m_Scratch);
            m_LastFrameLighters.Clear();
            m_LastFrameLighters.AddRange(m_Scratch);

            switch (m_State)
            {
                case TargetLockState.NO_TARGET:
                    HandleNoTarget(frame.timestamp);
                    break;
                case TargetLockState.TARGET_ACQUIRE:
                    HandleAcquire(frame.timestamp);
                    break;
                case TargetLockState.TARGET_LOCKED:
                    HandleLocked(frame.timestamp);
                    break;
                case TargetLockState.TARGET_LOST:
                    HandleLost(frame.timestamp);
                    break;
            }

            StateChanged?.Invoke();
        }

        void HandleNoTarget(long timestamp)
        {
            m_LostElapsed = 0f;
            m_LastMatchScore = 0f;
            if (m_Scratch.Count == 0)
                return;

            if (!m_Selection.TrySelect(m_Scratch, m_FrameWidth, m_FrameHeight, out ObjectDetectionEvent pick))
                return;

            m_AcquireCandidate = pick;
            m_LockedDetection = pick;
            m_AcquireStreak = 1;
            SetState(TargetLockState.TARGET_ACQUIRE);
            Debug.Log(
                $"[SingleTarget] target candidate conf={pick.confidence:F2} " +
                $"center=({pick.centerX:F0},{pick.centerY:F0}) → ACQUIRE");
            if (m_AcquireStreak >= Mathf.Max(1, m_AcquireConfirmFrames))
                PromoteToLocked(timestamp, 1f);
            else
                EmitLockedUpdate(timestamp, 1f, 0f);
        }

        void HandleAcquire(long timestamp)
        {
            int idx = m_Matching.FindBestMatch(
                m_AcquireCandidate, m_Scratch, m_FrameWidth, m_FrameHeight,
                m_MatchAlphaIoU, m_MatchBetaCenter, m_MatchMinScore, out float score);
            m_LastMatchScore = score;

            if (idx < 0)
            {
                Debug.Log("[SingleTarget] ACQUIRE failed match → NO_TARGET");
                ClearToNoTarget();
                return;
            }

            m_AcquireCandidate = m_Scratch[idx];
            m_LockedDetection = m_AcquireCandidate;
            m_AcquireStreak++;
            EmitLockedUpdate(timestamp, score, 0f);

            if (m_AcquireStreak >= Mathf.Max(1, m_AcquireConfirmFrames))
                PromoteToLocked(timestamp, score);
        }

        void HandleLocked(long timestamp)
        {
            int idx = m_Matching.FindBestMatch(
                m_LockedDetection, m_Scratch, m_FrameWidth, m_FrameHeight,
                m_MatchAlphaIoU, m_MatchBetaCenter, m_MatchMinScore, out float score);
            m_LastMatchScore = score;

            if (idx < 0)
            {
                m_LostElapsed = 0f;
                SetState(TargetLockState.TARGET_LOST);
                Debug.Log("[SingleTarget] LOCKED → LOST (no match this frame)");
                // Do not emit update — Anchor keeps last pose.
                return;
            }

            // Critical: update tracked detection from MATCH, never from highest-confidence reselection.
            m_LockedDetection = m_Scratch[idx];
            m_LostElapsed = 0f;
            EmitLockedUpdate(timestamp, score, 0f);
        }

        void HandleLost(long timestamp)
        {
            int idx = m_Matching.FindBestMatch(
                m_LockedDetection, m_Scratch, m_FrameWidth, m_FrameHeight,
                m_MatchAlphaIoU, m_MatchBetaCenter, m_MatchMinScore, out float score);
            m_LastMatchScore = score;

            if (idx >= 0)
            {
                m_LockedDetection = m_Scratch[idx];
                m_LostElapsed = 0f;
                SetState(TargetLockState.TARGET_LOCKED);
                Debug.Log($"[SingleTarget] LOST → LOCKED (reamatch score={score:F2})");
                EmitLockedUpdate(timestamp, score, 0f);
                return;
            }

            // No match: advance grace using wall time between frames approximately via Time.deltaTime
            // (Detection frames may not be every Unity frame; also tick in Update).
            // Here we only mark; Update accumulates lost time continuously.
        }

        void Update()
        {
            if (m_State != TargetLockState.TARGET_LOST)
                return;

            m_LostElapsed += Time.deltaTime;
            StateChanged?.Invoke();

            if (m_LostElapsed >= m_LostTargetGracePeriod)
            {
                Debug.Log($"[SingleTarget] LOST grace exceeded ({m_LostElapsed:F2}s) → NO_TARGET (reacquire allowed)");
                ClearToNoTarget();
                StateChanged?.Invoke();
            }
        }

        void PromoteToLocked(long timestamp, float score)
        {
            SetState(TargetLockState.TARGET_LOCKED);
            m_LostElapsed = 0f;
            Debug.Log($"[SingleTarget] LOCKED conf={m_LockedDetection.confidence:F2}");
            EmitLockedUpdate(timestamp, score, 0f);
        }

        void ClearToNoTarget()
        {
            m_State = TargetLockState.NO_TARGET;
            m_AcquireStreak = 0;
            m_LostElapsed = 0f;
            m_LastMatchScore = 0f;
            m_LockedDetection = default;
            m_AcquireCandidate = default;
        }

        void EmitLockedUpdate(long timestamp, float matchScore, float lostSeconds)
        {
            LockedTargetUpdated?.Invoke(new LockedTargetEvent(
                hasTarget: true,
                state: m_State,
                detection: m_LockedDetection,
                matchScore: matchScore,
                lostSeconds: lostSeconds,
                timestamp: timestamp));
        }

        void SetState(TargetLockState state) => m_State = state;

        void FilterLighters(IReadOnlyList<ObjectDetectionEvent> source, List<ObjectDetectionEvent> dst)
        {
            dst.Clear();
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                ObjectDetectionEvent e = source[i];
                if (string.IsNullOrEmpty(e.className))
                    continue;
                if (!string.Equals(e.className, m_TargetClassName, StringComparison.OrdinalIgnoreCase))
                    continue;
                dst.Add(e);
            }
        }

        void OnDrawGizmos()
        {
            if (!m_DrawGizmos || !Application.isPlaying)
                return;

            // Image-space gizmos aren't world gizmos; draw world markers via Anchor if present.
            // Instead visualize last-frame centers projected roughly in front of main camera.
            Camera cam = Camera.main;
            if (cam == null || m_FrameWidth <= 0f || m_FrameHeight <= 0f)
                return;

            for (int i = 0; i < m_LastFrameLighters.Count; i++)
            {
                ObjectDetectionEvent e = m_LastFrameLighters[i];
                bool isLocked = HasLockedTarget
                    && ApproximatelySame(e, m_LockedDetection)
                    && m_State != TargetLockState.NO_TARGET;

                Gizmos.color = isLocked ? Color.green : new Color(0.6f, 0.6f, 0.6f, 0.8f);
                Vector3 world = PixelToWorld(cam, e.centerX, e.centerY);
                Gizmos.DrawWireSphere(world, isLocked ? 0.06f : 0.04f);
            }
        }

        static bool ApproximatelySame(ObjectDetectionEvent a, ObjectDetectionEvent b)
        {
            return Mathf.Abs(a.centerX - b.centerX) < 2f
                   && Mathf.Abs(a.centerY - b.centerY) < 2f
                   && Mathf.Abs(a.confidence - b.confidence) < 0.05f;
        }

        Vector3 PixelToWorld(Camera cam, float px, float py)
        {
            float viewportX = px / m_FrameWidth;
            float viewportY = 1f - (py / m_FrameHeight);
            Ray ray = cam.ViewportPointToRay(new Vector3(viewportX, viewportY, 0f));
            return ray.origin + ray.direction.normalized * 2f;
        }
    }
}
