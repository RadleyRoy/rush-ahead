using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A gang of geese guarding one bank. When a raft comes into their stretch they paddle out after it, swim alongside
    /// and peck: the gang pecks a raft about once a second, each peck taking a tenth of its speed and rocking it. They
    /// hold on to a raft rather than ram it, so they slow and wobble it but never push it across the river, and a fast
    /// raft simply outruns them. They're light loose bodies the host simulates, so a blower's air
    /// shoves them away like a barrel, and they go to joined players with the barrels and logs. They never leave their
    /// stretch: one blown off it, or over the edge, is back at its spot on the bank. Spot: <c>side</c>, <c>size</c> geese.
    /// </summary>
    public sealed class GooseGang : Obstacle, ISpotted
    {
        public const float Radius = 0.38f;
        const float Mass = 5f;
        const float Damping = 2.4f; // paddling flat out, about 6 m/s
        const float Swim = 75f;
        const float HomeSwim = 30f;
        const float Sight = 15f;
        const float Upstream = 8f; // their stretch, either side of their spot
        const float Downstream = 24f;
        const float Peck = 6f; // of the raft's velocity, as an impulse: a tenth of a 60 kg raft's speed
        const float PeckKnock = 10f; // backwards, against the raft's way: a jolt, not a shove across
        const float PeckHeel = 2.5f;
        const double PeckGap = 0.9; // the whole gang, per raft
        const float Hold = 0.6f; // this close to a raft's rim a goose swims with it instead of at it
        const float Lean = 8f; // and leans on it only this hard

        static readonly PhysicsMaterial2D Feathers = new("Goose") { bounciness = 0.3f, friction = 0.1f };

        readonly ObstacleSpot spot;
        readonly RiverSample at;
        readonly Rigidbody2D[] geese;
        readonly Vector2[] homes;
        readonly List<(Boat boat, float distance)> near = new();

        public GooseGang(Course course, Transform parent, ObstacleSpot spot) : base(course)
        {
            this.spot = spot;
            at = River.At(spot.distance);
            var count = spot.size >= 1f ? Mathf.Clamp(Mathf.RoundToInt(spot.size), 2, 5) : 4;
            geese = new Rigidbody2D[count];
            homes = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                var sample = River.At(spot.distance + (i - (count - 1) * 0.5f) * 2.4f);
                homes[i] = sample.Centre + sample.Left * (spot.side * (sample.HalfWidth - 1.1f));
                geese[i] = Goose(homes[i], parent);
            }
        }

        public ObstacleSpot Spot => spot;
        public RiverSample At => at;
        public IReadOnlyList<Rigidbody2D> Geese => geese;
        public override IReadOnlyList<Rigidbody2D> LooseBodies => geese;

        static Rigidbody2D Goose(Vector2 position, Transform parent)
        {
            var go = new GameObject("Goose");
            go.transform.position = position;
            go.transform.SetParent(parent, true);
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.useAutoMass = false;
            body.mass = Mass;
            body.linearDamping = Damping;
            body.angularDamping = 6f;
            body.freezeRotation = true; // the view turns them to face where they swim
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var circle = go.AddComponent<CircleCollider2D>();
            circle.radius = Radius;
            circle.sharedMaterial = Feathers;
            return body;
        }

        public override void ResetLoose()
        {
            scaredUntil = double.NegativeInfinity;
            for (var i = 0; i < geese.Length; i++) Home(i);
        }

        void Home(int i)
        {
            geese[i].position = homes[i];
            geese[i].transform.position = homes[i];
            geese[i].linearVelocity = Vector2.zero;
        }

        double scaredUntil = double.NegativeInfinity;

        /// <summary>An air horn near the gang: they flee for home for a few seconds, chasing and pecking nobody.</summary>
        public override void Scare(Vector2 at, float radius, double seconds)
        {
            foreach (var goose in geese)
                if ((goose.position - at).sqrMagnitude < radius * radius)
                {
                    scaredUntil = seconds + 5.0;
                    return;
                }
        }

        public override void Step(double seconds, float dt, IReadOnlyList<Boat> boats)
        {
            near.Clear();
            var scared = seconds < scaredUntil;
            foreach (var boat in boats)
            {
                if (scared) break;
                if (!Afloat(boat)) continue;
                var distance = River.Project(boat.Body.Position).Distance;
                if (distance > spot.distance - Upstream && distance < spot.distance + Downstream) near.Add((boat, distance));
            }

            for (var i = 0; i < geese.Length; i++)
            {
                var goose = geese[i];
                if (!goose.simulated) continue;
                var position = goose.position;
                var here = River.Project(position);
                if (Mathf.Abs(here.Lateral) > here.HalfWidth + 0.3f || here.Distance < spot.distance - Upstream - 6f || here.Distance > spot.distance + Downstream + 8f)
                {
                    Home(i); // blown away or over the edge: back on the bank
                    continue;
                }

                Boat target = null;
                var best = Sight;
                foreach (var (boat, _) in near)
                {
                    var d = Vector2.Distance(boat.Body.Position, position);
                    if (d >= best) continue;
                    best = d;
                    target = boat;
                }

                if (target == null) goose.AddForce(Vector2.ClampMagnitude(homes[i] - position, 1f) * HomeSwim);
                else
                {
                    var offset = target.Body.Position - position;
                    var toward = Away(position, target.Body.Position, at.Left);
                    // Out of reach: flat out at it. At its rim: keep up with it (what holding its speed against the
                    // water's drag takes) and lean in a little, so a gang of them never pushes it across the river.
                    goose.AddForce(offset.magnitude > RaftRadius + Radius + Hold
                        ? toward * Swim
                        : Vector2.ClampMagnitude(target.Body.Velocity * (Mass * Damping) + toward * Lean, Swim));
                }

                foreach (var (boat, _) in near)
                {
                    var raft = boat.Body.Position;
                    if (Vector2.Distance(raft, position) > RaftRadius + Radius + 0.2f || !Ready(boat, seconds, 0, PeckGap)) continue;
                    var velocity = boat.Body.Velocity;
                    boat.Push(-velocity * Peck);
                    boat.Shove((velocity.sqrMagnitude > 0.25f ? -velocity.normalized : Away(position, raft, at.Left)) * PeckKnock, PeckHeel);
                }
            }
        }

        public override void Zones(double seconds, List<Vector2> lines)
        {
            foreach (var goose in geese) Circle(lines, goose.transform.position, Radius + 0.2f);
        }
    }
}
