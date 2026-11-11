using System.Collections.Generic;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// The water's height where it isn't flat: down into a whirlpool's funnel. Rafts, barrels and logs ride it, so a
    /// raft caught in a whirlpool visibly sinks into the bowl and swings round it. Set by the course on screen.
    /// </summary>
    public static class WaterSurface
    {
        static readonly List<(Vector2 centre, float radius, float depth)> funnels = new();
        static readonly List<JumpRamp> ramps = new();

        /// <summary>How deep a funnel of this radius dips at its eye.</summary>
        public static float FunnelDepth(float radius) => Mathf.Min(1.5f, 0.24f * radius);

        /// <summary>The funnel's surface, <paramref name="r"/> metres from its middle: flat at the rim, steepening into the eye.</summary>
        public static float FunnelHeight(float r, float radius, float depth)
        {
            if (r >= radius) return 0f;
            var k = 1f - r / radius;
            return -depth * k * k;
        }

        public static void SetFunnels(IEnumerable<WhirlpoolSpot> spots)
        {
            funnels.Clear();
            foreach (var spot in spots) funnels.Add((spot.position, spot.radius, FunnelDepth(spot.radius)));
        }

        /// <summary>The course's jump ramps, which lift a raft up them and through the air beyond.</summary>
        public static void SetRamps(IEnumerable<JumpRamp> spots)
        {
            ramps.Clear();
            ramps.AddRange(spots);
        }

        public static void Clear()
        {
            funnels.Clear();
            ramps.Clear();
        }

        /// <summary>The height a raft rides at here, going this fast: the water's, or up a jump ramp and through the air past it.</summary>
        public static float Height(Vector2 point, Vector2 velocity)
        {
            var height = Height(point);
            foreach (var ramp in ramps) height = Mathf.Max(height, ramp.Height(point, velocity));
            return height;
        }

        /// <summary>The water's height at a point on the sim plane (0 almost everywhere).</summary>
        public static float Height(Vector2 point)
        {
            var height = 0f;
            foreach (var (centre, radius, depth) in funnels)
                height = Mathf.Min(height, FunnelHeight(Vector2.Distance(point, centre), radius, depth));
            return height;
        }
    }
}
