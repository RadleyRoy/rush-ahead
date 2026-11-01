using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A bed of electric eels right across the river. The water over them flickers (the warning), then pulses: a raft
    /// over the bed in a pulse gets a jolt and its crew can't blow for a moment, unless they're braced and holding on.
    /// </summary>
    public sealed class ElectricEels : Obstacle
    {
        public const float Depth = 3f; // how far up and down the river the bed reaches
        public const float StunSeconds = 1.3f;
        const float Flicker = 0.6f;
        const float Pulse = 0.82f;
        const float Calm = 0.95f;
        const float Jolt = 20f;
        const float Heel = 2f;

        readonly AcrossSpot spot;
        readonly RiverSample at;

        public ElectricEels(Course course, AcrossSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
        }

        public AcrossSpot Spot => spot;
        public RiverSample At => at;

        /// <summary>The warning flicker, rising from 0 to 1, and whether it's pulsing now.</summary>
        public float Warning(double seconds, out bool pulsing)
        {
            var phase = Phase(seconds, spot.period, spot.offset);
            pulsing = phase >= Pulse && phase < Calm;
            return pulsing ? 1f : phase >= Flicker && phase < Pulse ? (phase - Flicker) / (Pulse - Flicker) : 0f;
        }

        bool Over(Vector2 point, float margin) => Mathf.Abs(Vector2.Dot(point - at.Centre, at.Tangent)) < Depth * 0.5f + RaftRadius * 0.6f + margin;

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            Warning(seconds, out var pulsing);
            if (!pulsing) return;
            foreach (var boat in boats)
            {
                if (!Afloat(boat) || !Over(boat.Body.Position, 0f) || !Ready(boat, seconds)) continue;
                if (boat.BracedFraction < 0.5f) boat.StunnedSeconds = Mathf.Max(boat.StunnedSeconds, StunSeconds);
                boat.Shove(at.Left * (Jolt * (Mathf.PerlinNoise((float)seconds, spot.distance) - 0.5f)), Heel);
            }
        }

        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            if (!Over(position, 1.5f)) return false;
            for (var t = 0f; t <= lookahead; t += 0.1f)
            {
                Warning(seconds + t, out var pulsing);
                if (pulsing) return true;
            }

            return false;
        }
    }
}
