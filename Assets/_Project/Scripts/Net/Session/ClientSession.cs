using System;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Net
{
    /// <summary>
    /// A joining player. Tells the host which animal it wants to be, sends the controls for the seat it was given at
    /// 30 Hz, and draws the host's raft from snapshots. Runs no simulation of its own: everything it shows came from the host.
    /// </summary>
    public sealed class ClientSession : IDisposable
    {
        readonly INetTransport transport;
        readonly Func<int, CrewInput> readLocalInput;
        readonly ByteWriter writer = new(64);
        readonly byte[] burstCounts = new byte[CrewRoster.MaxSeats];
        readonly OrderMessage[] orders = new OrderMessage[CrewRoster.MaxRafts];
        readonly ulong device;
        Loadout profile;
        ushort sequence;
        float sendTimer;

        /// <param name="profile">This player's animal; the host may clamp it to what it has.</param>
        /// <param name="device">This device's own random id: the host keeps a dropped player's seat for that id.</param>
        /// <param name="readLocalInput">This device's controls for a seat. Called once per send, so it may consume one-shot presses.</param>
        public ClientSession(INetTransport transport, SimConfig config, CrewCatalog catalog, Loadout profile, ulong device, Func<int, CrewInput> readLocalInput)
        {
            this.transport = transport;
            this.readLocalInput = readLocalInput;
            this.profile = profile;
            this.device = device;
            Timeline = new SnapshotTimeline(config.tickRate, snapshotEveryTicks: 2);
            for (var raft = 0; raft < CrewRoster.MaxRafts; raft++) boats.Add(new InterpolatedBoat(config, catalog.blowers, raft));
            transport.PeerConnected += OnConnected;
            transport.PeerDisconnected += OnDisconnected;
            transport.Received += OnReceived;
        }

        /// <summary>A new roster arrived: seats, looks or both changed.</summary>
        public event Action RosterChanged;

        /// <summary>The host loaded a venue or started a race.</summary>
        public event Action RaceChanged;

        /// <summary>This player's raft's AI order, as last heard from the host.</summary>
        public OrderMessage Order => orders[MyRaft];

        public event Action Disconnected;

        readonly System.Collections.Generic.List<InterpolatedBoat> boats = new();

        public SnapshotTimeline Timeline { get; }

        /// <summary>The first raft (the only one in Co-op).</summary>
        public InterpolatedBoat Boat => boats[0];

        /// <summary>Every raft the host might send; only the first <see cref="Rafts"/> have data.</summary>
        public System.Collections.Generic.IReadOnlyList<InterpolatedBoat> Boats => boats;

        /// <summary>One raft (Co-op) or two (Team Race), as the last roster said.</summary>
        public int Rafts { get; private set; } = 1;

        /// <summary>The raft this player is on (0 while watching).</summary>
        public int MyRaft => Seats.Length > 0 && CrewCount > 0 ? Seats[0] / CrewCount : 0;
        public bool Connected { get; private set; }
        public RosterSeat[] Roster { get; private set; } = Array.Empty<RosterSeat>();

        /// <summary>The host's venue and race start, as last heard.</summary>
        public RaceMessage Race { get; private set; }
        /// <summary>Animals per raft.</summary>
        public int CrewCount => Rafts > 0 ? Roster.Length / Rafts : 0;

        /// <summary>The seats this device drives, numbered across all rafts: one, or none while it's watching.</summary>
        public int[] Seats { get; private set; } = Array.Empty<int>();

        /// <summary>Set when the host can't be played with (another version); the caller should leave and show it.</summary>
        public string Error { get; private set; }

        public int SnapshotsReceived { get; private set; }
        public float SendInterval { get; set; } = 1f / 30f;

        /// <summary>Changes this player's animal; the host applies it and everyone's roster updates.</summary>
        public void SetProfile(Loadout loadout)
        {
            profile = loadout;
            if (Connected) SendProfile();
        }

        /// <summary>Asks to move to a free seat. The host refuses if someone sits there by the time it hears.</summary>
        /// <summary>Asks the host's AI crew to do something. Everyone hears back what it's doing.</summary>
        public void RequestOrder(CrewOrder order, Vector2 point)
        {
            if (!Connected) return;
            writer.Reset();
            Protocol.Write(writer, new OrderMessage { Order = order, Point = point });
            transport.Send(Peers.Host, writer.Written, reliable: true);
        }

        public void ClaimSeat(int seat)
        {
            if (!Connected) return;
            writer.Reset();
            Protocol.WriteClaimSeat(writer, seat);
            transport.Send(Peers.Host, writer.Written, reliable: true);
        }

        public void Update(float deltaSeconds)
        {
            Timeline.Advance(deltaSeconds);
            foreach (var boat in boats) boat.Update(Timeline);

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

        void OnConnected(ulong host)
        {
            Connected = true;
            SendProfile();
        }

        void SendProfile()
        {
            writer.Reset();
            Protocol.WriteProfile(writer, profile, device);
            transport.Send(Peers.Host, writer.Written, reliable: true);
        }

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
                    case MessageType.Roster:
                        Roster = Protocol.ReadRoster(new ByteReader(message), out var rafts);
                        Rafts = rafts;
                        var seats = new System.Collections.Generic.List<int>();
                        for (var seat = 0; seat < Roster.Length; seat++)
                            if (Roster[seat].Yours) seats.Add(seat);
                        // Burst counts keep running across seat changes: the host baselines each seat on its first packet.
                        Seats = seats.ToArray();
                        RosterChanged?.Invoke();
                        break;

                    case MessageType.Order:
                        var order = Protocol.ReadOrder(new ByteReader(message));
                        orders[order.Raft] = order;
                        break;

                    case MessageType.Race:
                        Race = Protocol.ReadRace(new ByteReader(message));
                        RaceChanged?.Invoke();
                        break;

                    case MessageType.Snapshot:
                        var snapshot = Timeline.Rent();
                        Protocol.ReadSnapshot(new ByteReader(message), snapshot);
                        Timeline.Add(snapshot);
                        SnapshotsReceived++;
                        break;
                }
            }
            catch (VersionMismatchException e)
            {
                Error = e.Message;
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
