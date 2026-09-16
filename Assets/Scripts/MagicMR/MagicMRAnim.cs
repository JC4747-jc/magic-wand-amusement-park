using UnityEngine;

namespace MagicMR
{
    /// <summary>
    /// Frame-rate independent easing for 4D effect motion. No gameplay policy.
    /// </summary>
    public static class MagicMRAnim
    {
        public static float Saturate(float t) => t < 0f ? 0f : (t > 1f ? 1f : t);

        public static float SmoothStep(float t)
        {
            t = Saturate(t);
            return t * t * (3f - 2f * t);
        }

        public static float EaseOutCubic(float t)
        {
            t = Saturate(t);
            var inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        public static float EaseInCubic(float t)
        {
            t = Saturate(t);
            return t * t * t;
        }

        public static float EaseOutBack(float t)
        {
            t = Saturate(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var inv = t - 1f;
            return 1f + c3 * inv * inv * inv + c1 * inv * inv;
        }

        /// <summary>Exponential lerp factor: 1 - e^{-hz * dt}.</summary>
        public static float ExpLerp(float hz, float dt)
        {
            if (dt <= 0f || hz <= 0f)
                return 1f;
            return 1f - Mathf.Exp(-hz * dt);
        }
    }
}
