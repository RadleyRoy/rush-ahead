using UnityEngine;
using R = RushAhead.Render.Sculpture.Region;

namespace RushAhead.Render
{
    /// <summary>The creatures that sculpted meshes are baked for, split where they move (a croc's upper jaw, a bear's arm).</summary>
    public enum Creature { CrocBody, CrocJaw, Hippo, BearBody, BearArm, TurtleShell, TurtleHead, TurtleFlipper }

    /// <summary>
    /// The river's creatures as sculptures (see <see cref="Sculpture"/>), in the frames their views already use, so
    /// they drop in where the block versions stood. Region colours: the hide (fur), the pale underside, dark markings,
    /// and skin (teeth, a pink snout).
    /// </summary>
    public static class CreatureSculptures
    {
        /// <summary>How fine each is meshed: big beasts coarser.</summary>
        public static float Cell(Creature creature) => creature switch
        {
            Creature.Hippo or Creature.BearBody => 0.05f,
            Creature.CrocBody or Creature.TurtleShell => 0.035f,
            _ => 0.025f,
        };

        public static Sculpture For(Creature creature) => creature switch
        {
            Creature.CrocBody => CrocBody(),
            Creature.CrocJaw => CrocJaw(),
            Creature.Hippo => Hippo(),
            Creature.BearBody => BearBody(),
            Creature.BearArm => BearArm(),
            Creature.TurtleShell => TurtleShell(),
            Creature.TurtleHead => TurtleHead(),
            _ => TurtleFlipper(),
        };

        /// <summary>A crocodile lying low in the water, head toward +x: long body, tapering tail, splayed legs, armoured back, the lower jaw.</summary>
        static Sculpture CrocBody()
        {
            var s = new Sculpture();
            s.Ellipsoid(new Vector3(-0.1f, 0.12f, 0f), new Vector3(0.95f, 0.2f, 0.36f), R.Fur, 0.12f);
            s.Ellipsoid(new Vector3(-0.1f, 0.05f, 0f), new Vector3(0.85f, 0.13f, 0.32f), R.Pale, 0.06f); // pale belly underneath
            s.Limb(new Vector3(-0.85f, 0.1f, 0f), new Vector3(-1.45f, 0.09f, 0f), 0.22f, 0.13f, R.Fur, 0.1f);
            s.Limb(new Vector3(-1.45f, 0.09f, 0f), new Vector3(-1.95f, 0.08f, 0.04f), 0.13f, 0.035f, R.Fur, 0.06f);
            s.Ellipsoid(new Vector3(0.82f, 0.14f, 0f), new Vector3(0.36f, 0.15f, 0.25f), R.Fur, 0.1f); // head
            s.Limb(new Vector3(0.85f, 0.06f, 0f), new Vector3(1.72f, 0.05f, 0f), 0.17f, 0.1f, R.Pale, 0.05f); // lower jaw
            foreach (var side in new[] { -1f, 1f })
            {
                s.Sphere(new Vector3(0.62f, 0.27f, 0.13f * side), 0.075f, R.Fur, 0.04f); // eye bumps
                foreach (var x in new[] { 0.5f, -0.55f })
                {
                    s.Limb(new Vector3(x, 0.08f, 0.28f * side), new Vector3(x + 0.12f, -0.02f, 0.55f * side), 0.08f, 0.06f, R.Fur, 0.05f);
                    s.Ellipsoid(new Vector3(x + 0.16f, -0.04f, 0.6f * side), new Vector3(0.1f, 0.03f, 0.08f), R.Fur, 0.03f);
                }

                for (var i = 0; i < 9; i++) // rows of armour plates down the back
                    s.Ellipsoid(new Vector3(0.6f - i * 0.2f, 0.29f - 0.012f * i, (0.09f + 0.01f * i) * side), new Vector3(0.07f, 0.05f, 0.05f), R.Dark, 0.025f);
            }

            for (var i = 0; i < 8; i++) // the ridge along the tail
                s.Ellipsoid(new Vector3(-0.95f - i * 0.12f, 0.22f - 0.016f * i, 0f), new Vector3(0.05f, 0.06f - 0.004f * i, 0.025f), R.Dark, 0.02f);
            return s;
        }

