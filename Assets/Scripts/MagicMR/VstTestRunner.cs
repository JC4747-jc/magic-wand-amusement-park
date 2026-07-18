using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace MagicMR
{
    /// <summary>
    /// Minimal passthrough isolation scene (VstTest.unity). Mirrors the
    /// official PICO MR SeeThrough sample path: PXR_Manager on XR Origin,
    /// XR camera with alpha=0, no post-processing, then
    /// PXR_Manager.EnableVideoSeeThrough at runtime. No gestures, lighter, or
    /// study systems — use this build to decide whether VST works at all on the
    /// device before debugging MagicMR.unity.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class VstTestRunner : MonoBehaviour
    {
        const string k_BuildTag = "VstTest build: official-minimal-01 (PXR_Manager.EnableVideoSeeThrough, URP alpha)";

        [SerializeField]
        Camera m_Camera;

        [SerializeField]
        bool m_DisableOrphanCameras = true;

        void Awake()
        {
            Debug.Log($"[VstTest] {k_BuildTag}");
            ResolveCamera();
            if (m_DisableOrphanCameras)
                DisableOrphanMainCameras();
            DisableSceneVolumes();
            ConfigureCameraForVst();
        }

        void Start()
        {
            StartCoroutine(DeferredVstEnable());
        }

        void OnApplicationPause(bool pause)
        {
            if (!pause)
                PicoVideoSeeThrough.OnApplicationResumed();
        }

        IEnumerator DeferredVstEnable()
        {
            yield return null;
            ConfigureCameraForVst();
            yield return PicoVideoSeeThrough.EnableWithRetry(0.15f, 8, 0.35f);
        }

        void ResolveCamera()
        {
            if (m_Camera != null)
                return;

            var xrOrigin = GameObject.Find("[Building Block] PICO Video Seethrough XR Origin (XR Rig)");
            if (xrOrigin == null)
                xrOrigin = GameObject.Find("XR Origin (VR)");

            if (xrOrigin != null)
            {
                var origin = xrOrigin.GetComponent<Unity.XR.CoreUtils.XROrigin>();
                if (origin != null && origin.Camera != null)
                {
                    m_Camera = origin.Camera;
                    return;
                }

                m_Camera = xrOrigin.GetComponentInChildren<Camera>();
                if (m_Camera != null)
                    return;
            }

            m_Camera = Camera.main;
            if (m_Camera == null)
                Debug.LogError("[VstTest] No XR camera found.");
        }

        static void DisableOrphanMainCameras()
        {
            Camera xrCamera = null;
            var buildingBlock = GameObject.Find("[Building Block] PICO Video Seethrough XR Origin (XR Rig)");
            var xrOriginGo = buildingBlock != null ? buildingBlock : GameObject.Find("XR Origin (VR)");
            if (xrOriginGo != null)
            {
                var origin = xrOriginGo.GetComponent<Unity.XR.CoreUtils.XROrigin>();
                xrCamera = origin != null ? origin.Camera : xrOriginGo.GetComponentInChildren<Camera>();
            }

            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (cam == null || cam == xrCamera)
                    continue;

                if (!cam.CompareTag("MainCamera") && cam.enabled)
                    continue;

                Debug.Log($"[VstTest] Disabling orphan camera: {cam.name}", cam);
                cam.tag = "Untagged";
                cam.enabled = false;
                var listener = cam.GetComponent<AudioListener>();
                if (listener != null)
                    listener.enabled = false;
                cam.gameObject.SetActive(false);
            }
        }

        static void DisableSceneVolumes()
        {
            foreach (var volume in FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
            {
                volume.enabled = false;
                volume.gameObject.SetActive(false);
            }

            RenderSettings.fog = false;
        }

        void ConfigureCameraForVst()
        {
            if (m_Camera == null)
                return;

            PicoVideoSeeThrough.ConfigureCamera(m_Camera);
            m_Camera.allowHDR = false;
            m_Camera.allowMSAA = false;

            var cameraData = m_Camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
                cameraData = m_Camera.gameObject.AddComponent<UniversalAdditionalCameraData>();

            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.renderShadows = false;
            cameraData.allowHDROutput = false;

            Debug.Log(
                $"[VstTest] Camera '{m_Camera.name}': clear={m_Camera.clearFlags} " +
                $"bgA={m_Camera.backgroundColor.a:F2} hdr={m_Camera.allowHDR} " +
                $"post={cameraData.renderPostProcessing}",
                m_Camera);
        }
    }
}
