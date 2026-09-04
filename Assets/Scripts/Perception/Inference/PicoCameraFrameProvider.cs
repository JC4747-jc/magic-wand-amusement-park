using System;
using UnityEngine;

namespace Perception
{
    /// <summary>
    /// Adapter: PicoCameraCapture.FrameUpdated → IFrameProvider.FrameAvailable.
    /// Assigns monotonic FrameId (Capture itself does not own inference frame ids today;
    /// RemoteInferenceClient / Sender still owns PICF frame_id at send time in Phase 3).
    /// </summary>
    public class PicoCameraFrameProvider : MonoBehaviour, IFrameProvider
    {
        [SerializeField]
        PicoCameraCapture m_Capture;

        uint m_NextFrameId = 1;
        CameraFrame m_Latest;
        bool m_HasFrame;

        public event Action<CameraFrame> FrameAvailable;

        public bool HasFrame => m_HasFrame;
        public CameraFrame LatestFrame => m_Latest;

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
                m_Capture.FrameUpdated += OnCaptureFrame;
        }

        void OnDisable()
        {
            if (m_Capture != null)
                m_Capture.FrameUpdated -= OnCaptureFrame;
        }

        void OnCaptureFrame(Texture2D texture)
        {
            if (texture == null)
                return;

            long ts = m_Capture != null && m_Capture.LastCaptureTimestamp != 0
                ? m_Capture.LastCaptureTimestamp
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            m_Latest = new CameraFrame(
                m_NextFrameId++,
                ts,
                texture.width,
                texture.height,
                texture);
            m_HasFrame = true;
            FrameAvailable?.Invoke(m_Latest);
        }
    }
}
