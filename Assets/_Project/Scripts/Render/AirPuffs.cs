using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// Soft air clouds streaming from a blower's nozzle. Particles live in world space so they trail behind
    /// a moving raft, and travel about as far as the blower's cone actually reaches.
    /// </summary>
    public sealed class AirPuffs : MonoBehaviour
    {
        const float Lifetime = 0.75f;

        ParticleSystem system;

        public static AirPuffs Attach(Transform nozzle, Material material)
        {
            var go = new GameObject("Air");
            go.transform.SetParent(nozzle, false);
            var puffs = go.AddComponent<AirPuffs>();

            var system = puffs.system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true;
            main.startLifetime = Lifetime;
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            main.startColor = new Color(0.94f, 0.98f, 1f, 0.55f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.04f; // warm air drifts up a little
            main.maxParticles = 400;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone; // emits along the nozzle's +Z
            shape.radius = 0.05f;

            var emission = system.emission;
            emission.rateOverTime = 0f;

            // Puffs swell as they slow, merging into one soft cloud rather than a spray of dots.
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 2.6f));

            var billow = system.noise;
            billow.enabled = true;
            billow.strength = 0.5f;
            billow.frequency = 0.8f;

            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 0.95f, 1f), 1f) },
                         new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.35f, 0.55f), new GradientAlphaKey(0f, 1f) });
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = fade;

            var slowDown = system.limitVelocityOverLifetime;
            slowDown.enabled = true;
            slowDown.drag = 1.5f; // air jets lose speed quickly

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = -10f; // draw over the water and foam

            system.Play();
            return puffs;
        }

        /// <summary>Matches the cloud to a blower's cone: its spread and how far the air reaches.</summary>
        public void Configure(float coneHalfAngleDeg, float range)
        {
            var shape = system.shape;
            shape.angle = coneHalfAngleDeg;
            var main = system.main;
            main.startSpeed = range / Lifetime * 1.6f; // drag slows the puffs, so start fast enough to reach the range
        }

        public void SetFlow(float spool)
        {
            var emission = system.emission;
            emission.rateOverTime = spool * 45f;
        }

        public void Burst() => system.Emit(28);
    }
}
