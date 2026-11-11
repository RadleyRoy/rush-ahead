using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A gantry over the river with a huge iron ball swinging on a chain beneath it, up and down the river (head-on to
    /// the rafts). Low in its swing it skims the water and smashes whatever it meets the way it's going; high at either
    /// end it clears everything. Time it, or go round: it swings in one lane. Spot: <c>across</c> (its lane), <c>period</c>.
    /// </summary>
    public sealed class WreckingBall : Obstacle, ISpotted
    {
        public const float PivotHeight = 8.5f;
        public const float Chain = 7.6f; // pivot to the ball's middle
        public const float Ball = 1.25f;
        public const float SwingDeg = 55f;
        public const float LowEnough = 2.4f; // the ball's middle below this, and it hits the crew
        const float Knock = 125f;
        const float Heel = 6.5f;

        readonly ObstacleSpot spot;
        readonly RiverSample at;

        public WreckingBall(Course course, ObstacleSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            Pivot = at.Centre + at.Left * spot.across;
        }

        public ObstacleSpot Spot => spot;
        public RiverSample At => at;
        public Vector2 Pivot { get; }

        /// <summary>How far along the river either way it reaches at the ends of its swing.</summary>
        public static float Reach => Chain * Mathf.Sin(SwingDeg * Mathf.Deg2Rad) + Ball;

        /// <summary>The swing angle (radians, positive downstream).</summary>
        public float Swing(double seconds) => SwingDeg * Mathf.Deg2Rad * Mathf.Sin(2f * Mathf.PI * Phase(seconds, spot.period, spot.offset));

        /// <summary>The point on the water under the ball's middle, and the middle's height.</summary>
        public (Vector2 ground, float height) Pose(double seconds)
        {
            var swing = Swing(seconds);
            return (Pivot + at.Tangent * (Chain * Mathf.Sin(swing)), PivotHeight - Chain * Mathf.Cos(swing));
        }

        /// <summary>Which way it's moving along the river: +1 downstream.</summary>
        float Heading(double seconds) => Mathf.Cos(2f * Mathf.PI * Phase(seconds, spot.period, spot.offset)) >= 0f ? 1f : -1f;

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var (ball, height) = Pose(seconds);
            if (height >= LowEnough) return;
            var heading = at.Tangent * Heading(seconds);
            foreach (var boat in boats)
            {
                var raft = boat.Body.Position;
                if (!Afloat(boat) || Vector2.Distance(raft, ball) > Ball + RaftRadius * 0.75f || !Ready(boat, seconds, gap: 1.2)) continue;
                boat.Shove((heading * 0.8f + Away(ball, raft, heading) * 0.6f).normalized * Knock, Heel, 0.3f);
            }
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            var (ball, height) = Pose(seconds);
            if (height < LowEnough) Circle(lines, ball, Ball - RaftRadius * 0.25f);
        }

        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            if (Mathf.Abs(Vector2.Dot(position - Pivot, at.Left)) > Ball + RaftRadius + 0.6f) return false;
            for (var t = 0f; t <= lookahead; t += 0.05f)
            {
                var (ball, height) = Pose(seconds + t);
                if (height < LowEnough && Vector2.Distance(position, ball) < Ball + RaftRadius + 0.6f) return true;
            }

            return false;
        }

        public override float SteerAcross(float from, float to, float across, double seconds, float limit) =>
            Between(spot.distance, Reach + RaftRadius + 1f, from, to) ? Clear(across, spot.across, Ball + RaftRadius + 0.5f, limit) : across;
    }
}
