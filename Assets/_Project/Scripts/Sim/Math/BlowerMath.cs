using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    public static class BlowerMath
    {
        public static Vector2 Direction(float angleRad) => new(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

        /// <summary>
        /// Force on the boat from one blower. Air leaves along <paramref name="airDirection"/> and pushes
        /// the boat the opposite way, except a vacuum, which pulls the boat toward where it points.
        /// </summary>
        public static Vector2 ReactionForce(Vector2 airDirection, float thrust, float spool, BlowerSpecial special)
        {
            var sign = special == BlowerSpecial.Vacuum ? 1f : -1f;
            return sign * thrust * spool * airDirection;
        }

        /// <summary>Which way air pushes bodies in the cone: along the airflow, or back toward the nozzle for a vacuum.</summary>
        public static float ConeSign(BlowerSpecial special) => special == BlowerSpecial.Vacuum ? -1f : 1f;

        /// <summary>First-order lag toward <paramref name="target"/>, with separate spool-up and spool-down time constants.</summary>
        public static float Spool(float current, float target, float dt, float upSeconds, float downSeconds)
        {
            var tau = Mathf.Max(target > current ? upSeconds : downSeconds, 1e-4f);
            return current + (target - current) * (1f - Mathf.Exp(-dt / tau));
        }

        /// <summary>0..1 strength of the air cone at a point offset <paramref name="toTarget"/> from the nozzle; 0 outside the cone or range.</summary>
        public static float ConeWeight(Vector2 airDirection, Vector2 toTarget, float halfAngleDeg, float range, float falloffExp)
        {
            var distance = toTarget.magnitude;
            if (distance >= range) return 0f;
            if (distance < 1e-4f) return 1f;

            var cos = Vector2.Dot(airDirection, toTarget / distance);
            if (cos < Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad)) return 0f;

            return Mathf.Pow(1f - distance / range, falloffExp) * cos * cos;
        }

        /// <summary>Torque about the centre of mass from a force applied at offset <paramref name="r"/>: the 2D cross product r × F.</summary>
        public static float Torque(Vector2 r, Vector2 force) => r.x * force.y - r.y * force.x;
    }
}
