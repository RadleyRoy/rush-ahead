using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A glacier's ice cliff on one bank. Every round it cracks, a block leans out and drops into the river, and the
    /// splash sends a wave racing across the river from that bank: a raft it reaches is thrown toward the far side and
    /// heeled hard (brace to ride it out). The block bobs by the bank and melts away. Spot: <c>side</c>, <c>period</c>.
    /// </summary>
    public sealed class CalvingGlacier : Obstacle, ISpotted
    {
        public const float Crack = 0.38f; // cracks show, the block shudders
        public const float Calve = 0.55f; // it falls
        public const float Splash = 0.63f; // into the water: the wave sets off
        public const float Along = 7f; // the wave's reach either way along the river
        public const float Front = 1.2f; // half the wave's thickness
        public const float WaveSpeed = 9f;
        const float Knock = 100f;
        const float Heel = 7.5f;

        readonly ObstacleSpot spot;
        readonly RiverSample at;

        public CalvingGlacier(Course course, ObstacleSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
        }

        public ObstacleSpot Spot => spot;
        public RiverSample At => at;

        public float IcePhase(double seconds) => Phase(seconds, spot.period, spot.offset);

        /// <summary>The wave's front (metres across, positive the left) and its strength, 0 to 1; strength 0 when there's none.</summary>
        public (float across, float strength) Wave(double seconds)
        {
            var phase = IcePhase(seconds);
            if (phase < Splash) return (spot.side * at.HalfWidth, 0f);
            var travelled = WaveSpeed * (phase - Splash) * spot.period;
            var width = 2f * at.HalfWidth + 1f;
            if (travelled > width) return (-spot.side * at.HalfWidth, 0f);
            return (spot.side * (at.HalfWidth + 0.5f - travelled), 1f - 0.4f * travelled / width);
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var (front, strength) = Wave(seconds);
            if (strength <= 0f) return;
            foreach (var boat in boats)
            {
                if (!Afloat(boat)) continue;
                var offset = boat.Body.Position - at.Centre;
                if (Mathf.Abs(Vector2.Dot(offset, at.Tangent)) > Along) continue;
                if (Mathf.Abs(Vector2.Dot(offset, at.Left) - front) > Front + RaftRadius * 0.5f || !Ready(boat, seconds, gap: 1.5)) continue;
                boat.Shove(at.Left * (-spot.side * Knock * strength), Heel);
            }
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            var (front, strength) = Wave(seconds);
            if (strength > 0f) Box(lines, at.Centre + at.Left * front, at.Left, Front - RaftRadius * 0.5f, Along);
        }

        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            var offset = position - at.Centre;
            if (Mathf.Abs(Vector2.Dot(offset, at.Tangent)) > Along + RaftRadius + ApproachSpeed * lookahead) return false;
            var lateral = Vector2.Dot(offset, at.Left);
            for (var t = 0f; t <= lookahead; t += 0.05f)
            {
                var (front, strength) = Wave(seconds + t);
                if (strength > 0f && Mathf.Abs(lateral - front) < Front + RaftRadius + 0.6f) return true;
            }

            return false;
        }
    }
}
