using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace MagicMR
{
    /// <summary>
    /// Editor-only N/P scene picker for HCI meeting 4 previews.
    /// 1–4 stay on MagicMREditorDebugKeys (compiled out of player builds).
    /// </summary>
    public class HciMeetingPreviewInput : MonoBehaviour
    {
#if UNITY_EDITOR
        [SerializeField]
        bool m_ForcePinInEditor = true;

        void Start()
        {
            if (!m_ForcePinInEditor)
                return;

            var anchor = FindFirstObjectByType<LighterAnchorManager>();
            var lighter = GameObject.Find("Lighter");
            if (anchor != null && lighter != null && !anchor.IsCalibrated)
                anchor.CalibrateAt(lighter.transform.position);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null)
                return;

            var director = HciMeetingDirector.Instance ?? FindFirstObjectByType<HciMeetingDirector>();
            if (director == null)
                return;

            if (kb.nKey.wasPressedThisFrame || kb.rightBracketKey.wasPressedThisFrame)
                director.CycleScenario(1);
            if (kb.pKey.wasPressedThisFrame || kb.leftBracketKey.wasPressedThisFrame)
                director.CycleScenario(-1);
        }
#else
        void Awake()
        {
            Destroy(this);
        }
#endif
    }
}
