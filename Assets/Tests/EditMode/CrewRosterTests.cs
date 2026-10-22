using System.Collections.Generic;
using NUnit.Framework;
using RushAhead.Content;
using RushAhead.Net;
using UnityEngine;

namespace RushAhead.Tests
{
    /// <summary>Who sits where and wearing what: dealing, claiming, coming and going, and looks clamped to the catalog.</summary>
    public class CrewRosterTests
    {
        CrewCatalog catalog;
        readonly List<Object> created = new();

        [SetUp]
        public void SetUp()
        {
            catalog = Make<CrewCatalog>();
            catalog.blowers = new[] { Make<BlowerDef>(), Make<BlowerDef>(), Make<BlowerDef>() };
            catalog.species = new SpeciesDef[6];
            for (var i = 0; i < catalog.species.Length; i++)
            {
                catalog.species[i] = Make<SpeciesDef>();
                catalog.species[i].shape = (AnimalShape)i;
                catalog.species[i].defaultFur = i;
            }

            catalog.furColors = new Color[12];
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            created.Add(o);
            return o;
        }

        [Test]
        public void Joiners_TakeSeatsInDriveOrder_AndKeepThemWhenOthersLeave()
        {
            var roster = new CrewRoster(catalog, 4);
            Assert.That(roster.SeatOf(Peers.Host), Is.EqualTo(2), "the host keeps the best driving seat");

            foreach (var client in new ulong[] { 1, 2, 3, 4 }) roster.AddHuman(client);
            Assert.That(roster.SeatOf(1), Is.EqualTo(3));
            Assert.That(roster.SeatOf(2), Is.EqualTo(0));
            Assert.That(roster.SeatOf(3), Is.EqualTo(1));
            Assert.That(roster.SeatOf(4), Is.EqualTo(-1), "a fifth player watches");
            Assert.That(roster.LocalSeats(), Is.EqualTo(new[] { 2 }));

            roster.RemoveHuman(1);
            Assert.That(roster.OwnerOf(3), Is.EqualTo(CrewRoster.Nobody), "the leaver's seat becomes a spare...");
            Assert.That(roster.SeatOf(2), Is.EqualTo(0), "...and nobody else moves");
            Assert.That(roster.SeatOf(3), Is.EqualTo(1));
            Assert.That(roster.SeatOf(4), Is.EqualTo(-1), "a watcher isn't pushed into a seat; they can claim it");
            Assert.That(roster.LocalSeats(), Is.EqualTo(new[] { 2, 3 }), "the host's controls drive its own seat, then the spares");
        }

        [Test]
        public void Claims_OnlyMoveSomeoneIntoAFreeSeat()
        {
            var roster = new CrewRoster(catalog, 3); // drive order 1, 2, 0
            roster.AddHuman(7);
            Assert.That(roster.SeatOf(Peers.Host), Is.EqualTo(1));
            Assert.That(roster.SeatOf(7), Is.EqualTo(2));

            Assert.That(roster.Claim(7, 1), Is.False, "the host sits there");
            Assert.That(roster.Claim(7, 3), Is.False, "no such seat");
            Assert.That(roster.Claim(99, 0), Is.False, "not a player here");
            Assert.That(roster.Claim(7, 0), Is.True);
            Assert.That(roster.SeatOf(7), Is.EqualTo(0));
            Assert.That(roster.OwnerOf(2), Is.EqualTo(CrewRoster.Nobody), "their old seat is free again");

            Assert.That(roster.Claim(Peers.Host, 2), Is.True, "the host can move too");
            Assert.That(roster.LocalSeats(), Is.EqualTo(new[] { 2, 1 }), "the host's own seat comes first, so its main stick follows it");
        }

        [Test]
        public void ANewCrewSize_RedealsEveryoneInJoinOrder()
        {
            var roster = new CrewRoster(catalog, 4);
            roster.AddHuman(1);
            roster.AddHuman(2);
            roster.Claim(2, 1);

            roster.SetCrewCount(2); // drive order 0, 1
            Assert.That(roster.SeatOf(Peers.Host), Is.EqualTo(0));
            Assert.That(roster.SeatOf(1), Is.EqualTo(1));
            Assert.That(roster.SeatOf(2), Is.EqualTo(-1), "two seats, three players: the last to join watches");
        }

