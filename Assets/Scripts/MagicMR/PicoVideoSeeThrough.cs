using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MagicMR
{
    /// <summary>
    /// Enables PICO video see-through (passthrough) at runtime.
    /// Camera alpha=0 alone is not enough; PXR_Boundary must be called on device.
    /// </summary>
    public static class PicoVideoSeeThrough
    {
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
        }

        public static void Enable(bool enable)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var boundaryType = ResolveBoundaryType();
                if (boundaryType == null)
                {
                    Debug.LogError("[MagicMR] PXR_Boundary type not found. See-through API unavailable.");
                    return;
                }

                var method = boundaryType.GetMethod(
                    "EnableSeeThroughManual",
                    BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    Debug.LogError("[MagicMR] EnableSeeThroughManual not found on PXR_Boundary.");
                    return;
                }

                method.Invoke(null, new object[] { enable });
                Debug.Log($"[MagicMR] Video see-through set to {enable}.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[MagicMR] Failed to enable video see-through: {exception.Message}");
            }
#else
            Debug.Log($"[MagicMR] Video see-through skipped on this platform (enable={enable}).");
#endif
        }

        static Type ResolveBoundaryType()
        {
            foreach (var typeName in s_BoundaryTypeNames)
            {
                var type = Type.GetType(typeName);
                if (type != null)
                    return type;
            }

            // Fallback scan in case assembly name changes across SDK versions.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("Unity.XR.PXR.PXR_Boundary");
                if (type != null)
                    return type;
            }

            return null;
        }

        public static IEnumerator EnableAfterDelay(float delaySeconds, bool enable = true)
        {
            if (delaySeconds > 0f)
                yield return new WaitForSeconds(delaySeconds);

            Enable(enable);
        }

        public static IEnumerator EnableWithRetry(float startDelay = 0.15f, int attempts = 8, float interval = 0.35f)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (startDelay > 0f)
                yield return new WaitForSeconds(startDelay);

            for (var i = 0; i < attempts; i++)
            {
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
            Enable(true);
#endif
        }
    }
}
