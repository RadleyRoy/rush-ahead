using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A log ramp in the river. A raft that hits its lip fast enough takes off: a kick of speed and a flight of ten metres
    /// or more (the dealer keeps that water clear), then a hard landing that capsizes a crew that isn't braced. Slower
    /// rafts just roll up and drop off the end. The raft's height is a pure function of where it is and how fast it's
    /// going (<see cref="Height"/>), so joined players draw the same jump from the snapshots. Spot: <c>across</c>.
    /// </summary>
    public sealed class JumpRamp : Obstacle, ISpotted
    {
        public const float Length = 3.4f; // the ramp, along the river
        public const float HalfWidth = 2f;
        public const float Lip = 1.1f; // its top, above the water
        public const float TakeOffSpeed = 6f; // metres a second down the river, to fly
        public const float AngleDeg = 20f;
        public const float Flight = 14f; // room past the lip a flight needs
        const float FlightLane = HalfWidth + 0.6f;
        const float Gravity = 9.8f;
        const float Boost = 70f;
        const float LandKnock = 110f;
        const float LandHeel = 9f; // like a hippo coming up under you: over you go, unless braced

        readonly ObstacleSpot spot;
        readonly RiverSample at;
        readonly Dictionary<Boat, double> flying = new();

        public JumpRamp(Course course, ObstacleSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            Foot = at.Centre + at.Left * spot.across - at.Tangent * (Length * 0.5f);
        }

        public ObstacleSpot Spot => spot;
        public RiverSample At => at;

        /// <summary>The ramp's bottom edge, in the middle of its width.</summary>
        public Vector2 Foot { get; }

        /// <summary>
        /// How high a raft here, going this fast, is above the water: up the ramp, then along a flight from its lip (at
        /// <see cref="AngleDeg"/> if it took off fast enough, falling straight off the end if not). 0 anywhere else.
        /// </summary>
        public float Height(Vector2 position, Vector2 velocity)
        {
            var offset = position - Foot;
            var along = Vector2.Dot(offset, at.Tangent);
            if (along < 0f || along > Length + Flight) return 0f;
            var across = Mathf.Abs(Vector2.Dot(offset, at.Left));
            if (along <= Length) return across > HalfWidth + 0.3f ? 0f : Lip * along / Length;
            var speed = Vector2.Dot(velocity, at.Tangent);
            if (across > FlightLane || speed < 1f) return 0f;
            var angle = speed >= TakeOffSpeed ? AngleDeg * Mathf.Deg2Rad : 0f;
            var t = (along - Length) / (speed * Mathf.Cos(angle));
            return Mathf.Max(0f, Lip + speed * Mathf.Sin(angle) * t - 0.5f * Gravity * t * t);
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            foreach (var boat in boats)
            {
                if (!Afloat(boat))
                {
                    flying.Remove(boat);
                    continue;
                }

                var raft = boat.Body.Position;
                var velocity = boat.Body.Velocity;
                if (flying.TryGetValue(boat, out var since))
                {
                    // Down again: a hard slap on the water.
                    if (seconds - since > 3.0) flying.Remove(boat);
                    else if (seconds - since > 0.15 && Height(raft, velocity) <= 0f)
                    {
                        // One side of the tube hits the water first: a lurch sideways (a raft pitches much less than it rolls).
                        flying.Remove(boat);
                        var side = Vector2.Dot(raft - Foot, at.Left) >= 0f ? 1f : -1f;
                        boat.Shove(at.Left * (side * LandKnock), LandHeel);
                    }

                    continue;
                }

                var offset = raft - Foot;
                var along = Vector2.Dot(offset, at.Tangent);
                if (along < Length - 0.5f || along > Length + 0.5f || Mathf.Abs(Vector2.Dot(offset, at.Left)) > HalfWidth + 0.3f) continue;
                if (Vector2.Dot(velocity, at.Tangent) < TakeOffSpeed || !Ready(boat, seconds, gap: 1.5)) continue;
                boat.Push(at.Tangent * Boost);
                flying[boat] = seconds;
            }
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            Box(lines, Foot + at.Tangent * (Length * 0.5f), at.Tangent, Length * 0.5f, HalfWidth);
        }

        /// <summary>Brace for the landing: just before the lip and all through the flight.</summary>
        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            // In the air, braced for the landing however far it flies (a fast raft goes further than the ramp's room).
            foreach (var boat in flying.Keys)
                if ((boat.Body.Position - position).sqrMagnitude < 1f) return true;
            var offset = position - Foot;
            var along = Vector2.Dot(offset, at.Tangent);
            return along > Length - 2f && along < Length + Flight * 0.85f && Mathf.Abs(Vector2.Dot(offset, at.Left)) < FlightLane;
        }
    }
}
