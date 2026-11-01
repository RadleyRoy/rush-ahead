using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A bear standing on the lip. It rears up (the warning), then swipes a huge paw over the water on its side,
    /// flinging any raft there across the river toward the far edge. Keep to the other side when it rears.
    /// </summary>
    public sealed class Bear : Obstacle
    {
        public const float Reach = 3.8f; // how far over the water its paw sweeps
        const float Rear = 0.45f;
        const float Swipe = 0.65f;
        const float Done = 0.73f;
        const float Fling = 330f;
        const float Heel = 5f;

        readonly BearSpot spot;
        readonly RiverSample at;

        public Bear(Course course, BearSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
        }

        public BearSpot Spot => spot;
        public RiverSample At => at;

        /// <summary>How far it has reared up (0 to 1) and how far through its swipe it is (0 to 1; 0 when not swiping).</summary>
        public (float rear, float swipe) Pose(double seconds)
        {
            var phase = Phase(seconds, spot.period, spot.offset);
            var rear = phase < Rear ? 0f : phase < Swipe ? Smooth((phase - Rear) / (Swipe - Rear)) : phase < Done ? 1f : 1f - Smooth((phase - Done) / (1f - Done));
            var swipe = phase >= Swipe && phase < Done ? (phase - Swipe) / (Done - Swipe) : 0f;
            return (rear, swipe);
        }

        bool InReach(Vector2 point, float margin)
        {
            var offset = point - at.Centre;
            var along = Vector2.Dot(offset, at.Tangent);
            var across = Vector2.Dot(offset, at.Left) * spot.side; // positive toward the bear
            return Mathf.Abs(along) < 2.5f + margin && across > at.HalfWidth - Reach - margin;
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var (_, swipe) = Pose(seconds);
            if (swipe <= 0f) return;
            foreach (var boat in boats)
            {
                if (!Afloat(boat) || !InReach(boat.Body.Position, RaftRadius * 0.7f) || !Ready(boat, seconds, gap: 1.5)) continue;
                boat.Shove((-spot.side * at.Left + at.Tangent * 0.15f) * Fling, Heel, spin: 1f);
            }
        }

        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (!Between(spot.distance, 2.5f + RaftRadius, from, to)) return across;
            var soon = Phase(seconds + 1.0, spot.period, spot.offset);
            if (soon < Rear - 0.1f || soon > Done) return across;
            var safe = spot.side * (at.HalfWidth - Reach - RaftRadius - 0.4f);
            return (across - safe) * spot.side > 0f ? Mathf.Clamp(safe, -limit, limit) : across;
        }
    }
}
