using UnityEngine;
using UnityEngine.SceneManagement;
using MagicMR;

namespace Perception
{
    /// <summary>
    /// Supplies MagicMR with the complete stereo YOLO path without making the
    /// MagicMR assembly depend on the Assembly-CSharp perception code.
    /// </summary>
    public sealed class MagicMRStereoYoloBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateForMagicMR()
        {
            if (!StudySpec.StereoYoloPipelineEnabled)
                return;
            if (SceneManager.GetActiveScene().path != "Assets/Scenes/MagicMR.unity" ||
                FindFirstObjectByType<StereoYoloLocator>() != null)
                return;

            var lighter = GameObject.Find("Lighter");
            var camera = Camera.main;
            if (lighter == null || camera == null)
            {
                Debug.LogError("[MagicMR] Stereo YOLO not started: Lighter or XR camera is missing.");
                return;
            }

            var host = new GameObject("MagicMR Stereo YOLO");
            host.SetActive(false);

            // PicoStereoCapture supplies the sender directly through AcceptPair;
            // a PicoCameraCapture here would compete for the PICO RGB camera.
            var transport = host.AddComponent<TcpClient>();
            transport.ConfigureBridge("127.0.0.1", 5005, autoReconnect: true);
            var sender = host.AddComponent<PicoYoloFrameSender>();
            sender.ConfigureStereoSender();
            var detections = host.AddComponent<DetectionManager>();
            host.AddComponent<SingleTargetManager>();
            var anchor = host.AddComponent<AnchorManager>();
            host.AddComponent<LighterUnderAnchorBinder>();
            var locator = host.AddComponent<StereoYoloLocator>();
            locator.sender = sender;
            locator.detections = detections;
            locator.transport = transport;
            locator.trackedCamera = camera.transform;
            locator.lighter = lighter.transform;
            locator.followMode = LighterFollowMode.VisualAverage;
            anchor.ConfigureStereoDepth(locator);
            host.AddComponent<PicoStereoCapture>().locator = locator;

            host.SetActive(true);
            Debug.Log("[MagicMR] Stereo YOLO ready: PC bridge 127.0.0.1:5005, reconnect enabled.", host);
        }
    }
}
