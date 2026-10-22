using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RushAhead.AI;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Tests
{
    /// <summary>
    /// AI crewmates in real physics: a raft with no humans at all finishes the course at every skill (the zero-human
    /// smoke test), and each order does what it says, in open water with a player alongside.
    /// </summary>
    public class AiCrewTests
    {
        const float Dt = 1f / 60f;

        SimulationMode2D previousMode;
        SimConfig config;
        BlowerDef blower;
        SimWorld world;
        readonly List<Object> created = new();

        sealed class Player : ICrewInputSource
        {
            public CrewInput Input;
            public CrewInput ReadInput() => Input;
        }

        [SetUp]
        public void SetUp()
        {
            previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            config = Make<SimConfig>();
            blower = Make<BlowerDef>();
            world = new SimWorld(config);
        }

        [TearDown]
        public void TearDown()
        {
            world.DestroyAll();
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
            Physics2D.simulationMode = previousMode;
        }

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            created.Add(o);
            return o;
        }

        static IEnumerable<TestCaseData> CoursesAndSkills() =>
            from course in CourseSolvabilityTests.AllCourses()
            from skill in new[] { AiSkill.Clumsy, AiSkill.Keen, AiSkill.Ace }
            select new TestCaseData(course, skill).SetName($"{course.name}: an all-{skill} crew finishes");

        // How long each skill may take, as a multiple of par. Clumsy is allowed to be bad; it mustn't be hopeless.
        static float Allowance(AiSkill skill) => skill switch { AiSkill.Clumsy => 2f, AiSkill.Keen => 1.25f, _ => 1f };

        [TestCaseSource(nameof(CoursesAndSkills))]
        public void ACrewOfAiAlone_FinishesTheCourse(CourseDef def, AiSkill skill)
        {
            var course = Course.Build(def);
            try
            {
                world.WaterVelocity = course.WaterVelocityAt;
                world.Drifting.AddRange(course.BarrelBodies);
                var boat = world.SpawnBoat(course.SpawnPosition, course.SpawnAngleRad, new[] { blower, blower, blower, blower });
                var ai = new AiCrew(boat, Keep(AiProfile.Preset(skill)), config.tickRate, seed: 1) { River = course.River };
                ai.Drive(CrewSectors.DriveOrder(4));
                var race = new Race(course, boat, world.Tick, config.tickRate);

                var limit = race.StartTick + Mathf.CeilToInt(def.parSeconds * 2.5f * config.tickRate);
                while (race.Phase != RacePhase.Finished && world.Tick < limit)
                {
                    ai.Update(Dt);
                    world.Advance(Dt);
                    race.Step(world.Tick);
                }

                var time = race.SecondsAt(world.Tick);
                Debug.Log($"[RushAhead] {def.name}: all-{skill} AI crew {(race.Phase == RacePhase.Finished ? "finished" : "did not finish")} " +
                          $"in {time:0.0} s, {def.Stars(time)} stars, {boat.CapsizeCount} capsizes");
                Assert.That(race.Phase, Is.EqualTo(RacePhase.Finished), $"stuck with {boat.GatesPassed} of {course.Gates.Count} gates after {time:0} s");
                Assert.That(time, Is.LessThanOrEqualTo(def.parSeconds * Allowance(skill)), $"{skill} took {time:0.0} s");
            }
            finally
            {
                course.Destroy();
            }
        }

        AiProfile Keep(AiProfile profile)
        {
            created.Add(profile);
            return profile;
        }

        /// <summary>A raft in open water: the player in seat 2 (rear-left), AI crewmates everywhere else.</summary>
        (Boat boat, AiCrew ai, Player player) OpenWater(AiSkill skill = AiSkill.Ace)
        {
            var boat = world.SpawnBoat(Vector2.zero, 0f, new[] { blower, blower, blower, blower });
            var player = new Player();
            boat.Crew[2].Input = player;
            var ai = new AiCrew(boat, Keep(AiProfile.Preset(skill)), config.tickRate, seed: 3);
            ai.Drive(new[] { 3, 0, 1 });
            return (boat, ai, player);
        }

        void Run(AiCrew ai, float seconds)
        {
            for (var t = 0f; t < seconds; t += Dt)
            {
                ai.Update(Dt);
                world.Advance(Dt);
            }
        }

        [Test]
        public void InOpenWater_GoHelpsThePlayerPushTheirWay()
        {
            var (boat, ai, player) = OpenWater();
            player.Input = new CrewInput { AimAngle = Mathf.PI, HasAim = true, Throttle = 1f }; // the player drives ahead (+x)
            Run(ai, 2.5f);

            Assert.That(boat.Crew[3].IsBlowing, Is.True, "rear-right, whose slice also faces astern, joins in");
            Assert.That(Vector2.Dot(boat.Crew[3].AirDirection, Vector2.left), Is.GreaterThan(0.8f), "blowing the same way as the player");
            Assert.That(boat.Body.Velocity.x, Is.GreaterThan(2f), "faster than the player alone manages in 2.5 s");

            player.Input = CrewInput.Idle;
            Run(ai, 2f);
            Assert.That(boat.Crew.Count(c => c.IsBlowing), Is.Zero, "nobody to follow: the AI rests rather than guessing");
        }

        [Test]
        public void Hold_StopsTheAiBlowing_SoTheirTanksRefill()
        {
            var (boat, ai, player) = OpenWater();
            player.Input = new CrewInput { AimAngle = Mathf.PI, HasAim = true, Throttle = 1f };
            Run(ai, 2f);
            var air = boat.Crew[3].AirFraction;

            ai.Give(CrewOrder.Hold);
            Run(ai, 2.5f);
            Assert.That(boat.Crew[3].IsBlowing, Is.False);
            Assert.That(boat.Crew[3].AirFraction, Is.GreaterThan(air), "refilling while it holds");
            Assert.That(ai.Order, Is.EqualTo(CrewOrder.Hold), "hold lasts until someone says go");
        }

        [Test]
        public void Brace_SteadiesTheRaftForAFewSeconds_ThenTheCrewCarriesOn()
        {
            var (boat, ai, _) = OpenWater();
            ai.Give(CrewOrder.Brace);
            Run(ai, 1f);
            Assert.That(new[] { 0, 1, 3 }.All(seat => boat.Crew[seat].Braced), Is.True, "every AI crewmate braces");
            Assert.That(boat.Crew[2].Braced, Is.False, "the player decides for themself");

            Run(ai, 3f);
            Assert.That(ai.Order, Is.EqualTo(CrewOrder.Go), "brace wears off");
            Run(ai, 0.5f);
            Assert.That(boat.Crew[0].Braced, Is.False);
        }

        [Test]
        public void PushHere_TakesTheRaftToThePoint_ThenTheCrewCarriesOn()
        {
            var (boat, ai, _) = OpenWater();
            var point = new Vector2(-8f, 9f); // behind and to the left: the AI must sort out which of them can push that way
            ai.Give(CrewOrder.PushHere, point);

            var closest = float.MaxValue;
            for (var t = 0f; t < 12f && ai.Order == CrewOrder.PushHere; t += Dt)
            {
                Run(ai, Dt);
                closest = Mathf.Min(closest, Vector2.Distance(boat.Body.Position, point));
            }

            Assert.That(closest, Is.LessThan(3.1f), "got there");
            Assert.That(ai.Order, Is.EqualTo(CrewOrder.Go), "and went back to helping");
        }

        [Test]
        public void WithOnlyTheFrontSeats_TheAiTurnsTheRaftRoundSoItCanPush()
        {
            var def = Make<CourseDef>();
            def.river = new[] { new RiverPoint(0f, 0f, 14f), new RiverPoint(60f, 0f, 14f), new RiverPoint(120f, 0f, 14f) };
            def.startDistance = 5f;
            def.gates = new[] { 100f, 110f };
            var course = Course.Build(def);
            try
            {
                // Players hold both rear seats (the ones facing astern) and do nothing; the AI has only the front two.
                var boat = world.SpawnBoat(course.SpawnPosition, course.SpawnAngleRad, new[] { blower, blower, blower, blower });
                var ai = new AiCrew(boat, Keep(AiProfile.Preset(AiSkill.Ace)), config.tickRate, seed: 5) { River = course.River };
                ai.Drive(new[] { 0, 1 });
                var turnedRound = false;
                for (var t = 0f; t < 25f; t += Dt)
                {
                    Run(ai, Dt);
                    turnedRound |= Mathf.Abs(Mathf.DeltaAngle(boat.Body.AngleRad * Mathf.Rad2Deg, 0f)) > 150f;
                }

                Assert.That(turnedRound, Is.True, "it spun the raft so its blowers faced astern");
                Assert.That(boat.Body.Position.x, Is.GreaterThan(15f), "and then made real progress, rather than sitting stuck");
            }
            finally
            {
                course.Destroy();
            }
        }

        [Test]
        public void AClumsyCrewmate_ReactsLaterThanAnAce()
        {
            float ReactionTo(AiSkill skill)
            {
                world.DestroyAll();
                var (boat, ai, player) = OpenWater(skill);
                player.Input = new CrewInput { AimAngle = Mathf.PI, HasAim = true, Throttle = 1f };
                for (var t = 0f; t < 5f; t += Dt)
                {
                    Run(ai, Dt);
                    if (boat.Crew[3].Spool > 0.05f) return t;
                }

                return float.MaxValue;
            }

            var ace = ReactionTo(AiSkill.Ace);
            var clumsy = ReactionTo(AiSkill.Clumsy);
            Assert.That(clumsy, Is.GreaterThan(ace + 0.3f), $"clumsy {clumsy:0.00} s vs ace {ace:0.00} s");
        }
    }
}
