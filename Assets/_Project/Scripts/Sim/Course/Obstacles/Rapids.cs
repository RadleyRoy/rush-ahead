using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A stretch of white water. The current surges (that part is in <see cref="Course.WaterVelocityAt"/>), and every
    /// half second or so the boiling water throws a raft sideways and heels it over, one way or the other. Bracing
    /// rides it out, but a braced crew can't steer; AI crews leave it to their panic reflex.
    /// </summary>
    public sealed class Rapids : Obstacle
    {
        const float Kick = 40f;
        const float Heel = 3f;
        const double Every = 0.6;

        readonly RapidsSpot spot;

        public Rapids(Course course, RapidsSpot spot) : base(course) => this.spot = spot;

        public RapidsSpot Spot => spot;

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            foreach (var boat in boats)
            {
                if (!Afloat(boat)) continue;
                var at = River.Project(boat.Body.Position);
                if (at.Distance < spot.from || at.Distance > spot.to || !Ready(boat, seconds, gap: Every)) continue;
                // Which way the water throws it: smooth noise over the river and the clock, so it's the same for the
                // same raft at the same moment, but there's no learning it.
                var lurch = Mathf.PerlinNoise(at.Distance * 0.21f, (float)seconds * 1.7f) * 2f - 1f;
                var side = new Vector2(-at.Tangent.y, at.Tangent.x) * Mathf.Sign(lurch);
                boat.Shove(side * (Kick * (0.5f + Mathf.Abs(lurch))), Heel);
            }
        }
    }
}
