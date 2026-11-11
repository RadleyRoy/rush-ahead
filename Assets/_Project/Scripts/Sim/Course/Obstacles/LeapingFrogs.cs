using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// Three lily pads across the river and two giant frogs hopping between them, both at once and always to different
    /// pads (they swap mid-air). Each landing throws up a splash that knocks a raft beside the pad away: pass by the pad
    /// nobody's about to land on, or between hops. Spot: <c>period</c> (one hop).
    /// </summary>
    public sealed class LeapingFrogs : Obstacle, ISpotted
    {
        public const float PadRadius = 1.3f;
        public const float Splash = 2f;
        public const float Leap = 0.55f; // of a hop, crouched on the pad before it springs
        public const float HopHeight = 3.2f;
        public const int Frogs = 2;
        const float Window = 0.05f;
        const float Knock = 90f;
        const float Heel = 5.5f;

        static readonly int[] Pattern = { 0, 1, 2, 1 };

        readonly ObstacleSpot spot;
        readonly RiverSample at;
        readonly Vector2[] pads = new Vector2[3];

        public LeapingFrogs(Course course, ObstacleSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            var spread = Mathf.Min(at.HalfWidth * 0.62f, 5f);
            for (var i = 0; i < 3; i++) pads[i] = at.Centre + at.Left * ((i - 1) * spread);
        }

        public ObstacleSpot Spot => spot;
        public RiverSample At => at;
        public Vector2 Pad(int i) => pads[i];

        public float HopPhase(double seconds) => Phase(seconds, spot.period, spot.offset);

        /// <summary>The pad frog <paramref name="frog"/> sits on during hop <paramref name="round"/> (the two never share one).</summary>
        public static int PadOf(int frog, int round) => Pattern[(((round + frog * 3) % 4) + 4) % 4];

        /// <summary>Where a frog is, its height above the water, and how far through its leap (0 crouched, then 0 to 1 in the air).</summary>
        public (Vector2 position, float height, float leap) Frog(int frog, double seconds)
        {
            var round = Round(seconds, spot.period, spot.offset);
            var phase = HopPhase(seconds);
            var from = pads[PadOf(frog, round)];
            if (phase < Leap) return (from, 0.3f, 0f);
            var s = (phase - Leap) / (1f - Leap);
            return (Vector2.Lerp(from, pads[PadOf(frog, round + 1)], s), 0.3f + HopHeight * 4f * s * (1f - s), s);
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            if (HopPhase(seconds) >= Window) return;
            var round = Round(seconds, spot.period, spot.offset);
            for (var frog = 0; frog < Frogs; frog++)
            {
                var pad = pads[PadOf(frog, round)];
                foreach (var boat in boats)
                {
                    var raft = boat.Body.Position;
                    if (!Afloat(boat) || Vector2.Distance(raft, pad) > Splash + RaftRadius * 0.5f || !Ready(boat, seconds, frog, 0.8)) continue;
                    boat.Shove(Away(pad, raft, at.Tangent) * Knock, Heel);
                }
            }
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            var phase = HopPhase(seconds);
            if (phase < Leap && phase >= Window) return;
            var round = Round(seconds, spot.period, spot.offset) + (phase >= Leap ? 1 : 0);
            for (var frog = 0; frog < Frogs; frog++) Circle(lines, pads[PadOf(frog, round)], Splash - RaftRadius * 0.5f);
        }

        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            var phase = HopPhase(seconds);
            var lands = phase + lookahead / spot.period >= 1f || phase < Window;
            if (!lands) return false;
            var round = Round(seconds, spot.period, spot.offset) + (phase < Window ? 0 : 1);
            for (var frog = 0; frog < Frogs; frog++)
                if (Vector2.Distance(position, pads[PadOf(frog, round)]) < Splash + RaftRadius + 0.5f) return true;
            return false;
        }

        /// <summary>For the pad nobody lands on next.</summary>
        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (!Between(spot.distance, Splash + RaftRadius + 1f, from, to)) return across;
            var eta = Mathf.Clamp((spot.distance - from) / ApproachSpeed, 0f, 2f);
            var round = Round(seconds + eta, spot.period, spot.offset) + (HopPhase(seconds + eta) >= Leap ? 1 : 0);
            var free = 3 - PadOf(0, round) - PadOf(1, round); // 0 + 1 + 2 = 3
            var lateral = River.Project(pads[Mathf.Clamp(free, 0, 2)]).Lateral;
            return Mathf.Clamp(Mathf.MoveTowards(across, lateral, 3f), -limit, limit);
        }
    }
}
