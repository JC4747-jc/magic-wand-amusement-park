using UnityEngine;

namespace Perception
{
    // Translation only: preserve the registered size and all child animation offsets.
    public sealed class TabletopVisualFollow
    {
        public Vector3 Position { get; private set; }
        public bool AverageMeasurements { get; set; }
        public int SampleCount => sampleCount;
        const float WindowSeconds = .3f;
        readonly Vector3[] points = new Vector3[3];
        readonly float[] times = new float[3];
        int sampleCount;
        float lastObserved = float.NegativeInfinity, lastAccepted = float.NegativeInfinity;
        Vector3 candidate,candidateStep;
        float candidateAt;
        int confirmations;
        bool reacquiring;
        public void Reset(Vector3 position, bool requireConfirmation=false)
        {
            Position = position; confirmations = 0; candidateStep = Vector3.zero; reacquiring = requireConfirmation;
            sampleCount = 0; lastObserved = lastAccepted = float.NegativeInfinity;
        }
        public bool Observe(Vector3 point, float now)
            => Observe(point, now, now);

        // Capture time, not Unity Update time, identifies an independent measurement.
        public bool Observe(Vector3 point, float capturedAt, float now)
        {
            if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z)) return false;
            if (AverageMeasurements)
            {
                if (!float.IsFinite(capturedAt) || !float.IsFinite(now) || capturedAt <= lastObserved ||
                    now < capturedAt || now - capturedAt > WindowSeconds) return false;
                lastObserved = capturedAt;
                if (float.IsFinite(lastAccepted) && capturedAt - lastAccepted > WindowSeconds)
                { reacquiring = true; sampleCount = 0; }
                now = capturedAt;
            }
            float distance = Vector3.Distance(Position, point);
            if (!AverageMeasurements && !reacquiring && distance <= .003f) { confirmations = 0; return true; }
            // Require repeated evidence for larger moves; one bad depth must not teleport the object.
            if (reacquiring || distance > .06f)
            {
                float dt=now-candidateAt;
                Vector3 step=point-candidate;
                bool clustered=step.magnitude<=(reacquiring?.012f:.025f);
                bool moving=dt>0&&dt<=.4f&&step.magnitude<=Mathf.Min(.16f,.015f+dt*1.2f)&&
                    (candidateStep.magnitude<.012f||Vector3.Dot(candidateStep.normalized,step.normalized)>.4f);
                if (confirmations == 0 || dt<0 || dt > .75f || (!clustered&&!moving))
                { confirmations = 1;candidateStep=Vector3.zero; }
                else {confirmations++;candidateStep=step;}
                // Compare adjacent captures, not every moving point to the first one.
                candidate=point;
                candidateAt = now;
                if (confirmations < 3) return false;
                // Confirmed relocation starts a fresh window; old positions must not pull it back.
                sampleCount = 0;
            }
            if (AverageMeasurements)
            {
                int keep = 0;
                for (int i = 0; i < sampleCount; i++)
                    if (now - times[i] <= WindowSeconds)
                    { points[keep] = points[i]; times[keep++] = times[i]; }
                sampleCount = keep;
                if (sampleCount == 3)
                {
                    points[0] = points[1]; points[1] = points[2];
                    times[0] = times[1]; times[1] = times[2]; sampleCount = 2;
                }
                points[sampleCount] = point; times[sampleCount++] = now;
                Vector3 sum = Vector3.zero; float total = 0;
                for (int i = 0; i < sampleCount; i++)
                {
                    int rank = i + 3 - sampleCount;
                    float weight = rank == 0 ? .2f : rank == 1 ? .3f : .5f;
                    sum += points[i] * weight; total += weight;
                }
                point = sum / total;
                lastAccepted = now;
            }
            if (!AverageMeasurements || reacquiring || distance > .06f || Vector3.Distance(Position, point) > .003f)
                Position = point;
            confirmations = 0; reacquiring=false; return true;
        }
    }
}
