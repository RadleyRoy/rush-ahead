using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// The only place a boat touches <see cref="Rigidbody2D"/>. Keeping every physics call behind this
    /// wrapper means a later move to LowLevelPhysics2D (Box2D v3) is a contained change.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(PolygonCollider2D))]
    public sealed class BoatPhysics : MonoBehaviour
    {
        static readonly List<Collider2D> OverlapResults = new();
        readonly List<ContactPoint2D> contacts = new();

        Rigidbody2D body;
        float hullInertiaPerMass;
        float appliedMass = -1f, appliedInertiaMultiplier = -1f, appliedLinearDamping = -1f, appliedAngularDamping = -1f;
        Vector2 pendingImpactImpulse;

        public Vector2 Position => body.position;
        public float AngleRad => body.rotation * Mathf.Deg2Rad;
        public Vector2 Velocity => body.linearVelocity;
        public float AngularVelocityRad => body.angularVelocity * Mathf.Deg2Rad;
        public float Mass => body.mass;
        public float Inertia => body.inertia;

        public static BoatPhysics Create(SimConfig config, Vector2 position, float angleRad, string name)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angleRad * Mathf.Rad2Deg));

            var body = go.AddComponent<Rigidbody2D>();
            var outline = config.HullOutline();
            var hull = go.AddComponent<PolygonCollider2D>();
            hull.SetPath(0, outline);
            hull.sharedMaterial = new PhysicsMaterial2D("Hull") { bounciness = config.hullBounciness, friction = 0.3f };

            var physics = go.AddComponent<BoatPhysics>();
            physics.body = body;
            physics.hullInertiaPerMass = PolygonInertiaPerMass(outline);
            body.gravityScale = 0f;
            body.useAutoMass = false;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            physics.ApplyConfig(config);
            return physics;
        }

        /// <summary>Pushes changed tunables into the rigidbody. Cheap when nothing changed, so it runs every tick.</summary>
        public void ApplyConfig(SimConfig config)
        {
            if (!Mathf.Approximately(appliedMass, config.mass) || !Mathf.Approximately(appliedInertiaMultiplier, config.inertiaMultiplier))
            {
                body.mass = config.mass;
                body.centerOfMass = Vector2.zero; // seat offsets and torques are all measured from the hull origin
                body.inertia = hullInertiaPerMass * config.mass * config.inertiaMultiplier;
                appliedMass = config.mass;
                appliedInertiaMultiplier = config.inertiaMultiplier;
            }

            if (!Mathf.Approximately(appliedLinearDamping, config.linearDamping))
                body.linearDamping = appliedLinearDamping = config.linearDamping;
            if (!Mathf.Approximately(appliedAngularDamping, config.angularDamping))
                body.angularDamping = appliedAngularDamping = config.angularDamping;
        }

        public Vector2 WorldPoint(Vector2 localPoint) => body.GetRelativePoint(localPoint);
        public Vector2 LocalVector(Vector2 worldVector) => body.GetVector(worldVector);
        public Vector2 WorldVector(Vector2 localVector) => body.GetRelativeVector(localVector);

        public void AddForce(Vector2 worldForce) => body.AddForce(worldForce);
        public void AddTorque(float torque) => body.AddTorque(torque);

        public void AddForceAtLocalPoint(Vector2 worldForce, Vector2 localPoint, bool impulse = false) =>
            body.AddForceAtPosition(worldForce, WorldPoint(localPoint), impulse ? ForceMode2D.Impulse : ForceMode2D.Force);

        public void Stop()
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        public void Teleport(Vector2 position, float angleRad)
        {
            Stop();
            body.position = position;
            body.rotation = angleRad * Mathf.Rad2Deg;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angleRad * Mathf.Rad2Deg));
        }

        /// <summary>Sum of collision impulses received since the last call.</summary>
        public Vector2 ConsumeImpactImpulse()
        {
            var impulse = pendingImpactImpulse;
            pendingImpactImpulse = Vector2.zero;
            return impulse;
        }

        /// <summary>
        /// Applies an air cone's push to every other dynamic body inside it.
        /// <paramref name="sign"/> is +1 to push along the airflow, -1 to pull back toward the nozzle (vacuum).
        /// </summary>
        public int PushBodiesInCone(Vector2 nozzle, Vector2 airDirection, BlowerDef spec, float magnitude, bool impulse, float sign)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            Physics2D.OverlapCircle(nozzle, spec.range, filter, OverlapResults);

            var pushed = 0;
            foreach (var collider in OverlapResults)
            {
                var other = collider.attachedRigidbody;
                if (other == null || other == body || other.bodyType != RigidbodyType2D.Dynamic) continue;

                var point = collider.ClosestPoint(nozzle);
                var weight = BlowerMath.ConeWeight(airDirection, point - nozzle, spec.coneHalfAngleDeg, spec.range, spec.falloffExp);
                if (weight <= 0f) continue;

                other.AddForceAtPosition(sign * magnitude * weight * airDirection, point, impulse ? ForceMode2D.Impulse : ForceMode2D.Force);
                pushed++;
            }

            return pushed;
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            collision.GetContacts(contacts);
            foreach (var contact in contacts)
                pendingImpactImpulse += contact.normal * contact.normalImpulse;
        }

        /// <summary>Moment of inertia per unit mass of a uniform polygon about the origin.</summary>
        internal static float PolygonInertiaPerMass(IReadOnlyList<Vector2> points)
        {
            float twiceArea = 0f, sum = 0f;
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                var cross = a.x * b.y - a.y * b.x;
                twiceArea += cross;
                sum += cross * (Vector2.Dot(a, a) + Vector2.Dot(a, b) + Vector2.Dot(b, b));
            }

            // I = (density / 12) * sum and mass = density * area = density * twiceArea / 2,
            // so I / mass = sum / (6 * twiceArea).
            return sum / (6f * twiceArea);
        }
    }
}
