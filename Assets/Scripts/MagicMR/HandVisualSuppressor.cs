using UnityEngine;

namespace MagicMR
{
    /// <summary>
    /// Keeps XR/PICO hand mesh renderers disabled so passthrough shows real hands.
    /// Gesture detectors use joint data only; they do not need any hand visuals.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class HandVisualSuppressor : MonoBehaviour
    {
        float m_NextSweepTime;

        void LateUpdate()
        {
            if (Time.unscaledTime < m_NextSweepTime)
                return;

            m_NextSweepTime = Time.unscaledTime + 0.25f;
            Sweep();
        }

        public static void Sweep()
        {
            foreach (var name in new[] { "LeftHandTracking", "RightHandTracking", "HandVisualizer" })
            {
                var existing = GameObject.Find(name);
                if (existing != null)
                    Destroy(existing);
            }

            foreach (var viz in FindObjectsByType<HandJointVisualizer>(FindObjectsSortMode.None))
                Destroy(viz.gameObject);

            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer == null || !renderer.enabled)
                    continue;

                if (!IsHandVisualRenderer(renderer))
                    continue;

                renderer.enabled = false;
            }

            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour == null || !behaviour.enabled)
                    continue;

                var typeName = behaviour.GetType().FullName;
                if (typeName == "UnityEngine.XR.Hands.XRHandMeshController" ||
                    typeName == "UnityEngine.XR.Hands.Samples.VisualizerSample.HandVisualizer")
                    behaviour.enabled = false;
            }
        }

        static bool IsHandVisualRenderer(Renderer renderer)
        {
            var rootName = renderer.transform.root.name;
            if (rootName == "Lighter")
                return false;
            if (rootName.Contains("HandTracking") ||
                rootName.Contains("Hand Visual") ||
                rootName.Contains("HandVisualizer"))
                return true;

            var nodeName = renderer.gameObject.name;
            if (nodeName.Contains("HandMesh") ||
                nodeName.Contains("HandJoint") ||
                nodeName.Contains("LeftHand") ||
                nodeName.Contains("RightHand"))
                return true;

            return renderer is SkinnedMeshRenderer &&
                   renderer.transform.root.GetComponentInChildren<HandJointVisualizer>() != null;
        }
    }
}
