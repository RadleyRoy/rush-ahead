using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RushAhead.Content;
using RushAhead.Net;
using RushAhead.Sim;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RushAhead.Tests
{
    /// <summary>
    /// A whole networked match with no devices: a host and three clients over an in-process network with real-world
    /// latency, jitter and loss. Checks that client controls drive the host's raft, that every client's view tracks the
    /// host's actual path closely, that one-shot actions survive packet loss exactly once, that everyone's chosen
    /// animal reaches every roster, and that seats move only when someone leaves or asks for a free one.
    /// </summary>
    public class LoopbackSessionTests
    {
        const float Dt = 1f / 60f;

        SimulationMode2D previousMode;
        SimConfig config;
        CrewCatalog catalog;
        SimWorld world;
        GameObject barrel;
        readonly List<Object> created = new();

        [SetUp]
        public void SetUp()
        {
            previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            config = Make<SimConfig>();
            catalog = Make<CrewCatalog>();
            catalog.blowers = new[] { Make<BlowerDef>(), Make<BlowerDef>() };
            catalog.species = Enumerable.Range(0, 6).Select(_ => Make<SpeciesDef>()).ToArray();
            catalog.furColors = new Color[12];
            world = new SimWorld(config);
        }

        [TearDown]
        public void TearDown()
        {
            world.DestroyAll();
            if (barrel != null) Object.DestroyImmediate(barrel);
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

        [Test]
        public void FourPlayers_OverALossyNetwork_StayInSync()
        {
            var network = new LoopbackNetwork(seed: 1234, latencySeconds: 0.12f, jitterSeconds: 0.04f, lossRate: 0.05f);
            var roster = new CrewRoster(catalog, 4);
            var boat = world.SpawnBoat(Vector2.zero, 0f, roster.Blowers());
            barrel = PhysicsBodies.CreateBarrel(new Vector2(-6f, 0f), 0.4f, 8f);

            var host = new HostSession(network.Host, roster, new[] { barrel.GetComponent<Rigidbody2D>() });
            host.UseBoat(boat);

            // Clients act by the seat they're given, because join order is whatever order the host hears the
            // handshakes in, which network jitter shuffles, just as on a real network.
            var time = 0f;
            var burstSent = false;
            var clients = new List<(LoopbackNetwork.Endpoint endpoint, ClientSession session, Loadout profile)>();
            for (var i = 0; i < 3; i++)
            {
                var endpoint = network.Connect();
                var profile = new Loadout { Species = (byte)(3 + i), Fur = (byte)(6 + i), Blower = (byte)(i == 0 ? 1 : 0), Name = $"Player {i}" };
                var session = new ClientSession(endpoint, config, catalog, profile, seat =>
                {
                    if (seat == 3 && time < 3f) // rear-right drives forward for three seconds
                        return new CrewInput { AimAngle = boat.Body.AngleRad + Mathf.PI, HasAim = true, Throttle = 1f };
                    if (seat == 0 && time >= 2f && !burstSent) // front-left taps burst once
                    {
                        burstSent = true;
                        return new CrewInput { Burst = true };
                    }
                    return CrewInput.Idle;
                });
                clients.Add((endpoint, session, profile));
            }

            var hostPath = new Dictionary<int, Vector2>();
            var worstError = 0f;
            var compared = 0;
            int[] dealt = null;
            ulong[] joinOrder = null;
            ClientSession leaver = null, mover = null;
            var refusedClaimKeptSeat = false;
            for (var frame = 0; frame < 7 * 60; frame++)
            {
                time = frame * Dt;
                if (frame == 4 * 60)
                {
                    joinOrder = host.Clients.ToArray();
                    dealt = joinOrder.Select(c => roster.SeatOf(c)).ToArray();
                    mover = clients.First(c => c.session.Seats.Contains(0)).session;
                    mover.ClaimSeat(3); // taken: the host must refuse
                }

                if (frame == 5 * 60 - 1) refusedClaimKeptSeat = mover.Seats.SequenceEqual(new[] { 0 }) && roster.OwnerOf(3) == joinOrder[0];

                if (frame == 5 * 60)
                {
                    var leaving = clients.First(c => c.session.Seats.Contains(1)); // the holder of the last seat leaves
                    leaver = leaving.session;
                    network.Disconnect(leaving.endpoint);
                }

                if (frame == 6 * 60) mover.ClaimSeat(1); // now free

                network.Advance(Dt);
                foreach (var (_, session, _) in clients) session.Update(Dt);
                world.Advance(Dt);
                host.OnTick(world.Tick);
                hostPath[world.Tick] = boat.Body.Position;

                // After a second of warm-up, every client's drawn raft must sit on the host's real path at the moment it is showing.
                if (time < 1f) continue;
                foreach (var (_, session, _) in clients)
                {
                    // A player who has left stops getting snapshots, so their frozen view is expected to drift away.
                    if (!session.Boat.HasData || session == leaver) continue;
                    var playback = session.Timeline.PlaybackTick;
                    var before = (int)Math.Floor(playback);
                    if (!hostPath.TryGetValue(before, out var a) || !hostPath.TryGetValue(before + 1, out var b)) continue;
                    var truth = Vector2.Lerp(a, b, (float)(playback - before));
                    worstError = Mathf.Max(worstError, Vector2.Distance(session.Boat.RenderPosition, truth));
                    compared++;
                }
            }

            // 1. Seats dealt in drive order, by join order: the host keeps rear-left (2); joiners get 3, 0 and 1.
            Assert.That(dealt, Is.EqualTo(new[] { 3, 0, 1 }));
            Assert.That(clients.All(c => c.session.CrewCount == 4), Is.True);

            // 2. A client's controls moved the host's raft.
            Assert.That(boat.Body.Position.magnitude, Is.GreaterThan(1f), "the first joiner's thrust never reached the host");

            // 3. Every client's view tracked the host's path.
            Assert.That(compared, Is.GreaterThan(500), "too few comparisons: clients never received snapshots");
            Assert.That(worstError, Is.LessThan(0.05f), $"a client's raft strayed {worstError:0.000} m from the host's path");
            Assert.That(network.Dropped, Is.GreaterThan(0), "the run should have actually lost some packets");

            // 4. One tap, one burst, despite loss.
            Assert.That(boat.Crew[0].BurstCount, Is.EqualTo(1));

            // 5. A claim on a taken seat changed nothing; the leaver's seat became a spare and the mover took it.
            Assert.That(refusedClaimKeptSeat, Is.True, "asking for someone else's seat must not move anyone");
            Assert.That(host.Clients.Count, Is.EqualTo(2));
            Assert.That(roster.SeatOf(joinOrder[0]), Is.EqualTo(3), "the first joiner never moved");
            Assert.That(mover.Seats, Is.EqualTo(new[] { 1 }), "the mover heard about its new seat");
            Assert.That(roster.OwnerOf(0), Is.EqualTo(CrewRoster.Nobody), "the mover's old seat is a spare");
            Assert.That(roster.LocalSeats(), Is.EqualTo(new[] { 2, 0 }), "the host drives its own seat and the spare");

            // 6. Everyone's chosen animal reached the host, the host's raft and every remaining client's roster.
            foreach (var (endpoint, session, profile) in clients.Where(c => c.session != leaver))
            {
                var seat = roster.SeatOf(endpoint.Id);
                Assert.That(roster.LoadoutAt(seat), Is.EqualTo(profile), $"client {endpoint.Id}'s profile on the host");
                Assert.That(boat.Crew[seat].Blower, Is.EqualTo(catalog.BlowerOf(profile)), "the host's raft gave them the blower they picked");
                Assert.That(session.Roster.Select(s => s.Loadout), Is.EqualTo(roster.Describe(endpoint.Id).Select(s => s.Loadout)), "rosters agree");
                Assert.That(session.Roster.Count(s => s.Yours), Is.EqualTo(1));
            }

            host.Dispose();
            foreach (var (_, session, _) in clients) session.Dispose();
        }
    }
}
