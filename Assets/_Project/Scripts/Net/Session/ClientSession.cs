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
        readonly byte[] burstCounts = new byte[CrewSectors.MaxCrew];
        Loadout profile;
        ushort sequence;
        float sendTimer;

        /// <param name="profile">This player's animal; the host may clamp it to what it has.</param>
        /// <param name="readLocalInput">This device's controls for a seat. Called once per send, so it may consume one-shot presses.</param>
        public ClientSession(INetTransport transport, SimConfig config, CrewCatalog catalog, Loadout profile, Func<int, CrewInput> readLocalInput)
        {
            this.transport = transport;
            this.readLocalInput = readLocalInput;
            this.profile = profile;
            Timeline = new SnapshotTimeline(config.tickRate, snapshotEveryTicks: 2);
            Boat = new InterpolatedBoat(config, catalog.blowers);
            transport.PeerConnected += OnConnected;
            transport.PeerDisconnected += OnDisconnected;
            transport.Received += OnReceived;
        }

        /// <summary>A new roster arrived: seats, looks or both changed.</summary>
        public event Action RosterChanged;

        public event Action Disconnected;

        public SnapshotTimeline Timeline { get; }
        public InterpolatedBoat Boat { get; }
        public bool Connected { get; private set; }
        public RosterSeat[] Roster { get; private set; } = Array.Empty<RosterSeat>();
        public int CrewCount => Roster.Length;

        /// <summary>The seats this device drives: one, or none while the raft is full and it's watching.</summary>
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

        void OnConnected(ulong host)
        {
            Connected = true;
            SendProfile();
        }

        void SendProfile()
        {
            writer.Reset();
            Protocol.WriteProfile(writer, profile);
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
                        Roster = Protocol.ReadRoster(new ByteReader(message));
                        var seats = new System.Collections.Generic.List<int>();
                        for (var seat = 0; seat < Roster.Length; seat++)
                            if (Roster[seat].Yours) seats.Add(seat);
                        // Burst counts keep running across seat changes: the host baselines each seat on its first packet.
                        Seats = seats.ToArray();
                        RosterChanged?.Invoke();
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
