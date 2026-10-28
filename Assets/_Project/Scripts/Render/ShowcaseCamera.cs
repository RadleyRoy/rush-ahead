using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// The camera away from racing: a slow orbit round the player's raft behind the menus, a shot from ahead of the
    /// start line in the lobby, and a close-up of one animal in the dressing booth. It glides between shots on
    /// unscaled time, so it keeps moving while a solo race is paused.
    /// </summary>
    public sealed class ShowcaseCamera : MonoBehaviour
    {
        public enum Shot
        {
            Orbit,
            Front,
            Booth,
        }

        const float SwayDegrees = 50f;
        const float SwaySpeed = 0.22f;
        const float Glide = 2.5f;

        public Shot Mode;

        /// <summary>The raft (or the middle of the rafts) to show, on the water plane, and the way it faces.</summary>
        public (Vector2 position, float heading) Subject;

        /// <summary>Where the subject sits across the screen: -1 at the left edge, 0 in the middle, 1 at the right.</summary>
        public float ScreenOffset;

        /// <summary>Booth shots: the animal's middle, and the way it faces.</summary>
        public Vector3 BoothFocus;
        public Vector3 BoothFacing = Vector3.forward;

        Camera view;
        float sway;
        bool cut = true;

        void Awake() => view = GetComponent<Camera>();

        void OnEnable() => cut = true;

        void LateUpdate()
        {
            var dt = Time.unscaledDeltaTime;
            Vector3 position, focus;
            switch (Mode)
            {
                case Shot.Front:
                {
                    var centre = SimSpace.ToWorld(Subject.position, 0.4f);
                    var ahead = SimSpace.Direction(new Vector2(Mathf.Cos(Subject.heading), Mathf.Sin(Subject.heading)));
                    position = centre + ahead * 10.5f + Vector3.up * 4.2f;
                    focus = centre + Vector3.up * 0.2f;
                    break;
                }

                case Shot.Booth:
                    position = BoothFocus + BoothFacing.normalized * 4.4f + Vector3.up * 1.1f;
                    focus = BoothFocus + Vector3.up * 0.1f;
                    break;

                default:
                {
                    // Ahead of the raft is open water (down the river, or across the lagoon): sway to and fro there.
                    sway += dt * SwaySpeed;
                    var ahead = Mathf.Atan2(Mathf.Cos(Subject.heading), Mathf.Sin(Subject.heading)) * Mathf.Rad2Deg;
                    var centre = SimSpace.ToWorld(Subject.position, 0.5f);
                    var around = Quaternion.Euler(0f, ahead + SwayDegrees * Mathf.Sin(sway), 0f) * Vector3.forward;
                    position = centre + around * 9.5f + Vector3.up * 3.6f;
                    focus = centre;
                    break;
                }
            }

            // Shift the aim sideways so the subject sits where the screen's layout leaves room for it.
            var right = Vector3.Cross(Vector3.up, (focus - position).normalized);
            var halfWidth = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad) * view.aspect * Vector3.Distance(position, focus);
            focus -= right * (ScreenOffset * halfWidth);
            var rotation = Quaternion.LookRotation(focus - position, Vector3.up);

            var blend = cut ? 1f : 1f - Mathf.Exp(-Glide * dt);
            cut = false;
            transform.position = Vector3.Lerp(transform.position, position, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, blend);
        }
    }
}
