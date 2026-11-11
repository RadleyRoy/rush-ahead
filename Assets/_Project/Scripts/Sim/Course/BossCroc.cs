using System.Collections.Generic;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>What Old Snapjaw is doing, for its look and sounds.</summary>
    public enum BossMood : byte
    {
        Hidden = 0,
        /// <summary>Surfacing with a roar.</summary>
        Rising = 1,
        Chasing = 2,
        /// <summary>Its eyes glow and a red ring shows where it's about to surge: the tell.</summary>
        Lunging = 3,
        /// <summary>Surging at the raft ahead, jaws open.</summary>
        Surging = 4,
        Diving = 5,
    }

    /// <summary>
    /// Old Snapjaw, the boss course's giant crocodile. It rises upstream with a roar soon after GO and chases the last
    /// raft still racing: it closes the gap fast from far behind, but near a raft it's a little slower than a crew
    /// pushing well, so a good crew keeps ahead and a slow one is caught. Six to ten metres behind, it lunges (a tell,
    /// then a surge). Its jaws on a raft knock it over, costing a life (a shield bubble pops instead); then it dives and
    /// comes up again well behind the raft. An air horn near it sends it diving too. Host-authoritative: it reacts to
    /// the rafts, so its pose and mood go out in the snapshots. It has no body: the scripted bite does the work.
    /// </summary>
    public sealed class BossCroc
    {
        public const float Length = 7.5f; // about three ordinary crocodiles
        public const float JawReach = 3.4f; // from its middle to the end of its snout
        public const float BiteRadius = 2.2f;
        public const float TellSeconds = 0.8f;
        public const float SurgeSeconds = 0.7f;

        /// <summary>How close it must be for an air horn to send it diving, past the horn's own reach.</summary>
        public const float HornReach = Length * 0.5f;

        const float RiseAfterGo = 1.5f;
        const float RiseSeconds = 1.1f;
        const float DiveSeconds = 2.8f;
        const float FarSpeed = 9f; // metres per second over the water's, far behind
        const float NearSpeed = 3.2f; // and near: slower than a crew blowing steadily (3 to 6 over the water), so it's the ones that slow down it catches
        const float NearGap = 16f; // from its middle to the raft's, where it's down to its near speed
        const float FarGap = 28f; // and where it's at its far speed
        const float SurgeSpeed = 9f; // on top, while it surges
        const float LungeFrom = 8f; // metres from its jaws to the raft
        const float LungeTo = 5.5f;
        const float LungeCooldown = 2.5f;
        const float SideSpeed = 3.2f;
        const float FirstBack = 32f; // how far behind the last raft it first rises
        const float Back = 30f; // and after a bite or a horn
        const float RaftRadius = 1.6f;

        readonly Course course;
        float distance;
        float lateral;
        float moodSeconds;
        float cooldown;
        bool risen; // has it come up yet this race
        float back = FirstBack;

        public BossCroc(Course course) => this.course = course;

        public BossMood Mood { get; private set; }
        public Vector2 Position { get; private set; }
        public float AngleRad { get; private set; }

        /// <summary>How many rafts it has bitten this race.</summary>
        public int Chomps { get; private set; }

        public bool Present => Mood != BossMood.Hidden;

        /// <summary>How far down the river it is.</summary>
        public float Distance => distance;

        /// <summary>The raft it's after, if any.</summary>
        public Boat Target { get; private set; }

        public void Reset()
        {
            Mood = BossMood.Hidden;
            moodSeconds = 0f;
            cooldown = 0f;
            risen = false;
            back = FirstBack;
            Chomps = 0;
            Target = null;
        }

        static bool Racing(Boat boat) => !boat.Out && boat.FinishTick <= 0 && !boat.Moored;

        public void Step(double seconds, float dt, IReadOnlyList<Boat> boats, double raceStart)
        {
            if (double.IsNaN(raceStart) || seconds < raceStart + RiseAfterGo)
            {
                Reset();
                return;
            }

            // The hindmost raft still racing.
            Target = null;
            var targetAt = float.MaxValue;
            var targetLateral = 0f;
            foreach (var boat in boats)
            {
                if (!Racing(boat)) continue;
                var at = course.River.Project(boat.Body.Position);
                if (at.Distance >= targetAt) continue;
                Target = boat;
                targetAt = at.Distance;
                targetLateral = at.Lateral;
            }

            moodSeconds += dt;
            cooldown = Mathf.Max(0f, cooldown - dt);
            if (Target == null)
            {
                if (Mood != BossMood.Hidden && Mood != BossMood.Diving) Dive();
                if (Mood == BossMood.Diving && moodSeconds > DiveSeconds) Mood = BossMood.Hidden;
                return;
            }

            switch (Mood)
            {
                case BossMood.Hidden:
                    // Down below, waiting (at the start, or after a dive) until the raft it's after is back on the water.
                    // It comes up a fixed way behind, so after a respawn near the top it waits for the raft to get clear.
                    if (risen && moodSeconds < DiveSeconds) return;
                    if (Target.State != BoatState.Afloat || targetAt - back < 2f) return;
                    distance = targetAt - back;
                    lateral = Mathf.Clamp(targetLateral, -HalfRoom(distance), HalfRoom(distance));
                    risen = true;
                    SetMood(BossMood.Rising);
                    break;
                case BossMood.Rising:
                    if (moodSeconds >= RiseSeconds) SetMood(BossMood.Chasing);
                    break;
                case BossMood.Chasing:
                    // Its raft went back to a checkpoint behind it: down it goes, to come up behind again.
                    if (targetAt < distance - 4f)
                    {
                        Dive();
                        break;
                    }

                    Swim(dt, targetAt, targetLateral, 0f);
                    var gap = targetAt - distance - JawReach;
                    if (cooldown <= 0f && gap < LungeFrom && gap > LungeTo - 3f && Biteable(Target)) SetMood(BossMood.Lunging);
                    break;
                case BossMood.Lunging:
                    Swim(dt, targetAt, targetLateral, 0f, false);
                    if (moodSeconds >= TellSeconds) SetMood(BossMood.Surging);
                    break;
                case BossMood.Surging:
                    Swim(dt, targetAt, targetLateral, SurgeSpeed, false);
                    if (Bite(boats)) break;
                    if (moodSeconds >= SurgeSeconds)
                    {
                        cooldown = LungeCooldown;
                        SetMood(BossMood.Chasing);
                    }

                    break;
                case BossMood.Diving:
                    if (moodSeconds >= DiveSeconds * 0.4f) SetMood(BossMood.Hidden);
                    break;
            }

            Place();
        }

        float HalfRoom(float at) => Mathf.Max(0f, course.River.At(at).HalfWidth - 1.6f);

        void SetMood(BossMood mood)
        {
            Mood = mood;
            moodSeconds = 0f;
        }

        void Dive()
        {
            back = Back;
            SetMood(BossMood.Diving);
        }

        /// <summary>
        /// Down the river after the raft: fast from far behind, not quite a good crew's pace up close. Once it lunges it
        /// keeps to its line (the red ring shows where), so a raft can still slip out of the way to one side.
        /// </summary>
        void Swim(float dt, float targetAt, float targetLateral, float extra, bool steer = true)
        {
            var at = course.River.At(distance);
            var water = Vector2.Dot(course.WaterVelocityAt(at.Centre + at.Left * lateral), at.Tangent);
            var gap = targetAt - distance;
            var over = Mathf.Lerp(NearSpeed, FarSpeed, Mathf.Clamp01((gap - NearGap) / (FarGap - NearGap)));
            var speed = Mathf.Max(0f, water) + over + extra;
            if (gap < JawReach) speed = Mathf.Min(speed, Mathf.Max(0f, water)); // never past the raft
            distance = Mathf.Min(distance + speed * dt, course.River.Length - 2f);
            var room = HalfRoom(distance);
            if (steer) lateral = Mathf.MoveTowards(lateral, Mathf.Clamp(targetLateral, -room, room), SideSpeed * dt);
            else lateral = Mathf.Clamp(lateral, -room, room);
            if (course.Islands.OnIsland(distance, lateral, 1.5f)) lateral = course.Islands.OffIsland(distance, lateral, course.River.At(distance).HalfWidth);
        }

        void Place()
        {
            if (Mood == BossMood.Hidden) return;
            var at = course.River.At(distance);
            Position = at.Centre + at.Left * lateral;
            AngleRad = Mathf.Atan2(at.Tangent.y, at.Tangent.x);
        }

        /// <summary>Its jaws on any raft still racing: over it goes (or its bubble pops), and Snapjaw dives.</summary>
        bool Bite(IReadOnlyList<Boat> boats)
        {
            var jaws = Position + new Vector2(Mathf.Cos(AngleRad), Mathf.Sin(AngleRad)) * JawReach;
            foreach (var boat in boats)
            {
                if (!Racing(boat) || !Biteable(boat)) continue;
                if ((boat.Body.Position - jaws).sqrMagnitude > (BiteRadius + RaftRadius) * (BiteRadius + RaftRadius)) continue;
                boat.KnockOver();
                Chomps++;
                Dive();
                return true;
            }

            return false;
        }

        /// <summary>On the water, and not just back from a capsize or a fall (a raft gets a moment's grace then).</summary>
        static bool Biteable(Boat boat) => boat.State == BoatState.Afloat && boat.CapsizeImmunitySeconds <= 0f;

        /// <summary>An air horn: close enough, and down it goes, to come up again well behind.</summary>
        public void Scare(Vector2 at, float radius)
        {
            if (Mood is BossMood.Hidden or BossMood.Diving) return;
            if ((Position - at).sqrMagnitude > (radius + HornReach) * (radius + HornReach)) return;
            Dive();
        }

        /// <summary>For AI crews: whether it's lunging at (or surging on) a raft here, close enough to bite soon.</summary>
        public bool LungingAt(Vector2 position) =>
            Mood is BossMood.Lunging or BossMood.Surging && (Position - position).sqrMagnitude < (JawReach + LungeFrom + 4f) * (JawReach + LungeFrom + 4f);

        /// <summary>For AI crews deciding on the horn: whether it's up and close behind a raft here.</summary>
        public bool Near(Vector2 position, float reach) =>
            Mood is BossMood.Chasing or BossMood.Lunging or BossMood.Surging && (Position - position).sqrMagnitude < reach * reach;
    }
}
