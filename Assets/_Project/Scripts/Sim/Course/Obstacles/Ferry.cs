using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A cable ferry: a heavy flat raft hauled across the river and back on a cable, pausing at each side. It's solid
    /// and doesn't give way: get caught in front of it and it shoves you toward the edge it's heading for.
    /// </summary>
    public sealed class Ferry : Obstacle
    {
        public const float Length = 2.8f; // along the river
        public const float Width = 4.4f; // across it

        readonly FerrySpot spot;
        readonly RiverSample at;
        readonly Rigidbody2D body;
        bool placed;

        public Ferry(Course course, Transform parent, FerrySpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            body = Kinematic("Ferry", Position(0), parent);
            body.transform.rotation = Quaternion.Euler(0f, 0f, Degrees(at.Tangent));
            var box = body.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(Length, Width);
            box.sharedMaterial = Slick; // a bump stops a raft rather than rolling it over
        }

        public FerrySpot Spot => spot;
        public RiverSample At => at;

        /// <summary>How far it can go either way from the middle: right up to the lip.</summary>
        public float Range => Mathf.Max(0f, at.HalfWidth - Width * 0.5f + 0.4f);

        /// <summary>Metres across the river (positive is the left): across, a pause, back, a pause.</summary>
        public float Across(double seconds)
        {
            var phase = Phase(seconds, spot.period, spot.offset);
            var t = phase < 0.4f ? Smooth(phase / 0.4f) : phase < 0.5f ? 1f : phase < 0.9f ? 1f - Smooth((phase - 0.5f) / 0.4f) : 0f;
            return Mathf.Lerp(-Range, Range, t);
        }

        public Vector2 Position(double seconds) => at.Centre + at.Left * Across(seconds);

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            if (placed)
            {
                body.MovePosition(Position(seconds));
                return;
            }

            placed = true; // the clock may be anywhere when the course loads: jump there rather than sweep there
            body.position = Position(seconds);
        }

        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (!Between(spot.distance, Length * 0.5f + RaftRadius + 1f, from, to)) return across;
            var eta = Mathf.Clamp((spot.distance - from) / ApproachSpeed, 0f, 1.5f);
            var reach = Width * 0.5f + RaftRadius + 0.5f;
            return Clear(Clear(across, Across(seconds + eta), reach, limit), Across(seconds), reach, limit);
        }
    }
}
