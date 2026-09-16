using UnityEngine;

namespace MagicMR
{
    /// <summary>
    /// Fades a shared afterimage material, then destroys the ghost and its material.
    /// </summary>
    public sealed class MagicMRGhostFader : MonoBehaviour
    {
        float m_Lifetime = 0.45f;
        float m_Age;
        Material m_Mat;
        Color m_Base;

        public void Begin(float lifetime, Material mat)
        {
            m_Lifetime = Mathf.Max(0.05f, lifetime);
            m_Mat = mat;
            if (m_Mat != null)
                m_Base = m_Mat.HasProperty("_BaseColor") ? m_Mat.GetColor("_BaseColor") : m_Mat.color;
        }

        void Update()
        {
            m_Age += Time.deltaTime;
            var t = MagicMRAnim.Saturate(m_Age / m_Lifetime);
            if (m_Mat != null)
            {
                var c = m_Base;
                c.a = m_Base.a * (1f - MagicMRAnim.SmoothStep(t));
                if (m_Mat.HasProperty("_BaseColor"))
                    m_Mat.SetColor("_BaseColor", c);
                if (m_Mat.HasProperty("_Color"))
                    m_Mat.SetColor("_Color", c);
            }

            if (m_Age >= m_Lifetime)
                Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (m_Mat != null)
                Destroy(m_Mat);
        }
    }
}
