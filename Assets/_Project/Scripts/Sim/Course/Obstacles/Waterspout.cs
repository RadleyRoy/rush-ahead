using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A waterspout wandering back and forth across the river. Near it the water whirls round it and draws a raft
    /// in; get into its core and it spins the raft round and flings it out. Steer wide.
    /// </summary>
    public sealed class Waterspout : Obstacle
    {
        public const float Pull = 4.5f; // how far out it grabs
        public const float Core = 1.2f;
        const float Swirl = 220f;
        const float Draw = 70f;
        const float Fling = 150f;
        const float Heel = 6f;

        readonly CrossingSpot spot;
        readonly RiverSample at;

        public Waterspout(Course course, CrossingSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
        }

        public CrossingSpot Spot => spot;
        public RiverSample At => at;

        float Range => Mathf.Clamp(at.HalfWidth - Core - 1f, 0f, 5f);

        /// <summary>Where it is: across the river and a little up and down it, in a lazy figure of eight.</summary>
        public Vector2 Position(double seconds)
        {
            var angle = 2f * Mathf.PI * Phase(seconds, spot.period, spot.offset);
            return at.Centre + at.Left * (Range * Mathf.Sin(angle)) + at.Tangent * (2.5f * Mathf.Sin(2f * angle));
        }

        float Across(double seconds) => Vector2.Dot(Position(seconds) - at.Centre, at.Left);

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var centre = Position(seconds);
            foreach (var boat in boats)
            {
                if (!Afloat(boat)) continue;
                var offset = boat.Body.Position - centre;
                var distance = offset.magnitude;
                if (distance > Pull + RaftRadius * 0.5f || distance < 1e-3f) continue;
                var outward = offset / distance;
                var around = new Vector2(-outward.y, outward.x); // anticlockwise from above
                var strength = 1f - Mathf.Clamp01((distance - Core) / Pull);
                boat.Push((around * Swirl - outward * Draw) * (strength * dt));
                if (distance < Core + RaftRadius * 0.6f && Ready(boat, seconds, gap: 1.5))
                    boat.Shove((outward + around).normalized * Fling, Heel, spin: 1f);
            }
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            Circle(lines, Position(seconds), Pull - RaftRadius * 0.5f);
            Circle(lines, Position(seconds), Core - RaftRadius * 0.4f);
        }

        /// <summary>When the raft, coming down the river at the usual speed, would run into its core and be flung.</summary>
        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            var here = River.Project(position);
            var reach = Core + RaftRadius * 0.6f + 1f;
            const float wander = 2.5f; // up and down the river, in its figure of eight
            if (here.Distance > spot.distance + wander + reach || here.Distance < spot.distance - wander - reach - ApproachSpeed * lookahead) return false;
            for (var t = 0f; t <= lookahead; t += 0.1f)
                if (Vector2.Distance(Position(seconds + t), position + at.Tangent * (ApproachSpeed * t)) < reach) return true;
            return false;
        }

        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (!Between(spot.distance, Pull + 3f, from, to)) return across;
            var eta = Mathf.Clamp((spot.distance - from) / ApproachSpeed, 0f, 1.5f);
            var reach = Pull + RaftRadius * 0.5f;
            return Clear(Clear(across, Across(seconds + eta), reach, limit), Across(seconds), reach, limit);
        }
    }
}
