using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// Old spiked mines on chains, staggered down a stretch and alternating sides, each circling its anchor on the
    /// clock: a slalom. Touch one and it blasts the raft aside and heels it (then it needs a moment to re-arm for that
    /// raft). Spot: <c>across</c> (its sign: which side the first mine is), <c>period</c> of their bobbing, <c>size</c> mines.
    /// </summary>
    public sealed class ChainedMines : Obstacle, ISpotted
    {
        public const float MineRadius = 0.55f;
        public const float Bob = 0.5f; // how far each circles round its anchor
        public const float Spacing = 7f; // down the river between mines
        public const float Out = 0.6f; // each anchor's share of the half-width out from the middle, alternating sides
        const float Knock = 115f;
        const float Heel = 5f; // a heavy lurch: over it goes only if it was already rocking, and braced it rides it out

        readonly ObstacleSpot spot;
        readonly RiverSample at;
        readonly Vector2[] anchors;
        readonly float[] distances;
        readonly double[] blasts;

        public ChainedMines(Course course, ObstacleSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            var count = spot.size >= 1f ? Mathf.Clamp(Mathf.RoundToInt(spot.size), 2, 4) : 3;
            anchors = new Vector2[count];
            distances = new float[count];
            blasts = new double[count];
            var first = spot.across < 0f ? -1f : 1f;
            for (var i = 0; i < count; i++)
            {
                distances[i] = spot.distance + (i - (count - 1) * 0.5f) * Spacing;
                var sample = River.At(distances[i]);
                anchors[i] = sample.Centre + sample.Left * ((i % 2 == 0 ? first : -first) * sample.HalfWidth * Out);
                blasts[i] = double.NegativeInfinity;
            }
        }

        public ObstacleSpot Spot => spot;
        public RiverSample At => at;
        public int Count => anchors.Length;
        public Vector2 Anchor(int i) => anchors[i];

        /// <summary>Where mine <paramref name="i"/> is on its chain now.</summary>
        public Vector2 Mine(int i, double seconds)
        {
            var turn = 2f * Mathf.PI * Phase(seconds, spot.period, spot.offset + i * spot.period * 0.37f) * (i % 2 == 0 ? 1f : -1f);
            return anchors[i] + new Vector2(Mathf.Cos(turn), Mathf.Sin(turn)) * Bob;
        }

        /// <summary>The host's last blast at this mine (for its flash), or minus infinity.</summary>
        public double LastBlast(int i) => blasts[i];

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            for (var i = 0; i < anchors.Length; i++)
            {
                var mine = Mine(i, seconds);
                foreach (var boat in boats)
                {
                    var raft = boat.Body.Position;
                    if (!Afloat(boat) || Vector2.Distance(raft, mine) > MineRadius + RaftRadius || !Ready(boat, seconds, i, 2.0)) continue;
                    boat.Shove(Away(mine, raft, at.Tangent) * Knock, Heel, 0.4f);
                    blasts[i] = seconds;
                }
            }
        }

        /// <summary>About to touch a mine: on a mine's line across the river with it just ahead.</summary>
        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            var here = River.Project(position);
            for (var i = 0; i < anchors.Length; i++)
            {
                var ahead = distances[i] - here.Distance;
                if (ahead < -(MineRadius + RaftRadius) || ahead > MineRadius + RaftRadius + 6f) continue;
                if (Mathf.Abs(River.Project(Mine(i, seconds)).Lateral - here.Lateral) < MineRadius + RaftRadius + 0.4f) return true;
            }

            return false;
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            for (var i = 0; i < anchors.Length; i++) Circle(lines, Mine(i, seconds), MineRadius);
        }

        /// <summary>Round the next mine ahead only: steering for the one after would put the raft on this one.</summary>
        public override float SteerAcross(float from, float to, float across, double seconds, float limit)
        {
            // Down the middle if that clears every mine coming up: on fast water there's no time to weave round each.
            const float Reach = MineRadius + Bob + RaftRadius + 0.1f;
            bool Clears(float lane)
            {
                for (var i = 0; i < anchors.Length; i++)
                    if (distances[i] >= from - MineRadius - RaftRadius && distances[i] <= to + Spacing &&
                        Mathf.Abs(River.Project(anchors[i]).Lateral - lane) < Reach) return false;
                return true;
            }

            if (Clears(across)) return across;
            if (Clears(0f)) return 0f;
            for (var i = 0; i < anchors.Length; i++)
            {
                if (distances[i] < from - MineRadius - RaftRadius || distances[i] > to + 2f) continue;
                return Clear(across, River.Project(anchors[i]).Lateral, MineRadius + Bob + RaftRadius + 0.3f, limit);
            }

            return across;
        }
    }
}
