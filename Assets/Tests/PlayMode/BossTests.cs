using NUnit.Framework;
using RushAhead.AI;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Tests
{
    /// <summary>Old Snapjaw on a long straight river 16 m wide, flowing at 6 m/s.</summary>
    public class BossTests
    {
        const float Dt = 1f / 60f;

        SimulationMode2D previousMode;
        SimConfig config;
        BlowerDef blower;
        CourseDef def;
        SimWorld world;
        Course course;

        sealed class Hold : ICrewInputSource
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
            def = ScriptableObject.CreateInstance<CourseDef>();
            def.river = new[] { new RiverPoint(0f, 0f, 16f), new RiverPoint(150f, 0f, 16f), new RiverPoint(300f, 0f, 16f), new RiverPoint(460f, 0f, 16f) };
            def.startDistance = 8f;
            def.gates = new[] { 150f, 300f, 440f };
            def.flow = 6f;
            def.boss = true;
            world = new SimWorld(config);
            course = Course.Build(def);
            world.WaterVelocity = course.WaterVelocityAt;
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

        Boat Spawn()
        {
            var boat = world.SpawnBoat(course.SpawnPosition, course.SpawnAngleRad, new[] { blower, blower, blower, blower });
            foreach (var crew in boat.Crew) crew.Input = new Hold();
            return boat;
        }

        void Step(Race race, Boat boat, AiCrew ai = null)
        {
            ai?.Update(Dt);
            course.Hazards.Step(world.Tick / (double)config.tickRate, new[] { boat });
            world.Advance(Dt);
            race.Step(world.Tick);
        }

        [Test]
        public void Snapjaw_CatchesARaftThatSitsStill_ThenComesUpWellBehindIt()
        {
            var boss = course.Hazards.Boss;
            Assert.That(boss, Is.Not.Null, "the boss course has its croc");
            var boat = Spawn();
            var race = new Race(course, boat, world.Tick, config.tickRate);
            var rose = false;
            for (var i = 0; i < 60 * 40 && boss.Chomps == 0; i++)
            {
                Step(race, boat);
                if (boss.Mood == BossMood.Rising) rose = true;
            }

            Assert.That(rose, Is.True, "it rose with a roar first");
            Assert.That(boss.Chomps, Is.EqualTo(1), "it caught a raft nobody was paddling");
            Assert.That(boss.Mood, Is.EqualTo(BossMood.Diving), "and dived after biting");

            for (var i = 0; i < 60 * 12 && boss.Mood != BossMood.Chasing; i++) Step(race, boat);
            Assert.That(boat.Lives, Is.EqualTo(Race.MaxLives - 1), "the bite cost a life");
            Assert.That(boss.Mood, Is.EqualTo(BossMood.Chasing), "it came back up");
            var gap = course.River.Project(boat.Body.Position).Distance - boss.Distance;
            Assert.That(gap, Is.GreaterThan(20f), "well behind the raft");
            race.Dispose();
        }

        [Test]
        public void AnAceCrew_KeepsAheadOfSnapjaw_AllTheWay()
        {
            var boss = course.Hazards.Boss;
            var boat = world.SpawnBoat(course.SpawnPosition, course.SpawnAngleRad, new[] { blower, blower, blower, blower });
            var ai = new AiCrew(boat, AiProfile.Preset(AiSkill.Ace), config.tickRate) { River = course.River };
            CourseSolvabilityTests.Hook(ai, course, world, config);
            ai.Drive(CrewSectors.DriveOrder(4));
            var race = new Race(course, boat, world.Tick, config.tickRate);
            var chased = false;
            for (var i = 0; i < 60 * 90 && race.Phase != RacePhase.Finished; i++)
            {
                Step(race, boat, ai);
                if (boss.Mood == BossMood.Chasing) chased = true;
            }

            Assert.That(chased, Is.True, "it gave chase");
            Assert.That(race.Phase, Is.EqualTo(RacePhase.Finished));
            Assert.That(boss.Chomps, Is.Zero, "a good crew escapes");
            race.Dispose();
        }

        [Test]
        public void TheAirHorn_SendsSnapjawDiving()
        {
            var boss = course.Hazards.Boss;
            var boat = Spawn();
            var race = new Race(course, boat, world.Tick, config.tickRate);
            for (var i = 0; i < 60 * 30 && !(boss.Mood == BossMood.Chasing && course.River.Project(boat.Body.Position).Distance - boss.Distance < 14f); i++)
                Step(race, boat);
            Assert.That(boss.Mood, Is.EqualTo(BossMood.Chasing), "it's closing in");

            boat.Item = PowerUp.Horn;
            boat.ItemRequested = true;
            Step(race, boat);
            Step(race, boat);
            Assert.That(boss.Mood, Is.EqualTo(BossMood.Diving), "the horn sent it down");
            Assert.That(boss.Chomps, Is.Zero);
            race.Dispose();
        }
    }
}
