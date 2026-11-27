using System;
using UnityEngine;

namespace Espectros
{
    // Efectos de sonido generados por código (sin archivos de audio: no hay que gestionar licencias).
    // Si un GhostType tiene sus propios AudioClip, se usan esos en lugar de estos.
    public static class SfxLibrary
    {
        const int SampleRate = 44100;

        static AudioClip moan, appear, scare, die, bounce, star, sonar, click, hurt, throwClip, hit;

        public static AudioClip GhostMoan => moan != null ? moan : moan = Make("Sfx_Moan", 2.4f, Moan);
        public static AudioClip GhostAppear => appear != null ? appear : appear = Make("Sfx_Appear", 0.7f, Appear);
        public static AudioClip GhostScare => scare != null ? scare : scare = Make("Sfx_Scare", 0.8f, Scare);
        public static AudioClip GhostDie => die != null ? die : die = Make("Sfx_Die", 0.6f, Die);
        public static AudioClip GhostHit => hit != null ? hit : hit = Make("Sfx_Hit", 0.25f, Hit);
        public static AudioClip Bounce => bounce != null ? bounce : bounce = Make("Sfx_Bounce", 0.09f, BounceF);
        public static AudioClip Star => star != null ? star : star = Make("Sfx_Star", 0.5f, StarF);
        public static AudioClip Sonar => sonar != null ? sonar : sonar = Make("Sfx_Sonar", 1.2f, SonarF);
        public static AudioClip Click => click != null ? click : click = Make("Sfx_Click", 0.04f, ClickF);
        public static AudioClip Hurt => hurt != null ? hurt : hurt = Make("Sfx_Hurt", 0.5f, HurtF);
        public static AudioClip Throw => throwClip != null ? throwClip : throwClip = Make("Sfx_Throw", 0.25f, ThrowF);

        static readonly System.Random rng = new System.Random(1234);
        static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

        static AudioClip Make(string name, float seconds, Func<float, float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                data[i] = Mathf.Clamp(f(t, t / seconds), -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Sin(float freq, float t) => Mathf.Sin(2f * Mathf.PI * freq * t);
        static float Env(float u, float attack, float release) =>
            Mathf.Clamp01(u / Mathf.Max(attack, 1e-4f)) * Mathf.Clamp01((1f - u) / Mathf.Max(release, 1e-4f));

        // Lamento grave con vibrato (en bucle: empieza y acaba en silencio)
        static float lp;
        static float Moan(float t, float u)
        {
            float vib = 1f + 0.03f * Mathf.Sin(2f * Mathf.PI * 5f * t);
            float f = 150f + 40f * Mathf.Sin(2f * Mathf.PI * 0.4f * t);
            lp += (Noise() - lp) * 0.05f; // ruido filtrado: aire
            float s = Sin(f * vib, t) * 0.5f + Sin(f * 2f * vib, t) * 0.2f + lp * 0.6f;
            return s * Mathf.Sin(Mathf.PI * u) * 0.6f;
        }

        // Soplido que sube
        static float Appear(float t, float u)
        {
            lp += (Noise() - lp) * (0.02f + 0.2f * u);
            return (lp * 1.5f + Sin(200f + 600f * u * u, t) * 0.2f) * Env(u, 0.2f, 0.3f);
        }

        // Grito que baja
        static float Scare(float t, float u)
        {
            float f = 900f - 600f * u;
            float saw = 2f * ((f * t) % 1f) - 1f;
            return (saw * 0.5f + Noise() * 0.25f) * Env(u, 0.03f, 0.4f);
        }

        // Chapoteo: ruido que cae y "bloop" grave
        static float Die(float t, float u)
        {
            float bloop = Sin(300f - 220f * u, t) * Mathf.Exp(-6f * u);
            return (Noise() * Mathf.Exp(-10f * u) * 0.6f + bloop * 0.7f) * Env(u, 0.01f, 0.2f);
        }

        static float Hit(float t, float u) => (Sin(520f, t) * 0.5f + Noise() * 0.3f) * Mathf.Exp(-12f * u);
        static float BounceF(float t, float u) => (Sin(180f - 60f * u, t) * 0.8f + Noise() * 0.2f) * Mathf.Exp(-8f * u);
        static float StarF(float t, float u) => (Sin(u < 0.4f ? 880f : 1320f, t) * 0.5f + Sin(2640f, t) * 0.1f) * Env(u, 0.02f, 0.6f);
        static float SonarF(float t, float u) => Sin(1200f, t) * (Mathf.Exp(-5f * u) + 0.35f * Mathf.Exp(-5f * Mathf.Max(0f, u - 0.3f)) * (u > 0.3f ? 1f : 0f)) * 0.6f;
        static float ClickF(float t, float u) => Noise() * (1f - u) * 0.6f;
        static float HurtF(float t, float u) => (Sin(90f, t) * 0.8f + Noise() * 0.3f) * Mathf.Exp(-5f * u);
        static float ThrowF(float t, float u)
        {
            lp += (Noise() - lp) * 0.15f;
            return lp * 1.5f * Mathf.Sin(Mathf.PI * u);
        }
    }
}
