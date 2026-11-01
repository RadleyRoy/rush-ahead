using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A hippo lurking under the water. Bubbles rise where it is (the warning), then it surges up underneath, flipping
    /// any raft on top unless the crew braces, wallows a moment with its back out of the water, and sinks again.
    /// </summary>
    public sealed class Hippo : Obstacle
    {
        public const float Radius = 1.6f;
        const float Bubbles = 0.5f;
        const float Surge = 0.74f;
        const float Up = 0.8f;
        const float Sink = 0.93f;
        const float Shove = 110f;
        const float Heel = 9f;

        readonly HippoSpot spot;
        readonly Rigidbody2D body;

        public Hippo(Course course, Transform parent, HippoSpot spot) : base(course)
        {
            this.spot = spot;
            var at = River.At(spot.distance);
            Position = at.Centre + at.Left * spot.across;
            Heading = Degrees(at.Tangent);
            body = Kinematic("Hippo", Position, parent);
            var circle = body.gameObject.AddComponent<CircleCollider2D>();
            circle.radius = Radius;
            circle.isTrigger = true; // it surges up under rafts: a body switching solid with a raft on it would fling it off; the heave does the work
        }

        public Vector2 Position { get; }
        public float Heading { get; }

        /// <summary>How far up it is (0 under, 1 out of the water) and how hard the water above it bubbles (0 to 1).</summary>
        public (float rise, float bubbling) Pose(double seconds)
        {
            var phase = Phase(seconds, spot.period, spot.offset);
            var bubbling = phase >= Bubbles && phase < Up ? Mathf.InverseLerp(Bubbles, Surge, phase) : 0f;
            var rise = phase < Surge ? 0f : phase < Up ? Smooth((phase - Surge) / (Up - Surge)) : phase < Sink ? 1f : 1f - Smooth((phase - Sink) / (1f - Sink));
            return (rise, bubbling);
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var phase = Phase(seconds, spot.period, spot.offset);
            var (rise, _) = Pose(seconds);
            body.simulated = rise > 0.6f;

            if (phase < Surge || phase >= Up) return;
            foreach (var boat in boats)
            {
                if (!Afloat(boat)) continue;
                var offset = boat.Body.Position - Position;
                if (offset.magnitude > Radius + RaftRadius * 0.6f || !Ready(boat, seconds, gap: 2.0)) continue;
                boat.Shove((offset.sqrMagnitude > 1e-4f ? offset.normalized : Vector2.up) * Shove, Heel);
            }
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            if (Pose(seconds).bubbling > 0f || Pose(seconds).rise > 0f) Circle(lines, Position, Radius - RaftRadius * 0.4f);
        }

        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            if (Vector2.Distance(position, Position) > Radius + RaftRadius + 1f) return false;
            var (_, bubbling) = Pose(seconds);
            var soon = Phase(seconds + lookahead, spot.period, spot.offset);
            return bubbling > 0f || (soon >= Surge && soon < Up);
        }

        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (!Between(spot.distance, Radius + RaftRadius + 1f, from, to)) return across;
            var soon = Phase(seconds + 1.0, spot.period, spot.offset);
            return soon >= Bubbles - 0.05f && soon < Sink ? Clear(across, spot.across, Radius + RaftRadius + 0.4f, limit) : across;
        }
    }
}
