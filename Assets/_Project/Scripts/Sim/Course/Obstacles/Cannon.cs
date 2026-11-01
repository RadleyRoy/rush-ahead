using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A water cannon on the lip. Its jet points across the river and swings up and down it; a raft in the jet is
    /// pushed hard toward the far edge (and heeled over a little) for as long as it stays there. Bracing takes three
    /// quarters of it, like any knock, but a braced crew can't steer: AI crews blow against it instead.
    /// </summary>
    public sealed class Cannon : Obstacle
    {
        public const float JetRadius = 0.7f;
        const float Force = 170f; // at the muzzle; it eases off across the river
        const float JetHeel = 1.5f;

        readonly CannonSpot spot;
        readonly RiverSample at;

        public Cannon(Course course, CannonSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            Muzzle = at.Centre + at.Left * (spot.side * (at.HalfWidth + 0.9f));
            Reach = 2f * at.HalfWidth + 2f;
        }

        public CannonSpot Spot => spot;
        public Vector2 Muzzle { get; }
        public float Reach { get; }

        /// <summary>Which way the jet points on the water plane.</summary>
        public Vector2 Jet(double seconds)
        {
            var swing = spot.sweepDeg * Mathf.Sin(2f * Mathf.PI * Phase(seconds, spot.period, spot.offset)) * Mathf.Deg2Rad;
            var straight = -spot.side * at.Left;
            return Mathf.Cos(swing) * straight + Mathf.Sin(swing) * at.Tangent;
        }

        bool InJet(Vector2 point, Vector2 jet, float margin)
        {
            var offset = point - Muzzle;
            var along = Vector2.Dot(offset, jet);
            if (along < 0f || along > Reach) return false;
            return Mathf.Abs(Vector2.Dot(offset, new Vector2(-jet.y, jet.x))) < JetRadius + margin;
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            var jet = Jet(seconds);
            var half = Mathf.Max(0.05f, JetRadius + RaftRadius * 0.8f - RaftRadius);
            Box(lines, Muzzle + jet * (Reach * 0.5f), jet, Reach * 0.5f, half);
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var jet = Jet(seconds);
            foreach (var boat in boats)
            {
                if (!Afloat(boat) || !InJet(boat.Body.Position, jet, RaftRadius * 0.8f)) continue;
                var along = Vector2.Dot(boat.Body.Position - Muzzle, jet) / Reach;
                boat.Shove(jet * (Force * Mathf.Lerp(1f, 0.45f, along) * dt), JetHeel); // a little heel with each tick's push: it builds while the jet holds it
            }
        }
    }
}
