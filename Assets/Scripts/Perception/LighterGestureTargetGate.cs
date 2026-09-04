using MagicMR;
using UnityEngine;

namespace Perception
{
    /// <summary>
    /// BridgeTest proximity gate: gestures apply to Lighter only when the
    /// hand/pinch query point is within <see cref="m_GestureTargetDistance"/>.
    /// Does not move, reparent, or calibrate Anchor_Lighter / Lighter.
    /// </summary>
    public class LighterGestureTargetGate : GestureTargetGateBase
    {
        [SerializeField]
        Transform m_Lighter;

        [SerializeField]
        float m_GestureTargetDistance = 0.15f;

        [SerializeField]
        [Tooltip(
            "Phase 2 validation only. When false, skip distance rejection. " +
            "MUST stay true for production / Phase 3+. Do not leave bypass enabled.")]
        bool m_RequireProximity = true;

        [SerializeField]
        bool m_RejectWhenNoHandPosition = true;

        /// <summary>Inspector / runtime toggle for Phase 2 bypass. Default true.</summary>
        public bool RequireProximity
        {
            get => m_RequireProximity;
            set => m_RequireProximity = value;
        }

        void Awake()
        {
            if (m_Lighter == null)
            {
                var go = GameObject.Find("Lighter");
                if (go != null)
                    m_Lighter = go.transform;
            }
        }

        public override bool Allow(
            EditDimension dimension,
            string gestureName,
            Vector3 queryWorldPosition,
            bool hasQueryPosition)
        {
            if (m_Lighter == null)
            {
                var go = GameObject.Find("Lighter");
                if (go != null)
                    m_Lighter = go.transform;
            }

            if (m_Lighter == null)
            {
                Debug.LogWarning("[GestureTargetGate] Rejected: Lighter not found.");
                return false;
            }

            if (!hasQueryPosition)
            {
                if (m_RejectWhenNoHandPosition)
                {
                    Debug.Log(
                        $"[GestureTargetGate] {gestureName} rejected: no hand/pinch position.");
                    return false;
                }

                LogPhase2Accepted(dimension, gestureName);
                return true;
            }

            var distance = Vector3.Distance(queryWorldPosition, m_Lighter.position);
            if (distance <= m_GestureTargetDistance)
            {
                Debug.Log(
                    $"[GestureTargetGate] {gestureName} accepted: distance={distance:F3} " +
                    $"(≤{m_GestureTargetDistance:F3}) dim={dimension}");
                LogPhase2Accepted(dimension, gestureName);
                return true;
            }

            // Phase 2 validation bypass — does not change threshold; only skips rejection.
            if (!m_RequireProximity)
            {
                Debug.Log(
                    $"[GestureTargetGate] BYPASS proximity check: " +
                    $"dimension={dimension} gesture={gestureName} distance={distance:F3}");
                LogPhase2Accepted(dimension, gestureName);
                return true;
            }

            Debug.Log(
                $"[GestureTargetGate] {gestureName} rejected: distance={distance:F3} " +
                $"(>{m_GestureTargetDistance:F3}) dim={dimension}");
            return false;
        }

        static void LogPhase2Accepted(EditDimension dimension, string gestureName)
        {
            Debug.Log(
                $"[Phase2] Gesture accepted by gate: " +
                $"dimension={dimension} gesture={gestureName}");
        }
    }
}
