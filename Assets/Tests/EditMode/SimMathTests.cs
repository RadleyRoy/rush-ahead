using NUnit.Framework;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Tests
{
    public class WaterDragTests
    {
        [Test]
        public void Drag_OpposesVelocity()
        {
            Assert.That(WaterDrag.LocalForce(new Vector2(2f, 0f), 5f, 40f).x, Is.LessThan(0f));
            Assert.That(WaterDrag.LocalForce(new Vector2(0f, -3f), 5f, 40f).y, Is.GreaterThan(0f));
            Assert.That(WaterDrag.Torque(1.5f, 18f), Is.LessThan(0f));
        }

        [Test]
        public void Keel_ResistsSidewaysMoreThanForwardAtTheSameSpeed()
        {
            var config = ScriptableObject.CreateInstance<SimConfig>();
            var forward = WaterDrag.LocalForce(new Vector2(3f, 0f), config.forwardDrag, config.lateralDrag).magnitude;
            var sideways = WaterDrag.LocalForce(new Vector2(0f, 3f), config.forwardDrag, config.lateralDrag).magnitude;

            Assert.That(sideways / forward, Is.GreaterThanOrEqualTo(6f), "lateral/forward drag is what makes the boat track");
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Drag_IsQuadraticInSpeed()
        {
            var slow = WaterDrag.LocalForce(new Vector2(1.5f, 0f), 5f, 40f).x;
            var fast = WaterDrag.LocalForce(new Vector2(3f, 0f), 5f, 40f).x;
            Assert.That(fast, Is.EqualTo(4f * slow).Within(1e-4f));
        }
    }

    public class BlowerMathTests
    {
        [Test]
        public void Reaction_PushesBoatOppositeToTheAir()
        {
            var force = BlowerMath.ReactionForce(Vector2.right, 45f, 1f, BlowerSpecial.None);
            Assert.That(force.x, Is.EqualTo(-45f).Within(1e-4f));
            Assert.That(force.y, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void Vacuum_PullsBoatTowardWhereItPoints()
        {
            var force = BlowerMath.ReactionForce(Vector2.right, 40f, 1f, BlowerSpecial.Vacuum);
            Assert.That(force.x, Is.EqualTo(40f).Within(1e-4f));
            Assert.That(BlowerMath.ConeSign(BlowerSpecial.Vacuum), Is.EqualTo(-1f));
        }

        [Test]
        public void Spool_ReachesAbout63PercentAfterOneTimeConstant()
        {
            const float dt = 1f / 60f, tau = 0.25f;
            var spool = 0f;
            for (var t = 0f; t < tau - 1e-5f; t += dt) spool = BlowerMath.Spool(spool, 1f, dt, tau, 0.1f);
            Assert.That(spool, Is.EqualTo(1f - Mathf.Exp(-1f)).Within(0.02f));
        }

        [Test]
        public void Spool_WindsDownOnItsOwnTimeConstant()
        {
            var down = BlowerMath.Spool(1f, 0f, 0.1f, 10f, 0.1f);
            Assert.That(down, Is.EqualTo(Mathf.Exp(-1f)).Within(1e-3f), "spool-down must ignore the slow spool-up constant");
        }

        [Test]
        public void Cone_IsStrongestStraightAheadAndFadesWithDistance()
        {
            float Weight(Vector2 offset) => BlowerMath.ConeWeight(Vector2.right, offset, 18f, 4.5f, 1.6f);

            Assert.That(Weight(new Vector2(1f, 0f)), Is.GreaterThan(Weight(new Vector2(3f, 0f))));
            Assert.That(Weight(new Vector2(1f, 0f)), Is.GreaterThan(Weight(new Vector2(1f, 0.25f))));
            Assert.That(Weight(new Vector2(5f, 0f)), Is.EqualTo(0f), "beyond range");
            Assert.That(Weight(new Vector2(1f, 1f)), Is.EqualTo(0f), "45 degrees is outside an 18 degree half-angle");
            Assert.That(Weight(new Vector2(-1f, 0f)), Is.EqualTo(0f), "behind the nozzle");
        }

        [Test]
        public void ThrustOnTheLeftSide_TurnsTheBowToStarboard()
        {
            // Aft-left seat pushing the hull forward: the left side is driven ahead, so the bow swings right (clockwise).
            var aftLeft = new Vector2(-0.85f, 0.5f);
            Assert.That(BlowerMath.Torque(aftLeft, new Vector2(45f, 0f)), Is.LessThan(0f));
        }

        [Test]
        public void SymmetricAftThrust_ProducesNoTorque()
        {
            var forward = new Vector2(45f, 0f);
            var total = BlowerMath.Torque(new Vector2(-0.85f, 0.5f), forward) + BlowerMath.Torque(new Vector2(-0.85f, -0.5f), forward);
            Assert.That(total, Is.EqualTo(0f).Within(1e-4f));
        }
    }

    public class AirTankTests
    {
        BlowerDef blower;
        AirTank tank;

        [SetUp]
        public void SetUp()
        {
            blower = ScriptableObject.CreateInstance<BlowerDef>();
            blower.airCapacity = 100f;
            blower.drainPerSec = 20f;
            blower.rechargePerSec = 10f;
            blower.rechargeDelayMs = 500f;
            blower.burstImpulse = 60f;
            blower.burstCost = 25f;
            tank = new AirTank();
            tank.Reset(blower);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(blower);

        [Test]
        public void Drain_IsProportionalToThrottle()
        {
            tank.Tick(0.5f, 1f);
            Assert.That(tank.Air, Is.EqualTo(90f).Within(1e-3f));
        }

        [Test]
        public void EmptyTank_GivesNoThrottle()
        {
            tank.Tick(1f, 5f);
            Assert.That(tank.IsEmpty, Is.True);
            Assert.That(tank.Tick(1f, 0.1f), Is.EqualTo(0f));
        }

        [Test]
        public void HoldingTheTrigger_BlocksRechargeEvenWhenEmpty()
        {
            tank.Tick(1f, 5f);
            for (var i = 0; i < 120; i++) tank.Tick(1f, 1f / 60f);
            Assert.That(tank.Air, Is.EqualTo(0f), "you have to let go to recharge");
        }

        [Test]
        public void Recharge_StartsOnlyAfterTheDelay()
        {
            tank.Tick(1f, 1f); // 80 left
            tank.Tick(0f, 0.4f);
            Assert.That(tank.Air, Is.EqualTo(80f).Within(1e-3f), "still inside the delay");
            tank.Tick(0f, 0.2f);
            tank.Tick(0f, 1f);
            Assert.That(tank.Air, Is.GreaterThan(80f));
        }

        [Test]
        public void Burst_SpendsItsCostAndFailsWhenShort()
        {
            for (var i = 0; i < 4; i++) Assert.That(tank.TryBurst(), Is.True);
            Assert.That(tank.Air, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(tank.TryBurst(), Is.False);
        }

        [Test]
        public void ZeroDrain_MeansInfiniteAir()
        {
            blower.drainPerSec = 0f;
            tank.Reset(blower);
            for (var i = 0; i < 600; i++) Assert.That(tank.Tick(1f, 1f / 60f), Is.EqualTo(1f));
            Assert.That(tank.Fraction, Is.EqualTo(1f));
        }
    }

    public class TiltModelTests
    {
        const float Dt = 1f / 60f;

        static TiltModel Run(Vector2 forcing, Vector2 stiffness, Vector2 damping, float seconds, float capsizeAngle = 10f, float hold = 0.4f)
        {
            var tilt = new TiltModel();
            for (var t = 0f; t < seconds; t += Dt) tilt.Step(forcing, stiffness, damping, capsizeAngle, hold, Dt);
            return tilt;
        }

        [Test]
        public void Settles_BackToLevel()
        {
            var tilt = new TiltModel();
            tilt.Kick(new Vector2(1f, 0.5f));
            for (var i = 0; i < 600; i++) tilt.Step(Vector2.zero, Vector2.one * 6f, Vector2.one * 1.7f, 10f, 0.4f, Dt);
            Assert.That(tilt.Tilt.magnitude, Is.LessThan(0.01f));
        }

        [Test]
        public void SteadyForcing_SettlesAtForcingOverStiffness()
        {
            var tilt = Run(new Vector2(1.2f, 0f), stiffness: Vector2.one * 6f, damping: Vector2.one * 3f, seconds: 10f);
            Assert.That(tilt.Tilt.x, Is.EqualTo(0.2f).Within(0.01f));
        }

        [Test]
        public void MoreDamping_MeansLessOvershoot()
        {
            float Peak(float damping)
            {
                var tilt = new TiltModel();
                var peak = 0f;
                for (var i = 0; i < 300; i++)
                {
                    tilt.Step(new Vector2(1.2f, 0f), Vector2.one * 6f, Vector2.one * damping, 10f, 0.4f, Dt);
                    peak = Mathf.Max(peak, tilt.Tilt.x);
                }
                return peak;
            }

            Assert.That(Peak(5f), Is.LessThan(Peak(1f)));
        }

        [Test]
        public void StifferPitch_LeansLessForTheSameForcing()
        {
            var tilt = Run(new Vector2(1.2f, 1.2f), stiffness: new Vector2(6f, 18f), damping: new Vector2(3f, 3f * Mathf.Sqrt(3f)), seconds: 10f);

            Assert.That(tilt.Tilt.x, Is.EqualTo(0.2f).Within(0.01f), "roll settles at forcing / roll stiffness");
            Assert.That(tilt.Tilt.y, Is.EqualTo(0.2f / 3f).Within(0.01f), "pitch, three times stiffer, leans a third as far");
        }

        [Test]
        public void SqrtScaledPitchDamping_KeepsTheSameOvershootAsRoll()
        {
            // Damping ratio = c / (2 * sqrt(k)), so scaling c by sqrt(k ratio) keeps the response shape identical.
            float Overshoot(float stiffness, float damping)
            {
                var tilt = new TiltModel();
                var peak = 0f;
                for (var i = 0; i < 600; i++)
                {
                    tilt.Step(new Vector2(1.2f, 0f), new Vector2(stiffness, stiffness), new Vector2(damping, damping), 10f, 0.4f, Dt);
                    peak = Mathf.Max(peak, tilt.Tilt.x);
                }
                return peak / (1.2f / stiffness) - 1f;
            }

            Assert.That(Overshoot(18f, 1.7f * Mathf.Sqrt(3f)), Is.EqualTo(Overshoot(6f, 1.7f)).Within(0.03f));
        }

        [Test]
        public void Capsize_RequiresHoldingPastTheThresholdForTheHoldTime()
        {
            var forcing = new Vector2(30f, 0f); // settles at 5 rad, far past a 0.5 rad threshold
            var tilt = new TiltModel();
            var secondsOver = 0f;
            var capsizedAt = -1f;
            for (var i = 0; i < 300 && capsizedAt < 0f; i++)
            {
                tilt.Step(forcing, Vector2.one * 6f, Vector2.one * 1.7f, 0.5f, 0.4f, Dt);
                if (tilt.Tilt.magnitude > 0.5f) secondsOver += Dt;
                if (tilt.Capsized) capsizedAt = secondsOver;
            }

            Assert.That(tilt.Capsized, Is.True);
            Assert.That(capsizedAt, Is.EqualTo(0.4f).Within(Dt * 1.5f));
        }
    }

    public class HullInertiaTests
    {
        [Test]
        public void Rectangle_MatchesTheTextbookFormula()
        {
            var halfW = 1.5f;
            var halfH = 1f;
            var rectangle = new[] { new Vector2(-halfW, -halfH), new Vector2(halfW, -halfH), new Vector2(halfW, halfH), new Vector2(-halfW, halfH) };
            var expected = (9f + 4f) / 12f; // (w^2 + h^2) / 12 for w = 3, h = 2

            Assert.That(BoatPhysics.PolygonInertiaPerMass(rectangle), Is.EqualTo(expected).Within(1e-4f));
        }

        [Test]
        public void RoundRaft_IsCloseToASolidDisc()
        {
            var config = ScriptableObject.CreateInstance<SimConfig>();
            var disc = config.hullRadius * config.hullRadius / 2f; // I / m for a solid disc

            Assert.That(BoatPhysics.PolygonInertiaPerMass(config.HullOutline()), Is.EqualTo(disc).Within(disc * 0.03f));
            Object.DestroyImmediate(config);
        }
    }
}

namespace RushAhead.Tests
{
    public class CrewSectorTests
    {
        [Test]
        public void Slices_TileTheWholeCircle_ForEveryCrewSize([Values(1, 2, 3, 4)] int crew)
        {
            // Sample between whole degrees so no sample sits exactly on a shared edge.
            for (var degree = -180f + 0.5f; degree < 180f; degree += 1f)
            {
                var owners = 0;
                for (var seat = 0; seat < crew; seat++)
                    if (Mathf.Approximately(CrewSectors.Clamp(degree, seat, crew, out var allowed), 1f) && Mathf.Approximately(Mathf.DeltaAngle(allowed, degree), 0f))
                        owners++;

                Assert.That(owners, Is.EqualTo(1), $"{crew} players: {degree} degrees must belong to exactly one player");
            }
        }

        [Test]
        public void EachPlayer_GetsAnEqualShare([Values(1, 2, 3, 4)] int crew)
        {
            Assert.That(2f * CrewSectors.HalfWidthDeg(crew), Is.EqualTo(360f / crew).Within(1e-4f));
        }

        [Test]
        public void InsideTheSlice_IsUntouched()
        {
            var share = CrewSectors.Clamp(30f, seat: 0, crewCount: 4, out var allowed); // front-left owns 0..90
            Assert.That(share, Is.EqualTo(1f));
            Assert.That(allowed, Is.EqualTo(30f).Within(1e-4f));
        }

        [Test]
        public void OutsideTheSlice_TurnsToTheNearestEdgeAndKeepsOnlyTheUsefulPush()
        {
            var share = CrewSectors.Clamp(120f, seat: 0, crewCount: 4, out var allowed);
            Assert.That(allowed, Is.EqualTo(90f).Within(1e-4f));
            Assert.That(share, Is.EqualTo(Mathf.Cos(30f * Mathf.Deg2Rad)).Within(1e-4f));

            share = CrewSectors.Clamp(-60f, seat: 0, crewCount: 4, out allowed);
            Assert.That(allowed, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(share, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void TheFarSide_GivesNothing()
        {
            // Front-left asking to blow straight back (180) is 90 degrees past its nearest edge.
            Assert.That(CrewSectors.Clamp(180f, seat: 0, crewCount: 4, out _), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void ASoloPlayer_OwnsTheWholeCircle()
        {
            for (var degree = -179f; degree < 180f; degree += 7f)
                Assert.That(CrewSectors.Clamp(degree, 0, 1, out _), Is.EqualTo(1f));
        }

        [Test]
        public void EachAnimal_SitsInTheMiddleOfItsSlice([Values(1, 2, 3, 4)] int crew)
        {
            var config = ScriptableObject.CreateInstance<SimConfig>();
            for (var seat = 0; seat < crew; seat++)
            {
                var offset = config.SeatOffset(seat, crew);
                var angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                Assert.That(Mathf.DeltaAngle(angle, CrewSectors.CentreDeg(seat, crew)), Is.EqualTo(0f).Within(1e-3f));
                Assert.That(offset.magnitude, Is.EqualTo(config.seatRadius).Within(1e-4f));
            }

            Object.DestroyImmediate(config);
        }
    }
}
