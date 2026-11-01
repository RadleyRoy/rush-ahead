using System.Collections.Generic;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// Trees built in code from photographed parts: a tapering trunk and boughs in the bark texture, crowned with leafy
    /// sprays (stamped from scanned leaves) or tiers of needle sprays, on crossed cards. Each kind comes in a few
    /// variants, built once and shared by every tree; a tree is one renderer (bark and foliage as two sub-meshes),
    /// dropped when it's a speck on the screen.
    /// </summary>
    public static class Flora
    {
        public enum Kind { Broadleaf, Conifer }

        public const int Variants = 3;
        const float Built = 10f; // every tree mesh is built this tall, and scaled to size

        static readonly Dictionary<(Kind, int), Mesh> cache = new();

        /// <summary>Whether the palette has what trees are made of (leaf and needle cards); without it, the old round trees stand in.</summary>
        public static bool Available(ArtPalette palette) => palette.leafCards != null && palette.needleCards != null;

        public static GameObject Tree(Kind kind, Transform parent, Vector3 at, float height, float yawDeg, int variant, ArtPalette palette)
        {
            var tree = new GameObject(kind == Kind.Conifer ? "Conifer" : "Tree");
            tree.transform.SetParent(parent, false);
            tree.transform.SetLocalPositionAndRotation(at, Quaternion.Euler(0f, yawDeg, 0f));
            tree.transform.localScale = Vector3.one * (height / Built);
            tree.AddComponent<MeshFilter>().sharedMesh = Mesh(kind, ((variant % Variants) + Variants) % Variants);
            var renderer = tree.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { palette.wood, kind == Kind.Conifer ? palette.needleCards : palette.leafCards };
            var lod = tree.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.012f, new Renderer[] { renderer }) });
            return tree;
        }

        static Mesh Mesh(Kind kind, int variant)
        {
            if (cache.TryGetValue((kind, variant), out var mesh) && mesh != null) return mesh;
            var random = new System.Random(7919 * (variant + 1) + (int)kind * 104729);
            var build = new Builder();
            if (kind == Kind.Conifer) Conifer(build, random);
            else Broadleaf(build, random);
            mesh = build.Mesh($"{kind} {variant}");
            cache[(kind, variant)] = mesh;
            return mesh;
        }

        /// <summary>A spruce-like conifer: a straight trunk with tiers of drooping needle sprays, long at the foot, short at the tip.</summary>
        static void Conifer(Builder b, System.Random random)
        {
            b.Trunk(Vector3.zero, Vector3.up * Built, 0.26f, 0.03f, 7);
            const float from = 1.6f;
            var tiers = 10 + random.Next(3);
            for (var tier = 0; tier < tiers; tier++)
            {
                var t = tier / (float)(tiers - 1);
                var y = Mathf.Lerp(from, Built - 0.9f, t);
                var length = Mathf.Lerp(2.9f, 0.55f, Mathf.Pow(t, 0.85f)) * (0.85f + 0.3f * (float)random.NextDouble());
                var count = 6 + random.Next(2);
                var turn = (float)random.NextDouble() * Mathf.PI * 2f;
                for (var i = 0; i < count; i++)
                {
                    var angle = turn + i * Mathf.PI * 2f / count + ((float)random.NextDouble() - 0.5f) * 0.4f;
                    var out_ = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    var droop = -0.25f - 0.25f * (1f - t) - 0.1f * (float)random.NextDouble();
                    var tip = (out_ + Vector3.up * droop).normalized * length;
                    var root = Vector3.up * y;
                    b.Spray(root, root + tip, length * 0.62f, out_);
                }
            }

            b.Spray(Vector3.up * (Built - 1.3f), Vector3.up * (Built + 0.2f), 0.6f, Vector3.forward); // the leader
        }

        /// <summary>A broadleaf tree: a trunk forking into boughs under a rounded crown of leafy sprays.</summary>
        static void Broadleaf(Builder b, System.Random random)
        {
            var fork = 3.6f + (float)random.NextDouble() * 0.8f;
            b.Trunk(Vector3.zero, Vector3.up * fork, 0.34f, 0.22f, 8);
            var crown = new Vector3(((float)random.NextDouble() - 0.5f) * 0.6f, 6.6f, ((float)random.NextDouble() - 0.5f) * 0.6f);
            var radii = new Vector3(3.3f, 2.9f, 3.3f) * (0.9f + 0.2f * (float)random.NextDouble());
            var boughs = 4 + random.Next(2);
            for (var i = 0; i < boughs; i++)
            {
                var angle = i * Mathf.PI * 2f / boughs + (float)random.NextDouble() * 0.8f;
                var end = crown + new Vector3(Mathf.Cos(angle) * radii.x * 0.6f, ((float)random.NextDouble() - 0.2f) * radii.y * 0.6f, Mathf.Sin(angle) * radii.z * 0.6f);
                b.Trunk(Vector3.up * (fork - 0.3f), end, 0.2f, 0.05f, 5);
            }

            for (var i = 0; i < 64; i++)
            {
                // Sprays over the crown's outer shell, facing every way, so it reads as a mass of leaves from anywhere.
                var direction = new Vector3((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 0.8f, (float)random.NextDouble() * 2f - 1f).normalized;
                var depth = 0.55f + 0.45f * (float)random.NextDouble();
                var centre = crown + Vector3.Scale(direction, radii) * depth;
                var size = 1.9f + (float)random.NextDouble() * 0.9f;
                var spin = (float)random.NextDouble() * Mathf.PI * 2f;
                b.Card(centre, direction, spin, size);
            }
        }

        /// <summary>Accumulates a two-part mesh: bark (sub-mesh 0) and foliage cards (sub-mesh 1).</summary>
        sealed class Builder
        {
            readonly List<Vector3> vertices = new();
            readonly List<Vector3> normals = new();
            readonly List<Vector2> uvs = new();
            readonly List<int> bark = new();
            readonly List<int> foliage = new();

            /// <summary>A tapering limb from <paramref name="a"/> to <paramref name="b"/> (the bark is textured by projection, so no UVs to speak of).</summary>
            public void Trunk(Vector3 a, Vector3 b, float radiusA, float radiusB, int sides)
            {
                var axis = (b - a).normalized;
                var side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                var other = Vector3.Cross(axis, side);
                var start = vertices.Count;
                for (var i = 0; i <= sides; i++)
                {
                    var angle = i * Mathf.PI * 2f / sides;
                    var normal = side * Mathf.Cos(angle) + other * Mathf.Sin(angle);
                    vertices.Add(a + normal * radiusA);
                    vertices.Add(b + normal * radiusB);
                    normals.Add(normal);
                    normals.Add(normal);
                    uvs.Add(new Vector2(i / (float)sides, 0f));
                    uvs.Add(new Vector2(i / (float)sides, 1f));
                }

                for (var i = 0; i < sides; i++)
                {
                    var v = start + i * 2;
                    bark.AddRange(new[] { v, v + 1, v + 3, v, v + 3, v + 2 });
                }
            }

            /// <summary>A needle spray from <paramref name="root"/> to <paramref name="tip"/>, as two crossed cards (flat and upright).</summary>
            public void Spray(Vector3 root, Vector3 tip, float width, Vector3 outward)
            {
                var along = (tip - root).normalized;
                var flat = Vector3.Cross(along, Vector3.up);
                if (flat.sqrMagnitude < 1e-4f) flat = Vector3.right;
                flat.Normalize();
                var upright = Vector3.Cross(flat, along).normalized;
                Quad(root, tip, flat * (width * 0.5f), (outward * 0.6f + Vector3.up * 0.8f).normalized);
                Quad(root, tip, upright * (width * 0.35f), (outward + Vector3.up * 0.3f).normalized);
            }

            /// <summary>A square leafy card centred on <paramref name="centre"/>, facing <paramref name="facing"/>, turned by <paramref name="spin"/>.</summary>
            public void Card(Vector3 centre, Vector3 facing, float spin, float size)
            {
                var side = Vector3.Cross(facing, Mathf.Abs(facing.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                var up = Vector3.Cross(side, facing);
                var u = (side * Mathf.Cos(spin) + up * Mathf.Sin(spin)) * (size * 0.5f);
                var v = (-side * Mathf.Sin(spin) + up * Mathf.Cos(spin)) * (size * 0.5f);
                Quad(centre - v, centre + v, u, facing);
            }

            /// <summary>A card from <paramref name="a"/> to <paramref name="b"/>, <paramref name="halfWidth"/> either side; its texture runs base (v 0) to tip (v 1).</summary>
            void Quad(Vector3 a, Vector3 b, Vector3 halfWidth, Vector3 normal)
            {
                var start = vertices.Count;
                vertices.Add(a - halfWidth);
                vertices.Add(a + halfWidth);
                vertices.Add(b + halfWidth);
                vertices.Add(b - halfWidth);
                for (var i = 0; i < 4; i++) normals.Add(normal);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(0f, 1f));
                foliage.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(bark, 0);
                mesh.SetTriangles(foliage, 1);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
