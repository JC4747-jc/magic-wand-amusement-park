using UnityEngine;

namespace MagicMR
{
    /// <summary>
    /// Tiny procedural SFX library so study builds ship without external audio packs.
    /// </summary>
    public static class MagicMRAudioFactory
    {
        static AudioClip s_Ding;
        static AudioClip s_Whoosh;
        static AudioClip s_Growl;
        static AudioClip s_Shatter;
        static AudioClip s_ResetChime;

        public static AudioClip Ding => s_Ding ??= MakeTone(0.18f, 880f, 0.55f, fadeOut: true, harmonics: 2);
        public static AudioClip Whoosh => s_Whoosh ??= MakeNoiseSweep(0.22f, 0.35f);
        public static AudioClip Growl => s_Growl ??= MakeTone(0.35f, 90f, 0.4f, fadeOut: true, harmonics: 3, tremolo: 12f);
        public static AudioClip Shatter => s_Shatter ??= MakeNoiseBurst(0.28f, 0.5f);
        public static AudioClip ResetChime => s_ResetChime ??= MakeTone(0.25f, 660f, 0.45f, fadeOut: true, harmonics: 1);

        static AudioClip MakeTone(
            float duration,
            float frequency,
            float amplitude,
            bool fadeOut,
            int harmonics,
            float tremolo = 0f)
        {
            var sampleRate = 22050;
            var sampleCount = Mathf.CeilToInt(duration * sampleRate);
            var data = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleRate;
                var env = fadeOut ? 1f - t / duration : 1f;
                if (tremolo > 0f)
                    env *= 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * tremolo * t);

                var sample = 0f;
                for (var h = 1; h <= Mathf.Max(1, harmonics); h++)
                    sample += Mathf.Sin(2f * Mathf.PI * frequency * h * t) / h;

                data[i] = sample * amplitude * env / harmonics;
            }

            var clip = AudioClip.Create($"MagicMRTone_{frequency}", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeNoiseSweep(float duration, float amplitude)
        {
            var sampleRate = 22050;
            var sampleCount = Mathf.CeilToInt(duration * sampleRate);
            var data = new float[sampleCount];
            var rng = new System.Random(17);
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleCount;
                var noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                var tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(900f, 220f, t) * (i / (float)sampleRate));
                data[i] = (noise * 0.55f + tone * 0.45f) * amplitude * (1f - t);
            }

            var clip = AudioClip.Create("MagicMRWhoosh", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeNoiseBurst(float duration, float amplitude)
        {
            var sampleRate = 22050;
            var sampleCount = Mathf.CeilToInt(duration * sampleRate);
            var data = new float[sampleCount];
            var rng = new System.Random(42);
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleCount;
                var noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                var click = Mathf.Sin(2f * Mathf.PI * 1800f * (i / (float)sampleRate)) * Mathf.Exp(-t * 18f);
                data[i] = (noise * (1f - t) * 0.7f + click * 0.5f) * amplitude;
            }

            var clip = AudioClip.Create("MagicMRShatter", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
