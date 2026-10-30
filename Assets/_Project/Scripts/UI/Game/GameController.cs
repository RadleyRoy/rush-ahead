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
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>
    /// The game: one raft (Co-op) or two (Team Race) with 1 to 4 animals each, either in the practice lagoon (loose
    /// barrels, a rock slalom and a tight gate) or racing down a course, seen through a third-person chase camera. Plays
    /// offline, hosts a LAN or online game, or joins one. A joined device runs no simulation at all; it builds the same
    /// venue, draws the host's rafts and barrels from snapshots, and sends controls for its own seat.
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
    ///   -rushahead-mode team                 Team Race: two rafts, Red against Blue
    ///   -rushahead-ai off|clumsy|keen|ace    AI crewmates in the spare seats for this run (not saved)
    ///   -rushahead-ai-all [skill]            AI drives every seat, the host's too (default: the steady autopilot)
    ///   -rushahead-autopilot                 the same as -rushahead-ai-all steady
    /// </summary>
    public sealed partial class GameController : MonoBehaviour
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
        [Tooltip("The panel every screen draws into (made by the content bootstrap).")]
        [SerializeField] PanelSettings panelSettings;

        readonly List<GameObject> barrels = new();
        readonly List<Rigidbody2D> barrelBodies = new();
        SimRunner runner;
        ChaseCamera chase;
        CrewRoster roster;

        /// <summary>One raft this device simulates (offline or hosting): its boat, view, AI crew and race.</summary>
        sealed class Raft
        {
            public Boat Boat;
            public RaftView View;
            public AiCrew Ai;
            public Race Race;
            public OrderMessage ShownOrder;
        }

        readonly List<Raft> rafts = new();
        readonly List<RaftView> joinedViews = new(); // joined: a view over each of the host's rafts
        readonly List<int> joinedViewCrew = new();
        int raftCount = 1;

        // The venue: 0 is the practice lagoon, n is courses[n - 1]. Barrels are listed in an order every device agrees on.
        int venue;
        GameObject lagoon;
        Course course;
        CourseView courseView;
        AiSkill? aiDrivesEverything; // set: the AI has every seat, the host's too (demos and testing)
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
        bool hostLost;
        bool everConnected;

        public SimConfig Config => config;
        public ArtPalette Palette => palette;
        public CrewCatalog Catalog => catalog;
        public IReadOnlyList<CourseDef> Courses => courses;

        /// <summary>0 for the practice lagoon, n for <see cref="Courses"/>[n - 1].</summary>
        public int Venue => venue;

        /// <summary>The course loaded, or null in the lagoon.</summary>
        public Course CurrentCourse => course;

        /// <summary>The run on screen: countdown, clock, checkpoints and, once over, the result.</summary>
        public RaceInfo Run { get; private set; }

        /// <summary>AI crewmates in the spare seats: -1 off, otherwise an index into the skills (Clumsy, Keen, Ace).</summary>
        public int AiSetting => Mode == NetMode.Joined ? (client.Race.AiSkill == RaceMessage.AiOff ? -1 : client.Race.AiSkill) : Local.AiSetting;

        public IReadOnlyList<AiProfile> AiProfiles => aiProfiles;

        /// <summary>Whether any seat on this player's raft is an AI crewmate, so orders make sense.</summary>
        public bool HasAiCrew
        {
            get
            {
                var crew = CrewCount;
                for (var seat = 0; seat < Seats.Length; seat++)
                    if (Seats[seat].Ai && crew > 0 && seat / crew == MyRaft) return true;
                return false;
            }
        }

        /// <summary>This player's raft's AI order and its push-here point, as this device last heard.</summary>
        public OrderMessage CurrentOrder
        {
            get
            {
                if (Mode == NetMode.Joined) return client.Order;
                var ai = MyRaft < rafts.Count ? rafts[MyRaft].Ai : null;
                return ai != null ? new OrderMessage { Order = ai.Order, Point = ai.PushPoint, Raft = (byte)MyRaft } : default;
            }
        }

        /// <summary>One raft (Co-op) or two (Team Race).</summary>
        public int Rafts => Mode == NetMode.Joined ? client.Rafts : raftCount;

        public bool TeamRace => Rafts > 1;

        /// <summary>Animals per raft.</summary>
        public int CrewCount => Mode == NetMode.Joined ? client.CrewCount : roster.CrewCount;

        /// <summary>The raft this player is on (the first while watching).</summary>
        public int MyRaft => Mode == NetMode.Joined ? client.MyRaft : Mathf.Max(0, roster.RaftOfHuman(Peers.Host));

        /// <summary>A raft's state as drawn here: live offline or hosting, rebuilt from snapshots when joined.</summary>
        public IBoatState RaftState(int raft) =>
            Mode == NetMode.Joined ? (raft < client.Boats.Count ? client.Boats[raft] : null) : raft < rafts.Count ? rafts[raft].Boat : null;

        /// <summary>A raft's view, for name tags; null while it's still being built.</summary>
        public RaftView ViewOf(int raft) =>
            Mode == NetMode.Joined ? (raft < joinedViews.Count ? joinedViews[raft] : null) : raft < rafts.Count ? rafts[raft].View : null;

        public static string TeamName(int raft) => raft == 0 ? "Red" : "Blue";
        public CrewInputRouter Router { get; private set; }

        /// <summary>Every screen, over the world.</summary>
        public GameUi Ui { get; private set; }
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

        /// <summary>This player's live raft. Null while joined to someone else's game: the host owns the only ones.</summary>
        public Boat Boat => MyRaft < rafts.Count ? rafts[MyRaft].Boat : null;

        /// <summary>What's on screen: this player's raft, live offline or hosting, rebuilt from snapshots when joined.</summary>
        public IBoatState ViewedBoat => Mode == NetMode.Joined ? client.Boats[client.MyRaft] : Boat;

        public RaftView RaftView => ViewOf(MyRaft);

        /// <summary>What the menu camera shows: this player's raft, or in the lobby of a Team Race both rafts, and the way they face.</summary>
        public (Vector2 position, float heading) ShowcaseSubject
        {
            get
            {
                var mine = ViewedBoat;
                if (mine == null) return default;
                var other = Rafts > 1 && Phase == RoomPhase.Lobby ? RaftState(1 - MyRaft) : null;
                var position = other != null ? (mine.RenderPosition + other.RenderPosition) * 0.5f : mine.RenderPosition;
                return (position, mine.RenderAngleRad);
            }
        }

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
            runner.Ticked += StepRace; // ahead of the host's snapshots, so each one carries that tick's progress
            LoadVenue(0);

            chase = Camera.main.gameObject.AddComponent<ChaseCamera>();
            chase.TargetPosition = () => ViewedBoat.RenderPosition;
            chase.TargetVelocity = () => ViewedBoat.Velocity;
            chase.TargetDrop = () => RaftView != null ? RaftView.Drop : 0f;

            Router = gameObject.AddComponent<CrewInputRouter>();
            Router.CameraForward = () => chase.Forward;

            Local = LocalCrew.Load(catalog);
            ApplyCommandLineLook();
            roster = new CrewRoster(catalog, crewCount);
            roster.SetHostLooks(Local.Profile, Local.Spares);
            roster.SparesAreAi = Local.AiSetting >= 0;
            roster.Changed += OnRosterChanged;
            SetCrewCount(crewCount);

            Ui = GameUi.Create(this, panelSettings); // the title page covers the first frames while everything else is built
            Router.Blocked = () => Ui.InputBlocked;

            ApplyCommandLine();
            AutoDemo.StartIfRequested(this);
            ScreenTour.StartIfRequested(this);
        }

        void Update()
        {
            if (hostLost)
            {
                hostLost = false;
                var error = client?.Error;
                var reason = error ?? (everConnected ? "Lost the connection to the host." : $"Couldn't reach a host at {hostAddress}.");
                EndSession(reason);
                NetStatus = LastError = reason;
            }

            UpdateDiscovery();
            roster.Now = Time.realtimeSinceStartupAsDouble;

            // Brace and Push here wear off by themselves: keep joined players' order buttons honest when they do.
            for (var i = 0; i < rafts.Count; i++)
            {
                var ai = rafts[i].Ai;
                if (ai == null || (ai.Order == rafts[i].ShownOrder.Order && ai.PushPoint == rafts[i].ShownOrder.Point)) continue;
                rafts[i].ShownOrder = new OrderMessage { Order = ai.Order, Point = ai.PushPoint, Raft = (byte)i };
                host?.SetOrder(rafts[i].ShownOrder);
            }

            if (Mode == NetMode.Hosting && beacon != null)
            {
                NetStatus = $"Hosting on {LocalAddress()}:{port}. {host.Clients.Count} joined.";
                beacon.Advert.Players = (byte)SeatedPlayers();
                beacon.Advert.Seats = (byte)roster.SeatCount;
                beacon.Advert.Phase = phase;
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

        void OnDestroy()
        {
            ShutDownNetwork(); // closing: just let go of the network, don't rebuild offline play
            StopSearching();
        }

        void ApplyCommandLine()
        {
            if (ushort.TryParse(CommandLine.After("-rushahead-port"), out var customPort)) port = customPort;

            if (CommandLine.After("-rushahead-mode") == "team") SetTeamRace(true);
            var raceNow = int.TryParse(CommandLine.After("-rushahead-course"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var startCourse);
            if (raceNow) ChooseVenue(startCourse);
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

            // Given a course, a solo player or a host races at once; joiners follow whatever their host does.
            var joining = CommandLine.Has("-rushahead-join-code") || CommandLine.Has("-rushahead-join") || CommandLine.Has("-rushahead-find");
            if (!raceNow || joining) return;
            if (Mode == NetMode.Offline && !goingOnline) BeginSolo();
            StartRace();
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
    }
}
