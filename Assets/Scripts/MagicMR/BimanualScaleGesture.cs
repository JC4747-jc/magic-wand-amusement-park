using UnityEngine;

namespace MagicMR
{
    // Own both open palms before the right-hand recognizer can emit a swipe.
    public sealed class BimanualScaleGesture
    {
        public bool OwnsGesture { get; private set; }
        Vector3 startLeft, startRight, axis;
        float began, lastSample = -100, lastFire = -100;
        bool waitingForRearm;

        public void Reset()
        {
            OwnsGesture = waitingForRearm = false;
            lastSample = lastFire = -100;
        }

        public bool Step(bool bothOpen, bool canStart, Vector3 left, Vector3 right, Vector3 horizontalAxis, float now)
        {
            bool finite = Finite(left) && Finite(right) && float.IsFinite(now);
            if (!bothOpen || !finite || now < lastSample || now - lastSample > .15f)
            { OwnsGesture = waitingForRearm = false; }
            lastSample = now;
            if (!bothOpen || !finite) return false;
            float separation = Vector3.Distance(left, right);
            if (!OwnsGesture)
            {
                if (!canStart || !Finite(horizontalAxis) || horizontalAxis.sqrMagnitude < .1f) return false;
                OwnsGesture = true; Arm(left, right, horizontalAxis.normalized, now);
                return false;
            }
            if (waitingForRearm)
            {
                if (separation <= Vector3.Distance(startLeft, startRight) + .02f)
                { waitingForRearm = false; Arm(left, right, axis, now); }
                return false;
            }
            if (now - began > StudySpec.ScaleGestureMaxSeconds)
            {
                OwnsGesture = false;
                return false;
            }
            if (separation <= StudySpec.ScaleGestureRearmMeters)
            { Arm(left, right, axis, now); return false; }
            Vector3 ld = left - startLeft, rd = right - startRight;
            // Both hands must move outward; a one-handed swipe or vertical lift cannot scale.
            if (now - began < .12f || now - lastFire < .8f ||
                Vector3.Dot(ld, axis) > -.035f || Vector3.Dot(rd, axis) < .035f ||
                Mathf.Abs(Vector3.Dot(ld, axis)) < ld.magnitude * .75f ||
                Mathf.Abs(Vector3.Dot(rd, axis)) < rd.magnitude * .75f ||
                separation - Vector3.Distance(startLeft, startRight) < StudySpec.ScaleGesturePullMeters) return false;
            lastFire = now; waitingForRearm = true;
            return true;
        }

        void Arm(Vector3 left, Vector3 right, Vector3 horizontalAxis, float now)
        { startLeft = left; startRight = right; axis = horizontalAxis; began = now; }
        static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
    }
}
