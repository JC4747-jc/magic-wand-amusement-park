using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    /// <summary>World-space, pooled spell strokes. Preview never dispatches gameplay actions.</summary>
    public sealed class RightHandSpellVfx : MonoBehaviour
    {
        const int Capacity = 80;
        [Header("Passthrough visibility")]
        [SerializeField, Range(.5f, 4f)] float m_EffectScale = 2.5f;
        [SerializeField, Range(.5f, 3f)] float m_CastDurationScale = 1.8f;
        [SerializeField, Range(.5f, 3f)] float m_Brightness = 1.6f;
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        sealed class Stroke
        {
            public LineRenderer line;
            public Vector3 origin, destination, drift;
            public Color color;
            public float start, life, radius, phase;
            public long order;
            public int shape; // 0 spark, 1 ring, 2 petal, 3 transfer, 4 thread
        }
        readonly List<Stroke> pool = new List<Stroke>(Capacity);
        readonly List<Vector3> trace = new List<Vector3>(128);
        Material material;
        LineRenderer trail;
        float lastSample = -100, nextPreview, traceStart;
        Vector3 palm, index, thumb, previousPalm, velocity;
        bool tracing;
        long emittedOrder;
        static readonly Color Fire = new Color(1f, .32f, .055f);
        static readonly Color Life = new Color(.25f, 1f, .65f);
        static readonly Color Wind = new Color(.65f, .88f, 1f);
        static readonly Color Gold = new Color(1f, .78f, .28f);

        public static RightHandSpellVfx GetOrCreate(GameObject owner)
        {
            var fx = owner.GetComponent<RightHandSpellVfx>();
            return fx != null ? fx : owner.AddComponent<RightHandSpellVfx>();
        }

        void Awake()
        {
            if (material != null) return;
            var shader = Resources.Load<Shader>("MagicMR/HandSpell");
            if (shader == null) { enabled = false; Debug.LogError("HandSpell shader missing", this); return; }
            material = new Material(shader);
            material.SetFloat(IntensityId, m_Brightness);
            trail = MakeLine("Finger circle trail");
            trail.widthMultiplier = .0018f * m_EffectScale;
        }

        LineRenderer MakeLine(string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.enabled = false;
            return line;
        }

        public void Preview(Vector3 worldPalm, Vector3 worldIndex, Vector3 worldThumb,
            bool pointing, bool fist, bool pinching, bool open)
        {
            float now = Time.unscaledTime;
            float dt = now - lastSample;
            velocity = dt > 0 && dt < .15f ? (worldPalm - previousPalm) / dt : Vector3.zero;
            previousPalm = palm = worldPalm; index = worldIndex; thumb = worldThumb; lastSample = now;
            if (material == null) return;
            if (pointing && !fist && !pinching)
            {
                if (!tracing || now - traceStart > 3f) { trace.Clear(); traceStart = now; }
                tracing = true;
                if (trace.Count == 0 || Vector3.Distance(trace[trace.Count - 1], index) > .003f)
                {
                    if (trace.Count == 128) trace.RemoveAt(0);
                    trace.Add(index);
                }
                trail.positionCount = trace.Count;
                for (int n = 0; n < trace.Count; n++) trail.SetPosition(n, trace[n]);
                trail.startColor = new Color(Life.r, Life.g, Life.b, .12f);
                trail.endColor = Life; trail.enabled = trace.Count > 1;
            }
            else ClearTrace();
            if (now < nextPreview) return;
            nextPreview = now + .075f;
            if (fist) Emit(palm, palm, Gold, 0, .25f, .004f, Random.insideUnitSphere * .025f, false);
            else if (pinching) Emit((index + thumb) * .5f, index, Fire, 0, .16f, .003f, Vector3.up * .015f, false);
            else if (pointing) Emit(index, index, Life, 0, .16f, .002f, Vector3.zero, false);
            else if (open && velocity.magnitude > .3f)
                Emit(palm, palm, Wind, 0, .18f, .0015f, -velocity * .12f, false);
        }

        public void SummonPreview(Vector3 tip, float progress)
        {
            lastSample = Time.unscaledTime; thumb = tip;
            if (progress <= 0 || Time.unscaledTime < nextPreview) return;
            nextPreview = Time.unscaledTime + .07f;
            Emit(tip, tip, Gold, 1, .14f, .004f + .009f * progress, Vector3.zero, false);
        }

        public void Summon(Vector3 tip, Vector3 target)
        {
            Emit(tip, target, Gold, 3, .3f, .004f, Vector3.zero);
            Emit(target, target, Gold, 1, .55f, .055f, Vector3.zero);
        }

        public void Cast(EditDimension dimension, Vector3 target, Vector3 fallback, bool hasFallback)
        {
            bool fresh = Time.unscaledTime - lastSample <= .15f;
            if (!fresh && !hasFallback) return;
            Vector3 source = fresh ? palm : fallback;
            Color color = Gold;
            switch (dimension)
            {
                case EditDimension.Appearance: source = fresh ? (index + thumb) * .5f : source; color = Fire; break;
                case EditDimension.Agency: source = fresh ? index : source; color = Life; break;
                case EditDimension.Rule: color = Wind; break;
                case EditDimension.Deconstruction: color = Gold; break;
                case EditDimension.Scale: color = Gold; break;
                default: return;
            }
            ClearTrace();
            // Two visible, short-lived rings acknowledge an accepted cast.
            // Geometry and shader falloff provide the glow without passthrough bloom.
            Emit(source, source, color, 1, .46f, .028f, Vector3.zero);
            Emit(source, source, color, 1, .34f, .018f, Vector3.zero);
            for (int i = 0; i < 6; i++)
                Emit(source + Random.insideUnitSphere * .009f, source, color, 0,
                    Random.Range(.18f, .36f), .0025f, Random.onUnitSphere * .035f);
            Emit(source, target, color, dimension == EditDimension.Scale ? 4 : 3, .32f, .003f, Vector3.zero);
            Emit(target, target, color, 1, .48f, .045f, Vector3.zero);
            if (dimension == EditDimension.Scale)
            {
#if XR_HANDS_1_1_OR_NEWER
                var systems = new List<XRHandSubsystem>();
                SubsystemManager.GetSubsystems(systems);
                foreach (var system in systems)
                    if (system.running && system.leftHand.isTracked && system.leftHand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var pose))
                    {
                        var root = Camera.main != null ? Camera.main.transform.parent : null;
                        Emit(root != null ? root.TransformPoint(pose.position) : pose.position, target, color, 4, .5f, .002f, Vector3.zero);
                        break;
                    }
#endif
                return;
            }
            int count = dimension == EditDimension.Deconstruction ? 14 : 9;
            for (int i = 0; i < count; i++)
            {
                Vector3 drift = Random.onUnitSphere * Random.Range(.035f, .10f);
                if (dimension == EditDimension.Rule) drift = (velocity.sqrMagnitude > .01f ? velocity.normalized : (target - source).normalized) * .22f;
                Emit(source + Random.insideUnitSphere * .012f, target, color,
                    dimension == EditDimension.Deconstruction ? 2 : 0,
                    Random.Range(.3f, .65f), dimension == EditDimension.Deconstruction ? .009f : .002f, drift);
            }
        }

        void Emit(Vector3 origin, Vector3 destination, Color color, int shape, float life, float radius, Vector3 drift,
            bool acceptedCast = true)
        {
            if (material == null || !isActiveAndEnabled) return;
            Stroke s = null;
            foreach (var candidate in pool) if (!candidate.line.enabled) { s = candidate; break; }
            if (s == null)
            {
                if (pool.Count >= Capacity)
                {
                    // Preview never evicts cast feedback. A new accepted action
                    // must still be visible when earlier, longer pulses overlap.
                    if (!acceptedCast) return;
                    s = pool[0];
                    foreach (var candidate in pool) if (candidate.order < s.order) s = candidate;
                }
                else { s = new Stroke { line = MakeLine("Spell stroke") }; pool.Add(s); }
            }
            s.origin = origin; s.destination = destination; s.color = color; s.shape = shape;
            s.order = ++emittedOrder;
            float scale = acceptedCast ? m_EffectScale : Mathf.Sqrt(m_EffectScale);
            s.start = Time.unscaledTime; s.life = life * (acceptedCast ? m_CastDurationScale : 1f);
            s.radius = radius * scale; s.drift = drift;
            s.phase = Random.value * Mathf.PI * 2;
            s.line.positionCount = shape == 0 ? 2 : 25;
            s.line.widthMultiplier = (shape == 2 ? .003f : shape == 1 ? .0025f : radius) * scale;
            var camera = Camera.main;
            // Initialize before enabling a new or recycled line, so it cannot
            // flash at the world origin or its previous cast position for a frame.
            Draw(s, 0f, camera != null ? camera.transform.right : Vector3.right,
                camera != null ? camera.transform.up : Vector3.up);
            s.line.enabled = true;
        }

        void Update()
        {
            if (material != null) material.SetFloat(IntensityId, m_Brightness);
            if (trail != null) trail.widthMultiplier = .0018f * m_EffectScale;
            if (Time.unscaledTime - lastSample > .15f) ClearTrace();
            var camera = Camera.main;
            Vector3 right = camera != null ? camera.transform.right : Vector3.right;
            Vector3 up = camera != null ? camera.transform.up : Vector3.up;
            foreach (var s in pool)
            {
                if (!s.line.enabled) continue;
                float t = (Time.unscaledTime - s.start) / s.life;
                if (t >= 1) { s.line.enabled = false; continue; }
                Draw(s, t, right, up);
            }
        }

        static void Draw(Stroke s, float t, Vector3 right, Vector3 up)
        {
            // Hold the visible core briefly, then fade smoothly. Avoid a tiny
            // ring at onset followed by a large but already invisible ring.
            float fade = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(.15f, 1f, t));
            Color c = s.color; c.a = fade * .85f;
            s.line.startColor = s.line.endColor = c;
            Vector3 center = s.origin + s.drift * t;
            for (int n = 0; n < s.line.positionCount; n++)
            {
                float u = n / (float)(s.line.positionCount - 1);
                float a = u * Mathf.PI * 2 + s.phase;
                Vector3 p;
                if (s.shape == 0) p = center + (u - .5f) * (up * s.radius * 2 - s.drift * .2f);
                else if (s.shape == 1) p = center + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * s.radius * (.6f + t * .6f);
                else if (s.shape == 2)
                {
                    center = Vector3.Lerp(s.origin, s.destination, t * t) + s.drift * Mathf.Sin(t * Mathf.PI);
                    p = center + right * Mathf.Sin(a) * s.radius * .5f + up * Mathf.Cos(a) * s.radius;
                }
                else if (s.shape == 3)
                {
                    float travel = Mathf.Clamp01(t * 1.5f - u * .18f);
                    p = Vector3.Lerp(s.origin, s.destination, travel) + up * Mathf.Sin(travel * Mathf.PI) * .025f;
                }
                else p = Vector3.Lerp(s.origin, s.destination, u) + up * Mathf.Sin(u * Mathf.PI) * .02f * (1 - t);
                s.line.SetPosition(n, p);
            }
        }

        void ClearTrace() { tracing = false; trace.Clear(); if (trail != null) trail.enabled = false; }
        public void Clear()
        {
            ClearTrace(); lastSample = -100; velocity = Vector3.zero;
            foreach (var s in pool) s.line.enabled = false;
        }
        void OnDisable() { Clear(); }
        void OnApplicationPause(bool paused) { if (paused) Clear(); }
        void OnDestroy()
        {
            foreach (var s in pool) if (s.line != null) Release(s.line.gameObject);
            if (trail != null) Release(trail.gameObject);
            if (material != null) Release(material);
        }
        static void Release(Object resource)
        {
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }
    }
}
