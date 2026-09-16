using System.Collections;
using UnityEngine;

namespace MagicMR
{
    public enum EditDimension
    {
        None = 0,
        Appearance = 1,
        Agency = 2,
        Rule = 4,
        Deconstruction = 8
    }

    [System.Flags]
    public enum EnabledDimensions
    {
        None = 0,
        Appearance = EditDimension.Appearance,
        Agency = EditDimension.Agency,
        Rule = EditDimension.Rule,
        Deconstruction = EditDimension.Deconstruction,
        All = Appearance | Agency | Rule | Deconstruction
    }

    /// <summary>
    /// Four-dimensional reality editing controller for MR study targets.
    /// Appearance (Pinch), Agency (Circle), Rule (Swipe), Deconstruction (Fist→Open).
    /// </summary>
    public class RealityEditor : MonoBehaviour
    {
        [Header("Appearance")]
        [SerializeField]
        Renderer m_TargetRenderer;

        [SerializeField]
        Material m_BurntMaterial;

        [SerializeField]
        AudioSource m_AudioSource;

        [SerializeField]
        AudioClip m_BurnSfx;

        [Header("Agency")]
        [SerializeField]
        Animator m_AgencyAnimator;

        [SerializeField]
        string m_AliveBoolParameter = StudySpec.AliveBoolParameter;

        [SerializeField]
        GameObject m_EyesObject;

        [SerializeField]
        float m_IdlePulseSpeed = StudySpec.IdlePulseSpeed;

        [SerializeField]
        float m_IdlePulseScale = StudySpec.IdlePulseScale;

        [Header("Rule")]
        [SerializeField]
        float m_EvadeDistance = StudySpec.EvadeDistance;

        [SerializeField]
        float m_EvadeImpulse = StudySpec.EvadeImpulse;

        [SerializeField]
        bool m_ConstrainEvadeToXZ = StudySpec.ConstrainEvadeToXZ;

        [SerializeField]
        float m_GhostTrailLifetime = StudySpec.GhostTrailLifetime;

        [Header("Deconstruction")]
        [SerializeField]
        ParticleSystem m_DeconstructionVfx;

        [SerializeField]
        GameObject m_FlowerPrefab;

        [SerializeField]
        float m_DeconstructionDelay = StudySpec.DeconstructionDelay;

        Material m_OriginalMaterial;
        Material[] m_OriginalChildMaterials;
        Renderer[] m_AllRenderers;
        Vector3 m_BaseScale;
        Vector3 m_InitialPosition;
        Rigidbody m_Rigidbody;
        LighterAnchorManager m_Anchor;
        EditDimension m_ActiveDimension = EditDimension.None;
        float m_StateEnterTime;
        int m_EvadeCount;
        int m_EvasionFailureCount;
        Coroutine m_DeconstructionCoroutine;
        Coroutine m_AppearanceBlendCoroutine;
        GameObject m_SpawnedFlower;
        Vector3 m_FlowerBaseScale = Vector3.one;
        float m_FlowerAge;
        bool m_FlowerGrowing;
        bool m_IsDeconstructed;
        bool m_AppearanceBurnt;
        ParticleSystem m_SmokeParticles;
        float m_LastGrowlTime;
        float m_LastEvasionFailureLogTime;
        float m_LastGhostTrailTime;
        float m_PulsePhase;
        float m_AgencyAge;
        float m_EyesFade;
        Vector3 m_HopStart;
        Vector3 m_HopEnd;
        float m_HopElapsed;
        float m_HopDuration;
        bool m_Hopping;

        const float k_EvadeLinearDamping = 10f;
        const float k_EvadeLeashMultiplier = 2f;
        const float k_EvadeCooldown = 0.35f;
        const float k_EvasionFailureDistance = 0.10f;
        const float k_EvasionFailureMinFlee = 0.12f;
        float m_LastEvadeTime;

#if XR_HANDS_1_1_OR_NEWER
        readonly System.Collections.Generic.List<UnityEngine.XR.Hands.XRHandSubsystem> m_HandSubsystems =
            new System.Collections.Generic.List<UnityEngine.XR.Hands.XRHandSubsystem>(2);
#endif

