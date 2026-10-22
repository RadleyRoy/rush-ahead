using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using NUnit.Framework;
using RushAhead.Net;

namespace RushAhead.Tests
{
    /// <summary>
    /// Room discovery over real UDP sockets, kept on loopback so the test never touches the actual network (and never
    /// trips a firewall prompt). Broadcast across real Wi-Fi still needs two devices: that's Phase 4's hardware check.
    /// </summary>
    public class LanDiscoveryTests
    {
        const int TestPort = 47788; // away from the game's discovery port, so a copy of the game running nearby can't interfere

        static readonly RoomAdvert Advert = new()
        {
            RoomId = 42, Version = Protocol.Version, GamePort = 7790, Players = 1, Seats = 4, HostName = "Skipper",
        };

        [Test]
        public void ARoomIsHeard_ListedOnce_AndForgottenWhenItGoesQuiet()
        {
            using var discovery = new LanDiscovery(TestPort, IPAddress.Loopback);
            Assert.That(discovery.Error, Is.Null);
            using var beacon = new LanBeacon(TestPort, new[] { IPAddress.Loopback }) { Advert = Advert };

            beacon.Update(0f); // the first advert goes straight away
            Assert.That(WaitUntil(() =>
            {
                discovery.Update(0.0);
                return discovery.Rooms.Count > 0;
            }), Is.True, "the advert never arrived");

            var room = discovery.Rooms[0];
            Assert.That(room.Address, Is.EqualTo("127.0.0.1"));
            Assert.That(room.Advert.GamePort, Is.EqualTo(7790), "joiners connect to the port the host advertises");
            Assert.That(room.Label, Is.EqualTo("Skipper's raft"));

            beacon.Update(0.5f); // too soon: nothing sent
            beacon.Update(0.6f); // a second advert, a second after the first
            WaitUntil(() =>
            {
                discovery.Update(1.0);
                return discovery.Rooms[0].LastHeard >= 1.0;
            });
            Assert.That(discovery.Rooms.Count, Is.EqualTo(1), "the same room heard twice is listed once");

            discovery.Update(10.0); // nothing for nine seconds
            Assert.That(discovery.Rooms, Is.Empty);
        }

        [Test]
        public void BroadcastAddresses_IncludeEachNetworksOwn()
        {
            Assert.That(LanBeacon.BroadcastAddresses(), Does.Contain(IPAddress.Broadcast));
            Assert.That(LanBeacon.DirectedBroadcast(IPAddress.Parse("192.168.43.1"), IPAddress.Parse("255.255.255.0")),
                Is.EqualTo(IPAddress.Parse("192.168.43.255")), "an Android hotspot's network");
            Assert.That(LanBeacon.DirectedBroadcast(IPAddress.Parse("10.1.2.3"), IPAddress.Parse("255.0.0.0")), Is.EqualTo(IPAddress.Parse("10.255.255.255")));
            Assert.That(LanBeacon.DirectedBroadcast(IPAddress.Parse("10.1.2.3"), null), Is.Null);
        }

        static bool WaitUntil(Func<bool> condition)
        {
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 2000)
            {
                if (condition()) return true;
                Thread.Sleep(10);
            }

            return false;
        }
    }
}
