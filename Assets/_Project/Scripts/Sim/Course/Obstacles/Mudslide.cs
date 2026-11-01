using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A slope above one side that lets go every so often: a trickle and a rumble (the warning), then thick mud pours
    /// into the river, spreading a little past its middle. A raft in it is shoved toward the far side and bogged down
    /// while it flows. Keep to the far side when it rumbles.
    /// </summary>
    public sealed class Mudslide : Obstacle
    {
        public const float Along = 4f; // half the length of river it covers
        const float Rumble = 0.3f;
        const float Pour = 0.45f;
        const float Stop = 0.8f;
        const float PushAcross = 120f; // newtons, toward the far side
        const float Bog = 1.2f; // per second, of the raft's speed
        const float RaftMass = 60f;

        readonly SideSpot spot;
        readonly RiverSample at;

        public Mudslide(Course course, SideSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
        }

        public SideSpot Spot => spot;
        public RiverSample At => at;

        /// <summary>How far it reaches across from its side's edge, at full flow.</summary>
        public float Reach => 1.1f * at.HalfWidth;

        /// <summary>How much mud is pouring (0 none, 1 in full flow), and how loud the rumble before it is (0 to 1).</summary>
        public float Flow(double seconds, out float rumble)
        {
            var phase = Phase(seconds, spot.period, spot.offset);
            rumble = phase >= Rumble && phase < Pour ? (phase - Rumble) / (Pour - Rumble) : 0f;
            return phase < Pour ? 0f : phase < Stop ? Mathf.Min(1f, (phase - Pour) / 0.05f) : Mathf.Max(0f, 1f - (phase - Stop) / 0.08f);
        }

        bool InMud(Vector2 point, float reach)
        {
            var offset = point - at.Centre;
            var fromEdge = at.HalfWidth - Vector2.Dot(offset, at.Left) * spot.side; // 0 at its own edge
            return Mathf.Abs(Vector2.Dot(offset, at.Tangent)) < Along + RaftRadius * 0.5f && fromEdge < reach + RaftRadius * 0.5f;
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var flow = Flow(seconds, out _);
            if (flow <= 0f) return;
            foreach (var boat in boats)
            {
                if (!Afloat(boat) || !InMud(boat.Body.Position, Reach * flow)) continue;
                boat.Push((-spot.side * at.Left * PushAcross - boat.Body.Velocity * (Bog * RaftMass)) * dt);
            }
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            var flow = Flow(seconds, out _);
            if (flow <= 0f) return;
            var reach = Reach * flow - RaftRadius * 0.5f;
            Box(lines, at.Centre + at.Left * (spot.side * (at.HalfWidth - reach * 0.5f)), at.Tangent, Along - RaftRadius * 0.5f, reach * 0.5f);
        }

        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (!Between(spot.distance, Along + RaftRadius, from, to)) return across;
            if (Flow(seconds + 1.0, out var rumble) <= 0f && rumble <= 0f && Flow(seconds, out var now) <= 0f && now <= 0f) return across;
            var safe = spot.side * (at.HalfWidth - Reach - RaftRadius - 0.3f);
            return (across - safe) * spot.side > 0f ? Mathf.Clamp(safe, -limit, limit) : across;
        }
    }
}