        public EditDimension ActiveDimension => m_ActiveDimension;
        public bool IsDeconstructed => m_IsDeconstructed;
        public float CurrentStateDuration => m_ActiveDimension == EditDimension.None ? 0f : Time.time - m_StateEnterTime;
        public float CurrentVelocity =>
            m_Rigidbody != null ? m_Rigidbody.linearVelocity.magnitude : 0f;

        public int EvadeCount => m_EvadeCount;
        public int EvasionFailureCount => m_EvasionFailureCount;

        void Awake()
        {
            CacheRenderers();

            if (m_TargetRenderer != null)
                m_OriginalMaterial = m_TargetRenderer.sharedMaterial;

            m_BaseScale = transform.localScale;
            m_InitialPosition = transform.position;
            m_Anchor = GetComponent<LighterAnchorManager>();

            m_Rigidbody = GetComponent<Rigidbody>();
            if (m_Rigidbody == null)
            {
                m_Rigidbody = gameObject.AddComponent<Rigidbody>();
                m_Rigidbody.useGravity = false;
                m_Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            }

            m_Rigidbody.linearDamping = k_EvadeLinearDamping;

            EnsureAudioSource();
            EnsureAgencyEyes();
            m_IdlePulseSpeed = StudySpec.IdlePulseSpeed;
            m_IdlePulseScale = StudySpec.IdlePulseScale;
            m_EvadeDistance = StudySpec.EvadeDistance;
            m_EvadeImpulse = m_EvadeImpulse > 0f ? m_EvadeImpulse : StudySpec.EvadeImpulse;
            m_DeconstructionDelay = StudySpec.DeconstructionDelay;
            m_GhostTrailLifetime = StudySpec.GhostTrailLifetime;
            if (m_BurnSfx == null)
                m_BurnSfx = Resources.Load<AudioClip>("MagicMR/BurnSizzle");

            if (m_EyesObject != null)
                m_EyesObject.SetActive(false);
        }

        void EnsureAudioSource()
        {
            if (m_AudioSource == null)
                m_AudioSource = GetComponent<AudioSource>();
            if (m_AudioSource == null)
                m_AudioSource = gameObject.AddComponent<AudioSource>();
            m_AudioSource.playOnAwake = false;
            m_AudioSource.loop = false;
            m_AudioSource.spatialBlend = 0.6f;
            if (m_AudioSource.isPlaying)
                m_AudioSource.Stop();
        }

        void EnsureAgencyEyes()
        {
            if (m_EyesObject == null)
            {
                var existing = transform.Find("AgencyEyes");
                if (existing != null)
                    m_EyesObject = existing.gameObject;
            }

            if (m_EyesObject != null)
                return;

            m_EyesObject = new GameObject("AgencyEyes");
            m_EyesObject.transform.SetParent(transform, false);
            m_EyesObject.transform.localPosition = new Vector3(0f, 0.55f, 0.35f);

            CreateEyeQuad(m_EyesObject.transform, "Eye_L", new Vector3(-0.1f, 0f, 0f));
            CreateEyeQuad(m_EyesObject.transform, "Eye_R", new Vector3(0.1f, 0f, 0f));
            m_EyesObject.SetActive(false);
        }

