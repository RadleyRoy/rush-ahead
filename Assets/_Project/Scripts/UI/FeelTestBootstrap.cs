using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
    /// The feel test: one raft with 1 to 4 players in a lagoon with loose barrels, a rock slalom and a tight gate, seen
    /// through a third-person chase camera. Plays offline, hosts a LAN game, or joins one. A joined device runs no
    /// simulation at all; it draws the host's raft and barrels from snapshots and sends controls for its own seat.
    /// Every player brings their own animal (species, fur, blower, name); the host's <see cref="CrewRoster"/> decides
    /// who sits where and keeps everyone's view of the crew the same.
    ///
    /// Command line (for testing several copies on one PC):
    ///   -rushahead-host [listen address]   host; listen on 127.0.0.1 for same-machine tests, 0.0.0.0 (default) for LAN
    ///   -rushahead-join &lt;address&gt;          join a host by address
    ///   -rushahead-find [listen address]   look for a room on the LAN and join the first one heard
    ///   -rushahead-port &lt;port&gt;             default 7777
    ///   -rushahead-name / -animal / -fur / -blower   this run's player look (not saved)
    /// </summary>
    public sealed class FeelTestBootstrap : MonoBehaviour
    {
        static readonly Vector2 Spawn = new(-22f, 0f); // far enough from the west bank for the chase camera
        static readonly Vector2 ArenaHalfSize = new(32f, 20f);
        const float BarrelRadius = 0.4f;

        [SerializeField] SimConfig config;
        [SerializeField] CrewCatalog catalog;
        [SerializeField] ArtPalette palette;

        readonly List<GameObject> barrels = new();
        readonly List<Rigidbody2D> barrelBodies = new();
        SimRunner runner;
        RaftView raftView;
        ChaseCamera chase;
        CrewRoster roster;

        INetTransport transport;
        HostSession host;
        ClientSession client;
        LanBeacon beacon;
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
        public CrewInputRouter Router { get; private set; }
        public NetMode Mode { get; private set; }
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

            runner = gameObject.AddComponent<SimRunner>();
            runner.Init(config);
            BuildArena();

            chase = Camera.main.gameObject.AddComponent<ChaseCamera>();
            chase.TargetPosition = () => ViewedBoat.RenderPosition;
            chase.TargetVelocity = () => ViewedBoat.Velocity;

            Router = gameObject.AddComponent<CrewInputRouter>();
            Router.CameraForward = () => chase.Forward;

            Local = LocalCrew.Load(catalog);
            ApplyCommandLineLook();
            roster = new CrewRoster(catalog, crewCount);
            roster.SetHostLooks(Local.Profile, Local.Spares);
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

            if (Mode == NetMode.Hosting)
            {
                NetStatus = $"Hosting on {LocalAddress()}:{port}. {host.Clients.Count} joined.";
                beacon.Advert.Players = (byte)SeatedPlayers();
                beacon.Advert.Seats = (byte)roster.CrewCount;
                beacon.Advert.HostName = Local.Profile.Name;
                beacon.Update(Time.unscaledDeltaTime);
            }

            if (Mode != NetMode.Joined) return;
            client.Update(Time.deltaTime);
            everConnected |= client.Connected;
            if (client.Error != null) hostLost = true; // leave next frame, showing why
            ShowHostsBarrels();
            RebuildJoinedViewIfNeeded();
            NetStatus = !client.Connected ? $"Connecting to {hostAddress}..."
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
            Boat = runner.World.SpawnBoat(Spawn, 0f, roster.Blowers());
            raftView = RaftView.Build(Boat, config, palette, catalog, Looks(roster.Describe(Peers.Host)));
            host?.UseBoat(Boat);
            OnRosterChanged();
            chase.SnapTo(Spawn, 0f);
        }

        public void ResetBoat()
        {
            if (!CanEditRaft) return;
            Boat.ResetTo(Spawn, 0f);
            chase.SnapTo(Spawn, 0f);
            for (var i = 0; i < barrels.Count; i++)
                PhysicsBodies.Reset(barrels[i], BarrelSpot(i));
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
            if (Mode != NetMode.Offline) return;
            try
            {
                transport = NgoTransport.StartHost(port, listen);
            }
            catch (Exception e)
            {
                NetStatus = e.Message;
                return;
            }

            StopSearching();
            host = new HostSession(transport, roster, barrelBodies);
            runner.Ticked += host.OnTick;
            Mode = NetMode.Hosting;
            host.UseBoat(Boat);

            // Same-machine tests listen on loopback only, so they advertise there too and never touch the real network.
            var loopbackOnly = listen == IPAddress.Loopback.ToString();
            beacon = new LanBeacon(targets: loopbackOnly ? new[] { IPAddress.Loopback } : null)
            {
                Advert = new RoomAdvert { RoomId = (uint)UnityEngine.Random.Range(1, int.MaxValue), Version = Protocol.Version, GamePort = port },
            };
        }

        public void Join(DiscoveredRoom room)
        {
            if (room.Advert.Compatible) Join(room.Address, room.Advert.GamePort);
        }

        public void Join(string address, ushort gamePort = 0)
        {
            if (Mode != NetMode.Offline || string.IsNullOrWhiteSpace(address)) return;
            hostAddress = address.Trim();
            try
            {
                transport = NgoTransport.StartClient(hostAddress, gamePort != 0 ? gamePort : port);
            }
            catch (Exception e)
            {
                NetStatus = e.Message;
                return;
            }

            StopSearching();

            // The host runs the only simulation: drop our raft and stop simulating our barrels.
            runner.World.Despawn(Boat);
            Boat = null;
            Destroy(raftView.gameObject);
            raftView = null;
            foreach (var barrel in barrels) PhysicsBodies.Freeze(barrel);

            client = new ClientSession(transport, config, catalog, Local.Profile, seat => Router.Seats[seat].ReadInput());
            client.RosterChanged += OnClientRoster;
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
                foreach (var barrel in barrels) PhysicsBodies.Freeze(barrel, false);
                SetCrewCount(crewCount);
                ResetBoat();
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
            Mode = NetMode.Offline;
            NetStatus = "";
        }

        /// <summary>Offline or hosting: blowers, controls and looks follow the roster.</summary>
        void OnRosterChanged()
        {
            if (Mode == NetMode.Joined || Boat == null || Boat.Crew.Length != roster.CrewCount) return; // a new raft is on its way
            roster.ApplyBlowers(Boat);
            WireLocalSeats(roster.LocalSeats());
            Seats = roster.Describe(Peers.Host);
            if (raftView != null) raftView.Restyle(Looks(Seats));
        }

        void OnClientRoster()
        {
            Router.SetSeats(client.Seats);
            Seats = client.Roster;
            if (raftView != null) raftView.Restyle(Looks(Seats));
        }

        void WireLocalSeats(IReadOnlyList<int> seats)
        {
            Router.SetSeats(seats);
            foreach (var seat in seats) Boat.Crew[seat].Input = Router.Seats[seat];
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
            if (CommandLine.Has("-rushahead-host")) StartHosting(CommandLine.After("-rushahead-host") ?? "0.0.0.0");
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

        void BuildArena()
        {
            var scenery = Shapes3D.Group(transform, "Scenery");
            var random = new System.Random(7); // same scenery every run, and on every player's device
            var h = ArenaHalfSize;

            CourseDressing.Water(scenery, Vector2.zero, 2f * h + new Vector2(120f, 120f), palette);

            // Banks all round, walked counter-clockwise so each cliff builds outward, away from the water.
            var corners = new[] { new Vector2(-h.x, -h.y), new Vector2(h.x, -h.y), new Vector2(h.x, h.y), new Vector2(-h.x, h.y) };
            PhysicsBodies.CreateWallLoop(corners);
            for (var i = 0; i < corners.Length; i++)
                CourseDressing.CliffBank(scenery, corners[i], corners[(i + 1) % corners.Length], 7f, palette, random);

            // A slalom down the middle, then a gate between two rocks with 0.8 m to spare for the 3.2 m raft.
            foreach (var (position, radius) in new[]
                     {
                         (new Vector2(-4f, 6f), 1.5f), (new Vector2(4f, -6f), 1.5f), (new Vector2(12f, 6f), 1.5f), (new Vector2(20f, -6f), 1.5f),
                         (new Vector2(27f, 3.2f), 1.2f), (new Vector2(27f, -3.2f), 1.2f),
                     })
            {
                PhysicsBodies.CreateRock(position, radius);
                CourseDressing.Rock(scenery, position, radius, palette, random);
            }

            CourseDressing.Gate(scenery, new Vector2(27f, 3.2f), new Vector2(27f, -3.2f), palette);

            // Barrels are built in the same order on every device: snapshots identify them by position in this list.
            for (var i = 0; i < 8; i++)
            {
                var barrel = PhysicsBodies.CreateBarrel(BarrelSpot(i), BarrelRadius, 8f);
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
}
