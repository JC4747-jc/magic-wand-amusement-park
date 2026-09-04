using System.Reflection;
using MagicMR;
using UnityEngine;

namespace Perception
{
    /// <summary>
    /// One-shot BridgeTest probe: verifies SingleTarget → Anchor → Binder → Lighter → RealityEditor
    /// wiring and that AnchorManager is NOT listening to DetectionReceived.
    /// </summary>
    public class BridgeTargetPipelineProbe : MonoBehaviour
    {
        [SerializeField]
        bool m_RunOnStart = true;

        void Start()
        {
            if (m_RunOnStart)
                RunProbe();
        }

        [ContextMenu("Run Pipeline Probe")]
        public void RunProbe()
        {
            var dm = FindFirstObjectByType<DetectionManager>();
            var stm = FindFirstObjectByType<SingleTargetManager>();
            var am = FindFirstObjectByType<AnchorManager>();
            var binder = FindFirstObjectByType<LighterUnderAnchorBinder>();
            var lighterGo = GameObject.Find("Lighter");
            var editor = lighterGo != null ? lighterGo.GetComponent<RealityEditor>() : null;
            var gm = FindFirstObjectByType<GestureManager>();
            var gate = FindFirstObjectByType<LighterGestureTargetGate>();

            Debug.Log(
                "[BridgePipeline] ===== Single-Target Integration Probe =====\n" +
                $"  DetectionManager: {(dm != null ? dm.name : "MISSING")}\n" +
                $"  SingleTargetManager: {(stm != null ? stm.name : "MISSING")}\n" +
                $"  AnchorManager: {(am != null ? am.name : "MISSING")}\n" +
                $"  LighterUnderAnchorBinder: {(binder != null ? binder.name : "MISSING")}\n" +
                $"  Virtual Lighter GO: {(lighterGo != null ? lighterGo.name : "MISSING")} " +
                $"parent={(lighterGo != null && lighterGo.transform.parent != null ? lighterGo.transform.parent.name : "(none)")}\n" +
                $"  RealityEditor on Lighter: {(editor != null)}\n" +
                $"  GestureManager: {(gm != null ? gm.name : "MISSING")}\n" +
                $"  LighterGestureTargetGate: {(gate != null ? gate.name : "MISSING")}\n" +
                $"  Expected chain:\n" +
                "    DetectionFrameReceived → SingleTargetManager → LockedTargetUpdated\n" +
                "    → AnchorManager → AnchorUpdated → LighterUnderAnchorBinder\n" +
                "    → Virtual Lighter (scene) → GestureManager/RealityEditor\n" +
                $"  AnchorManager uses DetectionReceived? {AnchorManagerListensToDetections(am)}\n" +
                "============================================================");
        }

        static string AnchorManagerListensToDetections(AnchorManager am)
        {
            if (am == null)
                return "n/a";

            // Source inspection: field should be SingleTargetManager, not DetectionManager.
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var stmField = typeof(AnchorManager).GetField("m_SingleTargetManager", flags);
            var legacy = typeof(AnchorManager).GetField("m_DetectionManager", flags);
            if (legacy != null)
                return "YES (legacy field still present — unexpected)";
            if (stmField != null)
            {
                var val = stmField.GetValue(am);
                return val != null ? "NO (wired to SingleTargetManager only)" : "NO field but SingleTarget ref null";
            }

            return "unknown";
        }
    }
}