        static void CreateEyeQuad(Transform parent, string name, Vector3 localPos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(0.1f, 0.075f, 1f);
            Destroy(go.GetComponent<Collider>());

            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            var color = new Color(1f, 0.12f, 0.05f, 1f);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        void CacheRenderers()
        {
            if (m_TargetRenderer == null)
                m_TargetRenderer = GetComponentInChildren<Renderer>();

            m_AllRenderers = GetComponentsInChildren<Renderer>(true);
            m_OriginalChildMaterials = new Material[m_AllRenderers.Length];
            for (var i = 0; i < m_AllRenderers.Length; i++)
                m_OriginalChildMaterials[i] = m_AllRenderers[i] != null ? m_AllRenderers[i].sharedMaterial : null;
        }

        void Update()
        {
            if (m_ActiveDimension == EditDimension.Agency && !m_IsDeconstructed)
            {
                ApplyIdlePulse();
                UpdateAgencyEyes();
                MaybePlayGrowl();
            }

            if (m_ActiveDimension == EditDimension.Rule)
            {
                UpdateRuleEvade();
                UpdateEvasionFailures();
            }

            if (m_SpawnedFlower != null)
                UpdateFlowerMotion();
        }

        void MaybePlayGrowl()
        {
            if (m_AudioSource == null || Time.time - m_LastGrowlTime < 2.2f)
                return;

            // Growl near pulse peaks for "alive" feel.
            var phase = Mathf.Sin(m_PulsePhase);
            if (phase < 0.85f)
                return;

            m_LastGrowlTime = Time.time;
            m_AudioSource.PlayOneShot(MagicMRAudioFactory.Growl, 0.28f);
        }

        void UpdateFlowerMotion()
        {
            if (m_FlowerGrowing)
            {
                m_FlowerAge += Time.deltaTime;
                var t = MagicMRAnim.EaseOutBack(m_FlowerAge / StudySpec.FlowerGrowSeconds);
                m_SpawnedFlower.transform.localScale = m_FlowerBaseScale * t;
                if (m_FlowerAge >= StudySpec.FlowerGrowSeconds)
                {
                    m_SpawnedFlower.transform.localScale = m_FlowerBaseScale;
                    m_FlowerGrowing = false;
                    m_FlowerAge = 0f;
                }
            }

            if (!m_FlowerGrowing)
                m_FlowerAge += Time.deltaTime;
            var bob = m_FlowerGrowing ? 0f : Mathf.Sin(m_FlowerAge * 1.35f) * 0.012f;

            m_SpawnedFlower.transform.position = m_InitialPosition + Vector3.up * (0.02f + bob);
            m_SpawnedFlower.transform.Rotate(0f, 14f * Time.deltaTime, 0f, Space.World);
        }

        void FixedUpdate()
        {
            StepHop();
            ClampToLeash();
        }

        void StepHop()
        {
            if (!m_Hopping)
                return;

            if (m_Anchor != null && m_Anchor.IsFollowingHand)
            {
                CancelHop();
                return;
            }

            m_HopElapsed += Time.fixedDeltaTime;
            var t = MagicMRAnim.EaseOutCubic(m_HopElapsed / Mathf.Max(0.01f, m_HopDuration));
            var pos = Vector3.LerpUnclamped(m_HopStart, m_HopEnd, t);
            if (m_Rigidbody != null)
            {
                m_Rigidbody.MovePosition(pos);
                m_Rigidbody.linearVelocity = Vector3.zero;
            }
            else
            {
                transform.position = pos;
            }

            if (m_HopElapsed >= m_HopDuration)
            {
                if (m_Rigidbody != null)
                {
                    m_Rigidbody.position = m_HopEnd;
                    m_Rigidbody.linearVelocity = Vector3.zero;
                }
                else
                {
                    transform.position = m_HopEnd;
                }

                m_Hopping = false;
            }
        }

        void BeginHop(Vector3 target, float duration)
        {
            m_HopStart = transform.position;
            m_HopEnd = target;
            m_HopElapsed = 0f;
            m_HopDuration = Mathf.Max(0.05f, duration);
            m_Hopping = true;
            if (m_Rigidbody != null)
                m_Rigidbody.linearVelocity = Vector3.zero;
        }

        void CancelHop()
        {
            m_Hopping = false;
        }

        void ClampToLeash()
        {
            if (m_Rigidbody == null || m_IsDeconstructed || m_Hopping)
                return;

            if (m_Anchor != null && m_Anchor.IsFollowingHand)
                return;

            var offset = transform.position - m_InitialPosition;
            var maxDistance = Mathf.Max(m_EvadeDistance, 0.05f) * k_EvadeLeashMultiplier;
            if (offset.magnitude <= maxDistance)
                return;

            var clamped = m_InitialPosition + offset.normalized * maxDistance;
            m_Rigidbody.position = clamped;
            m_Rigidbody.linearVelocity *= 0.1f;
        }

        public void ApplyDimension(EditDimension dimension, Vector3 handPosition, bool hasHandPosition)
        {
            // Phase 2 gesture-pipeline validation: confirms gate did not block this call.
            Debug.Log($"[Phase2] RealityEditor.ApplyDimension: dimension={dimension}", this);

            // Coming back from flower: destroy flower and restore lighter before
            // applying a new dimension (except another deconstruction).
            if (m_IsDeconstructed && dimension != EditDimension.Deconstruction)
                RestoreFromDeconstruction();

            switch (dimension)
            {
                case EditDimension.Appearance:
                    TriggerAppearance();
                    break;
                case EditDimension.Agency:
                    TriggerAgency();
                    break;
                case EditDimension.Rule:
                    TriggerRule();
                    break;
                case EditDimension.Deconstruction:
                    TriggerDeconstruction();
                    break;
            }

            if (hasHandPosition && DataLogger.Instance != null)
            {
                DataLogger.Instance.LogEvent(
                    "dimension_applied",
                    dimension,
                    Vector3.Distance(handPosition, transform.position),
                    CurrentVelocity,
                    CurrentStateDuration);
            }
        }

        public float GetHandDistance(Vector3 handPosition, bool hasHandPosition)
        {
            return hasHandPosition ? Vector3.Distance(handPosition, transform.position) : -1f;
        }

        public void SyncWorldAnchor(Vector3 worldPosition)
        {
            m_InitialPosition = worldPosition;
            if (m_SpawnedFlower != null && !m_FlowerGrowing)
                m_SpawnedFlower.transform.position = worldPosition + Vector3.up * 0.02f;
        }

        /// <summary>
        /// Re-show the lighter only if it was not intentionally deconstructed.
        /// Calibration must not resurrect a flower+lighter double state.
        /// </summary>
        public void EnsureVisible()
        {
            if (m_IsDeconstructed)
            {
                Debug.Log("[MagicMR] EnsureVisible skipped (deconstructed / flower active).", this);
                return;
            }

            SetAllRenderersEnabled(true);
            gameObject.SetActive(true);
        }

        public void TriggerAppearance()
        {
            EnterState(EditDimension.Appearance);
            SetAllRenderersEnabled(true);

            if (m_AppearanceBlendCoroutine != null)
                StopCoroutine(m_AppearanceBlendCoroutine);
            m_AppearanceBlendCoroutine = StartCoroutine(BlendToBurntRoutine());

            PlayAppearanceVfx();

            if (m_AudioSource != null)
            {
                if (m_BurnSfx != null)
                    m_AudioSource.PlayOneShot(m_BurnSfx);
                else
                    m_AudioSource.PlayOneShot(MagicMRAudioFactory.Whoosh, 0.35f);
            }
        }

        IEnumerator BlendToBurntRoutine()
        {
            if (m_BurntMaterial == null)
            {
                Debug.LogWarning("[MagicMR] Appearance: burnt material missing.", this);
                yield break;
            }

            if (m_AllRenderers == null || m_AllRenderers.Length == 0)
                CacheRenderers();

            if (m_AppearanceBurnt)
            {
                ApplyBurntShared();
                yield break;
            }

            var count = m_AllRenderers.Length;
            var startMats = new Material[count];
            var blendMats = new Material[count];
            for (var i = 0; i < count; i++)
            {
                var renderer = m_AllRenderers[i];
                if (!IsBodyRenderer(renderer))
                    continue;

                startMats[i] = renderer.sharedMaterial != null ? renderer.sharedMaterial : m_OriginalMaterial;
                if (startMats[i] == null)
                    startMats[i] = m_BurntMaterial;
                blendMats[i] = new Material(startMats[i]);
                renderer.sharedMaterial = blendMats[i];
            }

            var elapsed = 0f;
            var duration = StudySpec.AppearanceBlendSeconds;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = MagicMRAnim.SmoothStep(elapsed / duration);
                for (var i = 0; i < count; i++)
                {
                    if (blendMats[i] != null)
                        blendMats[i].Lerp(startMats[i], m_BurntMaterial, t);
                }

                yield return null;
            }

            ApplyBurntShared();
            for (var i = 0; i < count; i++)
            {
                if (blendMats[i] != null)
                    Destroy(blendMats[i]);
            }

            m_AppearanceBurnt = true;
            m_AppearanceBlendCoroutine = null;
            Debug.Log("[MagicMR] Appearance: burnt material applied.", this);
        }

