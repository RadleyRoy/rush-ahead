using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// A little stage well away from any venue, where one animal stands on a patch of deck with its blower running:
    /// the Your animal screen's camera looks at it while the player picks a species, a fur and a blower. It looks the
    /// same whatever venue is loaded and whichever seat is being dressed.
    /// </summary>
    public sealed class DressingRoom : MonoBehaviour
    {
        static readonly Vector3 Spot = new(600f, 0f, 600f);
        const float BurstEverySeconds = 1.6f;

        ArtPalette palette;
        CrewCatalog catalog;
        Transform stand;
        AnimalView animal;
        AirPuffs puffs;
        Loadout shown;
        Color shownColour;
        bool built;
        float sinceBurst;

        /// <summary>Where the camera should look (the animal's middle), and the way to stand back from it.</summary>
        public Vector3 Focus => stand.position + Vector3.up * 0.65f;
        public Vector3 Facing => stand.rotation * new Vector3(1f, 0f, 1.3f); // three-quarters on, the blower off to one side

        public static DressingRoom Build(ArtPalette palette, CrewCatalog catalog)
        {
            var root = new GameObject("Dressing room");
            root.transform.position = Spot;
            var room = root.AddComponent<DressingRoom>();
            room.palette = palette;
            room.catalog = catalog;

            var water = Shapes3D.MeshObject("Water", Shapes3D.Disc(14f), root.transform, new Vector3(0f, -0.02f, 0f), palette.water);
            water.transform.localScale = Vector3.one;
            Shapes3D.MeshObject("Tube", Shapes3D.Torus(1.25f, 0.32f), root.transform, new Vector3(0f, 0.2f, 0f), palette.raftTube);
            Shapes3D.Primitive(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.08f, 0f), new Vector3(2.4f, 0.06f, 2.4f), palette.raftFloor, name: "Deck");
            room.stand = Shapes3D.Group(root.transform, "Stand", new Vector3(0f, 0.12f, 0f));
            room.stand.localRotation = Quaternion.Euler(0f, 200f, 0f);
            root.SetActive(false);
            return room;
        }

        /// <summary>Shows this look, wearing a collar in its seat's colour.</summary>
        public void Show(Loadout look, Color seatColour)
        {
            if (built && look.Equals(shown) && seatColour == shownColour) return;
            built = true;
            shown = look;
            shownColour = seatColour;
            if (animal != null) Destroy(animal.gameObject);

            var blower = catalog.BlowerOf(look);
            animal = AnimalView.Build(stand, catalog.SpeciesOf(look).shape, catalog.FurOf(look), seatColour, blower, palette);
            animal.transform.localScale = Vector3.one * 1.15f;
            puffs = AirPuffs.Attach(animal.Nozzle, palette.air);
            puffs.Configure(blower.coneHalfAngleDeg, blower.range);
            puffs.SetFlow(blower.thrustNewtons > 0f ? 0.8f : 0f);
            sinceBurst = BurstEverySeconds; // show a burst straight away
        }

        void Update()
        {
            if (animal == null) return;
            animal.Pose(stand.rotation * new Vector3(1f, 0f, 0.35f), Vector3.up, false, Time.unscaledDeltaTime);
            var blower = catalog.BlowerOf(shown);
            if (!blower.CanBurst) return;
            sinceBurst += Time.unscaledDeltaTime;
            if (sinceBurst < BurstEverySeconds) return;
            sinceBurst = 0f;
            puffs.Burst();
        }
    }
}
