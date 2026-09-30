using System;
using UnityEngine;

namespace MagicMR
{
    /// <summary>
    /// Directs the affective-narrative layer of RealityEditor.
    ///
    /// A physical object remains the spatial anchor while this director places a
    /// short, flat illustrated animation over it. The study therefore examines
    /// how animated reality rewriting is experienced, rather than teaching a
    /// collection of gesture-dependent effects.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public class RealityScenarioDirector : MonoBehaviour
    {
        [Serializable]
        public sealed class AnimationStage
        {
            public string label;
            public AffectiveMotif prototype;
            public Sprite[] frames = Array.Empty<Sprite>();
            public AudioClip audio;
            [Tooltip("Optional Particle System / Animator prefab imported from Asset Store. It follows the same anchor and stops with this stage.")]
            public GameObject assetStoreEffectPrefab;
            [Min(.1f)] public float duration = 3f;
            [Min(.01f)] public float widthMeters = .15f;
            public Vector3 offsetMeters;
        }

        [Serializable]
        public sealed class Scenario
        {
            [Tooltip("Optional scene object used as the physical/virtual anchor. Empty falls back to the active RealityEditor target.")]
            public GameObject target;

            [Tooltip("Stable identifier written to the affective event log.")]
            public string narrativeId;

            [Tooltip("Name of the real-world subject that becomes animated.")]
            public string subject;

            public AffectiveMotif motif;
            public Color accent = Color.white;

            [TextArea]
            public string narrative;

            [TextArea]
            public string emotionalInvitation;
            [Tooltip("A style, B agency, C interaction, D closure. Empty frames use visibly labelled prototype graphics.")]
            public AnimationStage[] stages;
        }

        public const int ScenarioCount = 8;
        public static RealityScenarioDirector Instance { get; private set; }

        [Header("Affective narrative mode")]
        [Tooltip("When enabled, the fixed right-hand gesture vocabulary activates the corresponding illustrated stage. Legacy 3D object effects are bypassed.")]
        [SerializeField] bool m_EnableAffectiveNarrativeMode = true;

        [SerializeField] Scenario[] m_Scenarios = DefaultScenarios();
        [SerializeField, Range(1, ScenarioCount)] int m_InitialScenario = 1;

        [Header("Development helpers")]
        [SerializeField] bool m_EnableKeyboardScenarioSwitching;
        [SerializeField] bool m_ShowDeveloperHud;

        int m_CurrentIndex;
        RealityEditor m_CurrentEditor;
        Transform m_CurrentAnchor;
        Affective2DOverlayAnimator m_Overlay;
        AffectiveExperienceLogger m_AffectLogger;
        AffectiveFramePlayer m_FramePlayer;
        AffectiveAssetEffectPlayer m_AssetEffectPlayer;
        int m_Activation;
        bool m_PlaybackPending;
        string m_LastFeedback = "Anchor an object, then use a gesture to animate it.";

        public bool IsAffectiveNarrativeMode => m_EnableAffectiveNarrativeMode;
        public int CurrentScenarioId => m_CurrentIndex + 1;
        public Scenario CurrentScenario => m_Scenarios != null && m_Scenarios.Length > m_CurrentIndex
            ? m_Scenarios[m_CurrentIndex] : null;
        public RealityEditor CurrentEditor => m_CurrentEditor;
        public Transform CurrentAnchor => m_CurrentAnchor;
        public bool HasExplicitAnchor => CurrentScenario != null && CurrentScenario.target != null &&
            CurrentScenario.target.activeInHierarchy;
        public string LastFeedback => m_LastFeedback;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureScenarioTable();
            EnsureServices();
        }

