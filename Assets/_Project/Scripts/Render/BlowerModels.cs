using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>The five kinds of blower, by how they play: each has its own model, air and sound.</summary>
    public enum BlowerKind
    {
        LeafBlower,
        HairDryer,
        Bellows,
        HandFan,
        ShopVac,
    }

    /// <summary>
    /// Each blower kind built from primitives, held at the animal's hands and pointing along +Z: a leaf blower with
    /// its engine on the animal's back, a pistol-grip hair dryer, wooden bellows with a brass spout, a folding hand
    /// fan on a stick, and a shop vac whose canister sits on the floor beside its owner.
    /// </summary>
    public static class BlowerModels
    {
        /// <summary>Which kind a blower is, from how it plays (so a new blower looks like the nearest of the five).</summary>
        public static BlowerKind KindOf(BlowerDef blower)
        {
            if (blower == null) return BlowerKind.LeafBlower;
            if (blower.special == BlowerSpecial.Vacuum) return BlowerKind.ShopVac;
            if (blower.thrustNewtons <= 0f) return BlowerKind.Bellows;
            if (blower.thrustNewtons < 22f) return BlowerKind.HandFan;
            return blower.spoolUpMs < 200f ? BlowerKind.HairDryer : BlowerKind.LeafBlower;
        }

        /// <summary>
        /// Builds a kind under <paramref name="root"/>. Returns where the air leaves (or, for the vac, goes in) and the
        /// part that moves: the bellows' boards and folds that squeeze, the fan that waves. Null for the rest.
        /// </summary>
        public static (Vector3 nozzle, Transform moving) Build(BlowerKind kind, Transform root, Color colour, ArtPalette palette)
        {
            var body = palette.Tinted(palette.animal, colour);
            var light = palette.Tinted(palette.animal, Color.Lerp(colour, Color.white, 0.5f));
            var trim = palette.blowerTrim;
            var dark = palette.eyeDark;
            var along = Quaternion.Euler(90f, 0f, 0f); // a cylinder's axis is its y: point it forward

            switch (kind)
            {
                case BlowerKind.HairDryer:
                {
                    Shapes3D.Primitive(PrimitiveType.Cylinder, root, new Vector3(0f, 0.06f, 0.06f), new Vector3(0.26f, 0.15f, 0.26f), body, along, "Barrel");
                    Shapes3D.Primitive(PrimitiveType.Cylinder, root, new Vector3(0f, 0.06f, -0.1f), new Vector3(0.22f, 0.012f, 0.22f), dark, along, "Grille");
                    Shapes3D.Primitive(PrimitiveType.Cylinder, root, new Vector3(0f, 0.06f, 0.29f), new Vector3(0.13f, 0.09f, 0.13f), trim, along, "Nozzle");
                    Shapes3D.Primitive(PrimitiveType.Cube, root, new Vector3(0f, 0.06f, 0.38f), new Vector3(0.17f, 0.06f, 0.04f), trim, name: "Concentrator");
                    Shapes3D.Primitive(PrimitiveType.Capsule, root, new Vector3(0f, -0.12f, -0.02f), new Vector3(0.1f, 0.13f, 0.1f), body, Quaternion.Euler(-15f, 0f, 0f), "Grip");
                    Shapes3D.Primitive(PrimitiveType.Cylinder, root, new Vector3(0f, -0.27f, -0.07f), new Vector3(0.025f, 0.08f, 0.025f), dark, Quaternion.Euler(-25f, 0f, 0f), "Cord");
                    return (new Vector3(0f, 0.06f, 0.42f), null);
                }

                case BlowerKind.Bellows:
                {
                    var wood = palette.Tinted(palette.wood, new Color(0.72f, 0.5f, 0.3f));
                    var leather = palette.Tinted(palette.wood, new Color(0.36f, 0.21f, 0.13f));
                    var brass = palette.Tinted(palette.sign, new Color(0.88f, 0.68f, 0.26f));
                    var pump = Shapes3D.Group(root, "Pump", new Vector3(0f, 0f, 0.04f));
                    foreach (var side in new[] { -1f, 1f })
                    {
                        Shapes3D.Primitive(PrimitiveType.Cube, pump, new Vector3(0f, 0.075f * side, 0f), new Vector3(0.32f, 0.035f, 0.4f), wood, name: "Board");
                        Shapes3D.Primitive(PrimitiveType.Cube, pump, new Vector3(0f, 0.075f * side, -0.27f), new Vector3(0.08f, 0.03f, 0.16f), wood, name: "Handle");
                    }

                    Shapes3D.Primitive(PrimitiveType.Cube, pump, new Vector3(0f, 0.096f, 0.03f), new Vector3(0.2f, 0.01f, 0.24f), body, name: "Paint");
                    Shapes3D.Primitive(PrimitiveType.Cube, pump, Vector3.zero, new Vector3(0.29f, 0.13f, 0.34f), leather, name: "Folds");
                    Shapes3D.Primitive(PrimitiveType.Cylinder, root, new Vector3(0f, 0f, 0.33f), new Vector3(0.07f, 0.1f, 0.07f), brass, along, "Spout");
                    return (new Vector3(0f, 0f, 0.45f), pump);
                }

                case BlowerKind.HandFan:
                {
                    Shapes3D.Primitive(PrimitiveType.Cylinder, root, new Vector3(0f, -0.1f, 0.1f), new Vector3(0.04f, 0.12f, 0.04f), palette.wood, name: "Stick");
                    var fan = Shapes3D.Group(root, "Fan", new Vector3(0f, 0f, 0.12f));
                    const int slats = 9;
                    for (var k = 0; k < slats; k++)
                    {
                        var turn = Quaternion.Euler(0f, 0f, -72f + k * 144f / (slats - 1));
                        Shapes3D.Primitive(PrimitiveType.Cube, fan, turn * new Vector3(0f, 0.2f, 0f), new Vector3(0.08f, 0.4f, 0.012f), k % 2 == 0 ? body : light, turn, "Slat");
                    }

                    Shapes3D.Primitive(PrimitiveType.Sphere, fan, Vector3.zero, Vector3.one * 0.06f, trim, name: "Rivet");
                    return (new Vector3(0f, 0.16f, 0.3f), fan);
                }

                case BlowerKind.ShopVac:
                {
                    // The canister stands on the floor beside the animal (the blower is held at about 0.55 m).
                    Shapes3D.Primitive(PrimitiveType.Cylinder, root, new Vector3(0.36f, -0.36f, -0.3f), new Vector3(0.34f, 0.2f, 0.34f), body, name: "Canister");
                    Shapes3D.Primitive(PrimitiveType.Cylinder, root, new Vector3(0.36f, -0.15f, -0.3f), new Vector3(0.36f, 0.03f, 0.36f), trim, name: "Lid");
                    Shapes3D.Primitive(PrimitiveType.Cube, root, new Vector3(0.36f, -0.1f, -0.3f), new Vector3(0.16f, 0.04f, 0.05f), trim, name: "Handle");
                    var hose = new[] { new Vector3(0.24f, -0.2f, -0.2f), new Vector3(0.22f, -0.12f, -0.07f), new Vector3(0.12f, -0.05f, -0.01f), new Vector3(0f, 0f, 0.02f) };
                    for (var k = 0; k < hose.Length - 1; k++)
                    {
                        var span = hose[k + 1] - hose[k];
                        Shapes3D.Primitive(PrimitiveType.Capsule, root, (hose[k] + hose[k + 1]) * 0.5f, new Vector3(0.075f, span.magnitude * 0.5f + 0.03f, 0.075f), dark,
                            Quaternion.FromToRotation(Vector3.up, span), "Hose");
                    }

                    Shapes3D.Primitive(PrimitiveType.Cylinder, root, new Vector3(0f, 0f, 0.2f), new Vector3(0.08f, 0.21f, 0.08f), trim, along, "Wand");
                    Shapes3D.Primitive(PrimitiveType.Cube, root, new Vector3(0f, 0f, 0.46f), new Vector3(0.34f, 0.07f, 0.1f), body, name: "Head");
                    Shapes3D.Primitive(PrimitiveType.Cube, root, new Vector3(0f, 0f, 0.515f), new Vector3(0.3f, 0.035f, 0.012f), dark, name: "Mouth");
                    return (new Vector3(0f, 0f, 0.53f), null);
                }

                default:
                {
                    Shapes3D.Primitive(PrimitiveType.Cylinder, root, new Vector3(0f, 0f, 0.35f), new Vector3(0.11f, 0.38f, 0.11f), body, along, "Tube");
                    Shapes3D.Primitive(PrimitiveType.Cylinder, root, new Vector3(0f, 0f, 0.74f), new Vector3(0.14f, 0.04f, 0.14f), trim, along, "Tip");
                    Shapes3D.Primitive(PrimitiveType.Cube, root, new Vector3(0f, -0.08f, 0.04f), new Vector3(0.06f, 0.14f, 0.12f), trim, name: "Handle");
                    // The engine is a pack on the animal's back, joined to the tube by a hose.
                    Shapes3D.Primitive(PrimitiveType.Cube, root, new Vector3(0f, 0.12f, -0.62f), new Vector3(0.36f, 0.38f, 0.2f), body, name: "Engine");
                    Shapes3D.Primitive(PrimitiveType.Cylinder, root, new Vector3(0f, 0.33f, -0.62f), new Vector3(0.12f, 0.03f, 0.12f), dark, name: "Cap");
                    Shapes3D.Primitive(PrimitiveType.Sphere, root, new Vector3(0.2f, 0.16f, -0.62f), Vector3.one * 0.06f, trim, name: "Pull cord");
                    Shapes3D.Primitive(PrimitiveType.Capsule, root, new Vector3(0.1f, 0.02f, -0.3f), new Vector3(0.08f, 0.24f, 0.08f), dark, Quaternion.Euler(90f, 0f, 20f), "Hose");
                    return (new Vector3(0f, 0f, 0.78f), null);
                }
            }
        }
    }
}
