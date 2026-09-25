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
        Deconstruction = 8,
        Scale = 16
    }

    [System.Flags]
    public enum EnabledDimensions
    {
        None = 0,
        Appearance = EditDimension.Appearance,
        Agency = EditDimension.Agency,
        Rule = EditDimension.Rule,
        Deconstruction = EditDimension.Deconstruction,
        Scale = EditDimension.Scale,
        All = Appearance | Agency | Rule | Deconstruction | Scale
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
        float m_GhostTrailLifetime = 0.5f;

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
        float m_CurrentScaleMultiplier = 1f;
        LighterInteriorView m_InteriorView;
        public bool InteriorVisible => m_InteriorView != null && m_InteriorView.Visible;
        Vector3 m_InitialPosition;
        Coroutine m_DodgeReturn;
        Rigidbody m_Rigidbody;
        EditDimension m_ActiveDimension = EditDimension.None;
        float m_StateEnterTime;
        int m_EvadeCount;
        int m_EvasionFailureCount;
        Coroutine m_DeconstructionCoroutine;
        GameObject m_SpawnedFlower;
        Vector3 m_FlowerSupportPoint;
        float m_FlowerHeight = .11f;
        Vector3 m_FlowerLighterSize = new Vector3(.025f,.08f,.012f);
        bool m_IsDeconstructed;
        ParticleSystem m_SmokeParticles;
        float m_LastGrowlTime;
        float m_LastEvasionFailureLogTime;
        float m_LastGhostTrailTime;

        const float k_EvadeLinearDamping = 10f;
        const float k_EvadeLeashMultiplier = 2f;
        const float k_EvadeCooldown = 0.35f;
        const float k_EvasionFailureDistance = 0.10f;
        const float k_EvasionFailureMinFlee = 0.12f;
        float m_LastEvadeTime;

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
            if (m_ActiveDimension == EditDimension.Agency)
            {
                ApplyIdlePulse();
                MaybePlayGrowl();
            }

            if (m_ActiveDimension == EditDimension.Rule)
            {
                UpdateRuleEvade();
                UpdateEvasionFailures();
            }

            if (m_SpawnedFlower != null)
                UpdateFlowerPose();
        }

        void MaybePlayGrowl()
        {
            if (m_AudioSource == null || Time.time - m_LastGrowlTime < 2.2f)
                return;

            // Growl near pulse peaks for "alive" feel.
            var phase = Mathf.Sin(Time.time * m_IdlePulseSpeed);
            if (phase < 0.85f)
                return;

            m_LastGrowlTime = Time.time;
            m_AudioSource.PlayOneShot(MagicMRAudioFactory.Growl, 0.28f);
        }

        void UpdateFlowerPose()
        {
            // Rotate about the grounded stem, without lifting it off the former lighter base.
            m_SpawnedFlower.transform.RotateAround(m_FlowerSupportPoint, Vector3.up, 18f * Time.deltaTime);
        }

        void FixedUpdate()
        {
            ClampToLeash();
        }

        void ClampToLeash()
        {
            if (m_Rigidbody == null || m_IsDeconstructed)
                return;

            var anchor = GetComponent<LighterAnchorManager>();
            if (anchor != null && anchor.IsFollowingHand)
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
                case EditDimension.Scale:
                    TriggerScale();
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
            var delta = worldPosition - m_InitialPosition;
            m_InitialPosition = worldPosition;
            if (m_IsDeconstructed) m_FlowerSupportPoint += delta;
            if (m_SpawnedFlower != null)
            {
                m_SpawnedFlower.transform.position += delta;
            }
        }

        /// <summary>
        /// Re-show the lighter only if it was not intentionally deconstructed.
        /// Calibration must not resurrect a flower+lighter double state.
        /// </summary>
        public void EnsureVisible()
        {
            if (InteriorVisible) return;
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

            if (m_BurntMaterial != null)
            {
                foreach (var renderer in m_AllRenderers)
                {
                    if (renderer != null)
                        renderer.sharedMaterial = m_BurntMaterial;
                }

                Debug.Log("[MagicMR] Appearance: burnt material applied.", this);
            }
            else
            {
                Debug.LogWarning("[MagicMR] Appearance: burnt material missing.", this);
            }

            PlayAppearanceVfx();

            if (m_AudioSource != null)
            {
                if (m_BurnSfx != null)
                    m_AudioSource.PlayOneShot(m_BurnSfx);
                else
                    m_AudioSource.PlayOneShot(MagicMRAudioFactory.Whoosh, 0.35f);
            }
        }

        void PlayAppearanceVfx()
        {
            if (m_SmokeParticles == null)
                m_SmokeParticles = MagicMRVfxFactory.CreateSmoke(transform);

            m_SmokeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            m_SmokeParticles.Play();
        }

        public void TriggerAgency()
        {
            EnterState(EditDimension.Agency);
            SetAllRenderersEnabled(true);
            EnsureAgencyEyes();

            if (m_EyesObject != null)
                m_EyesObject.SetActive(true);

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

            // Explicit swipe gives a brief dodge, then returns to the moving real-object anchor.
            ApplyRuleDodge();
            Debug.Log("[MagicMR] Rule: brief lateral dodge and return to real-object anchor.", this);
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
            SpawnEvadeGhostTrail();
            if(m_DodgeReturn!=null)StopCoroutine(m_DodgeReturn);
            m_DodgeReturn=StartCoroutine(DodgeAndReturn(right*(StudySpec.RuleDodgeMeters*side)));

            m_EvadeCount++;
            m_LastEvadeTime = Time.time;

            if (m_AudioSource != null)
                m_AudioSource.PlayOneShot(MagicMRAudioFactory.Whoosh, 0.55f);
        }

        public void TriggerDeconstruction()
        {
            EnterState(EditDimension.Deconstruction);
            // Cached body renderers exclude subsequently created eyes, smoke and dust.
            m_FlowerSupportPoint = MeshBottomCenter(m_AllRenderers, transform.position);
            if (TryMeshBounds(m_AllRenderers, out var lighterBounds))
            {
                m_FlowerLighterSize = lighterBounds.size;
                m_FlowerHeight = Mathf.Clamp(lighterBounds.size.y * 1.4f, .10f, .12f);
            }

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

            // Hide body + cap + wheel so only the flower remains.
            SetAllRenderersEnabled(false);
            if (m_EyesObject != null)
                m_EyesObject.SetActive(false);

            m_IsDeconstructed = true;

            yield return new WaitForSeconds(m_DeconstructionDelay);

            SpawnFlower();
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

            m_SpawnedFlower = Instantiate(m_FlowerPrefab, m_FlowerSupportPoint,
                Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            FitFlowerHeight(m_SpawnedFlower.transform, m_FlowerHeight);
            CoverLighterWithStem(m_SpawnedFlower.transform, m_FlowerLighterSize);
            AlignFlowerBase(m_SpawnedFlower.transform, m_FlowerSupportPoint);
            MagicMRVfxFactory.CreateGoldDust(m_SpawnedFlower.transform);
            Debug.Log($"[MagicMR] Flower base aligned to lighter bottom={m_FlowerSupportPoint:F4} (no vertical bob).");
        }

        public void TriggerScale()
        {
            EnterState(EditDimension.Scale);
            // Reaching the cap and opening the interior are separate actions.
            if (m_CurrentScaleMultiplier >= StudySpec.ScaleMaxMultiplier - .0001f)
            {
                if (InteriorVisible) return;
                m_InteriorView ??= new LighterInteriorView(transform, m_AllRenderers);
                m_InteriorView.Show();
                SetAllRenderersEnabled(false);
                DataLogger.Instance?.LogEvent("interior_revealed", EditDimension.Scale,
                    -1f, CurrentVelocity, CurrentStateDuration, notes: "schematic;scale=3");
                if (m_AudioSource != null)
                    m_AudioSource.PlayOneShot(MagicMRAudioFactory.Whoosh, .45f);
                return;
            }
            SetAllRenderersEnabled(true);

            var previous = m_CurrentScaleMultiplier;
            m_CurrentScaleMultiplier = Mathf.Min(
                StudySpec.ScaleMaxMultiplier,
                previous * StudySpec.ScaleStepMultiplier);

            // Monotonic by construction: Scale can increase or remain capped, never shrink.
            var target = Vector3.Scale(m_BaseScale, Vector3.one * m_CurrentScaleMultiplier);
            transform.localScale = new Vector3(
                Mathf.Max(transform.localScale.x, target.x),
                Mathf.Max(transform.localScale.y, target.y),
                Mathf.Max(transform.localScale.z, target.z));

            if (m_AudioSource != null)
                m_AudioSource.PlayOneShot(MagicMRAudioFactory.Whoosh, 0.45f);

            Debug.Log($"[MagicMR] Scale: grew from {previous:F2}x to {m_CurrentScaleMultiplier:F2}x.", this);
        }
        IEnumerator DodgeAndReturn(Vector3 displacement)
        {
            float began=Time.time;
            while(Time.time-began<.5f)
            {
                transform.position=m_InitialPosition+displacement*Mathf.Sin((Time.time-began)/.5f*Mathf.PI);
                yield return null;
            }
            transform.position=m_InitialPosition;m_DodgeReturn=null;
        }
        void StopDodge()
        {
            if(m_DodgeReturn==null)return;
            StopCoroutine(m_DodgeReturn);m_DodgeReturn=null;transform.position=m_InitialPosition;
        }

        public void RegisterVisualBounds(Vector3 bottom, float height)
        {
            if (m_AllRenderers == null || m_AllRenderers.Length == 0) CacheRenderers();
            if (!TryMeshBounds(m_AllRenderers, out var bounds) || bounds.size.y < .0001f) return;
            transform.localScale *= height / bounds.size.y;
            transform.position += bottom - MeshBottomCenter(m_AllRenderers, transform.position);
            // Reset/Agency must restore the registered size and root offset, not the authored placeholder.
            m_BaseScale = transform.localScale;
            m_InitialPosition = transform.position;
            Debug.Log($"[Registration] virtual bottom={MeshBottomCenter(m_AllRenderers, transform.position):F4} root={transform.position:F4} scale={m_BaseScale:F4}");
        }

        public static void AlignFlowerBase(Transform flower, Vector3 supportPoint)
        {
            var basePoint = flower.Find("Base");
            if (basePoint != null) { flower.position += supportPoint - basePoint.position; return; }
            // The stem is the contact point; asymmetric petals must not shift the base sideways.
            var stem = flower.Find("Stem");
            var source = stem != null ? stem : flower;
            var bottom = MeshBottomCenter(source.GetComponentsInChildren<Renderer>(true), flower.position);
            flower.position += supportPoint - bottom;
        }

        public static void FitFlowerHeight(Transform flower, float height)
        {
            if (TryMeshBounds(flower.GetComponentsInChildren<Renderer>(true), out var bounds) && bounds.size.y > .0001f)
                flower.localScale *= Mathf.Clamp(height, .10f, .12f) / bounds.size.y;
        }

        public static void CoverLighterWithStem(Transform flower, Vector3 lighterSize)
        {
            var stem = flower.Find("Stem");
            if (stem == null) return;
            var filter = stem.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            // Preserve coverage for every yaw of the rotating flower. Enlarge the bloom
            // and leaves together with the stem, rather than inflating only the stem.
            float diameter = Mathf.Max(.018f,new Vector2(lighterSize.x,lighterSize.z).magnitude + .004f);
            Vector3 size = filter.sharedMesh.bounds.size;
            Vector3 parentScale = flower.lossyScale;
            var scale = stem.localScale;
            scale.x = diameter / Mathf.Max(.0001f,size.x*Mathf.Abs(parentScale.x));
            scale.z = diameter / Mathf.Max(.0001f,size.z*Mathf.Abs(parentScale.z));
            float newHeight = Mathf.Max(.05f,lighterSize.y+.006f);
            scale.y = newHeight / Mathf.Max(.0001f,size.y*Mathf.Abs(parentScale.y));
            stem.localScale = scale;
            var leaves = flower.Find("Leaves");
            if (leaves != null) leaves.localScale = scale;
            var blossom = flower.Find("Blossom");
            if (blossom != null)
            {
                blossom.localPosition = Vector3.up*newHeight/Mathf.Max(.0001f,Mathf.Abs(parentScale.y));
                float currentDiameter=0;
                foreach(var part in blossom.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(part.sharedMesh==null)continue;
                    foreach(var vertex in part.sharedMesh.vertices)
                    {
                        Vector3 p=part.transform.TransformPoint(vertex)-blossom.position;
                        currentDiameter=Mathf.Max(currentDiameter,2*new Vector2(p.x,p.z).magnitude);
                    }
                }
                // Vertex radius is independent of the summon yaw; a rotated bounding
                // box would make identical flowers shrink at diagonal viewing angles.
                if(currentDiameter>.0001f)blossom.localScale*=Mathf.Max(.085f,diameter*4.5f)/currentDiameter;
            }
        }

        static Vector3 MeshBottomCenter(Renderer[] renderers, Vector3 fallback)
        {
            return TryMeshBounds(renderers, out var bounds)
                ? new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) : fallback;
        }

        static bool TryMeshBounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            if (renderers != null) foreach (var renderer in renderers)
            {
                if (renderer == null || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                // Hidden renderers may retain world bounds until Unity's renderer update.
                // Registration scales and queries again in the same frame: transform local bounds explicitly.
                Bounds local;
                if (renderer is SkinnedMeshRenderer skinned) local = skinned.localBounds;
                else
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    local = filter.sharedMesh.bounds;
                }
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 sign = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                    Vector3 point = renderer.transform.TransformPoint(local.center + Vector3.Scale(local.extents, sign));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return found;
        }

        void ClearFlower()
        {
            if (m_SpawnedFlower == null)
                return;

            Destroy(m_SpawnedFlower);
            m_SpawnedFlower = null;
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
            transform.localScale = m_BaseScale * m_CurrentScaleMultiplier;
            Debug.Log("[MagicMR] Restored lighter from deconstruction (flower cleared).", this);
        }

        void SetAllRenderersEnabled(bool enabled)
        {
            if (m_AllRenderers == null || m_AllRenderers.Length == 0)
                CacheRenderers();

            foreach (var renderer in m_AllRenderers)
            {
                if (renderer != null)
                    renderer.enabled = enabled;
            }
        }

        void RestoreOriginalMaterials()
        {
            if (m_AllRenderers == null)
                return;

            for (var i = 0; i < m_AllRenderers.Length; i++)
            {
                if (m_AllRenderers[i] == null)
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
        }

        void EnterState(EditDimension dimension)
        {
            if (dimension != EditDimension.Scale && InteriorVisible)
            {
                m_InteriorView.Hide();
                SetAllRenderersEnabled(true);
            }
            if(dimension!=EditDimension.Rule)StopDodge();
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
                    m_EyesObject.SetActive(false);
                if (m_AgencyAnimator != null && !string.IsNullOrEmpty(m_AliveBoolParameter))
                    m_AgencyAnimator.SetBool(m_AliveBoolParameter, false);
                transform.localScale = m_BaseScale * m_CurrentScaleMultiplier;
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
            var pulse = 1f + Mathf.Sin(Time.time * m_IdlePulseSpeed) * m_IdlePulseScale;
            transform.localScale = m_BaseScale * (m_CurrentScaleMultiplier * pulse);
        }

        void UpdateRuleEvade()
        {
            if (m_IsDeconstructed || m_Rigidbody == null || m_Rigidbody.isKinematic)
                return;

            // While pseudo-dynamic hand attachment is active, do not fight the
            // hand pose with evade impulses (hybrid tracking Mode B).
            var anchor = GetComponent<LighterAnchorManager>();
            if (anchor != null && anchor.IsFollowingHand)
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

            m_Rigidbody.AddForce(evadeDir.normalized * m_EvadeImpulse, ForceMode.Impulse);
            m_EvadeCount++;
            m_LastEvadeTime = Time.time;

            SpawnEvadeGhostTrail();
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

            var anchor = GetComponent<LighterAnchorManager>();
            var pin = anchor != null && anchor.IsCalibrated ? anchor.PinnedPosition : m_InitialPosition;
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
            var subsystems = new System.Collections.Generic.List<UnityEngine.XR.Hands.XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            if (subsystems.Count == 0)
                return false;

            var subsystem = subsystems[0];
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
            m_InteriorView?.Hide();
            StopDodge();
            StopAllCoroutines();
            m_DeconstructionCoroutine = null;
            ClearFlower();
            m_IsDeconstructed = false;

            SetAllRenderersEnabled(true);
            RestoreOriginalMaterials();

            if (m_EyesObject != null)
                m_EyesObject.SetActive(false);

            if (m_AgencyAnimator != null && !string.IsNullOrEmpty(m_AliveBoolParameter))
                m_AgencyAnimator.SetBool(m_AliveBoolParameter, false);

            transform.localScale = m_BaseScale;
            m_CurrentScaleMultiplier = 1f;
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
        }

        public void OnGestureA_Pinch() => ApplyDimension(EditDimension.Appearance, Vector3.zero, false);
        public void OnGestureB_Swipe() => ApplyDimension(EditDimension.Rule, Vector3.zero, false);
        public void OnGestureC_Circle() => ApplyDimension(EditDimension.Agency, Vector3.zero, false);
        public void OnGestureD_Snap() => ApplyDimension(EditDimension.Deconstruction, Vector3.zero, false);
        public void OnGestureD_FistBurst() => ApplyDimension(EditDimension.Deconstruction, Vector3.zero, false);
        public void OnGestureE_Scale() => ApplyDimension(EditDimension.Scale, Vector3.zero, false);

        void OnDestroy()
        {
            m_InteriorView?.Dispose();
        }
    }

    // Runtime schematic: dimensions follow the authored mesh, with no claims of
    // measured fuel level or scanned internal geometry. Opaque wire casing avoids
    // transparent-material sorting and shader variant issues on standalone MR.
    internal sealed class LighterInteriorView
    {
        readonly GameObject root;
        readonly System.Collections.Generic.List<Material> materials = new System.Collections.Generic.List<Material>();
        public bool Visible => root != null && root.activeSelf;

        public LighterInteriorView(Transform owner, Renderer[] body)
        {
            root = new GameObject("LighterInterior_Schematic");
            root.transform.SetParent(owner, false);
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            bool found = false;
            foreach (var renderer in body)
            {
                if (renderer == null) continue;
                var mesh = renderer.GetComponent<MeshFilter>();
                if (mesh == null || mesh.sharedMesh == null) continue;
                var local = mesh.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var sign = new Vector3((i & 1) == 0 ? -1 : 1,
                        (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                    var p = owner.InverseTransformPoint(renderer.transform.TransformPoint(
                        local.center + Vector3.Scale(local.extents, sign)));
                    if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                    else bounds.Encapsulate(p);
                }
            }
            root.transform.localPosition = bounds.center;
            root.transform.localScale = bounds.size;
            var cyan = MakeMaterial(new Color(.12f, .8f, 1f));
            var fuel = MakeMaterial(new Color(.1f, .55f, .9f));
            var metal = MakeMaterial(new Color(.75f, .8f, .88f));
            var orange = MakeMaterial(new Color(1f, .45f, .06f));
            // Twelve casing edges retain the silhouette while exposing the interior.
            for (int axis = 0; axis < 3; axis++)
                for (int a = -1; a <= 1; a += 2)
                    for (int b = -1; b <= 1; b += 2)
                    {
                        var center = Vector3.zero;
                        center[(axis + 1) % 3] = a * .5f;
                        center[(axis + 2) % 3] = b * .5f;
                        var size = Vector3.one * .012f; size[axis] = 1f;
                        Part("CasingEdge", PrimitiveType.Cube, center, size, cyan);
                    }
            Part("FuelReservoir", PrimitiveType.Cube, new Vector3(-.10f,-.12f,0), new Vector3(.56f,.62f,.55f), fuel);
            Part("Flint", PrimitiveType.Cylinder, new Vector3(.27f,.12f,0), new Vector3(.09f,.16f,.14f), metal);
            var wheel = Part("IgnitionWheel", PrimitiveType.Cylinder, new Vector3(.27f,.34f,0), new Vector3(.23f,.10f,.23f), metal);
            wheel.localRotation = Quaternion.Euler(90,0,0);
            Part("Nozzle", PrimitiveType.Cylinder, new Vector3(-.12f,.32f,0), new Vector3(.09f,.06f,.14f), metal);
            Part("FuelPath", PrimitiveType.Cube, new Vector3(-.12f,.22f,-.32f), new Vector3(.025f,.42f,.025f), orange);
            Part("FlamePath", PrimitiveType.Capsule, new Vector3(-.12f,.52f,0), new Vector3(.08f,.1f,.1f), orange);
            Label("INTERNAL VIEW (SCHEMATIC)", new Vector3(.65f,.60f,0), cyan.color);
            Label("Ignition wheel", new Vector3(.65f,.34f,0), metal.color);
            Label("Flint / nozzle", new Vector3(.65f,.15f,0), metal.color);
            Label("Fuel reservoir", new Vector3(.65f,-.12f,0), fuel.color);
            Label("Orange: fuel / flame path", new Vector3(.65f,-.39f,0), orange.color);
            root.SetActive(false);
        }

        Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader); material.color = color;
            materials.Add(material); return material;
        }

        Transform Part(string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(primitive); part.name = name;
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            var collider = part.GetComponent<Collider>(); collider.enabled = false;
            Object.Destroy(collider);
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        void Label(string caption, Vector3 position, Color color)
        {
            var label = new GameObject(caption); label.transform.SetParent(root.transform, false);
            label.transform.localPosition = position;
            // Compensate for the lighter's nonuniform dimensions to keep text legible.
            var s = root.transform.lossyScale;
            float height = Mathf.Abs(s.y) * .045f;
            label.transform.localScale = new Vector3(height / Mathf.Max(.00001f, Mathf.Abs(s.x)),
                height / Mathf.Max(.00001f, Mathf.Abs(s.y)), height / Mathf.Max(.00001f, Mathf.Abs(s.z)));
            var text = label.AddComponent<TextMesh>(); text.text = caption;
            text.fontSize = 64; text.characterSize = .15f; text.color = color;
            text.anchor = TextAnchor.MiddleLeft;
        }

        public void Show() { root.SetActive(true); }
        public void Hide() { root.SetActive(false); }
        public void Dispose()
        {
            if (root != null) Object.Destroy(root);
            foreach (var material in materials) Object.Destroy(material);
        }
    }
}
