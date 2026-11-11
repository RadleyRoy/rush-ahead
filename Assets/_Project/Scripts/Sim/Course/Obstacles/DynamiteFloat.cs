using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A miner's shack on one bank. Every round a crate of dynamite is lobbed into the river, its fuse fizzing; it drifts
    /// down and out toward the middle, and when the fuse runs out it blows, knocking any raft near it away and heeling it
    /// hard. Keep well clear, or get past before it goes. Spot: <c>side</c>, <c>period</c> (the fuse is most of it).
    /// </summary>
    public sealed class DynamiteFloat : Obstacle, ISpotted
    {
        public const float Blast = 4f;
        public const float Toss = 0.1f; // of a round, flying from the shack
        public const float Fuse = 0.86f; // it blows here
        public const float Drift = 3.6f; // metres a second down the river
        const float Window = 0.03f;
        const float Knock = 120f;
        const float Heel = 7f;

        readonly ObstacleSpot spot;
        readonly RiverSample at;

        public DynamiteFloat(Course course, ObstacleSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            Shack = at.Centre + at.Left * (spot.side * (at.HalfWidth + 2.2f));
        }

        public ObstacleSpot Spot => spot;
        public RiverSample At => at;
        public Vector2 Shack { get; }

        /// <summary>How far downstream the crate gets before it blows (the dealer keeps that water to itself).</summary>
        public static float Run(float period) => Drift * (Fuse - Toss) * period;

        public float FusePhase(double seconds) => Phase(seconds, spot.period, spot.offset);

        Vector2 Drifting(float wet)
        {
            var sample = River.At(spot.distance + Drift * wet);
            return sample.Centre + sample.Left * (spot.side * (sample.HalfWidth - 1.6f - Mathf.Min(wet * 0.9f, sample.HalfWidth * 0.9f)));
        }

        /// <summary>The crate: where it is, its height, how much of the fuse has burnt (0 to 1), and the blast (0 none, then 0 to 1).</summary>
        public (Vector2 position, float height, float fuse, float blast) Crate(double seconds)
        {
            var phase = FusePhase(seconds);
            var fuse = Mathf.Clamp01(phase / Fuse);
            if (phase < Toss)
            {
                var u = phase / Toss;
                return (Vector2.Lerp(Shack, Drifting(0f), u), 0.6f + 3f * 4f * u * (1f - u), fuse, 0f);
            }

            if (phase < Fuse) return (Drifting((phase - Toss) * spot.period), 0f, fuse, 0f);
            return (Drifting((Fuse - Toss) * spot.period), 0f, 1f, (phase - Fuse) / (1f - Fuse));
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var phase = FusePhase(seconds);
            if (phase < Fuse || phase >= Fuse + Window) return;
            var (crate, _, _, _) = Crate(seconds);
            foreach (var boat in boats)
            {
                var raft = boat.Body.Position;
                if (!Afloat(boat) || Vector2.Distance(raft, crate) > Blast + RaftRadius * 0.5f || !Ready(boat, seconds, gap: 1.5)) continue;
                boat.Shove(Away(crate, raft, at.Tangent) * Knock, Heel, 0.5f);
            }
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            var (crate, height, fuse, blast) = Crate(seconds);
            if (height <= 0.05f && blast <= 0f && fuse > 0.5f) Circle(lines, crate, Blast - RaftRadius * 0.5f);
        }

        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            var phase = FusePhase(seconds);
            if (phase < Toss || phase >= Fuse + Window) return false;
            var later = FusePhase(seconds + lookahead);
            if (later < Fuse - 0.02f && later >= phase) return false;
            return Vector2.Distance(position, Crate(seconds).position) < Blast + RaftRadius + 1f;
        }

        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            var phase = FusePhase(seconds);
            if (phase < Toss * 0.5f || phase >= Fuse + Window) return across;
            var here = River.Project(Crate(seconds + 0.5).position);
            return Between(here.Distance, Blast + RaftRadius, from, to) ? Clear(across, here.Lateral, Blast + RaftRadius + 0.3f, limit) : across;
        }
    }
}
