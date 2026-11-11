using System.Collections.Generic;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>What kind of race the room is playing.</summary>
    public enum GameMode : byte
    {
        /// <summary>One raft, 1 to 4 animals, against the clock.</summary>
        Coop = 0,

        /// <summary>Two rafts, Red against Blue: first over the line wins.</summary>
        TeamRace = 1,

        /// <summary>Three or four rafts: the last raft through each knockout gate is out, and the river gets faster.</summary>
        Elimination = 2,
    }

    /// <summary>
    /// The rules of Elimination, without any engine state so they can be tested. With R rafts there are R - 1 knockout
    /// gates: checkpoints spread down the course, the last being the finish line. Only R - k rafts may go through the
    /// k-th; once that many have, every raft still racing behind them is knocked out. A raft that runs out of lives is
    /// out too, so a gate with nobody left to knock out does nothing. The last raft left, or the first over the line,
    /// wins. After every raft that goes out the river runs faster.
    /// </summary>
    public static class KnockoutRule
    {
        public const int MinRafts = 3;
        public const int MaxRafts = 4;

        /// <summary>How much faster the river runs for each raft that's out.</summary>
        public const float FlowPerKnockout = 0.12f;

        public static float FlowBoost(int rafts, int outs) => 1f + FlowPerKnockout * Mathf.Clamp(outs, 0, Mathf.Max(0, rafts - 1));

        /// <summary>
        /// The knockout gates as checkpoint counts (a raft has gone through gate g once it has passed g checkpoints; the
        /// finish is the last, <paramref name="gates"/>), in order: strictly increasing when the course has a checkpoint for each.
        /// </summary>
        public static int[] Gates(int rafts, int gates)
        {
            var knockouts = Mathf.Max(0, rafts - 1);
            var result = new int[knockouts];
            if (knockouts == 0 || gates <= 0) return result;
            for (var k = 1; k <= knockouts; k++)
            {
                var g = k == knockouts ? gates : Mathf.RoundToInt(k * gates / (float)knockouts);
                var lowest = k == 1 ? 1 : result[k - 2] + 1;
                var highest = gates - (knockouts - k);
                result[k - 1] = Mathf.Min(gates, Mathf.Clamp(g, lowest, Mathf.Max(lowest, highest))); // too few checkpoints: several at the finish
            }

            return result;
        }

        /// <summary>
        /// Which rafts are knocked out now. <paramref name="passed"/> is how many checkpoints each raft has passed (the
        /// finish counts), <paramref name="outs"/> which are out already. Knocked-out rafts are added to
        /// <paramref name="knocked"/> (each once), later gates seeing the earlier gates' knockouts.
        /// </summary>
        public static void Decide(IReadOnlyList<int> passed, IReadOnlyList<bool> outs, int gates, List<int> knocked)
        {
            knocked.Clear();
            var rafts = passed.Count;
            if (rafts < 2) return;
            var knockoutGates = Gates(rafts, gates);
            for (var k = 1; k <= knockoutGates.Length; k++)
            {
                var gate = knockoutGates[k - 1];
                var allowed = rafts - k;
                int alive = 0, through = 0;
                for (var i = 0; i < rafts; i++)
                {
                    if (outs[i] || knocked.Contains(i)) continue;
                    alive++;
                    if (passed[i] >= gate) through++;
                }

                if (alive <= allowed || through < allowed) continue;
                for (var i = 0; i < rafts; i++)
                    if (!outs[i] && !knocked.Contains(i) && passed[i] < gate) knocked.Add(i);
            }
        }

        /// <summary>
        /// The winner: the first over the line (the lower raft on a tie), or else the only raft left; -1 while it's
        /// still open, or if every raft is out.
        /// </summary>
        public static int Winner(IReadOnlyList<int> finishTicks, IReadOnlyList<bool> outs)
        {
            var best = -1;
            for (var i = 0; i < finishTicks.Count; i++)
                if (finishTicks[i] > 0 && !outs[i] && (best < 0 || finishTicks[i] < finishTicks[best])) best = i;
            if (best >= 0) return best;

            var alive = -1;
            for (var i = 0; i < outs.Count; i++)
            {
                if (outs[i]) continue;
                if (alive >= 0) return -1;
                alive = i;
            }

            return alive;
        }

        /// <summary>
        /// Places, best first: the finishers by time, then the rafts still going (furthest first), then the rafts that
        /// went out, the last to go out first.
        /// </summary>
        public static int[] Places(IReadOnlyList<int> finishTicks, IReadOnlyList<int> outTicks, IReadOnlyList<float> progress)
        {
            var order = new List<int>();
            for (var i = 0; i < finishTicks.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                var groupA = Group(a);
                var groupB = Group(b);
                if (groupA != groupB) return groupA.CompareTo(groupB);
                var byGroup = groupA switch
                {
                    0 => finishTicks[a].CompareTo(finishTicks[b]),
                    1 => progress[b].CompareTo(progress[a]),
                    _ => outTicks[b] != outTicks[a] ? outTicks[b].CompareTo(outTicks[a]) : progress[b].CompareTo(progress[a]),
                };
                return byGroup != 0 ? byGroup : a.CompareTo(b);
            });
            return order.ToArray();

            int Group(int i) => outTicks[i] > 0 ? 2 : finishTicks[i] > 0 ? 0 : 1;
        }
    }
}
