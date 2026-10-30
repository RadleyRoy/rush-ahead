using NUnit.Framework;
using RushAhead.Render;
using UnityEngine;

namespace RushAhead.Tests
{
    public class SoundTests
    {
        static readonly string[] Loops = { "RiverCalm", "RiverRapids", "LakeLapping", "LeafBlower", "HairDryer", "HandFan", "ShopVac" };

        [Test]
        public void EverySound_IsMade_LoudEnough_AndNeverBroken()
        {
            var sounds = SoundBank.Render();
            Assert.That(sounds.Count, Is.EqualTo(19));
            foreach (var (name, samples) in sounds)
            {
                Assert.That(samples.Length, Is.GreaterThan(1000), name);
                var peak = 0f;
                foreach (var s in samples)
                {
                    Assert.That(float.IsFinite(s), Is.True, $"{name} has a broken sample");
                    peak = Mathf.Max(peak, Mathf.Abs(s));
                }

                Assert.That(peak, Is.InRange(0.6f, 0.95f), $"{name} is normalised");
            }
        }

        [Test]
        public void EveryLoop_JoinsUpWithoutAClick()
        {
            var sounds = SoundBank.Render();
            foreach (var name in Loops)
            {
                var samples = sounds[name];
                var steepest = 0f;
                for (var i = 1; i < samples.Length; i++) steepest = Mathf.Max(steepest, Mathf.Abs(samples[i] - samples[i - 1]));
                Assert.That(Mathf.Abs(samples[0] - samples[^1]), Is.LessThanOrEqualTo(steepest), $"{name}'s seam is no rougher than the sound itself");
            }
        }

        [Test]
        public void BothTunes_AreMade_AndTheirLengthIsEightBars()
        {
            var race = Music.Race();
            var menu = Music.Menu();
            Assert.That(race.Length, Is.EqualTo(Mathf.CeilToInt(8 * 4 * 60f / 132f * Music.Rate)));
            Assert.That(menu.Length, Is.EqualTo(Mathf.CeilToInt(8 * 4 * 60f / 96f * Music.Rate)));
            foreach (var tune in new[] { race, menu })
            {
                var energy = 0.0;
                foreach (var s in tune)
                {
                    Assert.That(float.IsFinite(s), Is.True);
                    energy += s * s;
                }

                Assert.That(energy / tune.Length, Is.GreaterThan(0.002), "not silent");
            }
        }
    }
}
