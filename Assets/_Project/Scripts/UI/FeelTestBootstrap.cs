using System;
using System.Collections.Generic;
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
    ///
    /// Command line (for testing several copies on one PC):
    ///   -rushahead-host [listen address]   host; listen on 127.0.0.1 for same-machine tests, 0.0.0.0 (default) for LAN
    ///   -rushahead-join &lt;address&gt;          join a host
    ///   -rushahead-port &lt;port&gt;             default 7777
    /// </summary>
    public sealed class FeelTestBootstrap : MonoBehaviour
    {
        static readonly Vector2 Spawn = new(-22f, 0f); // far enough from the west bank for the chase camera
        static readonly Vector2 ArenaHalfSize = new(32f, 20f);
        const float BarrelRadius = 0.4f;

        [SerializeField] SimConfig config;
        [Tooltip("Blowers selectable per seat in the tuning panel. The first is everyone's default.")]
        [SerializeField] BlowerDef[] blowers;
        [SerializeField] ArtPalette palette;

        readonly List<GameObject> barrels = new();
        readonly List<Rigidbody2D> barrelBodies = new();
        readonly BlowerDef[] seatBlowers = new BlowerDef[CrewSectors.MaxCrew];
        SimRunner runner;
        RaftView raftView;
        ChaseCamera chase;

        INetTransport transport;
        HostSession host;
        ClientSession client;
        ushort port = NgoTransport.DefaultPort;
        string hostAddress;
        int crewCount = CrewSectors.MaxCrew;
        int clientViewCrew;
        bool hostLost;
        bool everConnected;

        public SimConfig Config => config;
        public IReadOnlyList<BlowerDef> Blowers => blowers;
        public CrewInputRouter Router { get; private set; }
        public NetMode Mode { get; private set; }
        public string NetStatus { get; private set; } = "";

        /// <summary>The live raft. Null while joined to someone else's game: the host owns the only one.</summary>
        public Boat Boat { get; private set; }

        /// <summary>What's on screen: the live raft offline or hosting, the host's raft rebuilt from snapshots when joined.</summary>
        public IBoatState ViewedBoat => Mode == NetMode.Joined ? client.Boat : Boat;

        /// <summary>Crew size, blowers and resets are the host's to change.</summary>
        public bool CanEditRaft => Mode != NetMode.Joined;

        public int WorldTick => runner.World.Tick;
        public double PlaybackTick => client?.Timeline.PlaybackTick ?? 0;

        void Start()
        {
            Application.targetFrameRate = 60; // mobile defaults to 30
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            runner = gameObject.AddComponent<SimRunner>();
            runner.Init(config);
            BuildArena();
            for (var seat = 0; seat < seatBlowers.Length; seat++) seatBlowers[seat] = blowers[0];

            chase = Camera.main.gameObject.AddComponent<ChaseCamera>();
            chase.TargetPosition = () => ViewedBoat.RenderPosition;
            chase.TargetVelocity = () => ViewedBoat.Velocity;

            Router = gameObject.AddComponent<CrewInputRouter>();
            Router.CameraForward = () => chase.Forward;
            SetCrewCount(CrewSectors.MaxCrew);

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
                LeaveNetwork();
                NetStatus = everConnected ? "Lost the connection to the host." : $"Couldn't reach a host at {hostAddress}.";
            }

            if (Mode == NetMode.Hosting)
                NetStatus = $"Hosting on {LocalAddress()}:{port}. {host.Clients.Count} joined.";

            if (Mode != NetMode.Joined) return;
            client.Update(Time.deltaTime);
            everConnected |= client.Connected;
            ShowHostsBarrels();
            RebuildJoinedViewIfNeeded();
            NetStatus = !client.Connected ? $"Connecting to {hostAddress}..."
                : client.Seats.Length == 0 ? "Joined. The raft is full, so you're watching."
                : $"Joined as P{client.Seats[0] + 1}.";
        }

        void OnDestroy() => ShutDownNetwork(); // closing: just let go of the network, don't rebuild offline play

        /// <summary>A fresh raft with 1 to 4 players at the start. Each seat keeps the blower chosen for it.</summary>
        public void SetCrewCount(int count)
        {
            if (!CanEditRaft) return;
            crewCount = count;
            if (Boat != null)
            {
                foreach (var crew in Boat.Crew) seatBlowers[crew.Seat] = crew.Blower;
                runner.World.Despawn(Boat);
            }

            if (raftView != null) Destroy(raftView.gameObject);

            var loadout = new BlowerDef[count];
            for (var seat = 0; seat < count; seat++) loadout[seat] = seatBlowers[seat];
            Boat = runner.World.SpawnBoat(Spawn, 0f, loadout);
            raftView = RaftView.Build(Boat, config, palette);

            if (host != null) host.UseBoat(Boat); // re-deals seats to joined players, then hands the rest back here
            else WireLocalSeats(CrewSectors.DriveOrder(count));
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

        public void StartHosting(string listenAddress = "0.0.0.0")
        {
            if (Mode != NetMode.Offline) return;
            try
            {
                transport = NgoTransport.StartHost(port, listenAddress);
            }
            catch (Exception e)
            {
                NetStatus = e.Message;
                return;
            }

            host = new HostSession(transport, blowers, barrelBodies);
            host.SeatsChanged += () => WireLocalSeats(host.LocalSeats());
            runner.Ticked += host.OnTick;
            Mode = NetMode.Hosting;
            host.UseBoat(Boat);
        }

        public void Join(string address)
        {
            if (Mode != NetMode.Offline || string.IsNullOrWhiteSpace(address)) return;
            hostAddress = address.Trim();
            try
            {
                transport = NgoTransport.StartClient(hostAddress, port);
            }
            catch (Exception e)
            {
                NetStatus = e.Message;
                return;
            }

            // The host runs the only simulation: drop our raft and stop simulating our barrels.
            runner.World.Despawn(Boat);
            Boat = null;
            Destroy(raftView.gameObject);
            raftView = null;
            foreach (var barrel in barrels) PhysicsBodies.Freeze(barrel);

            client = new ClientSession(transport, config, blowers, seat => Router.Seats[seat].ReadInput());
            client.SeatsAssigned += () => Router.SetSeats(client.Seats);
            client.Disconnected += () => hostLost = true; // handled next frame, outside the transport's own callback
            Router.SetSeats(Array.Empty<int>()); // nothing to drive until the host deals us a seat
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
            else
            {
                WireLocalSeats(CrewSectors.DriveOrder(Boat.Crew.Length));
            }
        }

        void ShutDownNetwork()
        {
            if (host != null)
            {
                runner.Ticked -= host.OnTick;
                host.Dispose();
                host = null;
            }

            client?.Dispose();
            client = null;
            transport?.Shutdown();
            transport = null;
            Mode = NetMode.Offline;
            NetStatus = "";
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
            raftView = RaftView.Build(boat, config, palette);
            clientViewCrew = boat.CrewCount;
            chase.SnapTo(boat.RenderPosition, boat.RenderAngleRad);
        }

        void ShowHostsBarrels()
        {
            var props = client.Boat.Props;
            for (var i = 0; i < barrels.Count && i < props.Count; i++)
                PhysicsBodies.ShowAt(barrels[i], props[i].Position, props[i].AngleRad);
        }

        void ApplyCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            string After(string flag)
            {
                var i = Array.IndexOf(args, flag);
                return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : null;
            }

            if (ushort.TryParse(After("-rushahead-port"), out var customPort)) port = customPort;
            if (Array.IndexOf(args, "-rushahead-host") >= 0) StartHosting(After("-rushahead-host") ?? "0.0.0.0");
            else if (After("-rushahead-join") is { } address) Join(address);
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
}
