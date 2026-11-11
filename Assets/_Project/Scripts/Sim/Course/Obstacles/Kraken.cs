using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A kraken under one stretch of river. Each of its arms in turn: a line of bubbles shows where it will come down,
    /// the tentacle rears up out of the water by the bank, then slams across the river along that line, knocking a raft
    /// under it aside and heeling it hard (a braced crew rides it out). Where each slam lands is worked out from the
    /// clock, so every device agrees. Spot: <c>period</c> is each arm's round, <c>size</c> the arms (1 to 3, else 2).
    /// </summary>
    public sealed class Kraken : Obstacle, ISpotted
    {
        public const float Bubbles = 0.2f; // the line of bubbles shows from here
        public const float Rise = 0.5f; // the tentacle rears up out of the water
        public const float Slam = 0.7f; // and comes down
        public const float Lift = 0.86f; // lying on the water until here, then it slides back under
        public const float Thickness = 0.6f; // the tentacle's radius on the water
        public const float Spread = 4f; // metres up or down the river either way a slam can land
        const float Window = 0.035f;
        const float Knock = 105f;
        const float Heel = 7f;

        readonly ObstacleSpot spot;
        readonly RiverSample at;
        readonly int seed;

        public Kraken(Course course, ObstacleSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            seed = Mathf.RoundToInt(spot.distance * 16f);
        }

        public ObstacleSpot Spot => spot;
        public RiverSample At => at;
        public int Arms => spot.size >= 1f ? Mathf.Clamp(Mathf.RoundToInt(spot.size), 1, 3) : 2;

        double Shift(int arm) => arm * (double)spot.period / Arms;

        /// <summary>How far round its round an arm is: bubbles from <see cref="Bubbles"/>, up at <see cref="Rise"/>, down at <see cref="Slam"/>.</summary>
        public float ArmPhase(int arm, double seconds) => Phase(seconds + Shift(arm), spot.period, spot.offset);

        /// <summary>Where this round's slam lies: from the tentacle's root at one bank to its tip out in the river.</summary>
        public (Vector2 root, Vector2 tip) Line(int arm, double seconds)
        {
            var key = Round(seconds + Shift(arm), spot.period, spot.offset) * 4 + arm;
            var sample = River.At(spot.distance + (Hash(key, seed) * 2f - 1f) * Spread);
            var side = Hash(key, seed + 1) < 0.5f ? -1f : 1f;
            var reach = sample.HalfWidth * Mathf.Lerp(0.95f, 1.3f, Hash(key, seed + 2));
            var root = sample.Centre + sample.Left * (side * (sample.HalfWidth + 0.6f));
            var slant = (Hash(key, seed + 3) * 2f - 1f) * 0.35f; // never quite square across
            var direction = (-side * sample.Left + slant * sample.Tangent).normalized;
            return (root, root + direction * reach);
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            for (var arm = 0; arm < Arms; arm++)
            {
                var phase = ArmPhase(arm, seconds);
                if (phase < Slam || phase >= Slam + Window) continue;
                var (root, tip) = Line(arm, seconds);
                foreach (var boat in boats)
                {
                    if (!Afloat(boat)) continue;
                    var raft = boat.Body.Position;
                    var nearest = Nearest(raft, root, tip);
                    if (Vector2.Distance(raft, nearest) > Thickness + RaftRadius * 0.6f || !Ready(boat, seconds, arm)) continue;
                    boat.Shove(Away(nearest, raft, at.Tangent) * Knock, Heel, 0.6f);
                }
            }
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            for (var arm = 0; arm < Arms; arm++)
            {
                var phase = ArmPhase(arm, seconds);
                if (phase < Bubbles || phase >= Lift) continue;
                var (root, tip) = Line(arm, seconds);
                Box(lines, (root + tip) * 0.5f, (tip - root).normalized, Vector2.Distance(root, tip) * 0.5f, Thickness - RaftRadius * 0.4f);
            }
        }

        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            for (var arm = 0; arm < Arms; arm++)
            {
                var phase = ArmPhase(arm, seconds);
                if (phase < Bubbles || phase >= Slam + Window) continue;
                var (root, tip) = Line(arm, seconds);
                if (Vector2.Distance(position, Nearest(position, root, tip)) > Thickness + RaftRadius + 0.6f) continue;
                var later = ArmPhase(arm, seconds + lookahead);
                if (later >= Slam - 0.03f || later < phase) return true;
            }

            return false;
        }

        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (!Between(spot.distance, Spread + RaftRadius + 1f, from, to)) return across;
            for (var arm = 0; arm < Arms; arm++)
            {
                var phase = ArmPhase(arm, seconds);
                if (phase < Bubbles || phase >= Slam + Window) continue;
                var (root, tip) = Line(arm, seconds);
                var a = River.Project(root).Lateral;
                var b = River.Project(tip).Lateral;
                across = Clear(across, (a + b) * 0.5f, Mathf.Abs(a - b) * 0.5f + Thickness + RaftRadius + 0.3f, limit);
            }

            return across;
        }
    }
}
