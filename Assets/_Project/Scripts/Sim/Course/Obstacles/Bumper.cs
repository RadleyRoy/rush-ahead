using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A big springy buoy moored in the river. It doesn't move, but anything that touches it bounces off hard, with a
    /// kick on top of the bounce: placed near an edge, it's there to fling the careless over it.
    /// </summary>
    public sealed class Bumper : Obstacle
    {
        const float Kick = 50f;
        const float Heel = 2.5f;
        static readonly PhysicsMaterial2D Springy = new("Bumper") { bounciness = 0.8f, friction = 0f };

        readonly BumperSpot spot;
        readonly float across;

        public Bumper(Course course, Transform parent, BumperSpot spot) : base(course)
        {
            this.spot = spot;
            var at = River.Project(spot.position);
            Distance = at.Distance;
            across = at.Lateral;
            var go = new GameObject("Bumper");
            go.transform.position = spot.position;
            go.transform.SetParent(parent, true);
            var circle = go.AddComponent<CircleCollider2D>();
            circle.radius = spot.radius;
            circle.sharedMaterial = Springy;
        }

        public BumperSpot Spot => spot;
        public float Distance { get; }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            foreach (var boat in boats)
            {
                if (!Afloat(boat)) continue;
                var offset = boat.Body.Position - spot.position;
                if (offset.magnitude > spot.radius + RaftRadius + 0.15f || !Ready(boat, seconds, gap: 0.6)) continue;
                boat.Shove(offset.normalized * Kick, Heel);
            }
        }

        /// <summary>AI crews line up well before it, on the river side of it: between it and the edge is where rafts go over.</summary>
        public override float SteerAcross(float from, float to, float across, double seconds, float limit) =>
            Between(Distance, spot.radius + RaftRadius + 6f, from, to) ? Clear(across, this.across, spot.radius + RaftRadius + 0.8f, limit) : across;
    }
}
