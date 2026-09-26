using System.Collections;
using UnityEngine;

namespace MagicMR
{
    /// <summary>
    /// HCI meeting 4.pdf scene animations. Uses Asset Store War FX / coins / boxes
    /// when Resources/HciStore prefabs are present, otherwise procedural fallbacks.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class HciMeetingDirector : MonoBehaviour
    {
        public static HciMeetingDirector Instance { get; private set; }

        [SerializeField]
        HciMeetingScenario m_Scenario = HciMeetingScenario.LighterDemon;

        [SerializeField]
        Transform m_Anchor;

        RealityEditor m_Editor;
        GameObject m_Rig;
        TextMesh m_Hud;
        Transform m_Wings;
        Transform m_Branches;
        Transform m_Scar;
        Transform m_DreamBubble;
        Transform m_Portal;
        Transform m_Jellyfish;
        Transform m_Tail;
        Transform m_Actor;
        ParticleSystem m_Ash;
        ParticleSystem m_Falling;
        GameObject m_LoopFx;
        int m_SeasonIndex;
        int m_StyleIndex;
        int m_PlantAge = 1;
        Coroutine m_Motion;
        Vector3 m_AnchorHiddenScale = Vector3.one;
        bool m_SuppressRebuild;
        bool m_Whipping;

        public HciMeetingScenario Scenario => m_Scenario;

        void Awake()
        {
            Instance = this;
            if (m_Anchor == null)
            {
                var lighter = GameObject.Find("Lighter");
                m_Anchor = lighter != null ? lighter.transform : transform;
            }

            m_Editor = m_Anchor.GetComponent<RealityEditor>();
            m_AnchorHiddenScale = m_Anchor.localScale;
            RebuildRig();
        }

        void OnEnable()
        {
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        void LateUpdate()
        {
            if (m_Hud == null || Camera.main == null)
                return;

            var cam = Camera.main.transform;
            m_Hud.transform.position = cam.position + cam.forward * 0.72f + cam.up * 0.22f;
            m_Hud.transform.rotation = Quaternion.LookRotation(m_Hud.transform.position - cam.position);
            if (m_Rig != null)
            {
                foreach (var tm in m_Rig.GetComponentsInChildren<TextMesh>())
                    tm.transform.rotation = Quaternion.LookRotation(tm.transform.position - cam.position);
            }

            AnimateIdle();
        }

        public void CycleScenario(int delta)
        {
            var count = System.Enum.GetValues(typeof(HciMeetingScenario)).Length;
            var next = ((int)m_Scenario + delta + count) % count;
            SetScenario((HciMeetingScenario)next);
        }

        public void SetScenario(HciMeetingScenario scenario)
        {
            m_Scenario = scenario;
            m_SeasonIndex = 0;
            m_StyleIndex = 0;
            m_PlantAge = 1;
            m_SuppressRebuild = true;
            m_Editor?.DismissStudyRewards();
            m_Editor?.ResetTarget();
            m_SuppressRebuild = false;
            RebuildRig();
            Debug.Log($"[MagicMR] HCI scene → {HciMeetingScenarioNames.DisplayName(scenario)}");
        }

        public void Play(EditDimension dimension)
        {
            if (dimension == EditDimension.None)
                return;

            if (m_Motion != null)
                StopCoroutine(m_Motion);

            m_Whipping = false;
            m_Motion = StartCoroutine(PlayRoutine(dimension));
        }

        IEnumerator PlayRoutine(EditDimension dimension)
        {
            switch (m_Scenario)
            {
                case HciMeetingScenario.LighterDemon:
                    yield return PlayLighter(dimension);
                    break;
                case HciMeetingScenario.WhompingWillow:
                    yield return PlayWillow(dimension);
                    break;
                case HciMeetingScenario.PixelCritter:
                    yield return PlayCritter(dimension);
                    break;
                case HciMeetingScenario.HistoricStatue:
                    yield return PlayStatue(dimension);
                    break;
                case HciMeetingScenario.Bedroom:
                    yield return PlayBedroom(dimension);
                    break;
                case HciMeetingScenario.DeskPlant:
                    yield return PlayPlant(dimension);
                    break;
                case HciMeetingScenario.MangaPet:
                    yield return PlayPet(dimension);
                    break;
                case HciMeetingScenario.SeasonTree:
                    yield return PlaySeasonTree(dimension);
                    break;
            }

            m_Motion = null;
        }

        public void ResetVisuals()
        {
            if (m_SuppressRebuild)
                return;

            if (m_Motion != null)
            {
                StopCoroutine(m_Motion);
                m_Motion = null;
            }

            RebuildRig();
        }

        void RebuildRig()
        {
            if (m_Rig != null)
                Destroy(m_Rig);

            m_Wings = m_Branches = m_Scar = m_DreamBubble = m_Portal = m_Jellyfish = m_Tail = m_Actor = null;
            m_Ash = m_Falling = null;
            m_LoopFx = null;
            m_Whipping = false;

            m_Rig = new GameObject("HciScenarioRig");
            m_Rig.transform.SetParent(m_Anchor, false);
            m_Rig.transform.localPosition = Vector3.zero;
            m_Rig.transform.localRotation = Quaternion.identity;
            m_Rig.transform.localScale = m_Scenario == HciMeetingScenario.LighterDemon
                ? Vector3.one
                : Vector3.one * 3.2f;

            SetAnchorVisible(m_Scenario == HciMeetingScenario.LighterDemon);

            switch (m_Scenario)
            {
                case HciMeetingScenario.LighterDemon:
                    BuildLighterExtras();
                    break;
                case HciMeetingScenario.WhompingWillow:
                    BuildWillow();
                    break;
                case HciMeetingScenario.PixelCritter:
                    BuildCritter();
                    break;
                case HciMeetingScenario.HistoricStatue:
                    BuildStatue();
                    break;
                case HciMeetingScenario.Bedroom:
                    BuildBedroom();
                    break;
                case HciMeetingScenario.DeskPlant:
                    BuildPlant();
                    break;
                case HciMeetingScenario.MangaPet:
                    BuildPet();
                    break;
                case HciMeetingScenario.SeasonTree:
                    BuildSeasonTree();
                    break;
            }

            EnsureHud();
        }

        void SetAnchorVisible(bool visible)
        {
            foreach (var renderer in m_Anchor.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.transform.IsChildOf(m_Rig != null ? m_Rig.transform : m_Anchor))
                    continue;
                if (renderer.GetComponent<ParticleSystem>() != null)
                    continue;
                if (renderer.GetComponent<TextMesh>() != null)
                    continue;
                var n = renderer.gameObject.name;
                if (n.StartsWith("Hci") || n.Contains("AgencyEyes") || n.Contains("Ghost"))
                    continue;
                renderer.enabled = visible;
            }
        }

        void EnsureHud()
        {
            if (m_Hud != null)
            {
                RefreshHud();
                return;
            }

            var go = new GameObject("HciScenarioHud");
            DontDestroyOnLoad(go);
            m_Hud = go.AddComponent<TextMesh>();
            m_Hud.fontSize = 36;
            m_Hud.characterSize = 0.012f;
            m_Hud.anchor = TextAnchor.MiddleCenter;
            m_Hud.alignment = TextAlignment.Center;
            m_Hud.color = new Color(0.9f, 0.95f, 1f, 0.95f);
            RefreshHud();
        }

        void RefreshHud()
        {
            if (m_Hud == null)
                return;
            m_Hud.text =
                HciMeetingScenarioNames.DisplayName(m_Scenario) + "\n" +
                HciMeetingScenarioNames.Hint(m_Scenario) + "\n" +
                "1-4 施法   N 下一场景   P 上一场景";
        }

        void AnimateIdle()
        {
            if (m_Wings != null && m_Wings.gameObject.activeSelf && m_Wings.childCount >= 2)
            {
                var flap = 18f * Mathf.Sin(Time.time * 8f);
                m_Wings.GetChild(0).localRotation = Quaternion.Euler(0f, 0f, 25f + flap);
                m_Wings.GetChild(1).localRotation = Quaternion.Euler(0f, 0f, -25f - flap);
            }

            if (m_Branches != null && !m_Whipping)
            {
                for (var i = 0; i < m_Branches.childCount; i++)
                {
                    var b = m_Branches.GetChild(i);
                    var yaw = i * (360f / Mathf.Max(1, m_Branches.childCount));
                    var sway = 10f * Mathf.Sin(Time.time * 1.6f + i * 0.7f);
                    b.localRotation = Quaternion.Euler(28f + sway, yaw, 42f + sway * 0.4f);
                }
            }

            if (m_Actor != null && m_Motion == null && m_Actor.name == "Bug")
            {
                m_Actor.localPosition = new Vector3(0f, 0.09f + 0.014f * Mathf.Sin(Time.time * 3.2f), 0f);
                m_Actor.localRotation = Quaternion.Euler(0f, 10f * Mathf.Sin(Time.time * 1.5f), 0f);
            }

            if (m_Jellyfish != null && m_Jellyfish.gameObject.activeSelf)
            {
                for (var i = 0; i < m_Jellyfish.childCount; i++)
                {
                    var c = m_Jellyfish.GetChild(i);
                    c.localPosition = new Vector3(
                        Mathf.Sin(Time.time * 0.7f + i) * 0.2f,
                        0.28f + 0.1f * Mathf.Sin(Time.time * 1.6f + i),
                        Mathf.Cos(Time.time * 0.55f + i) * 0.14f);
                    var pulse = 1f + 0.12f * Mathf.Sin(Time.time * 3f + i);
                    c.localScale = Vector3.one * 0.08f * pulse;
                    var tent = c.Find("Tent");
                    if (tent != null)
                        tent.localRotation = Quaternion.Euler(12f * Mathf.Sin(Time.time * 4f + i), 0f, 8f * Mathf.Sin(Time.time * 3f + i));
                }
            }

            if (m_DreamBubble != null && m_DreamBubble.gameObject.activeSelf)
            {
                var s = 1f + 0.08f * Mathf.Sin(Time.time * 2.4f);
                m_DreamBubble.localScale = Vector3.one * 0.18f * s;
                var fish = m_DreamBubble.Find("Fish");
                if (fish != null)
                    fish.localRotation = Quaternion.Euler(0f, Time.time * 80f, 12f * Mathf.Sin(Time.time * 3f));
            }

            if (m_Tail != null)
                m_Tail.localRotation = Quaternion.Euler(0f, 0f, 25f + 18f * Mathf.Sin(Time.time * 4f));

            if (m_Portal != null && m_Portal.gameObject.activeSelf)
                m_Portal.localScale = new Vector3(0.22f, 0.3f, 1f) * (1f + 0.04f * Mathf.Sin(Time.time * 3f));
        }

        #region Lighter

        void BuildLighterExtras()
        {
            m_Wings = new GameObject("Wings").transform;
            m_Wings.SetParent(m_Rig.transform, false);
            m_Wings.localPosition = new Vector3(0f, 0.35f, 0f);
            HciMeetingVfx.Primitive(PrimitiveType.Quad, m_Wings, "L", new Vector3(-0.09f, 0f, 0f), new Vector3(0.14f, 0.08f, 1f), new Color(0.15f, 0.05f, 0.02f, 0.85f), true);
            HciMeetingVfx.Primitive(PrimitiveType.Quad, m_Wings, "R", new Vector3(0.09f, 0f, 0f), new Vector3(0.14f, 0.08f, 1f), new Color(0.15f, 0.05f, 0.02f, 0.85f), true);
            m_Wings.gameObject.SetActive(false);
            m_Ash = HciMeetingVfx.Burst(m_Rig.transform, "Ash", new Color(0.22f, 0.18f, 0.14f, 0.75f), 14, 0.03f, 0.01f, 1.4f, true, HciMeetingStore.Tex("SmokeNoise"));
            var ashMain = m_Ash.main;
            ashMain.gravityModifier = 0.55f;
            m_Ash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        IEnumerator PlayLighter(EditDimension dimension)
        {
            switch (dimension)
            {
                case EditDimension.Appearance:
                    if (m_LoopFx == null)
                        m_LoopFx = HciMeetingStore.LoopFire(m_Rig.transform, new Vector3(0f, 0.16f, 0f));
                    break;
                case EditDimension.Agency:
                    if (m_Wings != null)
                        m_Wings.gameObject.SetActive(true);
                    m_Ash?.Play();
                    HciMeetingStore.CoughSmoke(m_Anchor.position + m_Anchor.up * 0.12f + m_Anchor.forward * 0.05f);
                    yield return Cough();
                    break;
                case EditDimension.Rule:
                    yield return WindStagger();
                    break;
                case EditDimension.Deconstruction:
                    if (m_Wings != null)
                        m_Wings.gameObject.SetActive(false);
                    m_Ash?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    if (m_LoopFx != null)
                    {
                        Object.Destroy(m_LoopFx);
                        m_LoopFx = null;
                    }
                    HciMeetingStore.PurifyBlast(m_Anchor.position + Vector3.up * 0.1f);
                    HciMeetingStore.BloomReward(null, m_Anchor.position + Vector3.up * 0.12f);
                    break;
            }
        }

        #endregion

        #region Willow

        void Ground(Color color, float radius = 0.46f)
        {
            HciMeetingVfx.Primitive(
                PrimitiveType.Cylinder,
                m_Rig.transform,
                "Ground",
                new Vector3(0f, -0.006f, 0f),
                Quaternion.identity,
                new Vector3(radius, 0.01f, radius),
                HciMeetingVfx.LitGlow(color, Color.black));
        }

        void BuildWillow()
        {
            Ground(new Color(0.12f, 0.18f, 0.08f), 0.5f);
            var bark = HciMeetingVfx.LitGlow(new Color(0.28f, 0.14f, 0.06f), new Color(0.08f, 0.03f, 0.01f));
            var leaf = HciMeetingVfx.LitGlow(new Color(0.08f, 0.38f, 0.1f), new Color(0.04f, 0.32f, 0.05f));
            var moss = HciMeetingVfx.LitGlow(new Color(0.14f, 0.28f, 0.07f), new Color(0.05f, 0.18f, 0.03f));
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Mound", new Vector3(0f, 0.03f, 0f), Quaternion.identity, new Vector3(0.22f, 0.06f, 0.22f), moss);
            HciMeetingVfx.Primitive(PrimitiveType.Cylinder, m_Rig.transform, "Trunk", new Vector3(0f, 0.2f, 0f), Quaternion.Euler(0f, 0f, 8f), new Vector3(0.1f, 0.22f, 0.1f), bark);
            HciMeetingVfx.Primitive(PrimitiveType.Cylinder, m_Rig.transform, "Trunk2", new Vector3(0.03f, 0.44f, 0f), Quaternion.Euler(0f, 0f, -14f), new Vector3(0.07f, 0.14f, 0.07f), bark);
            HciMeetingVfx.Primitive(PrimitiveType.Cylinder, m_Rig.transform, "Trunk3", new Vector3(-0.04f, 0.38f, 0.04f), Quaternion.Euler(12f, 0f, 16f), new Vector3(0.045f, 0.1f, 0.045f), bark);
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Canopy", new Vector3(0f, 0.6f, 0f), Quaternion.identity, Vector3.one * 0.3f, leaf);
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Canopy2", new Vector3(0.16f, 0.52f, -0.08f), Quaternion.identity, Vector3.one * 0.18f, leaf);
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Canopy3", new Vector3(-0.14f, 0.5f, 0.1f), Quaternion.identity, Vector3.one * 0.16f, leaf);
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Canopy4", new Vector3(0.04f, 0.48f, 0.14f), Quaternion.identity, Vector3.one * 0.12f, leaf);

            m_Branches = new GameObject("Branches").transform;
            m_Branches.SetParent(m_Rig.transform, false);
            m_Branches.localPosition = new Vector3(0f, 0.5f, 0f);
            var vine = HciMeetingVfx.LitGlow(new Color(0.16f, 0.1f, 0.04f), Color.black);
            for (var i = 0; i < 10; i++)
            {
                var yaw = i * 36f;
                var b = HciMeetingVfx.Primitive(PrimitiveType.Capsule, m_Branches, "B" + i, Vector3.zero, Quaternion.Euler(28f, yaw, 42f), new Vector3(0.02f, 0.26f, 0.02f), vine);
                HciMeetingVfx.Primitive(PrimitiveType.Sphere, b.transform, "Tip", new Vector3(0f, -1.05f, 0f), Quaternion.identity, Vector3.one * 0.7f, leaf);
            }

            m_Scar = HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Scar", new Vector3(0.03f, 0.26f, 0.07f), Quaternion.identity, Vector3.one * 0.06f, HciMeetingVfx.LitGlow(new Color(0.95f, 0.08f, 0.02f), new Color(2.8f, 0.2f, 0.05f))).transform;
            m_Scar.gameObject.SetActive(false);
            HciMeetingStore.Fireflies(m_Rig.transform, new Vector3(0f, 0.42f, 0f));
        }

        IEnumerator PlayWillow(EditDimension dimension)
        {
            switch (dimension)
            {
                case EditDimension.Appearance:
                    TintChildren(m_Rig.transform, new Color(0.06f, 0.04f, 0.1f));
                    TintNamedGlow("Canopy", new Color(0.18f, 0.05f, 0.28f), new Color(0.35f, 0.05f, 0.7f));
                    TintNamedGlow("Canopy2", new Color(0.12f, 0.04f, 0.22f), new Color(0.4f, 0.08f, 0.8f));
                    TintNamedGlow("Scar", new Color(0.9f, 0.1f, 0.05f), new Color(2f, 0.2f, 0.05f));
                    HciMeetingStore.DarkFog(m_Rig.transform);
                    HciMeetingVfx.Make(m_Rig.transform, "Storm", new Color(0.55f, 0.2f, 1.4f, 1f), 40, 0.28f, 0.06f, 1.1f, false, HciMeetingStore.Tex("GlowCircle"), true, 0f, 0.18f, false, 0);
                    break;
                case EditDimension.Agency:
                    yield return WhipBranches();
                    break;
                case EditDimension.Rule:
                    if (m_Scar != null)
                        m_Scar.gameObject.SetActive(true);
                    if (m_LoopFx == null)
                        m_LoopFx = HciMeetingStore.ScarFire(m_Scar != null ? m_Scar : m_Rig.transform, Vector3.zero);
                    yield return PulseScar();
                    break;
                case EditDimension.Deconstruction:
                    TintNamedGlow("Canopy", new Color(0.55f, 0.82f, 1f), new Color(0.4f, 0.9f, 1.6f));
                    TintNamedGlow("Canopy2", new Color(0.7f, 0.9f, 1f), new Color(0.5f, 1f, 1.5f));
                    if (m_Branches != null)
                        m_Branches.localRotation = Quaternion.identity;
                    if (m_LoopFx != null)
                    {
                        Object.Destroy(m_LoopFx);
                        m_LoopFx = null;
                    }
                    HciMeetingStore.ChestReveal(m_Rig.transform, new Vector3(0f, 0.04f, 0.18f));
                    HciMeetingVfx.Make(m_Rig.transform, "Seal", new Color(0.75f, 0.95f, 1.6f, 1f), 48, 0.22f, 0.055f, 1.2f, false, HciMeetingStore.Tex("GlowCircle"), true, 0f, 0.1f, false, 0);
                    HciMeetingStore.AuraRing(m_Rig.transform, new Vector3(0f, 0.02f, 0f), new Color(0.6f, 0.9f, 1.5f, 1f));
                    break;
            }
        }

        IEnumerator WhipBranches()
        {
            if (m_Branches == null)
                yield break;
            m_Whipping = true;
            for (var i = 0; i < m_Branches.childCount; i++)
            {
                var b = m_Branches.GetChild(i);
                var rest = b.localRotation;
                var t = 0f;
                while (t < 0.16f)
                {
                    t += Time.deltaTime;
                    b.localRotation = rest * Quaternion.Euler(Mathf.SmoothStep(0f, 88f, t / 0.16f), 0f, 0f);
                    yield return null;
                }

                var tip = b.Find("Tip");
                HciMeetingStore.ImpactBurst(tip != null ? tip.position : b.position);
                t = 0f;
                while (t < 0.2f)
                {
                    t += Time.deltaTime;
                    b.localRotation = rest * Quaternion.Euler(Mathf.SmoothStep(88f, 0f, t / 0.2f), 0f, 0f);
                    yield return null;
                }

                b.localRotation = rest;
            }

            m_Whipping = false;
        }

        IEnumerator PulseScar()
        {
            var t = 0f;
            while (t < 1.8f && m_Scar != null)
            {
                t += Time.deltaTime;
                m_Scar.localScale = Vector3.one * (0.06f * (1f + 0.7f * Mathf.Sin(t * 12f)));
                yield return null;
            }
        }

        #endregion

        #region Critter

        void BuildCritter()
        {
            Ground(new Color(0.08f, 0.08f, 0.12f), 0.38f);
            var tile = HciMeetingVfx.Unlit(new Color(0.18f, 0.9f, 0.75f, 0.35f), true);
            for (var x = -2; x <= 2; x++)
            {
                for (var z = -2; z <= 2; z++)
                {
                    if ((x + z) % 2 != 0)
                        continue;
                    HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Rig.transform, "Tile", new Vector3(x * 0.08f, 0.002f, z * 0.08f), Quaternion.identity, new Vector3(0.075f, 0.004f, 0.075f), tile);
                }
            }

            m_Actor = new GameObject("Bug").transform;
            m_Actor.SetParent(m_Rig.transform, false);
            m_Actor.localPosition = new Vector3(0f, 0.09f, 0f);
            var shell = HciMeetingVfx.LitGlow(new Color(0.9f, 0.1f, 0.18f), new Color(0.55f, 0.02f, 0.05f));
            var dark = HciMeetingVfx.Unlit(new Color(0.1f, 0.07f, 0.09f));
            var eye = HciMeetingVfx.LitGlow(new Color(0.2f, 1f, 0.85f), new Color(0.25f, 1.8f, 1.3f));
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Actor, "Body", Vector3.zero, Quaternion.identity, new Vector3(0.14f, 0.06f, 0.18f), shell);
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Actor, "Head", new Vector3(0f, 0.035f, 0.12f), Quaternion.identity, new Vector3(0.09f, 0.07f, 0.08f), shell);
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Actor, "EyeL", new Vector3(-0.03f, 0.05f, 0.155f), Quaternion.identity, Vector3.one * 0.03f, eye);
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Actor, "EyeR", new Vector3(0.03f, 0.05f, 0.155f), Quaternion.identity, Vector3.one * 0.03f, eye);
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Actor, "AntL", new Vector3(-0.02f, 0.09f, 0.14f), Quaternion.Euler(-25f, 0f, 18f), new Vector3(0.012f, 0.06f, 0.012f), dark);
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Actor, "AntR", new Vector3(0.02f, 0.09f, 0.14f), Quaternion.Euler(-25f, 0f, -18f), new Vector3(0.012f, 0.06f, 0.012f), dark);
            for (var i = 0; i < 6; i++)
            {
                var side = i < 3 ? -1f : 1f;
                var z = (i % 3 - 1) * 0.05f;
                HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Actor, "Leg" + i, new Vector3(side * 0.09f, -0.035f, z), Quaternion.Euler(0f, 0f, side * 38f), new Vector3(0.06f, 0.014f, 0.014f), dark);
            }
        }

        IEnumerator PlayCritter(EditDimension dimension)
        {
            var bug = m_Actor != null ? m_Actor : m_Rig.transform.Find("Bug");
            switch (dimension)
            {
                case EditDimension.Appearance:
                    Pixelize(bug);
                    HciMeetingVfx.Make(m_Rig.transform, "PxBurst", new Color(0.3f, 1.5f, 1.2f, 1f), 36, 0.28f, 0.035f, 0.55f, false, HciMeetingStore.Tex("GlowCircle"), true, 0f, 0.08f, false, 0);
                    yield return PunchScale(1.22f, 0.28f);
                    break;
                case EditDimension.Agency:
                    yield return StepDance(bug);
                    break;
                case EditDimension.Rule:
                    yield return StunSpin(bug);
                    break;
                case EditDimension.Deconstruction:
                    if (bug != null)
                        bug.gameObject.SetActive(false);
                    HciMeetingStore.CoinExplosion(m_Anchor.position + Vector3.up * 0.14f);
                    HciMeetingVfx.Label(m_Rig.transform, "Exp", "+100 EXP", new Vector3(0f, 0.32f, 0f), new Color(1f, 0.92f, 0.2f), 0.016f);
                    HciMeetingStore.AuraRing(m_Rig.transform, new Vector3(0f, 0.02f, 0f), new Color(1.5f, 1.2f, 0.2f, 1f));
                    break;
            }
        }

        static void Pixelize(Transform bug)
        {
            if (bug == null)
                return;
            var palette = new[]
            {
                new Color(1f, 0.12f, 0.38f),
                new Color(0.12f, 1f, 0.88f),
                new Color(1f, 0.92f, 0.12f),
                new Color(0.28f, 0.4f, 1f),
                new Color(1f, 0.45f, 0.1f)
            };
            var i = 0;
            foreach (var r in bug.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer)
                    continue;
                r.sharedMaterial = HciMeetingVfx.Unlit(palette[i % palette.Length]);
                i++;
            }
        }

        IEnumerator StepDance(Transform bug)
        {
            if (bug == null)
                yield break;
            var origin = bug.localPosition;
            for (var i = 0; i < 12; i++)
            {
                var side = i % 2 == 0 ? -1f : 1f;
                bug.localPosition = origin + new Vector3(side * 0.07f, (i % 2) * 0.06f, 0f);
                bug.localRotation = Quaternion.Euler(0f, 22f * side, 8f * side);
                bug.localScale = new Vector3(1.15f, 0.82f, 1.15f);
                yield return new WaitForSeconds(0.08f);
                bug.localScale = Vector3.one;
                yield return new WaitForSeconds(0.04f);
            }

            bug.localPosition = origin;
            bug.localRotation = Quaternion.identity;
            bug.localScale = Vector3.one;
        }

        IEnumerator StunSpin(Transform bug)
        {
            if (bug == null)
                yield break;
            var stars = HciMeetingVfx.Make(m_Rig.transform, "Stars", new Color(1.7f, 1.35f, 0.2f, 1f), 28, 0.14f, 0.035f, 0.7f, true, HciMeetingStore.Tex("StarFlame") ?? HciMeetingStore.Tex("GlowCircle"), true, 0f, 0.1f, false, 0);
            stars.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            var t = 0f;
            while (t < 1.5f)
            {
                t += Time.deltaTime;
                bug.Rotate(0f, 720f * Time.deltaTime, 0f, Space.Self);
                bug.localPosition = new Vector3(0f, 0.09f + 0.04f * Mathf.Sin(t * 18f), 0f);
                yield return null;
            }

            Destroy(stars.gameObject);
            bug.localPosition = new Vector3(0f, 0.09f, 0f);
            bug.localRotation = Quaternion.identity;
        }

        #endregion

        #region Statue

        void BuildStatue()
        {
            Ground(new Color(0.28f, 0.26f, 0.22f), 0.42f);
            var stone = HciMeetingVfx.LitGlow(new Color(0.58f, 0.54f, 0.46f), new Color(0.08f, 0.07f, 0.05f));
            var dark = HciMeetingVfx.LitGlow(new Color(0.32f, 0.28f, 0.22f), Color.black);
            var bronze = HciMeetingVfx.LitGlow(new Color(0.62f, 0.42f, 0.16f), new Color(0.25f, 0.12f, 0.02f));
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Rig.transform, "Plinth", new Vector3(0f, 0.05f, 0f), Quaternion.identity, new Vector3(0.22f, 0.1f, 0.22f), dark);
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Rig.transform, "PlinthTop", new Vector3(0f, 0.11f, 0f), Quaternion.identity, new Vector3(0.16f, 0.03f, 0.16f), bronze);

            m_Actor = new GameObject("Lion").transform;
            m_Actor.SetParent(m_Rig.transform, false);
            m_Actor.localPosition = Vector3.zero;
            HciMeetingVfx.Primitive(PrimitiveType.Capsule, m_Actor, "Body", new Vector3(0f, 0.24f, 0f), Quaternion.identity, new Vector3(0.1f, 0.12f, 0.09f), stone);
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Actor, "Head", new Vector3(0f, 0.4f, 0.03f), Quaternion.identity, Vector3.one * 0.1f, stone);
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Actor, "Snout", new Vector3(0f, 0.37f, 0.09f), Quaternion.identity, new Vector3(0.055f, 0.045f, 0.07f), stone);
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Actor, "EarL", new Vector3(-0.055f, 0.47f, 0f), Quaternion.Euler(0f, 0f, 18f), new Vector3(0.032f, 0.055f, 0.022f), stone);
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Actor, "EarR", new Vector3(0.055f, 0.47f, 0f), Quaternion.Euler(0f, 0f, -18f), new Vector3(0.032f, 0.055f, 0.022f), stone);
            var mane = HciMeetingVfx.LitGlow(new Color(0.48f, 0.42f, 0.32f), new Color(0.12f, 0.08f, 0.04f));
            for (var i = 0; i < 8; i++)
            {
                var a = i / 8f * Mathf.PI * 2f;
                HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Actor, "Mane" + i, new Vector3(Mathf.Cos(a) * 0.08f, 0.4f, 0.02f + Mathf.Sin(a) * 0.05f), Quaternion.identity, Vector3.one * 0.055f, mane);
            }

            HciMeetingVfx.Primitive(PrimitiveType.Capsule, m_Actor, "ArmL", new Vector3(-0.11f, 0.22f, 0.03f), Quaternion.Euler(0f, 0f, 28f), new Vector3(0.032f, 0.09f, 0.032f), stone);
            HciMeetingVfx.Primitive(PrimitiveType.Capsule, m_Actor, "ArmR", new Vector3(0.11f, 0.22f, 0.03f), Quaternion.Euler(0f, 0f, -28f), new Vector3(0.032f, 0.09f, 0.032f), stone);
            HciMeetingVfx.Primitive(PrimitiveType.Capsule, m_Actor, "LegL", new Vector3(-0.05f, 0.14f, 0.02f), Quaternion.identity, new Vector3(0.03f, 0.07f, 0.03f), stone);
            HciMeetingVfx.Primitive(PrimitiveType.Capsule, m_Actor, "LegR", new Vector3(0.05f, 0.14f, 0.02f), Quaternion.identity, new Vector3(0.03f, 0.07f, 0.03f), stone);
            HciMeetingVfx.Primitive(PrimitiveType.Capsule, m_Actor, "Tail", new Vector3(0f, 0.2f, -0.1f), Quaternion.Euler(55f, 0f, 0f), new Vector3(0.022f, 0.09f, 0.022f), stone);
        }

        IEnumerator PlayStatue(EditDimension dimension)
        {
            switch (dimension)
            {
                case EditDimension.Appearance:
                    TintNamedGlow("Body", new Color(0.2f, 0.9f, 1f), new Color(0.2f, 1.2f, 1.8f));
                    TintNamedGlow("Head", new Color(1f, 0.4f, 0.85f), new Color(1.6f, 0.3f, 1.1f));
                    TintNamedGlow("Snout", new Color(1f, 0.78f, 0.25f), new Color(1.8f, 1f, 0.2f));
                    TintNamedGlow("ArmL", new Color(0.45f, 1f, 0.55f), new Color(0.3f, 1.4f, 0.4f));
                    TintNamedGlow("ArmR", new Color(0.45f, 1f, 0.55f), new Color(0.3f, 1.4f, 0.4f));
                    HciMeetingVfx.Make(m_Rig.transform, "Sparkle", new Color(1.6f, 0.95f, 1.5f, 1f), 48, 0.22f, 0.035f, 1.1f, false, HciMeetingStore.Tex("GlowCircle"), true, 0f, 0.12f, false, 0);
                    yield return PunchScale(1.16f, 0.3f);
                    break;
                case EditDimension.Agency:
                    yield return Yawn();
                    break;
                case EditDimension.Rule:
                    HciMeetingVfx.Label(m_Rig.transform, "Lore", "铜兽：我在这儿站了两百年。", new Vector3(0f, 0.62f, 0f), Color.white, 0.011f);
                    HciMeetingVfx.Make(m_Rig.transform, "Dust", new Color(0.75f, 0.68f, 0.5f, 0.85f), 24, 0.08f, 0.04f, 1.4f, false, HciMeetingStore.Tex("SmokeLoop"), false, 0.05f, 0.08f, false, 0);
                    break;
                case EditDimension.Deconstruction:
                    for (var i = 0; i < 8; i++)
                    {
                        var a = i / 8f * Mathf.PI * 2f;
                        var lotus = HciMeetingStore.CreateRewardFlower();
                        lotus.name = "Lily" + i;
                        lotus.transform.SetParent(m_Rig.transform, false);
                        lotus.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.28f, 0.02f, Mathf.Sin(a) * 0.28f);
                        lotus.transform.localScale = Vector3.one * 0.28f;
                    }

                    HciMeetingVfx.Primitive(PrimitiveType.Cylinder, m_Rig.transform, "WishTree", new Vector3(0.32f, 0.24f, 0.05f), new Vector3(0.032f, 0.24f, 0.032f), new Color(0.35f, 0.2f, 0.08f));
                    HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "WishCrown", new Vector3(0.32f, 0.48f, 0.05f), Vector3.one * 0.16f, new Color(1f, 0.85f, 0.3f, 0.8f), true);
                    HciMeetingStore.Sakura(m_Rig.transform);
                    HciMeetingStore.BloomReward(null, m_Anchor.position + Vector3.up * 0.12f);
                    break;
            }
        }

        IEnumerator Yawn()
        {
            var body = m_Actor != null ? m_Actor.Find("Body") : m_Rig.transform.Find("Body");
            var head = m_Actor != null ? m_Actor.Find("Head") : m_Rig.transform.Find("Head");
            var armL = m_Actor != null ? m_Actor.Find("ArmL") : null;
            var armR = m_Actor != null ? m_Actor.Find("ArmR") : null;
            if (body == null)
                yield break;
            var t = 0f;
            while (t < 1.25f)
            {
                t += Time.deltaTime;
                var k = Mathf.Sin(t * Mathf.PI);
                body.localScale = new Vector3(0.1f, 0.12f + 0.06f * k, 0.09f);
                if (head != null)
                    head.localRotation = Quaternion.Euler(-22f * k, 0f, 0f);
                if (armL != null)
                    armL.localRotation = Quaternion.Euler(-40f * k, 0f, 28f);
                if (armR != null)
                    armR.localRotation = Quaternion.Euler(-40f * k, 0f, -28f);
                yield return null;
            }
        }

        #endregion

        #region Bedroom

        void BuildBedroom()
        {
            var wood = HciMeetingVfx.LitGlow(new Color(0.42f, 0.28f, 0.16f), Color.black);
            var cream = HciMeetingVfx.Unlit(new Color(0.82f, 0.78f, 0.7f));
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Rig.transform, "Floor", new Vector3(0f, 0f, 0.08f), new Vector3(0.78f, 0.014f, 0.56f), new Color(0.36f, 0.24f, 0.14f));
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Rig.transform, "Back", new Vector3(0f, 0.24f, 0.35f), Quaternion.identity, new Vector3(0.78f, 0.48f, 0.014f), cream);
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Rig.transform, "SideL", new Vector3(-0.39f, 0.2f, 0.08f), new Vector3(0.014f, 0.4f, 0.54f), new Color(0.74f, 0.7f, 0.62f));
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Rig.transform, "Ceil", new Vector3(0f, 0.46f, 0.08f), new Vector3(0.78f, 0.012f, 0.56f), new Color(0.88f, 0.85f, 0.78f));
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Rig.transform, "Bed", new Vector3(-0.14f, 0.06f, 0.02f), Quaternion.identity, new Vector3(0.32f, 0.07f, 0.4f), wood);
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Rig.transform, "Blanket", new Vector3(-0.14f, 0.1f, 0.06f), new Vector3(0.3f, 0.03f, 0.28f), new Color(0.55f, 0.22f, 0.28f));
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Rig.transform, "Pillow", new Vector3(-0.14f, 0.12f, -0.12f), new Vector3(0.18f, 0.05f, 0.12f), new Color(0.92f, 0.9f, 0.82f));
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Rig.transform, "Desk", new Vector3(0.24f, 0.08f, 0.05f), Quaternion.identity, new Vector3(0.18f, 0.09f, 0.18f), wood);
            HciMeetingVfx.Primitive(PrimitiveType.Cylinder, m_Rig.transform, "Lamp", new Vector3(0.24f, 0.16f, 0.05f), Quaternion.identity, new Vector3(0.03f, 0.04f, 0.03f), HciMeetingVfx.LitGlow(new Color(1f, 0.85f, 0.45f), new Color(1.6f, 1.1f, 0.3f)));
            HciMeetingVfx.Primitive(PrimitiveType.Quad, m_Rig.transform, "Poster", new Vector3(-0.22f, 0.3f, 0.342f), new Vector3(0.16f, 0.2f, 1f), new Color(0.95f, 0.55f, 0.35f));
            var wash = HciMeetingVfx.Primitive(PrimitiveType.Quad, m_Rig.transform, "Wash", new Vector3(0f, 0.26f, 0.342f), new Vector3(0.76f, 0.46f, 1f), new Color(0.55f, 0.85f, 0.75f, 0.22f), true);
            wash.SetActive(true);
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Rig.transform, "Frame", new Vector3(0.24f, 0.28f, 0.348f), new Vector3(0.24f, 0.32f, 0.012f), new Color(0.25f, 0.18f, 0.1f));
            m_Portal = HciMeetingVfx.Primitive(PrimitiveType.Quad, m_Rig.transform, "Window", new Vector3(0.24f, 0.28f, 0.355f), new Vector3(0.2f, 0.28f, 1f), new Color(0.35f, 0.65f, 1f, 0.75f), true).transform;
            m_Portal.gameObject.SetActive(false);
            m_Jellyfish = new GameObject("Jellyfish").transform;
            m_Jellyfish.SetParent(m_Rig.transform, false);
            var jelly = HciMeetingVfx.Unlit(new Color(0.75f, 0.4f, 1f, 0.8f), true);
            var tentMat = HciMeetingVfx.Unlit(new Color(0.9f, 0.55f, 1f, 0.55f), true);
            for (var i = 0; i < 4; i++)
            {
                var body = HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Jellyfish, "J" + i, Vector3.zero, Quaternion.identity, Vector3.one * 0.08f, jelly);
                HciMeetingVfx.Primitive(PrimitiveType.Capsule, body.transform, "Tent", new Vector3(0f, -0.7f, 0f), Quaternion.identity, new Vector3(0.25f, 0.7f, 0.25f), tentMat);
            }

            m_Jellyfish.gameObject.SetActive(false);
        }

        IEnumerator PlayBedroom(EditDimension dimension)
        {
            switch (dimension)
            {
                case EditDimension.Appearance:
                    m_StyleIndex = 1 - m_StyleIndex;
                    var wash = m_Rig.transform.Find("Wash");
                    var cyber = m_StyleIndex == 1;
                    if (wash != null)
                    {
                        wash.gameObject.SetActive(true);
                        wash.GetComponent<Renderer>().sharedMaterial = HciMeetingVfx.Unlit(
                            cyber ? new Color(0.9f, 0.12f, 1f, 0.42f) : new Color(0.55f, 0.88f, 0.75f, 0.32f),
                            true);
                    }

                    var back = m_Rig.transform.Find("Back");
                    if (back != null)
                        back.GetComponent<Renderer>().sharedMaterial = HciMeetingVfx.Unlit(
                            cyber ? new Color(0.1f, 0.04f, 0.18f) : new Color(0.82f, 0.78f, 0.7f));
                    var blanket = m_Rig.transform.Find("Blanket");
                    if (blanket != null)
                        blanket.GetComponent<Renderer>().sharedMaterial = HciMeetingVfx.Unlit(
                            cyber ? new Color(0.15f, 0.85f, 1f) : new Color(0.55f, 0.22f, 0.28f));
                    HciMeetingVfx.Make(m_Rig.transform, "StyleFlash", cyber ? new Color(1.3f, 0.2f, 1.6f) : new Color(0.6f, 1.3f, 0.95f), 32, 0.2f, 0.05f, 0.7f, false, HciMeetingStore.Tex("GlowCircle"), true, 0f, 0.14f, false, 0);
                    break;
                case EditDimension.Agency:
                    if (m_Portal != null)
                    {
                        m_Portal.gameObject.SetActive(true);
                        HciMeetingVfx.Make(m_Portal, "Sky", new Color(0.55f, 0.85f, 1.6f, 1f), 28, 0.06f, 0.045f, 1.4f, true, HciMeetingStore.Tex("GlowCircle"), true, 0f, 0.05f, false, 0);
                    }

                    break;
                case EditDimension.Rule:
                    if (m_Jellyfish != null)
                        m_Jellyfish.gameObject.SetActive(true);
                    HciMeetingVfx.Make(m_Rig.transform, "Glow", new Color(0.8f, 0.4f, 1.5f, 1f), 20, 0.08f, 0.04f, 1.2f, false, HciMeetingStore.Tex("GlowCircle"), true, 0f, 0.16f, false, 0);
                    break;
                case EditDimension.Deconstruction:
                    HciMeetingStore.CandyRain(m_Rig.transform);
                    break;
            }

            yield break;
        }

        #endregion

        #region Plant

        void BuildPlant()
        {
            Ground(new Color(0.22f, 0.16f, 0.08f), 0.28f);
            var pot = HciMeetingVfx.LitGlow(new Color(0.72f, 0.22f, 0.14f), new Color(0.25f, 0.04f, 0.02f));
            var soil = HciMeetingVfx.Unlit(new Color(0.18f, 0.1f, 0.06f));
            var green = HciMeetingVfx.LitGlow(new Color(0.2f, 0.78f, 0.28f), new Color(0.08f, 0.45f, 0.1f));
            HciMeetingVfx.Primitive(PrimitiveType.Cylinder, m_Rig.transform, "Pot", new Vector3(0f, 0.05f, 0f), Quaternion.identity, new Vector3(0.11f, 0.05f, 0.11f), pot);
            HciMeetingVfx.Primitive(PrimitiveType.Cylinder, m_Rig.transform, "Rim", new Vector3(0f, 0.09f, 0f), Quaternion.identity, new Vector3(0.12f, 0.014f, 0.12f), pot);
            HciMeetingVfx.Primitive(PrimitiveType.Cylinder, m_Rig.transform, "Soil", new Vector3(0f, 0.085f, 0f), Quaternion.identity, new Vector3(0.095f, 0.012f, 0.095f), soil);
            HciMeetingVfx.Primitive(PrimitiveType.Cylinder, m_Rig.transform, "Stem", new Vector3(0f, 0.18f, 0f), Quaternion.identity, new Vector3(0.016f, 0.09f, 0.016f), green);
            var leaf = HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Leaf", new Vector3(0f, 0.28f, 0f), Quaternion.identity, Vector3.one * 0.08f, green);
            for (var i = 0; i < 6; i++)
            {
                var yaw = i * 60f;
                HciMeetingVfx.Primitive(PrimitiveType.Sphere, leaf.transform, "L" + i, Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, -0.15f, 0.75f), Quaternion.Euler(32f, yaw, 0f), new Vector3(0.75f, 0.16f, 1.15f), green);
            }

            var bud = HciMeetingVfx.LitGlow(new Color(1f, 0.35f, 0.55f), new Color(1.6f, 0.2f, 0.6f));
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, leaf.transform, "Bud", new Vector3(0f, 0.55f, 0f), Quaternion.identity, Vector3.one * 0.35f, bud);
        }

        IEnumerator PlayPlant(EditDimension dimension)
        {
            switch (dimension)
            {
                case EditDimension.Appearance:
                    HciMeetingStore.AuraRing(m_Rig.transform, new Vector3(0f, 0.02f, 0f), new Color(0.45f, 1.4f, 0.5f, 1f));
                    var leaf = m_Rig.transform.Find("Leaf");
                    if (leaf != null)
                    {
                        var colors = new[]
                        {
                            new Color(0.22f, 0.95f, 0.35f),
                            new Color(0.12f, 0.12f, 0.12f),
                            new Color(0.95f, 0.2f, 0.5f)
                        };
                        m_StyleIndex = (m_StyleIndex + 1) % colors.Length;
                        TintChildren(leaf, colors[m_StyleIndex]);
                    }

                    break;
                case EditDimension.Agency:
                    HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "EyeL", new Vector3(-0.025f, 0.24f, 0.06f), Vector3.one * 0.022f, Color.white);
                    HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "EyeR", new Vector3(0.025f, 0.24f, 0.06f), Vector3.one * 0.022f, Color.white);
                    HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "PupilL", new Vector3(-0.025f, 0.24f, 0.07f), Vector3.one * 0.01f, Color.black);
                    HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "PupilR", new Vector3(0.025f, 0.24f, 0.07f), Vector3.one * 0.01f, Color.black);
                    HciMeetingVfx.Label(m_Rig.transform, "Talk", "水分 42%\n今天又长了一点。", new Vector3(0f, 0.48f, 0f), new Color(0.85f, 1f, 0.75f), 0.01f);
                    yield return PunchScale(1.14f, 0.35f);
                    break;
                case EditDimension.Rule:
                    m_PlantAge = m_PlantAge == 0 ? 2 : 0;
                    yield return GrowPlant(m_PlantAge == 0);
                    break;
                case EditDimension.Deconstruction:
                    HciMeetingVfx.Make(null, "Puff", new Color(0.5f, 0.9f, 0.4f), 28, 0.22f, 0.04f, 0.7f, false, HciMeetingStore.Tex("SmokeLoop"), false, 0.12f, 0.05f, false, 0, m_Anchor.position + Vector3.up * 0.14f);
                    RebuildRig();
                    break;
            }
        }

        #endregion

        #region Pet

        void BuildPet()
        {
            Ground(new Color(0.85f, 0.78f, 0.68f), 0.32f);
            m_Actor = new GameObject("Cat").transform;
            m_Actor.SetParent(m_Rig.transform, false);
            var fur = HciMeetingVfx.LitGlow(new Color(0.96f, 0.78f, 0.48f), new Color(0.25f, 0.12f, 0.04f));
            var dark = HciMeetingVfx.Unlit(new Color(0.12f, 0.08f, 0.06f));
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Actor, "Body", new Vector3(0f, 0.09f, 0f), Quaternion.identity, new Vector3(0.16f, 0.1f, 0.12f), fur);
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Actor, "Head", new Vector3(0.07f, 0.17f, 0.03f), Quaternion.identity, Vector3.one * 0.09f, fur);
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Actor, "EarL", new Vector3(0.035f, 0.25f, 0.02f), Quaternion.Euler(0f, 0f, 18f), new Vector3(0.032f, 0.05f, 0.016f), fur);
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Actor, "EarR", new Vector3(0.1f, 0.25f, 0.02f), Quaternion.Euler(0f, 0f, -18f), new Vector3(0.032f, 0.05f, 0.016f), fur);
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Actor, "EyeL", new Vector3(0.08f, 0.18f, 0.075f), Quaternion.identity, Vector3.one * 0.02f, HciMeetingVfx.Unlit(Color.white));
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Actor, "EyeR", new Vector3(0.115f, 0.18f, 0.07f), Quaternion.identity, Vector3.one * 0.02f, HciMeetingVfx.Unlit(Color.white));
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Actor, "PupilL", new Vector3(0.082f, 0.18f, 0.085f), Quaternion.identity, Vector3.one * 0.009f, dark);
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Actor, "Nose", new Vector3(0.115f, 0.16f, 0.08f), Quaternion.identity, Vector3.one * 0.014f, dark);
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Actor, "WhiskL", new Vector3(0.06f, 0.155f, 0.09f), Quaternion.Euler(0f, 25f, 0f), new Vector3(0.07f, 0.004f, 0.004f), dark);
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Actor, "WhiskR", new Vector3(0.15f, 0.155f, 0.08f), Quaternion.Euler(0f, -25f, 0f), new Vector3(0.07f, 0.004f, 0.004f), dark);
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Actor, "PawFL", new Vector3(0.05f, 0.03f, 0.05f), Quaternion.identity, Vector3.one * 0.035f, fur);
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Actor, "PawFR", new Vector3(0.08f, 0.03f, 0.04f), Quaternion.identity, Vector3.one * 0.035f, fur);
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Actor, "PawBL", new Vector3(-0.05f, 0.03f, 0.02f), Quaternion.identity, Vector3.one * 0.038f, fur);
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Actor, "PawBR", new Vector3(-0.02f, 0.03f, -0.02f), Quaternion.identity, Vector3.one * 0.038f, fur);
            m_Tail = HciMeetingVfx.Primitive(PrimitiveType.Capsule, m_Actor, "Tail", new Vector3(-0.1f, 0.12f, -0.02f), Quaternion.Euler(0f, 0f, 40f), new Vector3(0.028f, 0.09f, 0.028f), fur).transform;
            m_DreamBubble = HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Dream", new Vector3(0.16f, 0.38f, 0f), Quaternion.identity, Vector3.one * 0.18f, HciMeetingVfx.Unlit(new Color(0.55f, 0.85f, 1f, 0.42f), true)).transform;
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_DreamBubble, "Fish", Vector3.zero, Quaternion.identity, new Vector3(0.38f, 0.14f, 0.22f), HciMeetingVfx.LitGlow(new Color(1f, 0.5f, 0.15f), new Color(1.8f, 0.45f, 0.05f)));
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_DreamBubble, "TailFin", new Vector3(-0.28f, 0f, 0f), Quaternion.identity, new Vector3(0.16f, 0.2f, 0.08f), HciMeetingVfx.LitGlow(new Color(1f, 0.35f, 0.1f), new Color(1.5f, 0.3f, 0.05f)));
            m_DreamBubble.gameObject.SetActive(false);
        }

        IEnumerator PlayPet(EditDimension dimension)
        {
            switch (dimension)
            {
                case EditDimension.Appearance:
                    HciMeetingStore.InkSplash(m_Anchor.position + Vector3.up * 0.1f);
                    TintNamed("Body", Color.white);
                    TintNamed("Head", new Color(0.06f, 0.06f, 0.08f));
                    TintNamed("EarL", new Color(0.06f, 0.06f, 0.08f));
                    TintNamed("EarR", Color.white);
                    TintNamed("PawFL", Color.white);
                    TintNamed("PawBR", new Color(0.06f, 0.06f, 0.08f));
                    break;
                case EditDimension.Agency:
                    if (m_DreamBubble != null)
                        m_DreamBubble.gameObject.SetActive(true);
                    HciMeetingVfx.Label(m_Rig.transform, "Zzz", "Zzz", new Vector3(0.28f, 0.42f, 0f), new Color(0.7f, 0.85f, 1f), 0.014f);
                    break;
                case EditDimension.Rule:
                    if (m_DreamBubble != null)
                    {
                        HciMeetingVfx.Make(m_DreamBubble, "Pop", Color.white, 28, 0.3f, 0.03f, 0.55f, false, HciMeetingStore.Tex("GlowCircle"), true, 0f, 0.05f, false, 0);
                        m_DreamBubble.gameObject.SetActive(false);
                    }

                    yield return PunchScale(1.22f, 0.28f);
                    break;
                case EditDimension.Deconstruction:
                    HciMeetingVfx.Make(m_Rig.transform, "Hearts", new Color(1.7f, 0.25f, 0.55f, 1f), 36, 0.26f, 0.045f, 1.2f, false, HciMeetingStore.Tex("GlowCircle"), true, -0.18f, 0.06f, false, 0);
                    HciMeetingVfx.Label(m_Rig.transform, "Affinity", "好感度 +1", new Vector3(0f, 0.42f, 0f), new Color(1f, 0.4f, 0.6f));
                    if (m_DreamBubble != null)
                        m_DreamBubble.gameObject.SetActive(false);
                    break;
            }
        }

        #endregion

        #region Season tree

        void BuildSeasonTree()
        {
            Ground(new Color(0.28f, 0.42f, 0.18f), 0.4f);
            var bark = HciMeetingVfx.LitGlow(new Color(0.38f, 0.22f, 0.1f), Color.black);
            var green = HciMeetingVfx.LitGlow(new Color(0.2f, 0.72f, 0.24f), new Color(0.06f, 0.32f, 0.08f));
            HciMeetingVfx.Primitive(PrimitiveType.Cylinder, m_Rig.transform, "Trunk", new Vector3(0f, 0.16f, 0f), Quaternion.identity, new Vector3(0.055f, 0.16f, 0.055f), bark);
            HciMeetingVfx.Primitive(PrimitiveType.Cylinder, m_Rig.transform, "BoughL", new Vector3(-0.06f, 0.28f, 0f), Quaternion.Euler(0f, 0f, 38f), new Vector3(0.028f, 0.08f, 0.028f), bark);
            HciMeetingVfx.Primitive(PrimitiveType.Cylinder, m_Rig.transform, "BoughR", new Vector3(0.06f, 0.28f, 0f), Quaternion.Euler(0f, 0f, -38f), new Vector3(0.028f, 0.08f, 0.028f), bark);
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Crown", new Vector3(0f, 0.38f, 0f), Quaternion.identity, Vector3.one * 0.22f, green);
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Crown2", new Vector3(0.12f, 0.33f, 0.05f), Quaternion.identity, Vector3.one * 0.14f, green);
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Crown3", new Vector3(-0.11f, 0.33f, -0.04f), Quaternion.identity, Vector3.one * 0.13f, green);
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Crown4", new Vector3(0.02f, 0.42f, -0.08f), Quaternion.identity, Vector3.one * 0.1f, green);
            m_Falling = HciMeetingVfx.Make(m_Rig.transform, "Fall", new Color(1f, 0.4f, 0.55f, 1f), 26, 0.06f, 0.03f, 1.8f, true, HciMeetingStore.Tex("StarFlame") ?? HciMeetingStore.Tex("SmokeNoise"), true, 0.55f, 0.14f, false, 0);
            m_Falling.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        IEnumerator PlaySeasonTree(EditDimension dimension)
        {
            switch (dimension)
            {
                case EditDimension.Appearance:
                    ApplyCrown(new Color(0.98f, 0.45f, 0.75f), new Color(1.5f, 0.3f, 0.75f));
                    HciMeetingStore.Sakura(m_Rig.transform);
                    break;
                case EditDimension.Agency:
                    HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "FaceL", new Vector3(-0.025f, 0.18f, 0.05f), Vector3.one * 0.024f, Color.white);
                    HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "FaceR", new Vector3(0.025f, 0.18f, 0.05f), Vector3.one * 0.024f, Color.white);
                    HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Smile", new Vector3(0f, 0.155f, 0.05f), new Vector3(0.04f, 0.012f, 0.012f), new Color(0.2f, 0.08f, 0.05f));
                    HciMeetingVfx.Label(m_Rig.transform, "Face", "今天想开花。", new Vector3(0f, 0.58f, 0f), Color.white, 0.01f);
                    yield return PunchScale(1.12f, 0.3f);
                    break;
                case EditDimension.Rule:
                    m_SeasonIndex = (m_SeasonIndex + 1) % 4;
                    var seasons = new[]
                    {
                        new Color(1f, 0.55f, 0.78f),
                        new Color(0.18f, 0.82f, 0.32f),
                        new Color(0.95f, 0.4f, 0.08f),
                        new Color(0.88f, 0.94f, 1f)
                    };
                    ApplyCrown(seasons[m_SeasonIndex], seasons[m_SeasonIndex] * 1.25f);
                    var crown = m_Rig.transform.Find("Crown");
                    if (crown != null)
                        crown.localScale = Vector3.one * (m_SeasonIndex == 3 ? 0.11f : 0.22f);
                    if (m_SeasonIndex == 0)
                        HciMeetingStore.Sakura(m_Rig.transform);
                    else if (m_SeasonIndex == 3)
                        HciMeetingStore.Snow(m_Rig.transform);
                    else if (m_SeasonIndex == 2)
                        m_Falling?.Play();
                    break;
                case EditDimension.Deconstruction:
                    m_Falling?.Play();
                    HciMeetingVfx.Make(m_Rig.transform, "Wind", new Color(1f, 0.7f, 0.3f, 1f), 52, 0.42f, 0.04f, 1.4f, false, HciMeetingStore.Tex("StarFlame"), true, 0.45f, 0.14f, false, 0);
                    break;
            }

            yield break;
        }

        void ApplyCrown(Color albedo, Color emit)
        {
            var mat = HciMeetingVfx.LitGlow(albedo, emit);
            foreach (var name in new[] { "Crown", "Crown2", "Crown3", "Crown4" })
            {
                var t = m_Rig.transform.Find(name);
                if (t != null)
                    t.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        #endregion

        IEnumerator GrowPlant(bool seedling)
        {
            var stem = m_Rig.transform.Find("Stem");
            var leaf = m_Rig.transform.Find("Leaf");
            var stemFrom = stem != null ? stem.localScale : Vector3.one;
            var leafFrom = leaf != null ? leaf.localScale : Vector3.one;
            var stemTo = seedling ? new Vector3(0.01f, 0.03f, 0.01f) : new Vector3(0.02f, 0.12f, 0.02f);
            var leafTo = Vector3.one * (seedling ? 0.04f : 0.12f);
            var t = 0f;
            while (t < 0.45f)
            {
                t += Time.deltaTime;
                var k = Mathf.SmoothStep(0f, 1f, t / 0.45f);
                if (stem != null)
                    stem.localScale = Vector3.Lerp(stemFrom, stemTo, k);
                if (leaf != null)
                    leaf.localScale = Vector3.Lerp(leafFrom, leafTo, k);
                yield return null;
            }
        }

        Vector3 RigScale()
        {
            return m_Scenario == HciMeetingScenario.LighterDemon ? Vector3.one : Vector3.one * 3.2f;
        }

        IEnumerator PunchScale(float peak, float duration)
        {
            var start = m_Anchor.localScale;
            var baseRig = RigScale();
            var t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                var k = Mathf.Clamp01(t / duration);
                var s = k < 0.5f
                    ? Mathf.SmoothStep(1f, peak, k * 2f)
                    : Mathf.SmoothStep(peak, 1f, (k - 0.5f) * 2f);
                if (m_Scenario == HciMeetingScenario.LighterDemon)
                    m_Anchor.localScale = start * s;
                else
                    m_Rig.transform.localScale = baseRig * s;
                yield return null;
            }

            if (m_Scenario == HciMeetingScenario.LighterDemon)
                m_Anchor.localScale = m_AnchorHiddenScale;
            else
                m_Rig.transform.localScale = baseRig;
        }

        IEnumerator Cough()
        {
            var start = m_Anchor.localPosition;
            for (var i = 0; i < 6; i++)
            {
                m_Anchor.localPosition = start + Vector3.up * (0.01f * ((i % 2) * 2 - 1));
                yield return new WaitForSeconds(0.07f);
            }

            m_Anchor.localPosition = start;
        }

        IEnumerator WindStagger()
        {
            var start = m_Anchor.rotation;
            var t = 0f;
            while (t < 0.8f)
            {
                t += Time.deltaTime;
                m_Anchor.rotation = start * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 16f) * 18f);
                yield return null;
            }

            m_Anchor.rotation = start;
        }

        void TintAnchor(Color dark, Color ember)
        {
            foreach (var renderer in m_Anchor.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.GetComponent<ParticleSystem>() != null)
                    continue;
                renderer.sharedMaterial = HciMeetingVfx.Unlit(Color.Lerp(dark, ember, 0.25f));
            }
        }

        void TintChildren(Transform root, Color color)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponent<ParticleSystem>() != null)
                    continue;
                renderer.sharedMaterial = HciMeetingVfx.Unlit(color);
            }
        }

        void TintNamed(string name, Color color)
        {
            var t = FindDeep(m_Rig.transform, name);
            if (t == null)
                return;
            var r = t.GetComponent<Renderer>();
            if (r != null)
                r.sharedMaterial = HciMeetingVfx.Unlit(color);
        }

        void TintNamedGlow(string name, Color albedo, Color emit)
        {
            var t = FindDeep(m_Rig.transform, name);
            if (t == null)
                return;
            var r = t.GetComponent<Renderer>();
            if (r != null)
                r.sharedMaterial = HciMeetingVfx.LitGlow(albedo, emit);
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
                return null;
            var direct = root.Find(name);
            if (direct != null)
                return direct;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
