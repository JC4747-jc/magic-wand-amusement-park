using UnityEngine;

namespace Perception
{
    /// <summary>
    /// Option B glue: parents scene "Lighter" under AnchorManager's Anchor_Lighter.
    /// Does not move the anchor, does not touch MagicMR gesture code, does not
    /// change AnchorManager / DetectionManager / TCP.
    /// </summary>
    public class LighterUnderAnchorBinder : MonoBehaviour
    {
        [SerializeField]
        AnchorManager m_AnchorManager;

        [SerializeField]
        Transform m_Lighter;

        [SerializeField]
        string m_ClassName = "Lighter";

        [SerializeField]
        bool m_ForceKinematicRigidbody = true;

        bool m_Bound;
        MagicMR.RealityEditor m_RealityEditor;
        int m_RegisteredRevision = -1;
        Vector3 m_RegisteredLocalPosition;
        bool m_HasRegistration;
        StereoYoloLocator m_Stereo;

        void Awake()
        {
            if (m_AnchorManager == null)
                m_AnchorManager = FindFirstObjectByType<AnchorManager>();

            if (m_Lighter == null)
            {
                var go = GameObject.Find("Lighter");
                if (go != null)
                    m_Lighter = go.transform;
            }

            if (m_Lighter != null)
                m_RealityEditor = m_Lighter.GetComponent<MagicMR.RealityEditor>();
            m_Stereo = FindFirstObjectByType<StereoYoloLocator>();
        }

        void OnEnable()
        {
            if (m_AnchorManager != null)
                m_AnchorManager.AnchorUpdated += OnAnchorUpdated;
            else
                Debug.LogError("[LighterUnderAnchorBinder] AnchorManager not found.");

            if (m_Lighter == null)
                Debug.LogError("[LighterUnderAnchorBinder] Lighter transform not found.");
        }

        void OnDisable()
        {
            if (m_AnchorManager != null)
                m_AnchorManager.AnchorUpdated -= OnAnchorUpdated;
        }

        void OnAnchorUpdated(AnchorPoseEvent evt)
        {
            if (m_Lighter == null || evt.anchor == null)
                return;

            if (evt.className != m_ClassName)
                return;

            if (!m_Bound || m_Lighter.parent != evt.anchor)
                BindToAnchor(evt.anchor);

            var stereo = FindFirstObjectByType<StereoYoloLocator>();
            if (stereo != null && stereo.TargetLocated && m_RegisteredRevision != stereo.RegistrationRevision)
            {
                m_RealityEditor?.RegisterVisualBounds(stereo.RegisteredBottom, stereo.RegisteredHeight);
                m_RegisteredRevision = stereo.RegistrationRevision;
                m_RegisteredLocalPosition = m_Lighter.localPosition;
                m_HasRegistration = true;
            }

            // Keep RealityEditor leash origin aligned with YOLO pose (public API only).
            if (stereo == null) m_RealityEditor?.SyncWorldAnchor(m_Lighter.position);
        }

        void LateUpdate()
        {
            if (m_HasRegistration && m_Stereo != null && m_Stereo.TargetLocated && m_Lighter != null && m_Lighter.parent != null)
                m_RealityEditor?.SyncWorldAnchor(m_Lighter.parent.TransformPoint(m_RegisteredLocalPosition));
        }
        public void RefreshFollowingEffects() { LateUpdate(); }

        public bool PrepareSummon(StereoYoloLocator stereo)
        {
            if (!m_HasRegistration || m_RegisteredRevision != stereo.RegistrationRevision ||
                m_Lighter == null || m_RealityEditor == null || stereo.trackedCamera == null) return false;
            // Broad face is local +Z (the model is wider in X than in Z). Set yaw once, not a billboard.
            m_Lighter.rotation = StereoFusionGeometry.UprightFacing(stereo.InteractionPosition,
                stereo.trackedCamera.position, m_Lighter.rotation);
            m_RealityEditor.RegisterVisualBounds(stereo.CurrentBottom, stereo.RegisteredHeight);
            m_RegisteredLocalPosition = m_Lighter.localPosition;
            Debug.Log($"[SummonPose] Registered bottom={stereo.CurrentBottom:F4} height={stereo.RegisteredHeight:F4} " +
                $"yaw={m_Lighter.eulerAngles.y:F1} camera={stereo.trackedCamera.position:F4}");
            return true;
        }

        void BindToAnchor(Transform anchor)
        {
            if (!m_Lighter.gameObject.activeSelf)
                m_Lighter.gameObject.SetActive(true);

            m_Lighter.SetParent(anchor, worldPositionStays: false);
            m_Lighter.localPosition = Vector3.zero;
            m_Lighter.localRotation = Quaternion.identity;
            // Preserve authored local scale from the BridgeTest/MagicMR Lighter.

            // A previous gesture/deconstruction state may have hidden renderers.
            // RealityEditor preserves intentional deconstruction, otherwise this
            // restores the authored Virtual Lighter for fusion validation.
            m_RealityEditor?.EnsureVisible();

            if (m_ForceKinematicRigidbody)
            {
                var rb = m_Lighter.GetComponent<Rigidbody>();
                if (rb != null)
                    rb.isKinematic = true;
            }

            m_Bound = true;
            var renderers = m_Lighter.GetComponentsInChildren<Renderer>(true);
            int enabledRendererCount = 0;
            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.enabled)
                    enabledRendererCount++;
            }

            Debug.Log(
                $"[LighterUnderAnchorBinder] Bound '{m_Lighter.name}' under '{anchor.name}'. " +
                $"Hierarchy={anchor.root.name}/{anchor.name}/{m_Lighter.name} " +
                $"world={m_Lighter.position} localScale={m_Lighter.localScale} " +
                $"renderers={enabledRendererCount}/{renderers.Length} active={m_Lighter.gameObject.activeInHierarchy}",
                m_Lighter);
        }
    }
}
