using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A churning patch of piranhas drifting back and forth across the river. A raft in it is nibbled: dragged back
    /// (it loses speed fast) and jostled this way and that. Nothing to brace for: just stay out of it.
    /// </summary>
    public sealed class PiranhaShoal : Obstacle
    {
        public const float Radius = 3f;
        const float Drag = 1.6f; // per second: how fast it eats a raft's speed
        const float Jostle = 25f;
        const float Heel = 2f;
        const float RaftMass = 60f;

        readonly CrossingSpot spot;
        readonly RiverSample at;

        public PiranhaShoal(Course course, CrossingSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
        }

        public CrossingSpot Spot => spot;
        public RiverSample At => at;

        float Range => Mathf.Clamp(at.HalfWidth - Radius, 0f, 4.5f);

        public float Across(double seconds) => Range * Mathf.Sin(2f * Mathf.PI * Phase(seconds, spot.period, spot.offset));

        public Vector2 Position(double seconds) => at.Centre + at.Left * Across(seconds);

        public bool Inside(Vector2 point, double seconds) => Vector2.Distance(point, Position(seconds)) < Radius + RaftRadius * 0.5f;

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            foreach (var boat in boats)
            {
                if (!Afloat(boat) || !Inside(boat.Body.Position, seconds)) continue;
                boat.Push(-boat.Body.Velocity * (Drag * RaftMass * dt));
                if (!Ready(boat, seconds, gap: 0.35)) continue;
                var lurch = Mathf.PerlinNoise((float)seconds * 3.1f, spot.distance) * 2f - 1f;
                boat.Shove(at.Left * (Jostle * lurch), Heel);
            }
        }

        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (!Between(spot.distance, Radius + RaftRadius + 1f, from, to)) return across;
            var eta = Mathf.Clamp((spot.distance - from) / 6f, 0f, 1.5f);
            var reach = Radius + RaftRadius * 0.5f + 0.6f;
            return Clear(Clear(across, Across(seconds + eta), reach, limit), Across(seconds), reach, limit);
        }
    }
}
