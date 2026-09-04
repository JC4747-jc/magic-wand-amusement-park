using System.Collections.Generic;
using UnityEngine;

namespace Perception
{
    /// <summary>
    /// Debug visualization for Phase 4 anchors.
    /// Subscribes to AnchorManager (not DetectionManager) and shows a sphere per class.
    /// </summary>
    public class DetectionVisualizer : MonoBehaviour
    {
        [SerializeField]
        AnchorManager m_AnchorManager;

        [SerializeField]
        float m_SphereScale = 0.1f;

        readonly Dictionary<string, Transform> m_DebugSpheres = new Dictionary<string, Transform>();
        Transform m_LighterForLog;

        void Awake()
        {
            if (m_AnchorManager == null)
                m_AnchorManager = FindFirstObjectByType<AnchorManager>();
        }

        void OnEnable()
        {
            if (m_AnchorManager != null)
            {
                m_AnchorManager.AnchorUpdated += OnAnchorUpdated;
                Debug.Log(
                    "[DetectionVisualizer] Subscribed to AnchorManager.AnchorUpdated " +
                    $"(manager={m_AnchorManager.name}).");
            }
            else
            {
                Debug.LogError(
                    "[DetectionVisualizer] No AnchorManager found. " +
                    "Add AnchorManager to the scene (Visualizer no longer uses DetectionManager).");
            }
        }

        void OnDisable()
        {
            if (m_AnchorManager != null)
            {
                m_AnchorManager.AnchorUpdated -= OnAnchorUpdated;
                Debug.Log("[DetectionVisualizer] Unsubscribed from AnchorManager.AnchorUpdated.");
            }
        }

        void OnDestroy()
        {
            foreach (var pair in m_DebugSpheres)
            {
                if (pair.Value != null)
                    Destroy(pair.Value.gameObject);
            }
            m_DebugSpheres.Clear();
        }

        void LateUpdate()
        {
            // Keep debug spheres locked to live (smoothed) anchor transforms.
            foreach (var pair in m_DebugSpheres)
            {
                string className = pair.Key;
                Transform sphere = pair.Value;
                if (sphere == null || m_AnchorManager == null)
                    continue;

                if (m_AnchorManager.Anchors.TryGetValue(className, out Transform anchor) &&
                    anchor != null)
                {
                    sphere.position = anchor.position;
                }
            }
        }

        void OnAnchorUpdated(AnchorPoseEvent evt)
        {
            if (evt.anchor == null || string.IsNullOrEmpty(evt.className))
                return;

            Transform sphere = GetOrCreateDebugSphere(evt.className);
            sphere.position = evt.anchor.position;

            Debug.Log(
                $"[DetectionVisualizer] Visualizing anchor={evt.anchor.name} " +
                $"worldPos={evt.anchor.position} class={evt.className} conf={evt.confidence:F2}");

            if (m_LighterForLog == null)
            {
                var lighterGo = GameObject.Find("Lighter");
                if (lighterGo != null)
                    m_LighterForLog = lighterGo.transform;
            }

            if (m_LighterForLog != null)
            {
                Debug.Log(
                    $"[DetectionVisualizer] Lighter worldPos={m_LighterForLog.position} " +
                    $"parent={(m_LighterForLog.parent != null ? m_LighterForLog.parent.name : "(none)")}");
            }
        }

        Transform GetOrCreateDebugSphere(string className)
        {
            if (m_DebugSpheres.TryGetValue(className, out Transform existing) && existing != null)
                return existing;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"DetectionDebug_{className}";
            go.transform.localScale = Vector3.one * m_SphereScale;
            Collider col = go.GetComponent<Collider>();
            if (col != null)
                col.enabled = false;

            // Bright debug marker only — not the production Lighter mesh.
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    var mat = new Material(shader);
                    mat.color = Color.magenta;
                    renderer.sharedMaterial = mat;
                }
            }

            m_DebugSpheres[className] = go.transform;
            return go.transform;
        }
    }
}
