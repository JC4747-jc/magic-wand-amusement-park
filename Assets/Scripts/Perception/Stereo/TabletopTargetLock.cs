using UnityEngine;

namespace Perception
{
    // Acquisition requires a stationary center and consistent full-object dimensions.
    public sealed class TabletopTargetLock
    {
        public bool IsLocked { get; private set; }
        public Vector3 Position { get; private set; }
        public int SampleCount => count;
        int count;
        float startedAt, lastAt;
        Vector3 first;
        float firstHeight, firstWidth, firstBottomOffset;

        public bool Observe(Vector3 point, float now, float height, float width, float bottomOffset)
        {
            if (!float.IsFinite(height) || !float.IsFinite(width) || !float.IsFinite(bottomOffset) ||
                height < .025f || height > .25f || width < .008f || width > .10f) return false;
            if (count > 0 && !IsLocked &&
                (Mathf.Abs(height - firstHeight) > firstHeight * .12f ||
                 Mathf.Abs(width - firstWidth) > firstWidth * .20f ||
                 Mathf.Abs(bottomOffset - firstBottomOffset) > .006f)) Reset();
            bool result = Observe(point, now);
            if (count == 1)
            { firstHeight = height; firstWidth = width; firstBottomOffset = bottomOffset; }
            return result;
        }

        public bool Observe(Vector3 point, float now)
        {
            if (IsLocked) return true;
            if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z) || !float.IsFinite(now)) return false;
            if (count > 0 && now <= lastAt) return false;
            if (count == 0 || now - lastAt > 0.8f || Vector3.Distance(point, first) > 0.012f)
            {
                count = 0; startedAt = now; first = point; Position = point;
            }
            lastAt = now;
            count++;
            Position = Vector3.Lerp(Position, point, 1f / count);
            IsLocked = count >= 5 && now - startedAt >= 0.6f;
            return IsLocked;
        }

        public void Reset() { IsLocked = false; count = 0; Position = default; }
    }
}
