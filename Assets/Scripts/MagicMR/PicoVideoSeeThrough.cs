using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MagicMR
{
    /// <summary>
    /// Enables PICO video see-through (passthrough) at runtime.
    /// Official sample path uses <c>PXR_Manager.EnableVideoSeeThrough</c>;
    /// we also call <c>PXR_Boundary.EnableSeeThroughManual</c> as a fallback.
    /// </summary>
    public static class PicoVideoSeeThrough
    {
        static readonly string[] s_ManagerTypeNames =
        {
            "Unity.XR.PXR.PXR_Manager, Unity.XR.PICO",
            "Unity.XR.PXR.PXR_Manager, Unity.XR.PXR",
            "Unity.XR.PXR.PXR_Manager"
        };

        static readonly string[] s_BoundaryTypeNames =
        {
            "Unity.XR.PXR.PXR_Boundary, Unity.XR.PICO",
            "Unity.XR.PXR.PXR_Boundary, Unity.XR.PXR",
            "Unity.XR.PXR.PXR_Boundary"
        };

        public static void ConfigureCamera(Camera camera)
        {
            if (camera == null)
                return;

            camera.clearFlags = CameraClearFlags.SolidColor;
            var bg = camera.backgroundColor;
            bg.r = 0f;
            bg.g = 0f;
            bg.b = 0f;
            bg.a = 0f;
            camera.backgroundColor = bg;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
                cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();

            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.renderShadows = false;
            cameraData.allowHDROutput = false;
        }

        public static void ConfigureAllXrCameras()
        {
            var buildingBlock = GameObject.Find("[Building Block] PICO Video Seethrough XR Origin (XR Rig)");
            var xrOrigin = buildingBlock != null ? buildingBlock : GameObject.Find("XR Origin (VR)");
            if (xrOrigin != null)
            {
                foreach (var cam in xrOrigin.GetComponentsInChildren<Camera>(true))
                    ConfigureCamera(cam);
            }

            if (Camera.main != null)
                ConfigureCamera(Camera.main);
        }

        /// <summary>
        /// Match the working VstTest.unity PXR_Manager compositing flags.
        /// MagicMR had useLayerBlend=1 (dst=10) which composites an opaque frame
        /// and hides passthrough even when EnableVideoSeeThrough succeeds.
        /// </summary>
        public static void ConfigurePxrManagerForVst()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var managerType = ResolveType(s_ManagerTypeNames, "Unity.XR.PXR.PXR_Manager");
                if (managerType == null)
                    return;

                var instance = UnityEngine.Object.FindFirstObjectByType(managerType);
                if (instance == null)
                {
                    Debug.LogWarning("[MagicMR] PXR_Manager not found in scene.");
                    return;
                }

                // VstTest / official sample values (NOT layer-blend MRC mode).
                SetMember(instance, "useLayerBlend", false);
                SetMember(instance, "usePremultipliedAlpha", false);
                SetMember(instance, "openMRC", true);
                SetMember(instance, "srcColor", 1);
                SetMember(instance, "dstColor", 1);
                SetMember(instance, "srcAlpha", 1);
                SetMember(instance, "dstAlpha", 1);

                Debug.Log(
                    "[MagicMR] PXR_Manager VST compositing reset " +
                    "(useLayerBlend=0, premult=0, openMRC=1).",
                    instance as Component);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[MagicMR] ConfigurePxrManagerForVst failed: {exception.Message}");
            }
#endif
        }

        static void SetMember(object target, string name, object value)
        {
            var type = target.GetType();
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
                prop.SetValue(target, value);
        }

        public static void DisableOpaqueEnvironment()
        {
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.25f, 0.28f, 1f);
        }

        public static void Enable(bool enable)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            ConfigurePxrManagerForVst();
            var managerOk = TrySetManagerSeeThrough(enable);
            var boundaryOk = TrySetBoundarySeeThrough(enable);
            if (!managerOk && !boundaryOk)
            {
                Debug.LogError(
                    "[MagicMR] No PICO see-through API found " +
                    "(PXR_Manager.EnableVideoSeeThrough / PXR_Boundary.EnableSeeThroughManual).");
            }
            else
            {
                Debug.Log(
                    $"[MagicMR] Video see-through set to {enable} " +
                    $"(manager={managerOk}, boundary={boundaryOk}).");
            }
#else
            Debug.Log($"[MagicMR] Video see-through skipped on this platform (enable={enable}).");
#endif
        }

        static bool TrySetManagerSeeThrough(bool enable)
        {
            try
            {
                var managerType = ResolveType(s_ManagerTypeNames, "Unity.XR.PXR.PXR_Manager");
                if (managerType == null)
                    return false;

                // Prefer static property: PXR_Manager.EnableVideoSeeThrough = true
                var prop = managerType.GetProperty(
                    "EnableVideoSeeThrough",
                    BindingFlags.Public | BindingFlags.Static);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(null, enable);
                    return true;
                }

                // Instance property on the scene singleton
                var instanceProp = managerType.GetProperty(
                    "EnableVideoSeeThrough",
                    BindingFlags.Public | BindingFlags.Instance);
                if (instanceProp != null && instanceProp.CanWrite)
                {
                    var instance = UnityEngine.Object.FindFirstObjectByType(managerType) as Component;
                    if (instance != null)
                    {
                        instanceProp.SetValue(instance, enable);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[MagicMR] PXR_Manager see-through failed: {exception.Message}");
                return false;
            }
        }

        static bool TrySetBoundarySeeThrough(bool enable)
        {
            try
            {
                var boundaryType = ResolveType(s_BoundaryTypeNames, "Unity.XR.PXR.PXR_Boundary");
                if (boundaryType == null)
                    return false;

                var method = boundaryType.GetMethod(
                    "EnableSeeThroughManual",
                    BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                    return false;

                method.Invoke(null, new object[] { enable });
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[MagicMR] PXR_Boundary see-through failed: {exception.Message}");
                return false;
            }
        }

        static Type ResolveType(string[] assemblyQualifiedNames, string shortName)
        {
            foreach (var typeName in assemblyQualifiedNames)
            {
                var type = Type.GetType(typeName);
                if (type != null)
                    return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(shortName);
                if (type != null)
                    return type;
            }

            return null;
        }

        public static IEnumerator EnableWithRetry(float startDelay = 0.15f, int attempts = 8, float interval = 0.35f)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (startDelay > 0f)
                yield return new WaitForSeconds(startDelay);

            DisableOpaqueEnvironment();
            for (var i = 0; i < attempts; i++)
            {
                ConfigureAllXrCameras();
                ConfigurePxrManagerForVst();
                Enable(true);
                yield return new WaitForSeconds(interval);
            }
#else
            yield break;
#endif
        }

        public static void OnApplicationResumed()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            DisableOpaqueEnvironment();
            ConfigureAllXrCameras();
            ConfigurePxrManagerForVst();
            Enable(true);
#endif
        }
    }
}