        [Test]
        public void Looks_FollowTheirOwners_AndAreClampedToTheCatalog()
        {
            var roster = new CrewRoster(catalog, 4);
            var changes = 0;
            roster.Changed += () => changes++;

            var mine = new Loadout { Species = 4, Fur = 11, Blower = 2, Name = "Skipper" };
            var spares = new Loadout[4];
            for (var seat = 0; seat < 4; seat++) spares[seat] = new Loadout { Species = (byte)seat, Fur = (byte)seat, Blower = 1, Name = "" };
            roster.SetHostLooks(mine, spares);
            Assert.That(roster.LoadoutAt(2), Is.EqualTo(mine), "the host's seat wears the host's own look");
            Assert.That(roster.LoadoutAt(0), Is.EqualTo(spares[0]), "spares wear their own seat's look");

            roster.AddHuman(5); // takes seat 3
            Assert.That(roster.LoadoutAt(3), Is.EqualTo(catalog.DefaultLoadout(4)), "a default look until their profile arrives");

            roster.SetProfile(5, new Loadout { Species = 200, Fur = 50, Blower = 9, Name = "Way too long a name for a raft" });
            var clamped = roster.LoadoutAt(3);
            Assert.That(clamped.Species, Is.EqualTo(0), "unknown species: the default");
            Assert.That(clamped.Fur, Is.EqualTo(0));
            Assert.That(clamped.Blower, Is.EqualTo(0));
            Assert.That(clamped.Name.Length, Is.LessThanOrEqualTo(Loadout.MaxNameChars));

            Assert.That(roster.Blowers(), Is.EqualTo(new[] { catalog.blowers[1], catalog.blowers[1], catalog.blowers[2], catalog.blowers[0] }));

            var asSeenBy5 = roster.Describe(5);
            Assert.That(asSeenBy5[3].Yours && asSeenBy5[3].Human && !asSeenBy5[3].Host, Is.True);
            Assert.That(asSeenBy5[2].Host && asSeenBy5[2].Human && !asSeenBy5[2].Yours, Is.True);
            Assert.That(asSeenBy5[0].Human, Is.False, "a spare");

            Assert.That(changes, Is.EqualTo(3), "looks, a join and a profile each raised Changed once");
            roster.SetProfile(5, new Loadout { Species = 200, Fur = 50, Blower = 9, Name = "Way too long a name for a raft" });
            Assert.That(changes, Is.EqualTo(3), "the same profile again changes nothing");
        }

        [Test]
        public void WithAiCrewmates_TheHostDrivesOnlyItsOwnSeat_AndTheAiHasTheSpares()
        {
            var roster = new CrewRoster(catalog, 4) { SparesAreAi = true };
            roster.AddHuman(1);
            Assert.That(roster.LocalSeats(), Is.EqualTo(new[] { 2 }));
            Assert.That(roster.AiSeats(), Is.EqualTo(new[] { 0, 1 }), "every seat no player holds");
            var seen = roster.Describe(1);
            Assert.That(seen[0].Ai && seen[1].Ai && !seen[2].Ai && !seen[3].Ai, Is.True);

            roster.SparesAreAi = false;
            Assert.That(roster.AiSeats(), Is.Empty);
            Assert.That(roster.LocalSeats(), Is.EqualTo(new[] { 2, 0, 1 }), "AI off: the host's other controls drive the spares again");
        }

        [Test]
        public void ADroppedPlayersSeat_IsKeptForThem_AndGivenBackWhenTheyReturn()
        {
            var roster = new CrewRoster(catalog, 4) { SparesAreAi = true, Now = 100.0 };
            roster.AddHuman(1);
            var otto = new Loadout { Species = 4, Fur = 9, Blower = 1, Name = "Otto" };
            roster.SetProfile(1, otto, device: 77);
            Assert.That(roster.SeatOf(1), Is.EqualTo(3));

            roster.RemoveHuman(1); // Otto's connection drops
            Assert.That(roster.IsKept(3), Is.True);
            var seen = roster.Describe(Peers.Host)[3];
            Assert.That(seen.Kept && seen.Ai && !seen.Human, Is.True, "an AI keeps the seat warm...");
            Assert.That(seen.Loadout, Is.EqualTo(otto), "...as Otto's own animal");
            Assert.That(roster.AiSeats(), Does.Contain(3));

            roster.Now = 120.0;
            roster.AddHuman(2); // someone new joins meanwhile
            Assert.That(roster.SeatOf(2), Is.EqualTo(0), "a newcomer is dealt another seat, not Otto's");

            roster.AddHuman(3); // Otto reconnects: a new connection, the same device
            roster.SetProfile(3, otto, device: 77);
            Assert.That(roster.SeatOf(3), Is.EqualTo(3), "back in the seat that was kept");
            Assert.That(roster.IsKept(3), Is.False);
        }

