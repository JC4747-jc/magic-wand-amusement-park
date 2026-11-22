using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MagicMR
{
    /// <summary>
    /// Keyboard (or remote) condition presets for study automation.
    /// 1 Baseline, 2 Appearance, 3 Appearance+Agency, 4 All.
    /// </summary>
    public class ExperimentConditionSwitcher : MonoBehaviour
    {
        [SerializeField]
        GestureManager m_GestureManager;

        [SerializeField]
        RealityEditor m_RealityEditor;

        public int CurrentConditionId { get; private set; } = 4;
        public EnabledDimensions CurrentDimensions => Resolve(CurrentConditionId).dims;
        public static EnabledDimensions DimensionsFor(int conditionId)=>Resolve(Mathf.Clamp(conditionId,1,4)).dims;

        void Start()
        {
            if (m_GestureManager == null)
                m_GestureManager = FindFirstObjectByType<GestureManager>();
            if (m_RealityEditor == null)
            {
                var lighter = GameObject.Find("Lighter");
                if (lighter != null)
                    m_RealityEditor = lighter.GetComponent<RealityEditor>();
            }
            if (DataLogger.Instance != null && DataLogger.Instance.SessionActive)
                SetCondition(DataLogger.Instance.ConditionId);
        }

        void Update()
        {
            // Park uses plain 1-4 for explicit virtual-test actions. Study presets
            // require Ctrl there so one key cannot both change condition and cast.
            bool park = FindFirstObjectByType<ParkController>() != null;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (park && !keyboard.leftCtrlKey.isPressed && !keyboard.rightCtrlKey.isPressed) return;

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                SetCondition(1);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                SetCondition(2);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                SetCondition(3);
            else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
                SetCondition(4);
            else if (keyboard.rKey.wasPressedThisFrame)
            {
                // Keyboard only — does not use the floating UI path.
                var reset = FindFirstObjectByType<StudyResetWristUi>();
                reset?.TriggerReset();
            }
#else
            if (park && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)) return;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                SetCondition(1);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                SetCondition(2);
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                SetCondition(3);
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
                SetCondition(4);
            else if (Input.GetKeyDown(KeyCode.R))
            {
                var reset = FindFirstObjectByType<StudyResetWristUi>();
                reset?.TriggerReset();
            }
#endif
        }

        public void SetCondition(int conditionId)
        {
            CurrentConditionId = Mathf.Clamp(conditionId, 1, 4);
            var (name, dims) = Resolve(CurrentConditionId);

            if (!m_GestureManager) m_GestureManager = FindFirstObjectByType<GestureManager>();
            if (!m_RealityEditor) m_RealityEditor = FindFirstObjectByType<RealityEditor>();

            m_GestureManager?.SetEnabledDimensions(dims);
            m_GestureManager?.SetConditionLabel(name);
            DataLogger.Instance?.SetCondition(name, CurrentConditionId);
            m_RealityEditor?.ResetTarget();
            FindFirstObjectByType<LighterAnchorManager>()?.ReturnToPinned();
            foreach (var park in FindObjectsByType<ParkController>(FindObjectsSortMode.None))
                park.ApplyStudyCondition(dims);

            Debug.Log($"[MagicMR] Condition -> {CurrentConditionId} ({name}, {dims})", this);
        }

        static (string name, EnabledDimensions dims) Resolve(int id)
        {
            switch (id)
            {
                case 1:
                    return ("C1_Baseline", EnabledDimensions.None);
                case 2:
                    return ("C2_Appearance", EnabledDimensions.Appearance);
                case 3:
                    return ("C3_AppearanceAgency", EnabledDimensions.Appearance | EnabledDimensions.Agency);
                default:
                    return ("C4_All", EnabledDimensions.All);
            }
        }
    }
}
