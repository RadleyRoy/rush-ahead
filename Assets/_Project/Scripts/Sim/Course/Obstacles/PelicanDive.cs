using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A pelican circling above one stretch of river. Each time round it picks a spot (its shadow grows on the water:
    /// the warning), then plunges into it, knocking over any raft there unless the crew braces. A different spot each
    /// dive, but the same on every device: it's worked out from the clock.
    /// </summary>
    public sealed class PelicanDive : Obstacle
    {
        public const float Splash = 2.2f;
        public const float Shadow = 0.35f;
        public const float Dive = 0.6f;
        public const float Done = 0.66f;
        const float Knock = 90f;
        const float Heel = 8f;

        readonly SideSpot spot;
        readonly RiverSample at;

        public PelicanDive(Course course, SideSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
        }

        public SideSpot Spot => spot;
        public RiverSample At => at;

        /// <summary>How far round its circuit it is, 0 to 1: shadow from <see cref="Shadow"/>, plunge at <see cref="Dive"/>, back up from <see cref="Done"/>.</summary>
        public float Circuit(double seconds) => Phase(seconds, spot.period, spot.offset);

        /// <summary>Where this dive lands, how dark its shadow is (0 to 1), and how far it has dropped (0 up high, 1 in the water).</summary>
        public (Vector2 target, float shadow, float plunge) Pose(double seconds)
        {
            var cycle = (long)System.Math.Floor((seconds + spot.offset) / spot.period);
            var phase = Phase(seconds, spot.period, spot.offset);
            var target = at.Centre + at.Left * (Pick(cycle) * Mathf.Max(0f, at.HalfWidth - 1.5f));
            var shadow = phase < Shadow ? 0f : phase < Dive ? (phase - Shadow) / (Dive - Shadow) : phase < Done ? 1f : 0f;
            var plunge = phase >= Dive - 0.08f && phase < Done ? Mathf.InverseLerp(Dive - 0.08f, Dive, phase) : 0f;
            return (target, shadow, plunge);
        }

        /// <summary>A steady pseudo-random number from -1 to 1 for each dive, in integer arithmetic so every device agrees.</summary>
        float Pick(long cycle)
        {
            unchecked
            {
                var h = (uint)cycle * 2654435761u ^ (uint)Mathf.RoundToInt(spot.distance * 16f) * 2246822519u;
                h ^= h >> 15;
                h *= 3266489917u;
                h ^= h >> 13;
                return (h >> 8) / 8388608f - 1f;
            }
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var phase = Phase(seconds, spot.period, spot.offset);
            if (phase < Dive || phase >= Dive + 0.03f) return;
            var (target, _, _) = Pose(seconds);
            foreach (var boat in boats)
            {
                var offset = boat.Body.Position - target;
                if (!Afloat(boat) || offset.magnitude > Splash + RaftRadius * 0.5f || !Ready(boat, seconds, gap: 1.5)) continue;
                boat.Shove((offset.sqrMagnitude > 1e-4f ? offset.normalized : at.Tangent) * Knock, Heel);
            }
        }

        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            var (target, shadow, _) = Pose(seconds);
            if (shadow <= 0f || Vector2.Distance(position, target) > Splash + RaftRadius + 0.5f) return false;
            return Phase(seconds + lookahead, spot.period, spot.offset) >= Dive - 0.05f;
        }

        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (!Between(spot.distance, Splash + RaftRadius + 1f, from, to)) return across;
            var (target, shadow, _) = Pose(seconds);
            return shadow > 0f ? Clear(across, River.Project(target).Lateral, Splash + RaftRadius + 0.3f, limit) : across;
        }
    }
}
