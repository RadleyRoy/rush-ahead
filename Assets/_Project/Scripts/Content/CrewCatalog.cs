using UnityEngine;

namespace RushAhead.Content
{
    /// <summary>
    /// Everything a player can pick for their animal. Loadouts refer to entries by index, so every device running the
    /// same build agrees on what an index means; the host clamps whatever a client asks for against its own catalog.
    /// </summary>
    [CreateAssetMenu(menuName = "Rush Ahead/Crew Catalog", fileName = "CrewCatalog")]
    public sealed class CrewCatalog : ScriptableObject
    {
        public SpeciesDef[] species;

        [Tooltip("The first is everyone's default.")]
        public BlowerDef[] blowers;

        [Tooltip("Curated fur swatches. A fixed palette rather than a free colour wheel keeps every animal readable on the raft and the water.")]
        public Color[] furColors;

        /// <summary>
        /// What a seat wears before anyone chooses: a different animal per slot, in its own fur, with the first blower.
        /// </summary>
        public Loadout DefaultLoadout(int slot)
        {
            var species = slot % this.species.Length;
            return new Loadout
            {
                Species = (byte)species,
                Fur = (byte)Mathf.Clamp(this.species[species].defaultFur, 0, furColors.Length - 1),
                Blower = 0,
                Name = "",
            };
        }

        public Loadout Clamp(Loadout loadout) => loadout.ClampTo(species.Length, furColors.Length, blowers.Length);

        public SpeciesDef SpeciesOf(Loadout loadout) => species[loadout.Species < species.Length ? loadout.Species : 0];
        public Color FurOf(Loadout loadout) => furColors[loadout.Fur < furColors.Length ? loadout.Fur : 0];
        public BlowerDef BlowerOf(Loadout loadout) => blowers[loadout.Blower < blowers.Length ? loadout.Blower : 0];

        public int IndexOf(BlowerDef blower) => System.Array.IndexOf(blowers, blower);
    }
}
