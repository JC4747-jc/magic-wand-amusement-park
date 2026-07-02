namespace MagicMR
{
    /// <summary>
    /// Tender / study technical parameters (应标参数). Single source of truth for
    /// gesture thresholds, reality-editing behavior, logging, and content paths.
    /// </summary>
    public static class StudySpec
    {
        // Gesture recognition
        public const float PinchDistanceThreshold = 0.02f;
        public const float SwipeSpeedThreshold = 1.5f;
        public const float SwipeCooldownSeconds = 0.5f;
        public const float CircleWindowSeconds = 1.5f;
        public const float CircleMinPathLength = 0.25f;
        public const float CircleMaxAspectRatio = 1.5f;
        // Loosened from 0.03/0.08/2.0: PICO hand tracking's sampling rate makes a
        // real fast "snap" hard to catch at the tighter thresholds in practice.
        public const float SnapCloseDistanceThreshold = 0.045f;
        public const float SnapOpenDistanceThreshold = 0.07f;
        public const float SnapMinCloseSpeed = 1f;

        // Reality editing (RealityEditor)
        public const float EvadeDistance = 0.3f;
        public const float EvadeImpulse = 5f;
        public const bool ConstrainEvadeToXZ = true;
        public const float DeconstructionDelay = 0.5f;
        public const float IdlePulseSpeed = 2f;
        public const float IdlePulseScale = 0.05f;
        public const string AliveBoolParameter = "IsAlive";

        // Study session / logging
        public const float GlobalCooldownSeconds = 0.75f;
        public const float TrajectorySampleInterval = 0.1f;
        public const bool LogHandTrajectory = true;
        public const string DefaultSubjectId = "S01";
        public const string DefaultCondition = "All";
        public const int DefaultTrialId = 1;

        // MR placement
        public const float LighterDistance = 0.75f;
        public const float LighterHeightOffset = -0.15f;

        // Content paths (Assets/MagicMR bundle)
        public const string BurnSfxPath = "Assets/MagicMR/Audio/BurnSizzle.wav";
        public const string FlowerPrefabPath = "Assets/MagicMR/Prefabs/Flower.prefab";
        public const string BurntMaterialPath = "Assets/Models/Materials/burnt.mat";
        public const string LighterDefaultMaterialPath = "Assets/MagicMR/Materials/LighterDefault.mat";
        public const string ConfettiVfxPrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.0.5/Starter Assets/DemoSceneAssets/Prefabs/Interactables/Confetti.prefab";
    }
}