        void Start()
        {
            SelectScenario(m_InitialScenario, true);
            LockToNarrativeInput();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (m_PlaybackPending && !m_Overlay.IsPlaying && !m_FramePlayer.IsPlaying)
            {
                m_PlaybackPending = false;
                DataLogger.Instance?.LogEvent("affective_animation_completed", EditDimension.None, -1f, 0f,
                    notes: $"activation={m_Activation}");
                m_LastFeedback = "Animation finished. How did you feel?";
            }
#if ENABLE_INPUT_SYSTEM
            if (!m_EnableKeyboardScenarioSwitching)
                return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.f1Key.wasPressedThisFrame) SelectScenario(1);
            else if (keyboard.f2Key.wasPressedThisFrame) SelectScenario(2);
            else if (keyboard.f3Key.wasPressedThisFrame) SelectScenario(3);
            else if (keyboard.f4Key.wasPressedThisFrame) SelectScenario(4);
            else if (keyboard.f5Key.wasPressedThisFrame) SelectScenario(5);
            else if (keyboard.f6Key.wasPressedThisFrame) SelectScenario(6);
            else if (keyboard.f7Key.wasPressedThisFrame) SelectScenario(7);
            else if (keyboard.f8Key.wasPressedThisFrame) SelectScenario(8);
#endif
        }

        /// <summary>
        /// Selects an emotional story. Switching always removes the previous
        /// animated overlay before exposing the next object/story combination.
        /// </summary>
        public void SelectScenario(int scenarioId, bool initial = false)
        {
            EnsureScenarioTable();
            EnsureServices();
            scenarioId = Mathf.Clamp(scenarioId, 1, m_Scenarios.Length);

            if (!initial)
            {
                ResetNarrative();
                FindFirstObjectByType<TabletopGestureRecognizer>()?.ResetRecognition();
                // Clears any legacy visual state when a development scene was
                // previously run with the old controller, without invoking it
                // as part of normal affective playback.
                m_CurrentEditor?.ResetTarget();
            }

            m_CurrentIndex = scenarioId - 1;
            var scenario = CurrentScenario;
            m_CurrentAnchor = scenario != null && scenario.target != null
                ? scenario.target.transform
                : null;

            m_CurrentEditor = scenario != null && scenario.target != null
                ? scenario.target.GetComponentInChildren<RealityEditor>(true)
                : null;
            // Only the lighter has a deployed detector. Other subjects require
            // an explicitly placed/tracked target; never label a lighter as a pet.
            if (m_CurrentEditor == null && scenarioId == 1)
                m_CurrentEditor = FindFirstObjectByType<RealityEditor>();
            if (m_CurrentAnchor == null && m_CurrentEditor != null)
                m_CurrentAnchor = m_CurrentEditor.transform;

            DataLogger.Instance?.SetScenario(scenarioId, scenario != null ? scenario.subject : "Unnamed");
            m_LastFeedback = scenario == null
                ? "No affective scenario is configured."
                : $"{scenario.subject}: use a gesture to reveal its animated story.";
            LockToNarrativeInput();

            Debug.Log($"[AffectiveMR] Scenario {scenarioId}/{ScenarioCount}: {scenario?.narrativeId} " +
                      $"on {scenario?.subject}. Input: fixed four-gesture vocabulary.", this);
        }

        /// <summary>Returns whether a detected gesture can activate the affective study.</summary>
        public bool AcceptsGesture(EditDimension dimension)
        {
            return m_EnableAffectiveNarrativeMode && StageIndex(dimension) >= 0;
        }

        /// <summary>
        /// Plays the selected 2D illustrated animation at the currently anchored
        /// object. It deliberately never calls <see cref="RealityEditor.ApplyDimension"/>.
        /// </summary>
        public bool TryActivateNarrative(EditDimension dimension, Vector3 handPosition, bool hasHandPosition)
        {
            if (!AcceptsGesture(dimension) || CurrentScenario == null)
                return false;

            if (m_CurrentAnchor == null && CurrentScenarioId == 1)
            {
                m_CurrentEditor ??= FindFirstObjectByType<RealityEditor>();
                m_CurrentAnchor = m_CurrentEditor != null ? m_CurrentEditor.transform : null;
            }

            if (m_CurrentAnchor == null)
            {
                m_LastFeedback = "Assign and register a target for this scenario.";
                return false;
            }

            EnsureServices();
            if (m_PlaybackPending)
            {
                m_LastFeedback = "Wait for this animation to finish.";
                return false;
            }
            var scenario = CurrentScenario;
            var stage = scenario.stages[StageIndex(dimension)];
            m_Overlay.Stop();
            m_FramePlayer.Stop();
            m_AssetEffectPlayer.Stop();
            bool authored = stage.frames != null && stage.frames.Length > 0;
            if (authored)
                m_FramePlayer.Play(m_CurrentAnchor, stage.frames, stage.audio, stage.duration,
                    stage.widthMeters, stage.offsetMeters);
            else
            {
                m_Overlay.ConfigurePlacement(stage.widthMeters, stage.offsetMeters);
                m_Overlay.Play(m_CurrentAnchor, stage.prototype, scenario.accent, stage.duration, stage.label);
            }
            m_AssetEffectPlayer.Play(stage.assetStoreEffectPrefab, m_CurrentAnchor, stage.offsetMeters, stage.duration);
            if (authored ? !m_FramePlayer.IsPlaying : !m_Overlay.IsPlaying)
            {
                m_LastFeedback = "Animation asset unavailable.";
                return false;
            }
            m_Activation++;
            m_PlaybackPending = true;
            m_AffectLogger.ActivateNarrative(scenario.narrativeId + "/" + dimension, stage.label);

            var distance = hasHandPosition
                ? Vector3.Distance(handPosition, m_CurrentAnchor.position)
                : -1f;
            DataLogger.Instance?.LogEvent(
                "affective_animation_started",
                dimension,
                distance,
                0f,
                handPosition: hasHandPosition ? handPosition : (Vector3?)null,
                notes: $"narrative_id={NoteValue(scenario.narrativeId)};motif={scenario.motif};" +
                       $"stage={dimension};activation={m_Activation};asset_mode={(authored ? "authored_frames" : "prototype")}");

            m_LastFeedback = "Animation playing" + (authored ? "" : " [prototype]");
            Debug.Log($"[AffectiveMR] Played {scenario.narrativeId} ({scenario.motif}) on {scenario.subject}.", this);
            return true;
        }

        /// <summary>Stops the active visual story; safe to call during reset/reacquisition.</summary>
        public void ResetNarrative()
        {
            if (m_PlaybackPending)
                DataLogger.Instance?.LogEvent("affective_animation_cancelled", EditDimension.None, -1f, 0f,
                    notes: $"activation={m_Activation}");
            m_PlaybackPending = false;
            m_Overlay?.Stop();
            m_FramePlayer?.Stop();
            m_AssetEffectPlayer?.Stop();
            m_AffectLogger?.ClearNarrative();
            m_LastFeedback = "Animation cleared. Re-anchor, then use a gesture.";
        }

        void OnDisable() => ResetNarrative();

        // Kept as a narrow compatibility bridge for old callers. It cannot
        // revive the old four-effect semantics in affective-narrative mode.
        public void RecordSpell(EditDimension dimension)
        {
            // Logging compatibility only; activation must pass GestureManager gates.
        }

        void LockToNarrativeInput()
        {
            if (!m_EnableAffectiveNarrativeMode)
                return;
            GestureManager.Instance?.SetEnabledDimensions(EnabledDimensions.All);
        }

        void EnsureServices()
        {
            m_Overlay ??= GetComponent<Affective2DOverlayAnimator>() ?? gameObject.AddComponent<Affective2DOverlayAnimator>();
            m_AffectLogger ??= GetComponent<AffectiveExperienceLogger>() ?? gameObject.AddComponent<AffectiveExperienceLogger>();
            m_FramePlayer ??= GetComponent<AffectiveFramePlayer>() ?? gameObject.AddComponent<AffectiveFramePlayer>();
            m_AssetEffectPlayer ??= GetComponent<AffectiveAssetEffectPlayer>() ?? gameObject.AddComponent<AffectiveAssetEffectPlayer>();
        }

        void OnGUI()
        {
            if (!m_ShowDeveloperHud || CurrentScenario == null)
                return;

            GUI.Box(new Rect(18, 18, 620, 66),
                $"Affective MR · {CurrentScenario.subject} · {CurrentScenario.motif}\n" +
                "Gesture: reveal the matching 2D animated story over the anchored object\n" +
                m_LastFeedback);
        }

        void EnsureScenarioTable()
        {
            if (m_Scenarios == null || m_Scenarios.Length != ScenarioCount)
                m_Scenarios = DefaultScenarios();
            for (int i = 0; i < m_Scenarios.Length; i++)
            {
                if (m_Scenarios[i] == null) m_Scenarios[i] = DefaultScenarios()[i];
                if (m_Scenarios[i].stages == null || m_Scenarios[i].stages.Length != 4)
                    m_Scenarios[i].stages = DefaultStages(i);
                for (int j = 0; j < 4; j++)
                    if (m_Scenarios[i].stages[j] == null) m_Scenarios[i].stages[j] = DefaultStages(i)[j];
            }
        }

        public static int StageIndex(EditDimension dimension)
        {
            switch (dimension)
            {
                case EditDimension.Appearance: return 0;
                case EditDimension.Agency: return 1;
                case EditDimension.Rule: return 2;
                case EditDimension.Deconstruction: return 3;
                default: return -1;
            }
        }

        static AnimationStage[] DefaultStages(int scene)
        {
            string[][] labels = {
                new[] { "Charcoal skin", "Coughing little demon", "Dodge and sway", "Purification bloom" },
                new[] { "Dark bark", "Awakening tree knot", "Branch sweep", "Seal and calm" },
                new[] { "Pixel insect", "Arcade dance", "Dizzy stars", "Pixel coin reward" },
                new[] { "Cartoon monument", "Awakening statue", "Authored historical story", "Lotus spectacle" },
                new[] { "Watercolour room", "Jellyfish lamp", "Ocean window", "Candy rain" },
                new[] { "Illustrated plant", "Plant companion", "Growth timeline", "Restore plant" },
                new[] { "Comic pet", "Dream bubble", "Bubble stars", "Heart shower" },
                new[] { "Seasonal illustration", "Tree companion", "Season timeline", "Wind and falling petals" }
            };
            AffectiveMotif[][] motifs = {
                new[] { AffectiveMotif.LighterChar, AffectiveMotif.LighterDemon, AffectiveMotif.LighterRefusal, AffectiveMotif.LighterPurify },
                new[] { AffectiveMotif.Aversion, AffectiveMotif.Memory, AffectiveMotif.Warning, AffectiveMotif.Calm },
                new[] { AffectiveMotif.Dissolve, AffectiveMotif.Playful, AffectiveMotif.Surprise, AffectiveMotif.Memory },
                new[] { AffectiveMotif.Playful, AffectiveMotif.Surprise, AffectiveMotif.Memory, AffectiveMotif.Bloom },
                new[] { AffectiveMotif.Calm, AffectiveMotif.Playful, AffectiveMotif.Memory, AffectiveMotif.Dissolve },
                new[] { AffectiveMotif.Bloom, AffectiveMotif.Playful, AffectiveMotif.Bloom, AffectiveMotif.Dissolve },
                new[] { AffectiveMotif.Playful, AffectiveMotif.Memory, AffectiveMotif.Surprise, AffectiveMotif.Comfort },
                new[] { AffectiveMotif.Bloom, AffectiveMotif.Playful, AffectiveMotif.Calm, AffectiveMotif.Dissolve }
            };
            var stages = new AnimationStage[4];
            for (int i = 0; i < 4; i++)
                stages[i] = new AnimationStage { label = labels[scene][i], prototype = motifs[scene][i] };
            return stages;
        }

        static string NoteValue(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "unspecified"
                : value.Trim().Replace(";", "/").Replace("=", "-");
        }

        static Scenario[] DefaultScenarios() => new[]
        {
            new Scenario { narrativeId="lighter_release", subject="Lighter", motif=AffectiveMotif.Aversion, accent=new Color(.76f, 1f, .22f), narrative="The flame is sealed and the lighter loses its invitation to smoke.", emotionalInvitation="Reflect on restraint, distance, or relief." },
            new Scenario { narrativeId="willow_alert", subject="Willow", motif=AffectiveMotif.Warning, accent=new Color(1f, .22f, .16f), narrative="An illustrated warning grows through the branches.", emotionalInvitation="Notice tension, alertness, or fear." },
            new Scenario { narrativeId="pixel_bug", subject="Small insect", motif=AffectiveMotif.Surprise, accent=new Color(1f, .72f, .16f), narrative="The insect becomes a playful pixel burst.", emotionalInvitation="Notice surprise, curiosity, or amusement." },
            new Scenario { narrativeId="statue_memory", subject="Historical statue", motif=AffectiveMotif.Memory, accent=new Color(1f, .73f, .32f), narrative="A flat animated memory frame appears around the monument.", emotionalInvitation="Notice awe, curiosity, or nostalgia." },
            new Scenario { narrativeId="room_comfort", subject="Rental room", motif=AffectiveMotif.Comfort, accent=new Color(1f, .42f, .52f), narrative="The room receives a gentle illustrated warmth.", emotionalInvitation="Notice comfort, safety, or unease." },
            new Scenario { narrativeId="plant_bloom", subject="Desktop plant", motif=AffectiveMotif.Bloom, accent=new Color(1f, .38f, .62f), narrative="A two-dimensional flower blooms from the living plant.", emotionalInvitation="Notice calm, care, or delight." },
            new Scenario { narrativeId="comic_companion", subject="Comic pet", motif=AffectiveMotif.Playful, accent=new Color(.68f, .38f, 1f), narrative="A bright comic companion animation appears beside the pet.", emotionalInvitation="Notice playfulness, affection, or companionship." },
            new Scenario { narrativeId="seasonal_tree", subject="Four-season tree", motif=AffectiveMotif.Calm, accent=new Color(.28f, .78f, 1f), narrative="Layered illustrated ripples suggest the passage of seasons.", emotionalInvitation="Notice calm, reflection, or a sense of time." }
        };
    }
}
