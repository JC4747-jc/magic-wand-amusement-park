#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MagicMR.Editor
{
    public static class MagicMRContentSetup
    {
        [MenuItem("Magic Wand/Setup MagicMR VFX Assets")]
        public static void SetupVfxAssets()
        {
            var lighter = GameObject.Find("Lighter");
            if (lighter == null)
            {
                Debug.LogError("[MagicMR] Lighter not found in open scene.");
                return;
            }

            var editor = lighter.GetComponent<RealityEditor>();
            if (editor == null)
            {
                Debug.LogError("[MagicMR] RealityEditor not found on Lighter.");
                return;
            }

            var burnClip = AssetDatabase.LoadAssetAtPath<AudioClip>(StudySpec.BurnSfxPath);
            var flower = AssetDatabase.LoadAssetAtPath<GameObject>(StudySpec.FlowerPrefabPath);
            var confetti = AssetDatabase.LoadAssetAtPath<GameObject>(StudySpec.ConfettiVfxPrefabPath);
            var vfx = confetti != null ? confetti.GetComponent<ParticleSystem>() : null;

            var audio = lighter.GetComponent<AudioSource>();
            if (audio == null)
                audio = lighter.AddComponent<AudioSource>();

            var eyes = lighter.transform.Find("AgencyEyes");
            if (eyes == null)
                Debug.LogWarning("[MagicMR] AgencyEyes child missing — open MagicMR.unity after pulling latest scene.");

            var so = new SerializedObject(editor);
            so.FindProperty("m_AudioSource").objectReferenceValue = audio;
            so.FindProperty("m_BurnSfx").objectReferenceValue = burnClip;
            so.FindProperty("m_FlowerPrefab").objectReferenceValue = flower;
            so.FindProperty("m_DeconstructionVfx").objectReferenceValue = vfx;
            if (eyes != null)
                so.FindProperty("m_EyesObject").objectReferenceValue = eyes.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            ApplyStudySpecToGestureDetectors();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[MagicMR] VFX assets and StudySpec parameters wired on Lighter.");
        }

        [MenuItem("Magic Wand/Apply MagicMR StudySpec Parameters")]
        public static void ApplyStudySpecParameters()
        {
            ApplyStudySpecToGestureDetectors();
            ApplyStudySpecToBootstrap();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[MagicMR] StudySpec parameters applied to open scene.");
        }

        static void ApplyStudySpecToGestureDetectors()
        {
            var root = GameObject.Find("GestureDetectors");
            if (root == null)
                return;

            var pinch = root.GetComponent<PinchGestureDetector>();
            if (pinch != null)
            {
                var so = new SerializedObject(pinch);
                so.FindProperty("m_PinchDistanceThreshold").floatValue = StudySpec.PinchDistanceThreshold;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var swipe = root.GetComponent<SwipeGestureDetector>();
            if (swipe != null)
            {
                var so = new SerializedObject(swipe);
                so.FindProperty("m_SwipeSpeedThreshold").floatValue = StudySpec.SwipeSpeedThreshold;
                so.FindProperty("m_CooldownSeconds").floatValue = StudySpec.SwipeCooldownSeconds;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var circle = root.GetComponent<CircleGestureDetector>();
            if (circle != null)
            {
                var so = new SerializedObject(circle);
                so.FindProperty("m_WindowSeconds").floatValue = StudySpec.CircleWindowSeconds;
                so.FindProperty("m_MinPathLength").floatValue = StudySpec.CircleMinPathLength;
                so.FindProperty("m_MaxAspectRatio").floatValue = StudySpec.CircleMaxAspectRatio;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var snap = root.GetComponent<SnapGestureDetector>();
            if (snap != null)
            {
                var so = new SerializedObject(snap);
                so.FindProperty("m_CloseDistanceThreshold").floatValue = StudySpec.SnapCloseDistanceThreshold;
                so.FindProperty("m_OpenDistanceThreshold").floatValue = StudySpec.SnapOpenDistanceThreshold;
                so.FindProperty("m_MinCloseSpeed").floatValue = StudySpec.SnapMinCloseSpeed;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void ApplyStudySpecToBootstrap()
        {
            var bootstrap = Object.FindFirstObjectByType<MagicMRStudyBootstrap>();
            if (bootstrap == null)
                return;

            var so = new SerializedObject(bootstrap);
            so.FindProperty("m_LighterDistance").floatValue = StudySpec.LighterDistance;
            so.FindProperty("m_LighterHeightOffset").floatValue = StudySpec.LighterHeightOffset;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
