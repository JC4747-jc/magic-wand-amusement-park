using UnityEngine;

namespace Perception
{
    public enum InferenceBackend
    {
        Legacy = 0,
        Remote = 1
    }

    /// <summary>
    /// Phase 3 dual-run switch: only one TCP client talks to the YOLO server at a time.
    /// Default = Legacy (PicoYoloFrameSender + TcpClient + DetectionManager).
    /// Remote = PicoCameraFrameProvider + RemoteInferenceClient (DetectionManager not rewired yet).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class InferenceBackendSwitcher : MonoBehaviour
    {
        [SerializeField]
        InferenceBackend m_Backend = InferenceBackend.Legacy;

        [Header("Legacy path")]
        [SerializeField]
        TcpClient m_LegacyTcp;

        [SerializeField]
        PicoYoloFrameSender m_LegacySender;

        [SerializeField]
        PicoYoloEditorTestSource m_EditorTestSource;

        [Header("Remote path")]
        [SerializeField]
        PicoCameraFrameProvider m_FrameProvider;

        [SerializeField]
        RemoteInferenceClient m_RemoteClient;

        public InferenceBackend Backend => m_Backend;

        void Awake()
        {
            ResolveRefs();
            ApplyBackend("Awake");
        }

        void OnValidate()
        {
            // Inspector tweaks in Edit Mode only update refs; Apply at runtime.
            if (!Application.isPlaying)
                return;
            ResolveRefs();
            ApplyBackend("OnValidate");
        }

        [ContextMenu("Apply Backend Now")]
        public void ApplyBackendFromMenu()
        {
            ResolveRefs();
            ApplyBackend("ContextMenu");
        }

        public void SetBackend(InferenceBackend backend)
        {
            m_Backend = backend;
            ApplyBackend("SetBackend");
        }

        void ResolveRefs()
        {
            if (m_LegacyTcp == null)
                m_LegacyTcp = FindFirstObjectByType<TcpClient>();
            if (m_LegacySender == null)
                m_LegacySender = FindFirstObjectByType<PicoYoloFrameSender>();
            if (m_EditorTestSource == null)
                m_EditorTestSource = FindFirstObjectByType<PicoYoloEditorTestSource>();
            if (m_FrameProvider == null)
                m_FrameProvider = FindFirstObjectByType<PicoCameraFrameProvider>();
            if (m_RemoteClient == null)
                m_RemoteClient = FindFirstObjectByType<RemoteInferenceClient>();
        }

        void ApplyBackend(string reason)
        {
            bool remote = m_Backend == InferenceBackend.Remote;

            if (m_LegacyTcp != null)
            {
                m_LegacyTcp.AutoConnect = !remote;
                if (remote)
                {
                    m_LegacyTcp.DisconnectSocket();
                    m_LegacyTcp.enabled = false;
                }
                else
                {
                    m_LegacyTcp.enabled = true;
                    if (Application.isPlaying && !m_LegacyTcp.IsConnected)
                        m_LegacyTcp.ConnectIfNeeded();
                }
            }

            if (m_LegacySender != null)
                m_LegacySender.enabled = !remote;

            // Editor test drives Legacy Sender; disable in Remote to avoid dual send attempts.
            if (m_EditorTestSource != null)
                m_EditorTestSource.enabled = !remote;

            if (m_FrameProvider != null)
                m_FrameProvider.enabled = remote;

            if (m_RemoteClient != null)
            {
                if (remote)
                {
                    m_RemoteClient.enabled = true;
                    if (Application.isPlaying)
                        m_RemoteClient.Connect();
                }
                else
                {
                    if (Application.isPlaying)
                        m_RemoteClient.Disconnect();
                    m_RemoteClient.enabled = false;
                }
            }

            Debug.Log(
                $"[InferenceBackend] {reason}: backend={m_Backend} " +
                $"(Legacy TCP/Sender={!remote}, Remote client={remote})");
        }
    }
}
