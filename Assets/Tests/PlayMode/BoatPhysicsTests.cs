using System.Collections.Generic;
using NUnit.Framework;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Tests
{
    /// <summary>
    /// Real 2D physics, stepped manually so a simulated minute runs in milliseconds.
    /// The boat starts at the origin facing +x; seats are fore-left 0, fore-right 1, aft-left 2, aft-right 3.
    /// </summary>
    public class BoatPhysicsTests
    {
        const float Dt = 1f / 60f;
        const float Backward = Mathf.PI; // air out of the stern drives the boat forward

        SimulationMode2D previousMode;
        SimConfig config;
        BlowerDef leafBlower;
        SimWorld world;
        readonly List<Object> created = new();

        sealed class FixedInput : ICrewInputSource
        {
            public CrewInput Input;
            public bool BurstOnce;

            public CrewInput ReadInput()
            {
                var input = Input;
                input.Burst = BurstOnce;
                BurstOnce = false;
                return input;
            }
        }

        [SetUp]
        public void SetUp()
        {
            previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            config = Track(ScriptableObject.CreateInstance<SimConfig>());
            leafBlower = Track(ScriptableObject.CreateInstance<BlowerDef>());
            world = new SimWorld(config);
        }

        [TearDown]
        public void TearDown()
        {
            world.DestroyAll();
            foreach (var obj in created)
                if (obj != null) Object.DestroyImmediate(obj);
            created.Clear();
            Physics2D.simulationMode = previousMode;
        }

        T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        Boat SpawnBoat(BlowerDef blower = null)
        {
            var b = blower ? blower : leafBlower;
            return world.SpawnBoat(Vector2.zero, 0f, new[] { b, b, b, b });
        }

        static FixedInput Blow(Boat boat, int seat, float airAngle, float throttle = 1f)
        {
            var source = new FixedInput { Input = new CrewInput { AimAngle = airAngle, HasAim = true, Throttle = throttle } };
            boat.Crew[seat].Input = source;
            return source;
        }

        void Run(float seconds)
        {
            for (var t = 0f; t < seconds; t += Dt) world.Advance(Dt);
        }

        [Test]
        public void SymmetricAftThrust_MovesStraightAhead()
        {
            var boat = SpawnBoat();
            Blow(boat, 2, Backward);
            Blow(boat, 3, Backward);

            Run(3f);

            Assert.That(boat.Body.Position.x, Is.GreaterThan(2f), "should have travelled forward");
            Assert.That(Mathf.Abs(boat.Body.Position.y), Is.LessThan(0.05f), "no sideways drift");
            Assert.That(Mathf.Abs(boat.Body.AngleRad * Mathf.Rad2Deg), Is.LessThan(1f), "no turning");
        }

        [Test]
        public void ThrustFromTheLeftSeat_TurnsTheBoatToStarboard()
        {
            var boat = SpawnBoat();
            Blow(boat, 2, Backward);

            Run(2f);

            Assert.That(boat.Body.AngleRad * Mathf.Rad2Deg, Is.LessThan(-5f), "off-centre thrust must turn the boat clockwise");
        }

        [Test]
        public void TopSpeed_IsBoundedByWaterDrag()
        {
            leafBlower.drainPerSec = 0f; // infinite air: a real tank empties in ~4.5 s, long before terminal speed
            var boat = SpawnBoat();
            for (var seat = 0; seat < 4; seat++) Blow(boat, seat, Backward);

            Run(15f);

            // Terminal speed where thrust equals quadratic drag: sqrt(4 * thrust / forwardDrag).
            var terminal = Mathf.Sqrt(4f * leafBlower.thrustNewtons / config.forwardDrag);
            Assert.That(boat.ForwardSpeed, Is.EqualTo(terminal).Within(terminal * 0.1f));
        }

        [Test]
        public void Burst_GivesAnImmediateKick()
        {
            var boat = SpawnBoat();
            Blow(boat, 2, Backward, throttle: 0f).BurstOnce = true;
            Blow(boat, 3, Backward, throttle: 0f).BurstOnce = true;

            world.Advance(Dt);

            var expected = 2f * leafBlower.burstImpulse / config.mass;
            Assert.That(boat.ForwardSpeed, Is.EqualTo(expected).Within(expected * 0.15f));
            Assert.That(boat.Crew[2].Tank.Air, Is.EqualTo(leafBlower.airCapacity - leafBlower.burstCost).Within(0.5f));
        }

        [Test]
        public void HeelingPastTheThreshold_CapsizesThenRecovers()
        {
            config.capsizeAngle = 0.15f;
            config.capsizeHoldSeconds = 0.1f;
            config.recoverySeconds = 0.5f;
            var boat = SpawnBoat();
            var capsizes = 0;
            boat.CapsizedEvent += _ => capsizes++;
            for (var seat = 0; seat < 4; seat++) Blow(boat, seat, -Mathf.PI / 2f); // everyone shoves the boat to port

            for (var i = 0; i < 180 && boat.State == BoatState.Afloat; i++) world.Advance(Dt);
            Assert.That(boat.State, Is.EqualTo(BoatState.Capsized));
            Assert.That(capsizes, Is.EqualTo(1));

            foreach (var crew in boat.Crew) crew.Input = null;
            Run(0.6f);
            Assert.That(boat.State, Is.EqualTo(BoatState.Afloat));
            Assert.That(boat.Tilt.Tilt.magnitude, Is.LessThan(0.05f));
        }

        [Test]
        public void Bracing_PreventsTheSameCapsize()
        {
            config.capsizeAngle = 0.15f;
            config.capsizeHoldSeconds = 0.1f;
            var boat = SpawnBoat();
            for (var seat = 0; seat < 4; seat++) Blow(boat, seat, -Mathf.PI / 2f);
            foreach (var crew in boat.Crew) ((FixedInput)crew.Input).Input.Brace = true;

            Run(3f);

            Assert.That(boat.State, Is.EqualTo(BoatState.Afloat), "braced crew don't blow, so nothing heels the boat");
        }

        [Test]
        public void AirCone_BlowsALooseBarrelAway()
        {
            var boat = SpawnBoat();
            var barrel = Track(PhysicsBodies.CreateBarrel(new Vector2(-2.5f, 0.5f), 0.4f, 8f));
            Blow(boat, 2, Backward);

            Run(1f);

            Assert.That(barrel.transform.position.x, Is.LessThan(-2.8f), "air pushes the barrel along the airflow");
        }

        [Test]
        public void Vacuum_PullsTheBoatTowardTheAimAndTheBarrelIn()
        {
            var vacuum = Track(ScriptableObject.CreateInstance<BlowerDef>());
            vacuum.special = BlowerSpecial.Vacuum;
            var boat = SpawnBoat(vacuum);
            // Far enough astern that the boat, pulled backwards, doesn't reach it within the second.
            var barrel = Track(PhysicsBodies.CreateBarrel(new Vector2(-3.5f, 0.5f), 0.4f, 8f));
            Blow(boat, 2, Backward);
            Blow(boat, 3, Backward);

            Run(1f);

            Assert.That(boat.Body.Position.x, Is.LessThan(-0.2f), "a vacuum aimed astern pulls the boat backwards");
            Assert.That(barrel.transform.position.x, Is.GreaterThan(-3.45f), "the barrel is sucked toward the nozzle");
        }

        [Test]
        public void SameInputs_GiveIdenticalResults()
        {
            (Vector2 position, float angle, float tilt) Scenario()
            {
                var boat = SpawnBoat();
                var aftLeft = Blow(boat, 2, Backward);
                var foreRight = Blow(boat, 1, Mathf.PI / 2f, 0.6f);
                for (var tick = 0; tick < 600; tick++)
                {
                    aftLeft.Input.AimAngle = Backward + 0.8f * Mathf.Sin(tick * 0.05f);
                    if (tick == 200) aftLeft.BurstOnce = true;
                    world.Advance(Dt);
                }

                var result = (boat.Body.Position, boat.Body.AngleRad, boat.Tilt.Tilt.x);
                world.DestroyAll();
                return result;
            }

            // Same binary only: Box2D promises nothing across architectures, which is why the netcode is host-authoritative.
            var first = Scenario();
            var second = Scenario();

            Assert.That(second.position, Is.EqualTo(first.position));
            Assert.That(second.angle, Is.EqualTo(first.angle));
            Assert.That(second.tilt, Is.EqualTo(first.tilt));
        }
    }
}
