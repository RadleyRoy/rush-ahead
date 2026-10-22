using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// The simulation runs in a 2D plane; visuals live in ordinary Y-up 3D space with the water surface at y = 0.
    /// Sim (x, y) maps to world (x, height, y). Seen from above, both keep x to the right, so a sim direction
    /// becomes the world direction at the same angle on the water.
    /// </summary>
    public static class SimSpace
    {
        public static Vector3 ToWorld(Vector2 sim, float height = 0f) => new(sim.x, height, sim.y);

        public static Vector3 Direction(Vector2 sim) => new(sim.x, 0f, sim.y);

        /// <summary>Rotation whose forward (+Z) points along a sim heading.</summary>
        public static Quaternion Heading(float angleRad) =>
            Quaternion.LookRotation(new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad)));

        /// <summary>
        /// A hull-local sim offset (x toward the bow, y toward port) inside a model built facing +Z with port at -X.
        /// </summary>
        public static Vector3 HullLocal(Vector2 local, float height = 0f) => new(-local.y, height, local.x);
    }
}
