using System;

namespace RushAhead.Net
{
    /// <summary>
    /// Moves opaque messages between the host and its clients. Knows nothing about the game, so the same sessions run
    /// over the in-process loopback (tests), NGO on a LAN, and later the online relay.
    /// </summary>
    public interface INetTransport
    {
        bool IsHost { get; }

        /// <summary>On the host: a client joined. On a client: the connection to the host opened (peer = <see cref="Peers.Host"/>).</summary>
        event Action<ulong> PeerConnected;

        event Action<ulong> PeerDisconnected;

        /// <summary>The segment is only valid during the callback; copy it to keep it.</summary>
        event Action<ulong, ArraySegment<byte>> Received;

        /// <summary>Unreliable messages may be dropped or arrive out of order; reliable ones arrive once, in order.</summary>
        void Send(ulong peer, ArraySegment<byte> message, bool reliable);

        void Shutdown();
    }

    public static class Peers
    {
        public const ulong Host = 0;
    }
}
