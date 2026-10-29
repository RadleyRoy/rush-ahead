using System;
using System.Collections.Generic;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// The round inflatable raft and its crew. Reads simulation state every frame and never writes it.
    /// The sim's tilt becomes a real roll and pitch here, on top of a gentle bob; a capsize flips the raft over.
    /// Each player's air slice is painted on the water around the raft in their player colour.
    /// </summary>
    public sealed class RaftView : MonoBehaviour
    {
        /// <summary>Fixed per seat, unlike fur, which players choose: their slice on the water, their collar and their name tag.</summary>
        static readonly Color[] PlayerColors =
        {
            new(1f, 0.38f, 0.3f), new(0.3f, 0.55f, 1f), new(1f, 0.84f, 0.2f), new(0.4f, 0.9f, 0.4f),
        };

        const float FloorHeight = 0.16f;

        public static Color PlayerColor(int seat) => PlayerColors[seat % PlayerColors.Length];

        IBoatState boat;
        SimConfig config;
        ArtPalette palette;
        CrewCatalog catalog;
        Transform hull;
        AnimalView[] animals;
        AirPuffs[] puffs;
        Loadout[] shownLook;
        BlowerDef[] shownBlower;
        int[] shownBursts;
        float capsize;
        AudioSource[] blowerSounds;
        readonly Queue<(float time, Vector2 velocity)> recent = new();
        int shownTeleports;
        bool wasCapsized;
        float quietUntil;
        float wakeDue;

        const float BumpChange = 1.3f; // a change of speed (m/s) over BumpWindow that can only be a knock
        const float BumpWindow = 0.1f;

        /// <summary>The raft hit something: 0 a knock, 1 a crash. Read from sudden changes of speed, so it works when joined too.</summary>
        public event Action<float> Bumped;

        /// <summary>The raft just went over.</summary>
        public event Action Capsized;

        /// <summary>An animal (by seat) just fired a burst.</summary>
        public event Action<int> Burst;

        /// <param name="boat">The live boat on the host, or a raft rebuilt from snapshots on a client.</param>
        /// <param name="looks">Each seat's chosen animal and fur; seats beyond the list get the catalog's defaults.</param>
        /// <param name="tube">The tube's colour, to tell Team Race rafts apart; null keeps the classic peach.</param>
        public static RaftView Build(IBoatState boat, SimConfig config, ArtPalette palette, CrewCatalog catalog, IReadOnlyList<Loadout> looks,
                                     Color? tube = null)
        {
            var view = new GameObject($"Raft {boat.Index}").AddComponent<RaftView>();
            view.boat = boat;
            view.config = config;
            view.palette = palette;
            view.catalog = catalog;
            view.BuildRaft(config.hullRadius, tube.HasValue ? palette.Tinted(palette.raftTube, tube.Value) : palette.raftTube);
            view.BuildSlices(config.hullRadius);
            view.BuildCrew(looks);
            view.shownTeleports = boat.TeleportCount;
            view.LateUpdate();
            return view;
        }

        /// <summary>Confetti, and the whole crew jumps for joy.</summary>
        public void Celebrate()
        {
            Splashes.Confetti(transform.position, palette);
            foreach (var animal in animals) animal.Cheer();
        }

        /// <summary>Re-dresses any animal whose species or fur changed, leaving the rest (and where they're aiming) alone.</summary>
        public void Restyle(IReadOnlyList<Loadout> looks)
        {
            for (var seat = 0; seat < animals.Length; seat++)
            {
                var look = LookAt(looks, seat);
                if (look.Species == shownLook[seat].Species && look.Fur == shownLook[seat].Fur) continue;

                var facing = animals[seat].transform.rotation;
                Destroy(animals[seat].gameObject);
                BuildAnimal(seat, look);
                animals[seat].transform.rotation = facing;
            }
        }

        /// <summary>World position just above a seat's animal, for its name tag.</summary>
        public Vector3 TagPosition(int seat) => animals[seat].TagAnchor.position;

        void BuildRaft(float radius, Material tubeMaterial)
        {
            Shapes3D.MeshObject("Foam", Shapes3D.Disc(radius + 0.55f), transform, new Vector3(0f, 0.02f, 0f), palette.foam);

            hull = Shapes3D.Group(transform, "Hull");
            const float tube = 0.24f;
            Shapes3D.MeshObject("Band", Shapes3D.Torus(radius - tube, tube), hull, new Vector3(0f, 0.06f, 0f), palette.raftBand);
            Shapes3D.MeshObject("Tube", Shapes3D.Torus(radius - tube - 0.02f, tube * 0.95f), hull, new Vector3(0f, 0.36f, 0f), tubeMaterial);
            Shapes3D.MeshObject("Floor", Shapes3D.Disc(radius - tube), hull, new Vector3(0f, FloorHeight, 0f), palette.raftFloor);

            // Grab handles around the tube, like the real thing.
            for (var i = 0; i < 8; i++)
            {
                var angle = (i + 0.5f) * Mathf.PI / 4f;
                var at = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (radius - 0.02f);
                Shapes3D.Primitive(PrimitiveType.Cube, hull, at + Vector3.up * 0.38f, new Vector3(0.06f, 0.08f, 0.26f), palette.blowerTrim,
                    Quaternion.LookRotation(Vector3.Cross(Vector3.up, at)), "Handle");
            }
        }

        void BuildCrew(IReadOnlyList<Loadout> looks)
        {
            var count = boat.CrewCount;
            animals = new AnimalView[count];
            puffs = new AirPuffs[count];
            blowerSounds = new AudioSource[count];
            shownLook = new Loadout[count];
            shownBlower = new BlowerDef[count];
            shownBursts = new int[count];

            for (var seat = 0; seat < count; seat++)
            {
                BuildAnimal(seat, LookAt(looks, seat));
                shownBursts[seat] = boat.CrewAt(seat).BurstCount;
            }
        }

        void BuildAnimal(int seat, Loadout look)
        {
            var crew = boat.CrewAt(seat);
            var animal = animals[seat] = AnimalView.Build(hull, catalog.SpeciesOf(look).shape, catalog.FurOf(look), PlayerColor(seat), crew.Blower, palette);
            animal.transform.localPosition = SimSpace.HullLocal(config.SeatOffset(seat, boat.CrewCount) * 0.85f, FloorHeight);
            puffs[seat] = AirPuffs.Attach(animal.Nozzle, palette.air);
            var sound = blowerSounds[seat] = animal.Nozzle.gameObject.AddComponent<AudioSource>();
            GameAudio.Spatial(sound);
            sound.loop = true;
            sound.volume = 0f;
            shownLook[seat] = look;
            shownBlower[seat] = null; // the new figure's blower gets its colour and cone on the next frame
        }

        Loadout LookAt(IReadOnlyList<Loadout> looks, int seat) =>
            looks != null && seat < looks.Count ? catalog.Clamp(looks[seat]) : catalog.DefaultLoadout(seat);

        /// <summary>Each player's slice as a coloured wedge on the water, turning with the raft. Small gaps keep the edges readable.</summary>
        void BuildSlices(float radius)
        {
            var count = boat.CrewCount;
            var half = CrewSectors.HalfWidthDeg(count);
            for (var seat = 0; seat < count; seat++)
            {
                var centre = CrewSectors.CentreDeg(seat, count);
                var gap = count > 1 ? 1.5f : 0f;
                // Vertex colours skip the gamma conversion material colours get, so convert for the Linear colour space
                // ourselves; otherwise the slices come out as washed-out pastels.
                var color = PlayerColor(seat).linear;
                var mesh = Shapes3D.Sector(radius + 0.08f, radius + 1.8f, centre - half + gap, centre + half - gap,
                    new Color(color.r, color.g, color.b, 0.6f), new Color(color.r, color.g, color.b, 0.15f));
                // The air material has no opacity cap (foam's is 55%), and the sort order keeps the colour above the
                // white foam ring, which sits at the same distance from the camera.
                var slice = Shapes3D.MeshObject($"Slice {seat}", mesh, transform, new Vector3(0f, 0.03f, 0f), palette.air);
                slice.GetComponent<MeshRenderer>().sortingOrder = 1;
            }
        }

        void LateUpdate()
        {
            var dt = Time.deltaTime;
            transform.SetPositionAndRotation(SimSpace.ToWorld(boat.RenderPosition), SimSpace.Heading(boat.RenderAngleRad));

            var capsized = boat.Capsized;
            capsize = Mathf.MoveTowards(capsize, capsized ? 1f : 0f, dt / (capsized ? 0.35f : 0.25f));

            // Roll (toward port) tips the top toward -X; pitch (bow down) tips it toward +Z.
            var tilt = boat.Tilt * Mathf.Rad2Deg;
            var flipSide = tilt.x >= 0f ? 1f : -1f;
            var bob = 0.04f * Mathf.Sin(Time.time * 2.3f + boat.Index * 1.7f);
            hull.SetLocalPositionAndRotation(
                new Vector3(0f, bob - 0.25f * capsize, 0f),
                Quaternion.Euler(tilt.y * (1f - capsize), 0f, tilt.x * (1f - capsize) + flipSide * 160f * capsize));

            var airAllowed = !capsized && capsize < 0.05f;
            for (var i = 0; i < animals.Length; i++) UpdateCrew(i, boat.CrewAt(i), airAllowed, dt);

            Feel(capsized, dt);
        }

        /// <summary>Bumps, capsizes and the wake: the sounds, splashes and events that go with them.</summary>
        void Feel(bool capsized, float dt)
        {
            var audio = GameAudio.Instance;
            var now = Time.time;
            var velocity = boat.Velocity;

            // A respawn or a restart moves the raft without anything hitting it.
            if (boat.TeleportCount != shownTeleports)
            {
                shownTeleports = boat.TeleportCount;
                recent.Clear();
                quietUntil = now + 0.3f;
            }

            recent.Enqueue((now, velocity));
            while (recent.Count > 1 && now - recent.Peek().time > BumpWindow) recent.Dequeue();
            var change = velocity - recent.Peek().velocity;
            if (change.magnitude > BumpChange && now >= quietUntil && !capsized)
            {
                quietUntil = now + 0.25f; // one bump, not one per frame of it
                recent.Clear();
                var strength = Mathf.Clamp01((change.magnitude - BumpChange) / 3f);
                var hit = SimSpace.ToWorld(boat.RenderPosition - change.normalized * config.hullRadius, 0.2f);
                Splashes.Splash(hit, 0.15f + 0.5f * strength, palette);
                audio?.PlayAt(audio.Bank.Bump, hit, 0.5f + 0.5f * strength, UnityEngine.Random.Range(0.9f, 1.1f));
                Bumped?.Invoke(strength);
            }

            if (capsized && !wasCapsized)
            {
                Splashes.Splash(transform.position, 1f, palette);
                audio?.PlayAt(audio.Bank.Capsize, transform.position, 1f, UnityEngine.Random.Range(0.95f, 1.05f));
                Capsized?.Invoke();
            }
            else if (!capsized && wasCapsized)
            {
                Splashes.Splash(transform.position, 0.35f, palette);
                audio?.PlayAt(audio.Bank.Splash, transform.position, 0.6f);
            }

            wasCapsized = capsized;

            // The wake: foam left behind the stern, more of it the faster the raft goes.
            var speed = velocity.magnitude;
            if (capsized || speed < 0.4f) return;
            wakeDue += dt * Mathf.Min(24f, speed * 8f);
            while (wakeDue >= 1f)
            {
                wakeDue -= 1f;
                var behind = -velocity / speed;
                var side = new Vector2(-behind.y, behind.x) * UnityEngine.Random.Range(-1.2f, 1.2f);
                Splashes.Wake(SimSpace.ToWorld(boat.RenderPosition + behind * config.hullRadius + side), SimSpace.Direction(side * 0.3f), palette);
            }
        }

        void UpdateCrew(int i, ICrewState crew, bool airAllowed, float dt)
        {
            var audio = GameAudio.Instance;
            if (shownBlower[i] != crew.Blower)
            {
                shownBlower[i] = crew.Blower;
                animals[i].SetBlower(crew.Blower);
                puffs[i].Configure(crew.Blower.coneHalfAngleDeg, crew.Blower.range);
                blowerSounds[i].clip = audio != null ? audio.Bank.LoopFor(crew.Blower) : null;
            }

            // The blower's motor follows its spool: quiet and low winding up, loud and high flat out.
            var sound = blowerSounds[i];
            if (sound.clip == null && audio != null) sound.clip = audio.Bank.LoopFor(crew.Blower); // the sounds may still be in the making
            var on = airAllowed && crew.IsBlowing ? crew.Spool : 0f;
            var level = audio != null && sound.clip != null ? on * 0.5f * audio.EffectsVolume : 0f;
            sound.volume = Mathf.MoveTowards(sound.volume, level, dt * 3f);
            sound.pitch = 0.8f + 0.35f * crew.Spool;
            if (sound.volume > 0.001f && !sound.isPlaying && sound.clip != null) sound.Play();
            else if (sound.volume <= 0.001f && sound.isPlaying) sound.Stop();
            animals[i].Alarm(Mathf.InverseLerp(0.55f, 0.95f, boat.TiltDanger));

            animals[i].Pose(SimSpace.Direction(BlowerMath.Direction(crew.AimAngle)), hull.up, crew.Braced, dt);
            puffs[i].SetFlow(airAllowed && crew.IsBlowing ? crew.Spool : 0f);

            // Bursts are counted, so a change means a burst happened, however many frames or snapshots went by.
            if (airAllowed && crew.BurstCount != shownBursts[i])
            {
                puffs[i].Burst();
                quietUntil = Time.time + 0.2f; // a burst's kick isn't a bump
                audio?.PlayAt(audio.Bank.Burst, animals[i].Nozzle.position, 0.85f, crew.Blower.thrustNewtons <= 0f ? 0.8f : UnityEngine.Random.Range(0.95f, 1.1f));
                Burst?.Invoke(i);
            }

            shownBursts[i] = crew.BurstCount;
        }
    }
}
