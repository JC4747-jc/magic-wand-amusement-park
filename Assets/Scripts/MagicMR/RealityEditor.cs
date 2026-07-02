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
    /// Appearance (Pinch), Agency (Circle), Rule (Swipe), Deconstruction (Snap).
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

        [Header("Deconstruction")]
        [SerializeField]
        ParticleSystem m_DeconstructionVfx;

        [SerializeField]
        GameObject m_FlowerPrefab;

        [SerializeField]
        float m_DeconstructionDelay = StudySpec.DeconstructionDelay;

        Material m_OriginalMaterial;
        Vector3 m_BaseScale;
        Vector3 m_InitialPosition;
        Rigidbody m_Rigidbody;
        EditDimension m_ActiveDimension = EditDimension.None;
        float m_StateEnterTime;
        int m_EvadeCount;
        Coroutine m_DeconstructionCoroutine;

        // Without damping the object would drift forever at a constant velocity
        // (no gravity, no friction); this keeps the "evade" hop short and bounded
        // to a small leash around its spawn point instead of flying across the room.
        const float k_EvadeLinearDamping = 10f;
        const float k_EvadeLeashMultiplier = 2f;

        public EditDimension ActiveDimension => m_ActiveDimension;
        public float CurrentStateDuration => m_ActiveDimension == EditDimension.None ? 0f : Time.time - m_StateEnterTime;
        public float CurrentVelocity =>
            m_Rigidbody != null ? m_Rigidbody.linearVelocity.magnitude : 0f;

        public int EvadeCount => m_EvadeCount;

        void Awake()
        {
            if (m_TargetRenderer == null)
                m_TargetRenderer = GetComponentInChildren<Renderer>();

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
        }

        void Update()
        {
            if (m_ActiveDimension == EditDimension.Agency && m_AgencyAnimator == null)
                ApplyIdlePulse();

            if (m_ActiveDimension == EditDimension.Rule)
                UpdateRuleEvade();
        }

        void FixedUpdate()
        {
            ClampToLeash();
        }

        void ClampToLeash()
        {
            if (m_Rigidbody == null)
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

        public void TriggerAppearance()
        {
            EnterState(EditDimension.Appearance);

            if (m_TargetRenderer != null && m_BurntMaterial != null)
                m_TargetRenderer.sharedMaterial = m_BurntMaterial;

            if (m_AudioSource != null && m_BurnSfx != null)
                m_AudioSource.PlayOneShot(m_BurnSfx);
        }

        public void TriggerAgency()
        {
            EnterState(EditDimension.Agency);

            if (m_EyesObject != null)
                m_EyesObject.SetActive(true);

            if (m_AgencyAnimator != null && !string.IsNullOrEmpty(m_AliveBoolParameter))
                m_AgencyAnimator.SetBool(m_AliveBoolParameter, true);
        }

        public void TriggerRule()
        {
            EnterState(EditDimension.Rule);
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
            if (m_DeconstructionVfx != null)
            {
                var vfx = Instantiate(m_DeconstructionVfx, transform.position, transform.rotation);
                Destroy(vfx.gameObject, m_DeconstructionDelay + 1f);
            }

            if (m_TargetRenderer != null)
                m_TargetRenderer.enabled = false;

            yield return new WaitForSeconds(m_DeconstructionDelay);

            if (m_FlowerPrefab != null)
                Instantiate(m_FlowerPrefab, transform.position, transform.rotation);
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
            transform.localScale = m_BaseScale * pulse;
        }

        void UpdateRuleEvade()
        {
            var hand = FindTrackedHand();
            if (!hand.HasValue)
                return;

            var handPos = hand.Value;
            var distance = Vector3.Distance(transform.position, handPos);
            if (distance >= m_EvadeDistance)
                return;

            var evadeDir = (transform.position - handPos).normalized;
            if (m_ConstrainEvadeToXZ)
                evadeDir.y = 0f;

            if (evadeDir.sqrMagnitude < 0.0001f)
                evadeDir = Vector3.forward;

            m_Rigidbody.AddForce(evadeDir.normalized * m_EvadeImpulse, ForceMode.Impulse);
            m_EvadeCount++;

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

        Vector3? FindTrackedHand()
        {
#if XR_HANDS_1_1_OR_NEWER
            var subsystems = new System.Collections.Generic.List<UnityEngine.XR.Hands.XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            if (subsystems.Count == 0)
                return null;

            var hand = subsystems[0].rightHand;
            if (!hand.isTracked || !hand.GetJoint(UnityEngine.XR.Hands.XRHandJointID.Palm).TryGetPose(out var pose))
                return null;

            return pose.position;
#else
            return null;
#endif
        }

        public void ResetTarget()
        {
            StopAllCoroutines();
            m_DeconstructionCoroutine = null;

            if (m_TargetRenderer != null)
            {
                m_TargetRenderer.enabled = true;
                if (m_OriginalMaterial != null)
                    m_TargetRenderer.sharedMaterial = m_OriginalMaterial;
            }

            if (m_EyesObject != null)
                m_EyesObject.SetActive(false);

            if (m_AgencyAnimator != null && !string.IsNullOrEmpty(m_AliveBoolParameter))
                m_AgencyAnimator.SetBool(m_AliveBoolParameter, false);

            transform.localScale = m_BaseScale;
            transform.position = m_InitialPosition;

            if (m_Rigidbody != null)
            {
                m_Rigidbody.linearVelocity = Vector3.zero;
                m_Rigidbody.angularVelocity = Vector3.zero;
            }

            m_EvadeCount = 0;
            m_ActiveDimension = EditDimension.None;
            m_StateEnterTime = Time.time;
        }

        // Legacy hooks for old Inspector wiring (no-op redirects).
        public void OnGestureA_Pinch() => ApplyDimension(EditDimension.Appearance, Vector3.zero, false);
        public void OnGestureB_Swipe() => ApplyDimension(EditDimension.Rule, Vector3.zero, false);
        public void OnGestureC_Circle() => ApplyDimension(EditDimension.Agency, Vector3.zero, false);
        public void OnGestureD_Snap() => ApplyDimension(EditDimension.Deconstruction, Vector3.zero, false);
    }
}
