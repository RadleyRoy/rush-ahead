using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Render
{
    public enum AnimalLook
    {
        Bear,
        Cat,
        Bunny,
        Pig,
    }

    /// <summary>
    /// A chunky toy-like animal built from primitives, standing on the raft and holding its blower out in
    /// front. Faces +Z; the air leaves along <see cref="Nozzle"/>'s forward. Phase 8 swaps in real models.
    /// </summary>
    public sealed class AnimalView : MonoBehaviour
    {
        static readonly Color Pink = new(1f, 0.62f, 0.7f);

        Transform figure;
        Renderer blowerBody;
        ArtPalette palette;

        /// <summary>The blower's tip. Its forward is the airflow direction.</summary>
        public Transform Nozzle { get; private set; }

        public static AnimalView Build(Transform parent, AnimalLook look, Color fur, Color playerColor, BlowerDef blower, ArtPalette palette)
        {
            var root = new GameObject($"{look}").transform;
            root.SetParent(parent, false);
            var view = root.gameObject.AddComponent<AnimalView>();
            view.palette = palette;
            view.figure = Shapes3D.Group(root, "Figure");
            view.BuildBody(look, palette.Tinted(palette.animal, fur), palette.Tinted(palette.animal, Color.Lerp(fur, Color.white, 0.45f)));
            // A collar in the player's colour ties each animal to its slice painted on the water.
            Shapes3D.Primitive(PrimitiveType.Cylinder, view.figure, new Vector3(0f, 0.76f, 0.02f), new Vector3(0.5f, 0.045f, 0.46f),
                palette.Tinted(palette.animal, playerColor), name: "Collar");
            view.BuildBlower();
            view.SetBlower(blower);
            return view;
        }

        void BuildBody(AnimalLook look, Material furMaterial, Material lightMaterial)
        {
            var f = figure;
            var skin = look == AnimalLook.Pig ? palette.Tinted(palette.animal, Pink) : lightMaterial;

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

            switch (look)
            {
                case AnimalLook.Bear:
                    foreach (var side in new[] { -1f, 1f })
                        Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0.18f * side, 0.2f, -0.02f), new Vector3(0.16f, 0.16f, 0.08f), furMaterial, name: "Ear");
                    Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0f, -0.07f, 0.21f), new Vector3(0.2f, 0.15f, 0.16f), lightMaterial, name: "Muzzle");
                    Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0f, -0.03f, 0.29f), new Vector3(0.08f, 0.06f, 0.05f), palette.eyeDark, name: "Nose");
                    break;
                case AnimalLook.Cat:
                    foreach (var side in new[] { -1f, 1f })
                        Shapes3D.Primitive(PrimitiveType.Cube, head, new Vector3(0.14f * side, 0.21f, 0f), new Vector3(0.13f, 0.13f, 0.05f), furMaterial,
                            Quaternion.Euler(0f, 0f, 45f + 12f * side), "Ear");
                    Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0f, -0.05f, 0.24f), new Vector3(0.06f, 0.045f, 0.04f), palette.Tinted(palette.animal, Pink), name: "Nose");
                    break;
                case AnimalLook.Bunny:
                    foreach (var side in new[] { -1f, 1f })
                        Shapes3D.Primitive(PrimitiveType.Capsule, head, new Vector3(0.09f * side, 0.38f, -0.02f), new Vector3(0.1f, 0.2f, 0.06f), furMaterial,
                            Quaternion.Euler(-8f, 0f, -10f * side), "Ear");
                    Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0f, -0.05f, 0.24f), new Vector3(0.06f, 0.045f, 0.04f), palette.Tinted(palette.animal, Pink), name: "Nose");
                    break;
                case AnimalLook.Pig:
                    foreach (var side in new[] { -1f, 1f })
                        Shapes3D.Primitive(PrimitiveType.Sphere, head, new Vector3(0.17f * side, 0.17f, 0.04f), new Vector3(0.16f, 0.07f, 0.12f), skin,
                            Quaternion.Euler(20f, 0f, -35f * side), "Ear");
                    Shapes3D.Primitive(PrimitiveType.Cylinder, head, new Vector3(0f, -0.05f, 0.25f), new Vector3(0.17f, 0.03f, 0.13f), skin,
                        Quaternion.Euler(90f, 0f, 0f), "Snout");
                    break;
            }

            foreach (var side in new[] { -1f, 1f })
                Shapes3D.Primitive(PrimitiveType.Capsule, f, new Vector3(0.2f * side, 0.58f, 0.2f), new Vector3(0.12f, 0.17f, 0.12f), furMaterial,
                    Quaternion.Euler(70f, 0f, 0f), "Arm");
        }

        void BuildBlower()
        {
            var blower = Shapes3D.Group(figure, "Blower", new Vector3(0f, 0.55f, 0.32f));
            blowerBody = Shapes3D.Primitive(PrimitiveType.Cylinder, blower, Vector3.zero, new Vector3(0.22f, 0.17f, 0.22f), palette.animal,
                Quaternion.Euler(90f, 0f, 0f), "Body").GetComponent<Renderer>();
            Shapes3D.Primitive(PrimitiveType.Cylinder, blower, new Vector3(0f, 0f, 0.36f), new Vector3(0.1f, 0.22f, 0.1f), palette.blowerTrim,
                Quaternion.Euler(90f, 0f, 0f), "Pipe");
            Nozzle = Shapes3D.Group(blower, "Nozzle", new Vector3(0f, 0f, 0.6f));
        }

        public void SetBlower(BlowerDef blower) => blowerBody.sharedMaterial = palette.Tinted(palette.animal, blower.color);

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
    }
}
