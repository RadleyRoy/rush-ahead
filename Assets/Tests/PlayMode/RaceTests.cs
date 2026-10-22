using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RushAhead.AI;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Tests
{
    /// <summary>The race rules on a real raft in real 2D physics: countdown, checkpoints, respawn, currents and the clock.</summary>
    public class RaceTests
    {
        const float Dt = 1f / 60f;

        SimulationMode2D previousMode;
        SimConfig config;
        BlowerDef blower;
        CourseDef def;
        SimWorld world;
        Course course;

        sealed class Push : ICrewInputSource
        {
            public CrewInput Input;
            public CrewInput ReadInput() => Input;
        }

        [SetUp]
        public void SetUp()
        {
            previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            config = ScriptableObject.CreateInstance<SimConfig>();
            blower = ScriptableObject.CreateInstance<BlowerDef>();
            blower.drainPerSec = 0f; // endless air keeps these tests about the rules, not air management

            def = ScriptableObject.CreateInstance<CourseDef>();
            def.river = new[] { new RiverPoint(0f, 0f, 14f), new RiverPoint(40f, 0f, 14f), new RiverPoint(80f, 0f, 14f) };
            def.startDistance = 5f;
            def.gates = new[] { 20f, 40f, 60f };
            world = new SimWorld(config);
            course = null;
        }

        [TearDown]
        public void TearDown()
        {
            world.DestroyAll();
            course?.Destroy();
            Object.DestroyImmediate(def);
            Object.DestroyImmediate(blower);
            Object.DestroyImmediate(config);
            Physics2D.simulationMode = previousMode;
        }

        (Boat boat, Race race, Push[] crew) Start()
        {
            course = Course.Build(def);
            world.WaterVelocity = course.WaterVelocityAt;
            var boat = world.SpawnBoat(Vector2.zero, 0f, new[] { blower, blower, blower, blower });
            var crew = new Push[4];
            for (var i = 0; i < crew.Length; i++) boat.Crew[i].Input = crew[i] = new Push();
            return (boat, new Race(course, boat, world.Tick, config.tickRate), crew);
        }

        void Step(Race race)
        {
            world.Advance(Dt);
            race.Step(world.Tick);
        }

        static void DriveDownstream(Push[] crew)
        {
            foreach (var seat in crew) seat.Input = new CrewInput { AimAngle = Mathf.PI, HasAim = true, Throttle = 1f };
        }

        [Test]
        public void TheCountdown_HoldsTheRaftOnTheLine_ThenTheClockStarts()
        {
            var (boat, race, crew) = Start();
            Assert.That(boat.Body.Position.x, Is.EqualTo(5f).Within(0.01f), "on the start line");
            DriveDownstream(crew);

            while (world.Tick < race.StartTick - 1) Step(race);
            Assert.That(race.Phase, Is.EqualTo(RacePhase.Countdown));
            Assert.That(boat.Body.Velocity.magnitude, Is.LessThan(0.01f), "no jump start");
            Assert.That(RaceClock.CountdownLeft(race.StartTick, world.Tick, config.tickRate), Is.EqualTo(1));

            for (var i = 0; i < 60; i++) Step(race);
            Assert.That(race.Phase, Is.EqualTo(RacePhase.Racing));
            Assert.That(boat.Body.Position.x, Is.GreaterThan(5.2f), "under way (blowers take a moment to wind up)");
            Assert.That(race.SecondsAt(world.Tick), Is.EqualTo(1f).Within(0.02f));
        }

        [Test]
        public void Checkpoints_CountInOrder_AndTheFinishStopsTheClock()
        {
            var (boat, race, crew) = Start();
            DriveDownstream(crew);
            var passedAt = new List<float>();
            for (var i = 0; i < 60 * 60 && race.Phase != RacePhase.Finished; i++)
            {
                var before = boat.GatesPassed;
                Step(race);
                if (boat.GatesPassed != before) passedAt.Add(boat.Body.Position.x);
            }

            Assert.That(race.Phase, Is.EqualTo(RacePhase.Finished));
            Assert.That(passedAt.Count, Is.EqualTo(3));
            for (var i = 0; i < 3; i++) Assert.That(passedAt[i], Is.InRange(def.gates[i], def.gates[i] + 1f), "counted as it crossed");
            Assert.That(boat.ControlsLocked, Is.True, "the time is in");

            var time = race.SecondsAt(world.Tick);
            for (var i = 0; i < 60; i++) Step(race);
            Assert.That(race.SecondsAt(world.Tick), Is.EqualTo(time), "the clock stopped at the line");
            Assert.That(time, Is.EqualTo((boat.FinishTick - race.StartTick) / 60f).Within(1e-4f));
        }

        [Test]
        public void BackingUpThroughACheckpoint_DoesNotCountItTwice()
        {
            var (boat, race, crew) = Start();
            DriveDownstream(crew);
            while (boat.Body.Position.x < 24f) Step(race);
            Assert.That(boat.GatesPassed, Is.EqualTo(1));

            foreach (var seat in crew) seat.Input = new CrewInput { AimAngle = 0f, HasAim = true, Throttle = 1f }; // back upstream
            for (var i = 0; i < 20 * 60 && boat.Body.Position.x > 16f; i++) Step(race);
            Assert.That(boat.Body.Position.x, Is.LessThan(16.5f), "made it back upstream of gate 1");

            DriveDownstream(crew);
            while (boat.Body.Position.x < 30f) Step(race);
            Assert.That(boat.GatesPassed, Is.EqualTo(1), "crossing gate 1 again isn't gate 2");
        }

        [Test]
        public void ACapsize_RespawnsPastTheLastCheckpoint_WithAMomentsProtection()
        {
            config.recoverySeconds = 0.5f;
            var (boat, race, crew) = Start();
            DriveDownstream(crew);
            while (boat.GatesPassed < 1) Step(race);

            config.capsizeAngle = 0.12f;
            config.capsizeHoldSeconds = 0.05f;
            foreach (var seat in crew) seat.Input = new CrewInput { AimAngle = -Mathf.PI / 2f, HasAim = true, Throttle = 1f }; // shove it over
            for (var i = 0; i < 240 && boat.State == BoatState.Afloat; i++) Step(race);
            Assert.That(boat.State, Is.EqualTo(BoatState.Capsized));

            var teleports = boat.TeleportCount;
            for (var i = 0; i < 60 && boat.State == BoatState.Capsized; i++) Step(race);
            Assert.That(boat.TeleportCount, Is.EqualTo(teleports + 1));
            var (position, _) = course.RespawnPose(1);
            Assert.That(Vector2.Distance(boat.Body.Position, position), Is.LessThan(0.2f), "back just past checkpoint 1");
            Assert.That(boat.CapsizeImmunitySeconds, Is.GreaterThan(1.5f));
            Assert.That(boat.CapsizeCount, Is.EqualTo(1));

            // Still shoving: protected for now, so no second capsize straight away.
            for (var i = 0; i < 60; i++) Step(race);
            Assert.That(boat.State, Is.EqualTo(BoatState.Afloat));
        }

        [Test]
        public void ACurrent_SpeedsUpOrHoldsBackTheRaft_AndSlowlyCarriesAnIdleOne()
        {
            float RunWith(float currentSpeed, bool drive)
            {
                world.DestroyAll();
                course?.Destroy();
                def.currents = currentSpeed == 0f ? new CurrentZone[0] : new[] { new CurrentZone(0f, 80f, currentSpeed) };
                var (boat, race, crew) = Start();
                if (drive) DriveDownstream(crew);
                while (race.Phase == RacePhase.Countdown) Step(race);
                for (var i = 0; i < 6 * 60; i++) Step(race);
                return boat.Body.Position.x;
            }

            var still = RunWith(0f, drive: true);
            var helped = RunWith(1f, drive: true);
            var hindered = RunWith(-1f, drive: true);
            Assert.That(helped, Is.GreaterThan(still + 2f), "riding the current");
            Assert.That(hindered, Is.LessThan(still - 2f), "fighting it");

            var drifted = RunWith(1f, drive: false);
            Assert.That(drifted, Is.GreaterThan(def.startDistance + 1f), "an idle raft is carried downstream");
        }
    }

    /// <summary>
    /// The highest-value test in the project: every course must be finishable. A competent autopilot crew of four
    /// runs each one in real physics and must finish inside par times 1.5, so an impossible course, or a tuning
    /// change that makes the raft too slow for its courses, fails here rather than in a player's hands.
    /// </summary>
    public class CourseSolvabilityTests
    {
        const float Dt = 1f / 60f;

        static IEnumerable<TestCaseData> Courses() => AllCourses().Select(c => new TestCaseData(c).SetName($"{c.name} can be finished"));

        [TestCaseSource(nameof(Courses))]
        public void TheAutopilot_FinishesTheCourse_InsideParAndAHalf(CourseDef def)
        {
            var previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            var config = ScriptableObject.CreateInstance<SimConfig>();
            var leafBlower = ScriptableObject.CreateInstance<BlowerDef>(); // the default blower, with real air limits
            var world = new SimWorld(config);
            var course = Course.Build(def);
            try
            {
                world.WaterVelocity = course.WaterVelocityAt;
                world.Drifting.AddRange(course.BarrelBodies);
                var boat = world.SpawnBoat(course.SpawnPosition, course.SpawnAngleRad, new[] { leafBlower, leafBlower, leafBlower, leafBlower });
                var autopilot = new CourseAutopilot(boat, course.River);
                var race = new Race(course, boat, world.Tick, config.tickRate);

                var limit = race.StartTick + Mathf.CeilToInt(def.parSeconds * 1.5f * config.tickRate);
                var furthest = 0f;
                while (race.Phase != RacePhase.Finished && world.Tick < limit)
                {
                    autopilot.Update();
                    world.Advance(Dt);
                    race.Step(world.Tick);
                    furthest = Mathf.Max(furthest, course.River.Project(boat.Body.Position).Distance);
                }

                var time = race.SecondsAt(world.Tick);
                LogFlatOutTime(def, config, leafBlower);
                Debug.Log($"[RushAhead] {def.name}: autopilot {(race.Phase == RacePhase.Finished ? "finished" : "did not finish")} in {time:0.0} s " +
                          $"(par {def.parSeconds:0}, stars {def.Stars(time)}), capsizes {boat.CapsizeCount}, gates {boat.GatesPassed}/{course.Gates.Count}, " +
                          $"furthest {furthest:0} m of {def.FinishDistance:0} m");
                Assert.That(race.Phase, Is.EqualTo(RacePhase.Finished),
                    $"stuck {furthest:0} m down a {def.FinishDistance:0} m course after {time:0} s, with {boat.GatesPassed} of {course.Gates.Count} gates");
            }
            finally
            {
                world.DestroyAll();
                course.Destroy();
                Object.DestroyImmediate(leafBlower);
                Object.DestroyImmediate(config);
                Physics2D.simulationMode = previousMode;
            }
        }

        /// <summary>For setting star times: the same crew pushing as hard as it can, reported but not asserted.</summary>
        static void LogFlatOutTime(CourseDef def, SimConfig config, BlowerDef blower)
        {
            var world = new SimWorld(config);
            var course = Course.Build(def);
            try
            {
                world.WaterVelocity = course.WaterVelocityAt;
                world.Drifting.AddRange(course.BarrelBodies);
                var boat = world.SpawnBoat(course.SpawnPosition, course.SpawnAngleRad, new[] { blower, blower, blower, blower });
                var autopilot = new CourseAutopilot(boat, course.River) { CruiseSpeed = 6f };
                var race = new Race(course, boat, world.Tick, config.tickRate);
                var limit = race.StartTick + Mathf.CeilToInt(def.parSeconds * 1.5f * config.tickRate);
                while (race.Phase != RacePhase.Finished && world.Tick < limit)
                {
                    autopilot.Update();
                    world.Advance(Dt);
                    race.Step(world.Tick);
                }

                Debug.Log($"[RushAhead] {def.name}: flat-out autopilot {(race.Phase == RacePhase.Finished ? "finished" : "did not finish")} " +
                          $"in {race.SecondsAt(world.Tick):0.0} s, capsizes {boat.CapsizeCount}");
            }
            finally
            {
                world.DestroyAll();
                course.Destroy();
            }
        }

        static IEnumerable<CourseDef> AllCourses()
        {
#if UNITY_EDITOR
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:CourseDef", new[] { "Assets/_Project/Content" }))
                yield return UnityEditor.AssetDatabase.LoadAssetAtPath<CourseDef>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
#else
            yield break;
#endif
        }
    }
}
