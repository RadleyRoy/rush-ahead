using NUnit.Framework;
using RushAhead.AI;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Tests
{
    /// <summary>An island splitting a straight river 26 m wide: its water, its shores, and crews going round it either side.</summary>
    public class IslandTests
    {
        const float Dt = 1f / 60f;
        const float Nose = 90f;
        const float Length = 40f;

        SimulationMode2D previousMode;
        SimConfig config;
        BlowerDef blower;
        CourseDef def;
        SimWorld world;
        Course course;

        [SetUp]
        public void SetUp()
        {
            previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            config = ScriptableObject.CreateInstance<SimConfig>();
            blower = ScriptableObject.CreateInstance<BlowerDef>();
            def = ScriptableObject.CreateInstance<CourseDef>();
            def.river = new[] { new RiverPoint(0f, 0f, 26f), new RiverPoint(80f, 0f, 26f), new RiverPoint(160f, 0f, 26f), new RiverPoint(240f, 0f, 26f) };
            def.startDistance = 5f;
            def.gates = new[] { 50f, 200f };
            def.flow = 6f;
            // A fast line 7.5 m wide on the left, a safe line 11.5 m wide on the right.
            def.islands = new[] { new IslandSpot(Nose, Length, 2f, 3.5f, 1f) };
            world = new SimWorld(config);
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

        [Test]
        public void TheFastLine_RunsHarder_AndTheSafeLineSlower()
        {
            course = new Course(def);
            var island = course.Islands.All[0];
            var middle = Nose + Length * 0.5f;
            var fast = island.Branch(1, middle).centre;
            var safe = island.Branch(-1, middle).centre;
            Assert.That(fast, Is.GreaterThan(island.Across + island.Spot.halfWidth));
            Assert.That(safe, Is.LessThan(island.Across - island.Spot.halfWidth));
            var fastWater = course.WaterVelocityAt(new Vector2(middle, fast)).x;
            var safeWater = course.WaterVelocityAt(new Vector2(middle, safe)).x;
            Assert.That(fastWater, Is.GreaterThan(def.flow + 1.5f), "the narrow side runs hard");
            Assert.That(safeWater, Is.LessThan(def.flow - 0.8f), "the wide side runs slower");
            Assert.That(course.WaterVelocityAt(new Vector2(Nose - 3f, island.Across + 0.5f)).y, Is.GreaterThan(0.2f), "the water parts round the nose");
        }

        [Test]
        public void ARaftDriftingStraightAtTheNose_IsTurnedAside_AndNeverEndsUpOnTheIsland()
        {
            course = Course.Build(def);
            world.WaterVelocity = course.WaterVelocityAt;
            var island = course.Islands.All[0];
            var boat = world.SpawnBoat(new Vector2(Nose - 25f, island.Across), 0f, new[] { blower, blower, blower, blower });
            for (var i = 0; i < 60 * 14; i++)
            {
                world.Advance(Dt);
                var at = course.River.Project(boat.Body.Position);
                Assert.That(course.Islands.OnIsland(at.Distance, at.Lateral, -0.3f), Is.False, $"on the island at {at.Distance:0.0} m");
            }

            Assert.That(boat.Body.Position.x, Is.GreaterThan(island.Tail + 5f), "carried on past it");
            Assert.That(course.IsOffCourse(boat.Body.Position), Is.False);
        }

        [TestCase(false, TestName = "A crew going for safety takes the safe line, and finishes")]
        [TestCase(true, TestName = "A crew going for speed takes the fast line, and finishes")]
        public void ACrew_GoesRoundTheIsland_TheWayItChose(bool fast)
        {
            course = Course.Build(def);
            world.WaterVelocity = course.WaterVelocityAt;
            var island = course.Islands.All[0];
            var boat = world.SpawnBoat(course.SpawnPosition, course.SpawnAngleRad, new[] { blower, blower, blower, blower });
            var ai = new AiCrew(boat, AiProfile.Preset(AiSkill.Steady), config.tickRate) { River = course.River, FastLines = fast };
            CourseSolvabilityTests.Hook(ai, course, world, config);
            ai.Drive(CrewSectors.DriveOrder(4));
            var race = new Race(course, boat, world.Tick, config.tickRate);
            var side = 0;
            for (var i = 0; i < 60 * 60 && race.Phase != RacePhase.Finished; i++)
            {
                ai.Update(Dt);
                course.Hazards.Step(world.Tick / (double)config.tickRate, new[] { boat });
                world.Advance(Dt);
                race.Step(world.Tick);
                var at = course.River.Project(boat.Body.Position);
                if (side == 0 && at.Distance > Nose + Length * 0.5f) side = island.SideOf(at.Lateral);
            }

            Assert.That(side, Is.EqualTo(fast ? island.FastSide : -island.FastSide), "went round the side it meant to");
            Assert.That(race.Phase, Is.EqualTo(RacePhase.Finished));
            Assert.That(boat.Lives, Is.EqualTo(Race.MaxLives), "without a fall");
            race.Dispose();
        }
    }
}
