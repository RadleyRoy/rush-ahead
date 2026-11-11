using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// Deals a round's obstacles from a course's menu: how many (between its min and max) and where, from a seed, so
    /// every device that has the seed deals the same layout. It drops obstacles into free stretches of river, picking
    /// kinds the water there suits (cannons, ferries, sweepers and buoys only where it's wide), each with its own
    /// room either side, clear of the start, every place a raft comes back and the finish, never two you must brace
    /// for back to back, never in a whirlpool or on a rock, and with sides, timings and reaches drawn at random within
    /// each kind's fair range. The rules are what Phase 14's play-testing with the AI crews taught.
    /// </summary>
    public static class LayoutGenerator
    {
        const float RaftRadius = 1.6f;
        const float Gap = 6.5f; // clear water between one obstacle's stretch and the next
        const float BraceGap = 34f; // between two obstacles you have to brace for
        const float BraceRoom = 14f; // either side of one you have to brace for: a braced crew isn't blowing, so it can't dodge
        const float LaneRoom = 16f; // between two that each send a raft to one side: room to cross over between them
        const float RespawnPastGate = 3f;
        const float RunUp = 20f; // clear water after a place a raft comes back, before the next obstacle starts
        const int TriesPerObstacle = 150;

        const int Redeals = 8;

        /// <summary>Rafts come at twice the speed they used to: most obstacles' timetables run this much quicker to keep up.</summary>
        const float Tempo = 0.75f;

        static float Quick(float seconds, float tempo = Tempo) => seconds * tempo;

        public static CourseLayout Generate(CourseDef def, River river, int seed)
        {
            // Now and then the first pieces dealt (a long stretch of rapids, say) leave too little room for the rest: deal the
            // round again from a seed derived from this one (the same on every device), keeping the fullest.
            CourseLayout best = null;
            for (var attempt = 0; attempt < Redeals; attempt++)
            {
                var layout = DealOnce(def, river, unchecked(seed + attempt * 7919));
                if (best == null || layout.Count > best.Count) best = layout;
                if (best.Count >= def.minObstacles) break;
            }

            best.Seed = seed;
            return best;
        }

        /// <summary>
        /// One obstacle of <paramref name="kind"/> alone, as near <paramref name="distance"/> as it'll go, dealt by the same
        /// rules as a round (for the obstacle gallery). Null if it won't go anywhere near there.
        /// </summary>
        public static CourseLayout Single(CourseDef def, River river, ObstacleKind kind, float distance, int seed = 1)
        {
            var dice = new Dice(seed);
            for (var tries = 0; tries < 200; tries++)
            {
                var deal = new Deal(def, river);
                if (deal.TryPlace(dice, kind, distance + (tries % 2 == 0 ? 1f : -1f) * (tries / 2) * 0.5f)) return deal.Layout;
            }

            return null;
        }

        static CourseLayout DealOnce(CourseDef def, River river, int seed)
        {
            var dice = new Dice(seed);
            var count = dice.Range(def.minObstacles, def.maxObstacles + 1);
            var deal = new Deal(def, river);
            var from = def.startDistance + 26f;
            var to = def.FinishDistance - 6f;
            for (var tries = 0; deal.Count < count && tries < count * TriesPerObstacle; tries++)
            {
                var distance = dice.Range(from, to);
                if (!deal.Pick(dice, distance, out var kind)) continue;
                deal.TryPlace(dice, kind, distance);
            }

            // Chance can leave free stretches it never hit: sweep down the river from a random start (wrapping round) to fill them.
            var start = dice.Range(from, to);
            for (var step = 0f; deal.Count < count && step < to - from; step += 1.5f)
            {
                var distance = from + Mathf.Repeat(start - from + step, to - from);
                for (var pick = 0; pick < 4 && deal.Count < count; pick++)
                    if (deal.Pick(dice, distance, out var kind) && deal.TryPlace(dice, kind, distance)) break;
            }

            // Boost lanes in the free water left between obstacles: about one per 110 m.
            var lanes = Mathf.Clamp(Mathf.RoundToInt((to - from) / 110f), 2, 5);
            for (var tries = 0; deal.Boosts < lanes && tries < 600; tries++) deal.TryBoost(dice, dice.Range(from, to - BoostLane.Length));
            return deal.Layout;
        }

        /// <summary>Kinds that send a raft to one side of the river or the other to get past.</summary>
        static bool Lanes(ObstacleKind kind) => kind is ObstacleKind.Wheel or ObstacleKind.Sweeper or ObstacleKind.Bumper or ObstacleKind.Cannon or ObstacleKind.Bear or
            ObstacleKind.Mudslide or ObstacleKind.Sluice or ObstacleKind.BeaverDam or ObstacleKind.FallingTree or ObstacleKind.PaddleSteamer or
            ObstacleKind.WreckingBall or ObstacleKind.MillRace or ObstacleKind.GiantFan;

        /// <summary>Kinds that wall off part of the river (or all of it) a raft can be pinned against.</summary>
        static bool Walls(ObstacleKind kind) => kind is ObstacleKind.Sluice or ObstacleKind.BeaverDam or ObstacleKind.FallingTree or ObstacleKind.IceFloes or
            ObstacleKind.Ferry or ObstacleKind.NetBoat or ObstacleKind.Turtle or ObstacleKind.Weir or ObstacleKind.Drawbridge or ObstacleKind.PaddleSteamer or
            ObstacleKind.MillRace;

        static bool Braces(ObstacleKind kind) => kind is ObstacleKind.SwingingLog or ObstacleKind.Geyser or ObstacleKind.Hippo or ObstacleKind.Weir or
            ObstacleKind.FallingTree or ObstacleKind.Pelican or ObstacleKind.TidalBore or ObstacleKind.CalvingGlacier or ObstacleKind.JumpRamp;

        /// <summary>One round's dealing: what's placed so far, and the rules for placing more.</summary>
        sealed class Deal
        {
            readonly CourseDef def;
            readonly River river;
            readonly List<(float from, float to, bool brace, bool lane)> taken = new();
            readonly List<float> respawns = new();
            readonly List<WheelSpot> wheels = new();
            readonly List<CrocSpot> crocs = new();
            readonly List<SwingSpot> swingers = new();
            readonly List<GeyserSpot> geysers = new();
            readonly List<RockslideSpot> rockslides = new();
            readonly List<HippoSpot> hippos = new();
            readonly List<SluiceSpot> sluices = new();
            readonly List<SweeperSpot> sweepers = new();
            readonly List<CannonSpot> cannons = new();
            readonly List<RapidsSpot> rapids = new();
            readonly List<WeirSpot> weirs = new();
            readonly List<BumperSpot> bumpers = new();
            readonly List<FerrySpot> ferries = new();
            readonly List<BearSpot> bears = new();
            readonly List<SideSpot> trees = new();
            readonly List<CrossingSpot> turtles = new();
            readonly List<CrossingSpot> shoals = new();
            readonly List<CrossingSpot> waterspouts = new();
            readonly List<AcrossSpot> eels = new();
            readonly List<SideSpot> mudslides = new();
            readonly List<AcrossSpot> floes = new();
            readonly List<AcrossSpot> dams = new();
            readonly List<SideSpot> pelicans = new();
            readonly List<CrossingSpot> netBoats = new();
            readonly List<ObstacleSpot> extras = new();
            readonly List<BoostSpot> boosts = new();

            public int Boosts => boosts.Count;

            /// <summary>A boost lane at <paramref name="distance"/> if the water there is free: off every obstacle, respawn, whirlpool and rock.</summary>
            public void TryBoost(Dice dice, float distance)
            {
                var from = distance - 2f;
                var to = distance + BoostLane.Length + 2f;
                foreach (var (takenFrom, takenTo, _, _) in taken)
                    if (from < takenTo + 2f && to > takenFrom - 2f) return; // right up to an obstacle: the fast line is the risky one

                // Never into a tight bend (the lane itself only pushes as fast as the next bend allows).
                for (var d = distance; d <= distance + BoostLane.Length + 20f; d += 3f)
                    if (Mathf.Abs(river.At(d).Curvature) > 0.045f) return;
                foreach (var boost in boosts)
                    if (from < boost.distance + BoostLane.Length + 20f && to > boost.distance - 20f) return;
                foreach (var respawn in respawns)
                    if (from < respawn + 4f && to > respawn - 4f) return;
                foreach (var pool in def.whirlpools)
                {
                    var poolAt = river.Project(pool.position).Distance;
                    if (from < poolAt + pool.radius + 3f && to > poolAt - pool.radius - 3f) return;
                }

                var at = river.At(distance + BoostLane.Length * 0.5f);
                var across = dice.Range(-1f, 1f) * Mathf.Max(0f, at.HalfWidth - BoostLane.HalfWidth - 1.8f);
                foreach (var rock in def.rocks)
                {
                    var rockAt = river.Project(rock.position);
                    if (rockAt.Distance > from && rockAt.Distance < to && Mathf.Abs(rockAt.Lateral - across) < rock.radius + BoostLane.HalfWidth + 1.6f) return;
                }

                boosts.Add(new BoostSpot(distance, across));
            }

            public Deal(CourseDef def, River river)
            {
                this.def = def;
                this.river = river;
                respawns.Add(def.startDistance);
                foreach (var gate in def.gates) respawns.Add(gate + RespawnPastGate);
            }

            public int Count => taken.Count;

            public CourseLayout Layout => new()
            {
                wheels = wheels.ToArray(), crocodiles = crocs.ToArray(), swingers = swingers.ToArray(), geysers = geysers.ToArray(),
                rockslides = rockslides.ToArray(), hippos = hippos.ToArray(), sluices = sluices.ToArray(), sweepers = sweepers.ToArray(),
                cannons = cannons.ToArray(), rapids = rapids.ToArray(), weirs = weirs.ToArray(), bumpers = bumpers.ToArray(),
                ferries = ferries.ToArray(), bears = bears.ToArray(), trees = trees.ToArray(), turtles = turtles.ToArray(), shoals = shoals.ToArray(),
                waterspouts = waterspouts.ToArray(), eels = eels.ToArray(), mudslides = mudslides.ToArray(), floes = floes.ToArray(), dams = dams.ToArray(),
                pelicans = pelicans.ToArray(), netBoats = netBoats.ToArray(), extras = extras.ToArray(), boosts = boosts.ToArray(),
            };

            /// <summary>A kind from the menu, by weight, among those the water here suits.</summary>
            public bool Pick(Dice dice, float distance, out ObstacleKind kind)
            {
                var half = river.At(distance).HalfWidth;
                var total = 0f;
                foreach (var chance in def.menu)
                    if (Suits(chance.kind, half)) total += chance.weight;
                kind = default;
                if (total <= 0f) return false;
                var roll = dice.Value() * total;
                foreach (var chance in def.menu)
                {
                    if (!Suits(chance.kind, half)) continue;
                    kind = chance.kind;
                    roll -= chance.weight;
                    if (roll <= 0f) break;
                }

                return true;
            }

            /// <summary>Kinds that need room only go where the river is wide enough to leave a way past.</summary>
            static bool Suits(ObstacleKind kind, float half) => kind switch
            {
                ObstacleKind.PaddleSteamer => half >= 7f,
                ObstacleKind.ChainedMines => half >= 7f, // a lane between them at speed
                ObstacleKind.WreckingBall or ObstacleKind.MillRace or ObstacleKind.LogFlume or ObstacleKind.DynamiteFloat or
                    ObstacleKind.LeapingFrogs or ObstacleKind.GiantFan => half >= 6f,
                ObstacleKind.Kraken or ObstacleKind.GooseGang or ObstacleKind.CargoCrane => half >= 5.5f,
                ObstacleKind.Cannon => half >= 7f,
                ObstacleKind.Ferry or ObstacleKind.Waterspout => half >= 6.5f,
                ObstacleKind.Wheel or ObstacleKind.Sweeper or ObstacleKind.Bumper or ObstacleKind.Turtle or ObstacleKind.PiranhaShoal or ObstacleKind.Bear or
                    ObstacleKind.Mudslide or ObstacleKind.IceFloes or ObstacleKind.NetBoat => half >= 6f,
                ObstacleKind.Sluice or ObstacleKind.BeaverDam => half >= 5.5f,
                ObstacleKind.FallingTree => half >= 6.5f, // its gap runs to the open edge: in narrow water that's a trap at speed
                _ => true,
            };

            /// <summary>How far up and down the river a kind needs to itself, either side of where it is.</summary>
            static float Reach(ObstacleKind kind, float half) => kind switch
            {
                ObstacleKind.Wheel => 5f,
                ObstacleKind.Geyser => 4f,
                ObstacleKind.Sluice => 5f,
                ObstacleKind.Sweeper => 0.6f * half + 3f,
                ObstacleKind.Cannon => 9f,
                ObstacleKind.Bumper => 3f,
                ObstacleKind.Ferry => 5f,
                ObstacleKind.PiranhaShoal or ObstacleKind.BeaverDam or ObstacleKind.NetBoat => 5f,
                ObstacleKind.Mudslide => 6f,
                ObstacleKind.Waterspout => 7f,
                ObstacleKind.Kraken => Kraken.Spread + 2.5f,
                ObstacleKind.TidalBore => TidalBore.Run + 2f,
                ObstacleKind.JumpRamp => 3f,
                ObstacleKind.GooseGang => 6f,
                ObstacleKind.ChainedMines => 9.5f,
                ObstacleKind.WreckingBall => WreckingBall.Reach + 1f,
                ObstacleKind.StormCloud => StormCloud.Spread + 2.5f,
                ObstacleKind.PaddleSteamer => PaddleSteamer.Range + PaddleSteamer.Length * 0.5f + 1.5f,
                ObstacleKind.MillRace => MillRace.Along + 1f,
                ObstacleKind.CargoCrane => 6.5f,
                ObstacleKind.CalvingGlacier => CalvingGlacier.Along + 1f,
                ObstacleKind.DynamiteFloat => 5f,
                ObstacleKind.GiantFan => GiantFan.Along + 1f,
                _ => 4f,
            };

            /// <returns>Whether it went in.</returns>
            public bool TryPlace(Dice dice, ObstacleKind kind, float distance)
            {
                var at = river.At(distance);
                var half = at.HalfWidth;
                // How far it runs on downstream of its spot: a stretch of rapids, floes, a jump's flight, flume logs and dynamite drifting down.
                var length = kind switch
                {
                    ObstacleKind.Rapids => dice.Range(20f, 28f),
                    ObstacleKind.IceFloes => IceFloes.Run,
                    ObstacleKind.JumpRamp => JumpRamp.Flight,
                    ObstacleKind.LogFlume => LogFlume.Run(Quick(4.4f)),
                    ObstacleKind.DynamiteFloat => DynamiteFloat.Run(Quick(6.6f)),
                    _ => 0f,
                };
                var from = distance - Reach(kind, half);
                var to = distance + length + Reach(kind, half);
                if (to > def.FinishDistance - 3f) return false;

                // Clear of every checkpoint (a bridge on a gate) and every place a raft starts or comes back: by its own
                // reach, or for a crocodile (which lunges right across) by the river's width.
                foreach (var gate in def.gates)
                    if (from < gate + 2f && to > gate - 2f) return false;
                var clearance = (kind == ObstacleKind.Crocodile ? half : Mathf.Max(2f, Reach(kind, half))) + RaftRadius + 4.5f;
                foreach (var respawn in respawns)
                {
                    if (Mathf.Abs(distance - respawn) < clearance || (from < respawn + 2f && to > respawn - 2f)) return false;
                    if (distance > respawn && from - respawn < RunUp) return false; // a raft starting again from rest needs a run-up
                }

                // Room from what's already placed, and two brace-or-die obstacles never close together.
                foreach (var (takenFrom, takenTo, brace, lane) in taken)
                {
                    var room = Braces(kind) || brace ? BraceRoom : Lanes(kind) && lane ? LaneRoom : Gap;
                    if (from < takenTo + room && to > takenFrom - room) return false;
                    if (Braces(kind) && brace && Mathf.Min(Mathf.Abs(from - takenTo), Mathf.Abs(takenFrom - to)) < BraceGap) return false;
                }

                // Nothing that walls off the river where a rock stands in it: a raft gets wedged between the two for good.
                if (Walls(kind))
                    foreach (var rock in def.rocks)
                    {
                        var rockAt = river.Project(rock.position).Distance;
                        if (rockAt > from - 3f && rockAt < to + 3f) return false;
                    }

                // Never in a whirlpool's water, or on a rock.
                foreach (var pool in def.whirlpools)
                {
                    var poolAt = river.Project(pool.position).Distance;
                    if (from < poolAt + pool.radius + 3f && to > poolAt - pool.radius - 3f) return false;
                }

                var side = dice.Value() < 0.5f ? -1f : 1f;
                var across = 0f;
                var radius = 0f;
                switch (kind)
                {
                    case ObstacleKind.Wheel:
                        radius = dice.Range(2.6f, 3f);
                        across = side * half * 0.5f;
                        if (!ClearOfRocks(distance, across, radius)) return false;
                        // Turning so the arms that meet a raft coming downstream sweep it toward the middle.
                        wheels.Add(new WheelSpot(at.Centre + at.Left * across, radius, 4, dice.Range(110f, 145f) * side));
                        break;
                    case ObstacleKind.Crocodile:
                        crocs.Add(new CrocSpot(distance, side, half >= 6f ? dice.Range(0.38f, 0.45f) : 0.3f, Quick(dice.Range(3.4f, 4.4f)), dice.Range(0f, 4f)));
                        break;
                    case ObstacleKind.SwingingLog:
                        swingers.Add(new SwingSpot(distance, Quick(dice.Range(2.6f, 3.4f)), dice.Range(0f, 3f), dice.Range(4f, 4.5f)));
                        break;
                    case ObstacleKind.Geyser:
                        radius = dice.Range(1.6f, 2.2f);
                        across = dice.Range(-1f, 1f) * Mathf.Max(0f, half - radius - 1f);
                        if (!ClearOfRocks(distance, across, radius)) return false;
                        geysers.Add(new GeyserSpot(at.Centre + at.Left * across, radius, Quick(dice.Range(2.8f, 3.5f)), dice.Range(0f, 3f)));
                        break;
                    case ObstacleKind.Rockslide:
                        rockslides.Add(new RockslideSpot(distance, side, Quick(dice.Range(4.5f, 5.5f)), dice.Range(0f, 5f), dice.Range(1.2f, 1.4f)));
                        break;
                    case ObstacleKind.Hippo:
                        across = dice.Range(-1f, 1f) * Mathf.Max(0f, half - 2.6f);
                        if (!ClearOfRocks(distance, across, 1.6f)) return false;
                        hippos.Add(new HippoSpot(distance, across, Quick(dice.Range(4f, 5f)), dice.Range(0f, 4f)));
                        break;
                    case ObstacleKind.Sluice:
                        if (Mathf.Abs(at.Curvature) > 0.03f) return false; // a straight run: room to see the gap and line up
                        sluices.Add(new SluiceSpot(distance, Mathf.Min(dice.Range(5.5f, 6f), 2f * half - 1.5f), Quick(dice.Range(4.5f, 5.5f)), dice.Range(0f, 5f)));
                        break;
                    case ObstacleKind.Sweeper:
                        if (!ClearOfRocks(distance, side * 0.4f * half, 0.6f * half)) return false;
                        sweepers.Add(new SweeperSpot(distance, side * 0.4f * half, 1f * half, dice.Range(55f, 72f) * (dice.Value() < 0.5f ? -1f : 1f)));
                        break;
                    case ObstacleKind.Cannon:
                        cannons.Add(new CannonSpot(distance, side, Quick(dice.Range(2.6f, 3.2f)), dice.Range(0f, 3f), dice.Range(25f, 35f)));
                        break;
                    case ObstacleKind.Rapids:
                        rapids.Add(new RapidsSpot(distance, distance + length, dice.Range(2.6f, 3.8f)));
                        break;
                    case ObstacleKind.Weir:
                        if (Mathf.Abs(at.Curvature) > 0.03f) return false; // never on a bend, where a braced crew can't steer round it
                        weirs.Add(new WeirSpot(distance));
                        break;
                    case ObstacleKind.Bumper:
                        radius = dice.Range(0.9f, 1.1f);
                        across = side * (half - 2.5f);
                        if (!ClearOfRocks(distance, across, radius)) return false;
                        bumpers.Add(new BumperSpot(at.Centre + at.Left * across, radius));
                        break;
                    case ObstacleKind.Ferry:
                        // Bank to bank, never faster than about 5 m/s: on wide water the crossing takes longer.
                        ferries.Add(new FerrySpot(distance, Mathf.Max(dice.Range(5.5f, 7f), 1.5f * (half - Ferry.Width * 0.5f + 0.4f)), dice.Range(0f, 6f)));
                        break;
                    case ObstacleKind.Bear:
                        bears.Add(new BearSpot(distance, side, Quick(dice.Range(3.2f, 3.8f)), dice.Range(0f, 3f)));
                        break;
                    case ObstacleKind.FallingTree:
                        // Once it's down its gap is the only way past, so the water below the gap must be clear of rocks.
                        if (!ClearAlong(distance, distance + 18f, -side * 0.5f * half, 1f)) return false;
                        trees.Add(new SideSpot(distance, side, Quick(dice.Range(6.5f, 8f)), dice.Range(0f, 8f)));
                        break;
                    case ObstacleKind.Turtle:
                        turtles.Add(new CrossingSpot(distance, dice.Range(7f, 9f), dice.Range(0f, 9f)));
                        break;
                    case ObstacleKind.PiranhaShoal:
                        shoals.Add(new CrossingSpot(distance, dice.Range(6f, 8f), dice.Range(0f, 8f)));
                        break;
                    case ObstacleKind.Waterspout:
                        waterspouts.Add(new CrossingSpot(distance, dice.Range(10f, 13f), dice.Range(0f, 13f)));
                        break;
                    case ObstacleKind.ElectricEels:
                        eels.Add(new AcrossSpot(distance, Quick(dice.Range(3.2f, 4.2f), 0.85f), dice.Range(0f, 4f)));
                        break;
                    case ObstacleKind.Mudslide:
                        mudslides.Add(new SideSpot(distance, side, Quick(dice.Range(5f, 6.5f)), dice.Range(0f, 6f)));
                        break;
                    case ObstacleKind.IceFloes:
                        floes.Add(new AcrossSpot(distance, Quick(dice.Range(7f, 9f), 0.6f), dice.Range(0f, 9f)));
                        break;
                    case ObstacleKind.BeaverDam:
                        if (Mathf.Abs(at.Curvature) > 0.03f) return false; // a straight run: room to see which gap is open and line up
                        dams.Add(new AcrossSpot(distance, Quick(half >= 8f ? dice.Range(9.5f, 11f) : dice.Range(7f, 8.5f), 0.8f), dice.Range(0f, 11f)));
                        break;
                    case ObstacleKind.Pelican:
                        pelicans.Add(new SideSpot(distance, side, Quick(dice.Range(3.8f, 4.8f), 0.85f), dice.Range(0f, 5f)));
                        break;
                    case ObstacleKind.NetBoat:
                        netBoats.Add(new CrossingSpot(distance, dice.Range(6.5f, 8.5f), dice.Range(0f, 8f)));
                        break;
                    case ObstacleKind.Kraken:
                        extras.Add(new ObstacleSpot(kind, distance, period: Quick(dice.Range(4.6f, 5.6f)), offset: dice.Range(0f, 6f), size: 2f));
                        break;
                    case ObstacleKind.TidalBore:
                        extras.Add(new ObstacleSpot(kind, distance, period: Quick(dice.Range(7.5f, 9f)), offset: dice.Range(0f, 9f)));
                        break;
                    case ObstacleKind.JumpRamp:
                        // A straight run to the lip and all through the flight, with nothing standing in its lane.
                        for (var d = distance - JumpRamp.Length; d <= distance + JumpRamp.Flight; d += 3f)
                            if (Mathf.Abs(river.At(d).Curvature) > 0.03f) return false;
                        across = dice.Range(-1f, 1f) * Mathf.Max(0f, half - JumpRamp.HalfWidth - 1.6f);
                        if (!ClearAlong(distance - JumpRamp.Length, distance + JumpRamp.Flight, across, JumpRamp.HalfWidth)) return false;
                        extras.Add(new ObstacleSpot(kind, distance, across: across));
                        break;
                    case ObstacleKind.GooseGang:
                        extras.Add(new ObstacleSpot(kind, distance, side, size: dice.Range(3, 5)));
                        break;
                    case ObstacleKind.ChainedMines:
                    {
                        var mines = half >= 7.5f ? 3 : 2;
                        for (var i = 0; i < mines; i++)
                            if (!ClearOfRocks(distance + (i - (mines - 1) * 0.5f) * ChainedMines.Spacing, (i % 2 == 0 ? side : -side) * half * ChainedMines.Out,
                                    ChainedMines.MineRadius + ChainedMines.Bob)) return false;
                        extras.Add(new ObstacleSpot(kind, distance, across: side, period: Quick(dice.Range(2.6f, 3.4f)), offset: dice.Range(0f, 4f), size: mines));
                        break;
                    }
                    case ObstacleKind.WreckingBall:
                        across = side * dice.Range(0f, Mathf.Max(0f, half - 3.2f));
                        if (!ClearAlong(distance - WreckingBall.Reach, distance + WreckingBall.Reach, across, WreckingBall.Ball)) return false;
                        extras.Add(new ObstacleSpot(kind, distance, across: across, period: Quick(dice.Range(3.6f, 4.4f)), offset: dice.Range(0f, 4f)));
                        break;
                    case ObstacleKind.StormCloud:
                        extras.Add(new ObstacleSpot(kind, distance, period: Quick(dice.Range(2.2f, 2.8f), 0.9f), offset: dice.Range(0f, 3f)));
                        break;
                    case ObstacleKind.PaddleSteamer:
                        // Up and back no faster than about 5 m/s (like the ferry), so it never flings a raft.
                        across = side * (half - PaddleSteamer.Width * 0.5f - 1.2f);
                        extras.Add(new ObstacleSpot(kind, distance, across: across, period: dice.Range(12f, 15f), offset: dice.Range(0f, 15f)));
                        break;
                    case ObstacleKind.MillRace:
                        if (!ClearOfRocks(distance, side * (half - 0.6f), MillRace.WheelRadius)) return false;
                        extras.Add(new ObstacleSpot(kind, distance, side, period: dice.Range(2.5f, 3.5f)));
                        break;
                    case ObstacleKind.Drawbridge:
                        if (Mathf.Abs(at.Curvature) > 0.03f) return false; // a straight run: room to see it coming down
                        extras.Add(new ObstacleSpot(kind, distance, period: Quick(dice.Range(7.5f, 9f)), offset: dice.Range(0f, 9f)));
                        break;
                    case ObstacleKind.CargoCrane:
                        extras.Add(new ObstacleSpot(kind, distance, side, period: Quick(dice.Range(4.6f, 5.6f)), offset: dice.Range(0f, 6f)));
                        break;
                    case ObstacleKind.LogFlume:
                        extras.Add(new ObstacleSpot(kind, distance, side, period: Quick(dice.Range(3.6f, 4.4f)), offset: dice.Range(0f, 5f)));
                        break;
                    case ObstacleKind.CalvingGlacier:
                        extras.Add(new ObstacleSpot(kind, distance, side, period: Quick(dice.Range(7f, 8.5f)), offset: dice.Range(0f, 9f)));
                        break;
                    case ObstacleKind.DynamiteFloat:
                        extras.Add(new ObstacleSpot(kind, distance, side, period: Quick(dice.Range(5.6f, 6.6f)), offset: dice.Range(0f, 7f)));
                        break;
                    case ObstacleKind.LeapingFrogs:
                    {
                        var spread = Mathf.Min(half * 0.62f, 5f);
                        for (var i = -1; i <= 1; i++)
                            if (!ClearOfRocks(distance, i * spread, LeapingFrogs.PadRadius)) return false;
                        extras.Add(new ObstacleSpot(kind, distance, period: Quick(dice.Range(2f, 2.4f)), offset: dice.Range(0f, 3f)));
                        break;
                    }
                    case ObstacleKind.GiantFan:
                        extras.Add(new ObstacleSpot(kind, distance, side, period: Quick(dice.Range(6f, 7.2f)), offset: dice.Range(0f, 8f)));
                        break;
                }

                taken.Add((from, to, Braces(kind), Lanes(kind)));
                return true;
            }

            /// <summary>Clear of rocks all the way down a lane, from one distance to another.</summary>
            bool ClearAlong(float from, float to, float across, float radius)
            {
                for (var d = from; d <= to; d += 2f)
                    if (!ClearOfRocks(d, across, radius)) return false;
                return true;
            }

            bool ClearOfRocks(float distance, float across, float radius)
            {
                var at = river.At(distance);
                var point = at.Centre + at.Left * across;
                foreach (var rock in def.rocks)
                    if (Vector2.Distance(rock.position, point) < rock.radius + radius + RaftRadius + 0.5f) return false;
                return true;
            }
        }

        /// <summary>
        /// A small seeded random number generator (xorshift), so the same seed deals the same layout on every device
        /// and every runtime; UnityEngine.Random and System.Random make no such promise.
        /// </summary>
        sealed class Dice
        {
            uint state;

            public Dice(int seed) => state = (uint)seed * 2654435761u ^ 0x9E3779B9u | 1u;

            uint Next()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state;
            }

            /// <summary>0 (inclusive) to 1 (exclusive).</summary>
            public float Value() => (Next() >> 8) / 16777216f;

            public float Range(float min, float max) => min + (max - min) * Value();

            /// <summary>An integer from min (inclusive) to max (exclusive).</summary>
            public int Range(int min, int max) => max <= min ? min : min + (int)(Next() % (uint)(max - min));
        }
    }
}
