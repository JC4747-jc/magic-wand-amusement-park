using UnityEngine;
#if UNITY_EDITOR
using System.Collections;
using UnityEngine.InputSystem;
#endif

namespace MagicMR
{
    /// <summary>
    /// Editor Play-only 4D preview. Keys are compiled out of player / PICO builds.
    /// </summary>
    [DefaultExecutionOrder(80)]
    public class MagicMREditorDebugKeys : MonoBehaviour
    {
#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void SpawnInEditorPlay()
        {
            if (!Application.isPlaying)
                return;
            if (FindFirstObjectByType<MagicMRStudyBootstrap>() == null)
                return;
            if (FindFirstObjectByType<MagicMREditorDebugKeys>() != null)
                return;

            var go = new GameObject("MagicMREditorDebugKeys");
            go.AddComponent<MagicMREditorDebugKeys>();
        }

        RealityEditor m_Editor;
        bool m_HintVisible = true;

        IEnumerator Start()
        {
            yield return null;
            PreparePreviewTarget();
            if (ShouldAutoVerifyKeys())
                yield return VerifyKeysRoutine();
        }

        static bool ShouldAutoVerifyKeys()
        {
            var flag = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath)?.FullName ?? ".",
                "Temp",
                "MagicMRVerifyKeys.flag");
            if (!System.IO.File.Exists(flag))
                return false;
            System.IO.File.Delete(flag);
            return true;
        }

        IEnumerator VerifyKeysRoutine()
        {
            yield return new WaitForSeconds(0.4f);
            Debug.Log("[MagicMR] Verify 1 charcoal/magma", this);
            m_Editor?.OnGestureA_Pinch();
            yield return new WaitForSeconds(1.1f);
            Debug.Log("[MagicMR] Verify 2 wings/cough", this);
            m_Editor?.OnGestureC_Circle();
            yield return new WaitForSeconds(1.1f);
            Debug.Log("[MagicMR] Verify 3 dodge/wind", this);
            m_Editor?.OnGestureB_Swipe();
            yield return new WaitForSeconds(0.8f);
            Debug.Log("[MagicMR] Verify 4 purify/flower", this);
            m_Editor?.OnGestureD_FistBurst();
        }

        void Update()
        {
            if (m_Editor == null)
                m_Editor = ResolveEditor();
            if (m_Editor == null)
                return;

            var kb = Keyboard.current;
            if (kb == null)
                return;

            if (Pressed(kb.digit1Key, kb.numpad1Key))
                m_Editor.OnGestureA_Pinch();
            else if (Pressed(kb.digit2Key, kb.numpad2Key))
                m_Editor.OnGestureC_Circle();
            else if (Pressed(kb.digit3Key, kb.numpad3Key))
                m_Editor.OnGestureB_Swipe();
            else if (Pressed(kb.digit4Key, kb.numpad4Key))
                m_Editor.OnGestureD_FistBurst();
            else if (kb.nKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame || kb.rightBracketKey.wasPressedThisFrame)
                CycleHci(1);
            else if (kb.pKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame || kb.leftBracketKey.wasPressedThisFrame)
                CycleHci(-1);
            else if (kb.hKey.wasPressedThisFrame)
                m_HintVisible = !m_HintVisible;
        }

        static void CycleHci(int delta)
        {
            var director = HciMeetingDirector.Instance ?? FindFirstObjectByType<HciMeetingDirector>();
            director?.CycleScenario(delta);
        }

        void OnGUI()
        {
            if (!m_HintVisible)
                return;

            var director = HciMeetingDirector.Instance ?? FindFirstObjectByType<HciMeetingDirector>();
            var scene = director != null
                ? HciMeetingScenarioNames.DisplayName(director.Scenario)
                : "（无 HCI Director）";
            var hint = director != null ? HciMeetingScenarioNames.Hint(director.Scenario) : "";

            const int pad = 12;
            var rect = new Rect(pad, pad, 520, 148);
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(rect.x + 10, rect.y + 8, rect.width - 16, rect.height - 12),
                "当前场景  " + scene + "\n" +
                hint + "\n" +
                "1 外观   2 生命   3 规则   4 净化绽放（开花奖励）\n" +
                "N / → 下一场景    P / ← 上一场景    H 隐藏");
        }

        static bool Pressed(UnityEngine.InputSystem.Controls.KeyControl a, UnityEngine.InputSystem.Controls.KeyControl b)
        {
            return (a != null && a.wasPressedThisFrame) || (b != null && b.wasPressedThisFrame);
        }

        void PreparePreviewTarget()
        {
            var lighterGo = GameObject.Find("Lighter");
            if (lighterGo == null)
            {
                Debug.LogWarning("[MagicMR] Editor debug keys: no Lighter in the scene.");
                return;
            }

            lighterGo.SetActive(true);
            EnsureLighterInView(lighterGo.transform);

            var anchor = lighterGo.GetComponent<LighterAnchorManager>() ??
                         FindFirstObjectByType<LighterAnchorManager>();
            if (anchor != null && !anchor.IsCalibrated)
                anchor.CalibrateAt(lighterGo.transform.position);

            m_Editor = lighterGo.GetComponent<RealityEditor>() ?? ResolveEditor();
            m_Editor?.EnsureVisible();
            ForceRenderersOn(lighterGo);

            var ghost = GameObject.Find("GhostLighter_Calibration");
            if (ghost != null)
                Destroy(ghost);
            var prompt = GameObject.Find("CalibrationPrompt");
            if (prompt != null)
                Destroy(prompt);

            Debug.Log(
                "[MagicMR] Editor Play 4D keys ready: 1 burnt+smoke, 2 eyes+breath, " +
                "3 hop, 4 shatter→flower (bypasses FSM / pinch / YOLO).",
                this);
        }

        static RealityEditor ResolveEditor()
        {
            var lighter = GameObject.Find("Lighter");
            if (lighter != null)
            {
                var editor = lighter.GetComponent<RealityEditor>();
                if (editor != null)
                    return editor;
            }

            return FindFirstObjectByType<RealityEditor>();
        }

        static void EnsureLighterInView(Transform lighter)
        {
            var cam = Camera.main;
            if (cam == null || !cam.isActiveAndEnabled)
            {
                foreach (var candidate in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                {
                    if (candidate != null && candidate.enabled && candidate.gameObject.activeInHierarchy)
                    {
                        cam = candidate;
                        break;
                    }
                }
            }

            if (cam == null)
                return;

            var dist = Vector3.Distance(cam.transform.position, lighter.position);
            if (dist >= 0.25f && dist <= 1.8f)
                return;

            var forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();
            lighter.position = cam.transform.position
                               + forward * StudySpec.LighterDistance
                               + Vector3.up * StudySpec.LighterHeightOffset;
        }

        static void ForceRenderersOn(GameObject lighter)
        {
            foreach (var renderer in lighter.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer is ParticleSystemRenderer)
                    continue;
                renderer.enabled = true;
            }
        }
#else
        void Awake()
        {
            Destroy(this);
        }
#endif
    }
}
