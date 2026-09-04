using System;
using UnityEngine;

namespace Perception
{
    /// <summary>
    /// Phase 3: converts ObjectDetectionEvent (pixel centers) into a world pose.
    /// Publishes SpatialDetectionReceived. Does not create GameObjects.
    /// </summary>
    public class SpatialBinder : MonoBehaviour
    {
        [SerializeField]
        DetectionManager m_DetectionManager;

        [Header("Webcam frame size (must match Python capture)")]
        [SerializeField]
        float m_FrameWidth = 1280f;

        [SerializeField]
        float m_FrameHeight = 720f;

        [Header("Projection")]
        [SerializeField]
        float m_DistanceMeters = 2f;

        /// <summary>
        /// Raised after a detection is projected to world space.
        /// Subscribe here for spatial consumers (not TCP / not MagicMR yet).
        /// </summary>
        public event Action<SpatialDetectionEvent> SpatialDetectionReceived;

        void Awake()
        {
            if (m_DetectionManager == null)
                m_DetectionManager = FindFirstObjectByType<DetectionManager>();
        }

        void OnEnable()
        {
            if (m_DetectionManager != null)
            {
                m_DetectionManager.DetectionReceived += OnDetectionReceived;
                Debug.Log(
                    "[SpatialBinder] Subscribed to DetectionManager.DetectionReceived " +
                    $"(manager={m_DetectionManager.name}).");
            }
            else
            {
                Debug.LogError(
                    "[SpatialBinder] No DetectionManager found. " +
                    "Assign it in the Inspector or place one in the scene.");
            }
        }

        void OnDisable()
        {
            if (m_DetectionManager != null)
            {
                m_DetectionManager.DetectionReceived -= OnDetectionReceived;
                Debug.Log("[SpatialBinder] Unsubscribed from DetectionManager.DetectionReceived.");
            }
        }

        void OnDetectionReceived(ObjectDetectionEvent evt)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[SpatialBinder] Main Camera not found.");
                return;
            }

            if (m_FrameWidth <= 0f || m_FrameHeight <= 0f)
            {
                Debug.LogWarning("[SpatialBinder] Invalid frame width/height.");
                return;
            }

            // Webcam / YOLO: origin top-left. Unity viewport: origin bottom-left.
            float viewportX = evt.centerX / m_FrameWidth;
            float viewportY = 1f - (evt.centerY / m_FrameHeight);
            Vector3 viewport = new Vector3(viewportX, viewportY, 0f);

            Ray ray = cam.ViewportPointToRay(viewport);
            Vector3 direction = ray.direction.normalized;
            Vector3 worldPosition = ray.origin + direction * m_DistanceMeters;

            var spatial = new SpatialDetectionEvent(
                className: evt.className,
                confidence: evt.confidence,
                worldPosition: worldPosition,
                worldDirection: direction,
                timestamp: evt.timestamp);

            Debug.Log(
                $"[SpatialBinder] pixel=({evt.centerX:F1},{evt.centerY:F1}) " +
                $"viewport=({viewportX:F3},{viewportY:F3}) " +
                $"worldPos={worldPosition} worldDir={direction} " +
                $"class={spatial.className} conf={spatial.confidence:F2} t={spatial.timestamp}");

            SpatialDetectionReceived?.Invoke(spatial);
        }
    }
}
