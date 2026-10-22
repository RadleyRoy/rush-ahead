using System;
using System.Collections.Generic;
using RushAhead.Content;
using RushAhead.Sim;

namespace RushAhead.Net
{
    /// <summary>
    /// Who sits where, wearing what: the host's single source of truth, and an offline device's too. There are one or
    /// two rafts (Co-op, or Team Race) of the same crew size; seats are numbered across them, raft by raft, so with one
    /// raft a seat number is simply its seat. Every human (the host's own player and each client) owns at most one seat;
    /// a seat nobody owns is a spare, driven by the AI crew or (Co-op with AI off) by the host device's other controls.
    /// Joiners go to the raft with fewer players, taking its next free seat in drive order; anyone may move to a free
    /// seat on either raft, and players keep their seats when others come and go. A player who drops out has their seat
    /// (and look) kept for them for a while, so they get it back when they reconnect. Only a new layout re-deals
    /// everyone, in join order.
    /// </summary>
    public sealed class CrewRoster
    {
        public const ulong Nobody = ulong.MaxValue;
        public const int MaxRafts = 2;
        public const int MaxSeats = CrewSectors.MaxCrew * MaxRafts;

        /// <summary>How long a dropped player's seat is kept for them.</summary>
        public const double KeepSeatSeconds = 45.0;

        readonly List<ulong> humans = new() { Peers.Host };
        readonly Dictionary<ulong, Loadout> profiles = new();
        readonly Dictionary<ulong, ulong> devices = new(); // human -> the device id they sent, to recognise them if they come back
        readonly Dictionary<int, Reservation> kept = new(); // seat -> who it's being kept for
        readonly Loadout[] spareLooks = new Loadout[MaxSeats];
        Loadout hostProfile;
        ulong[] owners = Array.Empty<ulong>();
        bool sparesAreAi;

        public CrewRoster(CrewCatalog catalog, int crewCount, int raftCount = 1)
        {
            Catalog = catalog;
            hostProfile = catalog.DefaultLoadout(0);
            for (var seat = 0; seat < spareLooks.Length; seat++) spareLooks[seat] = catalog.DefaultLoadout(seat + 1);
            SetLayout(crewCount, raftCount);
        }

        struct Reservation
        {
            public ulong Device;
            public Loadout Look;
            public double Until;
        }

        /// <summary>Something changed: a seat, the layout or a loadout.</summary>
        public event Action Changed;

        /// <summary>Seconds on the host's clock, for keeping seats; the host keeps it current.</summary>
        public double Now { get; set; }

        /// <summary>Spare seats are AI crewmates rather than the host device's extra controls. Always true with two rafts.</summary>
        public bool SparesAreAi
        {
            get => sparesAreAi || RaftCount > 1;
            set
            {
                if (sparesAreAi == value) return;
                sparesAreAi = value;
                Changed?.Invoke();
            }
        }

        public CrewCatalog Catalog { get; }

        /// <summary>Animals per raft.</summary>
        public int CrewCount { get; private set; }

        public int RaftCount { get; private set; } = 1;
        public int SeatCount => owners.Length;

        /// <summary>The host's own player first, then clients in the order they joined.</summary>
        public IReadOnlyList<ulong> Humans => humans;

        public int RaftOf(int seat) => seat / CrewCount;
        public int SeatInRaft(int seat) => seat % CrewCount;
        public int SeatAt(int raft, int seatInRaft) => raft * CrewCount + seatInRaft;

        public ulong OwnerOf(int seat) => owners[seat];

        /// <summary>The seat a human sits in, or -1 if every raft was full when they joined and they're watching.</summary>
        public int SeatOf(ulong human) => Array.IndexOf(owners, human);

        /// <summary>The raft a human is on, or -1 while they're watching.</summary>
        public int RaftOfHuman(ulong human)
        {
            var seat = SeatOf(human);
            return seat < 0 ? -1 : RaftOf(seat);
        }

        /// <summary>
        /// Seats this device's controls drive, in the order its input slots take them: the host player's own seat, then
        /// (Co-op with AI off) the spares on that raft, best drivers first.
        /// </summary>
        public int[] LocalSeats()
        {
            var seats = new List<int>(CrewCount);
            var own = SeatOf(Peers.Host);
            if (own >= 0) seats.Add(own);
            if (!SparesAreAi)
            {
                var raft = own >= 0 ? RaftOf(own) : 0;
                foreach (var seat in CrewSectors.DriveOrder(CrewCount))
                    if (owners[SeatAt(raft, seat)] == Nobody) seats.Add(SeatAt(raft, seat));
            }

            return seats.ToArray();
        }

