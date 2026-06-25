using System.Collections;
using UnityEngine;

namespace MagicMR
{
    /// <summary>
    /// MVP magic target: lighter -> burnt -> demon -> flower.
    /// Wire gesture detector UnityEvents to these public methods in Inspector.
    /// </summary>
    public class MagicTarget : MonoBehaviour
    {
        [Header("Lighter")]
        [SerializeField]
        Renderer m_LighterRenderer;

        [SerializeField]
        Material m_BurntMaterial;

        [Header("Demon")]
        [SerializeField]
        GameObject m_DemonPrefab;

        [SerializeField]
        Transform m_DemonSpawnPoint;

        [SerializeField]
        Animator m_DemonAnimator;

        [SerializeField]
        string m_CoughAnimationTrigger = "Cough";

        [Header("Flower")]
        [SerializeField]
        GameObject m_FlowerPrefab;

        [SerializeField]
        ParticleSystem m_MagicVfx;

        [SerializeField]
        float m_VfxDuration = 1.5f;

        Material m_OriginalMaterial;
        GameObject m_DemonInstance;

        void Awake()
        {
            if (m_LighterRenderer != null)
                m_OriginalMaterial = m_LighterRenderer.sharedMaterial;
        }

        // Gesture A: pinch -> burnt lighter
        public void OnGestureA_Pinch()
        {
            if (m_LighterRenderer == null || m_BurntMaterial == null)
                return;

            m_LighterRenderer.sharedMaterial = m_BurntMaterial;
        }

        // Gesture B: swipe -> summon demon
        public void OnGestureB_Swipe()
        {
            if (m_DemonPrefab == null)
                return;

            if (m_LighterRenderer != null)
                m_LighterRenderer.gameObject.SetActive(false);

            if (m_DemonInstance != null)
                Destroy(m_DemonInstance);

            var spawnPoint = m_DemonSpawnPoint != null ? m_DemonSpawnPoint : transform;
            m_DemonInstance = Instantiate(m_DemonPrefab, spawnPoint.position, spawnPoint.rotation);
            m_DemonAnimator = m_DemonInstance.GetComponentInChildren<Animator>();

            if (m_DemonAnimator != null && !string.IsNullOrEmpty(m_CoughAnimationTrigger))
                m_DemonAnimator.SetTrigger(m_CoughAnimationTrigger);
        }

        // Gesture D: snap -> vfx, remove demon, spawn flower
        public void OnGestureD_Snap()
        {
            StartCoroutine(SnapRoutine());
        }

        IEnumerator SnapRoutine()
        {
            var spawnPoint = m_DemonSpawnPoint != null ? m_DemonSpawnPoint : transform;

            if (m_MagicVfx != null)
            {
                var vfx = Instantiate(m_MagicVfx, spawnPoint.position, spawnPoint.rotation);
                Destroy(vfx.gameObject, m_VfxDuration);
            }

            if (m_DemonInstance != null)
            {
                Destroy(m_DemonInstance);
                m_DemonInstance = null;
            }

            yield return new WaitForSeconds(m_VfxDuration);

            if (m_FlowerPrefab != null)
                Instantiate(m_FlowerPrefab, spawnPoint.position, spawnPoint.rotation);
        }

        public void ResetTarget()
        {
            if (m_LighterRenderer != null)
            {
                m_LighterRenderer.gameObject.SetActive(true);
                if (m_OriginalMaterial != null)
                    m_LighterRenderer.sharedMaterial = m_OriginalMaterial;
            }

            if (m_DemonInstance != null)
            {
                Destroy(m_DemonInstance);
                m_DemonInstance = null;
            }
        }
    }
}
