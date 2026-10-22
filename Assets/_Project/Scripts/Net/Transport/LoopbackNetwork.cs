using System;
using System.Collections.Generic;

namespace RushAhead.Net
{
    /// <summary>
    /// An in-process network for tests: one host and any number of clients, with latency, jitter and loss drawn from a
    /// seeded random generator so every failure reproduces exactly. Time only moves when <see cref="Advance"/> is called.
    /// Reliable messages are never dropped and keep their order per sender and receiver; unreliable ones may be
    /// dropped and may overtake each other, as on a real network.
    /// </summary>
    public sealed class LoopbackNetwork
    {
        readonly Random random;
        readonly List<Packet> inFlight = new();
        readonly Dictionary<(ulong, ulong), double> lastReliableArrival = new();
        readonly Dictionary<ulong, Endpoint> endpoints = new();
        double now;
        long sent;
        ulong nextClientId = 1;

        public LoopbackNetwork(int seed, float latencySeconds = 0f, float jitterSeconds = 0f, float lossRate = 0f)
        {
            random = new Random(seed);
            LatencySeconds = latencySeconds;
            JitterSeconds = jitterSeconds;
            LossRate = lossRate;
            Host = endpoints[Peers.Host] = new Endpoint(this, Peers.Host, isHost: true);
        }

        public float LatencySeconds { get; set; }
        public float JitterSeconds { get; set; }
        public float LossRate { get; set; }
        public int Dropped { get; private set; }
        public Endpoint Host { get; }

        /// <summary>A new client. Both sides hear about the connection after one latency, like a handshake.</summary>
        public Endpoint Connect()
        {
            var client = endpoints[nextClientId] = new Endpoint(this, nextClientId++, isHost: false);
            Enqueue(client.Id, Peers.Host, null, reliable: true, PacketKind.Connect);
            Enqueue(Peers.Host, client.Id, null, reliable: true, PacketKind.Connect);
            return client;
        }

        public void Disconnect(Endpoint client)
        {
            Enqueue(client.Id, Peers.Host, null, reliable: true, PacketKind.Disconnect);
            Enqueue(Peers.Host, client.Id, null, reliable: true, PacketKind.Disconnect);
        }

        /// <summary>Moves time forward and delivers everything due by then, earliest first.</summary>
        public void Advance(float seconds)
        {
            now += seconds;
            while (true)
            {
                var due = -1;
                for (var i = 0; i < inFlight.Count; i++)
                    if (inFlight[i].DeliverAt <= now && (due < 0 || Earlier(inFlight[i], inFlight[due])))
                        due = i;
                if (due < 0) return;

                var packet = inFlight[due];
                inFlight.RemoveAt(due);
                if (endpoints.TryGetValue(packet.To, out var receiver)) receiver.Deliver(packet);
            }
        }

        static bool Earlier(Packet a, Packet b) => a.DeliverAt < b.DeliverAt || (a.DeliverAt == b.DeliverAt && a.Order < b.Order);

        void Enqueue(ulong from, ulong to, byte[] data, bool reliable, PacketKind kind = PacketKind.Data)
        {
            if (!reliable && random.NextDouble() < LossRate)
            {
                Dropped++;
                return;
            }

            var arrival = now + LatencySeconds + random.NextDouble() * JitterSeconds;
            if (reliable)
            {
                // Reliable traffic between one pair stays in order: it can't overtake earlier reliable messages.
                if (lastReliableArrival.TryGetValue((from, to), out var previous)) arrival = Math.Max(arrival, previous);
                lastReliableArrival[(from, to)] = arrival;
            }

            inFlight.Add(new Packet { From = from, To = to, Data = data, Kind = kind, DeliverAt = arrival, Order = sent++ });
        }

        internal enum PacketKind
        {
            Data,
            Connect,
            Disconnect,
        }

        internal struct Packet
        {
            public ulong From, To;
            public byte[] Data;
            public PacketKind Kind;
            public double DeliverAt;
            public long Order;
        }

        public sealed class Endpoint : INetTransport
        {
            readonly LoopbackNetwork network;

            internal Endpoint(LoopbackNetwork network, ulong id, bool isHost)
            {
                this.network = network;
                Id = id;
                IsHost = isHost;
            }

            public ulong Id { get; }
            public bool IsHost { get; }

            public event Action<ulong> PeerConnected;
            public event Action<ulong> PeerDisconnected;
            public event Action<ulong, ArraySegment<byte>> Received;

            public void Send(ulong peer, ArraySegment<byte> message, bool reliable)
            {
                var copy = new byte[message.Count];
                Buffer.BlockCopy(message.Array!, message.Offset, copy, 0, message.Count);
                network.Enqueue(Id, peer, copy, reliable);
            }

            public void Shutdown() => network.endpoints.Remove(Id);

            internal void Deliver(Packet packet)
            {
                switch (packet.Kind)
                {
                    case PacketKind.Connect: PeerConnected?.Invoke(packet.From); break;
                    case PacketKind.Disconnect: PeerDisconnected?.Invoke(packet.From); break;
                    default: Received?.Invoke(packet.From, new ArraySegment<byte>(packet.Data)); break;
                }
            }
        }
    }
}
