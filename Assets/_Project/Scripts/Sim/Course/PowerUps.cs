using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>What a raft can carry from a crate, one at a time.</summary>
    public enum PowerUp : byte
    {
        None = 0,

        /// <summary>A hard surge down the river for a couple of seconds.</summary>
        Turbo = 1,

        /// <summary>A bubble that saves the raft once: from a fall off the edge, or a capsize.</summary>
        Shield = 2,

        /// <summary>A blast that shoves every other raft near you away and scatters geese and loose things.</summary>
        Horn = 3,
    }

    /// <summary>
    /// The round's power-up crates and what rafts do with what's in them. A raft that touches a crate while holding
    /// nothing takes an item (rafts at the back are likelier to get a turbo); the crate comes back a few seconds later.
    /// Any of its crew can use the item. Host-authoritative: the crates' readiness and each raft's item and effects go
    /// out in the snapshots.
    /// </summary>
    public sealed class PowerUps
    {
        public const float CrateRadius = 0.75f;
        public const float RespawnSeconds = 8f;
        public const float TurboSeconds = 2.5f;
        public const float HornRadius = 12f;
        public const int MaxCrates = 32; // the snapshot carries a bit each
        const float RaftRadius = 1.6f;
        const float TurboSurge = 16f; // metres per second, per second
        const float TurboAboveWater = 9f; // a little faster than a crew blowing flat out
        const float TurboTopSpeed = 18f;
        const float TurboBendGrip = 8f;
        const float RaftMass = 60f;
        const float HornShove = 85f;
        const float HornHeel = 5f;
        const float HornLoose = 9f; // impulse per kilogram-ish on barrels, logs and geese, falling off with distance

        readonly Course course;
        readonly Vector2[] positions;
        readonly double[] readyAt;
        readonly int[] taken;
        readonly int seed;

        public PowerUps(Course course, CrateSpot[] spots, int seed)
        {
            this.course = course;
            this.seed = seed;
            var count = Mathf.Min(spots?.Length ?? 0, MaxCrates);
            positions = new Vector2[count];
            readyAt = new double[count];
            taken = new int[count];
            for (var i = 0; i < count; i++)
            {
                var at = course.River.At(spots[i].distance);
                positions[i] = at.Centre + at.Left * spots[i].across;
            }
        }

        public int Count => positions.Length;
        public Vector2 Position(int crate) => positions[crate];

        /// <summary>Whether a crate is there to take at <paramref name="seconds"/>.</summary>
        public bool Ready(int crate, double seconds) => seconds >= readyAt[crate];

        /// <summary>A bit per crate, set while it's there to take: what the snapshots carry.</summary>
        public uint ReadyMask(double seconds)
        {
            var mask = 0u;
            for (var i = 0; i < positions.Length; i++)
                if (Ready(i, seconds)) mask |= 1u << i;
            return mask;
        }

        /// <summary>Every crate back, for a new race.</summary>
        public void Reset()
        {
            for (var i = 0; i < readyAt.Length; i++)
            {
                readyAt[i] = 0;
                taken[i] = 0;
            }
        }

        /// <summary>Host, every tick: crates picked up, items used, turbos pushing.</summary>
        public void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            foreach (var boat in boats)
            {
                var racing = boat.State == BoatState.Afloat && !boat.ControlsLocked && !boat.Out;
                if (!racing)
                {
                    boat.ItemRequested = false;
                    boat.TurboSeconds = 0f;
                    continue;
                }

                if (boat.Item == PowerUp.None)
                    for (var i = 0; i < positions.Length; i++)
                    {
                        if (!Ready(i, seconds) || (boat.Body.Position - positions[i]).sqrMagnitude > (RaftRadius + CrateRadius) * (RaftRadius + CrateRadius)) continue;
                        boat.Item = Deal(i, boat, boats);
                        boat.Pickups++;
                        readyAt[i] = seconds + RespawnSeconds;
                        taken[i]++;
                        break;
                    }

                if (boat.ItemRequested)
                {
                    boat.ItemRequested = false;
                    if (boat.Item != PowerUp.None) Use(boat, boats, seconds);
                }

                if (boat.TurboSeconds > 0f)
                {
                    boat.TurboSeconds = Mathf.Max(0f, boat.TurboSeconds - dt);
                    Surge(boat, dt);
                }
            }
        }

        void Use(Boat boat, IReadOnlyList<Boat> boats, double seconds)
        {
            var item = boat.Item;
            boat.Item = PowerUp.None;
            boat.ItemsUsed++;
            switch (item)
            {
                case PowerUp.Turbo:
                    boat.TurboSeconds = TurboSeconds;
                    break;
                case PowerUp.Shield:
                    boat.Shielded = true;
                    break;
                case PowerUp.Horn:
                    boat.HornCount++;
                    Blast(boat, boats, seconds);
                    break;
            }
        }

        /// <summary>The turbo's push down the river, never faster than the water plus a margin, nor than the next bend allows.</summary>
        void Surge(Boat boat, float dt)
        {
            var at = course.River.Project(boat.Body.Position);
            var bend = 0.001f;
            for (var d = at.Distance; d <= at.Distance + 40f; d += 4f) bend = Mathf.Max(bend, Mathf.Abs(course.River.At(d).Curvature));
            var water = Vector2.Dot(course.WaterVelocityAt(boat.Body.Position), at.Tangent);
            var limit = Mathf.Min(Mathf.Min(water + TurboAboveWater, TurboTopSpeed), Mathf.Sqrt(TurboBendGrip / bend));
            if (Vector2.Dot(boat.Body.Velocity, at.Tangent) >= limit) return;
            boat.Push(at.Tangent * (TurboSurge * RaftMass * dt));
        }

        /// <summary>The air horn: other rafts near are shoved away hard, loose things blown clear, creatures scared off.</summary>
        void Blast(Boat boat, IReadOnlyList<Boat> boats, double seconds)
        {
            var from = boat.Body.Position;
            foreach (var other in boats)
            {
                if (other == boat || other.State != BoatState.Afloat || other.Out) continue;
                var offset = other.Body.Position - from;
                var distance = offset.magnitude;
                if (distance > HornRadius) continue;
                var away = distance > 0.01f ? offset / distance : Vector2.right;
                var strength = 1f - 0.5f * distance / HornRadius;
                other.Shove(away * (HornShove * strength), HornHeel * strength);
            }

            foreach (var body in course.BarrelBodies)
            {
                if (body == null || !body.simulated) continue;
                var offset = body.position - from;
                var distance = offset.magnitude;
                if (distance > HornRadius || distance < 0.01f) continue;
                body.AddForce(offset / distance * (HornLoose * body.mass * (1f - distance / HornRadius)), ForceMode2D.Impulse);
            }

            course.Hazards.Scare(from, HornRadius, seconds);
        }

        /// <summary>
        /// What's in the crate: the same for the same round, crate, pickup and raft on every run. The leader gets
        /// shields and horns; anyone behind is likelier to get a turbo to catch up.
        /// </summary>
        PowerUp Deal(int crate, Boat boat, IReadOnlyList<Boat> boats)
        {
            var mine = course.River.Project(boat.Body.Position).Distance;
            var ahead = 0;
            foreach (var other in boats)
                if (other != boat && !other.Out && course.River.Project(other.Body.Position).Distance > mine + 2f) ahead++;
            var hash = unchecked((uint)(seed * 73856093) ^ (uint)(crate * 19349663) ^ (uint)(taken[crate] * 83492791) ^ (uint)(boat.Index * 2654435761u));
            hash ^= hash >> 13;
            hash *= 0x5bd1e995;
            hash ^= hash >> 15;
            var roll = (int)(hash % 100u);
            int turbo, shield;
            if (boats.Count <= 1) (turbo, shield) = (40, 40); // alone: turbo 40, shield 40, horn 20
            else if (ahead == 0) (turbo, shield) = (20, 40); // in front: shields and horns
            else (turbo, shield) = (55, 25); // behind: catch up
            return roll < turbo ? PowerUp.Turbo : roll < turbo + shield ? PowerUp.Shield : PowerUp.Horn;
        }
    }
}
