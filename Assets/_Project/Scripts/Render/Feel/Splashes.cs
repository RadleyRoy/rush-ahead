using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// Water thrown about: droplets and a ring of foam where a raft hits something or tips over, a foam wake behind a
    /// moving raft, and confetti at the finish. One shared particle system per kind, emitting where asked, so a race
    /// full of bumps costs no more objects than a quiet one.
    /// </summary>
    public static class Splashes
    {
        static ParticleSystem droplets;
        static ParticleSystem foam;
        static ParticleSystem confetti;
        static readonly Color[] ConfettiColours =
        {
            new(1f, 0.38f, 0.3f), new(0.3f, 0.55f, 1f), new(1f, 0.84f, 0.2f), new(0.4f, 0.9f, 0.4f), new(1f, 0.6f, 0.85f), Color.white,
        };

        /// <summary>A splash: <paramref name="size"/> 0.2 for a knock, 1 for a capsize.</summary>
        public static void Splash(Vector3 at, float size, ArtPalette palette)
        {
            Ensure(palette);
            var drops = Mathf.RoundToInt(Mathf.Lerp(8f, 60f, size));
            var emit = new ParticleSystem.EmitParams();
            for (var i = 0; i < drops; i++)
            {
                var around = Random.insideUnitCircle;
                emit.position = at + new Vector3(around.x, 0.1f, around.y) * 0.6f;
                emit.velocity = new Vector3(around.x * 2.5f, Random.Range(2.5f, 5.5f) * Mathf.Lerp(0.6f, 1.2f, size), around.y * 2.5f);
                emit.startSize = Random.Range(0.12f, 0.3f) * Mathf.Lerp(0.8f, 1.3f, size);
                droplets.Emit(emit, 1);
            }

            var rings = Mathf.RoundToInt(Mathf.Lerp(3f, 14f, size));
            for (var i = 0; i < rings; i++)
            {
                var around = Random.insideUnitCircle.normalized;
                emit.position = at + new Vector3(around.x, 0f, around.y) * Random.Range(0.3f, 1.2f) + Vector3.up * 0.05f;
                emit.velocity = new Vector3(around.x, 0f, around.y) * Random.Range(0.5f, 1.5f) * Mathf.Lerp(0.8f, 1.6f, size);
                emit.startSize = Random.Range(0.6f, 1.2f) * Mathf.Lerp(0.8f, 1.8f, size);
                foam.Emit(emit, 1);
            }
        }

        /// <summary>A puff of wake foam (called by each moving raft, more often the faster it goes).</summary>
        public static void Wake(Vector3 at, Vector3 drift, ArtPalette palette)
        {
            Ensure(palette);
            var emit = new ParticleSystem.EmitParams
            {
                position = at + Vector3.up * 0.04f,
                velocity = drift,
                startSize = Random.Range(0.5f, 0.9f),
                startLifetime = Random.Range(1.2f, 1.8f),
            };
            foam.Emit(emit, 1);
        }

        /// <summary>Confetti bursting up over a raft that's just won or finished.</summary>
        public static void Confetti(Vector3 at, ArtPalette palette)
        {
            Ensure(palette);
            var emit = new ParticleSystem.EmitParams();
            for (var i = 0; i < 90; i++)
            {
                var around = Random.insideUnitCircle;
                emit.position = at + Vector3.up * 1.5f;
                emit.velocity = new Vector3(around.x * 3.5f, Random.Range(5f, 9f), around.y * 3.5f);
                emit.startColor = ConfettiColours[Random.Range(0, ConfettiColours.Length)];
                emit.rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
                confetti.Emit(emit, 1);
            }
        }

        static void Ensure(ArtPalette palette)
        {
            if (droplets != null) return;
            droplets = Make("Droplets", palette.air, 0.9f, 2.2f, new Color(0.85f, 0.95f, 1f, 0.9f), ParticleSystemRenderMode.Billboard);

            foam = Make("Foam", palette.foam, 1.6f, 0f, new Color(1f, 1f, 1f, 0.7f), ParticleSystemRenderMode.HorizontalBillboard);
            var grow = foam.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.5f));

            var flat = new Material(palette.air) { name = "Confetti", mainTexture = Texture2D.whiteTexture };
            confetti = Make("Confetti", flat, 3f, 0.55f, Color.white, ParticleSystemRenderMode.Mesh);
            var renderer = confetti.GetComponent<ParticleSystemRenderer>();
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var main = confetti.main;
            main.startSize3D = true;
            main.startSizeX = 0.16f;
            main.startSizeY = 0.24f;
            main.startSizeZ = 1f;
            main.startRotation3D = true;
            var spin = confetti.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
            spin.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
            spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var drag = confetti.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 1.6f; // flutters down rather than falling like stones
        }

        static ParticleSystem Make(string name, Material material, float lifetime, float gravity, Color colour, ParticleSystemRenderMode mode)
        {
            var go = new GameObject($"Splashes: {name}");
            Object.DontDestroyOnLoad(go);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true; // nothing emits by itself (emission is off); looping just keeps it alive for Emit
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.gravityModifier = gravity;
            main.startColor = colour;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1500;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = mode;
            system.Play();
            return system;
        }
    }
}
