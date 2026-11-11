using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A tidal bore: a wall of white water that swells up at the bottom of one stretch, then rolls up the river right
    /// across it, against the flow, and dies away at the top. Whatever it rolls over is thrown back and heeled hard: meet
    /// it braced and you ride over it. It only hits while it's rolling. Spot: <c>period</c>, <c>offset</c>.
    /// </summary>
    public sealed class TidalBore : Obstacle, ISpotted
    {
        public const float Run = 12f; // metres either side of its spot it rolls
        public const float Build = 0.22f; // swelling at the bottom until here
        public const float Roll = 0.62f; // has rolled all the way up by here
        public const float Fade = 0.72f; // gone by here
        public const float Front = 1.1f; // half the wave's thickness
        const float Knock = 100f;
        const float Heel = 7.5f;

        readonly ObstacleSpot spot;

        public TidalBore(Course course, ObstacleSpot spot) : base(course) => this.spot = spot;

        public ObstacleSpot Spot => spot;

        public bool Rolling(double seconds)
        {
            var phase = Phase(seconds, spot.period, spot.offset);
            return phase >= Build && phase < Roll;
        }

        /// <summary>Where its front is (metres down the river) and how tall it stands, 0 to 1.</summary>
        public (float distance, float height) Wave(double seconds)
        {
            var phase = Phase(seconds, spot.period, spot.offset);
            if (phase < Build) return (spot.distance + Run, Smooth(phase / Build));
            if (phase < Roll) return (spot.distance + Run - 2f * Run * (phase - Build) / (Roll - Build), 1f);
            if (phase < Fade) return (spot.distance - Run, 1f - (phase - Roll) / (Fade - Roll));
            return (spot.distance + Run, 0f);
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            if (!Rolling(seconds)) return;
            var (front, _) = Wave(seconds);
            foreach (var boat in boats)
            {
                if (!Afloat(boat)) continue;
                var here = River.Project(boat.Body.Position);
                if (Mathf.Abs(here.Distance - front) > Front + RaftRadius * 0.5f || !Ready(boat, seconds, gap: 1.5)) continue;
                boat.Shove(-here.Tangent * Knock, Heel);
            }
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            if (!Rolling(seconds)) return;
            var at = River.At(Wave(seconds).distance);
            Box(lines, at.Centre, at.Tangent, Front - RaftRadius * 0.5f, at.HalfWidth);
        }

        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            var here = River.Project(position).Distance;
            if (here < spot.distance - Run - 3f || here > spot.distance + Run + 3f) return false;
            const float Reach = Front + RaftRadius + 0.8f;
            for (var t = 0f; t <= lookahead; t += 0.05f)
            {
                if (!Rolling(seconds + t)) continue;
                var ahead = Wave(seconds + t).distance - here; // the raft gets there at anything up to ApproachSpeed
                if (ahead > -Reach && ahead < ApproachSpeed * t + Reach) return true;
            }

            return false;
        }
    }
}