        /// <summary>Every seat the AI drives, across all rafts: each spare, when <see cref="SparesAreAi"/>.</summary>
        public int[] AiSeats()
        {
            var seats = new List<int>(SeatCount);
            if (SparesAreAi)
                for (var seat = 0; seat < SeatCount; seat++)
                    if (owners[seat] == Nobody) seats.Add(seat);
            return seats.ToArray();
        }

        /// <summary>The AI seats on one raft, numbered within that raft (as its crew and AI know them).</summary>
        public int[] AiSeatsOn(int raft)
        {
            var seats = new List<int>(CrewCount);
            foreach (var seat in AiSeats())
                if (RaftOf(seat) == raft) seats.Add(SeatInRaft(seat));
            return seats.ToArray();
        }

        /// <summary>How many people sit on a raft, for balancing the teams.</summary>
        public int HumansOn(int raft)
        {
            var count = 0;
            for (var seat = 0; seat < CrewCount; seat++)
                if (owners[SeatAt(raft, seat)] != Nobody) count++;
            return count;
        }

        /// <summary>Whether a seat is being kept for a player who dropped out, and still has time to come back.</summary>
        public bool IsKept(int seat) => kept.TryGetValue(seat, out var r) && r.Until > Now && owners[seat] == Nobody;

        /// <summary>What a seat's animal looks like and which blower it holds; always inside the catalog.</summary>
        public Loadout LoadoutAt(int seat)
        {
            var owner = owners[seat];
            if (owner == Peers.Host) return hostProfile;
            if (owner == Nobody && IsKept(seat)) return kept[seat].Look; // whoever keeps it warm wears the absent player's animal
            if (owner == Nobody) return spareLooks[seat]; // a spare keeps its look per seat, whoever comes and goes around it
            return profiles.TryGetValue(owner, out var profile) ? profile : Catalog.DefaultLoadout(seat + 1);
        }

        /// <summary>One raft's blowers in seat order, for spawning it to match the roster.</summary>
        public BlowerDef[] Blowers(int raft = 0)
        {
            var blowers = new BlowerDef[CrewCount];
            for (var seat = 0; seat < blowers.Length; seat++) blowers[seat] = Catalog.BlowerOf(LoadoutAt(SeatAt(raft, seat)));
            return blowers;
        }

        /// <summary>The roster as <paramref name="viewer"/> sees it: every seat on every raft, with who's a person, the host and theirs.</summary>
        public RosterSeat[] Describe(ulong viewer)
        {
            var seats = new RosterSeat[SeatCount];
            for (var seat = 0; seat < seats.Length; seat++)
            {
                var owner = owners[seat];
                seats[seat] = new RosterSeat
                {
                    Loadout = LoadoutAt(seat),
                    Human = owner != Nobody,
                    Host = owner == Peers.Host,
                    Yours = owner == viewer,
                    Ai = owner == Nobody && SparesAreAi,
                    Kept = IsKept(seat),
                };
            }

            return seats;
        }

        /// <summary>A fresh raft of a new size, keeping the number of rafts.</summary>
        public void SetCrewCount(int count) => SetLayout(count, RaftCount);

        /// <summary>
        /// New rafts: everyone is re-dealt a seat in join order (so the host keeps the best one), spreading people
        /// across the rafts as evenly as they go.
        /// </summary>
        public void SetLayout(int crewCount, int raftCount)
        {
            if (crewCount < 1 || crewCount > CrewSectors.MaxCrew) throw new ArgumentOutOfRangeException(nameof(crewCount), crewCount, "1 to 4 players");
            if (raftCount < 1 || raftCount > MaxRafts) throw new ArgumentOutOfRangeException(nameof(raftCount), raftCount, "1 or 2 rafts");
            CrewCount = crewCount;
            RaftCount = raftCount;
            owners = new ulong[crewCount * raftCount];
            Array.Fill(owners, Nobody);
            kept.Clear(); // different rafts: the old seats mean nothing now
            foreach (var human in humans) TakeNextFreeSeat(human);
            Changed?.Invoke();
        }

