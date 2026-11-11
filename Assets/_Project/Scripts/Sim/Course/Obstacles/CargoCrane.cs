using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A crane on one bank beside a stack of crates. Each round its jib swings back over the stack, picks a crate, swings
    /// out over the river and holds it there while its shadow darkens on the water, then lets go: a raft under it is
    /// knocked aside and heeled hard. The crate bobs a moment and sinks. Where it drops comes from the clock.
    /// Spot: <c>side</c>, <c>period</c>.
    /// </summary>
    public sealed class CargoCrane : Obstacle, ISpotted
    {
        public const float Crate = 0.95f; // half a crate's side
        public const float Back = 0.12f; // the jib is over the stack
        public const float Pick = 0.18f; // and has the next crate
        public const float Out = 0.35f; // over the drop, the shadow growing
        public const float Drop = 0.72f; // lets go
        public const float Land = 0.77f; // in the water
        public const float Sunk = 0.97f;
        public const float Hang = 6.5f; // the crate's bottom, on the hook
        const float Window = 0.03f;
        const float Knock = 105f;
        const float Heel = 6.5f;

        readonly ObstacleSpot spot;
        readonly RiverSample at;
        readonly int seed;

        public CargoCrane(Course course, ObstacleSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            seed = Mathf.RoundToInt(spot.distance * 16f) + 131;
            Base = at.Centre + at.Left * (spot.side * (at.HalfWidth + 2.4f));
            Stack = Base - at.Tangent * 2.6f;
        }

        public ObstacleSpot Spot => spot;
        public RiverSample At => at;

        /// <summary>The crane's mast, on the bank.</summary>
        public Vector2 Base { get; }

        /// <summary>The stack of crates beside it.</summary>
        public Vector2 Stack { get; }

        public float CranePhase(double seconds) => Phase(seconds, spot.period, spot.offset);

        /// <summary>Where this round's crate comes down.</summary>
        public Vector2 Target(double seconds) => TargetOf(Round(seconds, spot.period, spot.offset));

        Vector2 TargetOf(int round)
        {
            var sample = River.At(spot.distance + (Hash(round, seed) * 2f - 1f) * 4f);
            return sample.Centre + sample.Left * (spot.side * Mathf.Lerp(sample.HalfWidth - 1.4f, -sample.HalfWidth * 0.35f, Hash(round, seed + 1)));
        }

        /// <summary>The point on the water under the hook.</summary>
        public Vector2 Hook(double seconds)
        {
            var phase = CranePhase(seconds);
            var round = Round(seconds, spot.period, spot.offset);
            if (phase < Back) return Vector2.Lerp(TargetOf(round - 1), Stack, Smooth(phase / Back));
            if (phase < Pick) return Stack;
            if (phase < Out) return Vector2.Lerp(Stack, TargetOf(round), Smooth((phase - Pick) / (Out - Pick)));
            return TargetOf(round);
        }

        /// <summary>This round's crate: where it is, its bottom's height (on the hook, falling, bobbing, sinking), and whether it's on the hook.</summary>
        public (Vector2 position, float height, bool hooked) CratePose(double seconds)
        {
            var phase = CranePhase(seconds);
            if (phase < Pick) return (Stack, -10f, false); // not picked yet: it's the top of the stack
            if (phase < Drop) return (Hook(seconds), Hang, true);
            var target = Target(seconds);
            if (phase < Land)
            {
                var t = (phase - Drop) / (Land - Drop);
                return (target, Hang * (1f - t * t), false);
            }

            return (target, -2.4f * Smooth(Mathf.Clamp01((phase - Land) / (Sunk - Land))), false);
        }

        /// <summary>How dark the crate's shadow on the water is, 0 to 1.</summary>
        public float Shadow(double seconds)
        {
            var phase = CranePhase(seconds);
            return phase >= Out && phase < Land ? Mathf.Clamp01((phase - Out) / (Drop - Out)) : 0f;
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var phase = CranePhase(seconds);
            if (phase < Land - 0.01f || phase >= Land - 0.01f + Window) return;
            var target = Target(seconds);
            foreach (var boat in boats)
            {
                var raft = boat.Body.Position;
                if (!Afloat(boat) || Vector2.Distance(raft, target) > Crate + RaftRadius * 0.7f || !Ready(boat, seconds, gap: 1.5)) continue;
                boat.Shove(Away(target, raft, at.Tangent) * Knock, Heel);
            }
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            if (Shadow(seconds) > 0f) Box(lines, Target(seconds), at.Tangent, Crate - RaftRadius * 0.3f, Crate - RaftRadius * 0.3f);
        }

        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            var phase = CranePhase(seconds);
            if (phase < Out || phase >= Land + Window || Vector2.Distance(position, Target(seconds)) > Crate + RaftRadius + 0.6f) return false;
            var later = CranePhase(seconds + lookahead);
            return later >= Land - 0.04f || later < phase;
        }

        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            if (Shadow(seconds) <= 0f) return across;
            var here = River.Project(Target(seconds));
            return Between(here.Distance, Crate + RaftRadius + 1f, from, to) ? Clear(across, here.Lateral, Crate + RaftRadius + 0.4f, limit) : across;
        }
    }
}
