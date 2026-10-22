using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// Quadratic water drag in hull-local space. Using a far higher lateral than forward coefficient
    /// (a keel) is what makes the boat track along its heading instead of sliding like a puck.
    /// </summary>
    public static class WaterDrag
    {
        public static Vector2 LocalForce(Vector2 localVelocity, float forwardCoeff, float lateralCoeff) =>
            new(-forwardCoeff * localVelocity.x * Mathf.Abs(localVelocity.x),
                -lateralCoeff * localVelocity.y * Mathf.Abs(localVelocity.y));

        public static float Torque(float angularVelocityRad, float angularCoeff) =>
            -angularCoeff * angularVelocityRad * Mathf.Abs(angularVelocityRad);
    }
}
