using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// Big flat floes of ice riding the current down a stretch of river, drifting from side to side as they go. They're
    /// solid and heavy: they block, and squeeze a raft against each other. Each one surfaces at the top of the stretch
    /// and sinks out of the way at the bottom.
    /// </summary>
    public sealed class IceFloes : Obstacle
    {
        public const float Length = 3f;
        public const float Width = 2.4f;
        public const float Run = 26f; // the stretch they ride down
        public const int Count = 2;

        readonly AcrossSpot spot;
        readonly Rigidbody2D[] bodies = new Rigidbody2D[Count];
        readonly Collider2D[] boxes = new Collider2D[Count];
        bool placed;

        public IceFloes(Course course, Transform parent, AcrossSpot spot) : base(course)
        {
            this.spot = spot;
            for (var i = 0; i < Count; i++)
            {
                var (position, angle, _) = Floe(i, 0);
                bodies[i] = Kinematic($"Ice floe {i}", position, parent);
                bodies[i].transform.rotation = Quaternion.Euler(0f, 0f, angle);
                var box = bodies[i].gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(Length, Width);
                box.sharedMaterial = Slick;
                box.isTrigger = true;
                boxes[i] = box;
            }
        }

        public override IEnumerable<Collider2D> Barriers => boxes;

        public AcrossSpot Spot => spot;

        /// <summary>A floe: where it is, which way it's turned (degrees), and how far out of the water (0 sunk, 1 floating).</summary>
        public (Vector2 position, float angleDeg, float up) Floe(int i, double seconds)
        {
            var progress = Phase(seconds + i * spot.period / Count, spot.period, spot.offset);
            var at = River.At(spot.distance + progress * Run);
            var swing = Mathf.Max(0f, at.HalfWidth - Width * 0.5f - 0.8f) * 0.7f;
            var across = swing * Mathf.Sin(2f * Mathf.PI * (progress * 0.75f + i * 0.5f));
            var up = Mathf.Clamp01(Mathf.Min(progress, 1f - progress) / 0.08f);
            var angle = Degrees(at.Tangent) + 15f * Mathf.Sin(2f * Mathf.PI * progress + i);
            return (at.Centre + at.Left * across, angle, up);
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            for (var i = 0; i < Count; i++)
            {
                var (position, angle, up) = Floe(i, seconds);
                if (!placed)
                {
                    bodies[i].position = position; // the clock may be anywhere when the course loads: jump there
                    bodies[i].rotation = angle;
                }
                else
                {
                    bodies[i].MovePosition(position);
                    bodies[i].MoveRotation(angle);
                }

                // Solid while it's floating, but never switched solid under a raft (surfacing under one would fling it off).
                var under = false;
                foreach (var boat in boats)
                    if (Vector2.Distance(boat.Body.Position, position) < RaftRadius + Length * 0.5f + 0.3f) under = true;
                boxes[i].isTrigger = up < 0.9f || (boxes[i].isTrigger && under);
            }

            placed = true;
        }

        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (!Between(spot.distance + Run * 0.5f, Run * 0.5f + RaftRadius, from, to)) return across;
            for (var i = 0; i < Count; i++)
            {
                var (position, _, up) = Floe(i, seconds);
                if (up < 0.5f) continue;
                var here = River.Project(position);
                if (here.Distance < from - Length || here.Distance > to + Length + RaftRadius) continue;
                var eta = Mathf.Clamp((here.Distance - from) / (ApproachSpeed - 4f), 0f, 1.5f);
                var (later, _, _) = Floe(i, seconds + eta);
                var reach = Width * 0.5f + RaftRadius + 0.6f;
                across = Clear(Clear(across, River.Project(later).Lateral, reach, limit), here.Lateral, reach, limit);
            }

            return across;
        }
    }
}
