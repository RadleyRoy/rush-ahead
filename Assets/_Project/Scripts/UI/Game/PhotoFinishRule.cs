namespace RushAhead.UI
{
    /// <summary>
    /// When a finish is close enough for the slow-motion replay: the second raft over the line (a Team Race) or the
    /// runner-up still on its way when the winner crossed (an Elimination final) within <see cref="Within"/> seconds.
    /// </summary>
    public static class PhotoFinishRule
    {
        /// <summary>Seconds; more only for screenshots (<c>-rushahead-photo-any</c>).</summary>
        public static float Within { get; set; } = 0.6f;

        /// <summary>From two finishing ticks: the margin in seconds, or null if it wasn't close.</summary>
        public static float? Margin(int firstTick, int secondTick, int tickRate)
        {
            if (firstTick <= 0 || secondTick <= 0 || tickRate <= 0) return null;
            var margin = (secondTick - firstTick) / (float)tickRate;
            return margin >= 0f && margin <= Within ? margin : null;
        }

        /// <summary>From how far short of the line the runner-up was, going how fast down the river, as the winner crossed.</summary>
        public static float? Margin(float metresShort, float speed)
        {
            if (speed < 0.5f) return null;
            var margin = UnityEngine.Mathf.Max(0f, metresShort) / speed;
            return margin <= Within ? margin : null;
        }
    }
}
