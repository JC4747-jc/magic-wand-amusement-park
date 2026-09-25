using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.PXR;

namespace Perception
{
    public enum AnchorProjectionMode
    {
        /// <summary>Legacy: Camera.main ViewportPointToRay + fixed distance.</summary>
        FixedDistance = 0,

        /// <summary>
        /// Phase 2: PICO RGB Intrinsics/Extrinsics world ray.
        /// Default placement: ray.origin + direction * DistanceMeters.
        /// Phase 3 optional: enable UsePhysicsDepth on AnchorManager for Physics.Raycast hit.
        /// Falls back to FixedDistance if ray unavailable (Editor / API fail).
        /// </summary>
        PicoCameraRay = 1,
        StereoDepth = 2
    }

    /// <summary>
    /// Maintains one Unity GameObject anchor for the locked lighter target only.
    /// Subscribes to <see cref="SingleTargetManager.LockedTargetUpdated"/> — not raw detections.
    /// Physics Depth (Phase 3) only affects anchor placement depth — not GestureTargetGate.
    /// </summary>
    public class AnchorManager : MonoBehaviour
    {
        const int PhysicsDepthHitBufferSize = 32;

        static readonly Comparison<RaycastHit> s_RaycastHitDistanceComparison =
            (a, b) => a.distance.CompareTo(b.distance);

        [SerializeField]
        SingleTargetManager m_SingleTargetManager;

        [SerializeField]
        PicoCameraRayProvider m_RayProvider;

        [SerializeField] StereoYoloLocator m_StereoLocator;

        [Header("Webcam / YOLO frame size (must match detection source)")]
        [SerializeField]
        float m_FrameWidth = 1280f;

        [SerializeField]
        float m_FrameHeight = 720f;

        [Header("Projection")]
        [SerializeField]
        AnchorProjectionMode m_ProjectionMode = AnchorProjectionMode.FixedDistance;

        [SerializeField]
        [Tooltip(
            "Fixed distance used only when Physics Depth is OFF. Mesh mode never places at this distance.")]
        float m_DistanceMeters = 2f;

        [Header("Phase 3 Depth (optional)")]
        [SerializeField]
        [Tooltip(
            "Phase 3 opt-in. TRUE = Physics.Raycast along PicoCameraRay for depth (hit.point). " +
            "FALSE = keep Phase 2 behavior (ray * DistanceMeters, default 2m). " +
            "A miss keeps the last placement or waits for the first confirmed mesh point. " +
            "Does NOT change GestureTargetGate proximity.")]
        bool m_UsePhysicsDepth = false;

        [SerializeField]
        [Tooltip("Max Physics.Raycast distance when Use Physics Depth is enabled.")]
        float m_MaxDepthMeters = 8f;

        [SerializeField]
        [Tooltip("Layers included in Phase 3 depth Raycast. Triggers are ignored.")]
        LayerMask m_DepthLayers = ~0;

        [SerializeField]
        [Tooltip("Higher = snappier. Applied in Update via exponential Lerp.")]
        float m_SmoothSpeed = 8f;

        [Header("Spatial Mesh Stability")]
        [SerializeField]
        [Tooltip("Maximum accepted movement between consecutive stable mesh points.")]
        float m_MaxMeshJumpMeters = 0.15f;

        [SerializeField]
        [Tooltip("Consistent frames required before accepting a new distant mesh surface.")]
        int m_MeshJumpConfirmFrames = 5;

        [SerializeField]
        [Tooltip("New surface candidates must cluster within this radius.")]
        float m_MeshCandidateRadiusMeters = 0.08f;

        [SerializeField]
        float m_MeshConfirmSeconds = 0.15f;

        [SerializeField]
        string m_AnchorClassName = "Lighter";

        Transform m_Anchor;
        Transform m_CachedLighter;
        Vector3 m_TargetPosition;
        bool m_HasTarget;
        float m_LastConfidence;
        long m_LastTimestamp;
        string m_LastProjectionPath = "none";

