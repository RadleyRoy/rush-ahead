using System;
using System.Collections.Generic;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Net
{
    /// <summary>
    /// The authoritative side. Runs the only simulation, seats joiners through the <see cref="CrewRoster"/>, takes each
    /// client's inputs for the seat they own, keeps every client's roster current, and broadcasts snapshots. The host's
    /// own controls drive its player's seat and every spare.
    /// </summary>
    public sealed class HostSession : IDisposable
    {
        readonly INetTransport transport;
        readonly IReadOnlyList<Rigidbody2D> props;
        readonly List<ulong> clients = new();
        readonly Dictionary<(ulong client, int seat), RemoteSeatInput> remote = new();
        readonly ByteWriter writer = new(256);
        readonly Snapshot snapshot = new();

        public HostSession(INetTransport transport, CrewRoster roster, IReadOnlyList<Rigidbody2D> props)
        {
            this.transport = transport;
            this.props = props;
            Roster = roster;
            transport.PeerConnected += OnClientJoined;
            transport.PeerDisconnected += OnClientLeft;
            transport.Received += OnReceived;
            roster.Changed += OnRosterChanged;
        }

        public CrewRoster Roster { get; }
        public Boat Boat { get; private set; }
        public int SnapshotEveryTicks { get; set; } = 2; // 30 Hz at the 60 Hz tick
        public IReadOnlyList<ulong> Clients => clients;

        /// <summary>Starts serving <paramref name="boat"/>, a raft spawned to match the roster's crew size.</summary>
        public void UseBoat(Boat boat)
        {
            Boat = boat;
            remote.Clear(); // seat numbers mean different places on a raft of another size
            OnRosterChanged();
        }

        public bool IsRemote(int seat)
        {
            var owner = Roster.OwnerOf(seat);
            return owner != Peers.Host && owner != CrewRoster.Nobody;
        }

        /// <summary>Call after every simulation tick; broadcasts a snapshot every <see cref="SnapshotEveryTicks"/> ticks.</summary>
        public void OnTick(int tick)
        {
            if (Boat == null || clients.Count == 0 || tick % SnapshotEveryTicks != 0) return;

            Capture((uint)tick);
            writer.Reset();
            Protocol.Write(writer, snapshot);
            foreach (var client in clients) transport.Send(client, writer.Written, reliable: false);
        }

        void OnClientJoined(ulong client)
        {
            if (clients.Contains(client)) return;
            clients.Add(client);
            Roster.AddHuman(client); // they wear a default look until their profile arrives, a moment later
        }

        void OnClientLeft(ulong client)
        {
            if (!clients.Remove(client)) return;
            Roster.RemoveHuman(client); // their seat becomes a spare for the host's controls; nobody else moves
        }

        void OnRosterChanged()
        {
            if (Boat == null || Boat.Crew.Length != Roster.CrewCount) return; // a new raft is on its way; UseBoat calls back

            Roster.ApplyBlowers(Boat);

            // Point every client-owned seat at that client's input. An input source lives as long as its client keeps
            // the seat, so a re-sent roster never re-baselines (and can't drop) a burst in flight.
            var live = new HashSet<(ulong, int)>();
            foreach (var client in clients)
            {
                var seat = Roster.SeatOf(client);
                if (seat < 0) continue;
                live.Add((client, seat));
                if (!remote.TryGetValue((client, seat), out var input)) remote[(client, seat)] = input = new RemoteSeatInput();
                Boat.Crew[seat].Input = input;
            }

            var stale = new List<(ulong, int)>();
            foreach (var key in remote.Keys)
                if (!live.Contains(key)) stale.Add(key);
            foreach (var key in stale) remote.Remove(key);

            // A seat a client gave up mustn't keep replaying their last input; the host's own controls pick it up.
            foreach (var crew in Boat.Crew)
                if (crew.Input is RemoteSeatInput && !IsRemote(crew.Seat)) crew.Input = null;

            foreach (var client in clients) SendRoster(client);
        }

        void SendRoster(ulong client)
        {
            writer.Reset();
            Protocol.Write(writer, Roster.Describe(client));
            transport.Send(client, writer.Written, reliable: true);
        }

        void OnReceived(ulong from, ArraySegment<byte> message)
        {
            try
            {
                var reader = new ByteReader(message);
                switch (Protocol.PeekType(message))
                {
                    case MessageType.Input:
                        var input = Protocol.ReadInput(reader);
                        if (Roster.SeatOf(from) == input.Seat && remote.TryGetValue((from, input.Seat), out var seat)) // only your own seat
                            seat.Receive(input);
                        break;

                    case MessageType.Profile:
                        Roster.SetProfile(from, Protocol.ReadProfile(reader));
                        break;

                    case MessageType.ClaimSeat:
                        var wanted = Protocol.ReadClaimSeat(reader);
                        if (!Roster.Claim(from, wanted)) SendRoster(from); // refused: remind them where they actually sit
                        break;
                }
            }
            catch (ProtocolException e)
            {
                Debug.LogWarning($"[RushAhead] Ignored a bad message from client {from}: {e.Message}");
            }
        }

        void Capture(uint tick)
        {
            snapshot.Tick = tick;
            if (snapshot.Boats.Count == 0) snapshot.Boats.Add(new BoatSnapshot());

            var b = snapshot.Boats[0];
            b.Position = Boat.Body.Position;
            b.AngleRad = Boat.Body.AngleRad;
            b.Velocity = Boat.Body.Velocity;
            b.AngularVelocity = Boat.Body.AngularVelocityRad;
            b.Tilt = Boat.Tilt.Tilt;
            b.Capsized = Boat.State == BoatState.Capsized;
            b.CapsizeCount = (byte)Mathf.Min(Boat.CapsizeCount, 255);
            b.Crew.Clear();
            foreach (var crew in Boat.Crew)
            {
                b.Crew.Add(new CrewSnapshot
                {
                    LocalAimDeg = crew.LocalAimDeg,
                    Spool = crew.Spool,
                    Air = crew.Tank.Fraction,
                    Braced = crew.Braced,
                    BurstCount = (byte)crew.BurstCount,
                    BlowerIndex = (byte)Mathf.Max(0, Roster.Catalog.IndexOf(crew.Blower)),
                });
            }

            snapshot.Props.Clear();
            foreach (var prop in props)
                snapshot.Props.Add(new PropSnapshot { Position = prop.position, AngleRad = prop.rotation * Mathf.Deg2Rad });
        }

        public void Dispose()
        {
            transport.PeerConnected -= OnClientJoined;
            transport.PeerDisconnected -= OnClientLeft;
            transport.Received -= OnReceived;
            Roster.Changed -= OnRosterChanged;
            foreach (var client in clients) Roster.RemoveHuman(client); // back to a one-device raft
            clients.Clear();
        }
    }
}
