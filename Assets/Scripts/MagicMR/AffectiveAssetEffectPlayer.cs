using UnityEngine;

namespace MagicMR
{
    /// <summary>Plays an optional Asset Store particle, Animator, or VFX prefab at the anchored subject.</summary>
    [DisallowMultipleComponent]
    public sealed class AffectiveAssetEffectPlayer : MonoBehaviour
    {
        GameObject instance;
        Transform anchor;
        Vector3 offset;
        float endsAt;
        public bool IsPlaying => instance != null;

        public void Play(GameObject prefab, Transform target, Vector3 offsetMeters, float seconds)
        {
            Stop();
            if (prefab == null || target == null || !float.IsFinite(seconds)) return;
            anchor = target;
            offset = offsetMeters;
            endsAt = Time.unscaledTime + Mathf.Max(.1f, seconds);
            instance = Instantiate(prefab, anchor.position + anchor.rotation * offset, anchor.rotation);
            instance.name = "Affective Asset Store effect";
            LateUpdate();
        }

        void LateUpdate()
        {
            if (instance == null) return;
            if (anchor == null || !anchor.gameObject.activeInHierarchy || Time.unscaledTime >= endsAt)
            { Stop(); return; }
            instance.transform.SetPositionAndRotation(anchor.position + anchor.rotation * offset, anchor.rotation);
        }

        public void Stop()
        {
            if (instance != null)
            {
                if (Application.isPlaying) Destroy(instance); else DestroyImmediate(instance);
            }
            instance = null;
            anchor = null;
        }

        void OnDisable() => Stop();
        void OnDestroy() => Stop();
    }
}
