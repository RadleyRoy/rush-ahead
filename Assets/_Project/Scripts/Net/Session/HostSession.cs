using System;
using System.Collections.Generic;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Net
{
    /// <summary>
    /// The authoritative side. Runs the only simulation, seats joiners through the <see cref="CrewRoster"/>, takes each
    /// client's inputs for the seat they own (on whichever raft), keeps every client's roster current, and broadcasts
    /// snapshots of every raft. The host's own controls drive its player's seat; the AI (or, in Co-op with AI off, the
    /// host's other controls) drives the spares.
    /// </summary>
    public sealed class HostSession : IDisposable
    {
        readonly INetTransport transport;
        readonly List<ulong> clients = new();
        readonly Dictionary<(ulong client, int seat), RemoteSeatInput> remote = new();
        readonly List<Boat> boats = new();
        readonly OrderMessage[] orders = new OrderMessage[CrewRoster.MaxRafts];
        readonly ByteWriter writer = new(512);
        readonly Snapshot snapshot = new();
        RaceMessage race;

        public HostSession(INetTransport transport, CrewRoster roster, IReadOnlyList<Rigidbody2D> props)
        {
            this.transport = transport;
            Props = props;
            Roster = roster;
            for (var raft = 0; raft < orders.Length; raft++) orders[raft].Raft = (byte)raft;
            transport.PeerConnected += OnClientJoined;
            transport.PeerDisconnected += OnClientLeft;
            transport.Received += OnReceived;
            roster.Changed += OnRosterChanged;
        }

        public CrewRoster Roster { get; }

        /// <summary>The first raft (the only one in Co-op).</summary>
        public Boat Boat => boats.Count > 0 ? boats[0] : null;

        public IReadOnlyList<Boat> Boats => boats;
        public int SnapshotEveryTicks { get; set; } = 2; // 30 Hz at the 60 Hz tick
        public IReadOnlyList<ulong> Clients => clients;

        /// <summary>Loose bodies every client should see, in an order all devices agree on (the venue's barrel list).</summary>
        public IReadOnlyList<Rigidbody2D> Props { get; set; }

        /// <summary>
        /// A client asked the AI crew to do something; <see cref="OrderMessage.Raft"/> is set to the client's own raft,
        /// whatever they sent. The host decides, then calls <see cref="SetOrder"/>.
        /// </summary>
        public event Action<OrderMessage> OrderRequested;

        /// <summary>Tells every client (and anyone who joins later) what a raft's AI crew is doing, for their order buttons.</summary>
        public void SetOrder(OrderMessage current)
        {
            if (current.Raft >= orders.Length) return;
            orders[current.Raft] = current;
            foreach (var client in clients) Send(client, current);
        }

        void Send(ulong client, in OrderMessage message)
        {
            writer.Reset();
            Protocol.Write(writer, message);
            transport.Send(client, writer.Written, reliable: true);
        }

        /// <summary>Tells every client (and anyone who joins later) what's on: the venue, the race start and the room's phase.</summary>
        public void SetRace(in RaceMessage message)
        {
            race = message;
            foreach (var client in clients) SendRace(client);
        }

        /// <summary>Starts serving one raft (Co-op).</summary>
        public void UseBoat(Boat boat) => UseBoats(new[] { boat });

        /// <summary>Starts serving these rafts, one per roster raft, spawned to match its layout.</summary>
        public void UseBoats(IReadOnlyList<Boat> rafts)
        {
            boats.Clear();
            boats.AddRange(rafts);
            remote.Clear(); // seat numbers mean different places on rafts of another layout
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
            if (boats.Count == 0 || clients.Count == 0 || tick % SnapshotEveryTicks != 0) return;

            Capture((uint)tick);
            writer.Reset();
            Protocol.Write(writer, snapshot);
            foreach (var client in clients) transport.Send(client, writer.Written, reliable: false);
        }

        void OnClientJoined(ulong client)
        {
            if (clients.Contains(client)) return;
            clients.Add(client);
            SendRace(client); // first, so they build the right venue before the rafts appear on it
            for (var raft = 0; raft < Roster.RaftCount; raft++) Send(client, orders[raft]);
            Roster.AddHuman(client); // they wear a default look until their profile arrives, a moment later
        }

        void OnClientLeft(ulong client)
        {
            if (!clients.Remove(client)) return;
            Roster.RemoveHuman(client); // their seat becomes a spare (the AI's, or the host's); nobody else moves
        }

        bool RaftsMatchRoster()
        {
            if (boats.Count != Roster.RaftCount) return false;
            foreach (var boat in boats)
                if (boat == null || boat.Crew.Length != Roster.CrewCount) return false;
            return true;
        }

        void OnRosterChanged()
        {
            if (!RaftsMatchRoster()) return; // new rafts are on their way; UseBoats calls back

            for (var raft = 0; raft < boats.Count; raft++) Roster.ApplyBlowers(boats[raft], raft);

            // Point every client-owned seat at that client's input. An input source lives as long as its client keeps
            // the seat, so a re-sent roster never re-baselines (and can't drop) a burst in flight.
            var live = new HashSet<(ulong, int)>();
            foreach (var client in clients)
            {
                var seat = Roster.SeatOf(client);
                if (seat < 0) continue;
                live.Add((client, seat));
                if (!remote.TryGetValue((client, seat), out var input)) remote[(client, seat)] = input = new RemoteSeatInput();
                boats[Roster.RaftOf(seat)].Crew[Roster.SeatInRaft(seat)].Input = input;
            }

            var stale = new List<(ulong, int)>();
            foreach (var key in remote.Keys)
                if (!live.Contains(key)) stale.Add(key);
            foreach (var key in stale) remote.Remove(key);

            // A seat a client gave up mustn't keep replaying their last input; whoever drives spares picks it up.
            for (var raft = 0; raft < boats.Count; raft++)
                foreach (var crew in boats[raft].Crew)
                    if (crew.Input is RemoteSeatInput && !IsRemote(Roster.SeatAt(raft, crew.Seat))) crew.Input = null;

            foreach (var client in clients) SendRoster(client);
        }

        void SendRace(ulong client)
        {
            writer.Reset();
            Protocol.Write(writer, race);
            transport.Send(client, writer.Written, reliable: true);
        }

        void SendRoster(ulong client)
        {
            writer.Reset();
            Protocol.Write(writer, Roster.Describe(client), Roster.RaftCount);
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
                        var look = Protocol.ReadProfile(reader, out var device);
                        Roster.SetProfile(from, look, device); // a returning player gets their kept seat back here
                        break;

                    case MessageType.Order:
                        var order = Protocol.ReadOrder(reader);
                        var raft = Roster.RaftOfHuman(from);
                        if (raft < 0) break; // watching: no crew to order about
                        order.Raft = (byte)raft;
                        OrderRequested?.Invoke(order);
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
            while (snapshot.Boats.Count < boats.Count) snapshot.Boats.Add(new BoatSnapshot());
            if (snapshot.Boats.Count > boats.Count) snapshot.Boats.RemoveRange(boats.Count, snapshot.Boats.Count - boats.Count);

            for (var i = 0; i < boats.Count; i++)
            {
                var boat = boats[i];
                var b = snapshot.Boats[i];
                b.Position = boat.Body.Position;
                b.AngleRad = boat.Body.AngleRad;
                b.Velocity = boat.Body.Velocity;
                b.AngularVelocity = boat.Body.AngularVelocityRad;
                b.Tilt = boat.Tilt.Tilt;
                b.Capsized = boat.State == BoatState.Capsized;
                b.CapsizeCount = (byte)Mathf.Min(boat.CapsizeCount, 255);
                b.GatesPassed = (byte)Mathf.Min(boat.GatesPassed, 255);
                b.FinishTick = (uint)Mathf.Max(0, boat.FinishTick);
                b.TeleportCount = (byte)boat.TeleportCount;
                b.Fallen = boat.State == BoatState.Fallen;
                b.Out = boat.Out;
                b.Lives = (byte)Mathf.Clamp(boat.Lives, 0, 255);
                b.Crew.Clear();
                foreach (var crew in boat.Crew)
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
            }

            snapshot.Props.Clear();
            foreach (var prop in Props)
                if (prop != null) snapshot.Props.Add(new PropSnapshot { Position = prop.position, AngleRad = prop.rotation * Mathf.Deg2Rad });
        }

        public void Dispose()
        {
            transport.PeerConnected -= OnClientJoined;
            transport.PeerDisconnected -= OnClientLeft;
            transport.Received -= OnReceived;
            Roster.Changed -= OnRosterChanged;
            foreach (var client in clients) Roster.RemoveHuman(client); // back to a one-device game
            clients.Clear();
        }
    }
}
