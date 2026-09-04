using System.Collections;
using MagicMR;
using UnityEngine;

namespace Perception
{
    /// <summary>
    /// Minimal BridgeTest bootstrap for PICO hands + gestures.
    /// Intentionally does NOT create LighterAnchorManager, calibration,
    /// follow-hand, or any Lighter pose ownership.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class BridgeGestureBootstrap : MonoBehaviour
    {
        [SerializeField]
        bool m_ConfigureVst = true;

        [SerializeField]
        bool m_DisableOrphanCameras = true;

        void Awake()
        {
            EnsureFistBurstDetector();
            if (m_DisableOrphanCameras)
                DisableOrphanMainCameras();
            if (m_ConfigureVst)
            {
                PicoVideoSeeThrough.ConfigurePxrManagerForVst();
                PicoVideoSeeThrough.ConfigureAllXrCameras();
            }
        }

        IEnumerator Start()
        {
            if (m_ConfigureVst)
                yield return PicoVideoSeeThrough.EnableWithRetry(0.15f, 8, 0.35f);

            HandGestureDetectorBase.EnsureAllSubscribed();
            var manager = FindFirstObjectByType<GestureManager>();
            manager?.RebindDetectors();

            AssertNoSpatialOwnershipSystems();
            Debug.Log(
                "[BridgeGesture] Ready: hands+gestures only. YOLO remains spatial owner.",
                this);
        }

        static void EnsureFistBurstDetector()
        {
            var root = GameObject.Find("GestureDetectors");
            if (root == null)
                return;

            if (root.GetComponent<FistBurstGestureDetector>() == null)
                root.AddComponent<FistBurstGestureDetector>();
        }

        void DisableOrphanMainCameras()
        {
            var xr = GameObject.Find("XR Origin (VR)");
            if (xr == null)
                xr = GameObject.Find("[Building Block] PICO Video Seethrough XR Origin (XR Rig)");
            if (xr == null)
                return;

            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (cam == null || cam.transform.IsChildOf(xr.transform))
                    continue;
                if (cam.CompareTag("MainCamera") || cam.gameObject.name == "Main Camera")
                {
                    Debug.Log($"[BridgeGesture] Disabling orphan camera '{cam.name}'.", cam);
                    cam.gameObject.SetActive(false);
                }
            }
        }

        static void AssertNoSpatialOwnershipSystems()
        {
            var lighterAnchor = FindFirstObjectByType<LighterAnchorManager>();
            if (lighterAnchor != null)
            {
                Debug.LogError(
                    "[BridgeGesture] LighterAnchorManager found in BridgeTest — " +
                    "this conflicts with YOLO pose ownership. Remove it.",
                    lighterAnchor);
            }

            var fsm = FindFirstObjectByType<MRGestureController>();
            if (fsm != null)
            {
                Debug.LogError(
                    "[BridgeGesture] MRGestureController found — FollowingHand/calibration " +
                    "can steal Lighter pose. Remove it for BridgeTest.",
                    fsm);
            }

            var bootstrap = FindFirstObjectByType<MagicMRStudyBootstrap>();
            if (bootstrap != null)
            {
                Debug.LogError(
                    "[BridgeGesture] MagicMRStudyBootstrap found — disable/remove it; " +
                    "it adds LighterAnchorManager and PlaceLighterInFront.",
                    bootstrap);
            }
        }
    }
}
