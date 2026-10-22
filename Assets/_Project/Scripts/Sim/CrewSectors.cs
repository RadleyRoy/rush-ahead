using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// Each player owns an equal slice of the directions around the raft, 360/N degrees for N players, fixed to the
    /// raft and centred on their seat. Air can only leave a nozzle inside its owner's slice, so nobody can push the
    /// raft every way alone: the crew has to cooperate.
    /// Angles are hull-local degrees: 0 toward the bow, +90 toward port (left), -90 toward starboard (right).
    /// </summary>
    public static class CrewSectors
    {
        public const int MaxCrew = 4;

        // Slice centres by crew size. Two players split port and starboard. Three get a bow slice (braking) and two
        // rear quarters (driving). Four get the four quadrants. One player owns the whole circle.
        static readonly float[][] Centres =
        {
            new[] { 180f },
            new[] { 90f, -90f },
            new[] { 0f, 120f, -120f },
            new[] { 45f, -45f, 135f, -135f },
        };

        // Seats ordered by how much they help drive the raft forward: the ones whose slice faces astern come first.
        // Local input fills seats in this order, and network players are given seats in it as they join.
        static readonly int[][] DriveOrders =
        {
            new[] { 0 },
            new[] { 0, 1 },
            new[] { 1, 2, 0 },
            new[] { 2, 3, 0, 1 },
        };

        public static float CentreDeg(int seat, int crewCount) => Centres[crewCount - 1][seat];

        public static int[] DriveOrder(int crewCount) => (int[])DriveOrders[crewCount - 1].Clone();

        public static float HalfWidthDeg(int crewCount) => 180f / crewCount;

        /// <summary>
        /// Limits a requested air direction to the seat's slice, turning the nozzle to the nearest edge if needed.
        /// </summary>
        /// <returns>
        /// The share of the requested push that still helps: 1 inside the slice, the cosine of how far the nozzle had
        /// to turn back otherwise, and 0 once that is 90 degrees or more. Air never goes where nobody asked for it.
        /// </returns>
        public static float Clamp(float requestedLocalDeg, int seat, int crewCount, out float allowedLocalDeg)
        {
            var centre = CentreDeg(seat, crewCount);
            var half = HalfWidthDeg(crewCount);
            var offset = Mathf.DeltaAngle(centre, requestedLocalDeg);
            var clamped = Mathf.Clamp(offset, -half, half);

            allowedLocalDeg = centre + clamped;
            return Mathf.Max(0f, Mathf.Cos((offset - clamped) * Mathf.Deg2Rad));
        }

        /// <summary>
        /// Swings a nozzle from <paramref name="currentDeg"/> toward <paramref name="targetDeg"/> by at most
        /// <paramref name="maxStepDeg"/>, never leaving the seat's slice on the way. A plain shortest-arc turn could
        /// cut across another player's slice when going edge to edge; moving through the slice centre can't.
        /// </summary>
        public static float TurnTowards(float currentDeg, float targetDeg, float maxStepDeg, int seat, int crewCount)
        {
            if (crewCount == 1) return Mathf.MoveTowardsAngle(currentDeg, targetDeg, maxStepDeg); // the whole circle is yours

            var centre = CentreDeg(seat, crewCount);
            var from = Mathf.DeltaAngle(centre, currentDeg);
            var to = Mathf.DeltaAngle(centre, targetDeg);
            return centre + Mathf.MoveTowards(from, to, maxStepDeg);
        }
    }
}