        bool m_HasStableMeshPoint;
        Vector3 m_StableMeshPoint;
        bool m_HasPendingMeshPoint;
        Vector3 m_PendingMeshPoint;
        int m_PendingMeshFrames;
        float m_PendingMeshStartTime;
        float m_LastPendingMeshTime;
        long m_LastProcessedFrameTimestamp;

        // Phase 3 log throttle: log on path/result change, or at most ~1 Hz while stable.
        string m_LastPhase3LogKey = string.Empty;
        float m_NextPhase3LogTime;
        bool m_LoggedPhysicsDepthEnabled;
        readonly RaycastHit[] m_PhysicsDepthHitBuffer = new RaycastHit[PhysicsDepthHitBufferSize];

        /// <summary>
        /// Fired after an anchor target pose is updated from the locked target.
        /// </summary>
        public event Action<AnchorPoseEvent> AnchorUpdated;

        public Transform LighterAnchor => m_Anchor;
        public float FrameWidth => m_FrameWidth;
        public float FrameHeight => m_FrameHeight;
        public AnchorProjectionMode ProjectionMode => m_ProjectionMode;
        public bool UsePhysicsDepth => m_UsePhysicsDepth;
        public float DistanceMeters => m_DistanceMeters;
        public string LastProjectionPath => m_LastProjectionPath;

        public void ResetPlacement()
        {
            m_HandDriven = false;
            m_HasTarget = false;
            m_LastProcessedFrameTimestamp = 0;
            m_HasStableMeshPoint = false;
            ClearPendingMeshPoint();
        }

        public IReadOnlyDictionary<string, Transform> Anchors
        {
            get
            {
                var dict = new Dictionary<string, Transform>(1);
                if (m_Anchor != null)
                    dict[m_AnchorClassName] = m_Anchor;
                return dict;
            }
        }

        /// <summary>
        /// Sync projection frame size to the PICO Camera / JPEG pixel space.
        /// </summary>
        public void SetFrameSize(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return;
            if (Mathf.Approximately(m_FrameWidth, width) && Mathf.Approximately(m_FrameHeight, height))
                return;
            m_FrameWidth = width;
            m_FrameHeight = height;
            Debug.Log(
                $"[AnchorManager] Frame size synced to detection image {width}x{height} " +
                $"(mode={m_ProjectionMode}, distance={m_DistanceMeters}m, " +
                $"physicsDepth={m_UsePhysicsDepth}).");
        }

        void Awake()
        {
            if (m_SingleTargetManager == null)
                m_SingleTargetManager = FindFirstObjectByType<SingleTargetManager>();
            if (m_RayProvider == null)
                m_RayProvider = FindFirstObjectByType<PicoCameraRayProvider>();

            var lighterGo = GameObject.Find("Lighter");
            if (lighterGo != null)
                m_CachedLighter = lighterGo.transform;
        }

        void OnEnable()
        {
            if (m_SingleTargetManager != null)
            {
                m_SingleTargetManager.LockedTargetUpdated += OnLockedTargetUpdated;
                m_SingleTargetManager.StateChanged += OnTargetStateChanged;
                Debug.Log(
                    "[AnchorManager] Subscribed to SingleTargetManager.LockedTargetUpdated " +
                    $"(manager={m_SingleTargetManager.name} mode={m_ProjectionMode} " +
                    $"physicsDepth={m_UsePhysicsDepth}).");
            }
            else
            {
                Debug.LogError(
                    "[AnchorManager] No SingleTargetManager found. " +
                    "Assign it in the Inspector or place one in the scene.");
            }

            if (m_UsePhysicsDepth && !m_LoggedPhysicsDepthEnabled)
            {
                m_LoggedPhysicsDepthEnabled = true;
                Debug.Log(
                    $"[Phase3] PhysicsDepth enabled " +
                    $"(max={m_MaxDepthMeters:0.##}m layers=0x{m_DepthLayers.value:X} " +
                    "miss=HoldOrWait; source=XRCamera; meshOnly=true). " +
                    "Independent of GestureTargetGate proximity.");
            }
        }

