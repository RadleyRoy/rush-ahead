using System;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// World-aligned follow camera: it never rotates with the boat, so stick directions stay fixed on
    /// screen. It looks ahead along the boat's velocity and zooms out a little with speed.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class FollowCamera : MonoBehaviour
    {
        public Transform Target;
        public Func<Vector2> Velocity;
        public float BaseSize = 8f;
        public float SizePerSpeed = 0.35f;
        public float LookaheadSeconds = 0.6f;
        public float Responsiveness = 4f;

        Camera view;

        void Awake()
        {
            view = GetComponent<Camera>();
            view.orthographic = true;
        }

        void LateUpdate()
        {
            if (Target == null) return;

            var velocity = Velocity?.Invoke() ?? Vector2.zero;
            var goal = (Vector2)Target.position + velocity * LookaheadSeconds;
            var blend = 1f - Mathf.Exp(-Responsiveness * Time.deltaTime);

            var position = Vector2.Lerp(transform.position, goal, blend);
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            view.orthographicSize = Mathf.Lerp(view.orthographicSize, BaseSize + velocity.magnitude * SizePerSpeed, blend * 0.5f);
        }
    }
}
