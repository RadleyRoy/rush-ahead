using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// The boat's lean, as a 2-DOF spring-damper running outside the 2D physics engine.
    /// x is roll (positive heels toward port, +y), y is pitch (positive dips the bow).
    /// Faking buoyancy this way is cheap and deterministic, and it produces the constant rocking
    /// that sells "this boat is barely under control". Capsize is leaning past the threshold for long enough.
    /// </summary>
    public sealed class TiltModel
    {
        float secondsOverThreshold;

        public Vector2 Tilt { get; private set; }
        public Vector2 TiltVelocity { get; private set; }
        public bool Capsized { get; private set; }

        public void Reset()
        {
            Tilt = TiltVelocity = Vector2.zero;
            secondsOverThreshold = 0f;
            Capsized = false;
        }

        /// <summary>An instantaneous change in tilt velocity, from an impact or a burst.</summary>
        public void Kick(Vector2 deltaVelocity) => TiltVelocity += deltaVelocity;

        /// <returns>True on the tick the boat capsizes.</returns>
        public bool Step(Vector2 forcingAccel, float stiffness, float damping, float capsizeAngle, float holdSeconds, float dt)
        {
            if (Capsized) return false;

            var accel = forcingAccel - stiffness * Tilt - damping * TiltVelocity;
            TiltVelocity += accel * dt; // semi-implicit Euler: stable for a stiff spring at 60 Hz
            Tilt += TiltVelocity * dt;

            secondsOverThreshold = Tilt.magnitude > capsizeAngle ? secondsOverThreshold + dt : 0f;
            if (secondsOverThreshold < holdSeconds) return false;

            Capsized = true;
            return true;
        }
    }
}
