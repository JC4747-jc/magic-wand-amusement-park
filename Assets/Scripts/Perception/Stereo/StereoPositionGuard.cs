using UnityEngine;

namespace Perception
{
    // A single distant stereo mismatch must not teleport the virtual object.
    public sealed class StereoPositionGuard
    {
        Vector3 accepted, candidate;
        float candidateTime;
        int confirmations;
        bool initialized;

        public void Reset() { initialized = false; confirmations = 0; }

        public bool Accept(Vector3 point, float now)
        {
            if (!StereoFusionGeometry.Finite(point.x) || !StereoFusionGeometry.Finite(point.y) ||
                !StereoFusionGeometry.Finite(point.z)) return false;
            if (!initialized || Vector3.Distance(accepted, point) <= .15f)
            {
                initialized = true; accepted = point; confirmations = 0; return true;
            }
            if (confirmations == 0 || now - candidateTime > .75f || Vector3.Distance(candidate, point) > .04f)
            { candidate = point; confirmations = 1; }
            else confirmations++;
            candidateTime = now;
            if (confirmations < 3) return false;
            accepted = point; confirmations = 0; return true;
        }
    }
}