        void ApplyBurntShared()
        {
            if (m_AllRenderers == null)
                return;

            foreach (var renderer in m_AllRenderers)
            {
                if (IsBodyRenderer(renderer))
                    renderer.sharedMaterial = m_BurntMaterial;
            }
        }

        static bool IsBodyRenderer(Renderer renderer)
        {
            if (renderer == null || renderer is ParticleSystemRenderer)
                return false;

            var t = renderer.transform;
            while (t != null)
            {
                var n = t.name;
                if (n == "AgencyEyes" || n.StartsWith("Appearance_smoke") || n.StartsWith("flower_") ||
                    n.StartsWith("deconstruction_") || n.StartsWith("evade_ghost"))
                    return false;
                t = t.parent;
            }

            return true;
        }

        void PlayAppearanceVfx()
        {
            if (m_SmokeParticles == null)
                m_SmokeParticles = MagicMRVfxFactory.CreateSmoke(transform);

            MagicMRVfxFactory.EmitSmokeBurst(m_SmokeParticles);
        }

        public void TriggerAgency()
        {
            EnterState(EditDimension.Agency);
            SetAllRenderersEnabled(true);
            EnsureAgencyEyes();

            m_PulsePhase = 0f;
            m_AgencyAge = 0f;
            m_EyesFade = 0f;

            if (m_EyesObject != null)
            {
                m_EyesObject.transform.localScale = Vector3.zero;
                m_EyesObject.SetActive(true);
            }

            if (m_AgencyAnimator != null && !string.IsNullOrEmpty(m_AliveBoolParameter))
                m_AgencyAnimator.SetBool(m_AliveBoolParameter, true);

            if (m_AudioSource != null)
                m_AudioSource.PlayOneShot(MagicMRAudioFactory.Growl, 0.4f);

            Debug.Log("[MagicMR] Agency: alive / pulse + eyes.", this);
        }

