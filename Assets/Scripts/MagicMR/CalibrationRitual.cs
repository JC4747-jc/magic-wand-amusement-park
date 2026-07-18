using UnityEngine;

namespace MagicMR
{
    /// <summary>
    /// Embodied registration: semi-transparent lighter-shaped wireframe ghost
    /// pinned at the desk preset (not a floating camera billboard).
    /// </summary>
    public class CalibrationRitual : MonoBehaviour
    {
        [SerializeField]
        LighterAnchorManager m_Anchor;

        [SerializeField]
        Transform m_Lighter;

        [SerializeField]
        Camera m_Camera;

        GameObject m_Ghost;
        TextMesh m_Prompt;
        AudioSource m_Audio;
        ParticleSystem m_Ring;
        bool m_Completed;
        Renderer[] m_LighterRenderers;
        bool[] m_LighterRendererWasEnabled;
        Vector3 m_GhostBaseScale = Vector3.one * 0.08f;
        Vector3 m_WireframePosition;
        Quaternion m_WireframeRotation = Quaternion.identity;
        bool m_HasWireframePose;

        public bool IsComplete => m_Completed;

        void Start()
        {
            if (m_Lighter == null)
            {
                var go = GameObject.Find("Lighter");
                if (go != null)
                    m_Lighter = go.transform;
            }

            if (m_Anchor == null)
                m_Anchor = FindFirstObjectByType<LighterAnchorManager>();

            if (m_Camera == null)
                m_Camera = Camera.main;

            m_Audio = gameObject.AddComponent<AudioSource>();
            m_Audio.playOnAwake = false;
            m_Audio.spatialBlend = 0.35f;

            if (m_Anchor != null)
                m_Anchor.Calibrated += OnCalibrated;

            CapturePoseFromLighter();

            if (m_Anchor != null && m_Anchor.IsCalibrated)
            {
                m_Completed = true;
                return;
            }

            CacheAndDimLighter();
            SpawnGhostAndPrompt();
        }

        void OnDestroy()
        {
            if (m_Anchor != null)
                m_Anchor.Calibrated -= OnCalibrated;
        }

        public void SetWireframeWorldPose(Vector3 position, Quaternion rotation)
        {
            m_WireframePosition = position;
            m_WireframeRotation = rotation;
            m_HasWireframePose = true;

            if (m_Ghost != null)
            {
                m_Ghost.transform.SetPositionAndRotation(position, rotation);
                m_Ghost.transform.localScale = m_GhostBaseScale;
            }

            m_Anchor?.SetDeskReferenceHeight(position.y);
        }

        public bool TryGetWireframePosition(out Vector3 position)
        {
            if (m_Ghost != null)
            {
                position = m_Ghost.transform.position;
                return true;
            }

            if (m_HasWireframePose)
            {
                position = m_WireframePosition;
                return true;
            }

            position = default;
            return false;
        }

        void LateUpdate()
        {
            if (m_Completed || m_Ghost == null)
                return;

            if (!m_HasWireframePose)
                CapturePoseFromLighter();

            if (m_HasWireframePose)
            {
                m_Ghost.transform.SetPositionAndRotation(m_WireframePosition, m_WireframeRotation);
                m_Ghost.transform.localScale = m_GhostBaseScale;
                m_Anchor?.SetDeskReferenceHeight(m_WireframePosition.y);
            }

            if (m_Prompt == null)
                return;

            if (m_Camera == null)
                m_Camera = Camera.main;

            var promptPos = m_Ghost.transform.position + Vector3.up * 0.11f;
            m_Prompt.transform.position = promptPos;
            if (m_Camera != null)
            {
                m_Prompt.transform.rotation = Quaternion.LookRotation(
                    m_Prompt.transform.position - m_Camera.transform.position);
            }

            var fsm = MRGestureController.Instance;
            var progress = fsm != null
                ? fsm.CalibrationHoldProgress
                : (FindFirstObjectByType<PinchGestureDetector>()?.CalibrationHoldProgress ?? 0f);

            if (fsm != null && fsm.State == MRState.Cooldown)
            {
                m_Prompt.text = "准备中…";
                m_Prompt.color = new Color(0.7f, 0.8f, 0.9f, 0.85f);
            }
            else if (progress > 0.02f && progress < 0.999f)
            {
                m_Prompt.text = $"校准中… {Mathf.CeilToInt(progress * 100f)}%";
                m_Prompt.color = Color.Lerp(
                    new Color(0.85f, 0.95f, 1f, 0.95f),
                    new Color(0.4f, 1f, 0.55f, 1f),
                    progress);
            }
            else
            {
                m_Prompt.text = "对齐半透明火机，右手捏合并保持约1秒";
                m_Prompt.color = new Color(0.85f, 0.95f, 1f, 0.95f);
            }
        }

        void CapturePoseFromLighter()
        {
            var fsm = MRGestureController.Instance;
            if (fsm != null && fsm.HasDeskPreset)
            {
                SetWireframeWorldPose(fsm.DeskPresetPosition, fsm.DeskPresetRotation);
                return;
            }

            if (m_Lighter == null)
                return;

            SetWireframeWorldPose(m_Lighter.position, m_Lighter.rotation);
        }