        void OnDisable()
        {
            if (m_SingleTargetManager != null)
            {
                m_SingleTargetManager.LockedTargetUpdated -= OnLockedTargetUpdated;
                m_SingleTargetManager.StateChanged -= OnTargetStateChanged;
                Debug.Log("[AnchorManager] Unsubscribed from SingleTargetManager.LockedTargetUpdated.");
            }
        }

        public Vector3 PlacementPosition => m_Anchor != null ? m_Anchor.position : m_TargetPosition;
        public void HoldPlacement() { if (m_Anchor != null) m_TargetPosition = m_Anchor.position; }
        bool m_HandDriven;
        public void SetHandPlacement(Vector3 position)
        { if (m_HasTarget) { m_TargetPosition = position; m_HandDriven = true; } }
        public void EndHandPlacement() { m_HandDriven = false; HoldPlacement(); }
        public void RenderHandPlacement(Vector3 position)
        { if(m_HandDriven && m_HasTarget && m_Anchor!=null) m_Anchor.position=position; }

        void Update()
        {
            if (!m_HasTarget || m_Anchor == null)
                return;

            float speed=m_SmoothSpeed;
            if(m_ProjectionMode==AnchorProjectionMode.StereoDepth)
                speed+=Mathf.Min(50f,Vector3.Distance(m_Anchor.position,m_TargetPosition)*500f);
            // VisualAverage already filters independent camera measurements; do not add a second lag.
            bool averagedVision = m_ProjectionMode == AnchorProjectionMode.StereoDepth &&
                m_StereoLocator != null && m_StereoLocator.UsesVisualAverage;
            float t = m_HandDriven || averagedVision ? 1f : 1f - Mathf.Exp(-speed * Time.deltaTime);
            m_Anchor.position = Vector3.Lerp(m_Anchor.position, m_TargetPosition, t);
        }

        void OnLockedTargetUpdated(LockedTargetEvent evt)
        {
            if (!evt.hasTarget)
                return;

            if (evt.state != TargetLockState.TARGET_LOCKED)
            {
                ClearPendingMeshPoint();
                return;
            }

            // Repeated notifications for the same image are not independent evidence.
            if (evt.timestamp > 0 && evt.timestamp == m_LastProcessedFrameTimestamp)
                return;
            m_LastProcessedFrameTimestamp = evt.timestamp;

            ObjectDetectionEvent det = evt.detection;
            if (string.IsNullOrEmpty(det.className))
                return;

            if (m_FrameWidth <= 0f || m_FrameHeight <= 0f)
            {
                Debug.LogWarning("[AnchorManager] Invalid frame width/height.");
                return;
            }

            if (!TryProjectToWorld(det, out Vector3 worldPosition, out string path))
            {
                ClearPendingMeshPoint();
                m_LastProjectionPath = m_ProjectionMode == AnchorProjectionMode.StereoDepth
                    ? path : path + (m_HasStableMeshPoint ? "+StableMeshHold" : "+WaitingForMesh");
                LogPhase3Throttled("hold:" + path, $"[Phase3] {m_LastProjectionPath}");
                return;
            }

            if (!TryStabilizeMeshProjection(ref worldPosition, ref path))
                return;

            m_LastProjectionPath = path;
            Transform anchor = GetOrCreateAnchor();
            m_TargetPosition = worldPosition;
            m_LastConfidence = det.confidence;
            m_LastTimestamp = det.timestamp;

            if (!m_HasTarget || (anchor.position - worldPosition).sqrMagnitude > 100f ||
                !anchor.gameObject.activeInHierarchy)
            {
                anchor.position = worldPosition;
            }
            m_HasTarget = true;

            Debug.Log(
                $"[AnchorManager] LOCKED class={det.className} state={evt.state} path={path} " +
                $"pixel=({det.centerX:F1},{det.centerY:F1}) " +
                $"bbox=({det.x1:F0},{det.y1:F0})-({det.x2:F0},{det.y2:F0}) " +
                $"match={evt.matchScore:F2} target={worldPosition}");

            AnchorUpdated?.Invoke(new AnchorPoseEvent(
                className: m_AnchorClassName,
                confidence: det.confidence,
                worldPosition: worldPosition,
                anchor: anchor,
                timestamp: det.timestamp));
        }

