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

        /// <summary>How far below the water the raft is (falling off the edge): the camera stays up top and looks down after it.</summary>
        public Func<float> TargetDrop;

        /// <summary>
        /// Looking round, -1 to 1 each way (a drag or a stick): sideways swings the camera up to half a turn round the raft,
        /// up and down tilts it a little. Let go and it eases back behind, unless <see cref="FreeLook"/> holds it there.
        /// </summary>
        public Func<Vector2> LookInput;

        /// <summary>While true (watching another raft) the view stays wherever it was swung to.</summary>
        public Func<bool> FreeLook;

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
        float lookYaw; // radians added to the heading for the view only, never for the sticks
        float lookPitch; // degrees added to the pitch
        float lookYawFrom, lookPitchFrom;
        bool looking;
        float lookDown;
        float rush; // 0 idling to 1 flat out down fast water: the view widens and drops closer
        float kick; // a boost lane's surge: the view lunges wider for a moment
        Camera view;
        Vector3 focus;
        Vector2 smoothedVelocity;

        /// <summary>The camera's heading on the water, in sim space. Stick-up pushes this way.</summary>
        public Vector2 Forward => new(Mathf.Cos(yawRad), Mathf.Sin(yawRad));

        /// <summary>Where the view looks, which differs from <see cref="Forward"/> while the player looks round.</summary>
        public Vector2 ViewForward => new(Mathf.Cos(yawRad + lookYaw), Mathf.Sin(yawRad + lookYaw));

        /// <summary>How far round the view is swung, in degrees (0 is straight behind the raft).</summary>
        public float LookDegrees => lookYaw * Mathf.Rad2Deg;

        /// <summary>How fast it feels, 0 idling to 1 flat out (the view widens, drops and looks further ahead).</summary>
        public float Rush => rush;

        /// <summary>What's left of a boost's kick, 0 to 1.</summary>
        public float Kicking => kick;

        /// <summary>A surge of speed (a boost lane): the view lunges wider and settles back over a second or so.</summary>
        public void Kick(float amount) => kick = Mathf.Min(1f, kick + amount);

        void Awake()
        {
            view = GetComponent<Camera>();
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

        void OnDisable()
        {
            rush = 0f;
            if (view != null) view.fieldOfView = FieldOfView; // the menu cameras share it
        }

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
            rush = Mathf.Lerp(rush, Mathf.InverseLerp(4f, 12f, speed), 1f - Mathf.Exp(-3f * dt)); // calm-water cruise is 8-10 m/s, flat out 12-14
            kick = Mathf.Max(0f, kick - dt * 1.6f);
            view.fieldOfView = FieldOfView + 22f * rush + 10f * kick * kick;
            if (speed > MinTurnSpeed)
            {
                var strength = Mathf.Clamp01((speed - MinTurnSpeed) / 3f);
                var target = Mathf.Atan2(velocity.y, velocity.x);
                yawRad = Mathf.LerpAngle(yawRad * Mathf.Rad2Deg, target * Mathf.Rad2Deg, 1f - Mathf.Exp(-TurnRate * strength * dt)) * Mathf.Deg2Rad;
            }

            Look(dt);
            focus = Vector3.Lerp(focus, Focus(TargetPosition()), 1f - Mathf.Exp(-FollowRate * dt));
            lookDown = Mathf.Lerp(lookDown, Mathf.Min(8f, (TargetDrop?.Invoke() ?? 0f) * 0.6f), 1f - Mathf.Exp(-6f * dt));
            Place();

            // Flat out, the view trembles a little with the water.
            var buzz = Mathf.Clamp01((rush - 0.6f) / 0.4f) * 0.05f;
            if (buzz > 0f)
            {
                var b = Time.time * 31f;
                transform.position += transform.right * ((Mathf.PerlinNoise(b, 3f) - 0.5f) * buzz) + transform.up * ((Mathf.PerlinNoise(3f, b) - 0.5f) * buzz);
            }

            if (trauma <= 0f) return;
            trauma = Mathf.Max(0f, trauma - dt * 1.4f);
            var shake = trauma * trauma;
            var t = Time.time * 24f;
            transform.position += transform.right * ((Mathf.PerlinNoise(t, 0f) - 0.5f) * 0.7f * shake) +
                                  transform.up * ((Mathf.PerlinNoise(0f, t) - 0.5f) * 0.5f * shake);
            transform.rotation *= Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(t, t) - 0.5f) * 6f * shake);
        }

        /// <summary>Swings the view by the look input: a drag carries on from wherever the view was when it began.</summary>
        void Look(float dt)
        {
            var input = LookInput?.Invoke() ?? Vector2.zero;
            var active = input.sqrMagnitude > 0.0004f;
            var free = FreeLook?.Invoke() ?? false;
            if (active && !looking)
            {
                lookYawFrom = lookYaw;
                lookPitchFrom = lookPitch;
            }

            looking = active;
            if (active)
            {
                var yaw = lookYawFrom - input.x * Mathf.PI; // drag right, look right
                if (!free) yaw = Mathf.Clamp(yaw, -Mathf.PI, Mathf.PI);
                var pitch = Mathf.Clamp(lookPitchFrom + input.y * 25f, -18f, 30f);
                var follow = 1f - Mathf.Exp(-14f * dt);
                lookYaw = Mathf.Lerp(lookYaw, yaw, follow);
                lookPitch = Mathf.Lerp(lookPitch, pitch, follow);
            }
            else if (!free)
            {
                var back = 1f - Mathf.Exp(-6f * dt); // let go: back behind the raft in about half a second
                lookYaw = Mathf.Lerp(Mathf.DeltaAngle(0f, lookYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad, 0f, back);
                lookPitch = Mathf.Lerp(lookPitch, 0f, back);
            }
        }

        // See further ahead at speed; looking round, keep the raft in the picture.
        Vector3 Focus(Vector2 position) =>
            SimSpace.ToWorld(position + ViewForward * ((LookAhead + 4f * rush) * Mathf.Lerp(1f, 0.25f, Mathf.Abs(Mathf.DeltaAngle(0f, LookDegrees)) / 180f)), LookHeight);

        void Place()
        {
            var pitch = Mathf.Clamp(PitchDeg - 8f * rush + lookPitch, 4f, 70f) * Mathf.Deg2Rad;
            var distance = Distance * (1f - 0.2f * rush - 0.08f * kick);
            var back = -SimSpace.Direction(ViewForward) * (Mathf.Cos(pitch) * distance);
            transform.position = focus + back + Vector3.up * (Mathf.Sin(pitch) * distance);
            transform.rotation = Quaternion.LookRotation(focus + Vector3.down * lookDown - transform.position);
        }
    }
}
