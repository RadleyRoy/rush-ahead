using RushAhead.Content;
using UnityEngine;
using R = RushAhead.Render.Sculpture.Region;

namespace RushAhead.Render
{
    /// <summary>
    /// The crew animals as sculptures: a soft rounded body and head melted together, with each species' ears, muzzle,
    /// nose and tail, and arms reaching forward to hold the blower. Same proportions as the old block animals (the
    /// blower, collar, eyes and name tag all hang where they did). The eyes stay separate, glossy, so they can blink.
    /// Baked into meshes in the editor (see SculptBootstrap), so nothing is sculpted on a phone.
    /// </summary>
    public static class AnimalSculptures
    {
        /// <summary>Where the head's middle is, in the figure's frame, and where the eyes sit on it.</summary>
        public static readonly Vector3 Head = new(0f, 0.96f, 0.04f);
        public static readonly Vector3 EyeOffset = new(0.1f, 0.05f, 0.2f);

        public static Sculpture For(AnimalShape shape)
        {
            var s = new Sculpture();
            Body(s, shape);
            HeadOf(s, shape);
            return s;
        }

        static Vector3 H(float x, float y, float z) => Head + new Vector3(x, y, z);

        static void Body(Sculpture s, AnimalShape shape)
        {
            var slim = shape is AnimalShape.Otter or AnimalShape.Cat;
            var torso = slim ? new Vector3(0.25f, 0.37f, 0.24f) : new Vector3(0.28f, 0.36f, 0.26f);
            s.Ellipsoid(new Vector3(0f, 0.43f, 0f), torso, R.Fur, 0.08f);
            // Hips a little wider than the chest: sat on the raft, weight low.
            s.Ellipsoid(new Vector3(0f, 0.25f, -0.02f), torso * 0.92f + new Vector3(0.02f, -0.12f, 0.01f), R.Fur, 0.1f);
            s.Ellipsoid(new Vector3(0f, 0.4f, 0.16f), new Vector3(0.19f, 0.25f, 0.11f), R.Pale, 0.04f); // belly
            foreach (var side in new[] { -1f, 1f })
            {
                s.Limb(new Vector3(0.12f * side, 0.22f, 0.03f), new Vector3(0.13f * side, 0.06f, 0.08f), 0.085f, 0.07f, R.Fur, 0.05f);
                s.Ellipsoid(new Vector3(0.13f * side, 0.045f, 0.1f), new Vector3(0.085f, 0.045f, 0.125f), shape == AnimalShape.Pig ? R.Skin : R.Pale, 0.03f);
                // Arms: from the shoulder, forward and down to the blower's grip.
                s.Limb(new Vector3(0.21f * side, 0.7f, 0.06f), new Vector3(0.14f * side, 0.56f, 0.33f), 0.075f, 0.06f, R.Fur, 0.05f);
                s.Sphere(new Vector3(0.13f * side, 0.555f, 0.35f), 0.065f, shape == AnimalShape.Pig ? R.Skin : R.Pale, 0.02f); // paw
            }

            s.Limb(new Vector3(0f, 0.7f, 0f), Head + new Vector3(0f, -0.12f, 0f), 0.17f, 0.15f, R.Fur, 0.08f); // neck

            switch (shape)
            {
                case AnimalShape.Bear:
                    s.Sphere(new Vector3(0f, 0.2f, -0.27f), 0.07f, R.Fur, 0.04f);
                    break;
                case AnimalShape.Cat:
                    s.Limb(new Vector3(0f, 0.16f, -0.22f), new Vector3(0.05f, 0.3f, -0.42f), 0.05f, 0.04f, R.Fur, 0.04f);
                    s.Limb(new Vector3(0.05f, 0.3f, -0.42f), new Vector3(0.02f, 0.62f, -0.44f), 0.04f, 0.035f, R.Fur, 0.03f);
                    break;
                case AnimalShape.Bunny:
                    s.Sphere(new Vector3(0f, 0.22f, -0.27f), 0.1f, R.Pale, 0.04f);
                    break;
                case AnimalShape.Pig:
                    s.Limb(new Vector3(0f, 0.3f, -0.25f), new Vector3(0.05f, 0.36f, -0.33f), 0.025f, 0.02f, R.Skin, 0.02f);
                    s.Limb(new Vector3(0.05f, 0.36f, -0.33f), new Vector3(-0.01f, 0.4f, -0.36f), 0.02f, 0.018f, R.Skin, 0.015f);
                    break;
                case AnimalShape.Otter:
                    s.Limb(new Vector3(0f, 0.16f, -0.2f), new Vector3(0f, 0.05f, -0.58f), 0.11f, 0.045f, R.Fur, 0.06f);
                    break;
                case AnimalShape.Raccoon:
                    // A bushy ringed tail curling up behind.
                    var points = new[] { new Vector3(0f, 0.18f, -0.22f), new Vector3(0f, 0.2f, -0.36f), new Vector3(0f, 0.28f, -0.48f), new Vector3(0f, 0.42f, -0.54f), new Vector3(0f, 0.56f, -0.53f) };
                    for (var i = 0; i < points.Length - 1; i++)
                        s.Limb(points[i], points[i + 1], 0.09f - 0.008f * i, 0.085f - 0.008f * i, i % 2 == 0 ? R.Fur : R.Dark, 0.03f);
                    break;
            }
        }

