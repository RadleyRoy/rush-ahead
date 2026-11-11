using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// An island splitting the river (see <see cref="IslandSpot"/>): lens-shaped, pointed at both ends, with a branch of
    /// water either side. Its shores follow the river round its bends, since it's laid out along the river's middle.
    /// </summary>
    public sealed class Island
    {
        const float Fullness = 0.55f; // how blunt its ends are: 1 is a pointed lens, 0 a block

        readonly River river;

        public Island(IslandSpot spot, River river)
        {
            Spot = spot;
            this.river = river;
        }

        public IslandSpot Spot { get; }
        public float Nose => Spot.distance;
        public float Tail => Spot.distance + Spot.length;
        public float Across => Spot.across;

        /// <summary>Which side the fast line is on: +1 the left, -1 the right.</summary>
        public int FastSide => Spot.fastSide >= 0f ? 1 : -1;

        /// <summary>How far through the island a distance down the river is, 0 at the nose and 1 at the tail.</summary>
        public float Through(float distance) => (distance - Nose) / Spot.length;

        public bool Covers(float distance) => distance > Nose && distance < Tail;

        /// <summary>Half the island's width at a distance down the river (0 off its ends).</summary>
        public float HalfWidthAt(float distance)
        {
            var t = Through(distance);
            if (t <= 0f || t >= 1f) return 0f;
            return Spot.halfWidth * Mathf.Pow(Mathf.Sin(Mathf.PI * t), Fullness);
        }

        /// <summary>The water on one side (+1 left, -1 right) at a distance: its middle and half its width, across the river.</summary>
        public (float centre, float half) Branch(int side, float distance, float riverHalfWidth)
        {
            var inner = Across + side * HalfWidthAt(distance);
            var outer = side * riverHalfWidth;
            return ((inner + outer) * 0.5f, Mathf.Abs(outer - inner) * 0.5f);
        }

        public (float centre, float half) Branch(int side, float distance) => Branch(side, distance, river.At(distance).HalfWidth);

        /// <summary>The side of the island a point across the river is on.</summary>
        public int SideOf(float lateral) => lateral >= Across ? 1 : -1;

        /// <summary>The shore all the way round, nose to tail down the left and back up the right, for its collider and its look.</summary>
        public Vector2[] Outline(int perSide = 28)
        {
            var points = new List<Vector2>(perSide * 2 + 2);
            for (var i = 0; i <= perSide; i++) points.Add(Shore(1, Nose + Spot.length * i / perSide));
            for (var i = perSide - 1; i > 0; i--) points.Add(Shore(-1, Nose + Spot.length * i / perSide));
            return points.ToArray();
        }

        /// <summary>The island's middle line at a distance down the river, and which way is left across it there.</summary>
        public (Vector2 middle, Vector2 left) Frame(float distance)
        {
            var at = river.At(distance);
            return (at.Centre + at.Left * Across, at.Left);
        }

        /// <summary>A point on the shore on one side, at a distance down the river.</summary>
        public Vector2 Shore(int side, float distance)
        {
            var at = river.At(distance);
            return at.Centre + at.Left * (Across + side * HalfWidthAt(distance));
        }
    }

    /// <summary>
    /// A course's islands, and what crews and the water need to know about them: which branch to take, how to keep to
    /// it, and how fast each branch runs.
    /// </summary>
    public sealed class Islands
    {
        /// <summary>How far before an island's nose a crew settles which side it'll go.</summary>
        public const float Approach = 28f;

        /// <summary>Extra speed down the fast line, and how much the safe line holds back, in metres per second.</summary>
        public const float FastSurge = 2.2f;
        public const float SafeHold = 1.4f;

        const float NosePush = 1.8f; // the water parting round the nose, so nothing sits on it for ever
        const float NoseReach = 10f;
        const float RaftRoom = 2.2f; // a raft's radius and a little more, from a shore

        readonly List<Island> islands = new();
        readonly River river;

        public Islands(IEnumerable<IslandSpot> spots, River river)
        {
            this.river = river;
            if (spots == null) return;
            foreach (var spot in spots) islands.Add(new Island(spot, river));
        }

        public IReadOnlyList<Island> All => islands;
        public int Count => islands.Count;

        /// <summary>The island whose shores are either side of this distance down the river, if any.</summary>
        public Island At(float distance)
        {
            foreach (var island in islands)
                if (island.Covers(distance)) return island;
            return null;
        }

        /// <summary>Whether a point (by distance and across) is on an island, or within <paramref name="margin"/> of its shore.</summary>
        public bool OnIsland(float distance, float lateral, float margin = 0f)
        {
            var island = At(distance);
            return island != null && Mathf.Abs(lateral - island.Across) < island.HalfWidthAt(distance) + margin;
        }

        /// <summary>Moves a place across the river off any island there, into the middle of the branch it's nearest.</summary>
        public float OffIsland(float distance, float lateral, float riverHalfWidth)
        {
            var island = At(distance);
            if (island == null || Mathf.Abs(lateral - island.Across) >= island.HalfWidthAt(distance) + RaftRoom) return lateral;
            return island.Branch(island.SideOf(lateral), distance, riverHalfWidth).centre;
        }

        /// <summary>
        /// Which side a crew goes round an island: the side it's on once it's between the shores (there's no crossing
        /// over), otherwise the fast line if it's after speed and the safe line if not.
        /// </summary>
        static int SideFor(Island island, float from, float lateral, bool fast)
        {
            if (island.HalfWidthAt(from) > 0.3f) return island.SideOf(lateral);
            return fast ? island.FastSide : -island.FastSide;
        }

        /// <summary>
        /// Where across the river a crew at <paramref name="from"/> (and <paramref name="lateral"/> across) should aim at
        /// <paramref name="to"/>, given the line's choice <paramref name="across"/>: easing over to its branch on the
        /// approach, down the middle of it between the shores, and back to the line past the tail.
        /// </summary>
        public float Steer(float from, float lateral, float to, float across, bool fast)
        {
            foreach (var island in islands)
            {
                if (to <= island.Nose - Approach || from >= island.Tail) continue;
                var side = SideFor(island, from, lateral, fast);
                var probe = Mathf.Clamp(to, island.Nose + island.Spot.length * 0.3f, island.Tail - island.Spot.length * 0.3f);
                var centre = island.Branch(side, probe).centre;
                // On the way in and out the river may still be narrower than the branch is far across: keep the aim on it.
                if (to < island.Nose) return Inside(Mathf.Lerp(across, centre, Mathf.SmoothStep(0f, 1f, 1f - (island.Nose - to) / Approach)), to);
                if (to <= island.Tail) return island.Branch(side, to).centre;
                return Inside(Mathf.Lerp(island.Branch(side, island.Tail - island.Spot.length * 0.15f).centre, across, Mathf.Clamp01((to - island.Tail) / 12f)), to);
            }

            return across;
        }

        float Inside(float aim, float at)
        {
            var room = Mathf.Max(0f, river.At(at).HalfWidth - RaftRoom - 0.8f);
            return Mathf.Clamp(aim, -room, room);
        }

        /// <summary>Keeps an aim (after dodging whatever else) inside the branch the crew is taking, clear of both its shores.</summary>
        public float Keep(float from, float lateral, float to, float across, bool fast)
        {
            foreach (var island in islands)
            {
                if (to <= island.Nose - 4f || to >= island.Tail || from >= island.Tail) continue;
                var side = SideFor(island, from, lateral, fast);
                var probe = Mathf.Max(to, island.Nose + island.Spot.length * 0.12f);
                var at = river.At(probe);
                var inner = island.Across + side * island.HalfWidthAt(probe);
                var outer = side * at.HalfWidth;
                var lo = Mathf.Min(inner, outer);
                var hi = Mathf.Max(inner, outer);
                var low = lo + RaftRoom;
                var high = hi - RaftRoom;
                return low <= high ? Mathf.Clamp(across, low, high) : (lo + hi) * 0.5f;
            }

            return across;
        }

        /// <summary>How close to the edge a crew lets the raft come before steering back in: less in a narrow branch, so it doesn't steer into the island.</summary>
        public float EdgeSense(float distance, float lateral, float riverHalfWidth, float usual)
        {
            var island = At(distance);
            if (island == null) return usual;
            var (_, half) = island.Branch(island.SideOf(lateral), distance, riverHalfWidth);
            return Mathf.Min(usual, Mathf.Max(1.5f, half - 0.3f));
        }

        /// <summary>
        /// What an island does to the water at a point: each branch runs like a river of its own, fastest down its middle;
        /// the fast line runs harder and the safe line slower; and the water parts round the nose.
        /// </summary>
        /// <param name="profile">The flow's profile across the river, replaced here by the branch's own.</param>
        /// <param name="extra">Speed added down the river (negative holds it back).</param>
        /// <param name="sideways">Speed added across it (positive to the left).</param>
        public bool Water(float distance, float lateral, float riverHalfWidth, ref float profile, out float extra, out float sideways)
        {
            extra = 0f;
            sideways = 0f;
            foreach (var island in islands)
            {
                var before = island.Nose - distance;
                if (before > 0f && before < NoseReach)
                {
                    var offset = lateral - island.Across;
                    var reach = island.Spot.halfWidth + 2f;
                    if (Mathf.Abs(offset) < reach)
                        sideways += (offset >= 0f ? 1f : -1f) * NosePush * (1f - Mathf.Abs(offset) / reach) * (1f - before / NoseReach);
                    return true;
                }

                var w = island.HalfWidthAt(distance);
                if (w <= 0f) continue;
                var side = island.SideOf(lateral);
                var (centre, half) = island.Branch(side, distance, riverHalfWidth);
                var u = half > 0.1f ? (lateral - centre) / half : 0f;
                profile = Mathf.Clamp01(1f - u * u);
                var ramp = Mathf.Sqrt(Mathf.Sin(Mathf.PI * island.Through(distance)));
                extra = (side == island.FastSide ? FastSurge : -SafeHold) * ramp * profile;
                return true;
            }

            return false;
        }
    }
}
