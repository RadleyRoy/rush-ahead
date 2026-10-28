using System;
using System.Collections.Generic;
using RushAhead.AI;
using RushAhead.Content;
using RushAhead.Net;
using RushAhead.Render;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>Rafts and crews: layout, AI crewmates, orders, seats and looks.</summary>
    public sealed partial class GameController
    {
        /// <summary>Fresh rafts with 1 to 4 animals each on the start line. Players are re-seated in the order they joined.</summary>
        public void SetCrewCount(int count) => SetLayout(count, raftCount);

        /// <summary>Co-op (one raft) or Team Race (two rafts, Red against Blue, up to 8 players).</summary>
        public void SetTeamRace(bool on) => SetLayout(crewCount, on ? 2 : 1);

        void SetLayout(int count, int raftsWanted)
        {
            if (!CanEditRaft || phase == RoomPhase.Racing) return; // the rafts are chosen in the lobby
            crewCount = count;
            raftCount = raftsWanted;
            DespawnRafts();

            roster.SetLayout(count, raftsWanted);
            for (var raft = 0; raft < raftsWanted; raft++)
            {
                var (position, heading) = SpawnPose(raft);
                var boat = runner.World.SpawnBoat(position, heading, roster.Blowers(raft));
                rafts.Add(new Raft
                {
                    Boat = boat,
                    View = RaftView.Build(boat, config, palette, catalog, LooksOf(roster.Describe(Peers.Host), raft, count), TeamColour(raft)),
                    Ai = NewAiCrew(boat),
                });
            }

            host?.UseBoats(Boats());
            OnRosterChanged();
            phase = RoomPhase.Lobby;
            Stage();
        }

        void DespawnRafts()
        {
            foreach (var raft in rafts)
            {
                raft.Race?.Dispose();
                runner.World.Despawn(raft.Boat);
                if (raft.View != null) Destroy(raft.View.gameObject);
            }

            rafts.Clear();
        }

        List<Boat> Boats() => rafts.ConvertAll(r => r.Boat);

        /// <summary>The tube colour that tells the teams apart; Co-op keeps the classic peach.</summary>
        Color? TeamColour(int raft) => raftCount < 2 ? null : raft == 0 ? new Color(1f, 0.5f, 0.45f) : new Color(0.5f, 0.72f, 1f);

        /// <summary>Where a raft starts in this venue: the middle for one raft, side by side for two.</summary>
        (Vector2 position, float heading) SpawnPose(int raft)
        {
            if (course != null) return course.RespawnPose(0, raft, raftCount);
            return (Spawn + (raftCount > 1 ? new Vector2(0f, raft == 0 ? 3f : -3f) : Vector2.zero), 0f);
        }

        /// <summary>AI crewmates off (-1) or at a skill (0 Clumsy, 1 Keen, 2 Ace). Saved on this device; the host's setting rules.</summary>
        public void SetAiSetting(int setting)
        {
            if (!CanEditRaft || phase == RoomPhase.Racing) return;
            Local.AiSetting = Mathf.Clamp(setting, -1, 2);
            Local.Save();
            for (var i = 0; i < rafts.Count; i++)
            {
                rafts[i].Ai = NewAiCrew(rafts[i].Boat);
                PointAiAtCourse(i);
            }

            roster.SparesAreAi = Local.AiSetting >= 0; // raises Changed if it flipped, which rewires the seats
            WireSeats();
            Seats = roster.Describe(Peers.Host);
            SendRace();
        }

        /// <summary>Tells this player's raft's AI crew what to do. Joined, the request goes to the host, whose AI it is.</summary>
        public void GiveOrder(CrewOrder order, Vector2 point = default)
        {
            if (Mode == NetMode.Joined) client.RequestOrder(order, point);
            else ApplyOrder(new OrderMessage { Order = order, Point = point, Raft = (byte)MyRaft });
        }

        void ApplyOrder(OrderMessage order)
        {
            if (order.Raft < rafts.Count) rafts[order.Raft].Ai?.Give(order.Order, order.Point);
        }

        void PointAiAtCourse(int raft)
        {
            var ai = rafts[raft].Ai;
            if (ai == null) return;
            ai.River = course?.River;
            ai.Lane = raft;
            ai.Lanes = rafts.Count;
        }

        AiCrew NewAiCrew(Boat boat)
        {
            if (boat == null) return null;
            var skill = aiDrivesEverything ?? (AiSkill)Mathf.Max(0, Local.AiSetting);
            var profile = skill != AiSkill.Steady && aiProfiles != null && (int)skill < aiProfiles.Length && aiProfiles[(int)skill] != null
                ? aiProfiles[(int)skill]
                : AiProfile.Preset(skill);
            return new AiCrew(boat, profile, config.tickRate, seed: Environment.TickCount + boat.Index);
        }

        /// <summary>Whether this device chooses a seat's look: its own player's seat, and on its own raft the spares too.</summary>
        public bool CanDress(int seat)
        {
            if (seat < 0 || seat >= Seats.Length) return false;
            return Mode == NetMode.Joined ? Seats[seat].Yours : Seats[seat].Yours || !Seats[seat].Human;
        }

        /// <summary>Changes a seat's look. Joined, it goes to the host, which updates everyone's roster (and ours) in a moment.</summary>
        public void Dress(int seat, Loadout look)
        {
            if (!CanDress(seat)) return;
            look = catalog.Clamp(look);
            if (Seats[seat].Yours) Local.Profile = look;
            else Local.Spares[seat] = look;
            Local.Save();

            if (Mode == NetMode.Joined) client.SetProfile(Local.Profile);
            else roster.SetHostLooks(Local.Profile, Local.Spares);
        }

        /// <summary>Changes this player's own look, whether or not they have a seat (watching, or at the menus).</summary>
        public void DressMe(Loadout look)
        {
            look = catalog.Clamp(look);
            Local.Profile = look;
            Local.Save();
            if (Mode == NetMode.Joined) client.SetProfile(Local.Profile);
            else roster.SetHostLooks(Local.Profile, Local.Spares);
        }

        /// <summary>Moves this device's player to a free seat. Joined, the host decides.</summary>
        public void TakeSeat(int seat)
        {
            if (Mode == NetMode.Joined) client.ClaimSeat(seat);
            else roster.Claim(Peers.Host, seat);
        }

        /// <summary>Offline or hosting: blowers, controls and looks follow the roster.</summary>
        void OnRosterChanged()
        {
            if (Mode == NetMode.Joined || rafts.Count != roster.RaftCount) return; // new rafts are on their way
            foreach (var raft in rafts)
                if (raft.Boat.Crew.Length != roster.CrewCount) return;

            for (var i = 0; i < rafts.Count; i++) roster.ApplyBlowers(rafts[i].Boat, i);
            WireSeats();
            Seats = roster.Describe(Peers.Host);
            for (var i = 0; i < rafts.Count; i++)
                if (rafts[i].View != null) rafts[i].View.Restyle(LooksOf(Seats, i, roster.CrewCount));
        }

        /// <summary>
        /// Hands every seat this device simulates to its driver: this device's controls (its player's seat, plus the
        /// spares in Co-op with AI off), or each raft's AI crew. Seats held by joined players are wired by the host session.
        /// </summary>
        void WireSeats()
        {
            if (rafts.Count == 0) return;
            if (aiDrivesEverything.HasValue)
            {
                Router.SetSeats(Array.Empty<int>());
                foreach (var raft in rafts) raft.Ai?.Drive(CrewSectors.DriveOrder(raft.Boat.Crew.Length));
                return;
            }

            var local = roster.LocalSeats(); // numbered across rafts; the router knows seats within one raft
            Router.SetSeats(Array.ConvertAll(local, roster.SeatInRaft));
            foreach (var seat in local) rafts[roster.RaftOf(seat)].Boat.Crew[roster.SeatInRaft(seat)].Input = Router.Seats[roster.SeatInRaft(seat)];
            for (var i = 0; i < rafts.Count; i++) rafts[i].Ai?.Drive(roster.AiSeatsOn(i));
        }

        /// <summary>One raft's looks out of a roster numbered across rafts.</summary>
        static Loadout[] LooksOf(RosterSeat[] seats, int raft, int crew)
        {
            var looks = new Loadout[Mathf.Max(0, Mathf.Min(crew, seats.Length - raft * crew))];
            for (var seat = 0; seat < looks.Length; seat++) looks[seat] = seats[raft * crew + seat].Loadout;
            return looks;
        }
    }
}
