using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A sawmill's flume on one bank. Every round a log slides down its wooden chute and shoots out across the river,
    /// turning to drift down with the water before it sinks; there are often two out at once. A raft it meets is knocked
    /// aside. The logs follow the clock (they aren't loose bodies), so everyone sees the same ones. Spot: <c>side</c>,
    /// <c>period</c> (one log each).
    /// </summary>
    public sealed class LogFlume : Obstacle, ISpotted
    {
        public const float LogLength = 4f;
        public const float LogRadius = 0.42f;
        public const float Chute = 0.28f; // of a round, sliding down the chute
        public const float Life = 1.75f; // rounds from the top of the chute until it's gone
        public const float Drift = 3f; // metres a second down the river once it's in
        public const float ChuteLength = 7f;
        public const float ChuteHeight = 3.5f;
        const float Knock = 80f;
        const float Heel = 4.5f;

        readonly ObstacleSpot spot;
        readonly RiverSample at;

        public LogFlume(Course course, ObstacleSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            Exit = at.Centre + at.Left * (spot.side * (at.HalfWidth - 0.4f));
            Top = Exit + at.Left * (spot.side * ChuteLength) - at.Tangent * 1f;
        }

        public ObstacleSpot Spot => spot;
        public RiverSample At => at;

        /// <summary>The chute's top, up on the bank, and where it meets the water.</summary>
        public Vector2 Top { get; }

        public Vector2 Exit { get; }

        /// <summary>How far downstream a log gets before it sinks (the dealer keeps that water to itself).</summary>
        public static float Run(float period) => Drift * (Life - Chute) * period;

        /// <summary>The newest log's number (older ones are one, two... less).</summary>
        public int Newest(double seconds) => Round(seconds, spot.period, spot.offset);

        /// <summary>Log <paramref name="k"/>: its middle, its length's direction, its height, whether it's in the water, and whether it's there at all.</summary>
        public (Vector2 centre, Vector2 axis, float height, bool inWater, bool alive) Log(int k, double seconds)
        {
            var age = (float)((seconds + spot.offset) / spot.period - k);
            if (age < 0f || age >= Life) return (default, Vector2.right, -10f, false, false);
            var outward = -spot.side * at.Left;
            if (age < Chute)
            {
                var t = age / Chute;
                var splash = Exit + outward * (LogLength * 0.5f); // its middle as it leaves the chute: just out on the water
                return (Vector2.Lerp(Top, splash, t * t), (splash - Top).normalized, ChuteHeight * (1f - t * t) + 0.3f, false, true);
            }

            var wet = (age - Chute) * spot.period; // seconds in the water
            var sample = River.At(spot.distance + Drift * wet);
            var shot = Mathf.Min(wet * 2.2f, sample.HalfWidth * 1.1f); // shot out from the bank, slowing
            var centre = sample.Centre + sample.Left * (spot.side * (sample.HalfWidth - 0.4f - LogLength * 0.5f - shot));
            var axis = Vector2.Lerp(outward, sample.Tangent, Mathf.Clamp01(wet / 1.5f)).normalized;
            var sink = Mathf.Clamp01((age - (Life - 0.2f)) / 0.2f);
            return (centre, axis, -0.8f * sink, true, true);
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var newest = Newest(seconds);
            for (var k = newest; k >= newest - 1; k--)
            {
                var (centre, axis, height, inWater, alive) = Log(k, seconds);
                if (!alive || !inWater || height < -0.3f) continue;
                var a = centre - axis * (LogLength * 0.5f);
                var b = centre + axis * (LogLength * 0.5f);
                foreach (var boat in boats)
                {
                    if (!Afloat(boat)) continue;
                    var raft = boat.Body.Position;
                    var nearest = Nearest(raft, a, b);
                    if (Vector2.Distance(raft, nearest) > LogRadius + RaftRadius || !Ready(boat, seconds, k & 1)) continue;
                    boat.Shove(Away(nearest, raft, at.Tangent) * Knock, Heel, 0.5f);
                }
            }
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            var newest = Newest(seconds);
            for (var k = newest; k >= newest - 1; k--)
            {
                var (centre, axis, height, inWater, alive) = Log(k, seconds);
                if (alive && inWater && height >= -0.3f) Box(lines, centre, axis, LogLength * 0.5f + LogRadius, LogRadius);
            }
        }

        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (!Between(spot.distance + Run(spot.period) * 0.5f, Run(spot.period) * 0.5f + RaftRadius + 2f, from, to)) return across;
            var newest = Newest(seconds);
            for (var k = newest; k >= newest - 1; k--)
            {
                var eta = 0.6;
                var (centre, axis, _, inWater, alive) = Log(k, seconds + eta);
                if (!alive || !inWater) continue;
                var here = River.Project(centre);
                if (!Between(here.Distance, LogLength * 0.5f + RaftRadius + 1f, from, to)) continue;
                var spread = Mathf.Abs(Vector2.Dot(axis, River.At(here.Distance).Left)) * LogLength * 0.5f;
                across = Clear(across, here.Lateral, spread + LogRadius + RaftRadius + 0.3f, limit);
            }

            return across;
        }
    }
}