        /// <summary>The upper jaw, hinged at its back end (the pivot), with nostrils on the tip and a row of teeth.</summary>
        static Sculpture CrocJaw()
        {
            var s = new Sculpture();
            s.Limb(Vector3.zero, new Vector3(0.88f, -0.03f, 0f), 0.16f, 0.1f, R.Fur, 0.06f);
            s.Ellipsoid(new Vector3(0.12f, 0.02f, 0f), new Vector3(0.24f, 0.13f, 0.22f), R.Fur, 0.08f);
            foreach (var side in new[] { -1f, 1f })
            {
                s.Sphere(new Vector3(0.84f, 0.05f, 0.04f * side), 0.035f, R.Dark, 0.015f); // nostrils
                for (var i = 0; i < 7; i++) // teeth
                    s.Limb(new Vector3(0.18f + i * 0.11f, -0.08f, (0.13f - 0.006f * i) * side), new Vector3(0.18f + i * 0.11f, -0.15f, (0.13f - 0.006f * i) * side), 0.022f, 0.006f, R.Skin, 0.006f);
            }

            return s;
        }

        /// <summary>A hippo heaving up out of the water, facing +z: broad back, big head, wide pink snout, small ears and eyes on top.</summary>
        static Sculpture Hippo()
        {
            var s = new Sculpture();
            s.Ellipsoid(new Vector3(0f, 0f, -0.3f), new Vector3(1.2f, 0.75f, 1.6f), R.Fur, 0.25f);
            s.Ellipsoid(new Vector3(0f, 0.25f, 1.35f), new Vector3(0.82f, 0.55f, 0.75f), R.Fur, 0.25f);
            s.Ellipsoid(new Vector3(0f, 0.02f, 2.0f), new Vector3(0.78f, 0.42f, 0.5f), R.Skin, 0.15f);
            foreach (var side in new[] { -1f, 1f })
            {
                s.Ellipsoid(new Vector3(0.45f * side, 0.7f, 1.2f), new Vector3(0.19f, 0.15f, 0.17f), R.Fur, 0.08f); // eye mounds
                s.Ellipsoid(new Vector3(0.6f * side, 0.86f, 0.85f), new Vector3(0.13f, 0.16f, 0.07f), R.Fur, 0.05f); // ears
                s.Ellipsoid(new Vector3(0.28f * side, 0.38f, 2.3f), new Vector3(0.09f, 0.06f, 0.06f), R.Dark, 0.03f); // nostrils
                s.Ellipsoid(new Vector3(0.62f * side, -0.05f, 1.85f), new Vector3(0.12f, 0.22f, 0.3f), R.Skin, 0.1f); // jowls
            }

            return s;
        }

        /// <summary>A big brown bear standing on the lip, facing +z: heavy torso, hunched shoulders, round ears, a pale muzzle.</summary>
        static Sculpture BearBody()
        {
            var s = new Sculpture();
            s.Ellipsoid(new Vector3(0f, 1.1f, 0f), new Vector3(0.75f, 1.05f, 0.65f), R.Fur, 0.2f);
            s.Ellipsoid(new Vector3(0f, 1.65f, 0.12f), new Vector3(0.7f, 0.45f, 0.55f), R.Fur, 0.25f); // shoulders
            s.Ellipsoid(new Vector3(0f, 2.35f, 0.25f), new Vector3(0.47f, 0.42f, 0.45f), R.Fur, 0.15f); // head
            s.Ellipsoid(new Vector3(0f, 2.23f, 0.7f), new Vector3(0.22f, 0.18f, 0.22f), R.Pale, 0.08f); // muzzle
            s.Ellipsoid(new Vector3(0f, 2.31f, 0.92f), new Vector3(0.08f, 0.06f, 0.05f), R.Dark, 0.03f); // nose
            foreach (var side in new[] { -1f, 1f })
            {
                s.Sphere(new Vector3(0.36f * side, 2.72f, 0.18f), 0.15f, R.Fur, 0.06f);
                s.Sphere(new Vector3(0.36f * side, 2.72f, 0.24f), 0.08f, R.Pale, 0.02f);
                s.Limb(new Vector3(0.38f * side, 0.6f, 0.05f), new Vector3(0.45f * side, 0.08f, 0.15f), 0.26f, 0.22f, R.Fur, 0.12f); // legs
                s.Ellipsoid(new Vector3(0.45f * side, 0.06f, 0.3f), new Vector3(0.22f, 0.1f, 0.3f), R.Fur, 0.06f);
                s.Limb(new Vector3(0.55f * side, 1.75f, 0.2f), new Vector3(0.6f * side, 1.1f, 0.45f), 0.2f, 0.15f, R.Fur, 0.1f); // the arm that doesn't swipe
            }

            return s;
        }

