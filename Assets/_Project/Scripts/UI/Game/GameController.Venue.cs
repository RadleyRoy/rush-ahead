using RushAhead.Content;
using RushAhead.Net;
using RushAhead.Render;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>Venues: the practice lagoon (0) or a course.</summary>
    public sealed partial class GameController
    {
        static bool showHitZones = CommandLine.Has("-rushahead-hitzones");
        HitZoneOverlay hitZones;

        /// <summary>
        /// Lines on the water showing what really hits on a course (colliders and each obstacle's reach): from
        /// <c>-rushahead-hitzones</c> or the developer overlay.
        /// </summary>
        public bool ShowHitZones
        {
            get => showHitZones;
            set
            {
                showHitZones = value;
                if (value && hitZones == null && courseView != null)
                    hitZones = HitZoneOverlay.Attach(courseView.transform, course.Hazards, ClockSeconds, palette);
                if (hitZones != null) hitZones.gameObject.SetActive(value);
            }
        }

        /// <summary>Whether a power-up crate is there to take: the host's power-ups, or a joined player's snapshots. All of them in the lobby.</summary>
        bool CrateReady(int crate)
        {
            if (course == null || Phase == RoomPhase.Lobby) return true;
            if (Mode == NetMode.Joined) return client.Boats.Count == 0 || (client.Boats[0].CratesReady & (1u << crate)) != 0;
            return course.Hazards.PowerUps.Ready(crate, runner.World.Tick / (double)config.tickRate);
        }

        /// <summary>Tears down the current venue and builds another. Joined, its barrels only show where the host says.</summary>
        void LoadVenue(int index)
        {
            UnloadVenue();
            venue = Mathf.Clamp(index, 0, courses.Length);
            if (venue == 0)
            {
                BuildLagoon();
            }
            else
            {
                course = Course.Build(courses[venue - 1], layoutSeed);
                Debug.Log($"[RushAhead] {course.Def.displayName}: round dealt from seed {layoutSeed}, {course.Layout.Count} obstacles");
                courseView = CourseView.Build(course, palette);
                courseView.Obstacles.Clock = ClockSeconds;
                if (showHitZones) ShowHitZones = true;
                var boosts = courseView.Obstacles.Boosts;
                boosts.Watched = () => ViewedBoat.RenderPosition;
                boosts.Entered += () =>
                {
                    chase?.Kick(1f);
                    var audio = GameAudio.Instance;
                    if (audio != null) audio.Play(audio.Bank.Burst, 0.9f, 1.45f);
                };
                courseView.Obstacles.Crates.Ready = CrateReady;
                if (course.Hazards.Boss != null) BossView.Build(palette, courseView.transform).State = BossState;
                barrels.AddRange(course.Barrels);
                barrelBodies.AddRange(course.BarrelBodies);
                runner.World.WaterVelocity = course.WaterVelocityAt;
            }

            runner.World.Drifting.AddRange(course != null ? course.DriftingBodies : barrelBodies);
            if (Mode == NetMode.Joined)
                foreach (var barrel in barrels) PhysicsBodies.Freeze(barrel);
            recordedRun = (-1, -1);
            layoutRaced = false;
            ApplyWeather(true);
        }

        System.Func<double> exhibitClock;

        /// <summary>
        /// The gallery: a course built from <paramref name="def"/> with exactly <paramref name="layout"/>, its obstacles
        /// posed (and stepped) on <paramref name="clock"/> rather than the race clock, every raft moored at
        /// <paramref name="raftAt"/> facing <paramref name="headingRad"/>.
        /// </summary>
        public Course ShowExhibit(CourseDef def, CourseLayout layout, System.Func<double> clock, Vector2 raftAt, float headingRad)
        {
            UnloadVenue();
            course = Course.Build(def, 0, layout);
            courseView = CourseView.Build(course, palette);
            exhibitClock = clock;
            courseView.Obstacles.Clock = ClockSeconds;
            if (showHitZones) ShowHitZones = true;
            barrels.AddRange(course.Barrels);
            barrelBodies.AddRange(course.BarrelBodies);
            runner.World.WaterVelocity = course.WaterVelocityAt;
            runner.World.Drifting.AddRange(course.DriftingBodies);
            for (var i = 0; i < rafts.Count; i++)
            {
                var boat = rafts[i].Boat;
                boat.ResetTo(raftAt + new Vector2(0f, i * 4f), headingRad);
                boat.ClearRun();
                boat.ControlsLocked = true;
                boat.Moored = true;
            }

            ExhibitWeather(Weather.Clear);
            return course;
        }

        /// <summary>The gallery: the weather over an exhibit (rain with its gusts, fog, night with the lamps lit).</summary>
        internal void ExhibitWeather(Weather weather)
        {
            if (weatherView == null || course == null) return;
            course.Gusts = weather == Weather.Rain ? new Gusts(7) : null;
            weatherView.Show(weather, course.Def.scene, true);
            courseView.SetNight(weather == Weather.Night);
            shownWeather = weather;
        }

        /// <summary>The gallery: the exhibit's view (for its knockout banners) and the art (for Old Snapjaw's picture).</summary>
        internal CourseView ExhibitView => courseView;

        internal ArtPalette ExhibitPalette => palette;

        /// <summary>Host or solo: deals a fresh layout of obstacles for the next round (rebuilding the course with it).</summary>
        void DealNewLayout()
        {
            if (Mode == NetMode.Joined || venue == 0 || !courses[venue - 1].HasMenu) return;
            layoutSeed = Random.Range(1, int.MaxValue);
            LoadVenue(venue);
        }

        void UnloadVenue()
        {
            foreach (var raft in rafts)
            {
                raft.Race?.Dispose();
                raft.Race = null;
            }

            course?.Destroy();
            course = null;
            if (courseView != null) Destroy(courseView.gameObject);
            courseView = null;
            hitZones = null; // went with the course view
            if (lagoon != null)
            {
                lagoon.SetActive(false); // out of the physics world now, not at the end of the frame
                Destroy(lagoon);
            }

            lagoon = null;
            barrels.Clear();
            barrelBodies.Clear();
            runner.World.Drifting.Clear();
            runner.World.WaterVelocity = null;
            exhibitClock = null;
        }

        void BuildLagoon()
        {
            lagoon = new GameObject("Practice lagoon");
            var scenery = Shapes3D.Group(lagoon.transform, "Scenery");
            var random = new System.Random(7); // same scenery every run, and on every player's device
            var h = ArenaHalfSize;

            CourseDressing.Water(scenery, Vector2.zero, 2f * h + new Vector2(120f, 120f), palette);

            // Banks all round, walked counter-clockwise so each builds outward, away from the water: low land with a
            // rocky shore rising into wooded hills, like the courses' (blocky cliffs if the land's art isn't there).
            var corners = new[] { new Vector2(-h.x, -h.y), new Vector2(h.x, -h.y), new Vector2(h.x, h.y), new Vector2(-h.x, h.y) };
            PhysicsBodies.CreateWallLoop(corners).transform.SetParent(lagoon.transform, true);
            if (palette.HasLand)
            {
                var land = GorgeTerrain.BuildPond(h, CourseScene.Creek, palette, scenery);
                CourseView.Plant(land, CourseScene.Creek, BankStyle.For(CourseScene.Creek, palette), palette, scenery, random);
                for (var i = 0; i < corners.Length; i++) RockyShore(scenery, corners[i], corners[(i + 1) % corners.Length], land, random);
            }
            else
            {
                for (var i = 0; i < corners.Length; i++)
                    CourseDressing.CliffBank(scenery, corners[i], corners[(i + 1) % corners.Length], 7f, palette, random);
            }

            // A slalom down the middle, then a gate between two rocks with 0.8 m to spare for the 3.2 m raft.
            foreach (var (position, radius) in new[]
                     {
                         (new Vector2(-4f, 6f), 1.5f), (new Vector2(4f, -6f), 1.5f), (new Vector2(12f, 6f), 1.5f), (new Vector2(20f, -6f), 1.5f),
                         (new Vector2(27f, 3.2f), 1.2f), (new Vector2(27f, -3.2f), 1.2f),
                     })
            {
                PhysicsBodies.CreateRock(position, radius).transform.SetParent(lagoon.transform, true);
                CourseDressing.Rock(scenery, position, radius, palette, random);
            }

            CourseDressing.Gate(scenery, new Vector2(27f, 3.2f), new Vector2(27f, -3.2f), palette);

            // Barrels are built in the same order on every device: snapshots identify them by position in this list.
            for (var i = 0; i < 8; i++)
            {
                var barrel = PhysicsBodies.CreateBarrel(BarrelSpot(i), BarrelRadius, 8f);
                barrel.transform.SetParent(lagoon.transform, true);
                CourseDressing.Barrel(scenery, barrel.transform, BarrelRadius, palette);
                barrels.Add(barrel);
                barrelBodies.Add(barrel.GetComponent<Rigidbody2D>());
            }
        }

        /// <summary>Scanned boulders along one side of the lagoon, just outside the wall the raft bumps: half sunk, in uneven clumps.</summary>
        void RockyShore(Transform parent, Vector2 from, Vector2 to, GorgeTerrain.Land land, System.Random random)
        {
            if (palette.boulderMeshes.Length == 0) return;
            var along = to - from;
            var length = along.magnitude;
            var direction = along / length;
            var outward = new Vector2(direction.y, -direction.x);
            var shore = Shapes3D.Group(parent, "Shore");
            for (var d = 0f; d < length; d += 1.4f + 3.6f * (float)(random.NextDouble() * random.NextDouble()))
            {
                var spot = from + direction * d + outward * (0.4f + 1.4f * (float)random.NextDouble());
                var pick = random.Next(palette.boulderMeshes.Length);
                var width = 1.1f + 2.2f * (float)(random.NextDouble() * random.NextDouble());
                Shapes3D.Model("Boulder", palette.boulderMeshes[pick], palette.boulderMaterials[pick], shore, SimSpace.ToWorld(spot, Mathf.Min(0.1f, land.Height(spot))),
                    width, (float)random.NextDouble() * 360f, 0.5f);
            }
        }

        static Vector2 BarrelSpot(int i) => new(-15f + 1.8f * (i % 4), -2.7f + 1.8f * (i / 4) + 0.9f * (i % 2));
    }
}
