using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// The authoritative simulation: owns every boat and advances them one fixed tick at a time.
    /// Plain C# so tests and headless tools can drive it without a scene loop.
    /// </summary>
    public sealed class SimWorld
    {
        readonly List<Boat> boats = new();

        public SimWorld(SimConfig config) => Config = config;

        public SimConfig Config { get; }
        public IReadOnlyList<Boat> Boats => boats;
        public int Tick { get; private set; }

        public Boat SpawnBoat(Vector2 position, float angleRad, IReadOnlyList<BlowerDef> loadout)
        {
            var body = BoatPhysics.Create(Config, position, angleRad, $"Boat {boats.Count}");
            var boat = new Boat(boats.Count, body, Config, loadout);
            boats.Add(boat);
            return boat;
        }

        /// <summary>Applies one tick of forces. In play mode Unity steps 2D physics straight after FixedUpdate.</summary>
        public void StepForces(float dt)
        {
            foreach (var boat in boats) boat.Step(dt);
            Tick++;
        }

        /// <summary>Forces plus a manual physics step, for tests and headless tools. Requires <see cref="SimulationMode2D.Script"/>.</summary>
        public void Advance(float dt)
        {
            StepForces(dt);
            Physics2D.Simulate(dt);
        }

        public void Despawn(Boat boat)
        {
            if (!boats.Remove(boat) || boat.Body == null) return;
            // Deactivate first: Destroy is deferred to the end of the frame, and a replacement raft spawned on the
            // same spot must not collide with the old one in the meantime.
            boat.Body.gameObject.SetActive(false);
            Object.Destroy(boat.Body.gameObject);
        }

        public void DestroyAll()
        {
            foreach (var boat in boats)
                if (boat.Body != null) Object.DestroyImmediate(boat.Body.gameObject);
            boats.Clear();
        }
    }
}