        bool TryProjectToWorld(ObjectDetectionEvent det, out Vector3 worldPosition, out string path)
        {
            worldPosition = default;
            path = "none";

            if (m_ProjectionMode == AnchorProjectionMode.StereoDepth)
            {
                path = "StereoUnavailable";
                return m_StereoLocator != null && m_StereoLocator.TryLocate(det, out worldPosition, out path);
            }

            if (m_ProjectionMode == AnchorProjectionMode.PicoCameraRay &&
                m_RayProvider != null &&
                m_RayProvider.TryGetWorldRay(
                    det.centerX,
                    det.centerY,
                    Mathf.RoundToInt(m_FrameWidth),
                    Mathf.RoundToInt(m_FrameHeight),
                    out Ray picoRay))
            {
                Vector3 origin = picoRay.origin;
                Vector3 direction = picoRay.direction.normalized;

                // Phase 3 opt-in only. false → identical to Phase 2 (ray * DistanceMeters).
                if (m_UsePhysicsDepth)
                {
                    // Prefer the ray whose world pose is the tracked XR camera used to
                    // view VST. Device testing showed it remains on the visible table,
                    // while the ambiguous SDK-extrinsics frame can hit unrelated blocks.
                    if (m_RayProvider.HasLastXrCameraRay)
                    {
                        Ray xrCameraRay = m_RayProvider.LastXrCameraRay;
                        if (TryPhysicsDepthHit(
                            xrCameraRay.origin,
                            xrCameraRay.direction.normalized,
                            out RaycastHit xrCameraHit))
                        {
                            worldPosition = xrCameraHit.point;
                            path =
                                $"PicoCameraRay+XRCameraDepth hit={xrCameraHit.distance:0.##}m " +
                                $"collider={xrCameraHit.collider.name}";
                            LogPhase3Throttled(
                                $"xr-hit:{xrCameraHit.collider.name}:{xrCameraHit.distance:F2}",
                                $"[Phase3] XRCameraDepth hit distance={xrCameraHit.distance:F3} " +
                                $"point={xrCameraHit.point} collider={xrCameraHit.collider.name}");
                            return true;
                        }
                    }

                    // Do not switch coordinate systems or invent a depth on failure.
                    path = m_RayProvider.HasLastXrCameraRay ? "XRCameraDepthMiss" : "XRCameraRayUnavailable";
                    return false;
                }

                worldPosition = origin + direction * m_DistanceMeters;
                path = $"PicoCameraRay+{m_DistanceMeters:0.##}m";
                return true;
            }

            if (m_UsePhysicsDepth)
            {
                path = "CameraRayUnavailable";
                return false;
            }
            return TryFixedDistance(det, out worldPosition, out path);
        }

        void OnTargetStateChanged()
        {
            if (m_SingleTargetManager.State == TargetLockState.TARGET_LOCKED)
                return;
            ClearPendingMeshPoint();
            m_LastProjectionPath = m_HasStableMeshPoint ? "TargetNotLocked+StableMeshHold" : "WaitingForLockedTarget";
            // Keep the accepted world position. Reacquisition must confirm a new point.
        }