        void SpawnGhostAndPrompt()
        {
            if (m_Lighter != null)
            {
                m_GhostBaseScale = m_Lighter.localScale;
                if (m_GhostBaseScale.magnitude < 0.01f)
                    m_GhostBaseScale = Vector3.one * 0.08f;

                m_Ghost = Instantiate(m_Lighter.gameObject);
                m_Ghost.name = "GhostLighter_Calibration";

                foreach (var rb in m_Ghost.GetComponentsInChildren<Rigidbody>(true))
                    Destroy(rb);
                foreach (var behaviour in m_Ghost.GetComponentsInChildren<MonoBehaviour>(true))
                    Destroy(behaviour);
                foreach (var col in m_Ghost.GetComponentsInChildren<Collider>(true))
                    Destroy(col);
            }
            else
            {
                m_Ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                m_Ghost.name = "GhostLighter_Calibration";
                Destroy(m_Ghost.GetComponent<Collider>());
                m_GhostBaseScale = Vector3.one * 0.08f;
            }

            m_Ghost.transform.localScale = m_GhostBaseScale;

            var ghostMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            var color = new Color(0.4f, 0.85f, 1f, 0.28f);
            if (ghostMat.HasProperty("_BaseColor"))
                ghostMat.SetColor("_BaseColor", color);
            if (ghostMat.HasProperty("_Color"))
                ghostMat.SetColor("_Color", color);
            SetMaterialTransparent(ghostMat);

            foreach (var renderer in m_Ghost.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;
                renderer.sharedMaterial = ghostMat;
                renderer.enabled = true;
            }

            if (m_HasWireframePose)
                m_Ghost.transform.SetPositionAndRotation(m_WireframePosition, m_WireframeRotation);

            var promptGo = new GameObject("CalibrationPrompt");
            promptGo.transform.SetParent(transform, false);
            m_Prompt = promptGo.AddComponent<TextMesh>();
            m_Prompt.text = "对齐半透明火机，右手捏合并保持约1秒";
            m_Prompt.fontSize = 32;
            m_Prompt.characterSize = 0.008f;
            m_Prompt.anchor = TextAnchor.MiddleCenter;
            m_Prompt.alignment = TextAlignment.Center;
            m_Prompt.color = new Color(0.85f, 0.95f, 1f, 0.95f);

            Debug.Log("[MagicMR] Ghost lighter wireframe spawned at desk preset.", this);
        }

        void CacheAndDimLighter()
        {
            if (m_Lighter == null)
                return;

            m_LighterRenderers = m_Lighter.GetComponentsInChildren<Renderer>(true);
            m_LighterRendererWasEnabled = new bool[m_LighterRenderers.Length];
            for (var i = 0; i < m_LighterRenderers.Length; i++)
            {
                if (m_LighterRenderers[i] == null)
                    continue;
                m_LighterRendererWasEnabled[i] = m_LighterRenderers[i].enabled;
                m_LighterRenderers[i].enabled = false;
            }

            m_Lighter.gameObject.SetActive(true);
        }

        void RestoreLighterRenderers()
        {
            if (m_LighterRenderers == null)
                return;

            for (var i = 0; i < m_LighterRenderers.Length; i++)
            {
                if (m_LighterRenderers[i] == null)
                    continue;
                m_LighterRenderers[i].enabled =
                    m_LighterRendererWasEnabled != null && i < m_LighterRendererWasEnabled.Length
                        ? m_LighterRendererWasEnabled[i]
                        : true;
            }
        }

        void OnCalibrated()
        {
            if (m_Completed)
                return;

            m_Completed = true;
            RestoreLighterRenderers();

            if (m_Audio != null)
                m_Audio.PlayOneShot(MagicMRAudioFactory.Ding, 0.9f);

            if (m_Lighter != null)
            {
                m_Ring = MagicMRVfxFactory.CreateRingBurst(m_Lighter);
                m_Ring.Play();
                Destroy(m_Ring.gameObject, 2f);
            }

            if (m_Ghost != null)
            {
                Destroy(m_Ghost);
                m_Ghost = null;
            }

            if (m_Prompt != null)
            {
                Destroy(m_Prompt.gameObject);
                m_Prompt = null;
            }

            Debug.Log("[MagicMR] Calibration ritual complete (ding + ring).", this);
        }

        public void RestartRitual()
        {
            m_Completed = false;

            if (m_Ghost != null)
                Destroy(m_Ghost);
            if (m_Prompt != null)
                Destroy(m_Prompt.gameObject);
            m_Ghost = null;
            m_Prompt = null;

            FindFirstObjectByType<PinchGestureDetector>()?.ResetForNewTrial();

            CapturePoseFromLighter();
            CacheAndDimLighter();
            SpawnGhostAndPrompt();
            Debug.Log("[MagicMR] Calibration ritual restarted (desk wireframe).", this);
        }

        static void SetMaterialTransparent(Material mat)
        {
            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend"))
                mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
    }
}
