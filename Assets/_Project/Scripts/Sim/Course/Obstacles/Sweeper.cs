using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A long log pinned at its middle to a post in the river, spinning flat across it. Twice a turn it lies along the
    /// current and leaves a way past on both sides; the rest of the time it's a club sweeping the whole width.
    /// </summary>
    public sealed class Sweeper : Obstacle
    {
        public const float LogWidth = 0.6f;
        public const float PostRadius = 0.45f;

        readonly SweeperSpot spot;
        readonly Rigidbody2D body;
        bool placed;

        public Sweeper(Course course, Transform parent, SweeperSpot spot) : base(course)
        {
            this.spot = spot;
            var at = River.At(spot.distance);
            Pivot = at.Centre + at.Left * spot.across;
            body = Kinematic("Sweeper", Pivot, parent);
            body.gameObject.AddComponent<CircleCollider2D>().radius = PostRadius;
            var box = body.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(spot.length, LogWidth);
            box.sharedMaterial = Slick; // it knocks a raft aside rather than rolling it over
            body.rotation = AngleDeg(0);
        }

        public SweeperSpot Spot => spot;
        public Vector2 Pivot { get; }

        public float AngleDeg(double seconds) => (float)(spot.degreesPerSecond * seconds % 360.0);

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            if (placed)
            {
                body.MoveRotation(AngleDeg(seconds));
                return;
            }

            placed = true; // the clock may be anywhere when the course loads: jump there rather than swing there
            body.rotation = AngleDeg(seconds);
        }

        /// <summary>AI crews take the lane it leaves open on the far side from its post, clear of the whole sweep of its log.</summary>
        public override float SteerAcross(float from, float to, float across, double seconds, float limit) =>
            Between(spot.distance, spot.length * 0.5f + RaftRadius, from, to) ? Clear(across, spot.across, spot.length * 0.5f + RaftRadius + 1.2f, limit) : across;
    }
}
