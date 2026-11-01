using System.Collections.Generic;
using System.Linq;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A beaver dam of sticks right across the river with two gaps in it (three on wide water). The beavers keep one gap
    /// open at a time, plugging it and opening the next in turn. Aim for the open one, and don't dawdle: it's about to
    /// shut.
    /// </summary>
    public sealed class BeaverDam : Obstacle
    {
        public const float Thickness = 0.8f;
        public const float WingLength = 6f;

        readonly AcrossSpot spot;
        readonly RiverSample at;
        readonly float[] gaps;
        readonly Collider2D[] gates;
        readonly Collider2D[] pieces;

        public BeaverDam(Course course, Transform parent, AcrossSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            var half = at.HalfWidth;
            if (half >= 8f)
            {
                gaps = new[] { -0.62f * half, 0f, 0.62f * half };
                GapWidth = Mathf.Min(5f, 0.62f * half - 0.8f);
            }
            else
            {
                gaps = new[] { -0.5f * half, 0.5f * half };
                GapWidth = Mathf.Min(5f, half - 0.6f);
            }

            // The dam: solid pieces between the gaps, and out past both edges.
            var angle = Degrees(at.Left);
            var dam = Kinematic("Beaver dam", at.Centre, parent);
            dam.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            var edges = new List<float> { -half - 4f };
            foreach (var gap in gaps)
            {
                edges.Add(gap - GapWidth * 0.5f);
                edges.Add(gap + GapWidth * 0.5f);
            }

            edges.Add(half + 4f);
            for (var i = 0; i < edges.Count; i += 2)
            {
                var piece = dam.gameObject.AddComponent<BoxCollider2D>();
                piece.sharedMaterial = Slick;
                piece.size = new Vector2(edges[i + 1] - edges[i], Thickness);
                piece.offset = new Vector2((edges[i] + edges[i + 1]) * 0.5f, 0f);
            }

            // Wing walls along both lips just upstream (local y runs upstream), so a raft sliding along the dam can't slip off the edge.
            foreach (var side in new[] { -1f, 1f })
            {
                var wing = dam.gameObject.AddComponent<BoxCollider2D>();
                wing.sharedMaterial = Slick;
                wing.size = new Vector2(1f, WingLength);
                wing.offset = new Vector2(side * (half + 0.5f), WingLength * 0.5f);
            }

            pieces = dam.GetComponents<Collider2D>();
            gates = new Collider2D[gaps.Length];
            for (var i = 0; i < gaps.Length; i++)
            {
                var gate = Kinematic($"Dam gate {i}", at.Centre + at.Left * gaps[i], parent);
                gate.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                var box = gate.gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(GapWidth, Thickness);
                box.sharedMaterial = Slick;
                box.isTrigger = true;
                gates[i] = box;
            }
        }

        public override IEnumerable<Collider2D> Barriers => pieces.Concat(gates);

        public AcrossSpot Spot => spot;
        public RiverSample At => at;

        /// <summary>Where across the river each gap is (positive is the left).</summary>
        public IReadOnlyList<float> Gaps => gaps;

        public float GapWidth { get; }

        /// <summary>Which gap is open (by index), and how far through its turn it is (0 just opened, 1 shutting).</summary>
        public int OpenGap(double seconds, out float closing)
        {
            var turn = Phase(seconds, spot.period, spot.offset) * gaps.Length;
            var open = Mathf.Min(gaps.Length - 1, (int)turn);
            closing = turn - open;
            return open;
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var open = OpenGap(seconds, out _);
            for (var i = 0; i < gaps.Length; i++)
            {
                // A gate only plugs once nothing is in its gap (shutting on a raft would fling it off).
                var inGap = false;
                foreach (var boat in boats)
                {
                    var offset = boat.Body.Position - at.Centre;
                    if (Mathf.Abs(Vector2.Dot(offset, at.Tangent)) < Thickness * 0.5f + RaftRadius &&
                        Mathf.Abs(Vector2.Dot(offset, at.Left) - gaps[i]) < GapWidth * 0.5f + RaftRadius) inGap = true;
                }

                gates[i].isTrigger = i == open || (gates[i].isTrigger && inGap);
            }

            foreach (var boat in boats) AlongTheWall(boat, at, Thickness, gaps[open], GapWidth, dt);
        }

        /// <summary>Aims for the gap that'll be open when the raft gets there.</summary>
        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (from > spot.distance + 1f || spot.distance - from > 20f) return across;
            // Time to get there: down the river, or across to the gap if that's further.
            var open = OpenGap(seconds, out _);
            var eta = Mathf.Clamp(Mathf.Max((spot.distance - from) / 6f, Mathf.Abs(gaps[open] - across) / 2.5f), 0f, 3f);
            open = OpenGap(seconds + eta, out var closing);
            if (closing > 0.75f) open = (open + 1) % gaps.Length; // that one shuts before we're through: wait at the next
            var slack = Mathf.Max(0f, GapWidth * 0.5f - RaftRadius - 0.2f);
            return Mathf.Clamp(Mathf.Clamp(across, gaps[open] - slack, gaps[open] + slack), -limit, limit);
        }
    }
}
