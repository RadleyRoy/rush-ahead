using System.Collections.Generic;
using RushAhead.Content;
using RushAhead.Render;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>
    /// Phase 2 feel test: one boat in an open lagoon with loose barrels, a rock slalom and a tight gate.
    /// No network, no art. Its only job is answering "is steering this boat fun?" and finding the
    /// <see cref="SimConfig"/> values that make it so.
    /// </summary>
    public sealed class FeelTestBootstrap : MonoBehaviour
    {
        static readonly Vector2 Spawn = new(-24f, 0f);
        static readonly Vector2 ArenaHalfSize = new(32f, 20f);
        static readonly Color Rock = new(0.45f, 0.45f, 0.5f);
        static readonly Color Barrel = new(0.85f, 0.55f, 0.2f);
        static readonly Color Bank = new(0.3f, 0.55f, 0.28f);

        [SerializeField] SimConfig config;
        [Tooltip("Blowers selectable per seat in the tuning panel. The first is everyone's default.")]
        [SerializeField] BlowerDef[] blowers;

        readonly List<GameObject> barrels = new();

        public SimConfig Config => config;
        public IReadOnlyList<BlowerDef> Blowers => blowers;
        public Boat Boat { get; private set; }
        public CrewInputRouter Router { get; private set; }

        void Start()
        {
            Application.targetFrameRate = 60; // mobile defaults to 30
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            var runner = gameObject.AddComponent<SimRunner>();
            runner.Init(config);
            BuildArena();

            Boat = runner.World.SpawnBoat(Spawn, 0f, new[] { blowers[0], blowers[0], blowers[0], blowers[0] });
            BoatView.Attach(Boat, config);

            Router = gameObject.AddComponent<CrewInputRouter>();
            for (var seat = 0; seat < Boat.Crew.Length; seat++) Boat.Crew[seat].Input = Router.Seats[seat];

            var cameraFollow = Camera.main.gameObject.AddComponent<FollowCamera>();
            cameraFollow.Target = Boat.Body.transform;
            cameraFollow.Velocity = () => Boat.Body.Velocity;
            Camera.main.transform.position = new Vector3(Spawn.x, Spawn.y, Camera.main.transform.position.z);

            var hud = gameObject.AddComponent<FeelTestHud>();
            hud.Game = this;
            Router.IsOverUi = hud.IsOverUi;

            FeelTestAutoDemo.StartIfRequested(this);
        }

        public void ResetBoat()
        {
            Boat.ResetTo(Spawn, 0f);
            for (var i = 0; i < barrels.Count; i++)
                PhysicsBodies.Reset(barrels[i], BarrelSpot(i));
        }

        void BuildArena()
        {
            var water = ShapeSprites.Spawn(transform, "Water", ShapeSprites.WaterCell(2f, new Color(0.16f, 0.42f, 0.62f), new Color(0.22f, 0.5f, 0.7f)), Color.white, -10);
            water.drawMode = SpriteDrawMode.Tiled;
            water.size = 2f * ArenaHalfSize + new Vector2(16f, 16f);

            var h = ArenaHalfSize;
            PhysicsBodies.CreateWallLoop(new[] { new Vector2(-h.x, -h.y), new Vector2(h.x, -h.y), new Vector2(h.x, h.y), new Vector2(-h.x, h.y) });
            BankStrip(new Vector2(0f, h.y + 2f), new Vector2(2f * h.x + 8f, 4f));
            BankStrip(new Vector2(0f, -h.y - 2f), new Vector2(2f * h.x + 8f, 4f));
            BankStrip(new Vector2(h.x + 2f, 0f), new Vector2(4f, 2f * h.y));
            BankStrip(new Vector2(-h.x - 2f, 0f), new Vector2(4f, 2f * h.y));

            // A slalom down the middle, then a gate only 2.8 m wide for a 2 m beam.
            foreach (var (position, radius) in new[]
                     {
                         (new Vector2(-4f, 6f), 1.5f), (new Vector2(4f, -6f), 1.5f), (new Vector2(12f, 6f), 1.5f), (new Vector2(20f, -6f), 1.5f),
                         (new Vector2(27f, 2.6f), 1.2f), (new Vector2(27f, -2.6f), 1.2f),
                     })
            {
                var rock = PhysicsBodies.CreateRock(position, radius);
                ShapeSprites.Spawn(rock.transform, "Rock", ShapeSprites.Circle(radius), Rock, 1);
            }

            for (var i = 0; i < 8; i++)
            {
                var barrel = PhysicsBodies.CreateBarrel(BarrelSpot(i), 0.4f, 8f);
                ShapeSprites.Spawn(barrel.transform, "Barrel", ShapeSprites.Circle(0.4f), Barrel, 1);
                barrels.Add(barrel);
            }
        }

        static Vector2 BarrelSpot(int i) => new(-17f + 1.6f * (i % 4), -2.4f + 1.6f * (i / 4) + 0.8f * (i % 2));

        void BankStrip(Vector2 centre, Vector2 size)
        {
            var strip = ShapeSprites.Spawn(transform, "Bank", ShapeSprites.Square, Bank, -5);
            strip.transform.position = centre;
            strip.transform.localScale = size;
        }
    }
}
