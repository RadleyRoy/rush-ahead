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
        static readonly Color[] FurColors =
        {
            new(0.62f, 0.42f, 0.28f), new(0.98f, 0.6f, 0.25f), new(0.96f, 0.94f, 0.9f), new(1f, 0.66f, 0.72f),
        };

        /// <summary>Distinct per player, unlike fur: used for their slice on the water and their collar.</summary>
        static readonly Color[] PlayerColors =
        {
            new(1f, 0.38f, 0.3f), new(0.3f, 0.55f, 1f), new(1f, 0.84f, 0.2f), new(0.4f, 0.9f, 0.4f),
        };

        static readonly AnimalLook[] SeatLooks = { AnimalLook.Bear, AnimalLook.Cat, AnimalLook.Bunny, AnimalLook.Pig };
        const float FloorHeight = 0.16f;

        public static AnimalLook LookFor(int seat) => SeatLooks[seat % SeatLooks.Length];
        public static Color PlayerColor(int seat) => PlayerColors[seat % PlayerColors.Length];

        IBoatState boat;
        ArtPalette palette;
        Transform hull;
        AnimalView[] animals;
        AirPuffs[] puffs;
        BlowerDef[] shownBlower;
        int[] shownBursts;
        float capsize;

        /// <param name="boat">The live boat on the host, or a raft rebuilt from snapshots on a client.</param>
        public static RaftView Build(IBoatState boat, SimConfig config, ArtPalette palette)
        {
            var view = new GameObject($"Raft {boat.Index}").AddComponent<RaftView>();
            view.boat = boat;
            view.palette = palette;
            view.BuildRaft(config.hullRadius);
            view.BuildSlices(config.hullRadius);
            view.BuildCrew(config);
            view.LateUpdate();
            return view;
        }

        void BuildRaft(float radius)
        {
            Shapes3D.MeshObject("Foam", Shapes3D.Disc(radius + 0.55f), transform, new Vector3(0f, 0.02f, 0f), palette.foam);

            hull = Shapes3D.Group(transform, "Hull");
            const float tube = 0.24f;
            Shapes3D.MeshObject("Band", Shapes3D.Torus(radius - tube, tube), hull, new Vector3(0f, 0.06f, 0f), palette.raftBand);
            Shapes3D.MeshObject("Tube", Shapes3D.Torus(radius - tube - 0.02f, tube * 0.95f), hull, new Vector3(0f, 0.36f, 0f), palette.raftTube);
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

        void BuildCrew(SimConfig config)
        {
            var count = boat.CrewCount;
            animals = new AnimalView[count];
            puffs = new AirPuffs[count];
            shownBlower = new BlowerDef[count];
            shownBursts = new int[count];

            for (var i = 0; i < count; i++)
            {
                var crew = boat.CrewAt(i);
                var animal = animals[i] = AnimalView.Build(hull, LookFor(i), FurColors[i % FurColors.Length], PlayerColor(i), crew.Blower, palette);
                animal.transform.localPosition = SimSpace.HullLocal(config.SeatOffset(i, count) * 0.85f, FloorHeight);
                puffs[i] = AirPuffs.Attach(animal.Nozzle, palette.air);
                shownBursts[i] = crew.BurstCount;
            }
        }

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
        }

        void UpdateCrew(int i, ICrewState crew, bool airAllowed, float dt)
        {
            if (shownBlower[i] != crew.Blower)
            {
                shownBlower[i] = crew.Blower;
                animals[i].SetBlower(crew.Blower);
                puffs[i].Configure(crew.Blower.coneHalfAngleDeg, crew.Blower.range);
            }

            animals[i].Pose(SimSpace.Direction(BlowerMath.Direction(crew.AimAngle)), hull.up, crew.Braced, dt);
            puffs[i].SetFlow(airAllowed && crew.IsBlowing ? crew.Spool : 0f);

            // Bursts are counted, so a change means a burst happened, however many frames or snapshots went by.
            if (airAllowed && crew.BurstCount != shownBursts[i]) puffs[i].Burst();
            shownBursts[i] = crew.BurstCount;
        }
    }
}
