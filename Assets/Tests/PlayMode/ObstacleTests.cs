using System.Linq;
using NUnit.Framework;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Tests
{
    /// <summary>The newer obstacles on a straight, still river 14 m wide: each does what it says, on the clock alone.</summary>
    public class ObstacleTests
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

        Boat Spawn(Vector2 position, bool brace = false, bool driveDownstream = false)
        {
            var boat = world.SpawnBoat(position, 0f, new[] { blower, blower, blower, blower });
            var input = new CrewInput { Brace = brace };
            if (driveDownstream) input = new CrewInput { AimAngle = Mathf.PI, HasAim = true, Throttle = 1f };
            foreach (var crew in boat.Crew) crew.Input = new Hold { Input = input };
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

        Collider2D At(Vector2 point) => Physics2D.OverlapPoint(point);

        [Test]
        public void ARockslide_RollsABoulderAcross_AndKnocksTheRaftInItsPath()
        {
            def.rockslides = new[] { new RockslideSpot(40f, 1f, 4f, 0f, 1.2f) };
            Build();
            var boat = Spawn(new Vector2(40f, 0f));
            var shoves = 0;
            boat.Shoved += _ => shoves++;
            var stillAt = 0f;
            while (Seconds < 1.9)
            {
                Run(Dt, boat);
                stillAt = Mathf.Max(stillAt, Vector2.Distance(boat.Body.Position, new Vector2(40f, 0f)));
            }

            Assert.That(stillAt, Is.LessThan(0.05f), "nothing touches it before the boulder comes");
            Run(2.6f, boat);
            Assert.That(shoves, Is.GreaterThanOrEqualTo(1), "the boulder came through");
            Assert.That(boat.Body.Position.y, Is.LessThan(-1f), "and knocked the raft toward the far side");
        }

        [Test]
        public void AHippo_BubblesThenFlipsAnUnbracedRaft_ButABracedCrewRidesItOut()
        {
            def.hippos = new[] { new HippoSpot(40f, 0f, 4f, 0f), new HippoSpot(20f, 0f, 4f, 0f) };
            Build();
            var hippo = (Hippo)course.Hazards.Obstacles[0];
            Assert.That(hippo.Pose(4f * 0.6f).bubbling, Is.GreaterThan(0f), "bubbles before it comes up");
            Assert.That(hippo.Pose(4f * 0.85f).rise, Is.EqualTo(1f), "then it's up");

            var loose = Spawn(new Vector2(40f, 0f));
            var braced = Spawn(new Vector2(20f, 0f), brace: true);
            Run(0.1f, loose, braced);
            Run(5f, loose, braced);
            Assert.That(loose.CapsizeCount, Is.GreaterThanOrEqualTo(1), "flipped");
            Assert.That(braced.CapsizeCount, Is.Zero, "braced through it");
        }

        [Test]
        public void ASluiceGate_HasOneGap_ThatSlidesFromSideToSide()
        {
            def.sluices = new[] { new SluiceSpot(40f, 4.4f, 4f, 0f) };
            Build();
            var sluice = (Sluice)course.Hazards.Obstacles[0];
            while (Seconds < 1.0) Run(Dt);
            var gap = sluice.GapCentre(Seconds - Dt);
            Assert.That(gap, Is.EqualTo(sluice.Range).Within(0.3f), "a quarter of the way through, the gap is over on the left");
            Assert.That(At(new Vector2(40f, gap)), Is.Null, "the gap is open");
            Assert.That(At(new Vector2(40f, -gap)), Is.Not.Null, "and the other side is boarded up");
            Assert.That(At(new Vector2(40f, 0f)), Is.Not.Null);
        }

        [Test]
        public void ASweeper_SpinsOnTheClock()
        {
            def.sweepers = new[] { new SweeperSpot(40f, 0f, 12f, 90f) };
            Build();
            while (Seconds < 1.0) Run(Dt); // a quarter turn: lying straight across the river
            Assert.That(At(new Vector2(40f, 5f)), Is.Not.Null, "across the river");
            Assert.That(At(new Vector2(45f, 0f)), Is.Null, "and not along it");
        }

        [Test]
        public void AWaterCannon_ShovesTheRaftItCatches_TowardTheFarEdge()
        {
            def.cannons = new[] { new CannonSpot(40f, 1f, 4f, 0f, 0f) };
            Build();
            var boat = Spawn(new Vector2(40f, 2f));
            Run(2f, boat);
            Assert.That(boat.Body.Position.y, Is.LessThan(1f), "pushed away from the cannon");
        }

        [Test]
        public void Rapids_SpeedTheWaterUp_AndThrowTheRaftAbout()
        {
            def.rapids = new[] { new RapidsSpot(20f, 60f, 2f) };
            Build();
            Assert.That(course.WaterVelocityAt(new Vector2(40f, 0f)).x, Is.EqualTo(2f).Within(0.01f), "the surge, mid-stream");
            Assert.That(course.WaterVelocityAt(new Vector2(70f, 0f)), Is.EqualTo(Vector2.zero), "still water past them");
            var boat = Spawn(new Vector2(30f, 0f));
            var shoves = 0;
            boat.Shoved += _ => shoves++;
            Run(3f, boat);
            Assert.That(shoves, Is.GreaterThanOrEqualTo(3), "thrown about every half second or so");
        }

        [Test]
        public void AWeir_PullsTheWaterOverIt_AndKicksTheRaftGoingOver()
        {
            def.weirs = new[] { new WeirSpot(40f) };
            Build();
            Assert.That(course.WaterVelocityAt(new Vector2(39f, 0f)).x, Is.GreaterThan(1.5f), "pouring over");
            Assert.That(course.WaterVelocityAt(new Vector2(20f, 0f)), Is.EqualTo(Vector2.zero), "still, well upstream");
            var boat = Spawn(new Vector2(33f, 0f), driveDownstream: true);
            var shoves = 0;
            boat.Shoved += _ => shoves++;
            Run(5f, boat);
            Assert.That(shoves, Is.EqualTo(1), "one landing");
        }

        [Test]
        public void ABumper_BouncesARaftBack()
        {
            def.bumpers = new[] { new BumperSpot(new Vector2(40f, 0f), 1f) };
            Build();
            var boat = Spawn(new Vector2(34f, 0f), driveDownstream: true);
            var bounced = 0f;
            for (var i = 0; i < 60 * 5; i++)
            {
                Run(Dt, boat);
                bounced = Mathf.Min(bounced, boat.Body.Velocity.x);
            }

            Assert.That(bounced, Is.LessThan(-0.5f), "sent back the way it came");
        }

        [Test]
        public void AFerry_IsWhereTheClockSays()
        {
            def.ferries = new[] { new FerrySpot(40f, 8f, 0f) };
            Build();
            var ferry = (Ferry)course.Hazards.Obstacles[0];
            while (Seconds < 2.0) Run(Dt);
            var hit = At(ferry.Position(Seconds - Dt));
            Assert.That(hit, Is.Not.Null);
            Assert.That(hit.attachedRigidbody.name, Is.EqualTo("Ferry"));
            Assert.That(ferry.Across(0), Is.EqualTo(-ferry.Range).Within(0.01f), "starts on the right");
            Assert.That(ferry.Across(8f * 0.45f), Is.EqualTo(ferry.Range).Within(0.01f), "waits on the left");
        }

        [Test]
        public void ABear_FlingsARaftOnItsSideAcrossTheRiver()
        {
            def.bears = new[] { new BearSpot(40f, 1f, 4f, 0f) };
            Build();
            var boat = Spawn(new Vector2(40f, 4.5f));
            var far = Spawn(new Vector2(20f, -4.5f));
            var shoves = 0;
            boat.Shoved += _ => shoves++;
            Run(4f, boat, far);
            Assert.That(shoves, Is.EqualTo(1), "one swipe");
            Assert.That(boat.Body.Position.y, Is.LessThan(2f), "flung away from the bear");
            Assert.That(far.Body.Position.y, Is.EqualTo(-4.5f).Within(0.2f), "out of reach, nothing happens");
        }

        [Test]
        public void ABoostLane_SpeedsUpARaftRidingIt()
        {
            def.boosts = new[] { new BoostSpot(30f, 0f) };
            Build();
            var riding = Spawn(new Vector2(31f, 0f));
            var beside = Spawn(new Vector2(31f, 5f));
            Run(0.5f, riding, beside);
            Assert.That(riding.Body.Velocity.x, Is.GreaterThan(2f), "surged down the river");
            Assert.That(beside.Body.Velocity.magnitude, Is.LessThan(0.2f), "off the lane, nothing");
        }

        [Test]
        public void AFallingTree_FlipsAnUnbracedRaftUnderIt_ThenLiesAcrossWithAGapPastItsCrown()
        {
            def.trees = new[] { new SideSpot(40f, 1f, 8f, 0f), new SideSpot(30f, 1f, 8f, 0f), new SideSpot(50f, 1f, 8f, 0f) };
            Build();
            var loose = Spawn(new Vector2(40f, 3f));
            var braced = Spawn(new Vector2(30f, 3f), brace: true);
            var tree = (FallingTree)course.Hazards.Obstacles[2];
            Run(20f, loose, braced);
            Assert.That(tree.Pose(Seconds).tilt, Is.Zero, "no race on: it stands");
            Assert.That(loose.CapsizeCount, Is.Zero, "and nothing happens");

            course.Hazards.RaceStart = Seconds + 1.0;
            var lands = ((FallingTree)course.Hazards.Obstacles[0]).LandsAt;
            Assert.That(lands, Is.EqualTo(tree.LandsAt), "trees this near the start all fall 2.5 s after it");
            while (Seconds < lands - 0.2) Run(Dt, loose, braced);
            Assert.That(loose.CapsizeCount, Is.Zero, "nothing happens while it creaks");
            Run(0.6f, loose, braced);
            Assert.That(loose.CapsizeCount, Is.GreaterThanOrEqualTo(1), "it came down on the raft");
            Assert.That(braced.CapsizeCount, Is.Zero, "braced, the crew rode it out");

            var trunk = At(new Vector2(50f, 3f));
            Assert.That(trunk, Is.Not.Null, "lying across the river");
            Assert.That(trunk.isTrigger, Is.False, "solid where no raft is under it");
            Assert.That(At(new Vector2(50f, tree.GapCentre)), Is.Null, "with open water past its crown");
            Assert.That(tree.GapCentre, Is.LessThan(-3f), "on the far side from its foot");
            Run(30f, loose, braced);
            Assert.That(tree.Pose(Seconds).tilt, Is.EqualTo(1f), "it stays down for the rest of the race");
            Assert.That(At(new Vector2(50f, 3f)), Is.Not.Null, "still across the river");
        }

        [Test]
        public void ATurtle_IsSolid_AndSnapsAtARaftByItsHead()
        {
            def.turtles = new[] { new CrossingSpot(40f, 8f, 0f) };
            Build();
            var turtle = (Turtle)course.Hazards.Obstacles[0];
            var boat = Spawn(new Vector2(40f, 4.5f));
            var shoves = 0;
            boat.Shoved += _ => shoves++;
            Run(1f, boat);
            var hit = At(turtle.Position(Seconds - Dt));
            Assert.That(hit, Is.Not.Null);
            Assert.That(hit.attachedRigidbody.name, Is.EqualTo("Turtle"));
            Assert.That(hit.isTrigger, Is.False, "a raft bounces off its shell");
            Assert.That(shoves, Is.GreaterThanOrEqualTo(1), "it snapped");
            Assert.That(Vector2.Distance(boat.Body.Position, new Vector2(40f, 4.5f)), Is.GreaterThan(0.5f), "and knocked the raft away");
        }

        [Test]
        public void APiranhaShoal_SlowsARaftGoingThroughIt()
        {
            def.shoals = new[] { new CrossingSpot(40f, 1000f, 0f) };
            Build();
            var through = Spawn(new Vector2(36f, 0f), driveDownstream: true);
            var clear = Spawn(new Vector2(14f, 0f), driveDownstream: true);
            Run(1.5f, through, clear);
            Assert.That(through.Body.Position.x - 36f, Is.LessThan(0.8f * (clear.Body.Position.x - 14f)), "nibbled to a crawl");
        }

        [Test]
        public void AWaterspout_WhirlsARaftRoundIt_AndFlingsOneFromItsCore()
        {
            def.waterspouts = new[] { new CrossingSpot(40f, 1000f, 0f), new CrossingSpot(20f, 1000f, 0f) };
            Build();
            var near = Spawn(new Vector2(40f, 3f));
            var core = Spawn(new Vector2(20f, 0.5f));
            var flung = 0;
            core.Shoved += _ => flung++;
            Run(1.5f, near, core);
            var round = Vector2.SignedAngle(new Vector2(0f, 3f), near.Body.Position - new Vector2(40f, 0f));
            Assert.That(Mathf.Abs(round), Is.GreaterThan(15f), "carried round it");
            Assert.That(flung, Is.GreaterThanOrEqualTo(1), "flung out of its core");
        }

        [Test]
        public void ElectricEels_StunAnUnbracedCrewInAPulse_WhoThenCantBlow()
        {
            def.eels = new[] { new AcrossSpot(40f, 4f, 0f) };
            Build();
            var loose = Spawn(new Vector2(40f, 3f));
            var braced = Spawn(new Vector2(40f, -3f), brace: true);
            while (Seconds < 4f * 0.82f + 0.1f) Run(Dt, loose, braced);
            Assert.That(loose.StunnedSeconds, Is.GreaterThan(0f), "zapped");
            Assert.That(braced.StunnedSeconds, Is.Zero, "holding on, the crew shrugged it off");

            var driver = Spawn(new Vector2(10f, 0f), driveDownstream: true);
            driver.StunnedSeconds = ElectricEels.StunSeconds;
            Run(0.5f, driver);
            foreach (var crew in driver.Crew) Assert.That(crew.IsBlowing, Is.False, "a stunned crew can't blow");
            Run(1.2f, driver);
            var blowing = 0;
            foreach (var crew in driver.Crew) if (crew.IsBlowing) blowing++;
            Assert.That(blowing, Is.GreaterThan(0), "until it wears off");
        }

        [Test]
        public void AMudslide_ShovesARaftOnItsSideAcross_AndLeavesTheFarSideAlone()
        {
            def.mudslides = new[] { new SideSpot(40f, 1f, 6f, 0f) };
            Build();
            var near = Spawn(new Vector2(40f, 4f));
            var far = Spawn(new Vector2(40f, -5f));
            Run(2.5f, near, far);
            Assert.That(near.Body.Position.y, Is.EqualTo(4f).Within(0.1f), "nothing before it pours");
            Run(1.8f, near, far);
            Assert.That(near.Body.Position.y, Is.LessThan(3f), "shoved toward the far side");
            Assert.That(far.Body.Position.y, Is.EqualTo(-5f).Within(0.2f), "out of its reach");
        }

        [Test]
        public void IceFloes_AreSolid_AndRideDownTheRiver()
        {
            def.floes = new[] { new AcrossSpot(20f, 8f, 0f) };
            Build();
            var floes = (IceFloes)course.Hazards.Obstacles[0];
            while (Seconds < 2.0) Run(Dt);
            var (position, _, up) = floes.Floe(0, Seconds - Dt);
            Assert.That(up, Is.EqualTo(1f), "floating");
            var hit = At(position);
            Assert.That(hit, Is.Not.Null);
            Assert.That(hit.isTrigger, Is.False, "solid");
            Assert.That(floes.Floe(0, 3.0).position.x, Is.GreaterThan(floes.Floe(0, 1.0).position.x + 4f), "drifting downstream");
        }

        [Test]
        public void ABeaverDam_KeepsOneGapOpenAtATime()
        {
            def.dams = new[] { new AcrossSpot(40f, 6f, 0f) };
            Build();
            var dam = (BeaverDam)course.Hazards.Obstacles[0];
            Assert.That(dam.Gaps.Count, Is.EqualTo(2));
            bool Plugged(int gap) => At(new Vector2(40f, dam.Gaps[gap])) is { isTrigger: false };
            Run(1f);
            Assert.That(dam.OpenGap(Seconds, out _), Is.EqualTo(0));
            Assert.That(Plugged(0), Is.False, "the open gap lets a raft through");
            Assert.That(Plugged(1), Is.True, "the other is plugged");
            Assert.That(At(new Vector2(40f, 0f)), Is.Not.Null, "the dam itself between them");
            Run(3f);
            Assert.That(Plugged(0), Is.True, "then that one shuts");
            Assert.That(Plugged(1), Is.False, "and the other opens");
        }

        [Test]
        public void APelican_DivesWhereItsShadowWas_AndKnocksTheRaftThere()
        {
            def.pelicans = new[] { new SideSpot(40f, 1f, 4f, 0f) };
            Build();
            var pelican = (PelicanDive)course.Hazards.Obstacles[0];
            var (target, shadow, _) = pelican.Pose(2.2);
            Assert.That(shadow, Is.GreaterThan(0f), "its shadow first");
            Assert.That(Mathf.Abs(target.y), Is.LessThan(7f), "over the water");
            Assert.That(pelican.Pose(6.2).target, Is.Not.EqualTo(target), "a different spot each dive");
            var boat = Spawn(target);
            var shoves = 0;
            boat.Shoved += _ => shoves++;
            Run(2.7f, boat);
            Assert.That(shoves, Is.EqualTo(1), "one dive, one knock");
        }

        [Test]
        public void ANetBoat_IsSolid_AndDragsASnaggedRaftAlong()
        {
            def.netBoats = new[] { new CrossingSpot(40f, 8f, 0f) };
            Build();
            var netBoat = (NetBoat)course.Hazards.Obstacles[0];
            var (near, far) = netBoat.Net(0);
            Assert.That(far, Is.LessThan(near), "the net streams out behind as it heads for the left bank");
            var snagged = Spawn(new Vector2(40f, (near + far) * 0.5f));
            Run(0.4f, snagged);
            Assert.That(snagged.Body.Velocity.y, Is.GreaterThan(1f), "dragged along with it");
            var hit = At(netBoat.Position(Seconds - Dt));
            Assert.That(hit, Is.Not.Null);
            Assert.That(hit.attachedRigidbody.name, Is.EqualTo("Net boat"));
        }

        // ---- The Phase 17 kinds (dealt as ObstacleSpots in a layout's extras) ----

        T Only<T>(params ObstacleSpot[] spots) where T : Obstacle
        {
            def.extras = spots;
            Build();
            return (T)course.Hazards.Obstacles[0];
        }

        static int Counting(Boat boat, out System.Func<int> shoves)
        {
            var count = 0;
            boat.Shoved += _ => count++;
            shoves = () => count;
            return 0;
        }

        [Test]
        public void AKraken_SlamsATentacleAlongTheLineItsBubblesShowed()
        {
            var kraken = Only<Kraken>(new ObstacleSpot(ObstacleKind.Kraken, 40f, period: 4f, size: 1f));
            const double slam = 4.0 * Kraken.Slam;
            var (root, tip) = kraken.Line(0, slam);
            Assert.That(kraken.Line(0, 4.0 * Kraken.Bubbles), Is.EqualTo((root, tip)), "the bubbles showed this very line");
            Assert.That(kraken.Line(0, slam + 4.0), Is.Not.EqualTo((root, tip)), "the next slam lands somewhere else");
            var under = Spawn(Vector2.Lerp(root, tip, 0.6f));
            var clear = Spawn(new Vector2(70f, 0f));
            Counting(under, out var hits);
            Counting(clear, out var missed);
            Run((float)slam + 0.4f, under, clear);
            Assert.That(hits(), Is.EqualTo(1), "slammed once");
            Assert.That(missed(), Is.Zero, "nothing away from the line");
        }

        [Test]
        public void ATidalBore_RollsUpTheRiver_FlippingAnUnbracedRaft_ButNotABracedOne()
        {
            var bore = Only<TidalBore>(new ObstacleSpot(ObstacleKind.TidalBore, 40f, period: 6f));
            Assert.That(bore.Wave(1.0).distance, Is.EqualTo(40f + TidalBore.Run).Within(0.01f), "it swells at the bottom of its stretch first");
            Assert.That(bore.Wave(2.0).distance, Is.LessThan(bore.Wave(1.6).distance), "then rolls up the river");
            var loose = Spawn(new Vector2(40f, 3.5f));
            var braced = Spawn(new Vector2(40f, -3.5f), brace: true);
            Run(4f, loose, braced);
            Assert.That(loose.CapsizeCount, Is.GreaterThanOrEqualTo(1), "flipped");
            Assert.That(braced.CapsizeCount, Is.Zero, "rode over it braced");
            Assert.That(braced.Body.Position.x, Is.LessThan(40f), "thrown back up the river");
        }

        [Test]
        public void AJumpRamp_LaunchesAFastRaft_AndOnlyABracedCrewLandsUpright()
        {
            def.flow = 9f;
            def.extras = new[] { new ObstacleSpot(ObstacleKind.JumpRamp, 40f, across: 3.5f), new ObstacleSpot(ObstacleKind.JumpRamp, 40f, across: -3.5f) };
            Build();
            var ramps = course.Hazards.Obstacles;
            var loose = Spawn(new Vector2(18f, 3.5f));
            var braced = Spawn(new Vector2(18f, -3.5f), brace: true);
            Counting(loose, out var knocks);
            var highest = 0f;
            for (var i = 0; i < 7 * 60; i++) // it lands about 5 s in; capsizing takes a moment more
            {
                Run(Dt, loose, braced);
                highest = Mathf.Max(highest, ((JumpRamp)ramps[0]).Height(loose.Body.Position, loose.Body.Velocity));
            }

            Assert.That(highest, Is.GreaterThan(JumpRamp.Lip + 0.3f), "it flew, higher than the lip");
            Assert.That(knocks(), Is.GreaterThanOrEqualTo(1), "it came down hard");
            Assert.That(loose.CapsizeCount, Is.GreaterThanOrEqualTo(1), "and over it went, unbraced");
            Assert.That(braced.CapsizeCount, Is.Zero, "braced, it lands upright");
            Assert.That(((JumpRamp)ramps[0]).Height(new Vector2(40f, 3.5f), Vector2.zero), Is.GreaterThan(0f), "up on the ramp");
            Assert.That(((JumpRamp)ramps[0]).Height(new Vector2(70f, 3.5f), new Vector2(9f, 0f)), Is.Zero, "and back on the water well past it");
        }

        [Test]
        public void AGooseGang_ChasesAndPecksARaft_AndTheGeeseAreLooseBodiesSentWithTheProps()
        {
            var gang = Only<GooseGang>(new ObstacleSpot(ObstacleKind.GooseGang, 40f, side: 1f, size: 3f));
            Assert.That(gang.Geese.Count, Is.EqualTo(3));
            Assert.That(course.BarrelBodies.Count, Is.EqualTo(3), "sent to joined players after the barrels and logs");
            for (var i = 0; i < 3; i++) Assert.That(course.IsObstacleBody(i), Is.True);
            Assert.That(course.DriftingBodies, Is.Empty, "they swim, the water doesn't carry them");
            var home = gang.Geese[0].position;
            var boat = Spawn(new Vector2(38f, -1f));
            Counting(boat, out var pecks);
            Run(4f, boat);
            Assert.That(pecks(), Is.GreaterThanOrEqualTo(1), "they caught it and pecked");
            Assert.That(Vector2.Distance(gang.Geese[0].position, home), Is.GreaterThan(1f), "off their bank after it");
            course.ResetBarrels();
            Assert.That(gang.Geese[0].position, Is.EqualTo(home), "a new race: back home");
        }

        [Test]
        public void ChainedMines_BlastARaftThatTouchesOne_AndLeaveTheWayBetweenThemClear()
        {
            var mines = Only<ChainedMines>(new ObstacleSpot(ObstacleKind.ChainedMines, 40f, across: 1f, period: 3f, size: 3f));
            Assert.That(mines.Anchor(0).y * mines.Anchor(1).y, Is.LessThan(0f), "they alternate sides: a slalom");
            var touching = Spawn(mines.Anchor(0));
            var far = Spawn(new Vector2(70f, 0f));
            Counting(touching, out var hits);
            Counting(far, out var missed);
            Run(1f, touching, far);
            Assert.That(hits(), Is.EqualTo(1), "one blast, then it needs a moment");
            Assert.That(missed(), Is.Zero);
        }

        [Test]
        public void AWreckingBall_SmashesARaftInItsLaneLowInItsSwing_ButNotOneBesideIt()
        {
            var ball = Only<WreckingBall>(new ObstacleSpot(ObstacleKind.WreckingBall, 40f, across: 0f, period: 3f));
            Assert.That(ball.Pose(0).height, Is.LessThan(WreckingBall.LowEnough), "low at the bottom of its swing");
            Assert.That(ball.Pose(0.75).height, Is.GreaterThan(3.5f), "high at the end of it");
            var under = Spawn(new Vector2(40f, 0f));
            var beside = Spawn(new Vector2(40f, 5f));
            Counting(under, out var hits);
            Counting(beside, out var missed);
            Run(0.5f, under, beside);
            Assert.That(hits(), Is.EqualTo(1));
            Assert.That(missed(), Is.Zero, "it swings in one lane");
        }

        [Test]
        public void AStormCloud_StrikesTheGlowingRing_KnockingAndStunningAnUnbracedRaft()
        {
            var storm = Only<StormCloud>(new ObstacleSpot(ObstacleKind.StormCloud, 40f, period: 2f));
            const double bolt = 2.0 * StormCloud.Bolt;
            var target = storm.Target(bolt);
            Assert.That(storm.Target(2.0 * StormCloud.Glow), Is.EqualTo(target), "the ring glows where it'll strike");
            var boat = Spawn(target);
            Counting(boat, out var hits);
            Run((float)bolt + 0.05f, boat);
            Assert.That(hits(), Is.EqualTo(1));
            Assert.That(boat.StunnedSeconds, Is.GreaterThan(0.5f), "its blowers cut out");
        }

        [Test]
        public void APaddleSteamer_IsSolid_AndItsPaddlesKnockARaftAway()
        {
            var steamer = Only<PaddleSteamer>(new ObstacleSpot(ObstacleKind.PaddleSteamer, 40f, across: 4f, period: 12f));
            Run(Dt);
            var (position, _) = steamer.Pose(Seconds);
            var hull = At(position);
            Assert.That(hull, Is.Not.Null);
            Assert.That(hull.attachedRigidbody.name, Is.EqualTo("Paddle steamer"));
            Assert.That(steamer.TopSpeed, Is.LessThan(5.5f), "never fast enough to fling a raft");
            var boat = Spawn(new Vector2(position.x, 4f - PaddleSteamer.Width * 0.5f - PaddleSteamer.Paddle - 1f));
            Counting(boat, out var hits);
            Run(1f, boat);
            Assert.That(hits(), Is.GreaterThanOrEqualTo(1), "the paddles caught it");
            Assert.That(boat.Body.Position.y, Is.LessThan(-0.2f), "and knocked it away");
        }

        [Test]
        public void AMillRace_DrawsARaftTowardItsBank()
        {
            Only<MillRace>(new ObstacleSpot(ObstacleKind.MillRace, 40f, side: 1f, period: 3f));
            var boat = Spawn(new Vector2(42f, -3f));
            Run(2f, boat);
            Assert.That(boat.Body.Position.y, Is.GreaterThan(-2.2f), "pulled toward the mill");
            var far = Spawn(new Vector2(60f, -3f));
            Run(1f, far);
            Assert.That(far.Body.Position.y, Is.EqualTo(-3f).Within(0.05f), "nothing past its stretch");
        }

        [Test]
        public void ADrawbridge_IsAWallOnlyWhileItsDown()
        {
            var bridge = Only<Drawbridge>(new ObstacleSpot(ObstacleKind.Drawbridge, 40f, period: 6f));
            Run(1f);
            Assert.That(bridge.Open(Seconds), Is.EqualTo(1f));
            Assert.That(bridge.Barriers.All(leaf => !leaf.enabled), Is.True, "up: the way is open");
            Run(2.4f);
            Assert.That(bridge.Open(Seconds), Is.EqualTo(0f));
            Assert.That(bridge.Barriers.All(leaf => leaf.enabled), Is.True, "down: a wall right across");
            Assert.That(At(new Vector2(40f, 3f)), Is.Not.Null);
            Assert.That(bridge.Warning(6.0 * 0.35), Is.True, "lights flash before it comes down");
        }

        [Test]
        public void ACargoCrane_DropsACrateWhereItsShadowShowed()
        {
            var crane = Only<CargoCrane>(new ObstacleSpot(ObstacleKind.CargoCrane, 40f, side: 1f, period: 4f));
            const double land = 4.0 * CargoCrane.Land;
            var target = crane.Target(land);
            Assert.That(crane.Shadow(4.0 * 0.6), Is.GreaterThan(0f), "its shadow first");
            Assert.That(crane.Target(4.0 * 0.6), Is.EqualTo(target));
            var boat = Spawn(target);
            Counting(boat, out var hits);
            Run((float)land + 0.1f, boat);
            Assert.That(hits(), Is.EqualTo(1));
        }

        [Test]
        public void ALogFlume_ShootsLogsIntoTheRiver_ThatKnockARaftAside()
        {
            var flume = Only<LogFlume>(new ObstacleSpot(ObstacleKind.LogFlume, 40f, side: 1f, period: 3.4f, offset: 1.54f));
            var (centre, _, _, inWater, alive) = flume.Log(0, 0.5);
            Assert.That(alive && inWater, Is.True, "a log already in the water");
            Assert.That(flume.Log(0, 0.9).centre.x, Is.GreaterThan(centre.x), "drifting down the river");
            var boat = Spawn(flume.Log(0, 0.6).centre);
            Counting(boat, out var hits);
            Run(0.8f, boat);
            Assert.That(hits(), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void ACalvingGlacier_SendsAWaveAcross_ThatFlipsAnUnbracedRaft()
        {
            Only<CalvingGlacier>(new ObstacleSpot(ObstacleKind.CalvingGlacier, 40f, side: 1f, period: 6f));
            var loose = Spawn(new Vector2(43f, 1f));
            var braced = Spawn(new Vector2(37f, 1f), brace: true);
            Run(5.5f, loose, braced);
            Assert.That(loose.CapsizeCount, Is.GreaterThanOrEqualTo(1), "flipped");
            Assert.That(braced.CapsizeCount, Is.Zero, "rode it out braced");
            Assert.That(braced.Body.Position.y, Is.LessThan(0.9f), "pushed away from the ice");
        }

        [Test]
        public void ADynamiteFloat_BlowsWhereItDrifted_WhenTheFuseRunsOut()
        {
            var dynamite = Only<DynamiteFloat>(new ObstacleSpot(ObstacleKind.DynamiteFloat, 40f, side: 1f, period: 4.5f));
            const double blows = 4.5 * DynamiteFloat.Fuse;
            var (crate, _, fuse, blast) = dynamite.Crate(blows);
            Assert.That(fuse, Is.EqualTo(1f));
            Assert.That(crate.x, Is.GreaterThan(45f), "it drifted down the river first");
            Assert.That(dynamite.Crate(blows - 0.5).blast, Is.Zero, "not before");
            var near = Spawn(crate + new Vector2(0f, -2.5f));
            var far = Spawn(crate + new Vector2(-12f, 0f));
            Counting(near, out var hits);
            Counting(far, out var missed);
            Run((float)blows + 0.1f, near, far);
            Assert.That(hits(), Is.EqualTo(1));
            Assert.That(missed(), Is.Zero);
        }

        [Test]
        public void LeapingFrogs_SplashARaftByThePadTheyLandOn_NeverTheSamePadTogether()
        {
            var frogs = Only<LeapingFrogs>(new ObstacleSpot(ObstacleKind.LeapingFrogs, 40f, period: 1.6f));
            for (var round = -8; round < 8; round++)
                Assert.That(LeapingFrogs.PadOf(0, round), Is.Not.EqualTo(LeapingFrogs.PadOf(1, round)), "two frogs, two pads");
            Assert.That(frogs.Frog(0, 1.6 * 0.8).height, Is.GreaterThan(1f), "in the air mid-hop");
            var onPad = Spawn(frogs.Pad(1));
            var between = Spawn(new Vector2(46f, 0f));
            Counting(onPad, out var hits);
            Counting(between, out var missed);
            Run(3.3f, onPad, between);
            Assert.That(hits(), Is.GreaterThanOrEqualTo(1));
            Assert.That(missed(), Is.Zero);
        }

        [Test]
        public void AGiantFan_BlowsRaftsAcrossTheRiver_OnlyWhileItsGusting()
        {
            var fan = Only<GiantFan>(new ObstacleSpot(ObstacleKind.GiantFan, 40f, side: 1f, period: 5f));
            var boat = Spawn(new Vector2(40f, 1f));
            Run(2.5f, boat);
            Assert.That(fan.Blowing(Seconds), Is.Zero);
            Assert.That(boat.Body.Position.y, Is.EqualTo(1f).Within(0.05f), "still: it's winding up");
            Counting(boat, out var lurch);
            Run(2f, boat);
            Assert.That(boat.Body.Position.y, Is.LessThan(0f), "blown toward the far side");
            Assert.That(lurch(), Is.EqualTo(1), "with one lurch as the gust hits");
        }
    }
}
