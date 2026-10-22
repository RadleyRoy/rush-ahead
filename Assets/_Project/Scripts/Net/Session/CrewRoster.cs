using System;
using System.Collections.Generic;
using RushAhead.Content;
using RushAhead.Sim;

namespace RushAhead.Net
{
    /// <summary>
    /// Who sits where, wearing what: the host's single source of truth, and an offline device's too. Every human (the
    /// host's own player and each client) owns at most one seat; a seat nobody owns is a spare, driven by the host
    /// device's other controls. Joiners take the next free seat in drive order, anyone may move to a free seat, and
    /// players keep their seats when others come and go. Only a new crew size re-deals everyone, in join order.
    /// </summary>
    public sealed class CrewRoster
    {
        public const ulong Nobody = ulong.MaxValue;

        readonly List<ulong> humans = new() { Peers.Host };
        readonly Dictionary<ulong, Loadout> profiles = new();
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

        /// <summary>Something changed: a seat, the crew size or a loadout.</summary>
        public event Action Changed;

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
            foreach (var seat in CrewSectors.DriveOrder(CrewCount))
                if (owners[seat] == Nobody) seats.Add(seat);
            return seats.ToArray();
        }

        /// <summary>What a seat's animal looks like and which blower it holds; always inside the catalog.</summary>
        public Loadout LoadoutAt(int seat)
        {
            var owner = owners[seat];
            if (owner == Peers.Host) return hostProfile;
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
            profiles.Remove(human);
            var seat = SeatOf(human);
            if (seat >= 0) owners[seat] = Nobody; // their seat becomes a spare; nobody else moves
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
            Changed?.Invoke();
            return true;
        }

        /// <summary>A client's chosen look, clamped to this device's catalog so nobody can show what the host doesn't have.</summary>
        public void SetProfile(ulong human, Loadout loadout)
        {
            if (human == Peers.Host || !humans.Contains(human)) return;
            var clamped = Catalog.Clamp(loadout);
            if (profiles.TryGetValue(human, out var current) && current.Equals(clamped)) return;
            profiles[human] = clamped;
            Changed?.Invoke();
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

        void TakeNextFreeSeat(ulong human)
        {
            foreach (var seat in CrewSectors.DriveOrder(CrewCount))
            {
                if (owners[seat] != Nobody) continue;
                owners[seat] = human;
                return;
            }
        }
    }
}
