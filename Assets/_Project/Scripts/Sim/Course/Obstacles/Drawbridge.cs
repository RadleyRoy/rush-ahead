using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A drawbridge: two leaves that lower from the banks to meet over the river, low enough to stop a raft, then lift
    /// again. Lights flash and a bell rings before they come down. While they're down it's a wall right across the river
    /// (a cushioned one: you stop and wait); caught under a leaf as it lands, you're knocked over unless braced. Time it.
    /// Spot: <c>period</c>.
    /// </summary>
    public sealed class Drawbridge : Obstacle, ISpotted
    {
        public const float Deck = 2.4f; // along the river
        public const float Warn = 0.28f; // lights from here
        public const float Lower = 0.4f; // the leaves start down
        public const float Down = 0.5f; // and meet
        public const float Raise = 0.8f; // start up
        public const float Up = 0.9f; // and are up
        public const float Shut = 0.12f; // solid while open less than this
        const float Knock = 70f;
        const float Heel = 6f;

        readonly ObstacleSpot spot;
        readonly RiverSample at;
        readonly BoxCollider2D[] leaves = new BoxCollider2D[2];
        bool shut;

        public Drawbridge(Course course, Transform parent, ObstacleSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            var body = Kinematic("Drawbridge", at.Centre, parent);
            body.transform.rotation = Quaternion.Euler(0f, 0f, Degrees(at.Tangent));
            var reach = at.HalfWidth + 0.8f;
            for (var i = 0; i < 2; i++)
            {
                var box = body.gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(Deck, reach);
                box.offset = new Vector2(0f, (i == 0 ? -0.5f : 0.5f) * reach);
                box.sharedMaterial = Slick;
                box.enabled = false;
                leaves[i] = box;
            }
        }

        public ObstacleSpot Spot => spot;
        public RiverSample At => at;
        public override IEnumerable<Collider2D> Barriers => leaves;

        public float BridgePhase(double seconds) => Phase(seconds, spot.period, spot.offset);

        /// <summary>How far up the leaves are: 1 open to the sky, 0 down across the river.</summary>
        public float Open(double seconds)
        {
            var phase = BridgePhase(seconds);
            if (phase < Lower) return 1f;
            if (phase < Down) return 1f - Smooth((phase - Lower) / (Down - Lower));
            if (phase < Raise) return 0f;
            if (phase < Up) return Smooth((phase - Raise) / (Up - Raise));
            return 1f;
        }

        /// <summary>Whether the warning lights are flashing: before the leaves come down, and while they do.</summary>
        public bool Warning(double seconds)
        {
            var phase = BridgePhase(seconds);
            return phase >= Warn && phase < Down;
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var nowShut = Open(seconds) < Shut;
            if (nowShut != shut)
            {
                shut = nowShut;
                foreach (var leaf in leaves) leaf.enabled = shut;
                if (shut) KeepLooseBodiesUnder();
            }

            // Water piled up against the shut bridge holds a raft in the middle, where the leaves part first, rather than
            // letting it slide along them and off an edge.
            if (shut) foreach (var boat in boats) AlongTheWall(boat, at, Deck, 0f, 0.6f, dt);

            var phase = BridgePhase(seconds);
            if (phase < Down - 0.04f || phase >= Down + 0.01f) return;
            foreach (var boat in boats)
            {
                if (!Afloat(boat)) continue;
                var along = Vector2.Dot(boat.Body.Position - at.Centre, at.Tangent);
                if (Mathf.Abs(along) > Deck * 0.5f + RaftRadius * 0.5f || !Ready(boat, seconds, gap: 1.5)) continue;
                boat.Shove(at.Tangent * ((along >= 0f ? 1f : -1f) * Knock), Heel);
            }
        }

        /// <summary>Under the bridge, or about to be, as the leaves come down: braced, the landing leaf is only a jolt.</summary>
        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            const float Reach = Deck * 0.5f + RaftRadius * 0.5f + 0.6f;
            var along = Vector2.Dot(position - at.Centre, at.Tangent);
            if (along > Reach || along < -(Reach + ApproachSpeed * lookahead)) return false;
            for (var t = 0f; t <= lookahead; t += 0.05f)
            {
                var phase = BridgePhase(seconds + t);
                if (phase >= Down - 0.05f && phase < Down + 0.02f) return true;
            }

            return false;
        }

        /// <summary>Barrels, logs and geese slip under the leaves (a collider switched back on may forget that).</summary>
        void KeepLooseBodiesUnder()
        {
            foreach (var body in Course.BarrelBodies)
            {
                if (body == null) continue;
                foreach (var collider in body.GetComponents<Collider2D>())
                    foreach (var leaf in leaves)
                        Physics2D.IgnoreCollision(leaf, collider);
            }
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            var phase = BridgePhase(seconds);
            if (phase >= Lower && phase < Up) Box(lines, at.Centre, at.Tangent, Deck * 0.5f, at.HalfWidth);
        }
    }
}
