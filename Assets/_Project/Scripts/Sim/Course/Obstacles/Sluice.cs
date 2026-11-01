using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A timber sluice gate: a wall right across the river with one gap in it, sliding from side to side on a
    /// timetable. Through the gap and away; miss it and the raft slams into the boards with the river pushing behind.
    /// The wall is two long kinematic boards either side of the gap, reaching out past both edges.
    /// </summary>
    public sealed class Sluice : Obstacle
    {
        public const float Thickness = 0.7f;

        readonly SluiceSpot spot;
        readonly RiverSample at;
        readonly Rigidbody2D left;
        readonly Rigidbody2D right;
        readonly float boardLength;
        bool placed;

        public Sluice(Course course, Transform parent, SluiceSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            boardLength = 2f * at.HalfWidth + 8f;
            left = Board("Sluice board (left)", parent);
            right = Board("Sluice board (right)", parent);
            var (l, r) = Boards(0);
            left.position = l;
            left.transform.position = l;
            right.position = r;
            right.transform.position = r;
        }

        /// <summary>Where the middles of the two boards are.</summary>
        public (Vector2 left, Vector2 right) Boards(double seconds)
        {
            var gap = GapCentre(seconds);
            var half = spot.gap * 0.5f + boardLength * 0.5f;
            return (at.Centre + at.Left * (gap + half), at.Centre + at.Left * (gap - half));
        }

        public override IEnumerable<Collider2D> Barriers => new[] { left.GetComponent<Collider2D>(), right.GetComponent<Collider2D>() };

        public SluiceSpot Spot => spot;
        public RiverSample At => at;
        public float BoardLength => boardLength;

        /// <summary>How far the gap can slide either way from the middle.</summary>
        public float Range => Mathf.Max(0f, at.HalfWidth - spot.gap * 0.5f - 0.4f);

        /// <summary>The middle of the gap, in metres across the river (positive is the left).</summary>
        public float GapCentre(double seconds) => Range * Mathf.Sin(2f * Mathf.PI * Phase(seconds, spot.period, spot.offset));

        Rigidbody2D Board(string name, Transform parent)
        {
            var body = Kinematic(name, at.Centre, parent);
            body.transform.rotation = Quaternion.Euler(0f, 0f, Degrees(at.Left));
            var box = body.gameObject.AddComponent<BoxCollider2D>();
            box.sharedMaterial = Slick;
            box.size = new Vector2(boardLength, Thickness);
            return body;
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var (l, r) = Boards(seconds);
            if (!placed)
            {
                placed = true; // the clock may be anywhere when the course loads: jump there rather than sweep there
                left.position = l;
                right.position = r;
                return;
            }

            left.MovePosition(l);
            right.MovePosition(r);
            var gap = GapCentre(seconds);
            foreach (var boat in boats) AlongTheWall(boat, at, Thickness, gap, spot.gap, dt);
        }

        /// <summary>Aims for where the gap will be when the raft gets there (it's quick about it: a second or so ahead).</summary>
        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (from > spot.distance + 1f || spot.distance - from > 22f) return across; // lines up from well back: it's fast water
            var eta = Mathf.Clamp((spot.distance - from) / 6f, 0f, 1.5f);
            var target = GapCentre(seconds + eta);
            var slack = Mathf.Max(0f, spot.gap * 0.5f - RaftRadius - 0.2f);
            return Mathf.Clamp(Mathf.Clamp(across, target - slack, target + slack), -limit, limit);
        }
    }
}
