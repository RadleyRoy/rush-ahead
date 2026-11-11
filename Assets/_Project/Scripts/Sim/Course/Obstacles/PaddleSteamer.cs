using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A big paddle steamer working up and down one half of the river, never faster than about 5 m/s. Its hull is solid,
    /// its turning paddle boxes knock a raft away hard, and its wake rocks rafts behind it from side to side. Go round
    /// on the other half. Spot: <c>across</c> (its lane), <c>period</c> (up and back).
    /// </summary>
    public sealed class PaddleSteamer : Obstacle, ISpotted
    {
        public const float Length = 9f;
        public const float Width = 3.4f;
        public const float Range = 10f; // either way along the river from its spot
        public const float Paddle = 1.2f; // how far each paddle box stands out of the hull
        public const float PaddleAlong = 1.3f; // half a paddle box's length
        public const float WakeLength = 9f;
        const float Knock = 95f;
        const float Heel = 4.5f;
        const float Wake = 40f;

        readonly ObstacleSpot spot;
        readonly RiverSample at;
        readonly Rigidbody2D body;
        bool placed;

        public PaddleSteamer(Course course, Transform parent, ObstacleSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            var (position, heading) = Pose(0);
            body = Kinematic("Paddle steamer", position, parent);
            body.transform.rotation = Quaternion.Euler(0f, 0f, Degrees(heading));
            var box = body.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(Length, Width);
            box.sharedMaterial = Slick; // a bump stops a raft rather than rolling it over
        }

        public ObstacleSpot Spot => spot;
        public RiverSample At => at;

        /// <summary>Its fastest, along the river (metres a second).</summary>
        public float TopSpeed => Range * 2f * Mathf.PI / Mathf.Max(0.1f, spot.period);

        float Turn(double seconds) => 2f * Mathf.PI * Phase(seconds, spot.period, spot.offset);

        /// <summary>Metres down the river from its spot (for turning the paddles as it goes).</summary>
        public float Offset(double seconds) => Range * Mathf.Sin(Turn(seconds));

        /// <summary>Metres a second along the river (positive downstream).</summary>
        public float Speed(double seconds) => TopSpeed * Mathf.Cos(Turn(seconds));

        /// <summary>Where its middle is and which way its bow points (always downstream: it backs up the river).</summary>
        public (Vector2 position, Vector2 heading) Pose(double seconds)
        {
            var sample = River.At(spot.distance + Offset(seconds));
            return (sample.Centre + sample.Left * spot.across, sample.Tangent);
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var (position, heading) = Pose(seconds);
            if (placed)
            {
                body.MovePosition(position);
                body.MoveRotation(Degrees(heading));
            }
            else
            {
                placed = true; // the clock may be anywhere when the course loads: jump there rather than sweep there
                body.position = position;
                body.rotation = Degrees(heading);
            }

            var side = Perpendicular(heading);
            var speed = Speed(seconds);
            var stern = -Mathf.Sign(speed == 0f ? 1f : speed) * heading; // the wake trails behind the way it's going
            foreach (var boat in boats)
            {
                if (!Afloat(boat)) continue;
                var offset = boat.Body.Position - position;
                var along = Vector2.Dot(offset, heading);
                var across = Vector2.Dot(offset, side);

                // The paddle boxes amidships, one each side.
                if (Mathf.Abs(along) < PaddleAlong + RaftRadius * 0.7f && Mathf.Abs(across) < Width * 0.5f + Paddle + RaftRadius * 0.7f &&
                    Mathf.Abs(across) > Width * 0.5f - 0.2f && Ready(boat, seconds, across > 0f ? 1 : 0))
                    boat.Shove(side * (Mathf.Sign(across) * Knock), Heel, 0.3f);

                // The wake: a raft behind it is rocked from side to side.
                var behind = Vector2.Dot(offset, stern) - Length * 0.5f;
                if (behind > 0f && behind < WakeLength && Mathf.Abs(across) < Width * 0.5f + 1.5f)
                {
                    var strength = Mathf.Abs(speed) / Mathf.Max(0.1f, TopSpeed) * (1f - behind / WakeLength);
                    boat.Push(side * (Mathf.Sin((float)seconds * 7f) * Wake * strength * dt));
                }
            }
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            var (position, heading) = Pose(seconds);
            var side = Perpendicular(heading);
            foreach (var s in new[] { -1f, 1f })
                Box(lines, position + side * (s * (Width * 0.5f + Paddle * 0.5f)), heading, PaddleAlong - RaftRadius * 0.3f, Paddle * 0.5f - RaftRadius * 0.3f);
        }

        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (!Between(spot.distance, Range + Length * 0.5f + RaftRadius + 1f, from, to)) return across;
            return Clear(across, spot.across, Width * 0.5f + Paddle + RaftRadius + 0.6f, limit);
        }
    }
}
