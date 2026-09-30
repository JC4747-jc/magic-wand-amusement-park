using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagicMR
{
    /// <summary>
    /// The visual story told by an <see cref="Affective2DOverlayAnimator"/>.
    /// These are intentionally presentation semantics rather than interaction semantics:
    /// a gesture can keep its meaning while a study selects the emotional image it reveals.
    /// </summary>
    public enum AffectiveMotif
    {
        None = 0,
        Bloom = 1,
        Comfort = 2,
        Playful = 3,
        Surprise = 4,
        Calm = 5,
        Warning = 6,
        Aversion = 7,
        Memory = 8,
        Dissolve = 9,
        LighterChar = 10,
        LighterDemon = 11,
        LighterRefusal = 12,
        LighterPurify = 13
    }

    /// <summary>
    /// Creates a short-lived, procedural 2D animation over an anchored real-world object.
    ///
    /// The overlay is made only from runtime meshes and an unlit material; it therefore has
    /// no prefab, texture, Sprite, or package dependency.  Every node is billboarded toward
    /// the selected camera, so the imagery reads as a flat illustrated layer in MR rather
    /// than as a new 3D object. Calling any Play overload always replaces the previous run.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Affective2DOverlayAnimator : MonoBehaviour
    {
        enum Shape
        {
            Quad,
            Disc,
            Ring,
            Heart,
            Star
        }

        enum Motion
        {
            Pop,
            Float,
            Orbit,
            Burst,
            Pulse,
            Drift,
            Shrink
        }

        sealed class Node
        {
            public Transform transform;
            public MeshRenderer renderer;
            public MaterialPropertyBlock properties;
            public Color color;
            public Vector3 origin;
            public Vector3 direction;
            public float delay;
            public float duration;
            public float phase;
            public float rotation;
            public float rotationSpeed;
            public float scale;
            public Motion motion;
            public Vector2 aspect = Vector2.one;
        }

        const string RuntimeRootName = "Affective 2D Overlay (Runtime)";
        const float MinimumDuration = 0.05f;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Anchor")]
        [Tooltip("Defaults to this component's Transform when empty.")]
        [SerializeField] Transform m_Anchor;

        [Tooltip("Defaults to Camera.main when empty. Use the MR camera here when it is not tagged MainCamera.")]
        [SerializeField] Camera m_BillboardCamera;

        [Tooltip("Offset in world metres, rotated with the anchor but deliberately unaffected by its scale. A positive Y usually keeps the illustration above an object.")]
        [SerializeField] Vector3 m_LocalOffset = new Vector3(0f, 0.055f, 0f);

        [Header("Playback")]
        [SerializeField] AffectiveMotif m_DefaultMotif = AffectiveMotif.Bloom;

        [SerializeField, Min(MinimumDuration)] float m_DefaultDuration = 2.25f;

        [Tooltip("Approximate width/height in metres of a full-size illustrated motif.")]
        [SerializeField, Min(0.001f)] float m_BaseSize = 0.12f;

        [SerializeField] bool m_UseUnscaledTime = true;

        [SerializeField] bool m_Loop;

        [Tooltip("Removes generated meshes and material when an animation completes. Keep enabled for trial-by-trial studies.")]
        [SerializeField] bool m_ClearWhenFinished = true;

        [Header("Optional Preview")]
        [SerializeField] bool m_PlayOnEnable;

        GameObject m_RuntimeRoot;
        Material m_RuntimeMaterial;
        Mesh m_QuadMesh;
        Mesh m_DiscMesh;
        Mesh m_RingMesh;
        Mesh m_HeartMesh;
        Mesh m_StarMesh;
        readonly List<Node> m_Nodes = new List<Node>(24);

        float m_StartTime;
        float m_Duration;
        bool m_IsPlaying;
        AffectiveMotif m_CurrentMotif;
        string m_CurrentCaption = string.Empty;
        Color m_PrimaryColor;
        bool m_HasPrimaryColor;
        Camera m_ResolvedCamera;
        float m_NextCameraSearchTime;

        /// <summary>Whether a motif is currently being animated.</summary>
        public bool IsPlaying => m_IsPlaying;

        /// <summary>The motif selected by the most recent <see cref="Play"/> call.</summary>
        public AffectiveMotif CurrentMotif => m_CurrentMotif;

        /// <summary>Optional caller-supplied identifier, useful for study logs and hierarchy inspection.</summary>
        public string CurrentCaption => m_CurrentCaption;

        /// <summary>The transform to which the generated illustration is currently attached.</summary>
        public Transform Anchor => m_Anchor != null ? m_Anchor : transform;

        void OnEnable()
        {
            if (m_PlayOnEnable)
                Play(Anchor, m_DefaultMotif, m_DefaultDuration, null);
        }

        void Update()
        {
            if (!m_IsPlaying)
                return;

            // A destroyed tracked-object anchor must not leave an orphan visual behind.
            if (m_Anchor == null || m_RuntimeRoot == null)
            {
                Stop();
                return;
            }

            var elapsed = Now - m_StartTime;
            if (!m_Loop && elapsed >= m_Duration)
            {
                Finish();
                return;
            }

            var normalizedTime = m_Loop
                ? Mathf.Repeat(elapsed, m_Duration) / m_Duration
                : Mathf.Clamp01(elapsed / m_Duration);
            Animate(normalizedTime);
        }

        void LateUpdate()
        {
            FollowAnchor();
            Billboard();
        }

        void OnDisable()
        {
            Stop();
        }

        void OnDestroy()
        {
            ClearRuntime();
        }

        /// <summary>Plays the default motif at this component's anchor.</summary>
        public void Play()
        {
            Play(Anchor, m_DefaultMotif, m_DefaultDuration, null);
        }

        /// <summary>Plays a motif at this component's anchor.</summary>
        public void Play(AffectiveMotif motif)
        {
            Play(Anchor, motif, m_DefaultDuration, null);
        }

        /// <summary>
        /// Simple integration entry point. The caption is optional metadata only; it does
        /// not alter the visual and is safe to use for condition or event identifiers.
        /// </summary>
        public void Play(Transform anchor, AffectiveMotif motif, string caption)
        {
            Play(anchor, motif, m_DefaultDuration, caption);
        }

        /// <summary>
        /// Plays a procedural 2D motif on <paramref name="anchor"/>. Calling this while a
        /// previous motif is visible safely clears that run before the new one is constructed.
        /// </summary>
        public void Play(Transform anchor, AffectiveMotif motif, float duration, string caption = null)
        {
            PlayInternal(anchor, motif, duration, caption, default, false);
        }

        /// <summary>
        /// Integration entry point for condition-specific overlays. <paramref name="primaryColor"/>
        /// tints the generated illustration while keeping its motif-specific silhouette and motion.
        /// </summary>
        public void Play(Transform anchor, AffectiveMotif motif, Color primaryColor, string caption)
        {
            PlayInternal(anchor, motif, m_DefaultDuration, caption, primaryColor, true);
        }

        /// <summary>Variant of the coloured integration entry point with an explicit duration.</summary>
        public void Play(Transform anchor, AffectiveMotif motif, Color primaryColor, float duration, string caption = null)
        {
            PlayInternal(anchor, motif, duration, caption, primaryColor, true);
        }

        void PlayInternal(Transform anchor, AffectiveMotif motif, float duration, string caption, Color primaryColor, bool hasPrimaryColor)
        {
            Stop();

            m_Anchor = anchor != null ? anchor : transform;
            m_CurrentMotif = motif;
            m_CurrentCaption = caption ?? string.Empty;
            m_PrimaryColor = primaryColor;
            m_HasPrimaryColor = hasPrimaryColor;
            m_Duration = Mathf.Max(MinimumDuration, duration > 0f ? duration : m_DefaultDuration);

            if (motif == AffectiveMotif.None || m_Anchor == null)
                return;

            if (!CreateRuntime())
                return;

            // Repeatable prototypes without changing the rest of the game's random stream.
            var randomState = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(1701 + (int)motif);
                BuildMotif(motif);
            }
            finally { UnityEngine.Random.state = randomState; }
            if (m_Nodes.Count == 0)
            {
                ClearRuntime();
                return;
            }

            m_StartTime = Now;
            m_IsPlaying = true;
            Billboard();
            Animate(0f);
        }

        /// <summary>Stops playback and removes all generated runtime objects immediately.</summary>
        public void Stop()
        {
            m_IsPlaying = false;
            m_CurrentMotif = AffectiveMotif.None;
            m_CurrentCaption = string.Empty;
            m_HasPrimaryColor = false;
            ClearRuntime();
        }

        /// <summary>Changes the default anchor used by future Play calls.</summary>
        public void SetAnchor(Transform anchor)
        {
            m_Anchor = anchor != null ? anchor : transform;
        }

        public void ConfigurePlacement(float widthMeters, Vector3 offsetMeters)
        {
            if (float.IsFinite(widthMeters)) m_BaseSize = Mathf.Max(.01f, widthMeters);
            m_LocalOffset = offsetMeters;
        }

        float Now => m_UseUnscaledTime ? Time.unscaledTime : Time.time;

        void Finish()
        {
            m_IsPlaying = false;
            if (m_ClearWhenFinished)
                ClearRuntime();
            else if (m_RuntimeRoot != null)
                m_RuntimeRoot.SetActive(false);
        }

        bool CreateRuntime()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogWarning("Affective2DOverlayAnimator could not find an unlit shader.", this);
                return false;
            }

            m_RuntimeMaterial = new Material(shader)
            {
                name = "Affective 2D Overlay Material (Runtime)",
                renderQueue = (int)RenderQueue.Transparent
            };
            ConfigureTransparent(m_RuntimeMaterial);

            m_QuadMesh = CreateQuadMesh();
            m_DiscMesh = CreateDiscMesh(24);
            m_RingMesh = CreateRingMesh(32, 0.68f);
            m_HeartMesh = CreateHeartMesh(32);
            m_StarMesh = CreateStarMesh(5, 0.48f);

            m_RuntimeRoot = new GameObject(RuntimeRootName);
            // Do not parent under the detected object. Detection targets are often scaled to
            // physical size (for example, a 0.08-scale lighter), which would silently shrink
            // a 12 cm overlay into a 1 cm one. Instead, follow its world pose every frame.
            m_RuntimeRoot.transform.SetPositionAndRotation(OverlayWorldPosition(), Quaternion.identity);
            m_RuntimeRoot.transform.localScale = Vector3.one;
            if (!string.IsNullOrWhiteSpace(m_CurrentCaption))
                m_RuntimeRoot.name = RuntimeRootName + " — " + m_CurrentCaption;
            return true;
        }

        Vector3 OverlayWorldPosition()
        {
            if (m_Anchor == null)
                return transform.position;
            // Rotation preserves a meaningful "above/left of the object" relationship while
            // excluding lossyScale, so all size fields in this component remain world metres.
            return m_Anchor.position + m_Anchor.rotation * m_LocalOffset;
        }

        void FollowAnchor()
        {
            if (m_RuntimeRoot != null && m_Anchor != null)
                m_RuntimeRoot.transform.position = OverlayWorldPosition();
        }

        void BuildMotif(AffectiveMotif motif)
        {
            switch (motif)
            {
                case AffectiveMotif.Bloom:
                    BuildBloom();
                    break;
                case AffectiveMotif.Comfort:
                    BuildComfort();
                    break;
                case AffectiveMotif.Playful:
                    BuildPlayful();
                    break;
                case AffectiveMotif.Surprise:
                    BuildSurprise();
                    break;
                case AffectiveMotif.Calm:
                    BuildCalm();
                    break;
                case AffectiveMotif.Warning:
                    BuildWarning();
                    break;
                case AffectiveMotif.Aversion:
                    BuildAversion();
                    break;
                case AffectiveMotif.Memory:
                    BuildMemory();
                    break;
                case AffectiveMotif.Dissolve:
                    BuildDissolve();
                    break;
                case AffectiveMotif.LighterChar: BuildLighterChar(); break;
                case AffectiveMotif.LighterDemon: BuildLighterDemon(); break;
                case AffectiveMotif.LighterRefusal: BuildLighterRefusal(); break;
                case AffectiveMotif.LighterPurify: BuildLighterPurify(); break;
            }
        }

        void BuildBloom()
        {
            var pink = new Color(1f, 0.38f, 0.62f, 1f);
            for (var i = 0; i < 8; i++)
            {
                var angle = i * Mathf.PI * 2f / 8f;
                var node = AddNode("Petal", Shape.Heart, pink, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * m_BaseSize * 0.23f, .30f, Motion.Orbit);
                node.direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                node.phase = angle;
                node.rotation = angle * Mathf.Rad2Deg - 90f;
                node.rotationSpeed = i % 2 == 0 ? 15f : -15f;
            }

            AddNode("Bloom core", Shape.Disc, new Color(1f, .78f, .16f, 1f), Vector3.zero, .30f, Motion.Pop);
            for (var i = 0; i < 5; i++)
            {
                var angle = i * Mathf.PI * 2f / 5f + .3f;
                var node = AddNode("Sparkle", Shape.Star, new Color(1f, .91f, .4f, 1f), new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * m_BaseSize * .48f, .09f, Motion.Float, .15f + i * .05f);
                node.direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            }
        }

        void BuildComfort()
        {
            var coral = new Color(1f, .38f, .48f, 1f);
            for (var i = 0; i < 3; i++)
            {
                var x = (i - 1) * m_BaseSize * .26f;
                var node = AddNode("Comfort heart", Shape.Heart, coral, new Vector3(x, (i == 1 ? .04f : -.015f) * m_BaseSize, 0f), .31f - i * .035f, Motion.Pulse, i * .09f);
                node.phase = i * .8f;
                node.rotation = (i - 1) * 12f;
            }
            AddNode("Soft halo", Shape.Ring, new Color(1f, .72f, .72f, .72f), Vector3.zero, .73f, Motion.Pulse);
        }

        void BuildPlayful()
        {
            var face = AddNode("Playful face", Shape.Disc, new Color(1f, .83f, .18f, 1f), Vector3.zero, .74f, Motion.Pop);
            face.phase = .2f;
            AddNode("Eye left", Shape.Disc, new Color(.12f, .10f, .2f, 1f), new Vector3(-.17f, .10f, 0f) * m_BaseSize, .13f, Motion.Pulse);
            AddNode("Eye right", Shape.Disc, new Color(.12f, .10f, .2f, 1f), new Vector3(.17f, .10f, 0f) * m_BaseSize, .13f, Motion.Pulse, .08f);
            var mouth = AddNode("Smile", Shape.Ring, new Color(.12f, .10f, .2f, 1f), new Vector3(0f, -.14f, 0f) * m_BaseSize, .26f, Motion.Pulse);
            mouth.rotation = 180f;
            for (var i = 0; i < 4; i++)
            {
                var angle = i * Mathf.PI * .5f + .3f;
                var node = AddNode("Playful star", Shape.Star, new Color(.65f, .35f, 1f, 1f), new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * m_BaseSize * .62f, .11f, Motion.Orbit, .08f * i);
                node.phase = angle;
            }
        }

        void BuildSurprise()
        {
            AddNode("Surprise burst", Shape.Star, new Color(1f, .78f, .1f, 1f), Vector3.zero, .55f, Motion.Pop);
            for (var i = 0; i < 10; i++)
            {
                var angle = i * Mathf.PI * 2f / 10f;
                var node = AddNode("Surprise ray", Shape.Quad, new Color(1f, .55f, .08f, 1f), new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * m_BaseSize * .5f, .13f, Motion.Burst, i * .018f);
                node.direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                node.rotation = angle * Mathf.Rad2Deg - 90f;
            }
        }

        void BuildCalm()
        {
            for (var i = 0; i < 3; i++)
            {
                var ring = AddNode("Calm ripple", Shape.Ring, new Color(.24f, .78f, 1f, .8f), Vector3.zero, .42f + i * .18f, Motion.Pulse, i * .12f);
                ring.phase = i * .9f;
            }
            for (var i = 0; i < 6; i++)
            {
                var angle = i * Mathf.PI * 2f / 6f;
                var node = AddNode("Calm mote", Shape.Disc, new Color(.72f, .94f, 1f, .9f), new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * m_BaseSize * .42f, .075f, Motion.Drift, i * .07f);
                node.direction = new Vector3(Mathf.Cos(angle) * .2f, .7f + Mathf.Sin(angle) * .15f, 0f);
                node.phase = angle;
            }
        }

        void BuildWarning()
        {
            AddNode("Warning circle", Shape.Ring, new Color(1f, .18f, .15f, 1f), Vector3.zero, .95f, Motion.Pop);
            var slash = AddNode("Warning slash", Shape.Quad, new Color(1f, .18f, .15f, 1f), Vector3.zero, .72f, Motion.Pop, .04f);
            slash.rotation = -45f;
            slash.aspect = new Vector2(1.75f, .19f);
            for (var i = 0; i < 5; i++)
            {
                var angle = Mathf.Lerp(-2.55f, -.6f, i / 4f);
                var node = AddNode("Warning mark", Shape.Star, new Color(1f, .5f, .08f, 1f), new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * m_BaseSize * .65f, .105f, Motion.Burst, .05f * i);
                node.direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            }
        }

        void BuildAversion()
        {
            AddNode("Aversion cloud", Shape.Disc, new Color(.18f, .12f, .2f, .86f), new Vector3(-.16f, 0f, 0f) * m_BaseSize, .49f, Motion.Pulse);
            AddNode("Aversion cloud", Shape.Disc, new Color(.24f, .16f, .28f, .82f), new Vector3(.17f, .07f, 0f) * m_BaseSize, .44f, Motion.Pulse, .08f);
            AddNode("Aversion cloud", Shape.Disc, new Color(.18f, .25f, .16f, .86f), new Vector3(.04f, -.14f, 0f) * m_BaseSize, .40f, Motion.Pulse, .16f);
            var crossA = AddNode("Aversion cross", Shape.Quad, new Color(.78f, 1f, .22f, 1f), Vector3.zero, .54f, Motion.Pop, .14f);
            crossA.rotation = 45f;
            crossA.aspect = new Vector2(1.35f, .16f);
            var crossB = AddNode("Aversion cross", Shape.Quad, new Color(.78f, 1f, .22f, 1f), Vector3.zero, .54f, Motion.Pop, .14f);
            crossB.rotation = -45f;
            crossB.aspect = new Vector2(1.35f, .16f);
        }

        void BuildMemory()
        {
            AddNode("Memory frame", Shape.Ring, new Color(1f, .77f, .34f, .9f), Vector3.zero, .86f, Motion.Pulse);
            for (var i = 0; i < 7; i++)
            {
                var angle = i * Mathf.PI * 2f / 7f + .15f;
                var node = AddNode("Memory star", Shape.Star, new Color(1f, .89f, .48f, 1f), new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * m_BaseSize * (.30f + (i % 2) * .13f), .10f + (i % 3) * .024f, Motion.Float, .07f * i);
                node.direction = new Vector3(Mathf.Cos(angle) * .14f, .25f + Mathf.Sin(angle) * .10f, 0f);
                node.phase = angle;
            }
        }

        void BuildDissolve()
        {
            for (var i = 0; i < 18; i++)
            {
                var angle = i * Mathf.PI * 2f / 18f + UnityEngine.Random.Range(-.13f, .13f);
                var color = Color.Lerp(new Color(.75f, .45f, 1f, 1f), new Color(.25f, .85f, 1f, 1f), i / 17f);
                var node = AddNode("Dissolve fragment", i % 3 == 0 ? Shape.Star : Shape.Quad, color, Vector3.zero, UnityEngine.Random.Range(.06f, .13f), Motion.Burst, i * .012f);
                node.direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * UnityEngine.Random.Range(.58f, 1.15f);
                node.rotation = angle * Mathf.Rad2Deg;
                node.rotationSpeed = UnityEngine.Random.Range(-180f, 180f);
            }
        }

        // Lighter stages are intentionally recognisable as one causal story:
        // corrupted invitation -> alive tempter -> refusal -> purification.
        void BuildLighterChar()
        {
            AddNode("Sealed lighter", Shape.Disc, new Color(.12f, .11f, .14f, .92f), Vector3.zero, .72f, Motion.Pulse);
            AddNode("No flame ring", Shape.Ring, new Color(.72f, 1f, .22f, 1f), Vector3.zero, .92f, Motion.Pop);
            var a = AddNode("No flame slash", Shape.Quad, new Color(.72f, 1f, .22f, 1f), Vector3.zero, .78f, Motion.Pop, .06f);
            a.rotation = 45f; a.aspect = new Vector2(1.5f, .14f);
            var b = AddNode("No flame slash", Shape.Quad, new Color(.72f, 1f, .22f, 1f), Vector3.zero, .78f, Motion.Pop, .06f);
            b.rotation = -45f; b.aspect = new Vector2(1.5f, .14f);
        }

        void BuildLighterDemon()
        {
            AddNode("Demon head", Shape.Disc, new Color(.18f, .07f, .24f, 1f), Vector3.zero, .72f, Motion.Pulse);
            AddNode("Demon eye left", Shape.Star, new Color(1f, .2f, .08f, 1f), new Vector3(-.18f, .10f, 0f) * m_BaseSize, .16f, Motion.Pulse);
            AddNode("Demon eye right", Shape.Star, new Color(1f, .2f, .08f, 1f), new Vector3(.18f, .10f, 0f) * m_BaseSize, .16f, Motion.Pulse, .08f);
            var hornL = AddNode("Demon horn", Shape.Star, new Color(.72f, .28f, 1f, 1f), new Vector3(-.28f, .31f, 0f) * m_BaseSize, .24f, Motion.Pop); hornL.rotation = -32f;
            var hornR = AddNode("Demon horn", Shape.Star, new Color(.72f, .28f, 1f, 1f), new Vector3(.28f, .31f, 0f) * m_BaseSize, .24f, Motion.Pop); hornR.rotation = 32f;
        }

        void BuildLighterRefusal()
        {
            for (int i = 0; i < 5; i++)
            {
                var n = AddNode("Wind refusal", Shape.Quad, new Color(.65f, .9f, 1f, 1f), new Vector3(-.36f + i * .16f, .03f * (i % 2), 0f) * m_BaseSize, .22f, Motion.Drift, i * .05f);
                n.aspect = new Vector2(1.4f, .1f);
            }
            var flame = AddNode("Retreating ember", Shape.Star, new Color(1f, .28f, .06f, 1f), new Vector3(.22f, .04f, 0f) * m_BaseSize, .22f, Motion.Burst);
            flame.direction = Vector3.right;
        }

        void BuildLighterPurify()
        {
            AddNode("Purification seal", Shape.Ring, new Color(1f, .83f, .28f, 1f), Vector3.zero, .95f, Motion.Pop);
            BuildBloom();
            for (int i = 0; i < 6; i++)
            {
                var a = i * Mathf.PI * 2f / 6f;
                var n = AddNode("Clean spark", Shape.Star, new Color(1f, .95f, .55f, 1f), Vector3.zero, .09f, Motion.Burst, i * .04f);
                n.direction = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            }
        }

        Node AddNode(string nodeName, Shape shape, Color color, Vector3 origin, float scale, Motion motion, float delay = 0f)
        {
            if (m_RuntimeRoot == null || m_RuntimeMaterial == null)
                return null;

            var go = new GameObject(nodeName);
            go.transform.SetParent(m_RuntimeRoot.transform, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = MeshFor(shape);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = m_RuntimeMaterial;
            renderer.sortingOrder = m_Nodes.Count;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var node = new Node
            {
                transform = go.transform,
                renderer = renderer,
                properties = new MaterialPropertyBlock(),
                color = ApplyPrimaryColor(color),
                origin = origin,
                direction = Vector3.one,
                delay = Mathf.Clamp01(delay),
                duration = 1f,
                phase = UnityEngine.Random.value * Mathf.PI * 2f,
                rotation = 0f,
                rotationSpeed = 0f,
                scale = Mathf.Max(.001f, scale),
                motion = motion
            };
            m_Nodes.Add(node);
            return node;
        }

        Color ApplyPrimaryColor(Color motifColor)
        {
            if (!m_HasPrimaryColor)
                return motifColor;

            // Keep a little of each motif's designed palette (for example the yellow centre
            // of Bloom), while letting a condition supply its own emotional colour family.
            var strength = Mathf.Clamp01(m_PrimaryColor.a);
            if (strength <= 0f)
                return motifColor;
            var tinted = Color.Lerp(motifColor, new Color(m_PrimaryColor.r, m_PrimaryColor.g, m_PrimaryColor.b, motifColor.a), .68f);
            tinted.a = motifColor.a * strength;
            return tinted;
        }

        Mesh MeshFor(Shape shape)
        {
            switch (shape)
            {
                case Shape.Disc: return m_DiscMesh;
                case Shape.Ring: return m_RingMesh;
                case Shape.Heart: return m_HeartMesh;
                case Shape.Star: return m_StarMesh;
                default: return m_QuadMesh;
            }
        }

        void Animate(float time)
        {
            for (var i = 0; i < m_Nodes.Count; i++)
            {
                var node = m_Nodes[i];
                if (node == null || node.transform == null)
                    continue;

                var localTime = Mathf.Clamp01((time - node.delay) / Mathf.Max(.001f, 1f - node.delay));
                var appear = EaseOutBack(Mathf.Clamp01(localTime * 4.5f));
                var fade = Mathf.Clamp01((1f - localTime) * 4.2f);
                var position = node.origin;
                var size = node.scale * m_BaseSize;
                var rotation = node.rotation;

                switch (node.motion)
                {
                    case Motion.Pop:
                        size *= appear * (1f + Mathf.Sin(localTime * Mathf.PI * 2f + node.phase) * .045f);
                        break;
                    case Motion.Float:
                        position += node.direction * (localTime * m_BaseSize * .34f);
                        position.y += Mathf.Sin(localTime * Mathf.PI * 2f + node.phase) * m_BaseSize * .035f;
                        size *= appear;
                        break;
                    case Motion.Orbit:
                    {
                        var angle = node.phase + localTime * Mathf.PI * 2f * .16f;
                        var radius = node.origin.magnitude * (0.75f + .25f * appear);
                        position = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                        size *= appear;
                        rotation += localTime * node.rotationSpeed;
                        break;
                    }
                    case Motion.Burst:
                        position += node.direction * (EaseOutCubic(localTime) * m_BaseSize * .70f);
                        size *= (.35f + .65f * appear);
                        rotation += localTime * node.rotationSpeed;
                        break;
                    case Motion.Pulse:
                        size *= (.88f + .12f * Mathf.Sin(localTime * Mathf.PI * 4f + node.phase));
                        break;
                    case Motion.Drift:
                        position += node.direction * (localTime * m_BaseSize * .6f);
                        position.x += Mathf.Sin(localTime * Mathf.PI * 2f + node.phase) * m_BaseSize * .06f;
                        size *= .65f + .35f * appear;
                        break;
                    case Motion.Shrink:
                        size *= 1f - localTime;
                        break;
                }

                var color = node.color;
                color.a *= fade;
                node.transform.localPosition = position;
                node.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
                node.transform.localScale = new Vector3(size * node.aspect.x, size * node.aspect.y, 1f);
                node.properties.SetColor(BaseColorId, color);
                node.properties.SetColor(ColorId, color);
                node.renderer.SetPropertyBlock(node.properties);
            }
        }

        void Billboard()
        {
            if (m_RuntimeRoot == null || !m_RuntimeRoot.activeSelf)
                return;

            var camera = ResolveCamera();
            if (camera == null)
                return;

            // The generated meshes have +Z front faces, so that direction must point toward
            // the viewer. The opposite direction causes URP's default backface culling to
            // make an otherwise correct overlay disappear.
            var toCamera = camera.transform.position - m_RuntimeRoot.transform.position;
            if (toCamera.sqrMagnitude < .000001f)
                toCamera = -camera.transform.forward;
            m_RuntimeRoot.transform.rotation = Quaternion.LookRotation(toCamera.normalized, camera.transform.up);
        }

        Camera ResolveCamera()
        {
            if (m_BillboardCamera != null)
                return m_BillboardCamera;
            if (m_ResolvedCamera != null && m_ResolvedCamera.isActiveAndEnabled)
                return m_ResolvedCamera;
            if (Now < m_NextCameraSearchTime)
                return null;

            m_NextCameraSearchTime = Now + .5f;
            m_ResolvedCamera = Camera.main;
            return m_ResolvedCamera;
        }

        void ClearRuntime()
        {
            if (m_RuntimeRoot != null)
            {
                // Disable before Destroy to avoid one-frame duplicate overlays during replay.
                m_RuntimeRoot.SetActive(false);
                DestroyUnityObject(m_RuntimeRoot);
            }

            DestroyUnityObject(m_QuadMesh);
            DestroyUnityObject(m_DiscMesh);
            DestroyUnityObject(m_RingMesh);
            DestroyUnityObject(m_HeartMesh);
            DestroyUnityObject(m_StarMesh);
            DestroyUnityObject(m_RuntimeMaterial);

            m_RuntimeRoot = null;
            m_RuntimeMaterial = null;
            m_QuadMesh = null;
            m_DiscMesh = null;
            m_RingMesh = null;
            m_HeartMesh = null;
            m_StarMesh = null;
            m_Nodes.Clear();
        }

        static void DestroyUnityObject(UnityEngine.Object objectToDestroy)
        {
            if (objectToDestroy == null)
                return;
            if (Application.isPlaying)
                Destroy(objectToDestroy);
            else
                DestroyImmediate(objectToDestroy);
        }

        static void ConfigureTransparent(Material material)
        {
            if (material.HasProperty("_Cull")) material.SetInt("_Cull", (int)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite"))
                material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh { name = "AffectiveOverlay_Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-.5f, -.5f, 0f), new Vector3(.5f, -.5f, 0f),
                new Vector3(.5f, .5f, 0f), new Vector3(-.5f, .5f, 0f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh CreateDiscMesh(int segments)
        {
            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * .5f;
            }
            for (var i = 0; i < segments; i++)
            {
                var index = i * 3;
                triangles[index] = 0;
                triangles[index + 1] = i + 1;
                triangles[index + 2] = i + 2;
            }
            return CreateMesh("AffectiveOverlay_Disc", vertices, triangles);
        }

        static Mesh CreateRingMesh(int segments, float innerRadius)
        {
            var vertices = new Vector3[(segments + 1) * 2];
            var triangles = new int[segments * 6];
            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                vertices[i * 2] = direction * .5f;
                vertices[i * 2 + 1] = direction * (.5f * innerRadius);
            }
            for (var i = 0; i < segments; i++)
            {
                var vertex = i * 2;
                var triangle = i * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }
            return CreateMesh("AffectiveOverlay_Ring", vertices, triangles);
        }

        static Mesh CreateHeartMesh(int segments)
        {
            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];
            vertices[0] = new Vector3(0f, -.05f, 0f);
            for (var i = 0; i <= segments; i++)
            {
                var theta = i * Mathf.PI * 2f / segments;
                var x = 16f * Mathf.Pow(Mathf.Sin(theta), 3f) / 32f;
                var y = (13f * Mathf.Cos(theta) - 5f * Mathf.Cos(2f * theta) - 2f * Mathf.Cos(3f * theta) - Mathf.Cos(4f * theta)) / 34f;
                vertices[i + 1] = new Vector3(x, y, 0f);
            }
            for (var i = 0; i < segments; i++)
            {
                var triangle = i * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = i + 1;
                triangles[triangle + 2] = i + 2;
            }
            return CreateMesh("AffectiveOverlay_Heart", vertices, triangles);
        }

        static Mesh CreateStarMesh(int points, float innerRadius)
        {
            var count = points * 2;
            var vertices = new Vector3[count + 2];
            var triangles = new int[count * 3];
            vertices[0] = Vector3.zero;
            for (var i = 0; i <= count; i++)
            {
                var angle = Mathf.PI * .5f + i * Mathf.PI * 2f / count;
                var radius = (i & 1) == 0 ? .5f : .5f * innerRadius;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            }
            for (var i = 0; i < count; i++)
            {
                var triangle = i * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = i + 1;
                triangles[triangle + 2] = i + 2;
            }
            return CreateMesh("AffectiveOverlay_Star", vertices, triangles);
        }

        static Mesh CreateMesh(string name, Vector3[] vertices, int[] triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static float EaseOutCubic(float value)
        {
            var inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        static float EaseOutBack(float value)
        {
            value = Mathf.Clamp01(value);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var shifted = value - 1f;
            return 1f + c3 * shifted * shifted * shifted + c1 * shifted * shifted;
        }
    }
}
