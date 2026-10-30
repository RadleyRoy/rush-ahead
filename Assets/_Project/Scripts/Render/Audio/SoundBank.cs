using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// Every sound effect, made once at startup by <see cref="Synth"/>: a few hundred milliseconds of arithmetic, off
    /// the main thread, and nothing to download. Until they're ready every clip is null, which plays as silence. Loops (blowers, the river) are rendered long enough not to sound repetitive and faded into
    /// themselves so they never click.
    /// </summary>
    public sealed class SoundBank
    {
        public AudioClip Burst, Bump, Splash, Capsize, Fall, Beep, Go, Chime, Fanfare, Star, Jingle, Click;
        public AudioClip RiverCalm, RiverRapids, LakeLapping;
        public AudioClip LeafBlower, HairDryer, HandFan, ShopVac;

        /// <summary>
        /// Every sound's samples, by name. Pure arithmetic, so it runs off the main thread while the title shows;
        /// <see cref="From"/> then makes the clips.
        /// </summary>
        public static Dictionary<string, float[]> Render()
        {
            var noise = new Synth.Noise(20261029);
            var sounds = new Dictionary<string, float[]>
            {
                ["Burst"] = Make(0.5f, b =>
                {
                    // A whoomph: a rush of air dropping in pitch, over a low thump.
                    var sweep = Synth.Biquad.LowPass(2400f, 0.9f);
                    for (var i = 0; i < b.Length; i++)
                    {
                        var t = i / (float)Synth.Rate;
                        b[i] += sweep.Process(noise.White()) * Synth.Envelope(t, 0.015f, 0.16f);
                    }

                    Synth.AddSweep(b, 0f, 140f, 60f, 0.2f, 0.005f, 0.14f, 0.6f);
                }),
                ["Bump"] = Make(0.45f, b =>
                {
                    // Rubber meeting rock: a bouncy "bwomp" over a dull knock.
                    Synth.AddSweep(b, 0f, 190f, 110f, 0.12f, 0.004f, 0.12f, 0.7f);
                    Synth.AddSweep(b, 0f, 75f, 50f, 0.1f, 0.002f, 0.09f, 0.6f);
                    Synth.AddNoise(b, 0f, Synth.Biquad.LowPass(700f), 0.001f, 0.03f, 0.5f, noise);
                }),
                ["Splash"] = Make(0.9f, b =>
                {
                    Synth.AddNoise(b, 0f, Synth.Biquad.BandPass(900f, 0.6f), 0.01f, 0.22f, 1f, noise);
                    Synth.AddNoise(b, 0.02f, Synth.Biquad.HighPass(3200f), 0.005f, 0.12f, 0.45f, noise); // spray
                }),
                ["Capsize"] = Make(1.4f, b =>
                {
                    Synth.AddNoise(b, 0f, Synth.Biquad.BandPass(700f, 0.5f), 0.02f, 0.4f, 1f, noise);
                    Synth.AddNoise(b, 0.05f, Synth.Biquad.HighPass(2800f), 0.01f, 0.25f, 0.4f, noise);
                    Synth.AddSweep(b, 0.15f, 620f, 140f, 0.35f, 0.01f, 0.3f, 0.5f); // a comic bloop as it goes under
                }),
                ["Fall"] = Make(2.2f, b =>
                {
                    // Over the edge: a cartoon whistle falling away, then a far-off splash down in the gorge.
                    Synth.AddSweep(b, 0.05f, 1750f, 380f, 1.25f, 0.04f, 1.1f, 0.42f);
                    Synth.AddSweep(b, 0.05f, 1762f, 384f, 1.25f, 0.04f, 1.1f, 0.18f);
                    Synth.AddNoise(b, 1.32f, Synth.Biquad.LowPass(520f), 0.01f, 0.45f, 0.75f, noise);
                    Synth.AddNoise(b, 1.34f, Synth.Biquad.BandPass(1400f, 0.7f), 0.01f, 0.2f, 0.25f, noise);
                }),
                ["Beep"] = Make(0.2f, b =>
                {
                    Synth.AddSweep(b, 0f, 660f, 660f, 0f, 0.004f, 0.07f, 0.8f);
                    Synth.AddSweep(b, 0f, 1320f, 1320f, 0f, 0.004f, 0.05f, 0.25f);
                }),
                ["Go"] = Make(0.8f, b =>
                {
                    foreach (var hz in new[] { 880f, 1108.7f, 1318.5f })
                        Synth.AddSweep(b, 0f, hz, hz, 0f, 0.006f, 0.28f, 0.45f);
                }),
                ["Chime"] = Make(1.1f, b =>
                {
                    Synth.AddBell(b, 0f, 1046.5f, 0.45f, 0.6f);
                    Synth.AddBell(b, 0.12f, 1568f, 0.55f, 0.6f);
                }),
                ["Fanfare"] = Make(2f, b =>
                {
                    var notes = new[] { 72, 76, 79, 84 };
                    for (var n = 0; n < notes.Length; n++) Synth.AddPluck(b, n * 0.12f, Synth.Midi(notes[n]), 0.9f, 0.7f, noise, 0.997f);
                    foreach (var note in new[] { 72, 76, 79, 84, 88 }) Synth.AddPluck(b, 0.55f, Synth.Midi(note), 1.4f, 0.45f, noise, 0.998f);
                    Synth.AddBell(b, 0.55f, Synth.Midi(96), 0.6f, 0.25f);
                }),
                ["Star"] = Make(0.8f, b => Synth.AddBell(b, 0f, 1318.5f, 0.4f, 0.8f)),
                ["Jingle"] = Make(1.4f, b =>
                {
                    var notes = new[] { 72, 74, 76, 79, 81, 84 };
                    for (var n = 0; n < notes.Length; n++) Synth.AddBell(b, n * 0.08f, Synth.Midi(notes[n] + 12), 0.35f, 0.5f);
                }),
                ["Click"] = Make(0.06f, b =>
                {
                    Synth.AddSweep(b, 0f, 1500f, 1100f, 0.02f, 0.001f, 0.012f, 0.6f);
                    Synth.AddNoise(b, 0f, Synth.Biquad.HighPass(4000f), 0.0005f, 0.004f, 0.3f, noise);
                }),
                ["RiverCalm"] = MakeLoop(6f, 0.8f, b => Water(b, noise, 500f, 0.25f, 0.18f)),
                ["RiverRapids"] = MakeLoop(6f, 0.8f, b =>
                {
                    Water(b, noise, 1400f, 0.15f, 0.7f);
                    var hiss = Synth.Biquad.HighPass(2500f);
                    for (var i = 0; i < b.Length; i++) b[i] += hiss.Process(noise.White()) * 0.08f;
                }),
                ["LakeLapping"] = MakeLoop(6f, 0.8f, b => Water(b, noise, 350f, 0.6f, 0.08f)),
                ["LeafBlower"] = MakeLoop(2.2f, 0.2f, b => Engine(b, noise, 92f, 900f, 600f, 0.5f, 7f)),
                ["HairDryer"] = MakeLoop(2.2f, 0.2f, b =>
                {
                    var air = Synth.Biquad.BandPass(2600f, 0.7f);
                    for (var i = 0; i < b.Length; i++)
                    {
                        var t = i / (float)Synth.Rate;
                        b[i] += air.Process(noise.White()) * 0.8f + Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.12f + Mathf.Sin(2f * Mathf.PI * 1760f * t) * 0.05f;
                    }
                }),
                ["HandFan"] = MakeLoop(2.2f, 0.2f, b =>
                {
                    // Soft air in flaps, about three a second.
                    var air = Synth.Biquad.LowPass(900f);
                    for (var i = 0; i < b.Length; i++)
                    {
                        var t = i / (float)Synth.Rate;
                        b[i] += air.Process(noise.White()) * (0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Mathf.PI * 3f * t)));
                    }
                }),
                ["ShopVac"] = MakeLoop(2.2f, 0.2f, b =>
                {
                    var air = Synth.Biquad.HighPass(1200f);
                    for (var i = 0; i < b.Length; i++)
                    {
                        var t = i / (float)Synth.Rate;
                        b[i] += air.Process(noise.White()) * 0.55f + (Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.18f + Mathf.Sin(2f * Mathf.PI * 1980f * t) * 0.1f)
                            * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 5f * t));
                    }
                }),
            };
            return sounds;
        }

        /// <summary>The clips, from <see cref="Render"/>'s samples. Main thread only.</summary>
        public static SoundBank From(Dictionary<string, float[]> sounds)
        {
            AudioClip Clip(string name) => Synth.Clip(name, sounds[name]);
            return new SoundBank
            {
                Burst = Clip("Burst"), Bump = Clip("Bump"), Splash = Clip("Splash"), Capsize = Clip("Capsize"), Fall = Clip("Fall"), Beep = Clip("Beep"), Go = Clip("Go"),
                Chime = Clip("Chime"), Fanfare = Clip("Fanfare"), Star = Clip("Star"), Jingle = Clip("Jingle"), Click = Clip("Click"),
                RiverCalm = Clip("RiverCalm"), RiverRapids = Clip("RiverRapids"), LakeLapping = Clip("LakeLapping"),
                LeafBlower = Clip("LeafBlower"), HairDryer = Clip("HairDryer"), HandFan = Clip("HandFan"), ShopVac = Clip("ShopVac"),
            };
        }

        /// <summary>The steady sound a blower makes while it's on; null for the bellows, which only puffs.</summary>
        public AudioClip LoopFor(BlowerDef blower)
        {
            if (blower == null) return null;
            return BlowerModels.KindOf(blower) switch
            {
                BlowerKind.Bellows => null,
                BlowerKind.ShopVac => ShopVac,
                BlowerKind.HandFan => HandFan,
                BlowerKind.HairDryer => HairDryer,
                _ => LeafBlower,
            };
        }

        public AudioClip AmbienceFor(CourseScene? scene) => scene switch
        {
            CourseScene.Rapids => RiverRapids,
            CourseScene.Lake => LakeLapping,
            CourseScene.Creek => RiverCalm,
            _ => LakeLapping, // the practice lagoon
        };

        /// <summary>A small engine: a buzzing fundamental and its harmonics, softened, over rushing air, with a slight throb.</summary>
        static void Engine(float[] b, Synth.Noise noise, float hz, float toneCutoff, float airCutoff, float air, float throb)
        {
            var tone = Synth.Biquad.LowPass(toneCutoff);
            var rush = Synth.Biquad.LowPass(airCutoff);
            for (var i = 0; i < b.Length; i++)
            {
                var t = i / (float)Synth.Rate;
                var saw = 2f * (t * hz - Mathf.Floor(t * hz + 0.5f));
                var wobble = 1f + 0.12f * Mathf.Sin(2f * Mathf.PI * throb * t);
                b[i] += tone.Process(saw) * 0.6f * wobble + rush.Process(noise.White()) * air;
            }
        }

        /// <summary>Running water: low rumbling noise that swells and settles slowly.</summary>
        static void Water(float[] b, Synth.Noise noise, float cutoff, float swell, float brightness)
        {
            var low = Synth.Biquad.LowPass(cutoff);
            var high = Synth.Biquad.BandPass(cutoff * 4f, 0.8f);
            for (var i = 0; i < b.Length; i++)
            {
                var t = i / (float)Synth.Rate;
                var level = 1f - swell + swell * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.35f * t) * Mathf.Sin(2f * Mathf.PI * 0.13f * t + 1f));
                var white = noise.White();
                b[i] += (low.Process(white) + high.Process(white) * brightness) * level;
            }
        }

        static float[] Make(float seconds, System.Action<float[]> fill)
        {
            var buffer = Synth.Buffer(seconds);
            fill(buffer);
            return Synth.Normalise(buffer);
        }

        static float[] MakeLoop(float seconds, float fade, System.Action<float[]> fill)
        {
            var buffer = Synth.Buffer(seconds + fade);
            fill(buffer);
            return Synth.Normalise(Synth.Loop(buffer, fade), 0.7f);
        }
    }
}
