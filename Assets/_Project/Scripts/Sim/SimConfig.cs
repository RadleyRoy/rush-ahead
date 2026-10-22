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
        [Header("Tick")]
        [Range(30, 120)] public int tickRate = 60;

        [Header("Hull")]
        [Min(1f)] public float mass = 60f;
        [Tooltip("Boats resist spin more than their footprint suggests.")]
        [Min(0.1f)] public float inertiaMultiplier = 1.4f;
        [Tooltip("Outer radius of the round inflatable raft.")]
        [Min(0.5f)] public float hullRadius = 1.6f;
        [Range(8, 32)] public int hullSegments = 16;
        [Tooltip("Unity damping scales with mass, so keep this tiny: water drag below does the real work.")]
        [Min(0f)] public float linearDamping = 0.02f;
        [Min(0f)] public float angularDamping = 0.05f;
        [Range(0f, 1f)] public float hullBounciness = 0.2f;

        [Header("Water drag (quadratic, hull-local)")]
        [Min(0f)] public float forwardDrag = 5f;
        [Tooltip("The keel. lateral/forward of about 8 makes the raft track along the way the crew faces instead of " +
                 "sliding; set it equal to forward drag for a free-spinning, puck-like ring.")]
        [Min(0f)] public float lateralDrag = 40f;
        [Min(0f)] public float angularDrag = 18f;

        [Header("Blowers")]
        [Tooltip("How fast an animal can swing its blower round, in degrees per second. Turns stick flicks and " +
                 "8-way keyboard steps into a quick sweep, so the thrust changes direction smoothly.")]
        [Range(90f, 1440f)] public float nozzleTurnDegPerSec = 420f;
        [Tooltip("Global multiplier on every blower's thrust and burst, for feel tuning.")]
        [Min(0f)] public float thrustMultiplier = 1f;
        [Tooltip("How much of the air's push reaches bodies in the cone (air pushes others less than it pushes you).")]
        [Range(0f, 2f)] public float coneCoupling = 0.5f;

        [Header("Seats")]
        [Tooltip("Distance from the raft's centre to each animal. Each sits in the middle of its air slice (CrewSectors).")]
        [Min(0f)] public float seatRadius = 0.99f;

        [Header("Tilt: visual lean and capsize (radians)")]
        [Min(0.1f)] public float tiltInertia = 40f;
        [Tooltip("Roll stiffness: how hard the boat rights itself from a sideways lean.")]
        [Min(0f)] public float tiltStiffness = 6f;
        [Tooltip("Pitch stiffness as a multiple of roll stiffness. A long hull is far harder to tip end over end, " +
                 "so forward thrust shouldn't lean the boat anywhere near capsizing.")]
        [Min(1f)] public float pitchStiffnessMultiplier = 3f;
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

        /// <summary>Where a seat's animal sits (hull-local), for a crew of <paramref name="crewCount"/>.</summary>
        public Vector2 SeatOffset(int seat, int crewCount)
        {
            var angle = CrewSectors.CentreDeg(seat, crewCount) * Mathf.Deg2Rad;
            return seatRadius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        /// <summary>The round raft's outline as a regular polygon, counter-clockwise from the bow.</summary>
        public Vector2[] HullOutline()
        {
            var points = new Vector2[hullSegments];
            for (var i = 0; i < hullSegments; i++)
            {
                var angle = 2f * Mathf.PI * i / hullSegments;
                points[i] = hullRadius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }

            return points;
        }
    }
}
