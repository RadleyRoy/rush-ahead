using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A huge snapping turtle paddling slowly back and forth across the river. It's solid (a raft just bounces off its
    /// shell), and anything that comes near its head gets snapped at: a hard shove that sets the raft spinning.
    /// </summary>
    public sealed class Turtle : Obstacle
    {
        public const float Radius = 1.5f;
        const float Snap = 75f;
        const float Heel = 3f;

        readonly CrossingSpot spot;
        readonly RiverSample at;
        readonly Rigidbody2D body;
        bool placed;

        public Turtle(Course course, Transform parent, CrossingSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            body = Kinematic("Turtle", Position(0), parent);
            var shell = body.gameObject.AddComponent<CircleCollider2D>();
            shell.radius = Radius;
            shell.sharedMaterial = Slick;
        }

        public CrossingSpot Spot => spot;
        public RiverSample At => at;

        /// <summary>How far it swims either way from the middle: never so near the edge it could squeeze a raft off, nor further than it can paddle in time.</summary>
        public float Range => Mathf.Clamp(at.HalfWidth - Radius - 2.5f, 0f, 4.5f);

        /// <summary>Metres across the river (positive is the left): a slow, steady swim from side to side.</summary>
        public float Across(double seconds) => Range * Mathf.Sin(2f * Mathf.PI * Phase(seconds, spot.period, spot.offset));

        public Vector2 Position(double seconds) => at.Centre + at.Left * Across(seconds);

        /// <summary>Which way it's swimming (and so facing), across the river.</summary>
        public Vector2 Facing(double seconds) => Mathf.Cos(2f * Mathf.PI * Phase(seconds, spot.period, spot.offset)) >= 0f ? at.Left : -at.Left;

        public Vector2 Head(double seconds) => Position(seconds) + Facing(seconds) * (Radius + 0.4f);

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var position = Position(seconds);
            if (!placed)
            {
                placed = true; // the clock may be anywhere when the course loads: jump there rather than sweep there
                body.position = position;
            }
            else body.MovePosition(position);

            var head = Head(seconds);
            foreach (var boat in boats)
            {
                if (!Afloat(boat) || Vector2.Distance(boat.Body.Position, head) > RaftRadius + 0.6f || !Ready(boat, seconds, gap: 1.5)) continue;
                // A snap knocks the raft on along (or back up) the river and sets it spinning, rather than over the edge.
                var along = Vector2.Dot(boat.Body.Position - position, at.Tangent) >= 0f ? at.Tangent : -at.Tangent;
                boat.Shove(along * Snap, Heel, spin: 1f);
            }
        }

        public override void Zones(double seconds, List<Vector2> lines) => Circle(lines, Head(seconds), 0.6f);

        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (!Between(spot.distance, Radius + RaftRadius + 1f, from, to)) return across;
            var eta = Mathf.Clamp((spot.distance - from) / ApproachSpeed, 0f, 1.5f);
            var reach = Radius + RaftRadius + 0.8f;
            return Clear(Clear(across, Across(seconds + eta), reach, limit), Across(seconds), reach, limit);
        }
    }
}
