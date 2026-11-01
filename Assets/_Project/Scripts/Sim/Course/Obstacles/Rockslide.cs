using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A rocky outcrop on one side of the river. Every period a boulder teeters at its top (the warning), rolls down,
    /// thunders across the river and drops off the far edge. It's solid while it crosses, and a raft it hits is
    /// knocked hard and heeled over; bracing takes some of it, but timing (or luck) is the real answer.
    /// </summary>
    public sealed class Rockslide : Obstacle
    {
        // The timetable, in seconds before and after the boulder reaches the water (at the middle of the period).
        const float TeeterFor = 0.9f; // rocking at the top: the warning
        const float RollFor = 0.4f; // down the outcrop
        const float CrossFor = 0.8f; // across the river
        const float FallFor = 0.6f; // over the far edge and gone
        const float Shove = 150f;
        const float Heel = 6f;

        readonly RockslideSpot spot;
        readonly RiverSample at;
        readonly Rigidbody2D body;
        Vector2 last;

        public Rockslide(Course course, Transform parent, RockslideSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            body = Kinematic("Rockslide boulder", at.Centre + at.Left * (spot.side * (at.HalfWidth + 4.5f)), parent); // up on the outcrop
            var circle = body.gameObject.AddComponent<CircleCollider2D>();
            circle.radius = spot.radius;
            circle.isTrigger = true; // at this speed a solid hit would fling a raft off the course; the knock below is what it does
            body.simulated = false;
        }

        public RockslideSpot Spot => spot;
        public RiverSample At => at;

        /// <summary>
        /// The boulder: where it is on the water plane, how high above it (on the outcrop or falling past the edge), how
        /// far it has rolled (for spinning it), whether it's crossing the river, and how much it's rocking (0 to 1).
        /// </summary>
        public (Vector2 position, float height, float rolled, bool crossing, float teeter) Boulder(double seconds)
        {
            // Each stage as a share of the period, around the boulder reaching the water at the middle of it.
            var phase = Phase(seconds, spot.period, spot.offset);
            const float landing = 0.5f;
            var rolling = landing - RollFor / spot.period;
            var teetering = rolling - TeeterFor / spot.period;
            var gone = landing + CrossFor / spot.period;
            var fallen = gone + FallFor / spot.period;
            var top = spot.side * (at.HalfWidth + 4.5f);
            var lip = spot.side * (at.HalfWidth + 0.3f);
            var far = -spot.side * (at.HalfWidth + 2.5f);
            float across, height;
            if (phase < rolling)
            {
                across = top;
                height = 3f;
            }
            else if (phase < landing)
            {
                var t = (phase - rolling) / (landing - rolling);
                across = Mathf.Lerp(top, lip, t * t);
                height = Mathf.Lerp(3f, 0f, t * t);
            }
            else if (phase < gone)
            {
                across = Mathf.Lerp(lip, far, (phase - landing) / (gone - landing));
                height = 0f;
            }
            else
            {
                var t = Mathf.Clamp01((phase - gone) / (fallen - gone));
                across = far - spot.side * t * 3f;
                height = phase < fallen ? -40f * t * t : -100f; // gone over the edge
            }

            var teeter = phase >= teetering && phase < rolling ? (phase - teetering) / (rolling - teetering) : 0f;
            var rolled = Mathf.Abs(across - top);
            return (at.Centre + at.Left * across, height, rolled, phase >= landing && phase < gone, teeter);
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            var (position, _, _, crossing, _) = Boulder(seconds);
            if (crossing && !body.simulated)
            {
                // Switched on where it is, not where the body last was (which could be right on top of a raft).
                body.simulated = true;
                body.position = position;
                body.transform.position = position;
                last = position;
            }

            body.simulated = crossing;
            if (!crossing) return;
            body.MovePosition(position);

            // It moves a fair way each tick, so check the whole stretch it covered, not just where it ended up.
            var path = position - last;
            foreach (var boat in boats)
            {
                if (!Afloat(boat)) continue;
                var raft = boat.Body.Position;
                var along = path.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(raft - last, path) / path.sqrMagnitude) : 1f;
                var offset = raft - (last + path * along);
                if (offset.magnitude > spot.radius + RaftRadius + 0.2f || !Ready(boat, seconds)) continue;
                var away = offset.sqrMagnitude > 1e-4f ? offset.normalized : -spot.side * at.Left;
                boat.Shove((away - spot.side * at.Left).normalized * Shove, Heel, spin: 1f);
            }

            last = position;
        }

        public override bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            var here = River.Project(position);
            if (Mathf.Abs(here.Distance - spot.distance) > spot.radius + RaftRadius + 1f) return false;
            for (var t = 0f; t <= lookahead; t += 0.1f)
            {
                var (boulder, _, _, crossing, _) = Boulder(seconds + t);
                if (crossing && Vector2.Distance(boulder, position) < spot.radius + RaftRadius + 1f) return true;
            }

            return false;
        }
    }
}