        public void TriggerRule()
        {
            EnterState(EditDimension.Rule);
            SetAllRenderersEnabled(true);

            // One-shot lateral dodge (20cm) + ghost trail for virtual/physical mismatch.
            ApplyRuleDodge();
            Debug.Log("[MagicMR] Rule: 20cm lateral dodge + ghost trail.", this);
            Debug.Log("[Phase2] Rule effect executed (lateral dodge).", this);
        }

        void ApplyRuleDodge()
        {
            var cam = Camera.main;
            var right = cam != null ? cam.transform.right : Vector3.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.right;
            right.Normalize();

            // Alternate side each Rule trigger for clearer mismatch.
            var side = (m_EvadeCount % 2 == 0) ? 1f : -1f;
            var target = transform.position + right * (StudySpec.RuleDodgeMeters * side);
            target.y = transform.position.y;

            SpawnEvadeGhostTrail();
            BeginHop(target, StudySpec.RuleDodgeDuration);

            m_EvadeCount++;
            m_LastEvadeTime = Time.time;

            if (m_AudioSource != null)
                m_AudioSource.PlayOneShot(MagicMRAudioFactory.Whoosh, 0.55f);
        }

        public void TriggerDeconstruction()
        {
            EnterState(EditDimension.Deconstruction);

            if (m_DeconstructionCoroutine != null)
                StopCoroutine(m_DeconstructionCoroutine);
            m_DeconstructionCoroutine = StartCoroutine(DeconstructionRoutine());
        }

