using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// The last few seconds of one raft as it was drawn (where it was, its lean, each animal's aim, spool and brace), a
    /// pose every thirtieth of a second, kept on each device for the photo finish. Nothing is sent: every device records
    /// what it showed.
    /// </summary>
    public sealed class RaftRecording
    {
        public const float Rate = 30f;
        public const float Seconds = 4f;

        /// <summary>One moment of the raft, on the race clock.</summary>
        public sealed class Pose
        {
            public double Clock;
            public Vector2 Position;
            public float Angle;
            public Vector2 Velocity;
            public Vector2 Tilt;
            public float Danger;
            public bool Capsized, Fallen, Out, Turbo, Shielded;
            public float[] Aim, Spool, Air;
            public bool[] Blowing, Braced;
        }

        readonly Pose[] ring = new Pose[Mathf.CeilToInt(Rate * Seconds) + 2];
        int next;
        int count;

        public int Count => count;

        /// <summary>The i-th pose kept, oldest first.</summary>
        public Pose this[int i] => ring[(next - count + i + ring.Length * 2) % ring.Length];

        public Pose Newest => count > 0 ? this[count - 1] : null;

        public void Clear() => count = 0;

        /// <summary>Keeps how the raft looks now, at <paramref name="clock"/> on the race clock (a pose a thirtieth of a second at most).</summary>
        public void Record(IBoatState boat, double clock)
        {
            if (count > 0 && clock < Newest.Clock) Clear(); // the clock went back: a new race
            if (count > 0 && clock - Newest.Clock < 1.0 / Rate - 1e-4) return;
            var pose = ring[next] ??= new Pose();
            var crew = boat.CrewCount;
            if (pose.Aim == null || pose.Aim.Length != crew)
            {
                pose.Aim = new float[crew];
                pose.Spool = new float[crew];
                pose.Air = new float[crew];
                pose.Blowing = new bool[crew];
                pose.Braced = new bool[crew];
            }

            pose.Clock = clock;
            pose.Position = boat.RenderPosition;
            pose.Angle = boat.RenderAngleRad;
            pose.Velocity = boat.Velocity;
            pose.Tilt = boat.Tilt;
            pose.Danger = boat.TiltDanger;
            pose.Capsized = boat.Capsized;
            pose.Fallen = boat.Fallen;
            pose.Out = boat.Out;
            pose.Turbo = boat.Turbo;
            pose.Shielded = boat.Shielded;
            for (var seat = 0; seat < crew; seat++)
            {
                var c = boat.CrewAt(seat);
                pose.Aim[seat] = c.AimAngle;
                pose.Spool[seat] = c.Spool;
                pose.Air[seat] = c.AirFraction;
                pose.Blowing[seat] = c.IsBlowing;
                pose.Braced[seat] = c.Braced;
            }

            next = (next + 1) % ring.Length;
            count = Mathf.Min(count + 1, ring.Length);
        }

        /// <summary>The two poses either side of <paramref name="clock"/> and how far between them it falls.</summary>
        public (Pose a, Pose b, float t) At(double clock)
        {
            if (count == 0) return (null, null, 0f);
            if (clock <= this[0].Clock) return (this[0], this[0], 0f);
            for (var i = 1; i < count; i++)
            {
                var b = this[i];
                if (b.Clock < clock) continue;
                var a = this[i - 1];
                var span = b.Clock - a.Clock;
                return (a, b, span > 1e-6 ? (float)((clock - a.Clock) / span) : 1f);
            }

            return (Newest, Newest, 0f);
        }

        /// <summary>Where the raft was at <paramref name="clock"/>.</summary>
        public Vector2 PositionAt(double clock)
        {
            var (a, b, t) = At(clock);
            return a == null ? Vector2.zero : Vector2.Lerp(a.Position, b.Position, t);
        }
    }

    /// <summary>
    /// A raft played back from its <see cref="RaftRecording"/> for the photo finish, standing in for the live raft
    /// under its view. Its running counts (bursts, capsizes, horns) are the live raft's, so nothing replays as new.
    /// </summary>
    public sealed class ReplayBoat : IBoatState
    {
        readonly IBoatState live;
        readonly RaftRecording recording;
        readonly ReplayCrew[] crew;
        RaftRecording.Pose a, b;
        float t;

        public ReplayBoat(IBoatState live, RaftRecording recording)
        {
            this.live = live;
            this.recording = recording;
            crew = new ReplayCrew[live.CrewCount];
            for (var i = 0; i < crew.Length; i++) crew[i] = new ReplayCrew(this, live.CrewAt(i), i);
        }

        /// <summary>Shows the raft as it was at <paramref name="clock"/> on the race clock.</summary>
        public void Show(double clock) => (a, b, t) = recording.At(clock);

        bool Has => a != null;
        public int Index => live.Index;
        public Vector2 RenderPosition => Has ? Vector2.Lerp(a.Position, b.Position, t) : live.RenderPosition;
        public float RenderAngleRad => Has ? a.Angle + Mathf.DeltaAngle(a.Angle * Mathf.Rad2Deg, b.Angle * Mathf.Rad2Deg) * Mathf.Deg2Rad * t : live.RenderAngleRad;
        public Vector2 Velocity => Has ? Vector2.Lerp(a.Velocity, b.Velocity, t) : live.Velocity;
        public Vector2 Tilt => Has ? Vector2.Lerp(a.Tilt, b.Tilt, t) : live.Tilt;
        public bool Capsized => Has ? a.Capsized : live.Capsized;
        public int CapsizeCount => live.CapsizeCount;

        /// <summary>Not the live raft's: the view takes the switch to the replay (and back) as a cut, not a bump.</summary>
        public int TeleportCount => live.TeleportCount + 1000003;

        public int GatesPassed => live.GatesPassed;
        public int FinishTick => live.FinishTick;
        public bool Fallen => Has ? a.Fallen : live.Fallen;
        public int Lives => live.Lives;
        public bool Out => Has ? a.Out : live.Out;
        public float TiltDanger => Has ? Mathf.Lerp(a.Danger, b.Danger, t) : live.TiltDanger;
        public PowerUp Item => live.Item;
        public bool Turbo => Has ? a.Turbo : live.Turbo;
        public bool Shielded => Has ? a.Shielded : live.Shielded;
        public int ShieldPops => live.ShieldPops;
        public int HornCount => live.HornCount;
        public int Pickups => live.Pickups;
        public int CrewCount => crew.Length;
        public ICrewState CrewAt(int seat) => crew[seat];

        sealed class ReplayCrew : ICrewState
        {
            readonly ReplayBoat boat;
            readonly ICrewState live;
            readonly int seat;

            public ReplayCrew(ReplayBoat boat, ICrewState live, int seat)
            {
                this.boat = boat;
                this.live = live;
                this.seat = seat;
            }

            bool Has => boat.a != null && seat < boat.a.Aim.Length && seat < boat.b.Aim.Length;
            public int Seat => live.Seat;
            public BlowerDef Blower => live.Blower;
            public float AimAngle => Has ? boat.a.Aim[seat] + Mathf.DeltaAngle(boat.a.Aim[seat] * Mathf.Rad2Deg, boat.b.Aim[seat] * Mathf.Rad2Deg) * Mathf.Deg2Rad * boat.t : live.AimAngle;
            public float Spool => Has ? Mathf.Lerp(boat.a.Spool[seat], boat.b.Spool[seat], boat.t) : live.Spool;
            public bool IsBlowing => Has ? boat.a.Blowing[seat] : live.IsBlowing;
            public float AirFraction => Has ? Mathf.Lerp(boat.a.Air[seat], boat.b.Air[seat], boat.t) : live.AirFraction;
            public bool Braced => Has ? boat.a.Braced[seat] : live.Braced;
            public int BurstCount => live.BurstCount;
        }
    }
}
