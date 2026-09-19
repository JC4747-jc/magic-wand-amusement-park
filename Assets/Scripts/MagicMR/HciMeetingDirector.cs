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
        ParticleSystem m_Ash;
        ParticleSystem m_Falling;
        GameObject m_LoopFx;
        int m_SeasonIndex;
        int m_StyleIndex;
        int m_PlantAge = 1;
        Coroutine m_Motion;
        Vector3 m_AnchorHiddenScale = Vector3.one;
        bool m_SuppressRebuild;

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

            switch (m_Scenario)
            {
                case HciMeetingScenario.LighterDemon:
                    m_Motion = StartCoroutine(PlayLighter(dimension));
                    break;
                case HciMeetingScenario.WhompingWillow:
                    m_Motion = StartCoroutine(PlayWillow(dimension));
                    break;
                case HciMeetingScenario.PixelCritter:
                    m_Motion = StartCoroutine(PlayCritter(dimension));
                    break;
                case HciMeetingScenario.HistoricStatue:
                    m_Motion = StartCoroutine(PlayStatue(dimension));
                    break;
                case HciMeetingScenario.Bedroom:
                    m_Motion = StartCoroutine(PlayBedroom(dimension));
                    break;
                case HciMeetingScenario.DeskPlant:
                    m_Motion = StartCoroutine(PlayPlant(dimension));
                    break;
                case HciMeetingScenario.MangaPet:
                    m_Motion = StartCoroutine(PlayPet(dimension));
                    break;
                case HciMeetingScenario.SeasonTree:
                    m_Motion = StartCoroutine(PlaySeasonTree(dimension));
                    break;
            }
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

            m_Wings = m_Branches = m_Scar = m_DreamBubble = m_Portal = m_Jellyfish = null;
            m_Ash = m_Falling = null;
            m_LoopFx = null;

            m_Rig = new GameObject("HciScenarioRig");
            m_Rig.transform.SetParent(m_Anchor, false);
            m_Rig.transform.localPosition = Vector3.zero;
            m_Rig.transform.localRotation = Quaternion.identity;
            m_Rig.transform.localScale = Vector3.one;

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
            if (m_Wings != null && m_Wings.gameObject.activeSelf)
            {
                var flap = 18f * Mathf.Sin(Time.time * 8f);
                m_Wings.GetChild(0).localRotation = Quaternion.Euler(0f, 0f, 25f + flap);
                m_Wings.GetChild(1).localRotation = Quaternion.Euler(0f, 0f, -25f - flap);
            }

            if (m_Jellyfish != null && m_Jellyfish.gameObject.activeSelf)
            {
                for (var i = 0; i < m_Jellyfish.childCount; i++)
                {
                    var c = m_Jellyfish.GetChild(i);
                    c.localPosition = new Vector3(
                        Mathf.Sin(Time.time * 0.6f + i) * 0.18f,
                        0.25f + 0.08f * Mathf.Sin(Time.time * 1.4f + i),
                        Mathf.Cos(Time.time * 0.5f + i) * 0.12f);
                }
            }

            if (m_DreamBubble != null && m_DreamBubble.gameObject.activeSelf)
            {
                var s = 1f + 0.06f * Mathf.Sin(Time.time * 2.4f);
                m_DreamBubble.localScale = Vector3.one * 0.12f * s;
            }
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
                    break;
            }
        }

        #endregion

        #region Willow

        void BuildWillow()
        {
            HciMeetingVfx.Primitive(PrimitiveType.Cylinder, m_Rig.transform, "Trunk", new Vector3(0f, 0.18f, 0f), new Vector3(0.06f, 0.18f, 0.06f), new Color(0.35f, 0.22f, 0.12f));
            m_Branches = new GameObject("Branches").transform;
            m_Branches.SetParent(m_Rig.transform, false);
            m_Branches.localPosition = new Vector3(0f, 0.34f, 0f);
            for (var i = 0; i < 4; i++)
            {
                var b = HciMeetingVfx.Primitive(PrimitiveType.Capsule, m_Branches, "B" + i, Vector3.zero, new Vector3(0.018f, 0.16f, 0.018f), new Color(0.28f, 0.18f, 0.08f));
                b.transform.localRotation = Quaternion.Euler(0f, i * 90f, 35f);
            }

            m_Scar = HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Scar", new Vector3(0f, 0.22f, 0.035f), Vector3.one * 0.035f, new Color(1f, 0.15f, 0.05f)).transform;
            m_Scar.gameObject.SetActive(false);
        }

        IEnumerator PlayWillow(EditDimension dimension)
        {
            switch (dimension)
            {
                case EditDimension.Appearance:
                    TintChildren(m_Rig.transform, new Color(0.05f, 0.04f, 0.06f));
                    HciMeetingStore.DarkFog(m_Rig.transform);
                    break;
                case EditDimension.Rule:
                    yield return WhipBranches();
                    break;
                case EditDimension.Agency:
                    if (m_Scar != null)
                        m_Scar.gameObject.SetActive(true);
                    if (m_LoopFx == null)
                        m_LoopFx = HciMeetingStore.ScarFire(m_Scar != null ? m_Scar : m_Rig.transform, Vector3.zero);
                    yield return PulseScar();
                    break;
                case EditDimension.Deconstruction:
                    TintChildren(m_Rig.transform, new Color(0.45f, 0.7f, 1f));
                    if (m_Branches != null)
                        m_Branches.localRotation = Quaternion.identity;
                    if (m_LoopFx != null)
                    {
                        Object.Destroy(m_LoopFx);
                        m_LoopFx = null;
                    }
                    HciMeetingStore.ChestReveal(m_Rig.transform, new Vector3(0f, 0.02f, 0.14f));
                    break;
            }
        }

        IEnumerator WhipBranches()
        {
            if (m_Branches == null)
                yield break;
            var t = 0f;
            while (t < 1.1f)
            {
                t += Time.deltaTime;
                var k = Mathf.Sin(t * 14f) * 42f;
                m_Branches.localRotation = Quaternion.Euler(k, 0f, k * 0.4f);
                yield return null;
            }

            m_Branches.localRotation = Quaternion.identity;
        }

        IEnumerator PulseScar()
        {
            var t = 0f;
            while (t < 1.4f && m_Scar != null)
            {
                t += Time.deltaTime;
                var s = 0.035f * (1f + 0.45f * Mathf.Sin(t * 10f));
                m_Scar.localScale = Vector3.one * s;
                yield return null;
            }
        }

        #endregion

        #region Critter

        void BuildCritter()
        {
            var body = HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Rig.transform, "Bug", new Vector3(0f, 0.08f, 0f), new Vector3(0.08f, 0.03f, 0.12f), new Color(0.25f, 0.12f, 0.08f));
            body.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
        }

        IEnumerator PlayCritter(EditDimension dimension)
        {
            var bug = m_Rig.transform.Find("Bug");
            switch (dimension)
            {
                case EditDimension.Appearance:
                    Pixelize(bug);
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
                    HciMeetingStore.CoinExplosion(m_Anchor.position + Vector3.up * 0.1f);
                    HciMeetingVfx.Label(m_Rig.transform, "Exp", "+100 EXP", new Vector3(0f, 0.22f, 0f), new Color(1f, 0.9f, 0.2f));
                    break;
            }
        }

        static void Pixelize(Transform bug)
        {
            if (bug == null)
                return;
            bug.GetComponent<Renderer>().sharedMaterial = HciMeetingVfx.Unlit(new Color(0.95f, 0.15f, 0.22f));
            bug.localScale = new Vector3(0.1f, 0.03f, 0.1f);
            if (bug.Find("Px0") != null)
                return;
            var palette = new[]
            {
                new Color(0.2f, 0.95f, 0.85f),
                new Color(1f, 0.85f, 0.1f),
                new Color(0.15f, 0.2f, 1f)
            };
            for (var i = 0; i < 6; i++)
            {
                var voxel = HciMeetingVfx.Primitive(
                    PrimitiveType.Cube,
                    bug,
                    "Px" + i,
                    new Vector3((i % 3 - 1) * 0.35f, 0.8f, (i / 3 - 0.5f) * 0.4f),
                    Vector3.one * 0.28f,
                    palette[i % palette.Length]);
                voxel.transform.localRotation = Quaternion.identity;
            }
        }

        IEnumerator StepDance(Transform bug)
        {
            if (bug == null)
                yield break;
            for (var i = 0; i < 8; i++)
            {
                bug.localPosition = new Vector3((i % 2 == 0 ? -1f : 1f) * 0.04f, 0.08f + (i % 2) * 0.03f, 0f);
                yield return new WaitForSeconds(0.12f);
            }
        }

        IEnumerator StunSpin(Transform bug)
        {
            if (bug == null)
                yield break;
            var stars = HciMeetingVfx.Burst(m_Rig.transform, "Stars", Color.yellow, 12, 0.08f, 0.012f, 0.5f, true, HciMeetingStore.Tex("StarFlame"));
            var t = 0f;
            while (t < 1.2f)
            {
                t += Time.deltaTime;
                bug.Rotate(0f, 540f * Time.deltaTime, 0f, Space.Self);
                yield return null;
            }

            Destroy(stars.gameObject);
        }

        #endregion

        #region Statue

        void BuildStatue()
        {
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_Rig.transform, "Plinth", new Vector3(0f, 0.03f, 0f), new Vector3(0.12f, 0.06f, 0.12f), new Color(0.45f, 0.45f, 0.42f));
            HciMeetingVfx.Primitive(PrimitiveType.Capsule, m_Rig.transform, "Body", new Vector3(0f, 0.16f, 0f), new Vector3(0.07f, 0.1f, 0.07f), new Color(0.55f, 0.52f, 0.48f));
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Head", new Vector3(0f, 0.28f, 0f), Vector3.one * 0.07f, new Color(0.6f, 0.56f, 0.5f));
        }

        IEnumerator PlayStatue(EditDimension dimension)
        {
            switch (dimension)
            {
                case EditDimension.Appearance:
                    TintChildren(m_Rig.transform, new Color(1f, 0.45f, 0.7f));
                    TintNamed("Body", new Color(0.3f, 0.75f, 1f));
                    HciMeetingVfx.Burst(m_Rig.transform, "Sparkle", new Color(1f, 0.85f, 1f, 0.9f), 20, 0.12f, 0.012f, 0.8f, false, HciMeetingStore.Tex("GlowCircle")).Play();
                    break;
                case EditDimension.Agency:
                    yield return Yawn();
                    break;
                case EditDimension.Rule:
                    HciMeetingVfx.Label(m_Rig.transform, "Lore", "铜兽：我在这儿站了两百年。", new Vector3(0f, 0.42f, 0f), Color.white, 0.008f);
                    break;
                case EditDimension.Deconstruction:
                    for (var i = 0; i < 8; i++)
                    {
                        var a = i / 8f * Mathf.PI * 2f;
                        HciMeetingVfx.Primitive(PrimitiveType.Cylinder, m_Rig.transform, "Lily", new Vector3(Mathf.Cos(a) * 0.22f, 0.01f, Mathf.Sin(a) * 0.22f), new Vector3(0.05f, 0.005f, 0.05f), new Color(0.3f, 0.7f, 1f, 0.55f), true);
                    }

                    HciMeetingVfx.Primitive(PrimitiveType.Cylinder, m_Rig.transform, "WishTree", new Vector3(0.28f, 0.2f, 0.05f), new Vector3(0.03f, 0.2f, 0.03f), new Color(0.35f, 0.22f, 0.08f));
                    HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Canopy", new Vector3(0.28f, 0.4f, 0.05f), Vector3.one * 0.16f, new Color(1f, 0.85f, 0.25f, 0.7f), true);
                    break;
            }
        }

        IEnumerator Yawn()
        {
            var body = m_Rig.transform.Find("Body");
            if (body == null)
                yield break;
            var t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime;
                var k = Mathf.Sin(t * Mathf.PI);
                body.localScale = new Vector3(0.07f, 0.1f + 0.04f * k, 0.07f);
                yield return null;
            }
        }

        #endregion

        #region Bedroom

        void BuildBedroom()
        {
            var wash = HciMeetingVfx.Primitive(PrimitiveType.Quad, m_Rig.transform, "Wash", new Vector3(0f, 0.18f, 0.2f), new Vector3(0.55f, 0.35f, 1f), new Color(0.55f, 0.82f, 0.75f, 0.22f), true);
            wash.SetActive(false);
            m_Portal = HciMeetingVfx.Primitive(PrimitiveType.Quad, m_Rig.transform, "Window", new Vector3(0.22f, 0.18f, 0.18f), new Vector3(0.18f, 0.28f, 1f), new Color(0.05f, 0.08f, 0.25f, 0.85f), true).transform;
            m_Portal.gameObject.SetActive(false);
            m_Jellyfish = new GameObject("Jellyfish").transform;
            m_Jellyfish.SetParent(m_Rig.transform, false);
            for (var i = 0; i < 3; i++)
                HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Jellyfish, "J" + i, Vector3.zero, Vector3.one * 0.05f, new Color(0.7f, 0.4f, 1f, 0.7f), true);
            m_Jellyfish.gameObject.SetActive(false);
        }

        IEnumerator PlayBedroom(EditDimension dimension)
        {
            switch (dimension)
            {
                case EditDimension.Appearance:
                    m_StyleIndex = 1 - m_StyleIndex;
                    var wash = m_Rig.transform.Find("Wash");
                    if (wash != null)
                    {
                        wash.gameObject.SetActive(true);
                        wash.GetComponent<Renderer>().sharedMaterial = HciMeetingVfx.Unlit(
                            m_StyleIndex == 0
                                ? new Color(0.55f, 0.82f, 0.75f, 0.28f)
                                : new Color(0.7f, 0.12f, 0.85f, 0.28f),
                            true);
                    }
                    break;
                case EditDimension.Rule:
                    if (m_Portal != null)
                        m_Portal.gameObject.SetActive(true);
                    break;
                case EditDimension.Agency:
                    if (m_Jellyfish != null)
                        m_Jellyfish.gameObject.SetActive(true);
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
            HciMeetingVfx.Primitive(PrimitiveType.Cylinder, m_Rig.transform, "Pot", new Vector3(0f, 0.03f, 0f), new Vector3(0.07f, 0.03f, 0.07f), new Color(0.55f, 0.25f, 0.15f));
            HciMeetingVfx.Primitive(PrimitiveType.Cylinder, m_Rig.transform, "Stem", new Vector3(0f, 0.1f, 0f), new Vector3(0.015f, 0.07f, 0.015f), new Color(0.2f, 0.55f, 0.2f));
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Leaf", new Vector3(0f, 0.18f, 0f), Vector3.one * 0.08f, new Color(0.25f, 0.75f, 0.3f));
        }

        IEnumerator PlayPlant(EditDimension dimension)
        {
            switch (dimension)
            {
                case EditDimension.Appearance:
                    var leaf = m_Rig.transform.Find("Leaf");
                    if (leaf != null)
                    {
                        var colors = new[]
                        {
                            new Color(0.2f, 0.8f, 0.35f),
                            new Color(0.15f, 0.15f, 0.15f),
                            new Color(0.9f, 0.2f, 0.4f)
                        };
                        m_StyleIndex = (m_StyleIndex + 1) % colors.Length;
                        leaf.GetComponent<Renderer>().sharedMaterial = HciMeetingVfx.Unlit(colors[m_StyleIndex]);
                    }
                    break;
                case EditDimension.Agency:
                    HciMeetingVfx.Label(m_Rig.transform, "Talk", "水分 42%\n今天又长了一点。", new Vector3(0f, 0.32f, 0f), new Color(0.9f, 1f, 0.8f), 0.008f);
                    break;
                case EditDimension.Rule:
                    m_PlantAge = m_PlantAge == 0 ? 2 : 0;
                    yield return GrowPlant(m_PlantAge == 0);
                    break;
                case EditDimension.Deconstruction:
                    RebuildRig();
                    break;
            }

            yield break;
        }

        #endregion

        #region Pet

        void BuildPet()
        {
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Body", new Vector3(0f, 0.07f, 0f), new Vector3(0.12f, 0.08f, 0.09f), new Color(0.85f, 0.7f, 0.45f));
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Head", new Vector3(0.05f, 0.13f, 0.02f), Vector3.one * 0.07f, new Color(0.9f, 0.75f, 0.5f));
            m_DreamBubble = HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Dream", new Vector3(0.08f, 0.26f, 0f), Vector3.one * 0.12f, new Color(0.55f, 0.85f, 1f, 0.4f), true).transform;
            HciMeetingVfx.Primitive(PrimitiveType.Cube, m_DreamBubble, "Fish", new Vector3(0f, 0f, 0f), new Vector3(0.35f, 0.12f, 0.2f), new Color(1f, 0.55f, 0.15f));
            m_DreamBubble.gameObject.SetActive(false);
        }

        IEnumerator PlayPet(EditDimension dimension)
        {
            switch (dimension)
            {
                case EditDimension.Appearance:
                    TintNamed("Body", new Color(1f, 1f, 1f));
                    TintNamed("Head", new Color(0.05f, 0.05f, 0.05f));
                    break;
                case EditDimension.Agency:
                    if (m_DreamBubble != null)
                        m_DreamBubble.gameObject.SetActive(true);
                    break;
                case EditDimension.Rule:
                    if (m_DreamBubble != null)
                    {
                        HciMeetingVfx.Burst(m_DreamBubble, "Pop", Color.white, 16, 0.2f, 0.01f, 0.5f, false).Play();
                        m_DreamBubble.gameObject.SetActive(false);
                    }

                    yield return PunchScale(1.15f, 0.2f);
                    break;
                case EditDimension.Deconstruction:
                    HciMeetingVfx.Burst(m_Rig.transform, "Hearts", new Color(1f, 0.3f, 0.5f), 24, 0.2f, 0.018f, 1f, false, HciMeetingStore.Tex("GlowCircle")).Play();
                    HciMeetingVfx.Label(m_Rig.transform, "Affinity", "好感度 +1", new Vector3(0f, 0.32f, 0f), new Color(1f, 0.4f, 0.6f));
                    if (m_DreamBubble != null)
                        m_DreamBubble.gameObject.SetActive(false);
                    TintNamed("Body", new Color(0.85f, 0.7f, 0.45f));
                    TintNamed("Head", new Color(0.9f, 0.75f, 0.5f));
                    break;
            }
        }

        #endregion

        #region Season tree

        void BuildSeasonTree()
        {
            HciMeetingVfx.Primitive(PrimitiveType.Cylinder, m_Rig.transform, "Trunk", new Vector3(0f, 0.12f, 0f), new Vector3(0.04f, 0.12f, 0.04f), new Color(0.4f, 0.25f, 0.1f));
            HciMeetingVfx.Primitive(PrimitiveType.Sphere, m_Rig.transform, "Crown", new Vector3(0f, 0.28f, 0f), Vector3.one * 0.18f, new Color(0.25f, 0.7f, 0.28f));
            m_Falling = HciMeetingVfx.Burst(m_Rig.transform, "Fall", new Color(1f, 0.4f, 0.55f), 14, 0.08f, 0.016f, 1.4f, true, HciMeetingStore.Tex("SmokeNoise"));
            m_Falling.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        IEnumerator PlaySeasonTree(EditDimension dimension)
        {
            var crown = m_Rig.transform.Find("Crown");
            switch (dimension)
            {
                case EditDimension.Appearance:
                    if (crown != null)
                        crown.GetComponent<Renderer>().sharedMaterial = HciMeetingVfx.Unlit(new Color(0.95f, 0.45f, 0.7f));
                    break;
                case EditDimension.Agency:
                    HciMeetingVfx.Label(m_Rig.transform, "Face", "今天想开花。", new Vector3(0f, 0.46f, 0f), Color.white, 0.008f);
                    break;
                case EditDimension.Rule:
                    m_SeasonIndex = (m_SeasonIndex + 1) % 4;
                    var seasons = new[]
                    {
                        new Color(1f, 0.55f, 0.75f),
                        new Color(0.2f, 0.75f, 0.3f),
                        new Color(0.9f, 0.45f, 0.1f),
                        new Color(0.85f, 0.9f, 1f)
                    };
                    if (crown != null)
                        crown.GetComponent<Renderer>().sharedMaterial = HciMeetingVfx.Unlit(seasons[m_SeasonIndex]);
                    if (m_SeasonIndex == 3 && crown != null)
                        crown.localScale = Vector3.one * 0.08f;
                    else if (crown != null)
                        crown.localScale = Vector3.one * 0.18f;
                    break;
                case EditDimension.Deconstruction:
                    m_Falling?.Play();
                    break;
            }

            yield break;
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

        IEnumerator PunchScale(float peak, float duration)
        {
            var start = m_Anchor.localScale;
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
                    m_Rig.transform.localScale = Vector3.one * s;
                yield return null;
            }

            if (m_Scenario == HciMeetingScenario.LighterDemon)
                m_Anchor.localScale = m_AnchorHiddenScale;
            else
                m_Rig.transform.localScale = Vector3.one;
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
            var t = m_Rig.transform.Find(name);
            if (t == null)
                return;
            var r = t.GetComponent<Renderer>();
            if (r != null)
                r.sharedMaterial = HciMeetingVfx.Unlit(color);
        }
    }
}
