using UnityEngine;

namespace Perception
{
    // Only acquisition uses vision. Once locked, occlusion cannot change the target.
    public sealed class TabletopTargetLock
    {
        public bool IsLocked { get; private set; }
        public Vector3 Position { get; private set; }
        public int SampleCount => count;
        int count;
        float startedAt, lastAt;
        Vector3 first;

        public bool Observe(Vector3 point, float now)
        {
            if (IsLocked) return true;
            if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z)) return false;
            if (count == 0 || now - lastAt > 0.8f || Vector3.Distance(point, first) > 0.04f)
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
