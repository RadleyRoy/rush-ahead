using NUnit.Framework;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Tests
{
    /// <summary>Power-up crates and the three items, on a straight, still river 14 m wide.</summary>
    public class PowerUpTests
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
            def.river = new[] { new RiverPoint(0f, 0f, 14f), new RiverPoint(60f, 0f, 14f), new RiverPoint(120f, 0f, 14f) };
            def.startDistance = 5f;
            def.gates = new[] { 40f, 80f, 100f };
            def.flow = 0f;
            def.crates = new[] { new CrateSpot(30f, 0f) };
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

        void Build()
        {
            course = Course.Build(def);
            world.WaterVelocity = course.WaterVelocityAt;
        }

        Boat Spawn(Vector2 position)
        {
            var boat = world.SpawnBoat(position, 0f, new[] { blower, blower, blower, blower });
            foreach (var crew in boat.Crew) crew.Input = new Hold();
            return boat;
        }

        double Seconds => world.Tick / (double)config.tickRate;

        void Run(float seconds, params Boat[] boats)
        {
            for (var i = 0; i < Mathf.CeilToInt(seconds * 60f); i++)
            {
                course.Hazards.Step(Seconds, boats);
                world.Advance(Dt);
            }
        }

        [Test]
        public void ACrate_GivesARaftAnItem_AndComesBackLater()
        {
            Build();
            var powerUps = course.Hazards.PowerUps;
            Assert.That(powerUps.Count, Is.EqualTo(1));
            var boat = Spawn(new Vector2(30f, 0f));
            Run(0.1f, boat);
            Assert.That(boat.Item, Is.Not.EqualTo(PowerUp.None), "took what was in it");
            Assert.That(boat.Pickups, Is.EqualTo(1));
            Assert.That(powerUps.Ready(0, Seconds), Is.False, "the crate's gone");
            Assert.That(powerUps.ReadyMask(Seconds), Is.EqualTo(0u));

            var held = boat.Item;
            Run(PowerUps.RespawnSeconds + 0.2f, boat);
            Assert.That(powerUps.Ready(0, Seconds), Is.True, "and back again");
            Assert.That(boat.Item, Is.EqualTo(held), "a raft carries one item at a time: it can't take another");
            Assert.That(boat.Pickups, Is.EqualTo(1));
        }

        [Test]
        public void ARaftWaitingOnTheLine_TakesNothing()
        {
            Build();
            var boat = Spawn(new Vector2(30f, 0f));
            boat.ControlsLocked = true;
            Run(0.2f, boat);
            Assert.That(boat.Item, Is.EqualTo(PowerUp.None));
        }

        [Test]
        public void ATurbo_SurgesTheRaftDownTheRiver_ForAFewSeconds()
        {
            Build();
            var boat = Spawn(new Vector2(10f, 3f));
            var still = Spawn(new Vector2(10f, -3f));
            boat.Item = PowerUp.Turbo;
            boat.ItemRequested = true;
            Run(0.05f, boat, still);
            Assert.That(boat.Item, Is.EqualTo(PowerUp.None), "used up");
            Assert.That(((IBoatState)boat).Turbo, Is.True);
            Run(PowerUps.TurboSeconds, boat, still);
            Assert.That(boat.Body.Velocity.x, Is.GreaterThan(7f), "away down the river");
            Assert.That(boat.Body.Velocity.x, Is.LessThan(12f), "but no faster than the water plus its margin");
            Assert.That(((IBoatState)boat).Turbo, Is.False, "and then it's spent");
            Assert.That(still.Body.Velocity.magnitude, Is.LessThan(0.5f));
        }

        [Test]
        public void AShield_SavesTheRaftFromOneFall_ThenItsGone()
        {
            Build();
            var boat = Spawn(new Vector2(10f, 0f));
            var race = new Race(course, boat, world.Tick, config.tickRate);
            while (race.Phase == RacePhase.Countdown) Step(race, boat);
            boat.Shielded = true;

            boat.Body.Teleport(new Vector2(50f, 9f), 0f); // over the edge
            Step(race, boat);
            Assert.That(boat.State, Is.EqualTo(BoatState.Afloat), "bounced back onto the water");
            Assert.That(course.IsOffCourse(boat.Body.Position), Is.False);
            Assert.That(boat.Shielded, Is.False, "the bubble popped");
            Assert.That(boat.ShieldPops, Is.EqualTo(1));

            boat.Body.Teleport(new Vector2(60f, 9f), 0f);
            Step(race, boat);
            Assert.That(boat.State, Is.EqualTo(BoatState.Fallen), "no bubble the second time");
            for (var i = 0; i < 200 && boat.State != BoatState.Afloat; i++) Step(race, boat);
            Assert.That(boat.Lives, Is.EqualTo(Race.MaxLives - 1), "only the second fall cost a life");
            race.Dispose();
        }

        void Step(Race race, Boat boat)
        {
            course.Hazards.Step(Seconds, new[] { boat });
            world.Advance(Dt);
            race.Step(world.Tick);
        }

        [Test]
        public void AShield_StopsOneCapsize()
        {
            Build();
            var boat = Spawn(new Vector2(10f, 0f));
            boat.Shielded = true;
            boat.Shove(Vector2.up * 120f, 24f);
            Run(1.5f, boat);
            Assert.That(boat.CapsizeCount, Is.Zero, "the bubble kept it upright");
            Assert.That(boat.ShieldPops, Is.EqualTo(1));

            Run(1.5f, boat);
            boat.Shove(Vector2.up * 120f, 24f);
            Run(1.5f, boat);
            Assert.That(boat.CapsizeCount, Is.EqualTo(1), "but only once");
        }

        [Test]
        public void TheAirHorn_ShovesOtherRaftsNearby_AndNobodyFarAway()
        {
            Build();
            var boat = Spawn(new Vector2(10f, 0f));
            var near = Spawn(new Vector2(16f, 0f));
            var far = Spawn(new Vector2(40f, 0f));
            boat.Item = PowerUp.Horn;
            boat.ItemRequested = true;
            Run(0.3f, boat, near, far);
            Assert.That(boat.HornCount, Is.EqualTo(1));
            Assert.That(near.Body.Velocity.x, Is.GreaterThan(0.8f), "blown away from the horn");
            Assert.That(far.Body.Velocity.magnitude, Is.LessThan(0.05f), "out of earshot");
            Assert.That(boat.Body.Velocity.magnitude, Is.LessThan(0.05f), "the horn doesn't push its own raft");
        }
    }
}
