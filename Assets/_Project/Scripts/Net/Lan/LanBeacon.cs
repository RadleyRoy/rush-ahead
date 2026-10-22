using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace RushAhead.Net
{
    /// <summary>
    /// The host's side of finding each other on a LAN: broadcasts a small <see cref="RoomAdvert"/> about once a second
    /// on the discovery port. NGO has no LAN discovery, and plain UDP broadcast works between Android and iOS alike.
    /// Broadcast can be blocked (guest Wi-Fi, client isolation, some hotspots), which is why join-by-address stays.
    /// </summary>
    public sealed class LanBeacon : IDisposable
    {
        public const int DiscoveryPort = 7788;
        const float IntervalSeconds = 1f;

        readonly UdpClient socket;
        readonly ByteWriter writer = new(64);
        readonly List<IPEndPoint> targets = new();
        float untilNext;

        /// <param name="targets">Where to send. Defaults to every broadcast address this device can reach, see <see cref="BroadcastAddresses"/>.</param>
        public LanBeacon(int port = DiscoveryPort, IEnumerable<IPAddress> targets = null)
        {
            socket = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
            foreach (var address in targets ?? BroadcastAddresses()) this.targets.Add(new IPEndPoint(address, port));
        }

        /// <summary>What to tell the room: kept current by the host (players come and go).</summary>
        public RoomAdvert Advert;

        public IReadOnlyList<IPEndPoint> Targets => targets;

        public void Update(float deltaSeconds)
        {
            untilNext -= deltaSeconds;
            if (untilNext > 0f) return;
            untilNext = IntervalSeconds;

            writer.Reset();
            Protocol.Write(writer, Advert);
            var datagram = writer.ToArray();
            foreach (var target in targets)
            {
                try
                {
                    socket.Send(datagram, datagram.Length, target);
                }
                catch (SocketException)
                {
                    // An interface can vanish (Wi-Fi off, hotspot stopped); keep advertising on the others.
                }
            }
        }

        /// <summary>
        /// The limited broadcast address plus each IPv4 network's own broadcast address. The directed ones matter when
        /// the phone has several networks at once, such as its own hotspot plus mobile data, where 255.255.255.255 may
        /// leave by the wrong one. Where the OS won't say a network's mask, a private address is assumed to be a /24.
        /// </summary>
        public static List<IPAddress> BroadcastAddresses()
        {
            var addresses = new List<IPAddress> { IPAddress.Broadcast };
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        var broadcast = DirectedBroadcast(unicast.Address, MaskOf(unicast));
                        if (broadcast != null && !addresses.Contains(broadcast)) addresses.Add(broadcast);
                    }
                }
            }
            catch (Exception)
            {
                // Not every platform lets apps list network interfaces; the limited broadcast still goes out.
            }

            return addresses;
        }

        static IPAddress MaskOf(UnicastIPAddressInformation unicast)
        {
            try
            {
                var mask = unicast.IPv4Mask;
                if (mask != null && !mask.Equals(IPAddress.Any)) return mask;
            }
            catch (Exception)
            {
                // Some mobile runtimes don't implement it.
            }

            return IsPrivate(unicast.Address) ? IPAddress.Parse("255.255.255.0") : null;
        }

        /// <summary>The network's broadcast address: the host part of <paramref name="address"/> set to all ones.</summary>
        public static IPAddress DirectedBroadcast(IPAddress address, IPAddress mask)
        {
            if (mask == null) return null;
            var a = address.GetAddressBytes();
            var m = mask.GetAddressBytes();
            if (a.Length != 4 || m.Length != 4) return null;
            var b = new byte[4];
            for (var i = 0; i < 4; i++) b[i] = (byte)(a[i] | ~m[i]);
            return new IPAddress(b);
        }

        static bool IsPrivate(IPAddress address)
        {
            var b = address.GetAddressBytes();
            return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
        }

        public void Dispose() => socket.Dispose();
    }
}