        bool TryStabilizeMeshProjection(ref Vector3 worldPosition, ref string path)
        {
            if (!m_UsePhysicsDepth || m_ProjectionMode == AnchorProjectionMode.StereoDepth)
                return true;

            bool isMeshHit =
                path.Contains(" hit=") &&
                path.Contains("collider=Mesh");

            if (!isMeshHit)
            {
                ClearPendingMeshPoint();
                return false;
            }

            float maxJump = Mathf.Max(0.01f, m_MaxMeshJumpMeters);
            int requiredFrames = Mathf.Max(1, m_MeshJumpConfirmFrames);

            if (!m_HasStableMeshPoint)
            {
                if (!AccumulatePendingMeshPoint(worldPosition, m_MeshCandidateRadiusMeters, requiredFrames))
                    return false;

                AcceptStableMeshPoint(m_PendingMeshPoint);
                worldPosition = m_StableMeshPoint;
                path += "+StableConfirmed";
                return true;
            }

            if (Vector3.Distance(worldPosition, m_StableMeshPoint) <= maxJump)
            {
                m_StableMeshPoint = Vector3.Lerp(m_StableMeshPoint, worldPosition, 0.35f);
                ClearPendingMeshPoint();
                worldPosition = m_StableMeshPoint;
                path += "+Stable";
                return true;
            }

            if (AccumulatePendingMeshPoint(worldPosition, m_MeshCandidateRadiusMeters, requiredFrames))
            {
                AcceptStableMeshPoint(m_PendingMeshPoint);
                worldPosition = m_StableMeshPoint;
                path += "+JumpConfirmed";
                return true;
            }

            worldPosition = m_StableMeshPoint;
            path += "+JumpRejectedHold";
            LogPhase3Throttled(
                "mesh-jump-rejected",
                $"[Phase3] Mesh jump rejected; holding stable point={m_StableMeshPoint} " +
                $"candidate={m_PendingMeshPoint} frames={m_PendingMeshFrames}/{requiredFrames}");
            return true;
        }

        bool AccumulatePendingMeshPoint(Vector3 candidate, float maxJump, int requiredFrames)
        {
            float now = Time.unscaledTime;
            if (!m_HasPendingMeshPoint || now - m_LastPendingMeshTime > 0.25f ||
                Vector3.Distance(candidate, m_PendingMeshPoint) > Mathf.Max(0.01f, maxJump))
            {
                m_HasPendingMeshPoint = true;
                m_PendingMeshPoint = candidate;
                m_PendingMeshFrames = 1;
                m_PendingMeshStartTime = now;
            }
            else
            {
                m_PendingMeshFrames++;
                float blend = 1f / m_PendingMeshFrames;
                m_PendingMeshPoint = Vector3.Lerp(m_PendingMeshPoint, candidate, blend);
            }

            m_LastPendingMeshTime = now;
            return m_PendingMeshFrames >= requiredFrames &&
                now - m_PendingMeshStartTime >= Mathf.Max(0f, m_MeshConfirmSeconds);
        }

        void AcceptStableMeshPoint(Vector3 point)
        {
            m_HasStableMeshPoint = true;
            m_StableMeshPoint = point;
            ClearPendingMeshPoint();
        }

        void ClearPendingMeshPoint()
        {
            m_HasPendingMeshPoint = false;
            m_PendingMeshFrames = 0;
        }

        /// <summary>
        /// World-space Raycast along PicoCameraRay. Skips virtual Lighter / Anchor colliders
        /// so the depth probe does not stick to the object we are placing.
        /// </summary>
        bool TryPhysicsDepthHit(Vector3 origin, Vector3 direction, out RaycastHit bestHit)
        {
            bestHit = default;
            float maxDist = Mathf.Max(0.01f, m_MaxDepthMeters);
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                m_PhysicsDepthHitBuffer,
                maxDist,
                m_DepthLayers,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
                return false;

            if (hitCount > 1)
            {
                Array.Sort(
                    m_PhysicsDepthHitBuffer,
                    0,
                    hitCount,
                    Comparer<RaycastHit>.Create(s_RaycastHitDistanceComparison));
            }

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = m_PhysicsDepthHitBuffer[i];
                if (hit.collider == null)
                    continue;
                if (IsSelfPlacementCollider(hit.collider))
                    continue;
                // Accept only colliders owned by the official Spatial Mesh manager.
                if (!(hit.collider is MeshCollider) ||
                    hit.collider.GetComponentInParent<PXR_SpatialMeshManager>() == null)
                    continue;

                bestHit = hit;
                return true;
            }

