using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using RushAhead.Net;
using RushAhead.Render;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>Playing together: hosting and joining on Wi-Fi or online, finding rooms, and drawing the host's rafts when joined.</summary>
    public sealed partial class GameController
    {
        bool soloSession;
        int onlineAttempt;

        /// <summary>
        /// Whether a game is on, and what kind: none at the menus; solo (this device and its AI crewmates); hosting; or
        /// joined to someone else's.
        /// </summary>
        public SessionKind Session => Mode == NetMode.Hosting ? SessionKind.Hosting : Mode == NetMode.Joined ? SessionKind.Joined
            : soloSession ? SessionKind.Solo : SessionKind.None;

        /// <summary>Joined: connected, and the host has said who sits where and what's on. Always true otherwise.</summary>
        public bool SessionReady => Mode != NetMode.Joined || (client.Connected && client.HasRace && client.Roster.Length > 0);

        /// <summary>Why the last game ended without the player choosing to leave (lost the host, couldn't connect); null if they chose.</summary>
        public string SessionEndReason { get; private set; }

        /// <summary>Counts games ended, so screens can notice an ending even when the reason repeats.</summary>
        public int SessionsEnded { get; private set; }

        /// <summary>Hosting on a LAN: the address and port others can join by.</summary>
        public string HostingAddress { get; private set; }

        /// <summary>What went wrong with the last attempt to host or join, for the screen that tried; null if nothing did.</summary>
        public string LastError { get; private set; }

        /// <summary>A game on this device alone, with AI crewmates: straight into the lobby.</summary>
        public void BeginSolo()
        {
            if (Mode != NetMode.Offline || goingOnline) return;
            soloSession = true;
            if (venue == 0) ChooseVenue(PreferredVenue()); // the menus float on the lagoon; a game starts on a course
            else BackToLobby();
        }

        /// <summary>
        /// Ends whatever game is on and goes back to the menus: hosting stops (everyone else is told), a joined player
        /// leaves, and the lagoon comes back as the backdrop. <paramref name="reason"/> is set when the player didn't choose it.
        /// </summary>
        public void EndSession(string reason = null)
        {
            CancelGoingOnline();
            var hadNetwork = Mode != NetMode.Offline;
            if (hadNetwork) ShutDownNetwork();
            soloSession = false;
            SessionEndReason = reason;
            SessionsEnded++;
            Time.timeScale = 1f;
            phase = RoomPhase.Lobby;
            if (venue != 0 || hadNetwork) LoadVenue(0); // fresh barrels, simulated here again
            SetLayout(crewCount, raftCount);
        }

        /// <summary>Gives up on setting up or joining an online game. Anything that arrives afterwards is let go.</summary>
        public void CancelGoingOnline()
        {
            onlineAttempt++;
            goingOnline = false;
        }
        void UpdateJoined()
        {
            client.Update(Time.deltaTime);
            everConnected |= client.Connected;
            if (client.Error != null) hostLost = true; // leave next frame, showing why
            ShowHostsBarrels();
            RebuildJoinedViewIfNeeded();
            NetStatus = !client.Connected ? $"Connecting to {(onlineRoom != null ? "the online game" : hostAddress)}..."
                : client.Seats.Length == 0 ? "Joined. The rafts are full, so you're watching."
                : client.Rafts > 1 ? $"Joined the {TeamName(client.MyRaft)} raft as P{client.Seats[0] % client.CrewCount + 1}."
                : $"Joined as P{client.Seats[0] + 1}.";
        }

        /// <summary>Starts or stops listening for rooms on the LAN. The game only listens while the player is looking.</summary>
        public void SetSearching(bool on)
        {
            if (on && Mode == NetMode.Offline && AllowSearching) StartSearching(null);
            else if (!on && !joinFirstRoomHeard) StopSearching();
        }

        public void StartHosting(string listen = "0.0.0.0")
        {
            if (Mode != NetMode.Offline || goingOnline) return;
            LastError = null;
            try
            {
                transport = NgoTransport.StartHost(port, listen);
            }
            catch (Exception e)
            {
                NetStatus = LastError = e.Message;
                return;
            }

            BeginHosting();
            HostingAddress = $"{(listen == "0.0.0.0" ? LocalAddress() : listen)}:{port}";

            // Same-machine tests listen on loopback only, so they advertise there too and never touch the real network.
            var loopbackOnly = listen == IPAddress.Loopback.ToString();
            beacon = new LanBeacon(targets: loopbackOnly ? new[] { IPAddress.Loopback } : null)
            {
                Advert = new RoomAdvert { RoomId = (uint)UnityEngine.Random.Range(1, int.MaxValue), Version = Protocol.Version, GamePort = port },
            };
        }

        /// <summary>Hosts an online game through Unity Relay. Others join with the code it shows; no Wi-Fi in common needed.</summary>
        public async void StartHostingOnline()
        {
            if (Mode != NetMode.Offline || goingOnline) return;
            goingOnline = true;
            LastError = null;
            var attempt = ++onlineAttempt;
            NetStatus = "Setting up an online game...";
            try
            {
                var (relayTransport, room) = await OnlineRooms.HostAsync();
                if (this == null || Mode != NetMode.Offline || attempt != onlineAttempt) // closed, cancelled, or something else started
                {
                    relayTransport.Shutdown();
                    _ = room.LeaveAsync();
                    return;
                }

                transport = relayTransport;
                onlineRoom = room;
                BeginHosting();
                Debug.Log($"[RushAhead] Hosting online game {room.Code}");
            }
            catch (Exception e)
            {
                if (attempt != onlineAttempt) return;
                NetStatus = LastError = OnlineRooms.Explain(e);
                Debug.LogWarning($"[RushAhead] Hosting online failed: {e}");
            }
            finally
            {
                if (attempt == onlineAttempt) goingOnline = false;
            }
        }

        /// <summary>Joins an online game by the code its host sees.</summary>
        public async void JoinOnline(string code)
        {
            if (Mode != NetMode.Offline || goingOnline) return;
            goingOnline = true;
            LastError = null;
            var attempt = ++onlineAttempt;
            NetStatus = "Finding the online game...";
            try
            {
                var (relayTransport, room) = await OnlineRooms.JoinAsync(code);
                if (this == null || Mode != NetMode.Offline || attempt != onlineAttempt)
                {
                    relayTransport.Shutdown();
                    _ = room.LeaveAsync();
                    return;
                }

                onlineRoom = room;
                hostAddress = $"online game {room.Code}";
                BeginJoining(relayTransport);
                Debug.Log($"[RushAhead] Joined online game {room.Code}");
            }
            catch (Exception e)
            {
                if (attempt != onlineAttempt) return;
                NetStatus = LastError = OnlineRooms.Explain(e);
                Debug.LogWarning($"[RushAhead] Joining online failed: {e}");
            }
            finally
            {
                if (attempt == onlineAttempt) goingOnline = false;
            }
        }

        /// <summary>Starts serving this device's raft over a transport that's already listening.</summary>
        void BeginHosting()
        {
            StopSearching();
            host = new HostSession(transport, roster, barrelBodies);
            host.OrderRequested += ApplyOrder; // any player can order the AI crew about
            runner.Ticked += host.OnTick;
            Mode = NetMode.Hosting;
            if (venue == 0) ChooseVenue(PreferredVenue());
            host.UseBoats(Boats());
            SendRace();
        }

        public void Join(DiscoveredRoom room)
        {
            if (room.Advert.Compatible) Join(room.Address, room.Advert.GamePort);
        }

        public void Join(string address, ushort gamePort = 0)
        {
            if (Mode != NetMode.Offline || goingOnline || string.IsNullOrWhiteSpace(address)) return;
            hostAddress = address.Trim();
            LastError = null;
            NgoTransport direct;
            try
            {
                direct = NgoTransport.StartClient(hostAddress, gamePort != 0 ? gamePort : port);
            }
            catch (Exception e)
            {
                NetStatus = LastError = e.Message;
                return;
            }

            BeginJoining(direct);
        }

        /// <summary>Becomes a joined player over a transport that's connecting to a host.</summary>
        void BeginJoining(NgoTransport connecting)
        {
            transport = connecting;
            StopSearching();

            // The host runs the only simulation: drop our rafts and races, and stop simulating our barrels.
            DespawnRafts();
            foreach (var barrel in barrels) PhysicsBodies.Freeze(barrel);

            // Seats are numbered across rafts; this device's controls are numbered within its own raft.
            client = new ClientSession(transport, config, catalog, Local.Profile, Local.DeviceId,
                seat => Router.Seats[seat % Mathf.Max(1, client.CrewCount)].ReadInput());
            client.RosterChanged += OnClientRoster;
            client.RaceChanged += OnClientRace;
            client.Disconnected += () => hostLost = true; // handled next frame, outside the transport's own callback
            Router.SetSeats(Array.Empty<int>()); // nothing to drive until the host deals us a seat
            Seats = Array.Empty<RosterSeat>();
            everConnected = false;
            Mode = NetMode.Joined;
        }

        /// <summary>Stops hosting or leaves the game, back to the menus.</summary>
        public void LeaveNetwork() => EndSession();

        void ShutDownNetwork()
        {
            if (host != null)
            {
                runner.Ticked -= host.OnTick;
                host.Dispose(); // hands every client's seat back to this device's controls
                host = null;
            }

            client?.Dispose();
            client = null;
            foreach (var view in joinedViews)
                if (view != null) Destroy(view.gameObject);
            joinedViews.Clear();
            joinedViewCrew.Clear();
            beacon?.Dispose();
            beacon = null;
            transport?.Shutdown();
            transport = null;
            if (onlineRoom != null)
            {
                _ = onlineRoom.LeaveAsync(); // tell the service too, so the code stops working; never blocks leaving
                onlineRoom = null;
            }

            Mode = NetMode.Offline;
            NetStatus = "";
        }

        void OnClientRoster()
        {
            var crew = Mathf.Max(1, client.CrewCount);
            Router.SetSeats(Array.ConvertAll(client.Seats, seat => seat % crew)); // this device's controls know only its own raft
            Seats = client.Roster;
            for (var i = 0; i < joinedViews.Count; i++)
                if (joinedViews[i] != null) joinedViews[i].Restyle(LooksOf(Seats, i, crew));
        }

        void RebuildJoinedViewIfNeeded()
        {
            var crew = Mathf.Max(1, client.CrewCount);
            for (var i = 0; i < client.Boats.Count; i++)
            {
                while (joinedViews.Count <= i)
                {
                    joinedViews.Add(null);
                    joinedViewCrew.Add(0);
                }

                var boat = client.Boats[i];
                var wanted = i < client.Rafts && boat.HasData;
                if (!wanted)
                {
                    if (joinedViews[i] != null) Destroy(joinedViews[i].gameObject); // the host went back to one raft
                    joinedViews[i] = null;
                    continue;
                }

                if (joinedViews[i] != null && joinedViewCrew[i] == boat.CrewCount) continue;
                if (joinedViews[i] != null) Destroy(joinedViews[i].gameObject);
                var colour = client.Rafts < 2 ? (Color?)null : i == 0 ? new Color(1f, 0.5f, 0.45f) : new Color(0.5f, 0.72f, 1f);
                joinedViews[i] = RaftView.Build(boat, config, palette, catalog, LooksOf(Seats, i, crew), colour);
                joinedViewCrew[i] = boat.CrewCount;
                if (i == client.MyRaft) chase.SnapTo(boat.RenderPosition, boat.RenderAngleRad);
            }
        }

        void ShowHostsBarrels()
        {
            var props = client.Boat.Props;
            for (var i = 0; i < barrels.Count && i < props.Count; i++)
                PhysicsBodies.ShowAt(barrels[i], props[i].Position, props[i].AngleRad);
        }

        int SeatedPlayers()
        {
            var seated = 0;
            foreach (var seat in Seats)
                if (seat.Human) seated++;
            return seated;
        }

        void StartSearching(IPAddress listen)
        {
            if (discovery != null) return;
            discovery = new LanDiscovery(listenAddress: listen);
            multicastLock = new AndroidMulticastLock();
        }

        void StopSearching()
        {
            discovery?.Dispose();
            discovery = null;
            multicastLock?.Dispose();
            multicastLock = null;
        }

        void UpdateDiscovery()
        {
            if (discovery == null) return;
            discovery.Update(Time.realtimeSinceStartupAsDouble);
            if (!joinFirstRoomHeard || Mode != NetMode.Offline) return;

            foreach (var room in discovery.Rooms)
            {
                if (!room.Advert.Compatible) continue;
                joinFirstRoomHeard = false;
                Debug.Log($"[RushAhead] Found {room.Label} at {room.Address}:{room.Advert.GamePort} ({room.Advert.Players}/{room.Advert.Seats} aboard); joining.");
                Join(room);
                return;
            }
        }

        /// <summary>This device's LAN address, to tell other players where to join.</summary>
        static string LocalAddress()
        {
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
                        if (unicast.Address.AddressFamily == AddressFamily.InterNetwork) return unicast.Address.ToString();
                }
            }
            catch (Exception)
            {
                // Not every platform lets apps list network interfaces.
            }

            return "this device's IP";
        }

        void OnClientRace()
        {
            var message = client.Race;
            if (message.Course != venue) LoadVenue(message.Course);
            clientRaceStart = (int)message.StartTick;
        }
    }
}
