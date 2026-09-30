using UnityEngine;

namespace Perception
{
    // Position-only attachment to a confirmed left-hand grip. No rotation is inferred.
    public sealed class LeftGripFollow
    {
        public bool IsHolding { get; private set; }
        public Vector3 Position { get; private set; }
        float candidateAt = -1, lastValidAt = -100, openAt = -1;
        Vector3 candidatePalm, candidateTarget, offset, lastPalm;
        public void Reset() { IsHolding = false; candidateAt = openAt = -1; lastValidAt = -100; }
        public void Tick(float now)
        {
            if (now - lastValidAt > .35f) { IsHolding = false; candidateAt = openAt = -1; }
        }
        public void Observe(bool valid, bool closed, Vector3 palm, float nearestDistance, Vector3 target, float now)
        {
            Tick(now);
            if (!valid || !float.IsFinite(palm.sqrMagnitude) || !float.IsFinite(target.sqrMagnitude))
            { candidateAt = -1; return; }
            float gap = now - lastValidAt;
            if (IsHolding && Vector3.Distance(palm,lastPalm) > .10f + 3f * Mathf.Clamp(gap,0,.1f)) return;
            lastValidAt = now; lastPalm = palm;
            if (IsHolding)
            {
                if (!closed)
                {
                    if (openAt < 0) openAt = now;
                    if (now - openAt >= .18f) { IsHolding = false; candidateAt = -1; }
                    return; // Opening fingers must not drag a released lighter along with the palm.
                }
                openAt = -1; Position = palm + offset; return;
            }
            if (!closed || !float.IsFinite(nearestDistance) || nearestDistance > .10f || nearestDistance < 0)
            { candidateAt = -1; return; }
            if (candidateAt < 0 || gap > .15f || Vector3.Distance(palm,candidatePalm) > .08f)
            { candidateAt = now; candidatePalm = palm; candidateTarget = target; }
            if (now - candidateAt < .15f) return;
            offset = candidateTarget - candidatePalm;
            Position = palm + offset; IsHolding = true; openAt = -1;
        }
    }
}
