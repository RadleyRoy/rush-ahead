using UnityEngine;

namespace RushAhead.Content
{
    /// <summary>Which body the animal builder makes. Phase 8 swaps these primitive figures for real models.</summary>
    public enum AnimalShape
    {
        Bear,
        Cat,
        Bunny,
        Pig,
        Otter,
        Raccoon,
    }

    /// <summary>
    /// An animal players can pick. Purely cosmetic for now: in a family party game, picking the cute one must never
    /// mean losing. If species ever get stats, cap them at ±15%.
    /// </summary>
    [CreateAssetMenu(menuName = "Rush Ahead/Species", fileName = "Species")]
    public sealed class SpeciesDef : ScriptableObject
    {
        public string displayName = "Bear";
        public AnimalShape shape;

        [Tooltip("The fur a new player of this species starts with: an index into the catalog's fur colours.")]
        [Min(0)] public int defaultFur;
    }
}
