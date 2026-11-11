using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A huge fan on one bank. A windsock beside it lifts as it winds up; then it blows a gust right across the river,
    /// pushing every raft in its stretch toward the far edge (weaker the further it gets), with a lurch as it hits. Blow
    /// back into it, or brace for the lurch and hold your line. Spot: <c>side</c>, <c>period</c>.
    /// </summary>
    public sealed class GiantFan : Obstacle, ISpotted
    {
        public const float Windsock = 0.45f; // the windsock lifts from here: the tell
        public const float Gust = 0.62f; // blowing from here
        public const float Calm = 0.92f; // until here
        public const float Along = 7.5f; // the gust's reach either way along the river
        const float Force = 180f; // newtons, at the fan's bank
        const float Kick = 45f;
        const float Heel = 3.5f;

        readonly ObstacleSpot spot;
        readonly RiverSample at;

        public GiantFan(Course course, ObstacleSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            Fan = at.Centre + at.Left * (spot.side * (at.HalfWidth + 2.5f));
        }

        public ObstacleSpot Spot => spot;
        public RiverSample At => at;

        /// <summary>The fan's middle, on the bank.</summary>
        public Vector2 Fan { get; }

        public float FanPhase(double seconds) => Phase(seconds, spot.period, spot.offset);

        /// <summary>How hard it's blowing, 0 to 1.</summary>
        public float Blowing(double seconds)
        {
            var phase = FanPhase(seconds);
            if (phase < Gust || phase >= Calm) return 0f;
            return Mathf.Clamp01((phase - Gust) / 0.04f) * Mathf.Clamp01((Calm - phase) / 0.05f);
        }

        /// <summary>How high the windsock stands, 0 limp to 1 straight out.</summary>
        public float Sock(double seconds)
        {
            var phase = FanPhase(seconds);
            if (phase < Windsock || phase >= Calm + 0.04f) return 0f;
            return phase < Gust ? (phase - Windsock) / (Gust - Windsock) * 0.6f : phase < Calm ? 1f : 1f - (phase - Calm) / 0.04f;
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var blowing = Blowing(seconds);
            if (blowing <= 0f) return;
            var gusting = FanPhase(seconds) < Gust + 0.03f;
            foreach (var boat in boats)
            {
                if (!Afloat(boat)) continue;
                var offset = boat.Body.Position - at.Centre;
                var along = Vector2.Dot(offset, at.Tangent);
                if (Mathf.Abs(along) > Along) continue;
                var fromFan = Mathf.Clamp01((at.HalfWidth - spot.side * Vector2.Dot(offset, at.Left)) / (2f * at.HalfWidth));
                var away = at.Left * -spot.side;
                var strength = blowing * (1f - 0.45f * fromFan) * (1f - 0.5f * along * along / (Along * Along));
                boat.Push(away * (Force * strength * dt));
                if (gusting && Ready(boat, seconds, gap: 1.0)) boat.Shove(away * Kick, Heel);
            }
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            if (Blowing(seconds) > 0f) Box(lines, at.Centre, at.Tangent, Along, at.HalfWidth);
        }

        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            var phase = FanPhase(seconds);
            if (phase < Windsock || phase >= Gust + 0.03f) return false;
            return Mathf.Abs(Vector2.Dot(position - at.Centre, at.Tangent)) < Along + RaftRadius + ApproachSpeed * lookahead && FanPhase(seconds + lookahead) >= Gust - 0.02f;
        }

        /// <summary>Lean toward the fan's side, so the gust pushes the raft to the middle rather than over the far edge.</summary>
        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (!Between(spot.distance, Along + RaftRadius, from, to)) return across;
            var lean = spot.side * limit * 0.35f;
            return spot.side > 0f ? Mathf.Max(across, lean) : Mathf.Min(across, lean);
        }
    }
}
