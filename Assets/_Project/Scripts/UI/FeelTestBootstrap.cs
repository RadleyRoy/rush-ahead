using System.Collections.Generic;
using RushAhead.Content;
using RushAhead.Render;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>
    /// Phase 2 feel test: one raft with 1 to 4 players in a lagoon with loose barrels, a rock slalom and a tight
    /// gate, seen through a third-person chase camera. No network. Its only job is answering "is steering this raft
    /// fun?" and finding the <see cref="SimConfig"/> values that make it so.
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
        readonly BlowerDef[] seatBlowers = new BlowerDef[CrewSectors.MaxCrew];
        SimRunner runner;
        RaftView raftView;
        ChaseCamera chase;

        public SimConfig Config => config;
        public IReadOnlyList<BlowerDef> Blowers => blowers;
        public Boat Boat { get; private set; }
        public CrewInputRouter Router { get; private set; }

        void Start()
        {
            Application.targetFrameRate = 60; // mobile defaults to 30
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            runner = gameObject.AddComponent<SimRunner>();
            runner.Init(config);
            BuildArena();
            for (var seat = 0; seat < seatBlowers.Length; seat++) seatBlowers[seat] = blowers[0];

            chase = Camera.main.gameObject.AddComponent<ChaseCamera>();
            chase.TargetPosition = () => Boat.Body.transform.position; // interpolated, so the camera stays smooth
            chase.TargetVelocity = () => Boat.Body.Velocity;

            Router = gameObject.AddComponent<CrewInputRouter>();
            Router.CameraForward = () => chase.Forward;
            SetCrewCount(CrewSectors.MaxCrew);

            var hud = gameObject.AddComponent<FeelTestHud>();
            hud.Game = this;
            Router.IsOverUi = hud.IsOverUi;

            FeelTestAutoDemo.StartIfRequested(this);
        }

        /// <summary>Puts a fresh raft with 1 to 4 players at the start. Each seat keeps the blower chosen for it.</summary>
        public void SetCrewCount(int count)
        {
            if (Boat != null)
            {
                foreach (var crew in Boat.Crew) seatBlowers[crew.Seat] = crew.Blower;
                runner.World.Despawn(Boat);
                Destroy(raftView.gameObject);
            }

            var loadout = new BlowerDef[count];
            for (var seat = 0; seat < count; seat++) loadout[seat] = seatBlowers[seat];
            Boat = runner.World.SpawnBoat(Spawn, 0f, loadout);
            raftView = RaftView.Build(Boat, config, palette);

            Router.SetCrewCount(count);
            for (var seat = 0; seat < count; seat++) Boat.Crew[seat].Input = Router.Seats[seat];
            chase.SnapTo(Spawn, 0f);
        }

        public void ResetBoat()
        {
            Boat.ResetTo(Spawn, 0f);
            chase.SnapTo(Spawn, 0f);
            for (var i = 0; i < barrels.Count; i++)
                PhysicsBodies.Reset(barrels[i], BarrelSpot(i));
        }

        void BuildArena()
        {
            var scenery = Shapes3D.Group(transform, "Scenery");
            var random = new System.Random(7); // same scenery every run
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

            for (var i = 0; i < 8; i++)
            {
                var barrel = PhysicsBodies.CreateBarrel(BarrelSpot(i), BarrelRadius, 8f);
                CourseDressing.Barrel(scenery, barrel.transform, BarrelRadius, palette);
                barrels.Add(barrel);
            }
        }

        static Vector2 BarrelSpot(int i) => new(-15f + 1.8f * (i % 4), -2.7f + 1.8f * (i / 4) + 0.9f * (i % 2));
    }
}
