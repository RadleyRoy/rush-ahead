using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A low stone weir right across the river. The water speeds up as it pours over (that part is in
    /// <see cref="Course.WaterVelocityAt"/>), and a raft going over gets flung on and lands hard: braced, it bounces;
    /// unbraced, it usually goes over.
    /// </summary>
    public sealed class Weir : Obstacle
    {
        public const float RunUp = 8f; // metres before the weir where the water starts to pull
        const float Fling = 60f;
        const float Heel = 7.5f;

        readonly WeirSpot spot;
        readonly RiverSample at;
        readonly Dictionary<Boat, float> lastDistance = new();

        public Weir(Course course, WeirSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
        }

        public WeirSpot Spot => spot;
        public RiverSample At => at;

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            foreach (var boat in boats)
            {
                var distance = River.Project(boat.Body.Position).Distance;
                var crossed = lastDistance.TryGetValue(boat, out var before) && before < spot.distance && distance >= spot.distance;
                lastDistance[boat] = distance;
                if (!crossed || !Afloat(boat) || !Ready(boat, seconds, gap: 2.0)) continue;
                boat.Shove(at.Tangent * Fling, Heel);
            }
        }

        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            var distance = River.Project(position).Distance;
            return distance > spot.distance - 4f && distance < spot.distance + 1f;
        }
    }
}
