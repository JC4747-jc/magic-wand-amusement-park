using UnityEngine;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    /// <summary>
    /// Floating "Reset" label. Pinch near the button with either hand (~0.3s).
    /// No collider click — MR uses hand pinch proximity, not screen tap.
    /// </summary>
    public class StudyResetWristUi : MonoBehaviour
    {
        [SerializeField]
        float m_PinchActivateDistance = StudySpec.ResetButtonPinchDistance;

        [SerializeField]
        float m_HoldSeconds = StudySpec.ResetButtonHoldSeconds;

        [SerializeField]
        float m_StartupGuardSeconds = 0.5f;

        Transform m_Button;
        Material m_ButtonMat;
        TextMesh m_Label;
        AudioSource m_Audio;
        Vector3 m_ButtonBaseScale;
        readonly Color m_IdleColor = new Color(0.95f, 0.35f, 0.3f, 0.95f);
        readonly Color m_ChargeColor = new Color(1f, 0.85f, 0.2f, 1f);
        float m_HoldTimer;
        float m_CooldownUntil;
        float m_EarliestActivateTime;

        void Start()
        {
            m_Audio = gameObject.AddComponent<AudioSource>();
            m_Audio.playOnAwake = false;
            m_EarliestActivateTime = Time.time + m_StartupGuardSeconds;

            var buttonGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            buttonGo.name = "ResetButton";
            buttonGo.transform.SetParent(transform, false);
            m_ButtonBaseScale = new Vector3(0.14f, 0.055f, 0.02f);
            buttonGo.transform.localScale = m_ButtonBaseScale;
            Destroy(buttonGo.GetComponent<Collider>());
            m_Button = buttonGo.transform;

            m_ButtonMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            ApplyButtonColor(m_IdleColor);
            buttonGo.GetComponent<Renderer>().sharedMaterial = m_ButtonMat;

            var labelGo = new GameObject("ResetLabel");
            labelGo.transform.SetParent(m_Button, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.012f);
            labelGo.transform.localScale = Vector3.one * 0.012f;
            m_Label = labelGo.AddComponent<TextMesh>();
            m_Label.text = "Reset";
            m_Label.fontSize = 48;
            m_Label.characterSize = 0.4f;
            m_Label.anchor = TextAnchor.MiddleCenter;
            m_Label.alignment = TextAlignment.Center;
            m_Label.color = Color.white;

            Debug.Log("[MagicMR] Reset label ready (pinch near button, either hand).", this);
        }

        void LateUpdate()
        {
            UpdateFloatingPose();
            UpdatePinchActivation();
        }

        void UpdateFloatingPose()
        {
            if (m_Button == null)
                return;

            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null)
                return;

            m_Button.gameObject.SetActive(true);
            m_Button.position = cam.position + cam.forward * 0.5f + cam.up * -0.16f + cam.right * -0.22f;
            m_Button.rotation = Quaternion.LookRotation(m_Button.position - cam.position, Vector3.up);
        }

        void UpdatePinchActivation()
        {
            if (m_Button == null || Time.time < m_CooldownUntil || Time.time < m_EarliestActivateTime)
            {
                ResetChargeVisual();
                return;
            }

#if XR_HANDS_1_1_OR_NEWER
            var subsystems = new System.Collections.Generic.List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            if (subsystems.Count == 0)
                return;

            var subsystem = subsystems[0];
            var near =
                IsPinchNearButton(subsystem.leftHand) ||
                IsPinchNearButton(subsystem.rightHand);

            if (!near)
            {
                m_HoldTimer = 0f;
                ResetChargeVisual();
                return;
            }

            m_HoldTimer += Time.deltaTime;
            var t = Mathf.Clamp01(m_HoldTimer / m_HoldSeconds);
            ApplyButtonColor(Color.Lerp(m_IdleColor, m_ChargeColor, t));
            m_Button.localScale = m_ButtonBaseScale * (1f + 0.35f * t);

            if (m_HoldTimer >= m_HoldSeconds)
            {
                TriggerReset();
                m_HoldTimer = 0f;
                m_CooldownUntil = Time.time + 1.0f;
                ResetChargeVisual();
            }
#endif
        }

#if XR_HANDS_1_1_OR_NEWER
        bool IsPinchNearButton(XRHand hand)
        {
            if (!hand.isTracked)
                return false;

            if (!hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb) ||
                !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index))
                return false;

            var pinchDist = Vector3.Distance(thumb.position, index.position);
            if (pinchDist > StudySpec.HoldPinchDistanceThreshold)
                return false;

            var pinchPoint = (thumb.position + index.position) * 0.5f;
            return Vector3.Distance(pinchPoint, m_Button.position) < m_PinchActivateDistance;
        }
#endif

        void ResetChargeVisual()
        {
            if (m_Button != null)
                m_Button.localScale = m_ButtonBaseScale;
            ApplyButtonColor(m_IdleColor);
        }

        void ApplyButtonColor(Color color)
        {
            if (m_ButtonMat == null)
                return;
            if (m_ButtonMat.HasProperty("_BaseColor"))
                m_ButtonMat.SetColor("_BaseColor", color);
            if (m_ButtonMat.HasProperty("_Color"))
                m_ButtonMat.SetColor("_Color", color);
        }

        public void TriggerReset()
        {
            var fsm = MRGestureController.Instance;
            if (fsm != null)
                fsm.PerformFullReset("reset_button");
            else
                LegacyReset();

            if (m_Audio != null)
                m_Audio.PlayOneShot(MagicMRAudioFactory.ResetChime, 0.8f);

            DataLogger.Instance?.LogEvent(
                "study_reset",
                EditDimension.None,
                -1f,
                0f,
                0f,
                notes: "reset_button");

            Debug.Log("[MagicMR] Study reset via Reset label.", this);
        }

        static void LegacyReset()
        {
            var lighter = GameObject.Find("Lighter");
            var editor = lighter != null ? lighter.GetComponent<RealityEditor>() : null;
            editor?.ResetTarget();
            FindFirstObjectByType<LighterAnchorManager>()?.BeginRecalibration();
            FindFirstObjectByType<CalibrationRitual>()?.RestartRitual();
            FindFirstObjectByType<PinchGestureDetector>()?.ResetForNewTrial();
            FindFirstObjectByType<GestureManager>()?.NotifyStudyReset();
        }
    }
}
