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
    /// host's actual path closely, and that one-shot actions survive packet loss exactly once.
    /// </summary>
    public class LoopbackSessionTests
    {
        const float Dt = 1f / 60f;

        SimulationMode2D previousMode;
        SimConfig config;
        BlowerDef blower;
        SimWorld world;
        GameObject barrel;

        [SetUp]
        public void SetUp()
        {
            previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            config = ScriptableObject.CreateInstance<SimConfig>();
            blower = ScriptableObject.CreateInstance<BlowerDef>();
            world = new SimWorld(config);
        }

        [TearDown]
        public void TearDown()
        {
            world.DestroyAll();
            if (barrel != null) Object.DestroyImmediate(barrel);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(blower);
            Physics2D.simulationMode = previousMode;
        }

        [Test]
        public void FourPlayers_OverALossyNetwork_StayInSync()
        {
            var network = new LoopbackNetwork(seed: 1234, latencySeconds: 0.12f, jitterSeconds: 0.04f, lossRate: 0.05f);
            var blowers = new[] { blower };
            var boat = world.SpawnBoat(Vector2.zero, 0f, new[] { blower, blower, blower, blower });
            barrel = PhysicsBodies.CreateBarrel(new Vector2(-6f, 0f), 0.4f, 8f);

            var host = new HostSession(network.Host, blowers, new[] { barrel.GetComponent<Rigidbody2D>() });
            host.UseBoat(boat);

            // Clients act by the seat they're given, because join order is whatever order the host hears the
            // handshakes in, which network jitter shuffles, just as on a real network.
            var time = 0f;
            var burstSent = false;
            var clients = new List<(LoopbackNetwork.Endpoint endpoint, ClientSession session)>();
            for (var i = 0; i < 3; i++)
            {
                var endpoint = network.Connect();
                var session = new ClientSession(endpoint, config, blowers, seat =>
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
                clients.Add((endpoint, session));
            }

            var hostPath = new Dictionary<int, Vector2>();
            var worstError = 0f;
            var compared = 0;
            int[][] dealt = null;
            ulong[] joinOrder = null;
            ClientSession leaver = null;
            for (var frame = 0; frame < 6 * 60; frame++)
            {
                time = frame * Dt;
                if (frame == 4 * 60)
                {
                    joinOrder = host.Clients.ToArray();
                    dealt = joinOrder.Select(c => host.SeatsOf(c).ToArray()).ToArray();
                }

                if (frame == 5 * 60)
                {
                    var leaving = clients.First(c => c.session.Seats.Contains(1)); // the holder of the last seat leaves
                    leaver = leaving.session;
                    network.Disconnect(leaving.endpoint);
                }

                network.Advance(Dt);
                foreach (var (_, session) in clients) session.Update(Dt);
                world.Advance(Dt);
                host.OnTick(world.Tick);
                hostPath[world.Tick] = boat.Body.Position;

                // After a second of warm-up, every client's drawn raft must sit on the host's real path at the moment it is showing.
                if (time < 1f) continue;
                foreach (var (_, session) in clients)
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
            Assert.That(dealt, Is.EqualTo(new[] { new[] { 3 }, new[] { 0 }, new[] { 1 } }));
            foreach (var (endpoint, session) in clients.Take(2))
                Assert.That(session.Seats, Is.EqualTo(dealt[Array.IndexOf(joinOrder, endpoint.Id)]), "each client heard the seat the host gave it");
            Assert.That(clients.All(c => c.session.CrewCount == 4), Is.True);

            // 2. A client's controls moved the host's raft.
            Assert.That(boat.Body.Position.magnitude, Is.GreaterThan(1f), "the first joiner's thrust never reached the host");

            // 3. Every client's view tracked the host's path.
            Assert.That(compared, Is.GreaterThan(500), "too few comparisons: clients never received snapshots");
            Assert.That(worstError, Is.LessThan(0.05f), $"a client's raft strayed {worstError:0.000} m from the host's path");
            Assert.That(network.Dropped, Is.GreaterThan(0), "the run should have actually lost some packets");

            // 4. One tap, one burst, despite loss.
            Assert.That(boat.Crew[0].BurstCount, Is.EqualTo(1));

            // 5. The leaver's seat (1) came back to the host; everyone else kept theirs.
            Assert.That(host.IsRemote(1), Is.False);
            Assert.That(host.Clients.Count, Is.EqualTo(2));
            Assert.That(host.SeatsOf(joinOrder[0]), Is.EqualTo(new[] { 3 }));
            Assert.That(host.SeatsOf(joinOrder[1]), Is.EqualTo(new[] { 0 }));
            Assert.That(host.LocalSeats(), Is.EqualTo(new[] { 2, 1 }));

            host.Dispose();
            foreach (var (_, session) in clients) session.Dispose();
        }
    }
}
