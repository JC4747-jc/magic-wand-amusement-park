using UnityEngine;
using MagicMR;

namespace Perception
{
    /// <summary>
    /// Track B join mode B for MagicMR: PICO JPEG → TCP YOLO26n → one desk pin.
    /// Does not write the box to the lighter every frame, does not fire 4D effects,
    /// and does not unpin when the lighter leaves the image.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class MagicMRYoloAutoPin : MonoBehaviour
    {
        [SerializeField]
        DetectionManager m_DetectionManager;

        [SerializeField]
        PicoCameraRayProvider m_RayProvider;

        [SerializeField]
        PicoYoloFrameSender m_Sender;

        LighterAnchorManager m_Anchor;
        CalibrationRitual m_Ritual;
        int m_StableHits;
        bool m_LoggedSkip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void SpawnForMagicMR()
        {
            if (!StudySpec.YoloAutoPinEnabled)
                return;
            if (FindFirstObjectByType<MagicMRStudyBootstrap>() == null)
                return;
            if (FindFirstObjectByType<MagicMRYoloAutoPin>() != null)
                return;

            var go = new GameObject("MagicMRYoloAutoPin");
            go.AddComponent<MagicMRYoloAutoPin>();
        }

        void Awake()
        {
            if (!StudySpec.YoloAutoPinEnabled)
            {
                enabled = false;
                return;
            }

            EnsureBridgePipeline();
            m_Anchor = FindFirstObjectByType<LighterAnchorManager>();
            m_Ritual = FindFirstObjectByType<CalibrationRitual>();
        }

        void OnEnable()
        {
            if (m_DetectionManager != null)
                m_DetectionManager.DetectionFrameReceived += OnDetectionFrame;
        }

        void OnDisable()
        {
            if (m_DetectionManager != null)
                m_DetectionManager.DetectionFrameReceived -= OnDetectionFrame;
        }

        void EnsureBridgePipeline()
        {
            var capture = GetComponent<PicoCameraCapture>() ?? gameObject.AddComponent<PicoCameraCapture>();
            var tcp = GetComponent<TcpClient>() ?? gameObject.AddComponent<TcpClient>();
            m_Sender = GetComponent<PicoYoloFrameSender>() ?? gameObject.AddComponent<PicoYoloFrameSender>();
            m_DetectionManager = GetComponent<DetectionManager>() ?? gameObject.AddComponent<DetectionManager>();
            m_RayProvider = GetComponent<PicoCameraRayProvider>() ?? gameObject.AddComponent<PicoCameraRayProvider>();

            var host = PlayerPrefs.GetString(StudySpec.YoloHostPlayerPrefsKey, StudySpec.YoloBridgeHost);
            tcp.Configure(host, StudySpec.YoloBridgePort, autoConnect: true);
            m_Sender.SetVerboseLogging(false);
            m_DetectionManager.SetMinConfidence(StudySpec.YoloAutoPinMinConfidence);

            Debug.Log(
                $"[MagicMR] YOLO auto-pin bridge ready → {host}:{StudySpec.YoloBridgePort} " +
                $"(capture={capture != null}). Use `adb reverse tcp:{StudySpec.YoloBridgePort} tcp:{StudySpec.YoloBridgePort}` " +
                "when the PC server is on this USB host.",
                this);
        }

        void OnDetectionFrame(DetectionFrame frame)
        {
            var fsm = MRGestureController.Instance;
            if (fsm == null)
                return;

            if (m_Anchor == null)
                m_Anchor = FindFirstObjectByType<LighterAnchorManager>();

            // Already pinned: ignore further boxes and misses. Never unpin.
            if ((m_Anchor != null && m_Anchor.IsCalibrated) ||
                fsm.State == MRState.PinnedIdle || fsm.State == MRState.FollowingHand)
            {
                m_StableHits = 0;
                return;
            }

            if (!TryPickLighter(frame, out var det))
            {
                m_StableHits = 0;
                return;
            }

            if (!TryUnprojectToDesk(det, frame, out var worldAim))
            {
                m_StableHits = 0;
                if (!m_LoggedSkip)
                {
                    m_LoggedSkip = true;
                    Debug.LogWarning("[MagicMR] YOLO box received but desk unproject failed.", this);
                }

                return;
            }

            m_StableHits++;
            if (m_StableHits < StudySpec.YoloAutoPinStableFrames)
                return;

            if (fsm.TryAcceptYoloAutoPin(worldAim))
            {
                Debug.Log(
                    $"[MagicMR] YOLO auto-pin proposed at {worldAim} " +
                    $"conf={det.confidence:F2} box=({det.centerX:F0},{det.centerY:F0}) " +
                    $"frame={frame.frameId} {frame.width}x{frame.height}",
                    this);
            }

            m_StableHits = 0;
        }

        static bool TryPickLighter(DetectionFrame frame, out ObjectDetectionEvent best)
        {
            best = default;
            var found = false;
            var bestConf = -1f;

            var detections = frame.detections;
            for (var i = 0; i < detections.Count; i++)
            {
                var det = detections[i];
                if (!IsLighterClass(det.className) ||
                    det.confidence < StudySpec.YoloAutoPinMinConfidence ||
                    det.confidence <= bestConf)
                    continue;

                bestConf = det.confidence;
                best = det;
                found = true;
            }

            return found;
        }

        static bool IsLighterClass(string className)
        {
            if (string.IsNullOrEmpty(className))
                return false;
            return className.Equals(StudySpec.YoloLighterClass, System.StringComparison.OrdinalIgnoreCase)
                   || className.Equals("lighter", System.StringComparison.OrdinalIgnoreCase);
        }

        bool TryUnprojectToDesk(ObjectDetectionEvent det, DetectionFrame frame, out Vector3 worldAim)
        {
            worldAim = default;
            var width = frame.width > 0 ? frame.width : (m_Sender != null ? m_Sender.LastSentWidth : 0);
            var height = frame.height > 0 ? frame.height : (m_Sender != null ? m_Sender.LastSentHeight : 0);
            if (width <= 0 || height <= 0)
                return false;

            Ray ray;
            if (m_RayProvider != null &&
                m_RayProvider.TryGetWorldRay(det.centerX, det.centerY, width, height, out ray))
            {
                // RGB JPEG → PICO camera ray, then table plane (CalibrationRitual idea).
            }
            else
            {
                var cam = Camera.main;
                if (cam == null)
                    return false;

                // JPEG origin top-left; Unity viewport origin bottom-left.
                var viewport = new Vector3(det.centerX / width, 1f - det.centerY / height, 0f);
                ray = cam.ViewportPointToRay(viewport);
            }

            var deskY = ResolveDeskHeight(ray.origin.y);
            if (!TryIntersectHorizontalPlane(ray, deskY, out worldAim))
            {
                worldAim = ray.origin + ray.direction.normalized * StudySpec.LighterDistance;
                worldAim.y = deskY;
            }

            return true;
        }

        float ResolveDeskHeight(float fallbackY)
        {
            if (m_Anchor == null)
                m_Anchor = FindFirstObjectByType<LighterAnchorManager>();
            if (m_Anchor != null && Mathf.Abs(m_Anchor.DeskPlaneY) > 0.001f)
                return m_Anchor.DeskPlaneY;

            if (m_Ritual == null)
                m_Ritual = FindFirstObjectByType<CalibrationRitual>();
            if (m_Ritual != null && m_Ritual.TryGetWireframePosition(out var ghost))
                return ghost.y;

            var cam = Camera.main;
            if (cam != null)
                return cam.transform.position.y + StudySpec.LighterHeightOffset;

            return fallbackY;
        }

        static bool TryIntersectHorizontalPlane(Ray ray, float y, out Vector3 hit)
        {
            hit = default;
            if (Mathf.Abs(ray.direction.y) < 1e-5f)
                return false;

            var t = (y - ray.origin.y) / ray.direction.y;
            if (t <= 0.05f)
                return false;

            hit = ray.origin + ray.direction * t;
            return true;
        }
    }
}
