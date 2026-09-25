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
        bool m_ShowHandVisualizer = false;

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
        const string k_BuildTag = "MagicMR build: vst-fix-49 layerBlend=0 match VstTest + skybox off";

        LighterAnchorManager m_LighterAnchorManager;

        void Awake()
        {
            EnsureStudySystem();
            EnsureRealityEditorOnLighter();
            EnsureLighterAnchorManager();
            EnsureLighterDefaultAppearance();
            EnsurePolishSystems();
            if (m_ShowHandVisualizer)
                EnsureHandVisualizer();
            else
                StripHandVisuals();
            EnsureHandVisualSuppressor();
            if (m_DisableOrphanCameras)
                DisableOrphanMainCameras();
            if (m_ConfigureVstCamera)
            {
                DisableSceneVolumesForVst();
                PicoVideoSeeThrough.ConfigurePxrManagerForVst();
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
            PicoVideoSeeThrough.DisableOpaqueEnvironment();
        }

        static void StripHandVisuals()
        {
            foreach (var name in new[] { "LeftHandTracking", "RightHandTracking", "HandVisualizer" })
            {
                var existing = GameObject.Find(name);
                if (existing != null)
                {
                    Debug.Log($"[MagicMR] Removing hand visual object: {name}");
                    UnityEngine.Object.DestroyImmediate(existing);
                }
            }

            foreach (var viz in FindObjectsByType<HandJointVisualizer>(FindObjectsSortMode.None))
            {
                Debug.Log("[MagicMR] Removing HandJointVisualizer.", viz);
                UnityEngine.Object.DestroyImmediate(viz.gameObject);
            }

            HandVisualSuppressor.Sweep();
        }

        static void EnsureHandVisualSuppressor()
        {
            if (FindFirstObjectByType<HandVisualSuppressor>() != null)
                return;

            var go = new GameObject("HandVisualSuppressor");
            go.AddComponent<HandVisualSuppressor>();
        }

        static void DisableRuntimeHandMeshes()
        {
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer == null)
                    continue;

                var rootName = renderer.transform.root.name;
                if (rootName.Contains("HandTracking") || rootName.Contains("Hand Visual"))
                {
                    renderer.enabled = false;
                    Debug.Log($"[MagicMR] Disabled hand renderer on {renderer.name}.", renderer);
                }
            }

            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour == null)
                    continue;

                var typeName = behaviour.GetType().FullName;
                if (typeName == "UnityEngine.XR.Hands.XRHandMeshController" ||
                    typeName == "UnityEngine.XR.Hands.Samples.VisualizerSample.HandVisualizer")
                {
                    behaviour.enabled = false;
                    Debug.Log($"[MagicMR] Disabled {typeName}.", behaviour);
                }
            }
        }

        static void EnsureHandVisualizer()
        {
            var parent = FindHandVisualizerParent();

            var leftPrefab = Resources.Load<GameObject>("MagicMR/LeftHandTracking");
            var rightPrefab = Resources.Load<GameObject>("MagicMR/RightHandTracking");

            if (leftPrefab == null || rightPrefab == null)
            {
                Debug.LogWarning("[MagicMR] Hand prefabs removed from Resources; hand visuals disabled.");
                return;
            }

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
                PicoVideoSeeThrough.DisableOpaqueEnvironment();
                ConfigureXrCameraForVst();
                PicoVideoSeeThrough.ConfigurePxrManagerForVst();
                StartCoroutine(PicoVideoSeeThrough.EnableWithRetry(0.15f, 8, 0.35f));
            }

            yield return new WaitUntil(() => XRSettings.isDeviceActive);

            // Only use the fixed virtual placement as a starting point/fallback;
            // once the user calibrates onto the real lighter (pinch-and-hold),
            // that anchored position takes over and must not be overwritten.
            if (m_PlaceLighterInFront && (m_LighterAnchorManager == null || !m_LighterAnchorManager.IsCalibrated))
                PlaceLighterInFrontOfUser();

            var fsm = FindFirstObjectByType<MRGestureController>();
            var lighter = GameObject.Find("Lighter");
            if (fsm != null && lighter != null)
                fsm.SetDeskPreset(lighter.transform.position, lighter.transform.rotation);

            LogHandTrackingStatus();
            HandGestureDetectorBase.EnsureAllSubscribed();

            var manager = FindFirstObjectByType<GestureManager>();
            if (manager != null)
                manager.RebindDetectors();

            if (m_LighterAnchorManager == null)
                m_LighterAnchorManager = FindFirstObjectByType<LighterAnchorManager>();
            // FSM owns calibration; BindPinchHoldListener is a no-op when FSM exists.
            m_LighterAnchorManager?.BindPinchHoldListener();

            EnsurePicoVstKeeper();

            if (!m_ShowHandVisualizer)
                HandVisualSuppressor.Sweep();
        }

        static void EnsurePicoVstKeeper()
        {
            if (FindFirstObjectByType<PicoVstKeeper>() != null)
                return;

            var go = new GameObject("PicoVstKeeper");
            go.AddComponent<PicoVstKeeper>();
            Debug.Log("[MagicMR] PicoVstKeeper spawned for passthrough keep-alive.");
        }

        void EnsureStudySystem()
        {
            var logger = FindFirstObjectByType<DataLogger>();
            var manager = FindFirstObjectByType<GestureManager>();
            var fsm = FindFirstObjectByType<MRGestureController>();

            // Prefer attaching to GestureDetectors so Find/GetComponent always works.
            var root = GameObject.Find("GestureDetectors");
            var go = root != null ? root : new GameObject("StudySystem");

            logger ??= go.GetComponent<DataLogger>() ?? go.AddComponent<DataLogger>();
            manager ??= go.GetComponent<GestureManager>() ?? go.AddComponent<GestureManager>();
            fsm ??= go.GetComponent<MRGestureController>() ?? go.AddComponent<MRGestureController>();
            var poseBridge = go.GetComponent<PicoHandPoseGestureBridge>() ??
                             go.AddComponent<PicoHandPoseGestureBridge>();

            logger.ConfigureLogging(StudySpec.LogHandTrajectory, StudySpec.TrajectorySampleInterval);
            manager.ConfigureSession(m_SubjectId, m_Condition, m_TrialId, m_EnabledDimensions);
            manager.RebindDetectors();
            Debug.Log(
                $"[MagicMR] Study system ready (FSM={fsm != null}, HandPoseBridge={poseBridge != null}).",
                go);
        }

        static void EnsureRealityEditorOnLighter()
        {
            var lighter = GameObject.Find("Lighter");
            if (lighter == null || lighter.GetComponent<RealityEditor>() != null)
                return;

            lighter.AddComponent<RealityEditor>();
        }

        void EnsureLighterAnchorManager()
        {
            m_LighterAnchorManager = FindFirstObjectByType<LighterAnchorManager>();
            if (m_LighterAnchorManager == null)
            {
                var lighter = GameObject.Find("Lighter");
                if (lighter != null)
                    m_LighterAnchorManager = lighter.AddComponent<LighterAnchorManager>();
            }
        }

        static void EnsurePolishSystems()
        {
            if (FindFirstObjectByType<CalibrationRitual>() == null)
            {
                var ritual = new GameObject("CalibrationRitual");
                ritual.AddComponent<CalibrationRitual>();
            }

            if (FindFirstObjectByType<StudyResetWristUi>() == null)
            {
                var reset = new GameObject("StudyResetWristUi");
                reset.AddComponent<StudyResetWristUi>();
            }

            if (FindFirstObjectByType<ExperimentConditionSwitcher>() == null)
            {
                var conditions = new GameObject("ExperimentConditionSwitcher");
                conditions.AddComponent<ExperimentConditionSwitcher>();
            }

            Debug.Log("[MagicMR] Polish systems ready (ritual + reset + conditions).");
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
            cam.allowHDR = false;
            cam.allowMSAA = false;

            var cameraData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                cameraData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                Debug.LogWarning(
                    $"[MagicMR] '{cam.name}' had no UniversalAdditionalCameraData; added one at runtime.",
                    cam);
            }

            // Match VstTest isolation path: opaque post/fog hides passthrough.
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.renderShadows = false;
            cameraData.allowHDROutput = false;

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
            var buildingBlock = GameObject.Find("[Building Block] PICO Video Seethrough XR Origin (XR Rig)");
            var xrOrigin = buildingBlock != null ? buildingBlock : GameObject.Find("XR Origin (VR)");
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

            if (m_LighterAnchorManager != null)
                m_LighterAnchorManager.SetDeskReferenceHeight(targetPos.y);
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
