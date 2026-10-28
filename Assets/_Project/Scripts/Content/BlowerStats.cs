using UnityEngine;

namespace RushAhead.Content
{
    /// <summary>
    /// A blower's character as four bars from 0 to 1, for choosing one: power, how quickly it winds up, how far its air
    /// reaches, and how hard it bursts. Each is measured against a fixed design maximum, not against the other blowers,
    /// so adding a blower never rescales the rest.
    /// </summary>
    public readonly struct BlowerStats
    {
        public const float MaxThrust = 50f;
        public const float SlowestSpoolMs = 500f;
        public const float MaxRange = 7f;
        public const float MaxBurst = 120f;

        public readonly float Power;
        public readonly float QuickStart;
        public readonly float Reach;
        public readonly float Burst;

        public BlowerStats(BlowerDef blower)
        {
            Power = Mathf.Clamp01(blower.thrustNewtons / MaxThrust);
            QuickStart = blower.thrustNewtons > 0f ? Mathf.Clamp01(1f - blower.spoolUpMs / SlowestSpoolMs) : 0f;
            Reach = Mathf.Clamp01(blower.range / MaxRange);
            Burst = blower.CanBurst ? Mathf.Clamp01(blower.burstImpulse / MaxBurst) : 0f;
        }

        /// <summary>What sets it apart in a word or two, or null.</summary>
        public static string Tag(BlowerDef blower) =>
            blower.special == BlowerSpecial.Vacuum ? "Pulls instead"
            : blower.thrustNewtons <= 0f ? "Bursts only"
            : !blower.CanBurst ? "No burst"
            : null;
    }
}