            return false;
        }

        bool IsSelfPlacementCollider(Collider col)
        {
            Transform t = col.transform;
            if (m_Anchor != null && (t == m_Anchor || t.IsChildOf(m_Anchor)))
                return true;

            // Scene virtual lighter (and children) must not be depth surfaces.
            if (m_CachedLighter == null)
            {
                var lighterGo = GameObject.Find("Lighter");
                if (lighterGo != null)
                    m_CachedLighter = lighterGo.transform;
            }

            if (m_CachedLighter != null && (t == m_CachedLighter || t.IsChildOf(m_CachedLighter)))
                return true;

            return false;
        }

        void LogPhase3Throttled(string key, string message)
        {
            float now = Time.unscaledTime;
            if (key == m_LastPhase3LogKey && now < m_NextPhase3LogTime)
                return;

            m_LastPhase3LogKey = key;
            m_NextPhase3LogTime = now + 1f;
            Debug.Log(message);
        }

        bool TryFixedDistance(ObjectDetectionEvent det, out Vector3 worldPosition, out string path)
        {
            worldPosition = default;
            path = "FixedDistance";

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[AnchorManager] Main Camera not found (FixedDistance).");
                return false;
            }

            float viewportX = det.centerX / m_FrameWidth;
            float viewportY = 1f - (det.centerY / m_FrameHeight);
            Ray ray = cam.ViewportPointToRay(new Vector3(viewportX, viewportY, 0f));
            worldPosition = ray.origin + ray.direction.normalized * m_DistanceMeters;

            if (m_ProjectionMode == AnchorProjectionMode.PicoCameraRay)
                path = "FixedDistance(fallback)";

            return true;
        }

        Transform GetOrCreateAnchor()
        {
            if (m_Anchor != null)
                return m_Anchor;

            var go = new GameObject($"Anchor_{m_AnchorClassName}");
            go.transform.SetParent(transform, worldPositionStays: true);
            m_Anchor = go.transform;
            Debug.Log($"[AnchorManager] Created {go.name}");
            DumpActiveSceneHierarchyDiagnostic(go.name);
            return m_Anchor;
        }

        static void DumpActiveSceneHierarchyDiagnostic(string expectedAnchorName)
        {
            Scene scene = SceneManager.GetActiveScene();
            var sb = new StringBuilder(2048);
            sb.AppendLine($"[AnchorManager] DIAG Scene hierarchy dump (scene='{scene.name}')");
            GameObject[] roots = scene.GetRootGameObjects();
            GameObject found = null;
            foreach (GameObject root in roots)
                AppendHierarchyRecursive(sb, root.transform, root.name, expectedAnchorName, ref found);

            if (found != null)
                sb.AppendLine($"  EXISTS: '{expectedAnchorName}' path={GetTransformPath(found.transform)}");
            else
                sb.AppendLine($"  MISSING: '{expectedAnchorName}'");

            Debug.Log(sb.ToString());
        }

        static void AppendHierarchyRecursive(
            StringBuilder sb, Transform t, string path, string expectedAnchorName, ref GameObject found)
        {
            sb.AppendLine($"  {path}");
            if (found == null && t.name == expectedAnchorName)
                found = t.gameObject;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform child = t.GetChild(i);
                AppendHierarchyRecursive(sb, child, path + "/" + child.name, expectedAnchorName, ref found);
            }
        }

        static string GetTransformPath(Transform t)
        {
            var parts = new List<string>();
            Transform cur = t;
            while (cur != null)
            {
                parts.Add(cur.name);
                cur = cur.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
