using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A small fishing boat crossing back and forth with a net streaming out behind it. The boat is solid; a raft that
    /// blunders into the net is snagged and dragged along with the boat for a moment, losing its way.
    /// </summary>
    public sealed class NetBoat : Obstacle
    {
        public const float BoatLength = 2.4f; // across the river: it travels that way
        public const float BoatWidth = 1.3f;
        public const float NetLength = 4.5f;
        public const float NetDepth = 1.6f; // up and down the river
        const float Snag = 3f; // per second: how hard the net drags a raft to its own speed
        const float RaftMass = 60f;

        readonly CrossingSpot spot;
        readonly RiverSample at;
        readonly Rigidbody2D body;
        bool placed;

        public NetBoat(Course course, Transform parent, CrossingSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            body = Kinematic("Net boat", Position(0), parent);
            body.transform.rotation = Quaternion.Euler(0f, 0f, Degrees(at.Left));
            var hull = body.gameObject.AddComponent<BoxCollider2D>();
            hull.size = new Vector2(BoatLength, BoatWidth);
            hull.sharedMaterial = Slick;
        }

        public CrossingSpot Spot => spot;
        public RiverSample At => at;

        /// <summary>How far it goes either way from the middle: not so near the edge it could squeeze a raft off.</summary>
        float Range => Mathf.Clamp(at.HalfWidth - BoatLength * 0.5f - 2f, 0f, 5f);

        float Angle(double seconds) => 2f * Mathf.PI * Phase(seconds, spot.period, spot.offset);

        public float Across(double seconds) => Range * Mathf.Sin(Angle(seconds));

        /// <summary>Its speed across the river (positive toward the left bank).</summary>
        public float Speed(double seconds) => 2f * Mathf.PI / spot.period * Range * Mathf.Cos(Angle(seconds));

        public Vector2 Position(double seconds) => at.Centre + at.Left * Across(seconds);

        /// <summary>
        /// The net, as two distances across the river: from just behind the boat to its far end, streaming out the way the
        /// boat came, longest at full speed and bunched up as it turns.
        /// </summary>
        public (float near, float far) Net(double seconds)
        {
            var cos = Mathf.Cos(Angle(seconds));
            var near = Across(seconds) - (cos >= 0f ? 1f : -1f) * BoatLength * 0.5f;
            return (near, near - NetLength * cos);
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var position = Position(seconds);
            if (!placed)
            {
                placed = true; // the clock may be anywhere when the course loads: jump there rather than sweep there
                body.position = position;
            }
            else body.MovePosition(position);

            var (near, far) = Net(seconds);
            var lo = Mathf.Min(near, far);
            var hi = Mathf.Max(near, far);
            var netVelocity = at.Left * Speed(seconds);
            foreach (var boat in boats)
            {
                if (!Afloat(boat)) continue;
                var offset = boat.Body.Position - at.Centre;
                var across = Vector2.Dot(offset, at.Left);
                if (Mathf.Abs(Vector2.Dot(offset, at.Tangent)) > NetDepth * 0.5f + RaftRadius * 0.7f || across < lo - RaftRadius * 0.5f || across > hi + RaftRadius * 0.5f) continue;
                boat.Push((netVelocity - boat.Body.Velocity) * (Snag * RaftMass * dt));
            }
        }

        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (!Between(spot.distance, NetDepth + RaftRadius + 1f, from, to)) return across;
            var eta = Mathf.Clamp((spot.distance - from) / 6f, 0f, 1.5f);
            foreach (var t in new[] { seconds + eta, seconds })
            {
                var (_, far) = Net(t);
                var boatAt = Across(t);
                var reach = Mathf.Abs(far - boatAt) * 0.5f + BoatLength * 0.5f + RaftRadius + 0.5f;
                across = Clear(across, (boatAt + far) * 0.5f, reach, limit);
            }

            return across;
        }
    }
}
