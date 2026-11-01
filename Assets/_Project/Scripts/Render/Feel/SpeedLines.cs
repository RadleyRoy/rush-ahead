using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// Streaks of spray rushing past the edges of the view once the raft is really moving: thin stretched particles
    /// thrown at the camera from a ring ahead of it, more of them the faster it goes (see <see cref="ChaseCamera.Rush"/>).
    /// </summary>
    [RequireComponent(typeof(ChaseCamera))]
    public sealed class SpeedLines : MonoBehaviour
    {
        const float MaxRate = 140f;
        ChaseCamera chase;
        ParticleSystem streaks;
        ParticleSystem.EmissionModule emission;

        public static SpeedLines Attach(ChaseCamera chase, ArtPalette palette)
        {
            var lines = chase.gameObject.AddComponent<SpeedLines>();
            lines.chase = chase;
            var go = new GameObject("Speed lines");
            go.transform.SetParent(chase.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 14f);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = 0.35f;
            main.startSpeed = 0f;
            main.startSize = 0.07f;
            main.startColor = new Color(1f, 1f, 1f, 0.45f);
            main.maxParticles = 300;
            main.playOnAwake = false;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 6.5f;
            shape.radiusThickness = 0.35f; // just the rim: the middle of the view stays clear
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.z = new ParticleSystem.MinMaxCurve(-45f);
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 2f;
            renderer.sharedMaterial = palette.air;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lines.streaks = system;
            lines.emission = system.emission;
            lines.emission.rateOverTime = 0f;
            system.Play();
            return lines;
        }

        void LateUpdate()
        {
            if (streaks == null) return;
            var rush = Mathf.Max(chase.Rush, chase.Kicking);
            emission.rateOverTime = MaxRate * Mathf.Clamp01((rush - 0.35f) / 0.65f);
        }
    }
}