        IEnumerator DeconstructionRoutine()
        {
            if (m_AudioSource != null)
                m_AudioSource.PlayOneShot(MagicMRAudioFactory.Shatter, 0.85f);

            if (m_DeconstructionVfx != null)
            {
                var vfx = Instantiate(m_DeconstructionVfx, transform.position, transform.rotation);
                Destroy(vfx.gameObject, m_DeconstructionDelay + 1f);
            }

            MagicMRVfxFactory.CreateShatterBurst(transform);
            if (m_SmokeParticles != null)
                m_SmokeParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            var startScale = transform.localScale;
            var shrinkDur = Mathf.Min(Mathf.Max(0.08f, m_DeconstructionDelay), StudySpec.ShatterShrinkSeconds);
            var elapsed = 0f;
            while (elapsed < shrinkDur)
            {
                elapsed += Time.deltaTime;
                var t = MagicMRAnim.EaseInCubic(elapsed / shrinkDur);
                transform.localScale = Vector3.Lerp(startScale, m_BaseScale * 0.04f, t);
                yield return null;
            }

            // Hide body + cap + wheel so only the flower remains.
            SetAllRenderersEnabled(false);
            if (m_EyesObject != null)
                m_EyesObject.SetActive(false);

            transform.localScale = m_BaseScale;
            m_IsDeconstructed = true;
            CancelHop();

            var remain = m_DeconstructionDelay - shrinkDur;
            if (remain > 0f)
                yield return new WaitForSeconds(remain);

            SpawnFlower();
            m_DeconstructionCoroutine = null;
            Debug.Log("[MagicMR] Deconstruction: flower spawned, lighter hidden.", this);
        }

        void SpawnFlower()
        {
            if (m_FlowerPrefab == null)
            {
                Debug.LogWarning("[MagicMR] Flower prefab missing.", this);
                return;
            }

            if (m_SpawnedFlower != null)
                Destroy(m_SpawnedFlower);

            m_SpawnedFlower = Instantiate(m_FlowerPrefab, transform.position, transform.rotation);
            m_FlowerBaseScale = m_SpawnedFlower.transform.localScale;
            if (m_FlowerBaseScale.sqrMagnitude < 1e-6f)
                m_FlowerBaseScale = Vector3.one;
            m_SpawnedFlower.transform.localScale = Vector3.zero;
            m_FlowerAge = 0f;
            m_FlowerGrowing = true;
            MagicMRVfxFactory.CreateGoldDust(m_SpawnedFlower.transform);
        }

        void ClearFlower()
        {
            if (m_SpawnedFlower == null)
                return;

            Destroy(m_SpawnedFlower);
            m_SpawnedFlower = null;
            m_FlowerGrowing = false;
            m_FlowerAge = 0f;
        }

        void RestoreFromDeconstruction()
        {
            if (m_DeconstructionCoroutine != null)
            {
                StopCoroutine(m_DeconstructionCoroutine);
                m_DeconstructionCoroutine = null;
            }

            ClearFlower();
            m_IsDeconstructed = false;
            SetAllRenderersEnabled(true);
            RestoreOriginalMaterials();
            transform.localScale = m_BaseScale;
            Debug.Log("[MagicMR] Restored lighter from deconstruction (flower cleared).", this);
        }

        void SetAllRenderersEnabled(bool enabled)
        {
            if (m_AllRenderers == null || m_AllRenderers.Length == 0)
                CacheRenderers();

            foreach (var renderer in m_AllRenderers)
            {
                if (renderer != null && IsBodyRenderer(renderer))
                    renderer.enabled = enabled;
            }
        }

        void RestoreOriginalMaterials()
        {
            if (m_AllRenderers == null)
                return;

            for (var i = 0; i < m_AllRenderers.Length; i++)
            {
                if (!IsBodyRenderer(m_AllRenderers[i]))
                    continue;

                if (m_OriginalChildMaterials != null &&
                    i < m_OriginalChildMaterials.Length &&
                    m_OriginalChildMaterials[i] != null)
                {
                    m_AllRenderers[i].sharedMaterial = m_OriginalChildMaterials[i];
                }
                else if (m_OriginalMaterial != null)
                {
                    m_AllRenderers[i].sharedMaterial = m_OriginalMaterial;
                }
            }

            m_AppearanceBurnt = false;
        }

