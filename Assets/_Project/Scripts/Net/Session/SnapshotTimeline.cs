using System;
using System.Collections.Generic;

namespace RushAhead.Net
{
    /// <summary>
    /// Buffers snapshots and plays them back a little behind the newest one, so there is nearly always a pair to blend
    /// between even with jitter and loss. Playback runs slightly fast or slow to hold that distance, instead of jumping.
    /// </summary>
    public sealed class SnapshotTimeline
    {
        const int Capacity = 32;
        const double ResyncTicks = 30; // further off than this (a stall, a host hiccup): jump rather than catch up

        readonly List<Snapshot> buffer = new(); // ascending by tick
        readonly Stack<Snapshot> pool = new();
        double playback;
        bool started;

        /// <param name="snapshotEveryTicks">How often the host sends; the delay is two send intervals plus a little margin.</param>
        public SnapshotTimeline(float tickRate, int snapshotEveryTicks)
        {
            TickRate = tickRate;
            DelayTicks = 2 * snapshotEveryTicks + 2;
        }

        public float TickRate { get; }
        public float DelayTicks { get; set; }
        public double PlaybackTick => playback;
        public int Count => buffer.Count;
        public uint NewestTick => buffer.Count > 0 ? buffer[^1].Tick : 0;

        /// <summary>True once playback has reached the buffered snapshots, so there is something to interpolate rather than hold.</summary>
        public bool Ready => buffer.Count > 0 && playback >= buffer[0].Tick;

        /// <summary>A snapshot to read into; give it back through <see cref="Add"/>.</summary>
        public Snapshot Rent() => pool.Count > 0 ? pool.Pop() : new Snapshot();

        public void Add(Snapshot snapshot)
        {
            var index = buffer.Count;
            while (index > 0 && buffer[index - 1].Tick > snapshot.Tick) index--;

            var duplicate = index > 0 && buffer[index - 1].Tick == snapshot.Tick;
            var tooOld = buffer.Count >= Capacity && index == 0;
            if (duplicate || tooOld)
            {
                pool.Push(snapshot);
                return;
            }

            buffer.Insert(index, snapshot);
            while (buffer.Count > Capacity)
            {
                pool.Push(buffer[0]);
                buffer.RemoveAt(0);
            }
        }

        public void Advance(float deltaSeconds)
        {
            if (buffer.Count == 0) return;

            var target = buffer[^1].Tick - DelayTicks;
            if (!started || Math.Abs(target - playback) > ResyncTicks)
            {
                playback = target;
                started = true;
                return;
            }

            var behind = target - playback; // positive: we've fallen behind and should speed up
            var rate = 1.0 + Math.Clamp(behind * 0.05, -0.2, 0.2);
            playback += deltaSeconds * TickRate * rate;
        }

        /// <summary>
        /// The snapshots either side of playback and how far between them it is. Past the newest snapshot, both are the
        /// newest and <paramref name="overshootSeconds"/> says how far past.
        /// </summary>
        public bool Sample(out Snapshot from, out Snapshot to, out float t, out float overshootSeconds)
        {
            from = to = null;
            t = overshootSeconds = 0f;
            if (buffer.Count == 0) return false;

            if (playback <= buffer[0].Tick)
            {
                from = to = buffer[0];
                return true;
            }

            for (var i = 0; i < buffer.Count - 1; i++)
            {
                if (playback < buffer[i].Tick || playback >= buffer[i + 1].Tick) continue;
                from = buffer[i];
                to = buffer[i + 1];
                t = (float)((playback - from.Tick) / (to.Tick - from.Tick));
                return true;
            }

            from = to = buffer[^1];
            overshootSeconds = (float)((playback - from.Tick) / TickRate);
            return true;
        }
    }
}
