namespace MagicMR
{
    // Pure temporal state; the PICO bridge still owns the actual joint/pose classification.
    public sealed class HciFistOpenWindow
    {
        public const float MaximumSampleGapSeconds = .15f;
        public const float RepeatCooldownSeconds = .65f;
        float began, lastSample = float.NaN, lastFire = float.NegativeInfinity;
        public bool InFist { get; private set; }
        public float HeldSeconds(float now) => InFist ? now - began : 0;

        // Losing tracking cancels the pending action without bypassing an existing cooldown.
        public void ResetTracking() { InFist = false; began = 0; lastSample = float.NaN; }

        public bool Step(float now, bool tracked, bool fist, bool open, float minimumHold, float maximumBurst)
        {
            if (!tracked || !float.IsFinite(now) || !float.IsFinite(minimumHold) ||
                !float.IsFinite(maximumBurst) || minimumHold < 0 || maximumBurst < minimumHold ||
                (!float.IsNaN(lastSample) && now < lastSample))
            { ResetTracking(); return false; }
            if (!float.IsNaN(lastSample) && now - lastSample > MaximumSampleGapSeconds) ResetTracking();
            lastSample = now;
            if (fist)
            {
                if (!InFist) { InFist = true; began = now; }
                return false;
            }
            if (!InFist) return false;
            float held = now - began;
            if (held > maximumBurst) { InFist = false; return false; }
            if (!open) return false;
            InFist = false;
            if (held < minimumHold || now - lastFire <= RepeatCooldownSeconds) return false;
            lastFire = now;
            return true;
        }
    }
}
