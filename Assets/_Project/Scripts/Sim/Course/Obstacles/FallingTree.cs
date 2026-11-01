using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A tree on the lip. It creaks and shakes (the warning), topples across the river, and lies there as a barrier
    /// with a gap past its crown until the water takes it away. A raft caught under it as it lands goes over unless
    /// the crew braces; once it's down, steer for the gap.
    /// </summary>
    public sealed class FallingTree : Obstacle
    {
        public const float TrunkWidth = 0.8f;
        public const float Creak = 0.3f;
        public const float Fall = 0.45f;
        public const float Down = 0.53f;
        public const float Wash = 0.85f;
        const float Knock = 120f;
        const float Heel = 9f;

        readonly SideSpot spot;
        readonly RiverSample at;
        readonly BoxCollider2D trunk;

        public FallingTree(Course course, Transform parent, SideSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            Length = 1.15f * at.HalfWidth + 0.5f; // from the lip to a little past the middle, leaving the far side open
            var body = Kinematic("Fallen tree", Middle, parent);
            body.transform.rotation = Quaternion.Euler(0f, 0f, Degrees(at.Left));
            trunk = body.gameObject.AddComponent<BoxCollider2D>();
            trunk.size = new Vector2(Length, TrunkWidth);
            trunk.sharedMaterial = Slick;
            trunk.isTrigger = true;
        }

        public override IEnumerable<Collider2D> Barriers => new Collider2D[] { trunk };

        public SideSpot Spot => spot;
        public RiverSample At => at;
        public float Length { get; }

        /// <summary>Where its foot is, on the lip.</summary>
        public Vector2 Foot => at.Centre + at.Left * (spot.side * (at.HalfWidth + 0.5f));

        Vector2 Middle => Foot - at.Left * (spot.side * Length * 0.5f);

        /// <summary>The middle of the open water past its crown, across the river (positive is the left).</summary>
        public float GapCentre => -spot.side * (at.HalfWidth - 0.5f * (2f * at.HalfWidth + 0.5f - Length));

        /// <summary>How far over it is (0 standing, 1 flat across the river), how hard it shakes, and how far the water has carried it off.</summary>
        public (float tilt, float shake, float washed) Pose(double seconds)
        {
            var phase = Phase(seconds, spot.period, spot.offset);
            var shake = phase >= Creak && phase < Fall ? (phase - Creak) / (Fall - Creak) : 0f;
            var tilt = phase < Fall ? 0f : phase < Down ? Mathf.Pow((phase - Fall) / (Down - Fall), 2f) : 1f;
            var washed = phase >= Wash ? (phase - Wash) / (1f - Wash) : 0f;
            return (tilt, shake, washed);
        }

        /// <summary>Whether a raft's middle is under the trunk's line, give or take <paramref name="margin"/>.</summary>
        bool Under(Vector2 point, float margin)
        {
            var offset = point - Middle;
            return Mathf.Abs(Vector2.Dot(offset, at.Tangent)) < TrunkWidth * 0.5f + RaftRadius + margin &&
                   Mathf.Abs(Vector2.Dot(offset, at.Left)) < Length * 0.5f + margin;
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var phase = Phase(seconds, spot.period, spot.offset);
            var landing = phase >= Down - 0.01f && phase < Down + 0.02f;
            var overlapping = false;
            foreach (var boat in boats)
            {
                if (Under(boat.Body.Position, RaftRadius)) overlapping = true;
                if (!landing || !Afloat(boat) || !Under(boat.Body.Position, 0f) || !Ready(boat, seconds, gap: 2.0)) continue;
                var away = Vector2.Dot(boat.Body.Position - Middle, at.Tangent) >= 0f ? at.Tangent : -at.Tangent;
                boat.Shove(away * Knock, Heel);
            }

            // Solid while it lies across the water, but never switched solid on top of a raft (that would fling it off).
            var lying = phase >= Down && phase < Wash;
            trunk.isTrigger = !lying || (trunk.isTrigger && overlapping);
            if (!lying) return;
            var gapWidth = 2f * at.HalfWidth + 0.5f - Length;
            foreach (var boat in boats) AlongTheWall(boat, at, TrunkWidth, GapCentre, gapWidth, dt);
        }

        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            if (!Under(position, 1f)) return false;
            for (var t = 0f; t <= lookahead; t += 0.1f)
            {
                var phase = Phase(seconds + t, spot.period, spot.offset);
                if (phase >= Fall && phase < Down + 0.03f) return true;
            }

            return false;
        }

        /// <summary>Once it's creaking (or down), make for the gap past its crown.</summary>
        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (!Between(spot.distance, TrunkWidth + RaftRadius + 1f, from, to)) return across;
            var soon = Phase(seconds + 1.0, spot.period, spot.offset);
            var now = Phase(seconds, spot.period, spot.offset);
            return (soon >= Creak && soon < Wash) || (now >= Creak && now < Wash) ? Mathf.Clamp(GapCentre, -limit, limit) : across;
        }
    }
}
