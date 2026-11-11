using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A black storm cloud hanging over one stretch. Each round it drifts over a new spot, a glowing ring grows on the
    /// water under it, and lightning strikes there: a raft in the ring is knocked aside and its blowers cut out for a
    /// moment, unless the crew is braced. Where each strike lands comes from the clock. Spot: <c>period</c> per strike.
    /// </summary>
    public sealed class StormCloud : Obstacle, ISpotted
    {
        public const float Strike = 1.8f; // the ring's radius
        public const float Glow = 0.15f; // the ring shows from here
        public const float Bolt = 0.72f; // and the bolt comes down
        public const float Flash = 0.8f; // until here
        public const float Spread = 6f; // up or down the river either way it can strike
        public const float Height = 11f;
        const float Window = 0.04f;
        const float Knock = 70f;
        const float Heel = 4.5f;
        const float Stun = 1.2f;

        readonly ObstacleSpot spot;
        readonly RiverSample at;
        readonly int seed;

        public StormCloud(Course course, ObstacleSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            seed = Mathf.RoundToInt(spot.distance * 16f) + 77;
        }

        public ObstacleSpot Spot => spot;
        public RiverSample At => at;

        public float StrikePhase(double seconds) => Phase(seconds, spot.period, spot.offset);

        /// <summary>Where this round's strike lands.</summary>
        public Vector2 Target(double seconds)
        {
            var round = Round(seconds, spot.period, spot.offset);
            var sample = River.At(spot.distance + (Hash(round, seed) * 2f - 1f) * Spread);
            return sample.Centre + sample.Left * ((Hash(round, seed + 1) * 2f - 1f) * Mathf.Max(0f, sample.HalfWidth - 1.6f));
        }

        /// <summary>The point on the water under the cloud's middle: it drifts from the last strike to the next.</summary>
        public Vector2 Cloud(double seconds) => Vector2.Lerp(Target(seconds - spot.period), Target(seconds), Smooth(Mathf.Clamp01(StrikePhase(seconds) / 0.35f)));

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var phase = StrikePhase(seconds);
            if (phase < Bolt || phase >= Bolt + Window) return;
            var target = Target(seconds);
            foreach (var boat in boats)
            {
                var raft = boat.Body.Position;
                if (!Afloat(boat) || Vector2.Distance(raft, target) > Strike + RaftRadius * 0.5f || !Ready(boat, seconds, gap: 0.8)) continue;
                if (boat.BracedFraction < 0.5f) boat.StunnedSeconds = Mathf.Max(boat.StunnedSeconds, Stun);
                boat.Shove(Away(target, raft, at.Tangent) * Knock, Heel);
            }
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            var phase = StrikePhase(seconds);
            if (phase >= Glow && phase < Flash) Circle(lines, Target(seconds), Strike - RaftRadius * 0.5f);
        }

        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            var phase = StrikePhase(seconds);
            if (phase < Glow || phase >= Bolt + Window || Vector2.Distance(position, Target(seconds)) > Strike + RaftRadius + 0.5f) return false;
            var later = StrikePhase(seconds + lookahead);
            return later >= Bolt - 0.03f || later < phase;
        }

        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (!Between(spot.distance, Spread + Strike + RaftRadius, from, to)) return across;
            var phase = StrikePhase(seconds);
            if (phase < Glow || phase >= Bolt + Window) return across;
            var here = River.Project(Target(seconds));
            return Between(here.Distance, Strike + RaftRadius + 1f, from, to) ? Clear(across, here.Lateral, Strike + RaftRadius + 0.3f, limit) : across;
        }
    }
}
