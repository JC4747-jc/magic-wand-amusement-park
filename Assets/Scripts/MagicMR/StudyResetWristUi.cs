using UnityEngine;

namespace MagicMR
{
    /// <summary>
    /// Keyboard / legacy reset entry point. Interactive left-hand reset is owned
    /// by <see cref="MRGestureController"/> (pinch + palm facing HMD).
    /// Floating button UI is disabled to prevent left-hand mis-triggers.
    /// </summary>
    public class StudyResetWristUi : MonoBehaviour
    {
        [SerializeField]
        bool m_ShowLegacyFloatingButton;

        void Start()
        {
            if (!m_ShowLegacyFloatingButton)
            {
                Debug.Log(
                    "[MagicMR] Reset: left pinch + palm facing HMD (FSM). Floating UI hidden.",
                    this);
            }
        }

        /// <summary>Used by keyboard R / experiment tooling.</summary>
        public void TriggerReset()
        {
            var fsm = MRGestureController.Instance;
            if (fsm != null)
            {
                fsm.PerformFullReset("ui_or_keyboard");
                return;
            }

            // Legacy path if FSM missing.
            var lighter = GameObject.Find("Lighter");
            var editor = lighter != null ? lighter.GetComponent<RealityEditor>() : null;
            editor?.ResetTarget();
            FindFirstObjectByType<LighterAnchorManager>()?.BeginRecalibration();
            FindFirstObjectByType<CalibrationRitual>()?.RestartRitual();
            FindFirstObjectByType<PinchGestureDetector>()?.ResetForNewTrial();
            FindFirstObjectByType<GestureManager>()?.NotifyStudyReset();
            Debug.Log("[MagicMR] Study reset (legacy path).", this);
        }
    }
}
