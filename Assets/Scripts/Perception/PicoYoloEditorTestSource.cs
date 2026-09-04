using UnityEngine;

namespace Perception
{
    /// <summary>
    /// Editor-only: feeds a Test Image Texture2D into <see cref="PicoYoloFrameSender.TrySendTexture"/>.
    /// Does not touch PicoCameraCapture / PXR. Empty stub on Android player builds.
    /// Placed under Perception/ (not an Editor/ folder) so BridgeTest keeps a valid component on device.
    /// </summary>
    public class PicoYoloEditorTestSource : MonoBehaviour
    {
#if UNITY_EDITOR
        [SerializeField]
        Texture2D m_TestImage;

        [SerializeField]
        PicoYoloFrameSender m_Sender;

        [SerializeField]
        float m_SendFps = 5f;

        [SerializeField]
        bool m_AutoStart = true;

        float m_NextSendTime;
        bool m_Started;
        float m_NextConnectWarnTime;

        void Awake()
        {
            if (m_Sender == null)
                m_Sender = GetComponent<PicoYoloFrameSender>();
            if (m_Sender == null)
                m_Sender = FindFirstObjectByType<PicoYoloFrameSender>();
        }

        void Start()
        {
            if (!m_AutoStart)
                return;
            Begin();
        }

        void Update()
        {
            if (!m_Started || m_Sender == null || m_TestImage == null)
                return;

            if (Time.unscaledTime < m_NextSendTime)
                return;

            float interval = m_SendFps <= 0.1f ? 0.2f : 1f / m_SendFps;
            m_NextSendTime = Time.unscaledTime + interval;

            var tcp = FindFirstObjectByType<TcpClient>();
            if (tcp == null || !tcp.IsConnected)
            {
                if (Time.unscaledTime >= m_NextConnectWarnTime)
                {
                    m_NextConnectWarnTime = Time.unscaledTime + 2f;
                    Debug.LogWarning(
                        "[EditorTestSource] Waiting for TcpClient → 127.0.0.1:5005 " +
                        "(start python tcp_server.py first).");
                }

                return;
            }

            Debug.Log(
                $"[EditorTestSource] Sending test image: {m_TestImage.width}x{m_TestImage.height} " +
                $"name={m_TestImage.name}");

            // Caller rate-limits; Sender encodes JPEG / PICF / TCP.
            m_Sender.TrySendTexture(m_TestImage, respectSenderRateLimit: false);
        }

        [ContextMenu("Begin Editor Test Source")]
        public void Begin()
        {
            if (m_Sender == null)
            {
                Debug.LogError("[EditorTestSource] PicoYoloFrameSender missing.");
                return;
            }

            if (m_TestImage == null)
            {
                Debug.LogError(
                    "[EditorTestSource] Test Image is null. Drag a lighter Texture2D into the Inspector.");
                return;
            }

            m_Started = true;
            m_NextSendTime = 0f;
            Debug.Log(
                $"[EditorTestSource] Started | image={m_TestImage.name} " +
                $"{m_TestImage.width}x{m_TestImage.height} fps={m_SendFps} " +
                $"sender={m_Sender.name}");
        }

        [ContextMenu("Stop Editor Test Source")]
        public void StopSending()
        {
            m_Started = false;
            Debug.Log("[EditorTestSource] Stopped.");
        }
#else
        void Awake()
        {
            // Android / player: no-op stub so the scene component is not a missing script.
            enabled = false;
        }
#endif
    }
}
