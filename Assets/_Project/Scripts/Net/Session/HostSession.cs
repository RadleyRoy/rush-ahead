using System;
using System.Collections.Generic;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Net
{
    /// <summary>
    /// The authoritative side. Runs the only simulation, takes each client's inputs for the seats they own, and
    /// broadcasts snapshots. Clients get seats in drive order as they join, after the host's own first seat; the host's
    /// local controls drive every seat nobody else has.
    /// </summary>
    public sealed class HostSession : IDisposable
    {
        readonly INetTransport transport;
        readonly IReadOnlyList<BlowerDef> blowers;
        readonly IReadOnlyList<Rigidbody2D> props;
        readonly List<ulong> clients = new();
        readonly Dictionary<ulong, List<int>> seatsOf = new();
        readonly ByteWriter writer = new(256);
        readonly Snapshot snapshot = new();
        RemoteSeatInput[] remote = Array.Empty<RemoteSeatInput>();

        public HostSession(INetTransport transport, IReadOnlyList<BlowerDef> blowers, IReadOnlyList<Rigidbody2D> props)
        {
            this.transport = transport;
            this.blowers = blowers;
            this.props = props;
            transport.PeerConnected += OnClientJoined;
            transport.PeerDisconnected += OnClientLeft;
            transport.Received += OnReceived;
        }

        /// <summary>Raised when seats move between host and clients; the host should re-wire its local controls.</summary>
        public event Action SeatsChanged;

        public Boat Boat { get; private set; }
        public int SnapshotEveryTicks { get; set; } = 2; // 30 Hz at the 60 Hz tick
        public IReadOnlyList<ulong> Clients => clients;

        /// <summary>Starts serving <paramref name="boat"/>, e.g. after the crew size changes. Seats are re-dealt to clients in join order.</summary>
        public void UseBoat(Boat boat)
        {
            Boat = boat;
            Reassign();
        }

        public bool IsRemote(int seat) => seat < remote.Length && remote[seat] != null;

        /// <summary>The seats the host's own controls drive, best drivers first.</summary>
        public int[] LocalSeats()
        {
            var local = new List<int>();
            foreach (var seat in CrewSectors.DriveOrder(Boat.Crew.Length))
                if (!IsRemote(seat)) local.Add(seat);
            return local.ToArray();
        }

        public IReadOnlyList<int> SeatsOf(ulong client) => seatsOf.TryGetValue(client, out var seats) ? seats : Array.Empty<int>();

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
            if (Boat != null) Reassign();
        }

        void OnClientLeft(ulong client)
        {
            if (!clients.Remove(client)) return;
            if (Boat != null) Reassign(); // their seat comes back to the host
        }

        void Reassign()
        {
            var crew = Boat.Crew.Length;
            remote = new RemoteSeatInput[crew];
            seatsOf.Clear();

            // The host keeps the best driving seat; everyone else gets the next one, until the raft is full.
            var order = CrewSectors.DriveOrder(crew);
            var next = 1;
            foreach (var client in clients)
            {
                var seats = new List<int>();
                if (next < order.Length)
                {
                    var seat = order[next++];
                    seats.Add(seat);
                    remote[seat] = new RemoteSeatInput();
                    Boat.Crew[seat].Input = remote[seat];
                }

                seatsOf[client] = seats; // a full raft leaves late joiners watching
                SendSeats(client, crew, seats);
            }

            SeatsChanged?.Invoke();
        }

        void SendSeats(ulong client, int crew, List<int> seats)
        {
            var message = new SeatsMessage { CrewCount = (byte)crew, Seats = new byte[seats.Count] };
            for (var i = 0; i < seats.Count; i++) message.Seats[i] = (byte)seats[i];
            writer.Reset();
            Protocol.Write(writer, message);
            transport.Send(client, writer.Written, reliable: true);
        }

        void OnReceived(ulong from, ArraySegment<byte> message)
        {
            try
            {
                if (Protocol.PeekType(message) != MessageType.Input) return;
                var input = Protocol.ReadInput(new ByteReader(message));
                if (!seatsOf.TryGetValue(from, out var seats) || !seats.Contains(input.Seat)) return; // only your own seats
                remote[input.Seat].Receive(input);
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
                var blowerIndex = IndexOf(crew.Blower);
                b.Crew.Add(new CrewSnapshot
                {
                    LocalAimDeg = crew.LocalAimDeg,
                    Spool = crew.Spool,
                    Air = crew.Tank.Fraction,
                    Braced = crew.Braced,
                    BurstCount = (byte)crew.BurstCount,
                    BlowerIndex = (byte)Mathf.Max(0, blowerIndex),
                });
            }

            snapshot.Props.Clear();
            foreach (var prop in props)
                snapshot.Props.Add(new PropSnapshot { Position = prop.position, AngleRad = prop.rotation * Mathf.Deg2Rad });
        }

        int IndexOf(BlowerDef blower)
        {
            for (var i = 0; i < blowers.Count; i++)
                if (blowers[i] == blower) return i;
            return -1;
        }

        public void Dispose()
        {
            transport.PeerConnected -= OnClientJoined;
            transport.PeerDisconnected -= OnClientLeft;
            transport.Received -= OnReceived;
        }
    }
}
