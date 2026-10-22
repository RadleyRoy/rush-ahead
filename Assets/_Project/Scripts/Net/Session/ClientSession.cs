using System;
using System.Collections.Generic;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Net
{
    /// <summary>
    /// A joining player. Sends the controls for the seats the host gave it at 30 Hz, and draws the host's raft from
    /// snapshots. Runs no simulation of its own: everything it shows came from the host.
    /// </summary>
    public sealed class ClientSession : IDisposable
    {
        readonly INetTransport transport;
        readonly Func<int, CrewInput> readLocalInput;
        readonly ByteWriter writer = new(32);
        readonly byte[] burstCounts = new byte[CrewSectors.MaxCrew];
        ushort sequence;
        float sendTimer;

        /// <param name="readLocalInput">This device's controls for a seat. Called once per send, so it may consume one-shot presses.</param>
        public ClientSession(INetTransport transport, SimConfig config, IReadOnlyList<BlowerDef> blowers, Func<int, CrewInput> readLocalInput)
        {
            this.transport = transport;
            this.readLocalInput = readLocalInput;
            Timeline = new SnapshotTimeline(config.tickRate, snapshotEveryTicks: 2);
            Boat = new InterpolatedBoat(config, blowers);
            transport.PeerConnected += OnConnected;
            transport.PeerDisconnected += OnDisconnected;
            transport.Received += OnReceived;
        }

        public event Action SeatsAssigned;
        public event Action Disconnected;

        public SnapshotTimeline Timeline { get; }
        public InterpolatedBoat Boat { get; }
        public bool Connected { get; private set; }
        public int CrewCount { get; private set; }
        public int[] Seats { get; private set; } = Array.Empty<int>();
        public int SnapshotsReceived { get; private set; }
        public float SendInterval { get; set; } = 1f / 30f;

        public void Update(float deltaSeconds)
        {
            Timeline.Advance(deltaSeconds);
            Boat.Update(Timeline);

            sendTimer += deltaSeconds;
            if (sendTimer < SendInterval) return;
            sendTimer = sendTimer > 3f * SendInterval ? 0f : sendTimer - SendInterval; // after a stall, don't fire a backlog
            if (!Connected) return;

            foreach (var seat in Seats)
            {
                var input = readLocalInput(seat);
                if (input.Burst) burstCounts[seat]++;

                writer.Reset();
                Protocol.Write(writer, new InputMessage
                {
                    Seat = (byte)seat,
                    Sequence = sequence++,
                    AimAngle = input.AimAngle,
                    HasAim = input.HasAim,
                    Throttle = input.Throttle,
                    Brace = input.Brace,
                    BurstCount = burstCounts[seat],
                });
                transport.Send(Peers.Host, writer.Written, reliable: false);
            }
        }

        void OnConnected(ulong host) => Connected = true;

        void OnDisconnected(ulong host)
        {
            Connected = false;
            Disconnected?.Invoke();
        }

        void OnReceived(ulong from, ArraySegment<byte> message)
        {
            try
            {
                switch (Protocol.PeekType(message))
                {
                    case MessageType.Seats:
                        var seats = Protocol.ReadSeats(new ByteReader(message));
                        CrewCount = seats.CrewCount;
                        Seats = Array.ConvertAll(seats.Seats, s => (int)s);
                        // Burst counts keep running across re-deals: the host baselines each seat on its first packet.
                        SeatsAssigned?.Invoke();
                        break;

                    case MessageType.Snapshot:
                        var snapshot = Timeline.Rent();
                        Protocol.ReadSnapshot(new ByteReader(message), snapshot);
                        Timeline.Add(snapshot);
                        SnapshotsReceived++;
                        break;
                }
            }
            catch (ProtocolException e)
            {
                Debug.LogWarning($"[RushAhead] Ignored a bad message from the host: {e.Message}");
            }
        }

        public void Dispose()
        {
            transport.PeerConnected -= OnConnected;
            transport.PeerDisconnected -= OnDisconnected;
            transport.Received -= OnReceived;
        }
    }
}