        void EnterState(EditDimension dimension)
        {
            if (m_ActiveDimension == dimension)
                return;

            if (m_ActiveDimension != EditDimension.None && DataLogger.Instance != null)
            {
                DataLogger.Instance.LogEvent(
                    "state_exit",
                    m_ActiveDimension,
                    -1f,
                    CurrentVelocity,
                    CurrentStateDuration);
            }

            // Leaving Agency: stop eyes.
            if (m_ActiveDimension == EditDimension.Agency && dimension != EditDimension.Agency)
            {
                if (m_EyesObject != null)
                {
                    m_EyesObject.SetActive(false);
                    m_EyesObject.transform.localScale = Vector3.one;
                }

                if (m_AgencyAnimator != null && !string.IsNullOrEmpty(m_AliveBoolParameter))
                    m_AgencyAnimator.SetBool(m_AliveBoolParameter, false);
                transform.localScale = m_BaseScale;
                m_EyesFade = 0f;
            }

            m_ActiveDimension = dimension;
            m_StateEnterTime = Time.time;

            if (DataLogger.Instance != null)
            {
                DataLogger.Instance.LogEvent(
                    "state_enter",
                    dimension,
                    -1f,
                    CurrentVelocity,
                    0f);
            }
        }

        void ApplyIdlePulse()
        {
            m_AgencyAge += Time.deltaTime;
            m_PulsePhase += Time.deltaTime * m_IdlePulseSpeed;
            var fade = MagicMRAnim.SmoothStep(m_AgencyAge / StudySpec.AgencyPulseFadeInSeconds);
            var pulse = 1f + Mathf.Sin(m_PulsePhase) * m_IdlePulseScale * fade;
            transform.localScale = m_BaseScale * pulse;
        }

        void UpdateAgencyEyes()
        {
            if (m_EyesObject == null || !m_EyesObject.activeSelf)
                return;

            m_EyesFade = Mathf.MoveTowards(m_EyesFade, 1f, Time.deltaTime / StudySpec.AgencyEyesFadeInSeconds);
            m_EyesObject.transform.localScale = Vector3.one * MagicMRAnim.SmoothStep(m_EyesFade);

            var cam = Camera.main;
            if (cam == null)
                return;

            var toCam = cam.transform.position - m_EyesObject.transform.position;
            if (toCam.sqrMagnitude > 0.0001f)
                m_EyesObject.transform.rotation = Quaternion.LookRotation(toCam, Vector3.up);
        }

        void UpdateRuleEvade()
        {
            if (m_IsDeconstructed || m_Rigidbody == null || m_Hopping)
                return;

            // While pseudo-dynamic hand attachment is active, do not fight the
            // hand pose with evade impulses (hybrid tracking Mode B).
            if (m_Anchor != null && m_Anchor.IsFollowingHand)
                return;

            if (Time.time - m_LastEvadeTime < k_EvadeCooldown)
                return;

            if (!TryGetNearestHand(out var handPos, out var distance))
                return;

            if (distance >= m_EvadeDistance)
                return;

            var evadeDir = (transform.position - handPos).normalized;
            if (m_ConstrainEvadeToXZ)
                evadeDir.y = 0f;

            if (evadeDir.sqrMagnitude < 0.0001f)
                evadeDir = Vector3.forward;
            evadeDir.Normalize();

            var target = transform.position + evadeDir * StudySpec.RuleDodgeMeters;
            target.y = transform.position.y;
            SpawnEvadeGhostTrail();
            BeginHop(target, StudySpec.RuleDodgeDuration);

            m_EvadeCount++;
            m_LastEvadeTime = Time.time;

            if (m_AudioSource != null)
                m_AudioSource.PlayOneShot(MagicMRAudioFactory.Whoosh, 0.55f);

            if (DataLogger.Instance != null)
            {
                DataLogger.Instance.LogEvent(
                    "evade_triggered",
                    EditDimension.Rule,
                    distance,
                    CurrentVelocity,
                    CurrentStateDuration,
                    handPos,
                    $"count={m_EvadeCount}");
            }
        }

        void SpawnEvadeGhostTrail()
        {
            if (Time.time - m_LastGhostTrailTime < 0.2f)
                return;

            m_LastGhostTrailTime = Time.time;
            MagicMRVfxFactory.CreateGhostTrailClone(transform, m_GhostTrailLifetime);
        }

