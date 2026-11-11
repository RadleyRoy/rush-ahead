using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A tree on the lip. Once per race, a little before the leaders get there, it creaks and shakes (the warning),
    /// topples across the river and stays down for the rest of the race as a barrier with a gap past its crown. A raft
    /// caught under it as it lands goes over unless the crew braces; once it's down, steer for the gap. Between races
    /// (and before the first) it stands. When it falls is set from the race's start and how far down the river it is,
    /// so every device agrees from the clock alone.
    /// </summary>
    public sealed class FallingTree : Obstacle
    {
        public const float TrunkWidth = 0.8f;
        /// <summary>Seconds of creaking and shaking before it goes.</summary>
        public const float CreakSeconds = 1.4f;
        /// <summary>Seconds from starting to fall to lying flat.</summary>
        public const float FallSeconds = 0.55f;
        const float Knock = 120f;
        const float Heel = 9f;

        readonly SideSpot spot;
        readonly RiverSample at;
        readonly BoxCollider2D trunk;

        public FallingTree(Course course, Transform parent, SideSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            Length = 0.9f * at.HalfWidth + 0.5f; // from the lip to a little short of the middle, leaving the far side open
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

        /// <summary>Where its crown ends, across the river (positive is the left).</summary>
        public float CrownAcross => spot.side * (at.HalfWidth + 0.5f - Length);

        /// <summary>The middle of the open water past its crown, across the river (positive is the left).</summary>
        public float GapCentre => -spot.side * (at.HalfWidth - 0.5f * (2f * at.HalfWidth + 0.5f - Length));

        /// <summary>
        /// When it starts to fall on the race clock: a little before a fast crew could be there (at the approach speed,
        /// less a second and a half and a little more by the spot's offset), never sooner than 2.5 s after the start;
        /// NaN when no race is on.
        /// </summary>
        public double FallsAt
        {
            get
            {
                var start = Owner?.RaceStart ?? double.NaN;
                if (double.IsNaN(start)) return double.NaN;
                var lead = (spot.distance - Course.Def.startDistance) / ApproachSpeed - 1.5f - Mathf.Repeat(spot.offset, 1f);
                return start + Mathf.Max(2.5f, lead);
            }
        }

        /// <summary>When it lands flat across the river (NaN when no race is on).</summary>
        public double LandsAt => FallsAt + FallSeconds;

        /// <summary>How far over it is (0 standing, 1 flat across the river) and how hard it shakes.</summary>
        public (float tilt, float shake) Pose(double seconds)
        {
            var falls = FallsAt;
            if (double.IsNaN(falls) || seconds < falls - CreakSeconds) return (0f, 0f);
            if (seconds < falls) return (0f, (float)(1.0 - (falls - seconds) / CreakSeconds));
            var t = (float)((seconds - falls) / FallSeconds);
            return (t >= 1f ? 1f : t * t, 0f);
        }

        bool Lying(double seconds) => !double.IsNaN(FallsAt) && seconds >= LandsAt;

        /// <summary>Whether a raft's middle is under the trunk's line, give or take <paramref name="margin"/>.</summary>
        bool Under(Vector2 point, float margin)
        {
            var offset = point - Middle;
            return Mathf.Abs(Vector2.Dot(offset, at.Tangent)) < TrunkWidth * 0.5f + RaftRadius + margin &&
                   Mathf.Abs(Vector2.Dot(offset, at.Left)) < Length * 0.5f + margin;
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var lands = LandsAt;
            var landing = seconds >= lands - 0.05 && seconds < lands + 0.12;
            var overlapping = false;
            foreach (var boat in boats)
            {
                if (Under(boat.Body.Position, RaftRadius)) overlapping = true;
                if (!landing || !Afloat(boat) || !Under(boat.Body.Position, 0f) || !Ready(boat, seconds, gap: 2.0)) continue;
                var away = Vector2.Dot(boat.Body.Position - Middle, at.Tangent) >= 0f ? at.Tangent : -at.Tangent;
                boat.Shove(away * Knock, Heel);
            }

            // Solid while it lies across the water, but never switched solid on top of a raft (that would fling it off).
            var lying = Lying(seconds);
            trunk.isTrigger = !lying || (trunk.isTrigger && overlapping);
            if (!lying) return;
            var gapWidth = 2f * at.HalfWidth + 0.5f - Length;
            foreach (var boat in boats) AlongTheWall(boat, at, TrunkWidth, GapCentre, gapWidth, dt);
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            var (tilt, _) = Pose(seconds);
            if (tilt > 0f) Box(lines, Middle, at.Left, Length * 0.5f - RaftRadius, TrunkWidth * 0.5f);
        }

        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            if (!Under(position, 1f)) return false;
            var falls = FallsAt;
            return !double.IsNaN(falls) && seconds + lookahead >= falls && seconds < LandsAt + 0.15;
        }

        /// <summary>
        /// Once it's creaking (or down), make for the gap past its crown, lining up well before it (fast water leaves no
        /// time to cross the river at the last moment).
        /// </summary>
        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            const float LineUp = 40f; // metres ahead, three seconds or so at speed
            var ahead = spot.distance - from;
            if (ahead < -(TrunkWidth + RaftRadius + 1f) || ahead > Mathf.Max(LineUp, to - from + TrunkWidth + RaftRadius + 1f)) return across;
            var falls = FallsAt;
            if (double.IsNaN(falls) || seconds + 1.0 < falls - CreakSeconds) return across;
            // Just past the crown, not out by the far edge: there's no bank there to stop a raft going over.
            var safe = CrownAcross - spot.side * (RaftRadius + 1.2f);
            return (across - safe) * spot.side > 0f ? Mathf.Clamp(safe, -limit, limit) : across;
        }
    }
}
