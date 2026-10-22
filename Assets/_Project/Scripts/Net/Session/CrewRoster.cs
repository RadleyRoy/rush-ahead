using System;
using System.Collections.Generic;
using RushAhead.Content;
using RushAhead.Sim;

namespace RushAhead.Net
{
    /// <summary>
    /// Who sits where, wearing what: the host's single source of truth, and an offline device's too. Every human (the
    /// host's own player and each client) owns at most one seat; a seat nobody owns is a spare, driven by the AI crew
    /// or (with AI off) by the host device's other controls. Joiners take the next free seat in drive order, anyone may
    /// move to a free seat, and players keep their seats when others come and go. A player who drops out has their
    /// seat (and look) kept for them for a while, so they get it back when they reconnect. Only a new crew size
    /// re-deals everyone, in join order.
    /// </summary>
    public sealed class CrewRoster
    {
        public const ulong Nobody = ulong.MaxValue;

        /// <summary>How long a dropped player's seat is kept for them.</summary>
        public const double KeepSeatSeconds = 45.0;

        readonly List<ulong> humans = new() { Peers.Host };
        readonly Dictionary<ulong, Loadout> profiles = new();
        readonly Dictionary<ulong, ulong> devices = new(); // human -> the device id they sent, to recognise them if they come back
        readonly Dictionary<int, Reservation> kept = new(); // seat -> who it's being kept for
        readonly Loadout[] spareLooks = new Loadout[CrewSectors.MaxCrew];
        Loadout hostProfile;
        ulong[] owners = Array.Empty<ulong>();

        public CrewRoster(CrewCatalog catalog, int crewCount)
        {
            Catalog = catalog;
            hostProfile = catalog.DefaultLoadout(0);
            for (var seat = 0; seat < spareLooks.Length; seat++) spareLooks[seat] = catalog.DefaultLoadout(seat + 1);
            SetCrewCount(crewCount);
        }

        struct Reservation
        {
            public ulong Device;
            public Loadout Look;
            public double Until;
        }

        /// <summary>Something changed: a seat, the crew size or a loadout.</summary>
        public event Action Changed;

        /// <summary>Seconds on the host's clock, for keeping seats; the host keeps it current.</summary>
        public double Now { get; set; }

        /// <summary>Spare seats are AI crewmates rather than the host device's extra controls.</summary>
        public bool SparesAreAi
        {
            get => sparesAreAi;
            set
            {
                if (sparesAreAi == value) return;
                sparesAreAi = value;
                Changed?.Invoke();
            }
        }

        bool sparesAreAi;

        public CrewCatalog Catalog { get; }
        public int CrewCount => owners.Length;

        /// <summary>The host's own player first, then clients in the order they joined.</summary>
        public IReadOnlyList<ulong> Humans => humans;

        public ulong OwnerOf(int seat) => owners[seat];

        /// <summary>The seat a human sits in, or -1 if the raft was full when they joined and they're watching.</summary>
        public int SeatOf(ulong human) => Array.IndexOf(owners, human);

        /// <summary>
        /// Seats this device's controls drive, in the order its input slots take them: the host player's own seat, then
        /// the spares, best drivers first.
        /// </summary>
        public int[] LocalSeats()
        {
            var seats = new List<int>(CrewCount);
            var own = SeatOf(Peers.Host);
            if (own >= 0) seats.Add(own);
            if (!SparesAreAi)
                foreach (var seat in CrewSectors.DriveOrder(CrewCount))
                    if (owners[seat] == Nobody) seats.Add(seat);
            return seats.ToArray();
        }

        /// <summary>The seats the AI crew drives: every spare, when <see cref="SparesAreAi"/>.</summary>
        public int[] AiSeats()
        {
            var seats = new List<int>(CrewCount);
            if (SparesAreAi)
                for (var seat = 0; seat < CrewCount; seat++)
                    if (owners[seat] == Nobody) seats.Add(seat);
            return seats.ToArray();
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

        /// <summary>The blowers in seat order, for spawning a raft that matches the roster.</summary>
        public BlowerDef[] Blowers()
        {
            var blowers = new BlowerDef[CrewCount];
            for (var seat = 0; seat < blowers.Length; seat++) blowers[seat] = Catalog.BlowerOf(LoadoutAt(seat));
            return blowers;
        }

        /// <summary>The roster as <paramref name="viewer"/> sees it: every seat's look, and which seats are people, the host and theirs.</summary>
        public RosterSeat[] Describe(ulong viewer)
        {
            var seats = new RosterSeat[CrewCount];
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

        /// <summary>A fresh raft of a new size: everyone is re-dealt a seat in join order, so the host keeps the best one.</summary>
        public void SetCrewCount(int count)
        {
            if (count < 1 || count > CrewSectors.MaxCrew) throw new ArgumentOutOfRangeException(nameof(count), count, "1 to 4 players");
            owners = new ulong[count];
            Array.Fill(owners, Nobody);
            kept.Clear(); // a different raft: the old seats mean nothing now
            foreach (var human in humans) TakeNextFreeSeat(human);
            Changed?.Invoke();
        }

        public void AddHuman(ulong human)
        {
            if (humans.Contains(human)) return;
            humans.Add(human);
            TakeNextFreeSeat(human); // a full raft leaves them watching
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

        /// <summary>Moves a human to a free seat, or seats someone who was watching. Refused if another player sits there.</summary>
        public bool Claim(ulong human, int seat)
        {
            if (!humans.Contains(human) || seat < 0 || seat >= CrewCount) return false;
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

        /// <summary>The host device's own looks: its player's, and what each seat wears while it's a spare.</summary>
        public void SetHostLooks(Loadout profile, IReadOnlyList<Loadout> spares)
        {
            hostProfile = Catalog.Clamp(profile);
            for (var seat = 0; seat < spareLooks.Length && seat < spares.Count; seat++) spareLooks[seat] = Catalog.Clamp(spares[seat]);
            Changed?.Invoke();
        }

        /// <summary>Hands each animal on <paramref name="boat"/> the blower its loadout names. Swapping refills the tank.</summary>
        public void ApplyBlowers(Boat boat)
        {
            if (boat == null || boat.Crew.Length != CrewCount) return;
            foreach (var crew in boat.Crew)
            {
                var blower = Catalog.BlowerOf(LoadoutAt(crew.Seat));
                if (crew.Blower != blower) crew.Equip(blower);
            }
        }

        /// <summary>The best free seat, leaving seats kept for dropped players alone (a newcomer can still choose one).</summary>
        void TakeNextFreeSeat(ulong human)
        {
            foreach (var seat in CrewSectors.DriveOrder(CrewCount))
            {
                if (owners[seat] != Nobody || IsKept(seat)) continue;
                owners[seat] = human;
                return;
            }
        }
    }
}
