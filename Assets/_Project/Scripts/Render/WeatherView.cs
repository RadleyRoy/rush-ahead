using System;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// How the weather looks and sounds, on every device from the round's weather and the race clock. Rain: a darker sky,
    /// rain round the camera that leans with each gust (and before it, as the warning), thunder now and then, and its
    /// hiss. Fog: thick air that swallows the river a few seconds ahead. Night: moonlight, a dark sky, fireflies over
    /// the gentler places. The lanterns on the rafts and the lamps on the gates are their own views' (see
    /// <see cref="Night"/>). Changes the scene's light and fog while it shows anything but Clear, and puts them back.
    /// </summary>
    public sealed class WeatherView : MonoBehaviour
    {
        ArtPalette palette;
        Weather shown;
        CourseScene? place;
        bool captured;

        // The scene's own light, as it was before any weather.
        Color sunColour;
        float sunIntensity;
        Color ambientSky, ambientEquator, ambientGround;
        float reflections;
        Material sky;
        Material dimSky;

        // The place's air (each course view sets its own), captured whenever the venue changes.
        Color airColour;
        float airStart, airEnd;

        ParticleSystem rain;
        ParticleSystem fireflies;
        AudioSource hiss;
        float thunderIn = 12f;
        float flash;
        float rumbleIn = -1f;

        /// <summary>The gust now: how hard (0 to 1), which way across the river (as a world direction on the water), and how close the next one is (0 to 1).</summary>
        public Func<(float strength, Vector2 across, float warning)> Gust { get; set; }

        public Weather Shown => shown;

        /// <summary>Whether it's dark: the rafts light their lanterns and the gates their lamps.</summary>
        public bool Night => shown == Weather.Night;

        public static WeatherView Create(ArtPalette palette)
        {
            var view = new GameObject("Weather").AddComponent<WeatherView>();
            view.palette = palette;
            return view;
        }

        /// <summary>
        /// Shows a weather at a place (null: the practice lagoon, always clear). Call again whenever the venue changes,
        /// after its course view has set the place's air.
        /// </summary>
        public void Show(Weather weather, CourseScene? at, bool newVenue)
        {
            Capture();
            if (newVenue || !place.HasValue)
            {
                airColour = RenderSettings.fogColor;
                airStart = RenderSettings.fogStartDistance;
                airEnd = RenderSettings.fogEndDistance;
            }

            place = at;
            shown = at.HasValue ? weather : Weather.Clear;
            Apply();
        }

        void Capture()
        {
            if (captured) return;
            captured = true;
            var sun = RenderSettings.sun;
            sunColour = sun != null ? sun.color : Color.white;
            sunIntensity = sun != null ? sun.intensity : 1f;
            ambientSky = RenderSettings.ambientSkyColor;
            ambientEquator = RenderSettings.ambientEquatorColor;
            ambientGround = RenderSettings.ambientGroundColor;
            reflections = RenderSettings.reflectionIntensity;
            sky = RenderSettings.skybox;
            if (sky != null) dimSky = new Material(sky) { name = "Weather sky" };
        }

        void Apply()
        {
            var sun = RenderSettings.sun;
            var (light, intensity, ambient, fog, fogStart, fogEnd, skyTint, skyExposure, reflect) = shown switch
            {
                Weather.Rain => (new Color(0.78f, 0.82f, 0.9f), 0.5f, 0.55f, Color.Lerp(airColour, new Color(0.46f, 0.5f, 0.56f), 0.7f), airStart * 0.4f, airEnd * 0.6f,
                    new Color(0.55f, 0.58f, 0.64f), 0.55f, 0.55f),
                Weather.Fog => (new Color(0.92f, 0.94f, 0.96f), 0.65f, 0.85f, Color.Lerp(airColour, new Color(0.8f, 0.82f, 0.84f), 0.8f), 2f, 52f,
                    new Color(0.85f, 0.87f, 0.9f), 0.8f, 0.6f),
                Weather.Night => (new Color(0.55f, 0.66f, 1f), 0.32f, 0.28f, new Color(0.035f, 0.05f, 0.09f), airStart * 0.6f, airEnd * 0.85f,
                    new Color(0.22f, 0.28f, 0.45f), 0.1f, 0.25f),
                _ => (sunColour, sunIntensity, 1f, airColour, airStart, airEnd, Color.white, 1f, reflections),
            };

            if (sun != null)
            {
                sun.color = light;
                sun.intensity = shown == Weather.Clear ? sunIntensity : intensity;
            }

            var night = shown == Weather.Night ? new Color(0.45f, 0.55f, 1f) : Color.white;
            RenderSettings.ambientSkyColor = ambientSky * ambient * night;
            RenderSettings.ambientEquatorColor = ambientEquator * ambient * night;
            RenderSettings.ambientGroundColor = ambientGround * ambient * night;
            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.reflectionIntensity = reflect;
            if (dimSky != null)
            {
                if (shown == Weather.Clear) RenderSettings.skybox = sky;
                else
                {
                    if (dimSky.HasProperty("_Tint")) dimSky.SetColor("_Tint", skyTint);
                    if (dimSky.HasProperty("_Exposure")) dimSky.SetFloat("_Exposure", (sky.HasProperty("_Exposure") ? sky.GetFloat("_Exposure") : 1f) * skyExposure);
                    RenderSettings.skybox = dimSky;
                }

                DynamicGI.UpdateEnvironment();
            }

            EnsureRain();
            if (shown == Weather.Rain) rain.Play();
            else rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            EnsureFireflies();
            var glowing = shown == Weather.Night && place is CourseScene.Creek or CourseScene.Lake or CourseScene.Canyon or CourseScene.Swamp;
            if (glowing) fireflies.Play();
            else fireflies.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            EnsureHiss();
            if (shown == Weather.Rain && !hiss.isPlaying) hiss.Play();
            if (shown != Weather.Rain) hiss.Stop();
        }

        void OnDestroy()
        {
            if (!captured) return;
            shown = Weather.Clear;
            Apply();
        }

        void EnsureRain()
        {
            if (rain != null) return;
            rain = Particles("Rain", new Color(0.78f, 0.85f, 0.95f, 0.45f), 2600);
            var main = rain.main;
            main.startLifetime = 1.1f;
            main.startSpeed = 0f;
            main.startSize = 0.05f;
            var shape = rain.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44f, 1f, 44f);
            shape.position = new Vector3(0f, 14f, 10f);
            var emission = rain.emission;
            emission.rateOverTime = 1400f * Looks.Detail;
            var renderer = rain.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.045f;
            renderer.lengthScale = 1f;
        }

        void EnsureFireflies()
        {
            if (fireflies != null) return;
            fireflies = Particles("Fireflies", new Color(1f, 0.92f, 0.45f, 0.9f), 300);
            var main = fireflies.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            var shape = fireflies.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60f, 4f, 70f);
            shape.position = new Vector3(0f, 1.5f, 20f);
            var emission = fireflies.emission;
            emission.rateOverTime = 40f * Looks.Detail;
            var noise = fireflies.noise;
            noise.enabled = true;
            noise.strength = 0.8f;
            noise.frequency = 0.3f;
            var blink = fireflies.colorOverLifetime;
            blink.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.2f, 0.45f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            blink.color = gradient;
        }

        void EnsureHiss()
        {
            if (hiss != null) return;
            hiss = gameObject.AddComponent<AudioSource>();
            hiss.loop = true;
            hiss.playOnAwake = false;
            hiss.spatialBlend = 0f;
        }

        ParticleSystem Particles(string name, Color colour, int most)
        {
            var go = new GameObject($"Weather: {name}");
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startColor = colour;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = most;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = new Material(palette.air) { name = $"Weather {name}", color = colour };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        void LateUpdate()
        {
            var camera = Camera.main;
            if (camera != null)
            {
                // The rain and the fireflies go where the camera looks, so there's always some in front of it.
                var forward = camera.transform.forward;
                forward.y = 0f;
                var heading = forward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(forward) : Quaternion.identity;
                if (rain != null) rain.transform.SetPositionAndRotation(new Vector3(camera.transform.position.x, 0f, camera.transform.position.z), heading);
                if (fireflies != null) fireflies.transform.SetPositionAndRotation(new Vector3(camera.transform.position.x, 0f, camera.transform.position.z), heading);
            }

            if (shown != Weather.Rain) return;
            var audio = GameAudio.Instance;
            if (audio != null && audio.Bank != null)
            {
                if (hiss.clip != audio.Bank.Rain)
                {
                    hiss.clip = audio.Bank.Rain;
                    hiss.Play();
                }

                hiss.volume = 0.45f * audio.EffectsVolume;
            }

            // The rain leans with the wind: a little as a gust comes (the warning), hard while it blows.
            var (strength, across, warning) = Gust?.Invoke() ?? (0f, Vector2.zero, 0f);
            var lean = across * (strength * 11f + warning * 5f);
            var velocity = rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(lean.x);
            velocity.y = new ParticleSystem.MinMaxCurve(-22f);
            velocity.z = new ParticleSystem.MinMaxCurve(lean.y);

            // Thunder: a flash or two, then the rumble a moment later.
            thunderIn -= Time.deltaTime;
            if (thunderIn <= 0f)
            {
                thunderIn = UnityEngine.Random.Range(11f, 24f);
                flash = 0.35f;
                rumbleIn = UnityEngine.Random.Range(0.5f, 1.8f);
            }

            if (rumbleIn > 0f)
            {
                rumbleIn -= Time.deltaTime;
                if (rumbleIn <= 0f && audio != null && audio.Bank != null) audio.Play(audio.Bank.Thunder, 0.8f, UnityEngine.Random.Range(0.85f, 1.1f));
            }

            var sun = RenderSettings.sun;
            if (sun != null)
            {
                flash = Mathf.Max(0f, flash - Time.deltaTime);
                var lit = flash > 0.22f || (flash > 0.04f && flash < 0.12f) ? 2.6f : 0.5f;
                sun.intensity = lit;
            }
        }
    }
}
