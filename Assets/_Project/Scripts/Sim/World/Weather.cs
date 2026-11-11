using System.Collections.Generic;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>The weather a race is run in. The host picks it in the lobby (or Random, dealt from the round's seed).</summary>
    public enum Weather : byte
    {
        Clear = 0,

        /// <summary>Rain, a dark sky and gusts of wind across the river.</summary>
        Rain = 1,

        /// <summary>Thick fog: lamps on the obstacles so their tells still read.</summary>
        Fog = 2,

        /// <summary>Moonlight, lanterns on the rafts, glowing gates and crates.</summary>
        Night = 3,
    }

    public static class WeatherDeal
    {
        public const int Kinds = 4;

        /// <summary>The lobby's choice that deals the weather from the round's seed.</summary>
        public const int Random = Kinds;

        /// <summary>The weather a Random round gets: the same from the same seed on every device. Clear 40%, the others 20% each.</summary>
        public static Weather Deal(int seed)
        {
            var roll = (int)((uint)(seed * 2654435761u + 0x9E3779B9u) >> 8) % 10;
            return roll < 4 ? Weather.Clear : roll < 6 ? Weather.Rain : roll < 8 ? Weather.Fog : Weather.Night;
        }

        /// <summary>A lobby choice (a weather, or <see cref="Random"/>) made into this round's weather.</summary>
        public static Weather For(int choice, int seed) => choice >= Random ? Deal(seed) : (Weather)Mathf.Clamp(choice, 0, Kinds - 1);
    }

    /// <summary>
    /// Gusts of wind across the river in the rain: every 6 to 10 s one blows for about 2 s, from one bank or the other,
    /// with 1.2 s of warning first (the rain leans, streaks run across the water, an arrow on the screen). All from the
    /// round's seed and the race clock, so every device sees the same gusts; the host does the pushing.
    /// </summary>
    public sealed class Gusts
    {
        public const float Warning = 1.2f;
        public const float Blow = 2f;

        /// <summary>Newtons across the river on a raft nobody is bracing.</summary>
        public const float Force = 120f;

        /// <summary>How much of the push a fully braced crew takes off (blowing into it does the rest).</summary>
        public const float BraceHold = 0.55f;

        const float FirstAfter = 6f; // the first gust comes this soon after the clock starts, give or take three seconds

        readonly uint seed;
        readonly List<(double start, int side)> timetable = new();
        int last;

        public Gusts(int seed) => this.seed = (uint)seed;

        (double start, int side) Gust(int k)
        {
            while (timetable.Count <= k)
            {
                var n = timetable.Count;
                var h = Hash(n);
                var gap = n == 0 ? FirstAfter + (h & 0xFFFF) / 65535f * 3f : 6f + (h & 0xFFFF) / 65535f * 4f;
                var start = (n == 0 ? 0.0 : timetable[n - 1].start) + gap;
                timetable.Add((start, ((h >> 16) & 1) == 0 ? 1 : -1));
            }

            return timetable[k];
        }

        uint Hash(int n)
        {
            var x = seed * 2654435761u ^ (uint)(n + 1) * 0x85EBCA6Bu;
            x ^= x >> 13;
            x *= 0xC2B2AE35u;
            x ^= x >> 16;
            return x;
        }

        /// <summary>
        /// The gust at a time on the race clock: how hard it's blowing (0 to 1), which way (+1 towards the river's left
        /// bank, -1 its right), and how near the next one is (0 nowhere near, rising to 1 as it starts).
        /// </summary>
        public (float strength, int side, float warning) At(double seconds)
        {
            if (seconds < 0.0) return (0f, 0, 0f);
            if (last > 0 && Gust(last - 1).start + Blow >= seconds) last = 0; // the clock went back (a new race)
            while (Gust(last).start + Blow < seconds) last++;
            var (start, side) = Gust(last);
            if (seconds >= start)
            {
                var t = (float)(seconds - start);
                return (Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((Blow - t) / 0.4f), side, 0f);
            }

            var until = (float)(start - seconds);
            return (0f, side, until < Warning ? 1f - until / Warning : 0f);
        }

        /// <summary>The push on a raft here, now: across the river at the raft, less for a braced crew.</summary>
        public Vector2 Push(River river, Vector2 position, double seconds, float braced)
        {
            var (strength, side, _) = At(seconds);
            if (strength <= 0f) return Vector2.zero;
            var at = river.Project(position);
            return new Vector2(-at.Tangent.y, at.Tangent.x) * (side * Force * strength * (1f - BraceHold * Mathf.Clamp01(braced)));
        }

        /// <summary>For AI crews: which way across the river (as a direction) the wind blows or is about to, and how hard (0 to 1).</summary>
        public Vector2 Coming(River river, Vector2 position, double seconds)
        {
            var (strength, side, warning) = At(seconds);
            var amount = Mathf.Max(strength, warning * warning);
            if (amount <= 0f) return Vector2.zero;
            var at = river.Project(position);
            return new Vector2(-at.Tangent.y, at.Tangent.x) * (side * amount);
        }
    }
}
