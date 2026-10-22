using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace RushAhead.Net
{
    /// <summary>A room heard on the LAN, and where to join it.</summary>
    public sealed class DiscoveredRoom
    {
        public string Address;
        public RoomAdvert Advert;
        public double LastHeard;

        public string Label => string.IsNullOrEmpty(Advert.HostName) ? $"Raft at {Address}" : $"{Advert.HostName}'s raft";
    }

    /// <summary>
    /// The joiner's side: listens on the discovery port and keeps a list of rooms heard in the last few seconds.
    /// Polled from the game loop rather than run on a thread, so the list only ever changes between frames.
    /// </summary>
    public sealed class LanDiscovery : IDisposable
    {
        const double ForgetAfterSeconds = 3.5; // three missed adverts

        readonly UdpClient socket;
        readonly List<DiscoveredRoom> rooms = new();

        /// <param name="listenAddress">Any (the default) to hear the LAN; loopback for same-device tests.</param>
        public LanDiscovery(int port = LanBeacon.DiscoveryPort, IPAddress listenAddress = null)
        {
            try
            {
                socket = new UdpClient(AddressFamily.InterNetwork);
                // Another copy of the game on this device may be listening too; let both hear the broadcasts.
                socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.Client.Bind(new IPEndPoint(listenAddress ?? IPAddress.Any, port));
            }
            catch (SocketException e)
            {
                socket?.Dispose();
                socket = null;
                Error = $"Can't look for games on this network ({e.SocketErrorCode}). Join by address instead.";
            }
        }

        /// <summary>Set when listening failed; the room list then stays empty.</summary>
        public string Error { get; }

        /// <summary>Rooms heard recently, oldest first.</summary>
        public IReadOnlyList<DiscoveredRoom> Rooms => rooms;

        public void Update(double now)
        {
            if (socket == null) return;
            try
            {
                while (socket.Available > 0)
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    var datagram = socket.Receive(ref from);
                    if (Protocol.TryReadAdvert(new ArraySegment<byte>(datagram), out var advert)) Heard(from.Address.ToString(), advert, now);
                }
            }
            catch (SocketException)
            {
                // A datagram that couldn't be read (e.g. too large); the next one will be fine.
            }

            rooms.RemoveAll(room => now - room.LastHeard > ForgetAfterSeconds);
        }

        void Heard(string address, RoomAdvert advert, double now)
        {
            // Another version's advert is only read as far as its version, with no room id to match on, so go by address.
            var room = rooms.Find(r => advert.Compatible ? r.Advert.Compatible && r.Advert.RoomId == advert.RoomId : r.Address == address && !r.Advert.Compatible);
            if (room == null) rooms.Add(room = new DiscoveredRoom());
            room.Address = address;
            room.Advert = advert;
            room.LastHeard = now;
        }

        public void Dispose() => socket?.Dispose();
    }
}