        [Test]
        public void AKeptSeat_IsFreeAgainOnceTheWaitIsOver()
        {
            var roster = new CrewRoster(catalog, 2) { Now = 0.0 };
            roster.AddHuman(1);
            roster.SetProfile(1, catalog.DefaultLoadout(1), device: 5);
            roster.RemoveHuman(1);
            Assert.That(roster.IsKept(1), Is.True);

            roster.Now = CrewRoster.KeepSeatSeconds + 1.0;
            Assert.That(roster.IsKept(1), Is.False);
            roster.AddHuman(2);
            Assert.That(roster.SeatOf(2), Is.EqualTo(1), "too late: the seat goes to whoever joins");

            roster.AddHuman(3);
            roster.SetProfile(3, catalog.DefaultLoadout(1), device: 5);
            Assert.That(roster.SeatOf(3), Is.EqualTo(-1), "the late returner watches until a seat frees up");
        }

        [Test]
        public void InATeamRace_PlayersAreSpreadAcrossTheRafts_AndTheAiFillsTheRest()
        {
            var roster = new CrewRoster(catalog, 4, raftCount: 2);
            Assert.That(roster.SeatCount, Is.EqualTo(8));
            Assert.That(roster.SparesAreAi, Is.True, "two rafts are always crewed by AI where nobody sits");
            Assert.That(roster.SeatOf(Peers.Host), Is.EqualTo(2), "the host: Red raft, best driving seat");

            foreach (var client in new ulong[] { 1, 2, 3, 4, 5, 6, 7, 8 }) roster.AddHuman(client);
            Assert.That(roster.RaftOfHuman(1), Is.EqualTo(1), "the next joiner evens up the teams");
            Assert.That(roster.RaftOfHuman(2), Is.EqualTo(0));
            Assert.That(roster.RaftOfHuman(3), Is.EqualTo(1));
            Assert.That(roster.SeatOf(1), Is.EqualTo(roster.SeatAt(1, 2)), "and gets that raft's best seat");
            Assert.That(roster.HumansOn(0), Is.EqualTo(4));
            Assert.That(roster.HumansOn(1), Is.EqualTo(4));
            Assert.That(roster.SeatOf(8), Is.EqualTo(-1), "a ninth player watches");
            Assert.That(roster.AiSeats(), Is.Empty);

            roster.RemoveHuman(3);
            roster.RemoveHuman(5);
            Assert.That(roster.AiSeatsOn(1), Has.Length.EqualTo(2), "the Blue raft's two empty seats go to the AI");
            Assert.That(roster.AiSeatsOn(0), Is.Empty);
            Assert.That(roster.Claim(8, roster.SeatAt(1, 0)), Is.True, "the watcher can take a free seat on either raft");
            Assert.That(roster.RaftOfHuman(8), Is.EqualTo(1));
        }

        [Test]
        public void SwitchingToTeamRace_DealsEveryoneAgain()
        {
            var roster = new CrewRoster(catalog, 4);
            roster.AddHuman(1);
            roster.AddHuman(2);
            Assert.That(roster.RaftOfHuman(1), Is.EqualTo(0));

            roster.SetLayout(4, 2);
            Assert.That(roster.RaftOfHuman(Peers.Host), Is.EqualTo(0));
            Assert.That(roster.RaftOfHuman(1), Is.EqualTo(1));
            Assert.That(roster.RaftOfHuman(2), Is.EqualTo(0));
            Assert.That(roster.Blowers(1), Has.Length.EqualTo(4));
        }

        [Test]
        public void Names_AreTrimmedToPrintableCharacters_WithoutSplittingAnEmoji()
        {
            Assert.That(Loadout.CleanName(null), Is.EqualTo(""));
            Assert.That(Loadout.CleanName("  Ann\r\n "), Is.EqualTo("Ann"));
            Assert.That(Loadout.CleanName("ABCDEFGHIJKLMNOP"), Is.EqualTo("ABCDEFGHIJKL"));

            var name = Loadout.CleanName("Captain Duck 🦆🦆"); // the 12-character cut lands inside the emoji pair
            Assert.That(char.IsHighSurrogate(name[^1]), Is.False, "half an emoji would print as garbage");
            Assert.That(System.Text.Encoding.UTF8.GetByteCount(Loadout.CleanName("ああああああああああああ")), Is.LessThanOrEqualTo(Loadout.MaxNameBytes));
        }
    }
}
