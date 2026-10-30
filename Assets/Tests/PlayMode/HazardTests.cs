using NUnit.Framework;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Tests
{
    /// <summary>The moving obstacles on a straight, still river: what each does to a raft, and that they run on the clock alone.</summary>
    public class HazardTests
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
            def.river = new[] { new RiverPoint(0f, 0f, 14f), new RiverPoint(40f, 0f, 14f), new RiverPoint(80f, 0f, 14f) };
            def.startDistance = 5f;
            def.gates = new[] { 20f, 40f, 60f };
            def.flow = 0f;
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

        void Build()
        {
            course = Course.Build(def);
            world.WaterVelocity = course.WaterVelocityAt;
        }

        Boat Spawn(Vector2 position, bool brace = false)
        {
            var boat = world.SpawnBoat(position, 0f, new[] { blower, blower, blower, blower });
            foreach (var crew in boat.Crew) crew.Input = new Hold { Input = new CrewInput { Brace = brace } };
            return boat;
        }

        double Seconds => world.Tick / (double)config.tickRate;

        void Step(params Boat[] boats)
        {
            course.Hazards.Step(Seconds, boats);
            world.Advance(Dt);
        }

        [Test]
        public void ASwingingLog_KnocksAnUnbracedRaftOver()
        {
            def.swingers = new[] { new SwingSpot(40f, 4f, 1f, 3f) }; // starts out at the side of its swing
            Build();
            var boat = Spawn(new Vector2(40f, 0f));
            var shoves = 0;
            boat.Shoved += _ => shoves++;

            for (var i = 0; i < 60 * 6; i++) Step(boat);
            Assert.That(shoves, Is.GreaterThanOrEqualTo(1), "the log came through");
            Assert.That(boat.CapsizeCount, Is.GreaterThanOrEqualTo(1), "and over it went");
        }

        [Test]
        public void ABracedCrew_DucksUnderTheLog()
        {
            def.swingers = new[] { new SwingSpot(40f, 4f, 1f, 3f) }; // starts out at the side of its swing
            Build();
            var boat = Spawn(new Vector2(40f, 0f), brace: true);
            var shoves = 0;
            boat.Shoved += _ => shoves++;

            for (var i = 0; i < 60 * 6; i++) Step(boat);
            Assert.That(shoves, Is.Zero);
            Assert.That(boat.CapsizeCount, Is.Zero);
        }

        [Test]
        public void AGeyser_ThrowsARaftOutward_AndBracingTakesMostOfIt()
        {
            def.geysers = new[] { new GeyserSpot(new Vector2(20f, 3f), 1.5f, 4f, 0f), new GeyserSpot(new Vector2(20f, -3f), 1.5f, 4f, 0f) };
            Build();
            var loose = Spawn(new Vector2(20f, 3.4f));
            var braced = Spawn(new Vector2(20f, -3.4f), brace: true);
            float looseTilt = 0f, bracedTilt = 0f, looseSpeed = 0f, bracedSpeed = 0f;

            Assert.That(course.Hazards.GeyserWarning(0, 1.0, out var early), Is.Zero, "quiet at first");
            Assert.That(early, Is.False);
            for (var i = 0; i < 60 * 5; i++)
            {
                Step(loose, braced);
                looseTilt = Mathf.Max(looseTilt, loose.TiltDanger);
                bracedTilt = Mathf.Max(bracedTilt, braced.TiltDanger);
                looseSpeed = Mathf.Max(looseSpeed, loose.Body.Velocity.y);
                bracedSpeed = Mathf.Max(bracedSpeed, -braced.Body.Velocity.y);
            }

            Assert.That(looseSpeed, Is.GreaterThan(0.5f), "thrown away from the spout (up the screen, away from its middle)");
            Assert.That(bracedSpeed, Is.LessThan(looseSpeed * 0.5f));
            Assert.That(bracedTilt, Is.LessThan(looseTilt));
            Assert.That(braced.CapsizeCount, Is.Zero, "a braced crew rides it out");
            Assert.That(looseTilt, Is.GreaterThan(0.5f), "an unbraced one lurches hard");
        }

        [Test]
        public void AWheelsArms_SweepARaftAlong()
        {
            def.wheels = new[] { new WheelSpot(new Vector2(40f, 0f), 4f, 3, 90f) };
            Build();
            var boat = Spawn(new Vector2(40f, 5f));
            var start = boat.Body.Position;
            for (var i = 0; i < 60 * 3; i++) Step(boat);
            Assert.That(Vector2.Distance(boat.Body.Position, start), Is.GreaterThan(1f));
        }

        [Test]
        public void ACrocodile_WaitsOffTheWater_LungesOnTime_AndRamsWhatsThere()
        {
            def.crocodiles = new[] { new CrocSpot(40f, 1f, 0.4f, 6f, 0f) };
            Build();
            var hazards = course.Hazards;
            var waiting = hazards.Croc(0, 1.0);
            Assert.That(course.River.Project(waiting.position).Lateral, Is.GreaterThan(7f), "waiting on the left edge");
            Assert.That(waiting.lunging, Is.False);
            var out_ = hazards.Croc(0, 4.5);
            Assert.That(course.River.Project(out_.position).Lateral, Is.EqualTo(7f - 0.4f * 14f).Within(0.05f), "out in the river");
            Assert.That(out_.lunging, Is.True);

            var boat = Spawn(new Vector2(40f, 2f));
            var pushed = 0f;
            while (Seconds < 4.5)
            {
                Step(boat);
                pushed = Mathf.Min(pushed, boat.Body.Velocity.y);
            }

            Assert.That(pushed, Is.LessThan(-1f), "rammed toward the far side");
            var t = (world.Tick - 1) / (double)config.tickRate; // the pose the last step moved it to
            var hit = Physics2D.OverlapPoint(hazards.Croc(0, t).position);
            Assert.That(hit, Is.Not.Null);
            Assert.That(hit.attachedRigidbody.name, Does.StartWith("Crocodile"), "its body is where the clock says");
        }
    }
}
