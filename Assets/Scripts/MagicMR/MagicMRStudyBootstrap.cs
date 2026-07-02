using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    /// <summary>
    /// Ensures study systems exist when MagicMR scene loads.
    /// Also fixes common PICO MR runtime issues (VST camera, duplicate cameras, target placement).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class MagicMRStudyBootstrap : MonoBehaviour
    {
        [SerializeField]
        bool m_ConfigureVstCamera = true;

        [SerializeField]
        bool m_DisableOrphanCameras = true;

        [SerializeField]
        bool m_PlaceLighterInFront = true;

        [SerializeField]
        bool m_ShowHandVisualizer = true;

        [SerializeField]
        float m_LighterDistance = StudySpec.LighterDistance;

        [SerializeField]
        float m_LighterHeightOffset = StudySpec.LighterHeightOffset;

        [Header("Study Session (runtime StudySystem)")]
        [SerializeField]
        string m_SubjectId = StudySpec.DefaultSubjectId;

        [SerializeField]
        string m_Condition = StudySpec.DefaultCondition;

        [SerializeField]
        int m_TrialId = StudySpec.DefaultTrialId;

        [SerializeField]
        EnabledDimensions m_EnabledDimensions = EnabledDimensions.All;

        // Bump this string on every build-affecting fix. Logged (not rendered
        // in-headset) so we can confirm on-device which build is running via adb.
        const string k_BuildTag = "MagicMR build: vst-fix-05 (layer-blend)";

        void Awake()
        {
            EnsureStudySystem();
            EnsureRealityEditorOnLighter();
            EnsureLighterDefaultAppearance();
            if (m_ShowHandVisualizer)
                EnsureHandVisualizer();
            if (m_DisableOrphanCameras)
                DisableOrphanMainCameras();
            if (m_ConfigureVstCamera)
            {
                DisableSceneVolumesForVst();
                ConfigureXrCameraForVst();
            }

            SpawnBuildTag();
        }

        static void SpawnBuildTag()
        {
            Debug.Log($"[MagicMR] {k_BuildTag}");
        }

        static void DisableSceneVolumesForVst()
        {
            // URP post-processing / fog writes an opaque frame that hides passthrough.
            foreach (var volume in FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
            {
                volume.enabled = false;
                volume.gameObject.SetActive(false);
                Debug.Log($"[MagicMR] Disabled volume for VST: {volume.name}", volume);
            }

            RenderSettings.fog = false;
        }

        static void EnsureHandVisualizer()
        {
            var parent = FindHandVisualizerParent();

            var leftPrefab = Resources.Load<GameObject>("MagicMR/LeftHandTracking");
            var rightPrefab = Resources.Load<GameObject>("MagicMR/RightHandTracking");

            if (leftPrefab != null && rightPrefab != null)
            {
                SpawnHand(leftPrefab, parent, "LeftHandTracking");
                SpawnHand(rightPrefab, parent, "RightHandTracking");
                Debug.Log("[MagicMR] Spawned realistic hand meshes.");
                return;
            }

            if (FindFirstObjectByType<HandJointVisualizer>() != null)
                return;

            var go = new GameObject("HandVisualizer");
            go.AddComponent<HandJointVisualizer>();
            Debug.LogWarning("[MagicMR] Hand prefabs not found; using sphere joints fallback.");
        }

        static Material s_HandMaterial;

        static void SpawnHand(GameObject prefab, Transform parent, string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
                return;

            var hand = Instantiate(prefab, parent);
            hand.name = name;
            hand.transform.localPosition = Vector3.zero;
            hand.transform.localRotation = Quaternion.identity;

            ApplyUrpHandMaterial(hand);
        }

        static void ApplyUrpHandMaterial(GameObject hand)
        {
            var material = GetHandMaterial();
            if (material == null)
                return;

            foreach (var renderer in hand.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = material;
        }

        static Material GetHandMaterial()
        {
            if (s_HandMaterial != null)
                return s_HandMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                return null;

            var material = new Material(shader);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            var color = new Color(0.7f, 0.75f, 0.85f, 0.6f);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            s_HandMaterial = material;
            return s_HandMaterial;
        }

        static Transform FindHandVisualizerParent()
        {
            var xrOrigin = GameObject.Find("XR Origin (VR)");
            if (xrOrigin == null)
                return null;

            var origin = xrOrigin.GetComponent<Unity.XR.CoreUtils.XROrigin>();
            if (origin != null)
            {
                if (origin.CameraFloorOffsetObject != null)
                    return origin.CameraFloorOffsetObject.transform;
                return origin.transform;
            }

            return xrOrigin.transform;
        }

        void Start()
        {
            StartCoroutine(DeferredMrSetup());
        }

        void OnApplicationPause(bool pause)
        {
            if (!pause)
                PicoVideoSeeThrough.OnApplicationResumed();
        }

        IEnumerator DeferredMrSetup()
        {
            yield return null;

            if (m_ConfigureVstCamera)
            {
                ConfigureXrCameraForVst();
                StartCoroutine(PicoVideoSeeThrough.EnableWithRetry(0.15f, 8, 0.35f));
            }

            yield return new WaitUntil(() => XRSettings.isDeviceActive);

            if (m_PlaceLighterInFront)
                PlaceLighterInFrontOfUser();

            LogHandTrackingStatus();
        }

        void EnsureStudySystem()
        {
            var logger = FindFirstObjectByType<DataLogger>();
            var manager = FindFirstObjectByType<GestureManager>();

            if (logger == null || manager == null)
            {
                var go = new GameObject("StudySystem");
                logger ??= go.AddComponent<DataLogger>();
                manager ??= go.AddComponent<GestureManager>();
            }

            logger.ConfigureLogging(StudySpec.LogHandTrajectory, StudySpec.TrajectorySampleInterval);
            manager.ConfigureSession(m_SubjectId, m_Condition, m_TrialId, m_EnabledDimensions);
        }

        static void EnsureRealityEditorOnLighter()
        {
            var lighter = GameObject.Find("Lighter");
            if (lighter == null || lighter.GetComponent<RealityEditor>() != null)
                return;

            lighter.AddComponent<RealityEditor>();
        }

        static void DisableOrphanMainCameras()
        {
            var xrOrigin = GameObject.Find("XR Origin (VR)");
            Camera xrCamera = null;
            if (xrOrigin != null)
            {
                var origin = xrOrigin.GetComponent<Unity.XR.CoreUtils.XROrigin>();
                xrCamera = origin != null ? origin.Camera : xrOrigin.GetComponentInChildren<Camera>();
            }

            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (cam == null || cam == xrCamera)
                    continue;

                if (!cam.CompareTag("MainCamera"))
                    continue;

                Debug.Log($"[MagicMR] Disabling orphan camera: {cam.name}", cam);
                cam.tag = "Untagged";
                cam.enabled = false;
                var listener = cam.GetComponent<AudioListener>();
                if (listener != null)
                    listener.enabled = false;
                cam.gameObject.SetActive(false);
            }
        }

        static void ConfigureXrCameraForVst()
        {
            var cam = FindXrCamera();
            if (cam == null)
            {
                Debug.LogWarning("[MagicMR] XR camera not found; VST may not render correctly.");
                return;
            }

            PicoVideoSeeThrough.ConfigureCamera(cam);

            // RGBA8 (HDR off) buffer is required for URP alpha output to reach
            // the eye swapchain; HDR would switch to a format that can drop alpha.
            cam.allowHDR = false;

            // URP requires this component to route the camera through its render
            // passes (post-processing, alpha output, etc). Without it the camera
            // falls back to a plain/optimized path that discards alpha, which
            // silently defeats every alpha-based VST fix regardless of URP Asset
            // settings. Cameras created purely via script/prefab (never opened in
            // the Inspector while URP was active) can be missing it.
            var cameraData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                cameraData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                Debug.LogWarning(
                    $"[MagicMR] '{cam.name}' had no UniversalAdditionalCameraData; added one at runtime.",
                    cam);
            }

            // Post-processing must stay on so URP's alpha-output pass runs
            // (URP Asset "Alpha Processing" enabled) and preserves the
            // transparent background PICO needs for passthrough.
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.renderShadows = false;

            Debug.Log(
                $"[MagicMR] VST camera '{cam.name}': clear={cam.clearFlags} bgA={cam.backgroundColor.a:F2} " +
                $"hdr={cam.allowHDR} post={cameraData.renderPostProcessing}",
                cam);
        }

        static void EnsureLighterDefaultAppearance()
        {
            var lighter = GameObject.Find("Lighter");
            if (lighter == null)
                return;

            var renderer = lighter.GetComponent<Renderer>();
            if (renderer == null)
                return;

            var current = renderer.sharedMaterial;
            if (current == null || !IsBurntMaterial(current))
                return;

            var defaultMaterial = Resources.Load<Material>("MagicMR/LighterDefault");
            if (defaultMaterial == null)
            {
                Debug.LogWarning("[MagicMR] Lighter still uses burnt material; rebuild with latest scene.");
                return;
            }

            renderer.sharedMaterial = defaultMaterial;
            Debug.Log("[MagicMR] Reset Lighter to default (non-burnt) material.", lighter);
        }

        static bool IsBurntMaterial(Material material)
        {
            return material.name.IndexOf("burnt", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static Camera FindXrCamera()
        {
            var xrOrigin = GameObject.Find("XR Origin (VR)");
            if (xrOrigin != null)
            {
                var origin = xrOrigin.GetComponent<Unity.XR.CoreUtils.XROrigin>();
                if (origin != null && origin.Camera != null)
                    return origin.Camera;

                var childCam = xrOrigin.GetComponentInChildren<Camera>();
                if (childCam != null)
                    return childCam;
            }

            return Camera.main;
        }

        void PlaceLighterInFrontOfUser()
        {
            var lighter = GameObject.Find("Lighter");
            var cam = FindXrCamera();
            if (lighter == null || cam == null)
                return;

            var forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            // Keep the target small so the user is never "inside" it.
            var maxDimension = Mathf.Max(
                lighter.transform.localScale.x,
                lighter.transform.localScale.y,
                lighter.transform.localScale.z);
            if (maxDimension > 0.25f)
                lighter.transform.localScale = Vector3.one * 0.08f;

            var targetPos = cam.transform.position
                + forward * m_LighterDistance
                + Vector3.up * m_LighterHeightOffset;

            lighter.transform.position = targetPos;
            lighter.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
            Debug.Log($"[MagicMR] Placed Lighter at {targetPos} scale {lighter.transform.localScale.x}", lighter);
        }

        static void LogHandTrackingStatus()
        {
#if XR_HANDS_1_1_OR_NEWER
            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            if (subsystems.Count == 0)
            {
                Debug.LogError(
                    "[MagicMR] XR Hand Subsystem not found. " +
                    "Enable Hand Tracking in PICO system settings and rebuild the app.");
                return;
            }

            var subsystem = subsystems[0];
            if (!subsystem.running)
                subsystem.Start();

            Debug.Log($"[MagicMR] XR Hand Subsystem running: {subsystem.running}");
#else
            Debug.LogError("[MagicMR] XR Hands package not available at compile time.");
#endif
        }
    }
}
