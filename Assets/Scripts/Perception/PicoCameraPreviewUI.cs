using UnityEngine;
using UnityEngine.UI;

namespace Perception
{
    /// <summary>
    /// Minimal XR-facing preview for <see cref="PicoCameraCapture"/>.
    /// Creates a world-space panel if no RawImage is assigned.
    /// </summary>
    public class PicoCameraPreviewUI : MonoBehaviour
    {
        [SerializeField]
        PicoCameraCapture m_Capture;

        [SerializeField]
        RawImage m_RawImage;

        [SerializeField]
        Text m_StatusText;

        [SerializeField]
        float m_DistanceFromCamera = 1.2f;

        [SerializeField]
        Vector2 m_PanelSizeMeters = new Vector2(0.72f, 0.48f);

        Canvas m_RuntimeCanvas;
        bool m_BuiltRuntimeUi;

        void Awake()
        {
            if (m_Capture == null)
                m_Capture = GetComponent<PicoCameraCapture>();
            if (m_Capture == null)
                m_Capture = FindFirstObjectByType<PicoCameraCapture>();
        }

        void OnEnable()
        {
            if (m_Capture != null)
            {
                m_Capture.FrameUpdated += OnFrameUpdated;
                m_Capture.StatusChanged += OnStatusChanged;
            }
        }

        void OnDisable()
        {
            if (m_Capture != null)
            {
                m_Capture.FrameUpdated -= OnFrameUpdated;
                m_Capture.StatusChanged -= OnStatusChanged;
            }
        }

        void Start()
        {
            EnsureUi();
            OnStatusChanged();
        }

        void LateUpdate()
        {
            if (m_RuntimeCanvas == null)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            Transform t = m_RuntimeCanvas.transform;
            t.position = cam.transform.position + cam.transform.forward * m_DistanceFromCamera;
            t.rotation = Quaternion.LookRotation(t.position - cam.transform.position, Vector3.up);
        }

        void OnFrameUpdated(Texture2D texture)
        {
            EnsureUi();
            if (m_RawImage != null && texture != null)
                m_RawImage.texture = texture;
            OnStatusChanged();
        }

        void OnStatusChanged()
        {
            if (m_StatusText == null || m_Capture == null)
                return;

            string texInfo = m_Capture.PreviewTexture != null
                ? $"tex={m_Capture.PreviewTexture.width}x{m_Capture.PreviewTexture.height} id={m_Capture.PreviewTexture.GetInstanceID()}"
                : "tex=(none)";
            var sender = GetComponent<PicoYoloFrameSender>();
            string yoloSend = sender != null
                ? $"id={sender.LastSentFrameId} {sender.LastSentWidth}x{sender.LastSentHeight} jpg={sender.LastJpegBytes}B"
                : "(no sender)";
            m_StatusText.text =
                "PICO CAMERA (PXR_CameraImage)\n" +
                "Source: PicoCameraCapture.PreviewTexture\n" +
                $"NOT Camera.main / NOT VST / NOT Webcam\n" +
                $"{m_Capture.Width}x{m_Capture.Height}\n" +
                $"FPS: {m_Capture.MeasuredFps:0.0}\n" +
                $"Camera: {m_Capture.ActiveCameraId}\n" +
                $"Format: {m_Capture.PixelFormatLabel}\n" +
                $"{texInfo}\n" +
                $"YOLO send: {yoloSend}\n" +
                $"Status: {m_Capture.Status}\n" +
                m_Capture.StatusDetail;
        }

        void EnsureUi()
        {
            if (m_RawImage != null)
                return;

            if (m_BuiltRuntimeUi)
                return;

            m_BuiltRuntimeUi = true;

            var canvasGo = new GameObject("PicoCameraPreviewCanvas");
            canvasGo.transform.SetParent(transform, false);
            m_RuntimeCanvas = canvasGo.AddComponent<Canvas>();
            m_RuntimeCanvas.renderMode = RenderMode.WorldSpace;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(800f, 560f);
            float scaleX = m_PanelSizeMeters.x / 800f;
            float scaleY = m_PanelSizeMeters.y / 560f;
            rt.localScale = new Vector3(scaleX, scaleY, 1f);

            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.65f);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            var rawGo = new GameObject("RawImage");
            rawGo.transform.SetParent(canvasGo.transform, false);
            m_RawImage = rawGo.AddComponent<RawImage>();
            m_RawImage.color = Color.white;
            var rawRt = rawGo.GetComponent<RectTransform>();
            rawRt.anchorMin = new Vector2(0.05f, 0.22f);
            rawRt.anchorMax = new Vector2(0.95f, 0.95f);
            rawRt.offsetMin = Vector2.zero;
            rawRt.offsetMax = Vector2.zero;

            var textGo = new GameObject("Status");
            textGo.transform.SetParent(canvasGo.transform, false);
            m_StatusText = textGo.AddComponent<Text>();
            m_StatusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (m_StatusText.font == null)
                m_StatusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            m_StatusText.fontSize = 28;
            m_StatusText.color = Color.green;
            m_StatusText.alignment = TextAnchor.UpperLeft;
            m_StatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            m_StatusText.verticalOverflow = VerticalWrapMode.Overflow;
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.05f, 0.02f);
            textRt.anchorMax = new Vector2(0.95f, 0.22f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            Debug.Log("[PicoCamera] Runtime preview UI created (world-space panel).");
        }
    }
}
