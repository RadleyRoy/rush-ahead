using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using RushAhead.AI;
using RushAhead.Content;
using RushAhead.Net;
using RushAhead.Render;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.UI
{
    public enum NetMode
    {
        Offline,
        Hosting,
        Joined,
    }

    /// <summary>
    /// The feel test: one raft with 1 to 4 players, either in the practice lagoon (loose barrels, a rock slalom and a
    /// tight gate) or racing down a course, seen through a third-person chase camera. Plays offline, hosts a LAN game,
    /// or joins one. A joined device runs no simulation at all; it builds the same venue, draws the host's raft and
    /// barrels from snapshots, and sends controls for its own seat.
    /// Every player brings their own animal (species, fur, blower, name); the host's <see cref="CrewRoster"/> decides
    /// who sits where and keeps everyone's view of the crew the same.
    ///
    /// Command line (for testing several copies on one PC):
    ///   -rushahead-host [listen address]   host; listen on 127.0.0.1 for same-machine tests, 0.0.0.0 (default) for LAN
    ///   -rushahead-host-online               host an online game (needs the build linked to a Unity Cloud project)
    ///   -rushahead-join-code &lt;code&gt;        join an online game by its code
    ///   -rushahead-auth-profile &lt;name&gt;     sign in online as a separate anonymous player (copies on one PC)
    ///   -rushahead-join &lt;address&gt;          join a host by address
    ///   -rushahead-find [listen address]   look for a room on the LAN and join the first one heard
    ///   -rushahead-port &lt;port&gt;             default 7777
    ///   -rushahead-name / -animal / -fur / -blower   this run's player look (not saved)
    ///   -rushahead-course &lt;n&gt;              start on course n (1 is the first course; 0 is the lagoon)
    ///   -rushahead-ai off|clumsy|keen|ace    AI crewmates in the spare seats for this run (not saved)
    ///   -rushahead-ai-all [skill]            AI drives every seat, the host's too (default: the steady autopilot)
    ///   -rushahead-autopilot                 the same as -rushahead-ai-all steady
    /// </summary>
    public sealed class FeelTestBootstrap : MonoBehaviour
    {
        static readonly Vector2 Spawn = new(-22f, 0f); // far enough from the west bank for the chase camera
        static readonly Vector2 ArenaHalfSize = new(32f, 20f);
        const float BarrelRadius = 0.4f;

        [SerializeField] SimConfig config;
        [SerializeField] CrewCatalog catalog;
        [Tooltip("Race courses, in menu order. The order is part of the wire format: only ever append.")]
        [SerializeField] CourseDef[] courses;
        [SerializeField] ArtPalette palette;
        [Tooltip("AI crewmate skills, worst first: Clumsy, Keen, Ace.")]
        [SerializeField] AiProfile[] aiProfiles;

        readonly List<GameObject> barrels = new();
        readonly List<Rigidbody2D> barrelBodies = new();
        SimRunner runner;
        RaftView raftView;
        ChaseCamera chase;
        CrewRoster roster;

        // The venue: 0 is the practice lagoon, n is courses[n - 1]. Barrels are listed in an order every device agrees on.
        int venue;
        GameObject lagoon;
        Course course;
        CourseView courseView;
        Race race;
        AiCrew ai;
        AiSkill? aiDrivesEverything; // set: the AI has every seat, the host's too (demos and testing)
        OrderMessage shownOrder;
        Vector2 spawn = Spawn;
        float spawnAngle;
        int clientRaceStart;
        int shownTeleports = -1;
        (int venue, int start) recordedRun = (-1, -1);

        INetTransport transport;
        HostSession host;
        ClientSession client;
        LanBeacon beacon;
        OnlineRoom onlineRoom;
        bool goingOnline;
        LanDiscovery discovery;
        AndroidMulticastLock multicastLock;
        bool joinFirstRoomHeard;
        ushort port = NgoTransport.DefaultPort;
        string hostAddress;
        int crewCount = CrewSectors.MaxCrew;
        int clientViewCrew;
        bool hostLost;
        bool everConnected;

        public SimConfig Config => config;
        public CrewCatalog Catalog => catalog;
        public IReadOnlyList<CourseDef> Courses => courses;

        /// <summary>0 for the practice lagoon, n for <see cref="Courses"/>[n - 1].</summary>
        public int Venue => venue;

        /// <summary>The course loaded, or null in the lagoon.</summary>
        public Course CurrentCourse => course;

        /// <summary>The run on screen: countdown, clock, checkpoints and, once over, the result.</summary>
        public RaceInfo Run { get; private set; }

        /// <summary>AI crewmates in the spare seats: -1 off, otherwise an index into the skills (Clumsy, Keen, Ace).</summary>
        public int AiSetting => Local.AiSetting;

        public IReadOnlyList<AiProfile> AiProfiles => aiProfiles;

        /// <summary>Whether any seat on the raft on screen is an AI crewmate, so orders make sense.</summary>
        public bool HasAiCrew
        {
            get
            {
                foreach (var seat in Seats)
                    if (seat.Ai) return true;
                return false;
            }
        }

        /// <summary>The AI crew's current order and its push-here point, as this device last heard.</summary>
        public OrderMessage CurrentOrder => Mode == NetMode.Joined ? client.Order : ai != null ? new OrderMessage { Order = ai.Order, Point = ai.PushPoint } : default;
        public CrewInputRouter Router { get; private set; }

        /// <summary>The start page while it's up; null once it has faded away.</summary>
        public TitleScreen Title { get; private set; }
        public NetMode Mode { get; private set; }

        /// <summary>The join code of the online game this device is in, or null (offline, or on a LAN).</summary>
        public string OnlineCode => onlineRoom?.Code;

        /// <summary>Busy setting up or joining an online game (signing in, finding the session, connecting to Relay).</summary>
        public bool GoingOnline => goingOnline;

        public bool OnlineAvailable => OnlineRooms.Available;
        public string NetStatus { get; private set; } = "";

        /// <summary>The animals this device brings: its player's own, and the spare seats' looks.</summary>
        public LocalCrew Local { get; private set; }

        /// <summary>The crew as this device sees it: every seat's look, and which are people, the host and this player.</summary>
        public RosterSeat[] Seats { get; private set; } = Array.Empty<RosterSeat>();

        /// <summary>The live raft. Null while joined to someone else's game: the host owns the only one.</summary>
        public Boat Boat { get; private set; }

        /// <summary>What's on screen: the live raft offline or hosting, the host's raft rebuilt from snapshots when joined.</summary>
        public IBoatState ViewedBoat => Mode == NetMode.Joined ? client.Boat : Boat;

        public RaftView RaftView => raftView;

        /// <summary>Crew size and resets are the host's to change.</summary>
        public bool CanEditRaft => Mode != NetMode.Joined;

        public bool Searching => discovery != null;

        /// <summary>Scripted demos turn this off: listening on every network interface makes desktop firewalls ask the user.</summary>
        public bool AllowSearching { get; set; } = true;
        public IReadOnlyList<DiscoveredRoom> RoomsNearby => discovery != null ? discovery.Rooms : Array.Empty<DiscoveredRoom>();
        public string SearchError => discovery?.Error;

        public int WorldTick => runner.World.Tick;
        public double PlaybackTick => client?.Timeline.PlaybackTick ?? 0;

        void Start()
        {
            Application.targetFrameRate = 60; // mobile defaults to 30
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Title = TitleScreen.Show(gameObject); // the start page covers the first frames while everything is built

            runner = gameObject.AddComponent<SimRunner>();
            runner.Init(config);
            runner.Ticked += StepRace; // ahead of the host's snapshots, so each one carries that tick's progress
            LoadVenue(0);

            chase = Camera.main.gameObject.AddComponent<ChaseCamera>();
            chase.TargetPosition = () => ViewedBoat.RenderPosition;
            chase.TargetVelocity = () => ViewedBoat.Velocity;

            Router = gameObject.AddComponent<CrewInputRouter>();
            Router.CameraForward = () => chase.Forward;

            Local = LocalCrew.Load(catalog);
            ApplyCommandLineLook();
            roster = new CrewRoster(catalog, crewCount);
            roster.SetHostLooks(Local.Profile, Local.Spares);
            roster.SparesAreAi = Local.AiSetting >= 0;
            roster.Changed += OnRosterChanged;
            SetCrewCount(crewCount);

            var hud = gameObject.AddComponent<FeelTestHud>();
            hud.Game = this;
            Router.IsOverUi = hud.IsOverUi;

            ApplyCommandLine();
            FeelTestAutoDemo.StartIfRequested(this);
        }

        void Update()
        {
            if (hostLost)
            {
                hostLost = false;
                var error = client?.Error;
                LeaveNetwork();
                NetStatus = error ?? (everConnected ? "Lost the connection to the host." : $"Couldn't reach a host at {hostAddress}.");
            }

            UpdateDiscovery();
            roster.Now = Time.realtimeSinceStartupAsDouble;

            // Brace and Push here wear off by themselves: keep joined players' order buttons honest when they do.
            if (ai != null && (ai.Order != shownOrder.Order || ai.PushPoint != shownOrder.Point))
            {
                shownOrder = new OrderMessage { Order = ai.Order, Point = ai.PushPoint };
                host?.SetOrder(shownOrder);
            }

            if (Mode == NetMode.Hosting && beacon != null)
            {
                NetStatus = $"Hosting on {LocalAddress()}:{port}. {host.Clients.Count} joined.";
                beacon.Advert.Players = (byte)SeatedPlayers();
                beacon.Advert.Seats = (byte)roster.CrewCount;
                beacon.Advert.HostName = Local.Profile.Name;
                beacon.Update(Time.unscaledDeltaTime);
            }
            else if (Mode == NetMode.Hosting)
            {
                NetStatus = $"Online game {onlineRoom?.Code}. {host.Clients.Count} joined.";
            }

            if (Mode == NetMode.Joined) UpdateJoined();
            UpdateRun();
        }

        void UpdateJoined()
        {
            client.Update(Time.deltaTime);
            everConnected |= client.Connected;
            if (client.Error != null) hostLost = true; // leave next frame, showing why
            ShowHostsBarrels();
            RebuildJoinedViewIfNeeded();
            NetStatus = !client.Connected ? $"Connecting to {(onlineRoom != null ? "the online game" : hostAddress)}..."
                : client.Seats.Length == 0 ? "Joined. The raft is full, so you're watching."
                : $"Joined as P{client.Seats[0] + 1}.";
        }

        void OnDestroy()
        {
            ShutDownNetwork(); // closing: just let go of the network, don't rebuild offline play
            StopSearching();
        }

        /// <summary>A fresh raft with 1 to 4 players at the start. Players are re-seated in the order they joined.</summary>
        public void SetCrewCount(int count)
        {
            if (!CanEditRaft) return;
            crewCount = count;
            if (Boat != null) runner.World.Despawn(Boat);
            Boat = null;
            if (raftView != null) Destroy(raftView.gameObject);

            roster.SetCrewCount(count);
            Boat = runner.World.SpawnBoat(spawn, spawnAngle, roster.Blowers());
            raftView = RaftView.Build(Boat, config, palette, catalog, Looks(roster.Describe(Peers.Host)));
            ai = NewAiCrew();
            host?.UseBoat(Boat);
            OnRosterChanged();
            StartRun();
        }

        /// <summary>AI crewmates off (-1) or at a skill (0 Clumsy, 1 Keen, 2 Ace). Saved on this device; the host's setting rules.</summary>
        public void SetAiSetting(int setting)
        {
            if (!CanEditRaft) return;
            Local.AiSetting = Mathf.Clamp(setting, -1, 2);
            Local.Save();
            ai = NewAiCrew();
            ai.River = course?.River;
            roster.SparesAreAi = Local.AiSetting >= 0; // raises Changed if it flipped, which rewires the seats
            WireSeats();
            Seats = roster.Describe(Peers.Host);
        }

        /// <summary>Tells the AI crew what to do. Joined, the request goes to the host, whose AI it is.</summary>
        public void GiveOrder(CrewOrder order, Vector2 point = default)
        {
            if (Mode == NetMode.Joined) client.RequestOrder(order, point);
            else ApplyOrder(new OrderMessage { Order = order, Point = point });
        }

        void ApplyOrder(OrderMessage order) => ai?.Give(order.Order, order.Point);

        AiCrew NewAiCrew()
        {
            if (Boat == null) return null;
            var skill = aiDrivesEverything ?? (AiSkill)Mathf.Max(0, Local.AiSetting);
            var profile = skill != AiSkill.Steady && aiProfiles != null && (int)skill < aiProfiles.Length && aiProfiles[(int)skill] != null
                ? aiProfiles[(int)skill]
                : AiProfile.Preset(skill);
            return new AiCrew(Boat, profile, config.tickRate, seed: Environment.TickCount);
        }

        /// <summary>Back to the start: on a course, a fresh race with a countdown; in the lagoon, a standing start.</summary>
        public void ResetBoat() => StartRun();

        /// <summary>Loads a venue (0 is the practice lagoon) and starts a run there. Joined players follow the host.</summary>
        public void ChooseVenue(int index)
        {
            if (!CanEditRaft) return;
            LoadVenue(index);
            StartRun();
        }

        void StartRun()
        {
            if (!CanEditRaft || Boat == null) return;
            race?.Dispose();
            race = null;
            if (course != null)
            {
                race = new Race(course, Boat, runner.World.Tick, config.tickRate);
            }
            else
            {
                Boat.ResetTo(spawn, spawnAngle);
                for (var i = 0; i < barrels.Count; i++) PhysicsBodies.Reset(barrels[i], BarrelSpot(i));
            }

            if (ai != null)
            {
                ai.River = course?.River;
                ai.Give(CrewOrder.Go);
            }

            WireSeats();
            host?.SetRace(venue, race?.StartTick ?? 0);
        }

        void StepRace(int tick)
        {
            ai?.Update(config.TickDt); // decides the AI crew's controls for the coming ticks
            race?.Step(tick);
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

        /// <summary>Moves this device's player to a free seat. Joined, the host decides.</summary>
        public void TakeSeat(int seat)
        {
            if (Mode == NetMode.Joined) client.ClaimSeat(seat);
            else roster.Claim(Peers.Host, seat);
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
            try
            {
                transport = NgoTransport.StartHost(port, listen);
            }
            catch (Exception e)
            {
                NetStatus = e.Message;
                return;
            }

            BeginHosting();

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
            NetStatus = "Setting up an online game...";
            try
            {
                var (relayTransport, room) = await OnlineRooms.HostAsync();
                if (this == null || Mode != NetMode.Offline) // closed, or something else started while we waited
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
                NetStatus = OnlineRooms.Explain(e);
                Debug.LogWarning($"[RushAhead] Hosting online failed: {e}");
            }
            finally
            {
                goingOnline = false;
            }
        }

        /// <summary>Joins an online game by the code its host sees.</summary>
        public async void JoinOnline(string code)
        {
            if (Mode != NetMode.Offline || goingOnline) return;
            goingOnline = true;
            NetStatus = "Finding the online game...";
            try
            {
                var (relayTransport, room) = await OnlineRooms.JoinAsync(code);
                if (this == null || Mode != NetMode.Offline)
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
                NetStatus = OnlineRooms.Explain(e);
                Debug.LogWarning($"[RushAhead] Joining online failed: {e}");
            }
            finally
            {
                goingOnline = false;
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
            host.UseBoat(Boat);
            host.SetRace(venue, race?.StartTick ?? 0);
        }

        public void Join(DiscoveredRoom room)
        {
            if (room.Advert.Compatible) Join(room.Address, room.Advert.GamePort);
        }

        public void Join(string address, ushort gamePort = 0)
        {
            if (Mode != NetMode.Offline || goingOnline || string.IsNullOrWhiteSpace(address)) return;
            hostAddress = address.Trim();
            NgoTransport direct;
            try
            {
                direct = NgoTransport.StartClient(hostAddress, gamePort != 0 ? gamePort : port);
            }
            catch (Exception e)
            {
                NetStatus = e.Message;
                return;
            }

            BeginJoining(direct);
        }

        /// <summary>Becomes a joined player over a transport that's connecting to a host.</summary>
        void BeginJoining(NgoTransport connecting)
        {
            transport = connecting;
            StopSearching();

            // The host runs the only simulation: drop our raft and race, and stop simulating our barrels.
            race?.Dispose();
            race = null;
            ai = null;
            runner.World.Despawn(Boat);
            Boat = null;
            Destroy(raftView.gameObject);
            raftView = null;
            foreach (var barrel in barrels) PhysicsBodies.Freeze(barrel);

            client = new ClientSession(transport, config, catalog, Local.Profile, Local.DeviceId, seat => Router.Seats[seat].ReadInput());
            client.RosterChanged += OnClientRoster;
            client.RaceChanged += OnClientRace;
            client.Disconnected += () => hostLost = true; // handled next frame, outside the transport's own callback
            Router.SetSeats(Array.Empty<int>()); // nothing to drive until the host deals us a seat
            Seats = Array.Empty<RosterSeat>();
            everConnected = false;
            clientViewCrew = 0;
            Mode = NetMode.Joined;
        }

        /// <summary>Back to offline play: a host takes every seat back; a joined player gets their own raft again.</summary>
        public void LeaveNetwork()
        {
            if (Mode == NetMode.Offline) return;
            var wasJoined = Mode == NetMode.Joined;
            ShutDownNetwork();

            if (wasJoined)
            {
                LoadVenue(venue); // fresh barrels, simulated here again
                SetCrewCount(crewCount);
            }
        }

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

        /// <summary>Offline or hosting: blowers, controls and looks follow the roster.</summary>
        void OnRosterChanged()
        {
            if (Mode == NetMode.Joined || Boat == null || Boat.Crew.Length != roster.CrewCount) return; // a new raft is on its way
            roster.ApplyBlowers(Boat);
            WireSeats();
            Seats = roster.Describe(Peers.Host);
            if (raftView != null) raftView.Restyle(Looks(Seats));
        }

        void OnClientRoster()
        {
            Router.SetSeats(client.Seats);
            Seats = client.Roster;
            if (raftView != null) raftView.Restyle(Looks(Seats));
        }

        /// <summary>Hands every seat this device simulates to its driver: this device's controls, or the AI crew.</summary>
        void WireSeats()
        {
            if (Boat == null) return;
            int[] local, aiSeats;
            if (aiDrivesEverything.HasValue)
            {
                local = Array.Empty<int>();
                aiSeats = CrewSectors.DriveOrder(Boat.Crew.Length);
            }
            else
            {
                local = roster.LocalSeats();
                aiSeats = roster.AiSeats();
            }

            Router.SetSeats(local);
            foreach (var seat in local) Boat.Crew[seat].Input = Router.Seats[seat];
            ai?.Drive(aiSeats); // seats held by joined players are wired by the host session
        }

        void RebuildJoinedViewIfNeeded()
        {
            var boat = client.Boat;
            if (!boat.HasData || (raftView != null && clientViewCrew == boat.CrewCount)) return;

            if (raftView != null) Destroy(raftView.gameObject);
            raftView = RaftView.Build(boat, config, palette, catalog, Looks(Seats));
            clientViewCrew = boat.CrewCount;
            chase.SnapTo(boat.RenderPosition, boat.RenderAngleRad);
        }

        void ShowHostsBarrels()
        {
            var props = client.Boat.Props;
            for (var i = 0; i < barrels.Count && i < props.Count; i++)
                PhysicsBodies.ShowAt(barrels[i], props[i].Position, props[i].AngleRad);
        }

        static Loadout[] Looks(RosterSeat[] seats) => Array.ConvertAll(seats, s => s.Loadout);

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

        void ApplyCommandLine()
        {
            if (ushort.TryParse(CommandLine.After("-rushahead-port"), out var customPort)) port = customPort;

            if (int.TryParse(CommandLine.After("-rushahead-course"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var startCourse)) ChooseVenue(startCourse);
            OnlineRooms.Profile = CommandLine.After("-rushahead-auth-profile"); // several copies on one PC need different players
            if (CommandLine.Has("-rushahead-host-online")) StartHostingOnline();
            else if (CommandLine.After("-rushahead-join-code") is { } code) JoinOnline(code);
            else if (CommandLine.Has("-rushahead-host")) StartHosting(CommandLine.After("-rushahead-host") ?? "0.0.0.0");
            else if (CommandLine.After("-rushahead-join") is { } address) Join(address);
            else if (CommandLine.Has("-rushahead-find"))
            {
                joinFirstRoomHeard = true;
                StartSearching(CommandLine.After("-rushahead-find") is { } listen ? IPAddress.Parse(listen) : null);
            }
        }

        /// <summary>A look for this run only, so several copies on one PC are told apart. Not saved.</summary>
        void ApplyCommandLineLook()
        {
            var profile = Local.Profile;
            if (CommandLine.After("-rushahead-name") is { } name) profile.Name = name;
            if (byte.TryParse(CommandLine.After("-rushahead-animal"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var species)) profile.Species = species;
            if (byte.TryParse(CommandLine.After("-rushahead-fur"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fur)) profile.Fur = fur;
            if (byte.TryParse(CommandLine.After("-rushahead-blower"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var blower)) profile.Blower = blower;

            // AI for this run only: these never touch the saved setting.
            if (CommandLine.After("-rushahead-ai") is { } aiFlag)
                Local.AiSetting = Enum.TryParse<AiSkill>(aiFlag, true, out var chosen) && chosen != AiSkill.Steady ? (int)chosen : -1;
            if (CommandLine.Has("-rushahead-ai-all"))
                aiDrivesEverything = Enum.TryParse<AiSkill>(CommandLine.After("-rushahead-ai-all") ?? "", true, out var all) ? all : AiSkill.Steady;
            if (CommandLine.Has("-rushahead-autopilot")) aiDrivesEverything = AiSkill.Steady;
            Local.Profile = catalog.Clamp(profile);
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

        /// <summary>Tears down the current venue and builds another. Joined, its barrels only show where the host says.</summary>
        void LoadVenue(int index)
        {
            UnloadVenue();
            venue = Mathf.Clamp(index, 0, courses.Length);
            if (venue == 0)
            {
                BuildLagoon();
                spawn = Spawn;
                spawnAngle = 0f;
            }
            else
            {
                course = Course.Build(courses[venue - 1]);
                courseView = CourseView.Build(course, palette);
                barrels.AddRange(course.Barrels);
                barrelBodies.AddRange(course.BarrelBodies);
                runner.World.WaterVelocity = course.WaterVelocityAt;
                spawn = course.SpawnPosition;
                spawnAngle = course.SpawnAngleRad;
            }

            runner.World.Drifting.AddRange(barrelBodies);
            if (Mode == NetMode.Joined)
                foreach (var barrel in barrels) PhysicsBodies.Freeze(barrel);
            recordedRun = (-1, -1);
        }

        void UnloadVenue()
        {
            race?.Dispose();
            race = null;
            course?.Destroy();
            course = null;
            if (courseView != null) Destroy(courseView.gameObject);
            courseView = null;
            if (lagoon != null)
            {
                lagoon.SetActive(false); // out of the physics world now, not at the end of the frame
                Destroy(lagoon);
            }

            lagoon = null;
            barrels.Clear();
            barrelBodies.Clear();
            runner.World.Drifting.Clear();
            runner.World.WaterVelocity = null;
        }

        void OnClientRace()
        {
            var message = client.Race;
            if (message.Course != venue) LoadVenue(message.Course);
            clientRaceStart = (int)message.StartTick;
        }

        /// <summary>Keeps the camera, the gates and the run's numbers in step with the raft on screen.</summary>
        void UpdateRun()
        {
            var boat = ViewedBoat;
            if (boat == null) return;
            if (boat.TeleportCount != shownTeleports)
            {
                shownTeleports = boat.TeleportCount; // a respawn or restart: cut the camera there, don't swoop
                chase.SnapTo(boat.RenderPosition, boat.RenderAngleRad);
            }

            if (courseView != null) courseView.ShowProgress(boat.GatesPassed);
            if (venue == 0)
            {
                Run = default;
                return;
            }

            var def = courses[venue - 1];
            var start = Mode == NetMode.Joined ? clientRaceStart : race?.StartTick ?? 0;
            var now = Mode == NetMode.Joined ? PlaybackTick : runner.World.Tick;
            var info = new RaceInfo
            {
                Course = def,
                Countdown = RaceClock.CountdownLeft(start, now, config.tickRate),
                SinceStart = (float)((now - start) / config.tickRate),
                Seconds = RaceClock.Seconds(start, boat.FinishTick, now, config.tickRate),
                GatesPassed = boat.GatesPassed,
                Finished = boat.FinishTick > 0,
                Capsizes = boat.CapsizeCount,
                Best = PersonalBest(def),
            };

            if (info.Finished && recordedRun != (venue, start))
            {
                // Every device keeps its own best for each course, from the host's time.
                recordedRun = (venue, start);
                info.NewBest = info.Best <= 0f || info.Seconds < info.Best;
                if (info.NewBest)
                {
                    PlayerPrefs.SetFloat(BestKey(def), info.Seconds);
                    PlayerPrefs.Save();
                }
            }
            else
            {
                info.NewBest = Run.NewBest && info.Finished;
            }

            Run = info;
        }

        static string BestKey(CourseDef def) => $"rushahead.best.{def.name}";
        static float PersonalBest(CourseDef def) => PlayerPrefs.GetFloat(BestKey(def), 0f);

        void BuildLagoon()
        {
            lagoon = new GameObject("Practice lagoon");
            var scenery = Shapes3D.Group(lagoon.transform, "Scenery");
            var random = new System.Random(7); // same scenery every run, and on every player's device
            var h = ArenaHalfSize;

            CourseDressing.Water(scenery, Vector2.zero, 2f * h + new Vector2(120f, 120f), palette);

            // Banks all round, walked counter-clockwise so each cliff builds outward, away from the water.
            var corners = new[] { new Vector2(-h.x, -h.y), new Vector2(h.x, -h.y), new Vector2(h.x, h.y), new Vector2(-h.x, h.y) };
            PhysicsBodies.CreateWallLoop(corners).transform.SetParent(lagoon.transform, true);
            for (var i = 0; i < corners.Length; i++)
                CourseDressing.CliffBank(scenery, corners[i], corners[(i + 1) % corners.Length], 7f, palette, random);

            // A slalom down the middle, then a gate between two rocks with 0.8 m to spare for the 3.2 m raft.
            foreach (var (position, radius) in new[]
                     {
                         (new Vector2(-4f, 6f), 1.5f), (new Vector2(4f, -6f), 1.5f), (new Vector2(12f, 6f), 1.5f), (new Vector2(20f, -6f), 1.5f),
                         (new Vector2(27f, 3.2f), 1.2f), (new Vector2(27f, -3.2f), 1.2f),
                     })
            {
                PhysicsBodies.CreateRock(position, radius).transform.SetParent(lagoon.transform, true);
                CourseDressing.Rock(scenery, position, radius, palette, random);
            }

            CourseDressing.Gate(scenery, new Vector2(27f, 3.2f), new Vector2(27f, -3.2f), palette);

            // Barrels are built in the same order on every device: snapshots identify them by position in this list.
            for (var i = 0; i < 8; i++)
            {
                var barrel = PhysicsBodies.CreateBarrel(BarrelSpot(i), BarrelRadius, 8f);
                barrel.transform.SetParent(lagoon.transform, true);
                CourseDressing.Barrel(scenery, barrel.transform, BarrelRadius, palette);
                barrels.Add(barrel);
                barrelBodies.Add(barrel.GetComponent<Rigidbody2D>());
            }
        }

        static Vector2 BarrelSpot(int i) => new(-15f + 1.8f * (i % 4), -2.7f + 1.8f * (i / 4) + 0.9f * (i % 2));
    }

    /// <summary>Reads <c>-flag value</c> pairs from the player's command line.</summary>
    static class CommandLine
    {
        static readonly string[] Args = Environment.GetCommandLineArgs();

        public static bool Has(string flag) => Array.IndexOf(Args, flag) >= 0;

        /// <summary>The value after <paramref name="flag"/>, or null if it's absent or followed by another flag.</summary>
        public static string After(string flag)
        {
            var i = Array.IndexOf(Args, flag);
            return i >= 0 && i + 1 < Args.Length && !Args[i + 1].StartsWith("-") ? Args[i + 1] : null;
        }
    }

    /// <summary>A run as the HUD shows it: the same on the host and every joined device.</summary>
    public struct RaceInfo
    {
        public CourseDef Course;

        /// <summary>3, 2, 1 before the start; 0 once the clock is running.</summary>
        public int Countdown;

        /// <summary>Seconds since the start signal (negative during the countdown), for showing "GO!" briefly.</summary>
        public float SinceStart;

        /// <summary>The clock: time so far, or the finishing time.</summary>
        public float Seconds;

        public int GatesPassed;
        public bool Finished;
        public int Capsizes;

        /// <summary>This device's best time on the course before this run, or 0 if it has none.</summary>
        public float Best;

        public bool NewBest;

        public bool Active => Course != null;
        public int Stars => Finished ? Course.Stars(Seconds) : 0;
    }
}
