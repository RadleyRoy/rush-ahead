using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A water mill on one bank. Its race draws a strong side current toward the bank over a stretch either side of it,
    /// strongest close in and by the wheel; the turning wheel itself is solid and knocks a raft back out. The real danger
    /// is being drawn over the edge just past it: keep to the far side and blow against the pull. Spot: <c>side</c>,
    /// <c>period</c> (one turn of the wheel, for the view).
    /// </summary>
    public sealed class MillRace : Obstacle, ISpotted
    {
        public const float WheelRadius = 2.6f;
        public const float WheelWidth = 1.2f; // across the river
        public const float Along = 8f; // the pull's reach either way along the river
        const float Pull = 125f; // newtons at its strongest
        const float Knock = 85f;
        const float Heel = 4.5f;

        readonly ObstacleSpot spot;
        readonly RiverSample at;

        public MillRace(Course course, Transform parent, ObstacleSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            Wheel = at.Centre + at.Left * (spot.side * (at.HalfWidth - WheelWidth * 0.5f + 0.15f));
            var body = Kinematic("Mill wheel", Wheel, parent);
            body.transform.rotation = Quaternion.Euler(0f, 0f, Degrees(at.Tangent));
            var box = body.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(WheelRadius * 1.6f, WheelWidth);
            box.sharedMaterial = Slick;
        }

        public ObstacleSpot Spot => spot;
        public RiverSample At => at;

        /// <summary>The wheel's middle on the water.</summary>
        public Vector2 Wheel { get; }

        /// <summary>The wheel's turn, in degrees.</summary>
        public float WheelAngle(double seconds) => (float)(seconds / Mathf.Max(0.5f, spot.period) % 1.0) * 360f;

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            foreach (var boat in boats)
            {
                if (!Afloat(boat)) continue;
                var raft = boat.Body.Position;
                var offset = raft - at.Centre;
                var along = Vector2.Dot(offset, at.Tangent);
                if (Mathf.Abs(along) > Along) continue;
                var toBank = spot.side * Vector2.Dot(offset, at.Left); // +HalfWidth at the mill's bank
                var share = Mathf.Clamp01((toBank + at.HalfWidth) / (2f * at.HalfWidth));
                var strength = (1f - Mathf.Abs(along) / Along) * (0.35f + 0.65f * share);
                boat.Push(at.Left * (spot.side * Pull * strength * dt));

                if (Mathf.Abs(along) < WheelRadius * 0.8f + RaftRadius * 0.7f && toBank > at.HalfWidth - WheelWidth - RaftRadius * 0.8f && Ready(boat, seconds, gap: 0.8))
                    boat.Shove(at.Left * (-spot.side * Knock) + at.Tangent * 20f, Heel, 0.4f);
            }
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            Box(lines, Wheel, at.Tangent, WheelRadius * 0.8f, WheelWidth * 0.5f);
        }

        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (!Between(spot.distance, Along + RaftRadius, from, to)) return across;
            var far = -spot.side * limit * 0.55f; // keep to the far half
            return spot.side > 0f ? Mathf.Min(across, far) : Mathf.Max(across, far);
        }
    }
}
