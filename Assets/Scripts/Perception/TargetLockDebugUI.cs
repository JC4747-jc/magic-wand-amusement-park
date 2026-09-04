using UnityEngine;
using UnityEngine.UI;
using MagicMR;

namespace Perception
{
    /// <summary>
    /// Runtime debug HUD for the full lock → anchor → virtual lighter → reality-edit chain.
    /// </summary>
    public class TargetLockDebugUI : MonoBehaviour
    {
        [SerializeField]
        SingleTargetManager m_SingleTarget;

        [SerializeField]
        AnchorManager m_AnchorManager;

        [SerializeField]
        LighterUnderAnchorBinder m_Binder;

        [SerializeField]
        Transform m_VirtualLighter;

        [SerializeField]
        RealityEditor m_RealityEditor;

        [SerializeField]
        Text m_Text;

        [SerializeField]
        float m_DistanceFromCamera = 1.05f;

        Canvas m_Canvas;
        bool m_Built;

        void Awake()
        {
            ResolveRefs();
        }

        void OnEnable()
        {
            if (m_SingleTarget != null)
                m_SingleTarget.StateChanged += Refresh;
        }

        void OnDisable()
        {
            if (m_SingleTarget != null)
                m_SingleTarget.StateChanged -= Refresh;
        }

        void Start()
        {
            ResolveRefs();
            EnsureUi();
            Refresh();
        }

        void LateUpdate()
        {
            if (m_Canvas == null)
                return;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            Transform t = m_Canvas.transform;
            t.position = cam.transform.position + cam.transform.forward * m_DistanceFromCamera
                         + cam.transform.up * 0.15f
                         + cam.transform.right * -0.35f;
            t.rotation = Quaternion.LookRotation(t.position - cam.transform.position, Vector3.up);
            Refresh();
        }

        void ResolveRefs()
        {
            if (m_SingleTarget == null)
                m_SingleTarget = FindFirstObjectByType<SingleTargetManager>();
            if (m_AnchorManager == null)
                m_AnchorManager = FindFirstObjectByType<AnchorManager>();
            if (m_Binder == null)
                m_Binder = FindFirstObjectByType<LighterUnderAnchorBinder>();
            if (m_VirtualLighter == null)
            {
                var go = GameObject.Find("Lighter");
                if (go != null)
                    m_VirtualLighter = go.transform;
            }

            if (m_RealityEditor == null && m_VirtualLighter != null)
                m_RealityEditor = m_VirtualLighter.GetComponent<RealityEditor>();
        }

        void Refresh()
        {
            EnsureUi();
            if (m_Text == null || m_SingleTarget == null)
                return;

            ObjectDetectionEvent d = m_SingleTarget.LockedDetection;
            Vector3 anchorPos = m_AnchorManager != null && m_AnchorManager.LighterAnchor != null
                ? m_AnchorManager.LighterAnchor.position
                : Vector3.zero;
            Vector3 lighterPos = m_VirtualLighter != null ? m_VirtualLighter.position : Vector3.zero;
            string lighterParent = m_VirtualLighter != null && m_VirtualLighter.parent != null
                ? m_VirtualLighter.parent.name
                : "(none)";
            string dim = m_RealityEditor != null ? m_RealityEditor.ActiveDimension.ToString() : "(no RE)";
            float stateDur = m_RealityEditor != null ? m_RealityEditor.CurrentStateDuration : 0f;

            m_Text.text =
                "TARGET LOCK + PIPELINE\n" +
                $"Target: {(m_SingleTarget.HasLockedTarget ? m_SingleTarget.TargetClassDisplay() : "(none)")}\n" +
                $"State: {m_SingleTarget.State}\n" +
                $"Confidence: {d.confidence:F2}\n" +
                $"BBox: ({d.x1:F0},{d.y1:F0})-({d.x2:F0},{d.y2:F0})\n" +
                $"Match Score: {m_SingleTarget.LastMatchScore:F2}\n" +
                $"Lost: {m_SingleTarget.LostElapsed:F2}s\n" +
                $"Frame lighters: {m_SingleTarget.LastFrameLighters.Count}\n" +
                $"Anchor: {anchorPos.x:F2} {anchorPos.y:F2} {anchorPos.z:F2}\n" +
                $"Virtual Lighter: {lighterPos.x:F2} {lighterPos.y:F2} {lighterPos.z:F2}\n" +
                $"Lighter parent: {lighterParent}\n" +
                $"RealityEditor: {dim} ({stateDur:F1}s)";
        }

        void EnsureUi()
        {
            if (m_Text != null || m_Built)
                return;
            m_Built = true;

            var canvasGo = new GameObject("TargetLockDebugCanvas");
            canvasGo.transform.SetParent(transform, false);
            m_Canvas = canvasGo.AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(560f, 420f);
            rt.localScale = new Vector3(0.001f, 0.001f, 0.001f);

            var bg = new GameObject("BG");
            bg.transform.SetParent(canvasGo.transform, false);
            var img = bg.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.55f);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(canvasGo.transform, false);
            m_Text = textGo.AddComponent<Text>();
            m_Text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (m_Text.font == null)
                m_Text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            m_Text.fontSize = 24;
            m_Text.color = Color.yellow;
            m_Text.alignment = TextAnchor.UpperLeft;
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.04f, 0.04f);
            textRt.anchorMax = new Vector2(0.96f, 0.96f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
        }
    }

    public static class SingleTargetManagerDebugExt
    {
        public static string TargetClassDisplay(this SingleTargetManager mgr)
        {
            if (mgr == null || !mgr.HasLockedTarget)
                return "(none)";
            return string.IsNullOrEmpty(mgr.LockedDetection.className)
                ? "Lighter"
                : mgr.LockedDetection.className;
        }
    }
}
