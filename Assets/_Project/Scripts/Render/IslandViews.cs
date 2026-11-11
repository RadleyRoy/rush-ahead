using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// The islands: a low mound with a beach running under the water, the place's ground on top, a few trees and
    /// boulders, and a line of foam all round where the water meets it (whiter at the nose, where it parts).
    /// </summary>
    public static class IslandViews
    {
        const int Along = 32;
        const int Across = 12;
        const float Skirt = 0.9f; // how far the beach runs out under the water past the shore
        const float Top = 1.3f; // the mound's height at its widest, in metres above the water

        public static void Build(Course course, BankStyle style, ArtPalette palette, Transform parent, System.Random random)
        {
            if (course.Islands.Count == 0) return;
            var scene = course.Def.scene;
            var sand = palette.sand != null ? palette.sand : style.Lip;
            var beach = scene switch
            {
                CourseScene.Glacier => palette.Tinted(sand, new Color(0.86f, 0.9f, 0.98f)),
                CourseScene.Canyon => style.Ground,
                CourseScene.Rapids => style.Cliff,
                CourseScene.Swamp => palette.mud != null ? palette.mud : style.Cliff,
                _ => sand,
            };
            foreach (var island in course.Islands.All)
            {
                var root = Shapes3D.Group(parent, "Island");
                Shapes3D.MeshObject("Beach", Mound(island, 1f + Skirt / island.Spot.halfWidth, 0f), root, Vector3.zero, beach);
                Shapes3D.MeshObject("Ground", Mound(island, 0.72f, 0.06f), root, Vector3.zero, style.Ground);
                Shapes3D.MeshObject("Foam", Foam(island), root, Vector3.zero, palette.foam);
                Dress(island, scene, style, palette, root, random);
            }
        }

        /// <summary>The mound's height at a share <paramref name="r"/> of the way from its middle line to the shore (past 1 is under water).</summary>
        static float Height(Island island, float distance, float r)
        {
            var fullness = Mathf.Sqrt(island.HalfWidthAt(distance) / island.Spot.halfWidth);
            if (r < 1f) return 0.18f + Top * fullness * Mathf.Pow(1f - r * r, 0.6f);
            return Mathf.Lerp(0.18f, -0.45f, (r - 1f) * island.Spot.halfWidth / Skirt);
        }

        /// <summary>A grid laid over the island, out to <paramref name="reach"/> times its half-width, raised by <paramref name="lift"/>.</summary>
        static Mesh Mound(Island island, float reach, float lift)
        {
            var vertices = new Vector3[(Along + 1) * (Across + 1)];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[Along * Across * 6];
            for (var i = 0; i <= Along; i++)
            {
                var distance = island.Nose + island.Spot.length * i / Along;
                var w = Mathf.Max(island.HalfWidthAt(distance), 0.05f);
                for (var j = 0; j <= Across; j++)
                {
                    var v = j / (float)Across * 2f - 1f; // -1 the right side to +1 the left
                    var r = Mathf.Abs(v) * reach;
                    var across = v * reach * w;
                    var height = Height(island, distance, r) + lift;
                    var (middle, left) = island.Frame(distance);
                    var at = middle + left * across;
                    vertices[i * (Across + 1) + j] = SimSpace.ToWorld(at, height);
                    uvs[i * (Across + 1) + j] = at * 0.25f;
                }
            }

            var t = 0;
            for (var i = 0; i < Along; i++)
            for (var j = 0; j < Across; j++)
            {
                var a = i * (Across + 1) + j;
                var b = a + Across + 1;
                triangles[t++] = a;
                triangles[t++] = a + 1;
                triangles[t++] = b;
                triangles[t++] = b;
                triangles[t++] = a + 1;
                triangles[t++] = b + 1;
            }

            var mesh = new Mesh { name = "Island", vertices = vertices, uv = uvs, triangles = triangles };
            FaceUp(mesh);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Flips the winding if the grid came out facing down (it depends which way the river runs).</summary>
        static void FaceUp(Mesh mesh)
        {
            var v = mesh.vertices;
            var tris = mesh.triangles;
            var normal = Vector3.Cross(v[tris[1]] - v[tris[0]], v[tris[2]] - v[tris[0]]);
            if (normal.y >= 0f) return;
            for (var i = 0; i < tris.Length; i += 3) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            mesh.triangles = tris;
        }

        /// <summary>A band of foam round the shore at the waterline, widest at the nose where the river splits.</summary>
        static Mesh Foam(Island island)
        {
            var outline = island.Outline(Along);
            var count = outline.Length;
            var centre = Vector2.zero;
            foreach (var p in outline) centre += p;
            centre /= count;
            var nose = outline[0];
            var vertices = new Vector3[count * 2];
            var triangles = new int[count * 6];
            for (var i = 0; i < count; i++)
            {
                var prev = outline[(i + count - 1) % count];
                var next = outline[(i + 1) % count];
                var tangent = (next - prev).normalized;
                var outward = new Vector2(tangent.y, -tangent.x);
                if (Vector2.Dot(outward, outline[i] - centre) < 0f) outward = -outward;
                var width = 0.5f + 1.3f * Mathf.Clamp01(1f - Vector2.Distance(outline[i], nose) / 6f);
                vertices[i * 2] = SimSpace.ToWorld(outline[i] - outward * 0.2f, 0.05f);
                vertices[i * 2 + 1] = SimSpace.ToWorld(outline[i] + outward * width, 0.04f);
                var a = i * 2;
                var b = (i + 1) % count * 2;
                triangles[i * 6] = a;
                triangles[i * 6 + 1] = b;
                triangles[i * 6 + 2] = a + 1;
                triangles[i * 6 + 3] = a + 1;
                triangles[i * 6 + 4] = b;
                triangles[i * 6 + 5] = b + 1;
            }

            var mesh = new Mesh { name = "Island foam", vertices = vertices, triangles = triangles };
            FaceUp(mesh);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Trees down the island's spine (shrubs in the canyon), and boulders at its nose and tail.</summary>
        static void Dress(Island island, CourseScene scene, BankStyle style, ArtPalette palette, Transform parent, System.Random random)
        {
            var conifer = scene is CourseScene.Rapids or CourseScene.Glacier;
            var detail = Looks.Detail;
            for (var d = island.Nose + island.Spot.length * 0.22f; d < island.Tail - island.Spot.length * 0.2f; d += 7.5f)
            {
                if (random.NextDouble() > 0.55 + 0.45 * detail) continue;
                var (middle, side) = island.Frame(d);
                var w = island.HalfWidthAt(d);
                var spot = middle + side * (((float)random.NextDouble() * 2f - 1f) * w * 0.35f);
                var ground = Height(island, d, Vector2.Distance(spot, middle) / Mathf.Max(w, 0.1f)) - 0.1f;
                if (scene == CourseScene.Canyon && palette.shrubMeshes.Length > 0)
                {
                    var pick = random.Next(palette.shrubMeshes.Length);
                    Shapes3D.Model("Shrub", palette.shrubMeshes[pick], palette.shrubMaterials[pick], parent, SimSpace.ToWorld(spot, ground),
                        1.4f + (float)random.NextDouble(), (float)random.NextDouble() * 360f, 0.05f);
                }
                else if (Flora.Available(palette))
                {
                    var height = (conifer ? 6.5f : 5.5f) + 3f * (float)random.NextDouble();
                    Flora.Tree(conifer ? Flora.Kind.Conifer : Flora.Kind.Broadleaf, parent, SimSpace.ToWorld(spot, ground), height,
                        (float)random.NextDouble() * 360f, random.Next(Flora.Variants), palette);
                }
                else style.Plant(parent, spot, ground, palette, random);
            }

            if (palette.boulderMeshes.Length == 0) return;
            foreach (var t in new[] { 0.1f, 0.9f })
            {
                var d = island.Nose + island.Spot.length * t;
                var (middle, _) = island.Frame(d);
                var pick = random.Next(palette.boulderMeshes.Length);
                Shapes3D.Model("Boulder", palette.boulderMeshes[pick], palette.boulderMaterials[pick], parent, SimSpace.ToWorld(middle, 0.1f),
                    Mathf.Max(1.2f, island.HalfWidthAt(d) * 1.6f), (float)random.NextDouble() * 360f, 0.3f);
            }
        }
    }
}
