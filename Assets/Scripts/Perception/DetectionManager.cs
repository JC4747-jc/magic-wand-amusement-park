using System;
using System.Collections.Generic;
using UnityEngine;

namespace Perception
{
    /// <summary>
    /// Converts TCP JSON lines into detection frames / events.
    /// Supports frame payloads with optional frame_id / width / height (PICO Camera Phase 1).
    /// </summary>
    public class DetectionManager : MonoBehaviour
    {
        [SerializeField]
        TcpClient m_TcpClient;

        [SerializeField]
        float m_MinConfidence = 0f;

        [SerializeField]
        SingleTargetManager m_SingleTargetManager;

        [SerializeField]
        AnchorManager m_AnchorManager;

        [SerializeField]
        bool m_LogEveryFrame;

        /// <summary>Raised once per YOLO/camera frame (preferred for multi-target lock).</summary>
        public event Action<DetectionFrame> DetectionFrameReceived;

        /// <summary>
        /// Raised for each valid detection (legacy / debug). Prefer DetectionFrameReceived.
        /// </summary>
        public event Action<ObjectDetectionEvent> DetectionReceived;

        public long LastFrameId { get; private set; }
        public int LastFrameWidth { get; private set; }
        public int LastFrameHeight { get; private set; }

        public void SetMinConfidence(float value) => m_MinConfidence = Mathf.Max(0f, value);

        void Awake()
        {
            if (m_TcpClient == null)
                m_TcpClient = GetComponent<TcpClient>();
            if (m_SingleTargetManager == null)
                m_SingleTargetManager = FindFirstObjectByType<SingleTargetManager>();
            if (m_AnchorManager == null)
                m_AnchorManager = FindFirstObjectByType<AnchorManager>();
        }

        void OnEnable()
        {
            if (m_TcpClient != null)
                m_TcpClient.LineReceived += OnJsonLineReceived;
        }

        void OnDisable()
        {
            if (m_TcpClient != null)
                m_TcpClient.LineReceived -= OnJsonLineReceived;
        }

        void OnJsonLineReceived(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long frameId = 0;
            int width = 0;
            int height = 0;
            var events = new List<ObjectDetectionEvent>(8);

            if (json.IndexOf("\"detections\"", StringComparison.Ordinal) >= 0)
            {
                DetectionFrameMessage frameDto = JsonUtility.FromJson<DetectionFrameMessage>(json);
                if (frameDto != null)
                {
                    frameId = frameDto.frame_id;
                    if (frameDto.timestamp != 0)
                        timestamp = frameDto.timestamp;
                    width = frameDto.width;
                    height = frameDto.height;

                    if (frameDto.detections != null)
                    {
                        foreach (DetectionMessage dto in frameDto.detections)
                            TryAdd(dto, timestamp, events);
                    }
                }
            }
            else
            {
                DetectionMessage dto = JsonUtility.FromJson<DetectionMessage>(json);
                TryAdd(dto, timestamp, events);
            }

            if (width > 0 && height > 0)
            {
                LastFrameWidth = width;
                LastFrameHeight = height;
                m_SingleTargetManager?.SetFrameSize(width, height);
                m_AnchorManager?.SetFrameSize(width, height);
            }

            LastFrameId = frameId;
            var frame = new DetectionFrame(events, timestamp, frameId, width, height);
            DetectionFrameReceived?.Invoke(frame);

            if (m_LogEveryFrame)
            {
                Debug.Log(
                    $"[DetectionManager] detection received frame_id={frameId} " +
                    $"count={events.Count} {width}x{height}");
            }

            for (int i = 0; i < events.Count; i++)
            {
                ObjectDetectionEvent evt = events[i];
                if (m_LogEveryFrame)
                {
                    Debug.Log(
                        $"[DetectionManager] frame_id={frameId} {width}x{height} " +
                        $"{evt.className} conf={evt.confidence:F2} " +
                        $"center=({evt.centerX:F0},{evt.centerY:F0}) " +
                        $"bbox=({evt.x1:F0},{evt.y1:F0})-({evt.x2:F0},{evt.y2:F0}) t={evt.timestamp}");
                }

                DetectionReceived?.Invoke(evt);
            }
        }

        void TryAdd(DetectionMessage dto, long timestamp, List<ObjectDetectionEvent> events)
        {
            if (dto == null)
                return;

            if (string.IsNullOrEmpty(dto.@class))
            {
                Debug.LogWarning("[DetectionManager] Ignored detection with empty class.");
                return;
            }

            if (dto.confidence < m_MinConfidence)
                return;

            float x1 = dto.x1;
            float y1 = dto.y1;
            float x2 = dto.x2;
            float y2 = dto.y2;

            if (!(x2 > x1 && y2 > y1))
            {
                const float half = 8f;
                x1 = dto.centerX - half;
                y1 = dto.centerY - half;
                x2 = dto.centerX + half;
                y2 = dto.centerY + half;
            }

            events.Add(new ObjectDetectionEvent(
                className: dto.@class,
                confidence: dto.confidence,
                centerX: dto.centerX,
                centerY: dto.centerY,
                timestamp: timestamp,
                x1: x1,
                y1: y1,
                x2: x2,
                y2: y2));
        }
    }
}
