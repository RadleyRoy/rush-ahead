using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// A chunky toy-like animal built from primitives, standing on the raft and holding its blower out in
    /// front (each kind of blower its own model, see <see cref="BlowerModels"/>). Faces +Z; the air leaves along
    /// <see cref="Nozzle"/>'s forward.
    /// </summary>
    public sealed class AnimalView : MonoBehaviour
    {
        static readonly Color Pink = new(1f, 0.62f, 0.7f);

        Transform figure;
        Transform blowerRoot;
        Transform blowerModel;
        Transform moving;
        BlowerKind kind;
        BlowerDef shownBlower;
        ArtPalette palette;
        float squeeze;
        float blowing;
        Transform[] eyes;
        Vector3[] eyeScales;
        float nextBlink;
        float blinking;
        float cheering;
        float panic;
        float seed;

        /// <summary>The blower's tip. Its forward is the airflow direction.</summary>
        public Transform Nozzle { get; private set; }

        /// <summary>Just above the head (and a bunny's ears), where a name tag goes.</summary>
        public Transform TagAnchor { get; private set; }

        public static AnimalView Build(Transform parent, AnimalShape shape, Color fur, Color playerColor, BlowerDef blower, ArtPalette palette)
        {
            var root = new GameObject($"{shape}").transform;
            root.SetParent(parent, false);
            var view = root.gameObject.AddComponent<AnimalView>();
            view.palette = palette;
            view.figure = Shapes3D.Group(root, "Figure");
            view.TagAnchor = Shapes3D.Group(view.figure, "Tag anchor", new Vector3(0f, 1.5f, 0f));
            view.BuildBody(shape, palette.Tinted(palette.animal, fur), palette.Tinted(palette.animal, Color.Lerp(fur, Color.white, 0.45f)));
            // A collar in the player's colour ties each animal to its slice painted on the water.
            Shapes3D.Primitive(PrimitiveType.Cylinder, view.figure, new Vector3(0f, 0.76f, 0.02f), new Vector3(0.5f, 0.045f, 0.46f),
                palette.Tinted(palette.animal, playerColor), name: "Collar");
            view.BuildBlower();
            view.SetBlower(blower);

            // The eyes (whites and pupils), for blinking and going wide with alarm.
            var parts = view.figure.GetComponentsInChildren<Transform>();
            view.eyes = System.Array.FindAll(parts, t => t.name is "Eye" or "Pupil");
            view.eyeScales = System.Array.ConvertAll(view.eyes, t => t.localScale);
            view.seed = Random.value * 100f;
            view.nextBlink = Random.Range(1f, 4f);
            return view;
        }

        void BuildBody(AnimalShape shape, Material furMaterial, Material lightMaterial)
        {
            var f = figure;
            var skin = shape == AnimalShape.Pig ? palette.Tinted(palette.animal, Pink) : lightMaterial;

            Shapes3D.Primitive(PrimitiveType.Capsule, f, new Vector3(0f, 0.42f, 0f), new Vector3(0.56f, 0.38f, 0.5f), furMaterial, name: "Body");
            Shapes3D.Primitive(PrimitiveType.Sphere, f, new Vector3(0f, 0.4f, 0.17f), new Vector3(0.38f, 0.44f, 0.2f), lightMaterial, name: "Belly");
            foreach (var side in new[] { -1f, 1f })
                Shapes3D.Primitive(PrimitiveType.Sphere, f, new Vector3(0.13f * side, 0.05f, 0.06f), new Vector3(0.18f, 0.09f, 0.24f), furMaterial, name: "Foot");

            var head = Shapes3D.Group(f, "Head", new Vector3(0f, 0.96f, 0.04f));
            Shapes3D.Primitive(PrimitiveType.Sphere, head, Vector3.zero, Vector3.one * 0.5f, furMaterial, name: "Skull");
            foreach (var side in new[] { -1f, 1f })
            {
                Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0.1f * side, 0.05f, 0.2f), Vector3.one * 0.12f, palette.eyeWhite, name: "Eye");
                Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0.1f * side, 0.05f, 0.255f), Vector3.one * 0.07f, palette.eyeDark, name: "Pupil");
            }

            switch (shape)
            {
                case AnimalShape.Bear:
                    foreach (var side in new[] { -1f, 1f })
                        Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0.18f * side, 0.2f, -0.02f), new Vector3(0.16f, 0.16f, 0.08f), furMaterial, name: "Ear");
                    Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0f, -0.07f, 0.21f), new Vector3(0.2f, 0.15f, 0.16f), lightMaterial, name: "Muzzle");
                    Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0f, -0.03f, 0.29f), new Vector3(0.08f, 0.06f, 0.05f), palette.eyeDark, name: "Nose");
                    break;
                case AnimalShape.Cat:
                    foreach (var side in new[] { -1f, 1f })
                        Shapes3D.Primitive(PrimitiveType.Cube, head, new Vector3(0.14f * side, 0.21f, 0f), new Vector3(0.13f, 0.13f, 0.05f), furMaterial,
                            Quaternion.Euler(0f, 0f, 45f + 12f * side), "Ear");
                    Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0f, -0.05f, 0.24f), new Vector3(0.06f, 0.045f, 0.04f), palette.Tinted(palette.animal, Pink), name: "Nose");
                    break;
                case AnimalShape.Bunny:
                    foreach (var side in new[] { -1f, 1f })
                        Shapes3D.Primitive(PrimitiveType.Capsule, head, new Vector3(0.09f * side, 0.38f, -0.02f), new Vector3(0.1f, 0.2f, 0.06f), furMaterial,
                            Quaternion.Euler(-8f, 0f, -10f * side), "Ear");
                    Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0f, -0.05f, 0.24f), new Vector3(0.06f, 0.045f, 0.04f), palette.Tinted(palette.animal, Pink), name: "Nose");
                    break;
                case AnimalShape.Pig:
                    foreach (var side in new[] { -1f, 1f })
                        Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0.17f * side, 0.17f, 0.04f), new Vector3(0.16f, 0.07f, 0.12f), skin,
                            Quaternion.Euler(20f, 0f, -35f * side), "Ear");
                    Shapes3D.Primitive(PrimitiveType.Cylinder, head, new Vector3(0f, -0.05f, 0.25f), new Vector3(0.17f, 0.03f, 0.13f), skin,
                        Quaternion.Euler(90f, 0f, 0f), "Snout");
                    break;
                case AnimalShape.Otter:
                    // Small round ears set low and wide, a broad pale muzzle of whisker pads, and a thick tail along the floor.
                    foreach (var side in new[] { -1f, 1f })
                    {
                        Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0.2f * side, 0.12f, -0.02f), new Vector3(0.1f, 0.1f, 0.06f), furMaterial, name: "Ear");
                        Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0.06f * side, -0.08f, 0.21f), new Vector3(0.13f, 0.11f, 0.11f), lightMaterial, name: "Whisker pad");
                    }

                    Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0f, -0.02f, 0.27f), new Vector3(0.09f, 0.06f, 0.05f), palette.eyeDark, name: "Nose");
                    Shapes3D.Primitive(PrimitiveType.Capsule, f, new Vector3(0f, 0.15f, -0.38f), new Vector3(0.16f, 0.24f, 0.13f), furMaterial,
                        Quaternion.Euler(-110f, 0f, 0f), "Tail");
                    break;
                case AnimalShape.Raccoon:
                    // Pointed ears, the black bandit mask round the eyes, a pale muzzle and a ringed tail.
                    foreach (var side in new[] { -1f, 1f })
                        Shapes3D.Primitive(PrimitiveType.Cube, head, new Vector3(0.15f * side, 0.2f, -0.01f), new Vector3(0.12f, 0.12f, 0.05f), furMaterial,
                            Quaternion.Euler(0f, 0f, 45f + 8f * side), "Ear");
                    Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0f, 0.05f, 0.18f), new Vector3(0.38f, 0.12f, 0.12f), palette.eyeDark, name: "Mask");
                    Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0f, -0.08f, 0.21f), new Vector3(0.17f, 0.12f, 0.13f), lightMaterial, name: "Muzzle");
                    Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0f, -0.04f, 0.28f), new Vector3(0.07f, 0.05f, 0.04f), palette.eyeDark, name: "Nose");
                    for (var ring = 0; ring < 4; ring++)
                        Shapes3D.Primitive(PrimitiveType.Sphere, f, new Vector3(0f, 0.26f - 0.04f * ring, -0.3f - 0.07f * ring), Vector3.one * (0.17f - 0.012f * ring),
                            ring % 2 == 0 ? furMaterial : palette.eyeDark, name: "Tail ring");
                    break;
            }

            foreach (var side in new[] { -1f, 1f })
                Shapes3D.Primitive(PrimitiveType.Capsule, f, new Vector3(0.2f * side, 0.58f, 0.2f), new Vector3(0.12f, 0.17f, 0.12f), furMaterial,
                    Quaternion.Euler(70f, 0f, 0f), "Arm");
        }

        void BuildBlower()
        {
            blowerRoot = Shapes3D.Group(figure, "Blower", new Vector3(0f, 0.55f, 0.32f));
            Nozzle = Shapes3D.Group(blowerRoot, "Nozzle", new Vector3(0f, 0f, 0.6f)); // kept across blower changes: the air and its sound hang off it
        }

        /// <summary>Holds this blower: its own model in its own colour, with the nozzle moved to its tip.</summary>
        public void SetBlower(BlowerDef blower)
        {
            if (blower == shownBlower && blowerModel != null) return;
            shownBlower = blower;
            if (blowerModel != null)
            {
                if (Application.isPlaying) Destroy(blowerModel.gameObject);
                else DestroyImmediate(blowerModel.gameObject);
            }

            kind = BlowerModels.KindOf(blower);
            blowerModel = Shapes3D.Group(blowerRoot, "Model");
            var (nozzle, part) = BlowerModels.Build(kind, blowerModel, blower != null ? blower.color : Color.white, palette);
            moving = part;
            Nozzle.localPosition = nozzle;
        }

        /// <summary>A burst: the bellows squeeze shut, anything else kicks back in the hands.</summary>
        public void Puff() => squeeze = 1f;

        /// <summary>How hard the blower is running, 0 to 1: the fan waves, motors rumble.</summary>
        public void Blowing(float spool) => blowing = Mathf.Clamp01(spool);

        /// <summary>Turns to point the blower along <paramref name="airDirection"/> and crouches when bracing.</summary>
        public void Pose(Vector3 airDirection, Vector3 up, bool braced, float dt)
        {
            var flat = Vector3.ProjectOnPlane(airDirection, up);
            if (flat.sqrMagnitude > 1e-4f)
                // The sim already sweeps the nozzle smoothly, so follow it closely rather than adding a second, laggier filter.
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat, up), 1f - Mathf.Exp(-25f * dt));

            var crouch = braced ? 0.8f : 1f;
            figure.localScale = Vector3.Lerp(figure.localScale, new Vector3(1f, crouch, 1f), 1f - Mathf.Exp(-12f * dt));
        }

        /// <summary>How alarmed the animal is, 0 to 1: the raft heeling toward a capsize makes it tremble and stare.</summary>
        public void Alarm(float danger) => panic = Mathf.Clamp01(danger);

        /// <summary>Jumps for joy for a couple of seconds: the raft finished, or won.</summary>
        public void Cheer() => cheering = 2.4f;

        void Update()
        {
            var dt = Time.deltaTime;

            // Blink every few seconds, quickly; not while staring in alarm.
            nextBlink -= dt;
            if (nextBlink <= 0f && panic < 0.5f)
            {
                blinking = 0.12f;
                nextBlink = Random.Range(2f, 5.5f);
            }

            blinking = Mathf.Max(0f, blinking - dt);
            var wide = 1f + 0.3f * panic;
            var lid = blinking > 0f ? 0.12f : 1f;
            for (var i = 0; i < eyes.Length; i++)
                eyes[i].localScale = new Vector3(eyeScales[i].x * wide, eyeScales[i].y * wide * lid, eyeScales[i].z * wide);

            // A cheer is a few happy hops; alarm is a tremble.
            cheering = Mathf.Max(0f, cheering - dt);
            var hop = cheering > 0f ? Mathf.Abs(Mathf.Sin((2.4f - cheering) * 9f + seed)) * 0.28f * Mathf.Min(1f, cheering) : 0f;
            var t = Time.time * 30f + seed;
            var shiver = panic * panic * 7f;
            figure.localPosition = new Vector3(0f, hop, 0f);
            figure.localRotation = Quaternion.Euler((Mathf.PerlinNoise(t, 0f) - 0.5f) * shiver, 0f, (Mathf.PerlinNoise(0f, t) - 0.5f) * shiver);
            AnimateBlower(dt);
        }

        void AnimateBlower(float dt)
        {
            if (blowerModel == null) return;
            squeeze = Mathf.MoveTowards(squeeze, 0f, dt * 2.6f);
            var shut = squeeze > 0.7f ? 1f : squeeze / 0.7f; // snaps shut, then opens slowly
            switch (kind)
            {
                case BlowerKind.Bellows:
                    if (moving != null) moving.localScale = new Vector3(1f, Mathf.Lerp(1f, 0.3f, shut), 1f);
                    blowerModel.localPosition = Vector3.zero;
                    break;
                case BlowerKind.HandFan:
                    if (moving != null) moving.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 15f + seed) * 30f * blowing - 25f * squeeze, 0f, 0f);
                    break;
                default:
                    // Motors rumble in the hands while they run; a burst kicks back.
                    var rumble = 0.012f * blowing;
                    var r = Time.time * 40f + seed;
                    blowerModel.localPosition = new Vector3((Mathf.PerlinNoise(r, 1f) - 0.5f) * rumble, (Mathf.PerlinNoise(1f, r) - 0.5f) * rumble, -0.08f * squeeze * squeeze);
                    break;
            }
        }
    }
}
