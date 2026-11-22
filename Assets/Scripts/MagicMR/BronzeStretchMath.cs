using System;

namespace MagicMR
{
    // Scalar part of the production rig solver; usable without loading Unity.
    public static class BronzeStretchMath
    {
        public static float Amount(float progress)
        {
            if (!float.IsFinite(progress) || progress <= .12f || progress >= .96f) return 0;
            float value = progress < .42f ? (progress - .12f) / .3f :
                progress <= .62f ? 1 : (.96f - progress) / .34f;
            return value * value * (3 - 2 * value);
        }

        public static bool Triangle(float distance, float upper, float lower, out float along, out float height)
        {
            along = height = 0;
            if (!float.IsFinite(distance) || !float.IsFinite(upper) || !float.IsFinite(lower) ||
                upper <= 0 || lower <= 0 || distance <= 1e-6f ||
                distance > upper + lower || distance < Math.Abs(upper - lower)) return false;
            double a = ((double)upper * upper - (double)lower * lower + (double)distance * distance) / (2 * distance);
            double square = (double)upper * upper - a * a;
            if (square < -1e-8) return false;
            along = (float)a;
            height = (float)Math.Sqrt(Math.Max(0, square));
            return true;
        }
    }
}