        public void AddHuman(ulong human)
        {
            if (humans.Contains(human)) return;
            humans.Add(human);
            TakeNextFreeSeat(human); // full rafts leave them watching
            Changed?.Invoke();
        }

        public void RemoveHuman(ulong human)
        {
            if (human == Peers.Host || !humans.Remove(human)) return;
            var seat = SeatOf(human);
            if (seat >= 0)
            {
                // Their seat becomes a spare (nobody else moves), kept for them if we'd know them again.
                if (devices.TryGetValue(human, out var device))
                    kept[seat] = new Reservation { Device = device, Look = LoadoutAt(seat), Until = Now + KeepSeatSeconds };
                owners[seat] = Nobody;
            }

            profiles.Remove(human);
            devices.Remove(human);
            Changed?.Invoke();
        }

        /// <summary>Moves a human to a free seat on any raft, or seats someone who was watching. Refused if another player sits there.</summary>
        public bool Claim(ulong human, int seat)
        {
            if (!humans.Contains(human) || seat < 0 || seat >= SeatCount) return false;
            if (owners[seat] == human) return true;
            if (owners[seat] != Nobody) return false;

            var current = SeatOf(human);
            if (current >= 0) owners[current] = Nobody;
            owners[seat] = human;
            kept.Remove(seat); // whoever it was kept for chose not to come back in time, or this is them
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// A client's chosen look, clamped to this device's catalog so nobody can show what the host doesn't have. The
        /// device id recognises a player coming back: if their old seat is still being kept, they're moved back into it.
        /// </summary>
        public void SetProfile(ulong human, Loadout loadout, ulong device = 0)
        {
            if (human == Peers.Host || !humans.Contains(human)) return;
            if (device != 0) devices[human] = device;

            var keptSeat = -1;
            foreach (var pair in kept)
                if (device != 0 && pair.Value.Device == device && IsKept(pair.Key)) keptSeat = pair.Key;

            var rejoined = keptSeat >= 0;
            if (rejoined)
            {
                var current = SeatOf(human);
                if (current >= 0) owners[current] = Nobody;
                owners[keptSeat] = human;
                kept.Remove(keptSeat);
            }

            var clamped = Catalog.Clamp(loadout);
            var same = profiles.TryGetValue(human, out var known) && known.Equals(clamped);
            profiles[human] = clamped;
            if (rejoined || !same) Changed?.Invoke();
        }

        /// <summary>The host device's own looks: its player's, and what each seat (across all rafts) wears while it's a spare.</summary>
        public void SetHostLooks(Loadout profile, IReadOnlyList<Loadout> spares)
        {
            hostProfile = Catalog.Clamp(profile);
            for (var seat = 0; seat < spareLooks.Length && seat < spares.Count; seat++) spareLooks[seat] = Catalog.Clamp(spares[seat]);
            Changed?.Invoke();
        }

        /// <summary>Hands each animal on a raft's boat the blower its loadout names. Swapping refills the tank.</summary>
        public void ApplyBlowers(Boat boat, int raft = 0)
        {
            if (boat == null || boat.Crew.Length != CrewCount || raft >= RaftCount) return;
            foreach (var crew in boat.Crew)
            {
                var blower = Catalog.BlowerOf(LoadoutAt(SeatAt(raft, crew.Seat)));
                if (crew.Blower != blower) crew.Equip(blower);
            }
        }

        /// <summary>
        /// The best free seat on the raft with fewest people (the first raft on a tie), leaving seats kept for dropped
        /// players alone (a newcomer can still choose one). If that raft is full, the other one.
        /// </summary>
        void TakeNextFreeSeat(ulong human)
        {
            var rafts = new List<int>(RaftCount);
            for (var raft = 0; raft < RaftCount; raft++) rafts.Add(raft);
            rafts.Sort((a, b) => HumansOn(a) != HumansOn(b) ? HumansOn(a).CompareTo(HumansOn(b)) : a.CompareTo(b));

            foreach (var raft in rafts)
                foreach (var seat in CrewSectors.DriveOrder(CrewCount))
                {
                    var global = SeatAt(raft, seat);
                    if (owners[global] != Nobody || IsKept(global)) continue;
                    owners[global] = human;
                    return;
                }
        }
    }
}
