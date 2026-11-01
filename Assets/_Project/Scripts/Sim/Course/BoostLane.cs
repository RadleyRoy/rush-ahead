using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A boost lane: a strip of glowing chevrons on the water. A raft riding it gets a surge of speed down the river,
    /// up to a limit above the water's own speed. Dealt with the round's layout into free water between obstacles, so
    /// it's the same on every device and nothing about it goes over the network.
    /// </summary>
    public sealed class BoostLane
    {
        public const float Length = 9f;
        public const float HalfWidth = 1.5f;
        const float Surge = 9f; // metres per second, per second, while on it
        const float AboveWater = 8f; // it won't push a raft past the water's speed plus this
        const float BendGrip = 7f; // nor faster than the next bend can be taken (metres per second squared round it)
        const float LookAhead = 45f;
        const float RaftMass = 60f;

        readonly River river;

        public BoostLane(Course course, BoostSpot spot)
        {
            river = course.River;
            Spot = spot;
            At = river.At(spot.distance + Length * 0.5f);
            var bend = 0.001f;
            for (var d = spot.distance; d <= spot.distance + Length + LookAhead; d += 3f) bend = Mathf.Max(bend, Mathf.Abs(river.At(d).Curvature));
            SpeedLimit = Mathf.Sqrt(BendGrip / bend);
        }

        /// <summary>The fastest it pushes a raft: what the bend after it allows.</summary>
        public float SpeedLimit { get; }

        public BoostSpot Spot { get; }

        /// <summary>Its middle.</summary>
        public RiverSample At { get; }

        public Vector2 Middle => At.Centre + At.Left * Spot.across;

        /// <summary>Whether a raft whose middle is at <paramref name="point"/> is riding it.</summary>
        public bool Riding(Vector2 point)
        {
            var offset = point - Middle;
            return Mathf.Abs(Vector2.Dot(offset, At.Tangent)) < Length * 0.5f + 0.5f && Mathf.Abs(Vector2.Dot(offset, At.Left)) < HalfWidth + 0.6f;
        }

        public void Step(float dt, IReadOnlyList<Boat> boats, System.Func<Vector2, Vector2> water)
        {
            foreach (var boat in boats)
            {
                if (boat.State != BoatState.Afloat || !Riding(boat.Body.Position)) continue;
                var limit = Mathf.Min(Vector2.Dot(water(boat.Body.Position), At.Tangent) + AboveWater, SpeedLimit);
                if (Vector2.Dot(boat.Body.Velocity, At.Tangent) >= limit) continue;
                boat.Push(At.Tangent * (Surge * RaftMass * dt));
            }
        }

        /// <summary>For AI crews: take the lane when it's coming up and not far off the line they're on.</summary>
        public float SteerAcross(float from, float across, float limit)
        {
            var ahead = Spot.distance - from;
            if (ahead < -Length || ahead > 26f || Mathf.Abs(across - Spot.across) > 4.5f) return across;
            return Mathf.Clamp(Spot.across, -limit, limit);
        }
    }
}
