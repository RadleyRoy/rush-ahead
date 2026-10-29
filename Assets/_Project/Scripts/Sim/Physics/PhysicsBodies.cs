using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>Static and loose bodies for courses: banks, rocks and floating barrels the air cone can push.</summary>
    public static class PhysicsBodies
    {
        public static GameObject CreateWallLoop(Vector2[] corners, string name = "Walls")
        {
            var go = new GameObject(name);
            var edge = go.AddComponent<EdgeCollider2D>();
            var points = new Vector2[corners.Length + 1];
            corners.CopyTo(points, 0);
            points[corners.Length] = corners[0];
            edge.points = points;
            edge.edgeRadius = 0.1f;
            return go;
        }

        public static GameObject CreateRock(Vector2 position, float radius, string name = "Rock")
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.AddComponent<CircleCollider2D>().radius = radius;
            return go;
        }

        public static GameObject CreateBarrel(Vector2 position, float radius, float mass, string name = "Barrel")
        {
            var go = new GameObject(name);
            go.transform.position = position;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.useAutoMass = false;
            body.mass = mass;
            body.linearDamping = 1.2f; // floating in water
            body.angularDamping = 2f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var circle = go.AddComponent<CircleCollider2D>();
            circle.radius = radius;
            circle.sharedMaterial = new PhysicsMaterial2D("Barrel") { bounciness = 0.3f, friction = 0.2f };
            return go;
        }

        /// <summary>A log lying on the water: long, heavy and slow to turn, so it takes a real shove to move.</summary>
        public static GameObject CreateLog(Vector2 position, float angleRad, float length, float radius, float mass, string name = "Log")
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angleRad * Mathf.Rad2Deg));

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.useAutoMass = false;
            body.mass = mass;
            body.linearDamping = 1.0f; // floating in water
            body.angularDamping = 2.5f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var capsule = go.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Horizontal;
            capsule.size = new Vector2(length, 2f * radius);
            capsule.sharedMaterial = new PhysicsMaterial2D("Log") { bounciness = 0.15f, friction = 0.4f };
            return go;
        }

        /// <summary>
        /// Takes a loose body out of the local simulation, for a network client that only shows where the host says it is.
        /// </summary>
        public static void Freeze(GameObject go, bool frozen = true) => go.GetComponent<Rigidbody2D>().simulated = !frozen;

        /// <summary>Places a frozen body for display. Not a physics move: frozen bodies don't collide.</summary>
        public static void ShowAt(GameObject go, Vector2 position, float angleRad) =>
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angleRad * Mathf.Rad2Deg));

        /// <summary>Puts a loose body back at rest at <paramref name="position"/>.</summary>
        public static void Reset(GameObject go, Vector2 position)
        {
            var body = go.GetComponent<Rigidbody2D>();
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = position;
            go.transform.position = position;
        }

        /// <summary>Puts a loose body back at rest, turned to <paramref name="angleRad"/>.</summary>
        public static void Reset(GameObject go, Vector2 position, float angleRad)
        {
            Reset(go, position);
            var body = go.GetComponent<Rigidbody2D>();
            body.rotation = angleRad * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angleRad * Mathf.Rad2Deg);
        }
    }
}
