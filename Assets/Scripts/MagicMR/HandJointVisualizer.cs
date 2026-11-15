using System.Collections.Generic;
using UnityEngine;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    /// <summary>
    /// Renders small spheres at every tracked hand joint so the user can see their hands.
    /// Gesture detection works without this; this is purely visual feedback.
    /// </summary>
    public class HandJointVisualizer : MonoBehaviour
    {
        [SerializeField]
        float m_JointSize = 0.012f;

        [SerializeField]
        Color m_JointColor = new Color(0.2f, 0.8f, 1f, 1f);

#if XR_HANDS_1_1_OR_NEWER
        XRHandSubsystem m_Subsystem;
        static readonly List<XRHandSubsystem> s_Subsystems = new List<XRHandSubsystem>();

        readonly List<Transform> m_LeftJoints = new List<Transform>();
        readonly List<Transform> m_RightJoints = new List<Transform>();

        Material m_JointMaterial;
        int m_JointCount;

        void OnEnable()
        {
            SubsystemManager.GetSubsystems(s_Subsystems);
            if (s_Subsystems.Count > 0)
                m_Subsystem = s_Subsystems[0];

            m_JointCount = XRHandJointID.EndMarker.ToIndex();
            CreateMaterial();
            BuildJoints(m_LeftJoints, "Left");
            BuildJoints(m_RightJoints, "Right");
        }

        void CreateMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Standard");

            m_JointMaterial = new Material(shader);
            if (m_JointMaterial.HasProperty("_BaseColor"))
                m_JointMaterial.SetColor("_BaseColor", m_JointColor);
            if (m_JointMaterial.HasProperty("_Color"))
                m_JointMaterial.SetColor("_Color", m_JointColor);
        }

        void BuildJoints(List<Transform> list, string label)
        {
            for (var i = 0; i < m_JointCount; i++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"HandJoint_{label}_{i}";
                var collider = sphere.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);

                var renderer = sphere.GetComponent<Renderer>();
                if (renderer != null && m_JointMaterial != null)
                    renderer.sharedMaterial = m_JointMaterial;

                sphere.transform.SetParent(transform, false);
                sphere.transform.localScale = Vector3.one * m_JointSize;
                sphere.SetActive(false);
                list.Add(sphere.transform);
            }
        }

        void Update()
        {
            if (m_Subsystem == null)
            {
                SubsystemManager.GetSubsystems(s_Subsystems);
                if (s_Subsystems.Count > 0)
                    m_Subsystem = s_Subsystems[0];
                if (m_Subsystem == null)
                    return;
            }

            UpdateHand(m_Subsystem.leftHand, m_LeftJoints);
            UpdateHand(m_Subsystem.rightHand, m_RightJoints);
        }

        void UpdateHand(XRHand hand, List<Transform> joints)
        {
            if (!hand.isTracked)
            {
                SetActive(joints, false);
                return;
            }

            for (var i = 0; i < joints.Count; i++)
            {
                var jointId = XRHandJointIDUtility.FromIndex(i);
                if (hand.GetJoint(jointId).TryGetPose(out var pose))
                {
                    joints[i].SetPositionAndRotation(pose.position, pose.rotation);
                    joints[i].gameObject.SetActive(true);
                }
                else
                {
                    joints[i].gameObject.SetActive(false);
                }
            }
        }

        static void SetActive(List<Transform> joints, bool active)
        {
            for (var i = 0; i < joints.Count; i++)
                joints[i].gameObject.SetActive(active);
        }
#endif
    }
}
