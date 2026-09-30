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

        [Header("Optional affective animation study")]
        [Tooltip("Optional 2D narrative overlay. Disabled by default so the scene uses the original 4D lighter grammar.")]
        [SerializeField]
        bool m_EnableAffectiveNarrative = false;

        [SerializeField]
        string m_SubjectId = StudySpec.DefaultSubjectId;

        [SerializeField]
        int m_TrialId = StudySpec.DefaultTrialId;

        void Awake()
        {
            if (m_EnableAffectiveNarrative)
                EnsureAffectiveNarrativeSystem();
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
                "[BridgeGesture] Ready: hands + 4D lighter gestures. YOLO remains spatial owner.",
                this);
        }

        void EnsureAffectiveNarrativeSystem()
        {
            var root = GameObject.Find("GestureDetectors");
            var loggerHost = root != null ? root : gameObject;
            var logger = FindFirstObjectByType<DataLogger>();
            if (logger == null)
                logger = loggerHost.GetComponent<DataLogger>() ?? loggerHost.AddComponent<DataLogger>();

            // BridgeStereoFusion deliberately has GestureManager auto-start off,
            // so the affective study owns exactly one session here.
            if (!logger.SessionActive)
                logger.StartSession(m_SubjectId, "AffectiveNarrative", m_TrialId);

            var director = FindFirstObjectByType<RealityScenarioDirector>();
            if (director == null)
            {
                var host = new GameObject("AffectiveNarrativeSystem");
                director = host.AddComponent<RealityScenarioDirector>();
            }

            FindFirstObjectByType<GestureManager>()?.SetEnabledDimensions(EnabledDimensions.All);
            Debug.Log("[BridgeGesture] Affective narrative system ready: pinch-release → anchored 2D animation.", director);
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
