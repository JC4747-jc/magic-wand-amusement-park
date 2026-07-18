using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace MagicMR
{
    /// <summary>
    /// Thin reflection wrapper around PICO's native spatial anchor API
    /// (Unity.XR.PXR.PXR_MixedReality / PXR_SpatialAnchor). MagicMR's assembly
    /// definition has no direct reference to the PICO SDK assembly, so every
    /// call here is resolved by type/member name at runtime (see
    /// PicoVideoSeeThrough for the same pattern applied to VST).
    ///
    /// PXR_SpatialAnchor is a drop-in MonoBehaviour: once added to a GameObject
    /// it creates a native anchor at that transform's current pose and re-syncs
    /// the transform to the tracked anchor pose every frame. We only need to
    /// (1) start the "SpatialAnchor" sense data provider once, and
    /// (2) add/remove that component when the user (re)calibrates.
    /// </summary>
    public static class PicoSpatialAnchor
    {
        const string k_MixedRealityType = "Unity.XR.PXR.PXR_MixedReality";
        const string k_SpatialAnchorType = "Unity.XR.PXR.PXR_SpatialAnchor";
        const string k_SenseDataProviderTypeEnum = "Unity.XR.PXR.PxrSenseDataProviderType";

        static bool s_ProviderStarted;

        public static void StartProvider()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (s_ProviderStarted)
                return;

            try
            {
                var mixedRealityType = ResolveType(k_MixedRealityType);
                var enumType = ResolveType(k_SenseDataProviderTypeEnum);
                if (mixedRealityType == null || enumType == null)
                {
                    Debug.LogError("[MagicMR] PXR_MixedReality/PxrSenseDataProviderType not found; spatial anchors unavailable.");
                    return;
                }

                var method = mixedRealityType.GetMethod("StartSenseDataProvider", BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    Debug.LogError("[MagicMR] StartSenseDataProvider not found on PXR_MixedReality.");
                    return;
                }

                // Unity.XR.PXR.PxrSenseDataProviderType.SpatialAnchor == 0.
                var spatialAnchorEnumValue = Enum.ToObject(enumType, 0);
                var parameters = method.GetParameters();
                var args = new object[parameters.Length];
                args[0] = spatialAnchorEnumValue;
                for (var i = 1; i < parameters.Length; i++)
                    args[i] = Type.Missing;

                // Fire-and-forget: this returns a Task<PxrResult>, but we don't
                // need to await it here (PXR_SpatialAnchor components can be
                // added right after; the native side queues creation).
                method.Invoke(null, BindingFlags.OptionalParamBinding, null, args, null);
                s_ProviderStarted = true;
                Debug.Log("[MagicMR] Started PXR spatial anchor sense data provider.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[MagicMR] Failed to start spatial anchor provider: {exception.Message}");
            }
#endif
        }

        /// <summary>
        /// Replaces any existing native anchor on the object with a fresh one at
        /// its current transform. Call this after positioning the object.
        /// </summary>
        public static bool AttachAnchor(GameObject target)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (target == null)
                return false;

            try
            {
                var anchorType = ResolveType(k_SpatialAnchorType);
                if (anchorType == null)
                {
                    Debug.LogError("[MagicMR] PXR_SpatialAnchor type not found.");
                    return false;
                }

                var existing = target.GetComponent(anchorType);
                if (existing != null)
                {
                    // Regular Destroy() is deferred to end-of-frame, but
                    // PXR_SpatialAnchor is [DisallowMultipleComponent], so
                    // AddComponent right below would throw if the old one is
                    // still technically present. Recalibration is a rare,
                    // user-triggered action (not a per-frame op), so an
                    // immediate destroy here is safe.
                    UnityEngine.Object.DestroyImmediate(existing);
                }

                target.AddComponent(anchorType);
                Debug.Log($"[MagicMR] Attached native spatial anchor to '{target.name}'.", target);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[MagicMR] Failed to attach spatial anchor: {exception.Message}");
                return false;
            }
#else
            Debug.Log($"[MagicMR] Spatial anchor skipped in editor for '{target?.name}'.");
            return false;
#endif
        }

        public static bool RemoveAnchor(GameObject target)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (target == null)
                return false;

            try
            {
                var anchorType = ResolveType(k_SpatialAnchorType);
                if (anchorType == null)
                    return false;

                var existing = target.GetComponent(anchorType);
                if (existing == null)
                    return false;

                UnityEngine.Object.DestroyImmediate(existing);
                Debug.Log($"[MagicMR] Removed native spatial anchor from '{target.name}'.", target);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[MagicMR] Failed to remove spatial anchor: {exception.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// Waits for a freshly-attached PXR_SpatialAnchor component to finish
        /// creating its native anchor, then persists it to on-device storage so
        /// it survives app restarts. Invokes <paramref name="onPersisted"/> with
        /// the anchor's UUID (to save e.g. in PlayerPrefs) on success, or with
        /// null on failure/timeout.
        /// </summary>
        public static IEnumerator PersistWhenReady(GameObject target, Action<string> onPersisted, float timeoutSeconds = 8f)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var anchorType = ResolveType(k_SpatialAnchorType);
            if (anchorType == null || target == null)
            {
                onPersisted?.Invoke(null);
                yield break;
            }

            var component = target.GetComponent(anchorType);
            if (component == null)
            {
                onPersisted?.Invoke(null);
                yield break;
            }

            var createdField = anchorType.GetField("Created", BindingFlags.Public | BindingFlags.Instance);
            var handleField = anchorType.GetField("anchorHandle", BindingFlags.Public | BindingFlags.Instance);
            var uuidField = anchorType.GetField("anchorUuid", BindingFlags.Public | BindingFlags.Instance);
            if (createdField == null || handleField == null || uuidField == null)
            {
                Debug.LogError("[MagicMR] PXR_SpatialAnchor is missing expected fields; SDK version mismatch?");
                onPersisted?.Invoke(null);
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                // The component may have been replaced by a recalibration
                // while we were waiting; bail out rather than persist a stale one.
                if (component == null || target.GetComponent(anchorType) != component)
                {
                    onPersisted?.Invoke(null);
                    yield break;
                }

                if ((bool)createdField.GetValue(component))
                    break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!(bool)createdField.GetValue(component))
            {
                Debug.LogWarning("[MagicMR] Timed out waiting for spatial anchor creation; not persisting.");
                onPersisted?.Invoke(null);
                yield break;
            }

            var handle = (ulong)handleField.GetValue(component);
            var uuid = (Guid)uuidField.GetValue(component);

            var mixedRealityType = ResolveType(k_MixedRealityType);
            var persistMethod = mixedRealityType?.GetMethod("PersistSpatialAnchorAsync", BindingFlags.Public | BindingFlags.Static);
            if (persistMethod == null)
            {
                Debug.LogError("[MagicMR] PersistSpatialAnchorAsync not found on PXR_MixedReality.");
                onPersisted?.Invoke(null);
                yield break;
            }

            Task task;
            try
            {
                var parameters = persistMethod.GetParameters();
                var args = new object[parameters.Length];
                args[0] = handle;
                for (var i = 1; i < parameters.Length; i++)
                    args[i] = Type.Missing;
                task = (Task)persistMethod.Invoke(null, BindingFlags.OptionalParamBinding, null, args, null);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[MagicMR] PersistSpatialAnchorAsync invoke failed: {exception.Message}");
                onPersisted?.Invoke(null);
                yield break;
            }

            while (!task.IsCompleted)
                yield return null;

            var success = TryGetSuccessResult(task, out _);
            Debug.Log($"[MagicMR] PersistSpatialAnchorAsync completed, success={success}, uuid={uuid}");
            onPersisted?.Invoke(success ? uuid.ToString() : null);
#else
            onPersisted?.Invoke(null);
            yield break;
#endif
        }

        /// <summary>
        /// Looks up a previously persisted anchor by UUID and returns the
        /// tracked GameObject the SDK creates for it (already self-updating its
        /// transform), via <paramref name="onLoaded"/>. Passes null if the
        /// anchor could not be found/loaded.
        /// </summary>
        public static IEnumerator TryLoadPersistedAnchor(string uuidString, Action<GameObject> onLoaded, float timeoutSeconds = 8f)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (string.IsNullOrEmpty(uuidString) || !Guid.TryParse(uuidString, out var uuid))
            {
                onLoaded?.Invoke(null);
                yield break;
            }

            var mixedRealityType = ResolveType(k_MixedRealityType);
            var queryMethod = mixedRealityType?.GetMethod("QuerySpatialAnchorObjectsAsync", BindingFlags.Public | BindingFlags.Static);
            if (queryMethod == null)
            {
                Debug.LogError("[MagicMR] QuerySpatialAnchorObjectsAsync not found on PXR_MixedReality.");
                onLoaded?.Invoke(null);
                yield break;
            }

            Task task;
            try
            {
                var parameters = queryMethod.GetParameters();
                var args = new object[parameters.Length];
                args[0] = new[] { uuid };
                for (var i = 1; i < parameters.Length; i++)
                    args[i] = Type.Missing;
                task = (Task)queryMethod.Invoke(null, BindingFlags.OptionalParamBinding, null, args, null);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[MagicMR] QuerySpatialAnchorObjectsAsync invoke failed: {exception.Message}");
                onLoaded?.Invoke(null);
                yield break;
            }

            var elapsed = 0f;
            while (!task.IsCompleted && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!task.IsCompleted)
            {
                Debug.LogWarning("[MagicMR] Timed out querying persisted spatial anchor.");
                onLoaded?.Invoke(null);
                yield break;
            }

            if (!TryGetSuccessResult(task, out var resultTuple))
            {
                Debug.LogWarning("[MagicMR] QuerySpatialAnchorObjectsAsync returned a failure result.");
                onLoaded?.Invoke(null);
                yield break;
            }

            var objectsField = resultTuple.GetType().GetField("Item2");
            var loadedObjects = objectsField?.GetValue(resultTuple) as List<GameObject>;
            var loaded = loadedObjects != null && loadedObjects.Count > 0 ? loadedObjects[0] : null;

            Debug.Log(loaded != null
                ? $"[MagicMR] Loaded persisted spatial anchor {uuid} -> '{loaded.name}'."
                : $"[MagicMR] No persisted spatial anchor found for {uuid}.");
            onLoaded?.Invoke(loaded);
#else
            onLoaded?.Invoke(null);
            yield break;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Reads a completed Task&lt;(PxrResult, T)&gt; or Task&lt;PxrResult&gt;
        /// via reflection and reports whether the PxrResult was SUCCESS (0).
        /// </summary>
        static bool TryGetSuccessResult(Task task, out object resultTuple)
        {
            resultTuple = null;

            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[MagicMR] PXR task did not complete successfully: {task.Exception?.GetBaseException().Message}");
                return false;
            }

            try
            {
                var resultProperty = task.GetType().GetProperty("Result");
                if (resultProperty == null)
                    return false;

                var result = resultProperty.GetValue(task);
                if (result == null)
                    return false;

                // Either a bare PxrResult enum, or a ValueTuple whose Item1 is one.
                var item1Field = result.GetType().GetField("Item1");
                var pxrResult = item1Field != null ? item1Field.GetValue(result) : result;
                resultTuple = result;

                return Convert.ToInt32(pxrResult) == 0; // PxrResult.SUCCESS
            }
            catch (Exception exception)
            {
                Debug.LogError($"[MagicMR] Failed to read PXR task result: {exception.Message}");
                return false;
            }
        }
#endif

        static Type ResolveType(string fullName)
        {
            var type = Type.GetType(fullName);
            if (type != null)
                return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(fullName);
                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
