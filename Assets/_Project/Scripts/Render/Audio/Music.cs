using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// Two loops, written as notes and played by <see cref="Synth"/>'s plucked strings: a gentle one for the menus and
    /// a bouncier one with drums for racing. Both are eight bars in C (C, G, A minor, F) so they share a mood. Rendered at
    /// a lower sample rate, off the main thread, while the title shows.
    /// </summary>
    public static class Music
    {
        public const int Rate = 22050;

        // One bar per chord: root (for the bass), and eight eighth-note melody slots (0 is a rest).
        static readonly int[] Roots = { 48, 43, 45, 41, 48, 43, 41, 43 };

        static readonly int[][] Chords =
        {
            new[] { 60, 64, 67 }, new[] { 55, 59, 62 }, new[] { 57, 60, 64 }, new[] { 53, 57, 60 },
            new[] { 60, 64, 67 }, new[] { 55, 59, 62 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 },
        };

        static readonly int[][] Tune =
        {
            new[] { 76, 0, 79, 0, 81, 79, 76, 0 }, new[] { 74, 0, 79, 0, 74, 76, 74, 0 },
            new[] { 72, 0, 76, 0, 81, 0, 79, 76 }, new[] { 69, 0, 72, 0, 74, 72, 69, 0 },
            new[] { 76, 79, 81, 0, 79, 76, 74, 0 }, new[] { 74, 0, 76, 79, 0, 76, 74, 0 },
            new[] { 72, 0, 69, 72, 74, 0, 76, 0 }, new[] { 74, 0, 0, 0, 67, 0, 0, 0 },
        };

        /// <summary>The race loop: 132 beats a minute, plucked tune, pumping bass, kick, snare and hats.</summary>
        public static float[] Race()
        {
            const float bpm = 132f;
            var beat = 60f / bpm;
            var eighth = beat / 2f;
            var bar = beat * 4f;
            var buffer = new float[Mathf.CeilToInt(bar * Roots.Length * Rate)];
            var noise = new Synth.Noise(7);

            for (var b = 0; b < Roots.Length; b++)
            {
                var start = b * bar;
                for (var e = 0; e < 8; e++)
                {
                    var t = start + e * eighth;
                    Synth.AddPluck(buffer, t, Synth.Midi(Roots[b]), eighth * 0.9f, 0.55f, noise, 0.99f, Rate); // pumping bass
                    if (Tune[b][e] > 0) Synth.AddPluck(buffer, t, Synth.Midi(Tune[b][e]), eighth * 2.5f, 0.42f, noise, 0.996f, Rate);
                    Hat(buffer, t, e % 2 == 0 ? 0.12f : 0.07f, noise);
                }

                for (var q = 0; q < 4; q++)
                {
                    var t = start + q * beat;
                    if (q % 2 == 0) Kick(buffer, t);
                    else Snare(buffer, t, noise);
                }

                foreach (var note in Chords[b]) Synth.AddPluck(buffer, start + beat * 1.5f, Synth.Midi(note), beat, 0.16f, noise, 0.995f, Rate);
            }

            return Synth.Normalise(buffer, 0.85f);
        }

        /// <summary>The menu loop: slower, no drums, the chords rolled gently under the tune.</summary>
        public static float[] Menu()
        {
            const float bpm = 96f;
            var beat = 60f / bpm;
            var eighth = beat / 2f;
            var bar = beat * 4f;
            var buffer = new float[Mathf.CeilToInt(bar * Roots.Length * Rate)];
            var noise = new Synth.Noise(11);

            for (var b = 0; b < Roots.Length; b++)
            {
                var start = b * bar;
                Synth.AddPluck(buffer, start, Synth.Midi(Roots[b]), bar, 0.5f, noise, 0.998f, Rate);
                Synth.AddPluck(buffer, start + beat * 2f, Synth.Midi(Roots[b] + 7), beat * 2f, 0.3f, noise, 0.998f, Rate);
                for (var e = 0; e < 8; e++)
                {
                    var chord = Chords[b];
                    Synth.AddPluck(buffer, start + e * eighth, Synth.Midi(chord[e % chord.Length] + (e >= 4 ? 12 : 0)), eighth * 2f, 0.16f, noise, 0.997f, Rate);
                    if (Tune[b][e] > 0 && e % 2 == 0) Synth.AddPluck(buffer, start + e * eighth, Synth.Midi(Tune[b][e]), beat * 1.5f, 0.32f, noise, 0.998f, Rate);
                }
            }

            return Synth.Normalise(buffer, 0.8f);
        }

        static void Kick(float[] buffer, float at) => Synth.AddSweep(buffer, at, 130f, 45f, 0.09f, 0.002f, 0.12f, 0.9f, Rate);

        static void Snare(float[] buffer, float at, Synth.Noise noise)
        {
            Synth.AddNoise(buffer, at, Synth.Biquad.BandPass(1900f, 0.7f, Rate), 0.001f, 0.07f, 0.55f, noise, Rate);
            Synth.AddSweep(buffer, at, 210f, 170f, 0.03f, 0.001f, 0.05f, 0.3f, Rate);
        }

        static void Hat(float[] buffer, float at, float level, Synth.Noise noise) =>
            Synth.AddNoise(buffer, at, Synth.Biquad.HighPass(6500f, 0.7f, Rate), 0.0005f, 0.02f, level, noise, Rate);
    }
}
