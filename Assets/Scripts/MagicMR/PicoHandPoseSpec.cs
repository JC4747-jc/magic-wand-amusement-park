namespace MagicMR
{
    /// <summary>
    /// Inspector numbers for three PICO <c>PXR_HandPoseConfig</c> assets.
    /// Ranges copy PICO SDK <c>ShapesRecognizer</c> Open/Close constants.
    ///
    /// In Unity: PICO → Hand Pose Generator → Save Config, then paste these
    /// values. Circle / Swipe / Pinch-tap / FollowHand stay on the custom detectors.
    /// </summary>
    public static class PicoHandPoseSpec
    {
        public const bool Enabled = true;

        // Official PICO ShapesRecognizer curl / flexion windows (degrees).
        public const float CurlCloseMin = 0f;
        public const float CurlCloseMax = 73f;
        public const float CurlOpenMin = 107f;
        public const float CurlOpenMax = 180f;
        public const float CurlThumbCloseMin = 45f;
        public const float CurlThumbCloseMax = 90f;
        public const float CurlThumbOpenMin = 90f;
        public const float CurlThumbOpenMax = 180f;

        public const float FlexionCloseMin = 90f;
        public const float FlexionCloseMax = 126f;
        public const float FlexionOpenMin = 144f;
        public const float FlexionOpenMax = 180f;
        public const float FlexionThumbCloseMin = 90f;
        public const float FlexionThumbCloseMax = 120f;
        public const float FlexionThumbOpenMin = 155f;
        public const float FlexionThumbOpenMax = 180f;

        public const float ShapesHoldDuration = 0.09f;
        public const float BonesHoldDuration = 0.022f;
        public const float TransHoldDuration = 0.022f;

        // BonesRecognizer: Thumb_Tip ↔ Index_Tip. Loosened vs PICO default 0.025
        // so PICO tracking noise still matches (same as Appearance pinch).
        public const float LeftResetPinchDistance = StudySpec.PinchDistanceThreshold;
        public const float LeftResetPinchMargin = 0.01f;
        public const float TowardsFaceAngleThreshold = 35f;
        public const float TowardsFaceThresholdWidth = 10f;

        public const string FistConfigName = "HandPose_RightFist";
        public const string OpenConfigName = "HandPose_RightOpen";
        public const string LeftResetConfigName = "HandPose_LeftReset";
    }
}
