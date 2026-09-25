using UnityEngine;

namespace MagicMR
{
    // A stationary thumbs-up summons once; opening the hand separates it from editing gestures.
    public sealed class TabletopSummonGesture
    {
        const float HoldSeconds = .3f;
        float began = -1, lastSample = -100, releasedAt = -1;
        Vector3 origin;
        public float Progress { get; private set; }
        public bool AwaitingRelease { get; private set; }
        public void CancelDwell() { began = -1; Progress = 0; lastSample = -100; releasedAt = -1; }
        public void Reset() { CancelDwell(); AwaitingRelease = false; }
        public static bool IsThumbsUp(float index, float middle, float ring, float little,
            Vector3 thumbBase, Vector3 thumbJoint, Vector3 thumbTip, Vector3 indexTip)
        {
            if (!(index < .85f && middle < .85f && ring < .85f && little < .85f) ||
                index <= 0 || middle <= 0 || ring <= 0 || little <= 0) return false;
            Vector3 first = thumbJoint - thumbBase, last = thumbTip - thumbJoint;
            Vector3 direction = thumbTip - thumbBase;
            if (!float.IsFinite(direction.sqrMagnitude) || !float.IsFinite(first.sqrMagnitude) ||
                !float.IsFinite(last.sqrMagnitude) || !float.IsFinite(indexTip.sqrMagnitude) ||
                first.magnitude < .005f || last.magnitude < .005f || direction.magnitude < .025f) return false;
            return Vector3.Dot(first.normalized, last.normalized) > .55f &&
                Vector3.Dot(direction.normalized, Vector3.up) > .35f &&
                Vector3.Distance(thumbTip, indexTip) >= .03f;
        }
        public bool Step(bool eligible, bool thumbsUp, Vector3 palm, float now)
        {
            if (AwaitingRelease) return false;
            if (!eligible || !thumbsUp || !float.IsFinite(palm.x) || !float.IsFinite(palm.y) || !float.IsFinite(palm.z))
            { CancelDwell(); return false; }
            if (began < 0 || now - lastSample > .15f || now < lastSample || Vector3.Distance(origin, palm) > .04f)
            { began = now; origin = palm; }
            lastSample = now;
            Progress = Mathf.Clamp01((now - began) / HoldSeconds);
            if (now - began < HoldSeconds) return false;
            AwaitingRelease = true; releasedAt = -1;
            return true;
        }
        public bool BlockTransforms(bool waitingForOpenHand, float now)
        {
            if (!AwaitingRelease) return false;
            if (waitingForOpenHand || now - lastSample > .15f || now < lastSample) releasedAt = -1;
            lastSample = now;
            if (!waitingForOpenHand)
            {
                if (releasedAt < 0) releasedAt = now;
                if (now - releasedAt >= .15f) AwaitingRelease = false;
            }
            return true; // Never feed the release sample into transformation recognition.
        }
    }
}
