using UnityEngine;

namespace MagicMR
{
    /// <summary>Plays authored transparent sprite frames in world metres at an explicit anchor.</summary>
    [DisallowMultipleComponent]
    public sealed class AffectiveFramePlayer : MonoBehaviour
    {
        GameObject root;
        SpriteRenderer visual;
        AudioSource sound;
        Transform anchor;
        Sprite[] frames;
        float started, duration, width;
        Vector3 offset;
        public bool IsPlaying => root != null;

        public void Play(Transform target, Sprite[] sequence, AudioClip clip, float seconds,
            float widthMeters, Vector3 offsetMeters)
        {
            Stop();
            if (target == null || sequence == null || sequence.Length == 0) return;
            foreach (var frame in sequence) if (frame == null) return;
            if (!float.IsFinite(seconds) || !float.IsFinite(widthMeters)) return;
            anchor = target;
            frames = sequence;
            duration = Mathf.Max(.1f, seconds);
            width = Mathf.Max(.01f, widthMeters);
            offset = offsetMeters;
            root = new GameObject("Affective authored animation");
            visual = root.AddComponent<SpriteRenderer>();
            sound = root.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 1f;
            sound.clip = clip;
            started = Time.unscaledTime;
            LateUpdate();
            if (clip != null) sound.Play();
        }

        void LateUpdate()
        {
            if (root == null) return;
            if (anchor == null || !anchor.gameObject.activeInHierarchy || Time.unscaledTime - started >= duration)
            { Stop(); return; }
            int index = Mathf.Min(frames.Length - 1,
                Mathf.FloorToInt((Time.unscaledTime - started) / duration * frames.Length));
            visual.sprite = frames[index];
            root.transform.position = anchor.position + anchor.rotation * offset;
            // SpriteRenderer's front is local -Z. Keep the plane parallel to the view.
            var camera = Camera.main;
            if (camera != null) root.transform.rotation = camera.transform.rotation;
            root.transform.localScale = Vector3.one * (width / Mathf.Max(.0001f, visual.sprite.bounds.size.x));
        }

        public void Stop()
        {
            if (root != null)
            {
                root.SetActive(false);
                if (Application.isPlaying) Destroy(root); else DestroyImmediate(root);
            }
            root = null;
            visual = null;
            sound = null;
            frames = null;
            anchor = null;
        }
        void OnDisable() => Stop();
        void OnDestroy() => Stop();
    }
}
