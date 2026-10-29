using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// A small sound-making kit: noise, filters, envelopes and a plucked string, rendered into sample buffers that
    /// become AudioClips. Every sound in the game is made here, so there are no audio files to ship or license; a
    /// recorded clip can replace any of them later without touching the code that plays it.
    /// Pure arithmetic, so buffers can be rendered off the main thread; only <see cref="Clip"/> must run on it.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 44100;

        public static float[] Buffer(float seconds, int rate = Rate) => new float[Mathf.CeilToInt(seconds * rate)];

        /// <summary>Repeatable white noise (xorshift), so every device makes the same sounds.</summary>
        public sealed class Noise
        {
            uint state;

            public Noise(uint seed) => state = seed == 0 ? 0x9E3779B9u : seed;

            public float White()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state / 2147483648f - 1f;
            }
        }

        /// <summary>A second-order filter (the usual audio-cookbook biquads).</summary>
        public sealed class Biquad
        {
            readonly float b0, b1, b2, a1, a2;
            float x1, x2, y1, y2;

            Biquad(float b0, float b1, float b2, float a0, float a1, float a2)
            {
                this.b0 = b0 / a0;
                this.b1 = b1 / a0;
                this.b2 = b2 / a0;
                this.a1 = a1 / a0;
                this.a2 = a2 / a0;
            }

            public static Biquad LowPass(float hz, float q = 0.707f, int rate = Rate)
            {
                Coefficients(hz, q, rate, out var cos, out var alpha);
                return new Biquad((1f - cos) / 2f, 1f - cos, (1f - cos) / 2f, 1f + alpha, -2f * cos, 1f - alpha);
            }

            public static Biquad HighPass(float hz, float q = 0.707f, int rate = Rate)
            {
                Coefficients(hz, q, rate, out var cos, out var alpha);
                return new Biquad((1f + cos) / 2f, -(1f + cos), (1f + cos) / 2f, 1f + alpha, -2f * cos, 1f - alpha);
            }

            public static Biquad BandPass(float hz, float q = 1f, int rate = Rate)
            {
                Coefficients(hz, q, rate, out var cos, out var alpha);
                return new Biquad(alpha, 0f, -alpha, 1f + alpha, -2f * cos, 1f - alpha);
            }

            static void Coefficients(float hz, float q, int rate, out float cos, out float alpha)
            {
                var w = 2f * Mathf.PI * Mathf.Clamp(hz, 10f, rate * 0.45f) / rate;
                cos = Mathf.Cos(w);
                alpha = Mathf.Sin(w) / (2f * q);
            }

            public float Process(float x)
            {
                var y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = x;
                y2 = y1;
                y1 = y;
                return y;
            }
        }

        /// <summary>Rises over <paramref name="attack"/> seconds, then dies away with a time constant of <paramref name="decay"/>.</summary>
        public static float Envelope(float t, float attack, float decay) =>
            t < 0f ? 0f : t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);

        /// <summary>Adds a sine sliding from one pitch to another, shaped by an envelope.</summary>
        public static void AddSweep(float[] into, float start, float from, float to, float sweep, float attack, float decay, float level, int rate = Rate)
        {
            var phase = 0f;
            var first = Mathf.Max(0, (int)(start * rate));
            for (var i = first; i < into.Length; i++)
            {
                var t = (i - first) / (float)rate;
                var hz = Mathf.Lerp(from, to, Mathf.Clamp01(t / Mathf.Max(1e-4f, sweep)));
                phase += 2f * Mathf.PI * hz / rate;
                var e = Envelope(t, attack, decay);
                if (t > attack && e < 1e-4f) break;
                into[i] += Mathf.Sin(phase) * e * level;
            }
        }

        /// <summary>Adds a bell: a tone with the slightly out-of-tune overtones that make metal ring.</summary>
        public static void AddBell(float[] into, float start, float hz, float decay, float level, int rate = Rate)
        {
            AddSweep(into, start, hz, hz, 0f, 0.003f, decay, level, rate);
            AddSweep(into, start, hz * 2.76f, hz * 2.76f, 0f, 0.002f, decay * 0.45f, level * 0.35f, rate);
            AddSweep(into, start, hz * 5.4f, hz * 5.4f, 0f, 0.001f, decay * 0.2f, level * 0.15f, rate);
        }

        /// <summary>Adds filtered noise shaped by an envelope: splashes, whooshes, hats.</summary>
        public static void AddNoise(float[] into, float start, Biquad filter, float attack, float decay, float level, Noise noise, int rate = Rate)
        {
            var first = Mathf.Max(0, (int)(start * rate));
            for (var i = first; i < into.Length; i++)
            {
                var t = (i - first) / (float)rate;
                var e = Envelope(t, attack, decay);
                if (t > attack && e < 1e-4f) break;
                into[i] += filter.Process(noise.White()) * e * level;
            }
        }

        /// <summary>
        /// Adds a plucked string (Karplus-Strong): a burst of noise circulating in a delay line one period long, softened
        /// a little on every pass. Sounds like a ukulele or a harp, for almost nothing.
        /// </summary>
        public static void AddPluck(float[] into, float start, float hz, float seconds, float level, Noise noise, float damping = 0.996f, int rate = Rate)
        {
            var period = Mathf.Max(2, Mathf.RoundToInt(rate / hz));
            var line = new float[period];
            for (var i = 0; i < period; i++) line[i] = noise.White();
            var first = Mathf.Max(0, (int)(start * rate));
            var count = Mathf.Min(into.Length - first, (int)(seconds * rate));
            var index = 0;
            for (var i = 0; i < count; i++)
            {
                var next = (index + 1) % period;
                var sample = line[index];
                line[index] = damping * 0.5f * (line[index] + line[next]);
                index = next;
                var fade = i > count - 400 ? (count - i) / 400f : 1f; // no click if it's cut short
                into[first + i] += sample * level * fade;
            }
        }

        /// <summary>Scales so the loudest sample reaches <paramref name="peak"/>.</summary>
        public static float[] Normalise(float[] samples, float peak = 0.9f)
        {
            var max = 1e-6f;
            foreach (var s in samples) max = Mathf.Max(max, Mathf.Abs(s));
            var gain = peak / max;
            for (var i = 0; i < samples.Length; i++) samples[i] *= gain;
            return samples;
        }

        /// <summary>
        /// Makes a buffer loop without a click: its last <paramref name="fadeSeconds"/> are faded into its start
        /// (equal power), and the buffer is shortened by that much.
        /// </summary>
        public static float[] Loop(float[] samples, float fadeSeconds, int rate = Rate)
        {
            var fade = Mathf.Min(samples.Length / 2, (int)(fadeSeconds * rate));
            var length = samples.Length - fade;
            var looped = new float[length];
            System.Array.Copy(samples, looped, length);
            for (var i = 0; i < fade; i++)
            {
                var t = i / (float)fade;
                looped[i] = samples[i] * Mathf.Sin(t * Mathf.PI * 0.5f) + samples[length + i] * Mathf.Cos(t * Mathf.PI * 0.5f);
            }

            return looped;
        }

        /// <summary>The buffer as a mono AudioClip. Main thread only.</summary>
        public static AudioClip Clip(string name, float[] samples, int rate = Rate)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);
    }
}
