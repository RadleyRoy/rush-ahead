using System;
using System.Collections.Generic;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// The power-up crates: wooden crates bobbing and turning on the water under a glowing "?" sign, that vanish in a
    /// sparkle when a raft takes one and pop back up when they return. Whether each is there comes from
    /// <see cref="Ready"/> (the host's power-ups, or a joined player's snapshots).
    /// </summary>
    public sealed class CrateViews : MonoBehaviour
    {
        const float Size = 1.15f;

        readonly List<(Transform crate, Transform sign, Vector2 at, float phase)> crates = new();
        readonly List<float> shown = new();
        Material glow;
        ArtPalette palette;

        /// <summary>Whether crate i is there to take now.</summary>
        public Func<int, bool> Ready;

        static Texture2D questionMark;

        public static CrateViews Build(PowerUps powerUps, ArtPalette palette, Transform parent)
        {
            var view = new GameObject("Power-up crates").AddComponent<CrateViews>();
            view.transform.SetParent(parent, false);
            view.palette = palette;
            view.glow = new Material(palette.foam) { name = "Crate sign", color = Color.white, mainTexture = QuestionMark() };
            if (view.glow.HasProperty("_BaseMap")) view.glow.SetTexture("_BaseMap", QuestionMark());
            var band = palette.Tinted(palette.sign, new Color(1f, 0.55f, 0.12f));
            for (var i = 0; i < powerUps.Count; i++)
            {
                var root = Shapes3D.Group(view.transform, $"Crate {i}");
                if (palette.crateMesh != null && palette.crateMaterial != null)
                    Shapes3D.Model("Box", palette.crateMesh, palette.crateMaterial, root, Vector3.zero, Size);
                else Shapes3D.Primitive(PrimitiveType.Cube, root, Vector3.zero, Vector3.one * Size, palette.Planks, name: "Box");
                Shapes3D.Primitive(PrimitiveType.Cube, root, Vector3.zero, new Vector3(Size * 1.04f, Size * 0.22f, Size * 1.04f), band, name: "Band");
                var sign = Shapes3D.Primitive(PrimitiveType.Quad, view.transform, Vector3.zero, Vector3.one * 1.1f, view.glow, name: "Sign").transform;
                foreach (var shape in root.GetComponentsInChildren<Collider>()) Destroy(shape);
                foreach (var shape in sign.GetComponentsInChildren<Collider>()) Destroy(shape);
                view.crates.Add((root, sign, powerUps.Position(i), i * 1.7f));
                view.shown.Add(1f);
            }

            return view;
        }

        void Update()
        {
            var t = Time.time;
            var camera = Camera.main;
            for (var i = 0; i < crates.Count; i++)
            {
                var (crate, sign, at, phase) = crates[i];
                var there = Ready?.Invoke(i) ?? true;
                var was = shown[i];
                shown[i] = Mathf.MoveTowards(shown[i], there ? 1f : 0f, Time.deltaTime / (there ? 0.4f : 0.12f));
                if (was > 0.5f && shown[i] <= 0.5f) Splashes.Splash(SimSpace.ToWorld(at, 0.4f), 0.6f, palette); // taken: a burst of spray
                var scale = Mathf.SmoothStep(0f, 1f, shown[i]);
                var bob = 0.12f * Mathf.Sin(t * 2.4f + phase);
                crate.SetPositionAndRotation(SimSpace.ToWorld(at, 0.25f + bob), Quaternion.Euler(6f * Mathf.Sin(t * 1.3f + phase), t * 40f + phase * 50f, 0f));
                crate.localScale = Vector3.one * scale;
                sign.position = SimSpace.ToWorld(at, 1.55f + bob * 1.5f);
                if (camera != null) sign.rotation = Quaternion.LookRotation(sign.position - camera.transform.position);
                sign.localScale = Vector3.one * (1.1f * scale * (1f + 0.08f * Mathf.Sin(t * 5f + phase)));
            }
        }

        /// <summary>A bold white question mark with an orange outline on a clear background, drawn once.</summary>
        static Texture2D QuestionMark()
        {
            if (questionMark != null) return questionMark;
            string[] glyph =
            {
                ".####.",
                "##..##",
                "....##",
                "...##.",
                "..##..",
                "..##..",
                "......",
                "..##..",
                "..##..",
            };
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Question mark", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            var cell = size / 10f;
            bool Ink(float x, float y)
            {
                var gx = Mathf.FloorToInt((x - (size - glyph[0].Length * cell) * 0.5f) / cell);
                var gy = Mathf.FloorToInt((size - y - (size - glyph.Length * cell) * 0.5f) / cell);
                return gy >= 0 && gy < glyph.Length && gx >= 0 && gx < glyph[gy].Length && glyph[gy][gx] == '#';
            }

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var ink = Ink(x + 0.5f, y + 0.5f);
                var edge = false;
                for (var dy = -3; dy <= 3 && !ink && !edge; dy++)
                for (var dx = -3; dx <= 3 && !edge; dx++)
                    edge = Ink(x + 0.5f + dx, y + 0.5f + dy);
                pixels[y * size + x] = ink ? new Color32(255, 255, 255, 255) : edge ? new Color32(255, 128, 30, 255) : new Color32(255, 128, 30, 0);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return questionMark = texture;
        }
    }

    /// <summary>
    /// What a raft's power-ups look and sound like, from its state alone so joined players see the same: a shimmering
    /// bubble while shielded (popping when it saves the raft), flames and fizz out the back while a turbo pushes, a
    /// shockwave ring and a blare when the horn goes off, and a chime when the raft takes a crate.
    /// </summary>
    public sealed class PowerUpEffects : MonoBehaviour
    {
        IBoatState boat;
        ArtPalette palette;
        Transform bubble;
        Transform flames;
        Transform ring;
        Material ringMaterial;
        float ringAge = 10f;
        int pops, horns, pickups;
        bool wasTurbo;

        /// <summary>Whether this is the raft the player is on or watching (its sounds play up close).</summary>
        public bool Close { get; set; }

        /// <summary>The raft it follows: live, or replayed.</summary>
        public IBoatState Source
        {
            set => boat = value;
        }

        public static PowerUpEffects Attach(RaftView view, IBoatState boat, ArtPalette palette)
        {
            var effects = view.gameObject.AddComponent<PowerUpEffects>();
            effects.boat = boat;
            effects.palette = palette;
            effects.pops = boat.ShieldPops;
            effects.horns = boat.HornCount;
            effects.pickups = boat.Pickups;

            var bubbleMesh = Shapes3D.Primitive(PrimitiveType.Sphere, view.transform, new Vector3(0f, 0.6f, 0f), new Vector3(4.1f, 2.6f, 4.1f),
                palette.Spray(new Color(0.55f, 0.9f, 1f, 0.32f), streaks: 0.3f, spin: 0.25f, rim: 1.4f), name: "Shield bubble");
            effects.bubble = bubbleMesh.transform;
            effects.bubble.gameObject.SetActive(false);

            effects.flames = Shapes3D.Group(view.transform, "Turbo flames", new Vector3(0f, 0.45f, -1.7f));
            var fire = palette.Spray(new Color(1f, 0.55f, 0.15f, 0.8f), streaks: 0.6f, rise: 2f, rim: 1f);
            for (var i = -1; i <= 1; i += 2)
            {
                var cone = Shapes3D.MeshObject("Flame", Shapes3D.Cone(0.32f, 1.6f), effects.flames, new Vector3(0.5f * i, 0f, 0f), fire);
                cone.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            }

            effects.flames.gameObject.SetActive(false);
            effects.ringMaterial = new Material(palette.foam) { name = "Horn ring", color = new Color(1f, 0.95f, 0.7f, 0f), mainTexture = Texture2D.whiteTexture };
            if (effects.ringMaterial.HasProperty("_BaseMap")) effects.ringMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture); // solid, not the foam's soft dot
            effects.ring = Shapes3D.MeshObject("Horn ring", Shapes3D.Torus(1f, 0.06f, 48, 6), view.transform.parent, Vector3.zero, effects.ringMaterial).transform;
            effects.ring.gameObject.SetActive(false);
            return effects;
        }

        void OnDestroy()
        {
            if (ring != null) Destroy(ring.gameObject);
        }

        void LateUpdate()
        {
            if (boat == null) return;
            var t = Time.time;
            var audio = GameAudio.Instance;
            var at = transform.position;

            bubble.gameObject.SetActive(boat.Shielded);
            if (boat.Shielded) bubble.localScale = new Vector3(4.1f, 2.6f, 4.1f) * (1f + 0.04f * Mathf.Sin(t * 7f));
            if (boat.ShieldPops != pops)
            {
                pops = boat.ShieldPops;
                Splashes.Splash(at + Vector3.up * 0.5f, 1.4f, palette);
                if (audio != null) Sound(audio.Bank.ShieldPop, 1f);
            }

            var turbo = boat.Turbo;
            flames.gameObject.SetActive(turbo);
            if (turbo) flames.localScale = new Vector3(1f, 1f, 0.8f + 0.4f * Mathf.PerlinNoise(t * 18f, 0.3f));
            if (turbo && !wasTurbo && audio != null) Sound(audio.Bank.Turbo, 0.9f);
            wasTurbo = turbo;

            if (boat.HornCount != horns)
            {
                horns = boat.HornCount;
                ringAge = 0f;
                if (audio != null) Sound(audio.Bank.Horn, 1f);
            }

            ringAge += Time.deltaTime;
            var showRing = ringAge < 0.7f;
            ring.gameObject.SetActive(showRing);
            if (showRing)
            {
                var grow = ringAge / 0.7f;
                ring.position = new Vector3(at.x, 0.3f, at.z);
                ring.localScale = Vector3.one * Mathf.Lerp(1.5f, PowerUps.HornRadius, Mathf.Sqrt(grow));
                ringMaterial.color = new Color(1f, 0.95f, 0.7f, 0.9f * (1f - grow));
            }

            if (boat.Pickups != pickups)
            {
                pickups = boat.Pickups;
                if (audio != null) Sound(audio.Bank.Pickup, 0.8f);
            }
        }

        void Sound(AudioClip clip, float volume)
        {
            var audio = GameAudio.Instance;
            if (Close) audio.Play(clip, volume);
            else audio.PlayAt(clip, transform.position, volume);
        }
    }
}
