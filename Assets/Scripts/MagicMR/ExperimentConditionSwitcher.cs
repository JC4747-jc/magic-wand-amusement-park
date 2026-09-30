using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MagicMR
{
    /// <summary>
    /// Legacy keyboard condition presets. Affective-narrative mode ignores
    /// them so accidental keys cannot add gesture conditions to the study.
    /// </summary>
    public class ExperimentConditionSwitcher : MonoBehaviour
    {
        [SerializeField]
        GestureManager m_GestureManager;

        [SerializeField]
        RealityEditor m_RealityEditor;

        public int CurrentConditionId { get; private set; } = 4;

        void Start()
        {
            if (m_GestureManager == null)
                m_GestureManager = FindFirstObjectByType<GestureManager>();
            if (m_RealityEditor == null)
            {
                m_RealityEditor = RealityScenarioDirector.Instance != null
                    ? RealityScenarioDirector.Instance.CurrentEditor
                    : null;
                var lighter = GameObject.Find("Lighter");
                if (m_RealityEditor == null && lighter != null)
                    m_RealityEditor = lighter.GetComponent<RealityEditor>();
                m_RealityEditor ??= FindFirstObjectByType<RealityEditor>();
            }
        }

        void Update()
        {
            // This component remains in legacy scenes, but experimental
            // conditions must never re-enable Circle/Swipe/Fist in the
            // single-trigger affective-animation study.
            if (RealityScenarioDirector.Instance != null &&
                RealityScenarioDirector.Instance.IsAffectiveNarrativeMode)
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                    FindFirstObjectByType<StudyResetWristUi>()?.TriggerReset();
#else
                if (Input.GetKeyDown(KeyCode.R))
                    FindFirstObjectByType<StudyResetWristUi>()?.TriggerReset();
#endif
                return;
            }
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

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
            if (RealityScenarioDirector.Instance != null &&
                RealityScenarioDirector.Instance.IsAffectiveNarrativeMode)
            {
                CurrentConditionId = 1;
                m_GestureManager?.SetEnabledDimensions(EnabledDimensions.All);
                m_GestureManager?.SetConditionLabel("AffectiveNarrative");
                DataLogger.Instance?.SetCondition("AffectiveNarrative", 1);
                RealityScenarioDirector.Instance.ResetNarrative();
                return;
            }

            CurrentConditionId = Mathf.Clamp(conditionId, 1, 4);
            var (name, dims) = Resolve(CurrentConditionId);

            m_GestureManager?.SetEnabledDimensions(dims);
            m_GestureManager?.SetConditionLabel(name);
            DataLogger.Instance?.SetCondition(name, CurrentConditionId);
            // A condition change resets the current subject, rather than assuming
            // every experiment subject is a lighter.
            var director = RealityScenarioDirector.Instance;
            (director != null ? director.CurrentEditor : m_RealityEditor)?.ResetTarget();
            FindFirstObjectByType<LighterAnchorManager>()?.ReturnToPinned();

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
