using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// Every tunable in the boat simulation. Phase 2 exists to find good values for these; edit the
    /// asset in the Inspector during Play mode (changes persist) or with the in-game tuning panel.
    /// Hull-local axes: +x is the bow (forward), +y is port (left).
    /// </summary>
    [CreateAssetMenu(menuName = "Rush Ahead/Sim Config", fileName = "SimConfig")]
    public sealed class SimConfig : ScriptableObject
    {
        public const int SeatCount = 4;

        [Header("Tick")]
        [Range(30, 120)] public int tickRate = 60;

        [Header("Hull")]
        [Min(1f)] public float mass = 60f;
        [Tooltip("Boats resist spin more than their footprint suggests.")]
        [Min(0.1f)] public float inertiaMultiplier = 1.4f;
        [Tooltip("x = length (bow to stern), y = beam.")]
        public Vector2 hullSize = new(3f, 2f);
        [Min(0f)] public float bowLength = 0.8f;
        [Tooltip("Unity damping scales with mass, so keep this tiny: water drag below does the real work.")]
        [Min(0f)] public float linearDamping = 0.02f;
        [Min(0f)] public float angularDamping = 0.05f;
        [Range(0f, 1f)] public float hullBounciness = 0.2f;

        [Header("Water drag (quadratic, hull-local)")]
        [Min(0f)] public float forwardDrag = 5f;
        [Tooltip("The keel. lateral/forward of about 8 is what makes the boat track instead of sliding.")]
        [Min(0f)] public float lateralDrag = 40f;
        [Min(0f)] public float angularDrag = 18f;

        [Header("Blowers")]
        [Tooltip("Global multiplier on every blower's thrust and burst, for feel tuning.")]
        [Min(0f)] public float thrustMultiplier = 1f;
        [Tooltip("How much of the air's push reaches bodies in the cone (air pushes others less than it pushes you).")]
        [Range(0f, 2f)] public float coneCoupling = 0.5f;

        [Header("Seats (hull-local): fore-left, fore-right, aft-left, aft-right")]
        public Vector2[] seatOffsets =
        {
            new(0.55f, 0.5f), new(0.55f, -0.5f),
            new(-0.85f, 0.5f), new(-0.85f, -0.5f),
        };

        [Header("Tilt: visual lean and capsize (radians)")]
        [Min(0.1f)] public float tiltInertia = 40f;
        [Min(0f)] public float tiltStiffness = 6f;
        [Min(0f)] public float tiltDamping = 1.7f;
        [Tooltip("Height of the blowers above the waterline: horizontal thrust up there heels the boat.")]
        [Min(0f)] public float heelArm = 0.5f;
        [Tooltip("Turning at speed heels the boat outward.")]
        [Min(0f)] public float turnHeelCoupling = 0.15f;
        [Min(0f)] public float impactKickScale = 0.004f;
        [Range(0.1f, 1.5f)] public float capsizeAngle = 0.55f;
        [Min(0f)] public float capsizeHoldSeconds = 0.4f;
        [Min(1f)] public float braceDampingMultiplier = 3f;
        [Min(0f)] public float recoverySeconds = 1.2f;

        public float TickDt => 1f / tickRate;

        /// <summary>Convex hull outline: a rectangular midsection with a triangular bow.</summary>
        public Vector2[] HullOutline()
        {
            var halfLength = hullSize.x * 0.5f;
            var halfBeam = hullSize.y * 0.5f;
            var shoulder = halfLength - bowLength;
            return new[]
            {
                new Vector2(-halfLength, halfBeam),
                new Vector2(shoulder, halfBeam),
                new Vector2(halfLength, 0f),
                new Vector2(shoulder, -halfBeam),
                new Vector2(-halfLength, -halfBeam),
            };
        }
    }
}
