namespace MagicMR
{
    /// <summary>
    /// Tender / study technical parameters (应标参数). Single source of truth for
    /// gesture thresholds, reality-editing behavior, logging, and content paths.
    /// </summary>
    public static class StudySpec
    {
        // --- FSM / asymmetric bimanual policy ---
        public const float StartupCooldownSeconds = 3.0f;
        /// <summary>Coyote time: tracking / pinch dropout grace during calibration.</summary>
        public const float CoyoteTimeSeconds = 0.4f;
        public const float LeftResetHoldSeconds = 0.4f;
        public const float LeftPalmFacingDot = 0.6f;
        public const float RequireReleasePinchStrength = 0.2f;
        public const float FollowPinchStrengthMin = 0.9f;
        public const float CalibrationPinchStrengthMin = 0.45f;
        public const float CalibrationProximityMeters = 0.18f;
        public const float PinchOpenDistance = 0.12f;
        public const float PinchClosedDistance = 0.02f;
        public const float RuleDodgeMeters = 0.20f;

        // Gesture recognition
        // Appearance tap (empty-hand quick pinch) — early working threshold.
        public const float PinchDistanceThreshold = 0.055f;
        // Sustained pinch for calibration only (while uncalibrated).
        public const float HoldPinchDistanceThreshold = 0.12f;
        // Unused by restored tip-only hold, kept for StudySpec completeness.
        public const float GripPalmAverageDistance = 0.14f;
        public const float GripMaxThumbIndexForPalm = 0.22f;
        // Legacy alias — FSM uses CoyoteTimeSeconds (0.4s).
        public const float HoldDropoutGraceSeconds = CoyoteTimeSeconds;
        public const float SwipeSpeedThreshold = 0.85f;
        public const float SwipeCooldownSeconds = 0.5f;
        public const float SwipeMinHorizontalRatio = 0.65f;
        public const float CircleWindowSeconds = 2.5f;
        public const float CircleMinPathLength = 0.09f;
        public const float CircleMaxAspectRatio = 2.6f;
        public const float CircleMinAxisSpan = 0.025f;
        // Loosened further for PICO hand-tracking sampling / noisy tips.
        // Snap kept for reference only — Deconstruction now uses fist-burst.
        public const float SnapCloseDistanceThreshold = 0.055f;
        public const float SnapOpenDistanceThreshold = 0.08f;
        public const float SnapMinCloseSpeed = 0.55f;

        // Fist → open burst (Deconstruction). Tip-to-palm averages.
        public const float FistMaxTipDistance = 0.07f;
        public const float FistOpenMinTipDistance = 0.11f;
        public const float FistMinHoldSeconds = 0.08f;
        public const float FistMaxBurstSeconds = 0.3f;
        // Held (not tapped) pinch duration that triggers real-world anchor
        // calibration instead of the Appearance dimension.
        public const float PinchHoldSeconds = 1.0f;
        public const float AppearanceTapMaxSeconds = 0.5f;

        // Hybrid tracking (Pinned + FollowHandWhileGripping)
        // Keep attach tight so a casual pinch near the desk does not steal the target.
        public const float GripAttachDistance = 0.08f;
        public const float GripAttachDwellSeconds = 0.45f;
        public const float GripReleasePinDelaySeconds = 0.25f;

        // Reality editing (RealityEditor)
        public const float EvadeDistance = 0.3f;
        public const float EvadeImpulse = 5f;
        public const bool ConstrainEvadeToXZ = true;
        public const float DeconstructionDelay = 0.5f;
        public const float IdlePulseSpeed = 2.2f;
        public const float IdlePulseScale = 0.14f;
        public const string AliveBoolParameter = "IsAlive";

        // Study session / logging
        public const float GlobalCooldownSeconds = 0.5f;
        public const float TrajectorySampleInterval = 0.1f;
        public const bool LogHandTrajectory = true;
        public const string DefaultSubjectId = "S01";
        public const string DefaultCondition = "All";
        public const int DefaultTrialId = 1;

        // MR placement
        public const float LighterDistance = 0.55f;
        public const float LighterHeightOffset = -0.25f;

        // Content paths (Assets/MagicMR bundle)
        public const string BurnSfxPath = "Assets/MagicMR/Audio/BurnSizzle.wav";
        public const string FlowerPrefabPath = "Assets/MagicMR/Prefabs/Flower.prefab";
        public const string BurntMaterialPath = "Assets/Models/Materials/burnt.mat";
        public const string LighterDefaultMaterialPath = "Assets/MagicMR/Materials/LighterDefault.mat";
        public const string ConfettiVfxPrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.0.5/Starter Assets/DemoSceneAssets/Prefabs/Interactables/Confetti.prefab";
    }
}
