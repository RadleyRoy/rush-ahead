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

        /// <summary>Puts a loose body back at rest at <paramref name="position"/>.</summary>
        public static void Reset(GameObject go, Vector2 position)
        {
            var body = go.GetComponent<Rigidbody2D>();
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = position;
            go.transform.position = position;
        }
    }
}