        void UpdateEvasionFailures()
        {
            if (m_IsDeconstructed)
                return;

            var pin = m_Anchor != null && m_Anchor.IsCalibrated ? m_Anchor.PinnedPosition : m_InitialPosition;
            var fled = Vector3.Distance(transform.position, pin) >= k_EvasionFailureMinFlee;
            if (!fled)
                return;

            if (!TryGetNearestHand(out var handPos, out var distanceToVirtual))
                return;

            var distanceToPin = Vector3.Distance(handPos, pin);
            // Hand reaches the registration / habit point while the virtual target has fled.
            if (distanceToPin >= k_EvasionFailureDistance)
                return;

            if (Time.time - m_LastEvasionFailureLogTime < 0.6f)
                return;

            m_LastEvasionFailureLogTime = Time.time;
            m_EvasionFailureCount++;

            DataLogger.Instance?.LogEvent(
                "evasion_failure",
                EditDimension.Rule,
                distanceToPin,
                CurrentVelocity,
                CurrentStateDuration,
                handPos,
                $"failures={m_EvasionFailureCount};virtual_d={distanceToVirtual:F3}");
        }

        bool TryGetNearestHand(out Vector3 handPos, out float distance)
        {
            handPos = default;
            distance = float.MaxValue;

#if XR_HANDS_1_1_OR_NEWER
            m_HandSubsystems.Clear();
            SubsystemManager.GetSubsystems(m_HandSubsystems);
            if (m_HandSubsystems.Count == 0)
                return false;

            var subsystem = m_HandSubsystems[0];
            var found = false;

            if (TryHandPalm(subsystem.leftHand, out var leftPos))
            {
                var d = Vector3.Distance(transform.position, leftPos);
                if (d < distance)
                {
                    distance = d;
                    handPos = leftPos;
                    found = true;
                }
            }

            if (TryHandPalm(subsystem.rightHand, out var rightPos))
            {
                var d = Vector3.Distance(transform.position, rightPos);
                if (d < distance)
                {
                    distance = d;
                    handPos = rightPos;
                    found = true;
                }
            }

            return found;
#else
            return false;
#endif
        }

#if XR_HANDS_1_1_OR_NEWER
        static bool TryHandPalm(UnityEngine.XR.Hands.XRHand hand, out Vector3 position)
        {
            position = default;
            if (!hand.isTracked)
                return false;
            if (!hand.GetJoint(UnityEngine.XR.Hands.XRHandJointID.Palm).TryGetPose(out var pose))
                return false;
            position = pose.position;
            return true;
        }
#endif

        public void ResetTarget()
        {
            StopAllCoroutines();
            m_DeconstructionCoroutine = null;
            m_AppearanceBlendCoroutine = null;
            CancelHop();
            ClearFlower();
            m_IsDeconstructed = false;

            SetAllRenderersEnabled(true);
            RestoreOriginalMaterials();

            if (m_EyesObject != null)
            {
                m_EyesObject.SetActive(false);
                m_EyesObject.transform.localScale = Vector3.one;
            }

            if (m_AgencyAnimator != null && !string.IsNullOrEmpty(m_AliveBoolParameter))
                m_AgencyAnimator.SetBool(m_AliveBoolParameter, false);

            transform.localScale = m_BaseScale;
            transform.position = m_InitialPosition;

            if (m_Rigidbody != null)
            {
                m_Rigidbody.linearVelocity = Vector3.zero;
                m_Rigidbody.angularVelocity = Vector3.zero;
            }

            if (m_SmokeParticles != null)
                m_SmokeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            m_EvadeCount = 0;
            m_EvasionFailureCount = 0;
            m_ActiveDimension = EditDimension.None;
            m_StateEnterTime = Time.time;
            m_PulsePhase = 0f;
            m_AgencyAge = 0f;
            m_EyesFade = 0f;
            m_AppearanceBurnt = false;
        }

        public void OnGestureA_Pinch() => ApplyDimension(EditDimension.Appearance, Vector3.zero, false);
        public void OnGestureB_Swipe() => ApplyDimension(EditDimension.Rule, Vector3.zero, false);
        public void OnGestureC_Circle() => ApplyDimension(EditDimension.Agency, Vector3.zero, false);
        public void OnGestureD_Snap() => ApplyDimension(EditDimension.Deconstruction, Vector3.zero, false);
        public void OnGestureD_FistBurst() => ApplyDimension(EditDimension.Deconstruction, Vector3.zero, false);
    }
}
