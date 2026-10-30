using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// Soft air clouds streaming from a blower's nozzle. Particles live in world space so they trail behind
    /// a moving raft, and travel about as far as the blower's cone actually reaches. Each kind blows its own way:
    /// a leaf blower's big billows, a hair dryer's thin warm jet, a hand fan's wide soft waft, single big puffs from
    /// the bellows, and dusty air streaming into the shop vac.
    /// </summary>
    public sealed class AirPuffs : MonoBehaviour
    {
        const float Lifetime = 0.75f;

        ParticleSystem system;
        BlowerKind kind;
        float rate = 45f;
        int burstCount = 28;
        float range = 4.5f;
        float cone = 18f;
        float flow;
        float due;

        bool Inward => kind == BlowerKind.ShopVac;

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

        /// <summary>Matches the cloud to a blower: its kind's look, its cone's spread and how far the air reaches.</summary>
        public void Configure(BlowerDef blower)
        {
            kind = BlowerModels.KindOf(blower);
            range = blower.range;
            cone = blower.coneHalfAngleDeg;
            var shape = system.shape;
            shape.angle = cone;
            var speed = range / Lifetime * 1.6f; // drag slows the puffs, so start fast enough to reach the range
            switch (kind)
            {
                case BlowerKind.HairDryer: Look(0.3f, 0.6f, new Color(1f, 0.86f, 0.72f, 0.6f), speed * 1.15f, 70f, 24); break;
                case BlowerKind.HandFan: Look(1.1f, 1.8f, new Color(0.94f, 0.98f, 1f, 0.3f), speed * 0.7f, 20f, 16); break;
                case BlowerKind.Bellows: Look(1.2f, 2f, new Color(0.97f, 0.99f, 1f, 0.6f), speed, 0f, 44); break;
                case BlowerKind.ShopVac: Look(0.25f, 0.55f, new Color(0.86f, 0.84f, 0.78f, 0.6f), 0f, 55f, 26); break;
                default: Look(0.7f, 1.2f, new Color(0.94f, 0.98f, 1f, 0.55f), speed, 45f, 28); break;
            }

            var slowDown = system.limitVelocityOverLifetime;
            slowDown.enabled = !Inward; // air being sucked in speeds up, if anything
            var size = system.sizeOverLifetime;
            size.size = Inward ? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1.6f, 1f, 0.3f))
                : new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, kind == BlowerKind.HairDryer ? 1.6f : 2.6f));
        }

        void Look(float minSize, float maxSize, Color colour, float startSpeed, float perSecond, int burst)
        {
            var main = system.main;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = colour;
            main.startSpeed = startSpeed;
            rate = perSecond;
            burstCount = burst;
        }

        public void SetFlow(float spool)
        {
            flow = spool;
            var emission = system.emission;
            emission.rateOverTime = Inward ? 0f : spool * rate;
        }

        public void Burst()
        {
            if (Inward) Suck(burstCount, 1.5f);
            else system.Emit(burstCount);
        }

        void Update()
        {
            if (!Inward || flow <= 0f) return;
            due += flow * rate * Time.deltaTime;
            var whole = Mathf.FloorToInt(due);
            due -= whole;
            if (whole > 0) Suck(whole, 1f);
        }

        /// <summary>Puffs starting out in the cone and streaming into the nozzle, arriving just before they fade.</summary>
        void Suck(int count, float speedUp)
        {
            var nozzle = transform;
            for (var i = 0; i < count; i++)
            {
                var direction = Quaternion.AngleAxis(Random.Range(-cone, cone), nozzle.up) * Quaternion.AngleAxis(Random.Range(-cone, cone) * 0.5f, nozzle.right) * nozzle.forward;
                var start = nozzle.position + direction * (range * Random.Range(0.5f, 1f));
                system.Emit(new ParticleSystem.EmitParams
                {
                    position = start,
                    velocity = (nozzle.position - start) * (speedUp / (Lifetime * 0.85f)),
                    applyShapeToPosition = false,
                }, 1);
            }
        }
    }
}
