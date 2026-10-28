using UnityEngine;

namespace RushAhead.Content
{
    public enum BlowerSpecial
    {
        None,

        /// <summary>Reaction force inverts: pulls the boat toward where it points, and pulls loose bodies in.</summary>
        Vacuum,
    }

    /// <summary>
    /// A hand-held air blower. Every field except <see cref="color"/> maps onto a term in the simulation.
    /// Steady blowing never runs out; the air tank only feeds bursts. No burst is <c>burstImpulse = 0</c>; burst-only is
    /// <c>thrustNewtons = 0</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rush Ahead/Blower", fileName = "Blower")]
    public sealed class BlowerDef : ScriptableObject
    {
        public string displayName = "Leaf Blower";

        [Tooltip("One line for the pick screen: what makes this blower different.")]
        public string blurb = "";

        [Tooltip("Body colour of the blower model; with the name and blurb, the only cosmetic fields here.")]
        public Color color = new(1f, 0.55f, 0.15f);

        [Header("Thrust")]
        [Min(0f)] public float thrustNewtons = 45f;
        [Min(1f)] public float spoolUpMs = 260f;
        [Min(1f)] public float spoolDownMs = 180f;

        [Header("Air cone (pushes other bodies)")]
        [Range(1f, 60f)] public float coneHalfAngleDeg = 18f;
        [Min(0.1f)] public float range = 4.5f;
        [Min(0.1f)] public float falloffExp = 1.6f;

        [Header("Air tank (bursts only: steady blowing is free)")]
        [Min(0f)] public float airCapacity = 100f;
        [Min(0f)] public float rechargePerSec = 18f;
        [Tooltip("How long after a burst the tank starts refilling.")]
        [Min(0f)] public float rechargeDelayMs = 450f;

        [Header("Burst")]
        [Min(0f)] public float burstImpulse = 60f;
        [Min(0f)] public float burstCost = 25f;

        public BlowerSpecial special = BlowerSpecial.None;

        public bool CanBurst => burstImpulse > 0f;
    }
}
