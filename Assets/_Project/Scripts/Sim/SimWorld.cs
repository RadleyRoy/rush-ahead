using System;
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

        /// <summary>How fast the water moves at a point (a course's currents). Null for still water.</summary>
        public Func<Vector2, Vector2> WaterVelocity { get; set; }

        /// <summary>Loose bodies (barrels) that currents carry along. Their own damping is their drag in still water.</summary>
        public List<Rigidbody2D> Drifting { get; } = new();

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
            foreach (var boat in boats)
            {
                boat.WaterVelocity = WaterVelocity?.Invoke(boat.Body.Position) ?? Vector2.zero;
                boat.Step(dt);
            }

            if (WaterVelocity != null)
            {
                // Damping pulls a barrel toward still; this pulls it toward the water's speed instead, so it drifts with the flow.
                foreach (var body in Drifting)
                    if (body != null && body.simulated) body.AddForce(body.mass * body.linearDamping * WaterVelocity(body.position));
            }

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
            UnityEngine.Object.Destroy(boat.Body.gameObject);
        }

        public void DestroyAll()
        {
            foreach (var boat in boats)
                if (boat.Body != null) UnityEngine.Object.DestroyImmediate(boat.Body.gameObject);
            boats.Clear();
        }
    }
}