        static void HeadOf(Sculpture s, AnimalShape shape)
        {
            var wide = shape is AnimalShape.Bear or AnimalShape.Pig;
            s.Ellipsoid(Head, wide ? new Vector3(0.27f, 0.24f, 0.25f) : new Vector3(0.25f, 0.235f, 0.24f), R.Fur, 0.06f);
            s.Ellipsoid(H(0f, -0.07f, 0.04f), new Vector3(0.22f, 0.15f, 0.2f), R.Fur, 0.08f); // cheeks and jaw
            foreach (var side in new[] { -1f, 1f }) s.CarveSphere(H(EyeOffset.x * side, EyeOffset.y, EyeOffset.z + 0.02f), 0.05f, 0.03f); // eye sockets
            switch (shape)
            {
                case AnimalShape.Bear:
                    foreach (var side in new[] { -1f, 1f })
                    {
                        s.Ellipsoid(H(0.18f * side, 0.19f, -0.03f), new Vector3(0.075f, 0.075f, 0.045f), R.Fur, 0.03f);
                        s.Ellipsoid(H(0.18f * side, 0.19f, -0.005f), new Vector3(0.045f, 0.045f, 0.02f), R.Pale, 0.01f);
                    }

                    s.Ellipsoid(H(0f, -0.07f, 0.2f), new Vector3(0.11f, 0.08f, 0.1f), R.Pale, 0.04f);
                    s.Ellipsoid(H(0f, -0.035f, 0.29f), new Vector3(0.048f, 0.034f, 0.03f), R.Dark, 0.015f);
                    break;
                case AnimalShape.Cat:
                    foreach (var side in new[] { -1f, 1f })
                    {
                        s.Limb(H(0.13f * side, 0.15f, 0f), H(0.18f * side, 0.33f, -0.015f), 0.075f, 0.012f, R.Fur, 0.03f);
                        s.Limb(H(0.13f * side, 0.17f, 0.02f), H(0.17f * side, 0.29f, 0.01f), 0.04f, 0.008f, R.Skin, 0.01f);
                        for (var w = -1; w <= 1; w++) // whiskers
                            s.Limb(H(0.06f * side, -0.06f, 0.22f), H(0.2f * side, -0.05f + 0.025f * w, 0.24f), 0.005f, 0.003f, R.Pale, 0.002f);
                    }

                    s.Ellipsoid(H(0f, -0.065f, 0.2f), new Vector3(0.085f, 0.055f, 0.06f), R.Pale, 0.03f);
                    s.Ellipsoid(H(0f, -0.03f, 0.255f), new Vector3(0.032f, 0.022f, 0.02f), R.Skin, 0.01f);
                    break;
                case AnimalShape.Bunny:
                    foreach (var side in new[] { -1f, 1f })
                    {
                        s.Ellipsoid(H(0.09f * side, 0.38f, -0.04f), new Vector3(0.06f, 0.21f, 0.035f), R.Fur, 0.04f, new Vector3(-8f, 0f, -10f * side));
                        s.Ellipsoid(H(0.09f * side, 0.39f, -0.015f), new Vector3(0.035f, 0.17f, 0.015f), R.Skin, 0.01f, new Vector3(-8f, 0f, -10f * side));
                    }

                    s.Ellipsoid(H(0f, -0.07f, 0.2f), new Vector3(0.09f, 0.07f, 0.07f), R.Pale, 0.03f);
                    s.Ellipsoid(H(0f, -0.03f, 0.255f), new Vector3(0.03f, 0.022f, 0.02f), R.Skin, 0.01f);
                    break;
                case AnimalShape.Pig:
                    foreach (var side in new[] { -1f, 1f })
                        s.Box(H(0.17f * side, 0.17f, 0.04f), new Vector3(0.14f, 0.12f, 0.03f), 0.012f, R.Skin, 0.02f, new Vector3(20f, 0f, -35f * side));
                    s.Limb(H(0f, -0.05f, 0.19f), H(0f, -0.05f, 0.28f), 0.085f, 0.08f, R.Skin, 0.02f);
                    foreach (var side in new[] { -1f, 1f }) s.CarveSphere(H(0.03f * side, -0.05f, 0.29f), 0.018f, 0.006f);
                    break;
                case AnimalShape.Otter:
                    foreach (var side in new[] { -1f, 1f })
                    {
                        s.Sphere(H(0.2f * side, 0.11f, -0.03f), 0.05f, R.Fur, 0.02f);
                        s.Ellipsoid(H(0.055f * side, -0.08f, 0.2f), new Vector3(0.075f, 0.065f, 0.065f), R.Pale, 0.03f);
                    }

                    s.Ellipsoid(H(0f, -0.02f, 0.27f), new Vector3(0.05f, 0.033f, 0.028f), R.Dark, 0.012f);
                    break;
                case AnimalShape.Raccoon:
                    foreach (var side in new[] { -1f, 1f })
                    {
                        s.Limb(H(0.14f * side, 0.15f, -0.01f), H(0.17f * side, 0.3f, -0.03f), 0.07f, 0.018f, R.Fur, 0.03f);
                        s.Limb(H(0.14f * side, 0.17f, 0.01f), H(0.16f * side, 0.27f, -0.01f), 0.035f, 0.01f, R.Pale, 0.01f);
                    }

                    s.Ellipsoid(H(0f, 0.05f, 0.16f), new Vector3(0.2f, 0.065f, 0.08f), R.Dark, 0.02f); // the bandit mask
                    s.Ellipsoid(H(0f, -0.08f, 0.2f), new Vector3(0.09f, 0.065f, 0.08f), R.Pale, 0.03f);
                    s.Ellipsoid(H(0f, -0.04f, 0.275f), new Vector3(0.036f, 0.026f, 0.022f), R.Dark, 0.01f);
                    break;
            }
        }
    }
}