        /// <summary>The swiping arm, from the shoulder (the pivot) out along +z to a big pale-padded paw with claws.</summary>
        static Sculpture BearArm()
        {
            var s = new Sculpture();
            s.Limb(Vector3.zero, new Vector3(0f, 0f, 1.3f), 0.22f, 0.17f, R.Fur, 0.08f);
            s.Ellipsoid(new Vector3(0f, 0f, 1.45f), new Vector3(0.24f, 0.15f, 0.24f), R.Fur, 0.08f);
            s.Ellipsoid(new Vector3(0f, -0.1f, 1.45f), new Vector3(0.16f, 0.06f, 0.16f), R.Pale, 0.04f);
            for (var i = -2; i <= 2; i++)
                s.Limb(new Vector3(i * 0.075f, -0.02f, 1.62f), new Vector3(i * 0.08f, -0.08f, 1.78f), 0.025f, 0.008f, R.Skin, 0.008f);
            return s;
        }

        /// <summary>A snapping turtle's shell (domed, ridged, plated) with the underside showing at the rim; head and flippers are separate.</summary>
        static Sculpture TurtleShell()
        {
            var s = new Sculpture();
            s.Ellipsoid(new Vector3(0f, 0.08f, 0f), new Vector3(1.5f, 0.5f, 1.65f), R.Fur, 0.2f);
            s.Ellipsoid(new Vector3(0f, -0.12f, 0f), new Vector3(1.4f, 0.22f, 1.55f), R.Pale, 0.12f);
            for (var i = -2; i <= 2; i++)
                s.Ellipsoid(new Vector3(0f, 0.52f - 0.03f * Mathf.Abs(i), i * 0.55f), new Vector3(0.28f, 0.14f, 0.24f), R.Dark, 0.08f);
            foreach (var side in new[] { -1f, 1f })
                for (var i = -1; i <= 1; i++)
                    s.Ellipsoid(new Vector3(0.72f * side, 0.36f, i * 0.72f), new Vector3(0.3f, 0.11f, 0.3f), R.Dark, 0.1f);
            for (var a = 0; a < 16; a++) // the serrated rim
            {
                var angle = a * Mathf.PI * 2f / 16f;
                s.Ellipsoid(new Vector3(Mathf.Sin(angle) * 1.45f, 0f, Mathf.Cos(angle) * 1.6f), new Vector3(0.18f, 0.08f, 0.18f), R.Fur, 0.08f);
            }

            return s;
        }

        /// <summary>The head, from the neck (the pivot) out along +z to a hooked beak.</summary>
        static Sculpture TurtleHead()
        {
            var s = new Sculpture();
            s.Limb(new Vector3(0f, 0f, -0.35f), new Vector3(0f, 0.02f, 0.1f), 0.24f, 0.26f, R.Pale, 0.08f);
            s.Ellipsoid(new Vector3(0f, 0.03f, 0.15f), new Vector3(0.36f, 0.28f, 0.42f), R.Pale, 0.1f);
            s.Ellipsoid(new Vector3(0f, -0.02f, 0.5f), new Vector3(0.2f, 0.15f, 0.18f), R.Dark, 0.05f); // the hooked beak
            s.Limb(new Vector3(0f, 0.04f, 0.55f), new Vector3(0f, -0.08f, 0.66f), 0.07f, 0.02f, R.Dark, 0.03f);
            foreach (var side in new[] { -1f, 1f })
                s.Sphere(new Vector3(0.2f * side, 0.14f, 0.25f), 0.07f, R.Fur, 0.03f); // eye mounds
            return s;
        }

        /// <summary>A flipper, from the shoulder (the pivot) out along +x.</summary>
        static Sculpture TurtleFlipper()
        {
            var s = new Sculpture();
            s.Limb(Vector3.zero, new Vector3(0.45f, 0f, 0.05f), 0.13f, 0.1f, R.Pale, 0.05f);
            s.Ellipsoid(new Vector3(0.62f, -0.02f, 0.1f), new Vector3(0.3f, 0.06f, 0.18f), R.Pale, 0.06f, new Vector3(0f, -20f, 0f));
            return s;
        }
    }
}
