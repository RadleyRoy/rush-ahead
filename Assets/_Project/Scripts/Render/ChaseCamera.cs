using System;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// Third-person chase camera: trails behind the raft, looking a little ahead of it, and slowly swings round
    /// to face the way the raft is travelling. It never follows the raft's own spin, which would be nauseating on
    /// a round boat that turns constantly. <see cref="Forward"/> is what stick input is relative to.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ChaseCamera : MonoBehaviour
    {
        public Func<Vector2> TargetPosition;
        public Func<Vector2> TargetVelocity;

        public float Distance = 10f;
        public float PitchDeg = 30f;
        public float LookAhead = 2f;
        public float LookHeight = 0.6f;
        public float FieldOfView = 55f;

        [Tooltip("How quickly the camera swings round to the direction of travel, per second, at full speed.")]
        public float TurnRate = 0.9f;
        [Tooltip("Below this speed the camera holds its heading, so drifting and spinning don't swing it about.")]
        public float MinTurnSpeed = 1f;
        public float FollowRate = 5f;
        [Tooltip("How quickly the travel direction the camera steers by catches up with the raft's real velocity. " +
                 "Lower filters out the wobble from bumps and spins; sticks are camera-relative, so this smooths the controls too.")]
        public float HeadingSmoothing = 2.5f;

        float yawRad;
        Vector3 focus;
        Vector2 smoothedVelocity;

        /// <summary>The camera's heading on the water, in sim space. Stick-up pushes this way.</summary>
        public Vector2 Forward => new(Mathf.Cos(yawRad), Mathf.Sin(yawRad));

        void Awake()
        {
            var view = GetComponent<Camera>();
            view.orthographic = false;
            view.fieldOfView = FieldOfView;
            view.nearClipPlane = 0.3f;
            view.farClipPlane = 400f;
        }

        float trauma;

        /// <summary>
        /// Shakes the view for a bump or a capsize: 0.2 is a nudge, 1 a big crash. Shakes add up and die away over about
        /// a second, and the shake grows with the square of what's left, so small knocks stay small.
        /// </summary>
        public void Shake(float amount) => trauma = Mathf.Min(1f, trauma + amount);

        public void SnapTo(Vector2 position, float headingRad)
        {
            yawRad = headingRad;
            smoothedVelocity = Vector2.zero;
            focus = Focus(position);
            Place();
        }

        void LateUpdate()
        {
            if (TargetPosition == null) return;
            var dt = Time.deltaTime;

            var raw = TargetVelocity?.Invoke() ?? Vector2.zero;
            smoothedVelocity = Vector2.Lerp(smoothedVelocity, raw, 1f - Mathf.Exp(-HeadingSmoothing * dt));
            var velocity = smoothedVelocity;
            var speed = velocity.magnitude;
            if (speed > MinTurnSpeed)
            {
                var strength = Mathf.Clamp01((speed - MinTurnSpeed) / 3f);
                var target = Mathf.Atan2(velocity.y, velocity.x);
                yawRad = Mathf.LerpAngle(yawRad * Mathf.Rad2Deg, target * Mathf.Rad2Deg, 1f - Mathf.Exp(-TurnRate * strength * dt)) * Mathf.Deg2Rad;
            }

            focus = Vector3.Lerp(focus, Focus(TargetPosition()), 1f - Mathf.Exp(-FollowRate * dt));
            Place();

            if (trauma <= 0f) return;
            trauma = Mathf.Max(0f, trauma - dt * 1.4f);
            var shake = trauma * trauma;
            var t = Time.time * 24f;
            transform.position += transform.right * ((Mathf.PerlinNoise(t, 0f) - 0.5f) * 0.7f * shake) +
                                  transform.up * ((Mathf.PerlinNoise(0f, t) - 0.5f) * 0.5f * shake);
            transform.rotation *= Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(t, t) - 0.5f) * 6f * shake);
        }

        Vector3 Focus(Vector2 position) => SimSpace.ToWorld(position + Forward * LookAhead, LookHeight);

        void Place()
        {
            var pitch = PitchDeg * Mathf.Deg2Rad;
            var back = -SimSpace.Direction(Forward) * (Mathf.Cos(pitch) * Distance);
            transform.position = focus + back + Vector3.up * (Mathf.Sin(pitch) * Distance);
            transform.rotation = Quaternion.LookRotation(focus - transform.position);
        }
    }
}
