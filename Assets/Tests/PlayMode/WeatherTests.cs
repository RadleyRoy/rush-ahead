using NUnit.Framework;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Tests
{
    /// <summary>The rain's gusts of wind, on a straight, still river 30 m wide.</summary>
    public class WeatherTests
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
            def.river = new[] { new RiverPoint(0f, 0f, 30f), new RiverPoint(100f, 0f, 30f), new RiverPoint(200f, 0f, 30f) };
            def.startDistance = 5f;
            def.gates = new[] { 100f, 180f };
            def.flow = 0f;
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

        Boat Spawn(Vector2 position, bool brace)
        {
            var boat = world.SpawnBoat(position, 0f, new[] { blower, blower, blower, blower });
            foreach (var crew in boat.Crew) crew.Input = new Hold { Input = new CrewInput { Brace = brace } };
            return boat;
        }

        [Test]
        public void TheSameSeed_BlowsTheSameGusts_EverySixToTenSeconds()
        {
            var a = new Gusts(42);
            var b = new Gusts(42);
            var c = new Gusts(43);
            var differs = false;
            var starts = 0;
            var wasBlowing = false;
            var lastStart = -100.0;
            for (var t = 0.0; t < 120.0; t += 0.05)
            {
                var gust = a.At(t);
                Assert.That(b.At(t), Is.EqualTo(gust));
                if (c.At(t).side != gust.side || (c.At(t).strength > 0f) != (gust.strength > 0f)) differs = true;
                var blowing = gust.strength > 0f;
                if (blowing && !wasBlowing)
                {
                    if (starts > 0) Assert.That(t - lastStart, Is.InRange(5.9, 10.2), "between one gust and the next");
                    lastStart = t;
                    starts++;
                }

                wasBlowing = blowing;
            }

            Assert.That(starts, Is.InRange(11, 20));
            Assert.That(differs, Is.True, "another seed blows other gusts");
            Assert.That(a.At(lastStart - 0.6).warning, Is.GreaterThan(0.3f), "a warning before each gust");
        }

        [Test]
        public void AGust_PushesAnIdleRaftAcross_AndABracedOneLess()
        {
            course = Course.Build(def);
            world.WaterVelocity = course.WaterVelocityAt;
            course.Gusts = new Gusts(7);
            course.Hazards.RaceStart = 0.0;
            var idle = Spawn(new Vector2(30f, 0f), brace: false);
            var braced = Spawn(new Vector2(90f, 0f), brace: true);
            var first = 0.0;
            while (course.Gusts.At(first).strength <= 0f) first += 0.05;
            var side = course.Gusts.At(first).side;
            for (var t = 0.0; t < first + Gusts.Blow + 0.5; t += Dt)
            {
                course.Hazards.Step(t, new[] { idle, braced });
                world.Advance(Dt);
            }

            var pushed = idle.Body.Position.y * side;
            var held = braced.Body.Position.y * side;
            Assert.That(pushed, Is.GreaterThan(0.8f), "blown across the river, the way the gust blows");
            Assert.That(held, Is.LessThan(pushed * 0.7f), "a braced crew holds better");
            Assert.That(held, Is.GreaterThan(0f), "but still feels it");
        }

        [Test]
        public void NoGustsBlow_BeforeTheRaceStarts()
        {
            course = Course.Build(def);
            world.WaterVelocity = course.WaterVelocityAt;
            course.Gusts = new Gusts(7);
            var boat = Spawn(new Vector2(30f, 0f), brace: false);
            for (var t = 0.0; t < 30.0; t += Dt)
            {
                course.Hazards.Step(t, new[] { boat });
                world.Advance(Dt);
            }

            Assert.That(Mathf.Abs(boat.Body.Position.y), Is.LessThan(0.05f));
        }
    }
}
