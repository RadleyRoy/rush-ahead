using System.Collections.Generic;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// A smooth sculpted shape: simple solids (spheres, ellipsoids, tapered capsules, rounded boxes) melted together
    /// with signed distance functions, and meshed once (surface nets) into a single smooth skin. Each solid paints a
    /// region (fur, pale fur, dark, skin), carried in the vertex colours so one material can tint every animal. Built
    /// at load and cached by whoever asks for it.
    /// </summary>
    public sealed class Sculpture
    {
        /// <summary>The colour regions, as vertex colour channels: fur (r), pale (g), dark (b), skin (a).</summary>
        public enum Region { Fur, Pale, Dark, Skin }

        delegate float Field(Vector3 p);

        readonly List<(Field field, Region region, float blend, bool carve)> parts = new();
        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;

        public Sculpture Sphere(Vector3 centre, float radius, Region region = Region.Fur, float blend = 0.05f) =>
            Add(p => (p - centre).magnitude - radius, centre, Vector3.one * radius, region, blend);

        /// <summary>An ellipsoid with radii <paramref name="radii"/>, turned by <paramref name="turn"/> (degrees).</summary>
        public Sculpture Ellipsoid(Vector3 centre, Vector3 radii, Region region = Region.Fur, float blend = 0.05f, Vector3 turn = default)
        {
            var inverse = Quaternion.Inverse(Quaternion.Euler(turn));
            var smallest = Mathf.Min(radii.x, Mathf.Min(radii.y, radii.z));
            return Add(p =>
            {
                var q = inverse * (p - centre);
                var k0 = new Vector3(q.x / radii.x, q.y / radii.y, q.z / radii.z).magnitude;
                var k1 = new Vector3(q.x / (radii.x * radii.x), q.y / (radii.y * radii.y), q.z / (radii.z * radii.z)).magnitude;
                return k1 > 1e-6f ? k0 * (k0 - 1f) / k1 : -smallest;
            }, centre, Vector3.one * Mathf.Max(radii.x, Mathf.Max(radii.y, radii.z)), region, blend);
        }

        /// <summary>A capsule from <paramref name="a"/> (radius <paramref name="ra"/>) tapering to <paramref name="b"/> (radius <paramref name="rb"/>).</summary>
        public Sculpture Limb(Vector3 a, Vector3 b, float ra, float rb, Region region = Region.Fur, float blend = 0.05f)
        {
            var ab = b - a;
            var lengthSq = Mathf.Max(1e-6f, ab.sqrMagnitude);
            var r = Mathf.Max(ra, rb);
            return Add(p =>
            {
                var t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / lengthSq);
                return (p - (a + ab * t)).magnitude - Mathf.Lerp(ra, rb, t);
            }, (a + b) * 0.5f, Vector3.one * (ab.magnitude * 0.5f + r), region, blend);
        }

        /// <summary>A box with rounded edges (<paramref name="rounding"/>), turned by <paramref name="turn"/> (degrees).</summary>
        public Sculpture Box(Vector3 centre, Vector3 size, float rounding, Region region = Region.Fur, float blend = 0.05f, Vector3 turn = default)
        {
            var inverse = Quaternion.Inverse(Quaternion.Euler(turn));
            var half = size * 0.5f - Vector3.one * rounding;
            return Add(p =>
            {
                var q = inverse * (p - centre);
                var d = new Vector3(Mathf.Abs(q.x) - half.x, Mathf.Abs(q.y) - half.y, Mathf.Abs(q.z) - half.z);
                var outside = new Vector3(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f), Mathf.Max(d.z, 0f)).magnitude;
                return outside + Mathf.Min(Mathf.Max(d.x, Mathf.Max(d.y, d.z)), 0f) - rounding;
            }, centre, size * 0.5f, region, blend);
        }

        /// <summary>Cuts a sphere out (an eye socket, a mouth), softening the rim by <paramref name="blend"/>.</summary>
        public Sculpture CarveSphere(Vector3 centre, float radius, float blend = 0.02f)
        {
            parts.Add((p => (p - centre).magnitude - radius, Region.Dark, blend, true));
            return this;
        }

        Sculpture Add(Field field, Vector3 centre, Vector3 extent, Region region, float blend)
        {
            parts.Add((field, region, blend, false));
            min = Vector3.Min(min, centre - extent);
            max = Vector3.Max(max, centre + extent);
            return this;
        }

        /// <summary>Signed distance to the whole shape: the solids melted together, carvings taken out.</summary>
        public float Distance(Vector3 p)
        {
            var d = float.MaxValue;
            foreach (var (field, _, blend, carve) in parts)
            {
                var f = field(p);
                d = carve ? SmoothMax(d, -f, blend) : d == float.MaxValue ? f : SmoothMin(d, f, blend);
            }

            return d;
        }

        /// <summary>The region colours at a point on the skin: each solid paints the skin it makes, blending at the seams.</summary>
        Color Paint(Vector3 p)
        {
            var weights = new float[4];
            var total = 0f;
            foreach (var (field, region, blend, carve) in parts)
            {
                if (carve) continue;
                // Deeper inside a solid paints more strongly: a belly patch or a mask laid over the body wins where it shows.
                var w = Mathf.Exp(-Mathf.Max(-0.05f, field(p)) / Mathf.Max(0.004f, blend * 0.35f));
                weights[(int)region] += w;
                total += w;
            }

            return total <= 0f ? new Color(1f, 0f, 0f, 0f) : new Color(weights[0] / total, weights[1] / total, weights[2] / total, weights[3] / total);
        }

        static float SmoothMin(float a, float b, float k)
        {
            if (k <= 0f) return Mathf.Min(a, b);
            var h = Mathf.Max(k - Mathf.Abs(a - b), 0f) / k;
            return Mathf.Min(a, b) - h * h * k * 0.25f;
        }

        static float SmoothMax(float a, float b, float k) => -SmoothMin(-a, -b, k);

        /// <summary>
        /// Meshes the shape on a grid of <paramref name="cell"/>-sized cubes (surface nets: one vertex per cube the skin
        /// passes through, at the average of its edge crossings; one quad per grid edge it crosses), with normals from the
        /// distance field's slope and region colours painted in.
        /// </summary>
        public Mesh Mesh(string name, float cell)
        {
            var origin = min - Vector3.one * (cell * 2f + 0.1f);
            var span = max - min + Vector3.one * (cell * 4f + 0.2f);
            var nx = Mathf.CeilToInt(span.x / cell) + 1;
            var ny = Mathf.CeilToInt(span.y / cell) + 1;
            var nz = Mathf.CeilToInt(span.z / cell) + 1;
            var field = new float[nx * ny * nz];
            int Index(int x, int y, int z) => (z * ny + y) * nx + x;
            Vector3 At(int x, int y, int z) => origin + new Vector3(x, y, z) * cell;
            for (var z = 0; z < nz; z++)
            for (var y = 0; y < ny; y++)
            for (var x = 0; x < nx; x++)
                field[Index(x, y, z)] = Distance(At(x, y, z));

            var vertexOf = new Dictionary<int, int>();
            var vertices = new List<Vector3>();
            var corners = new Vector3Int[8];
            for (var i = 0; i < 8; i++) corners[i] = new Vector3Int(i & 1, (i >> 1) & 1, (i >> 2) & 1);
            var edges = new[] { (0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7) };
            var values = new float[8];
            for (var z = 0; z < nz - 1; z++)
            for (var y = 0; y < ny - 1; y++)
            for (var x = 0; x < nx - 1; x++)
            {
                var inside = 0;
                for (var i = 0; i < 8; i++)
                {
                    values[i] = field[Index(x + corners[i].x, y + corners[i].y, z + corners[i].z)];
                    if (values[i] < 0f) inside++;
                }

                if (inside == 0 || inside == 8) continue;
                var sum = Vector3.zero;
                var crossings = 0;
                foreach (var (a, b) in edges)
                {
                    if ((values[a] < 0f) == (values[b] < 0f)) continue;
                    var t = values[a] / (values[a] - values[b]);
                    sum += Vector3.Lerp(corners[a], corners[b], t);
                    crossings++;
                }

                vertexOf[Index(x, y, z)] = vertices.Count;
                vertices.Add(origin + (new Vector3(x, y, z) + sum / crossings) * cell);
            }

            var triangles = new List<int>();
            void Quad(int a, int b, int c, int d, bool flip)
            {
                if (flip) triangles.AddRange(new[] { a, c, b, a, d, c });
                else triangles.AddRange(new[] { a, b, c, a, c, d });
            }

            for (var z = 1; z < nz - 1; z++)
            for (var y = 1; y < ny - 1; y++)
            for (var x = 1; x < nx - 1; x++)
            {
                var here = field[Index(x, y, z)] < 0f;
                // Along x: the four cubes round the edge from (x,y,z) to (x+1,y,z).
                if (x < nx - 1 && here != (field[Index(x + 1, y, z)] < 0f) &&
                    vertexOf.TryGetValue(Index(x, y - 1, z - 1), out var a0) && vertexOf.TryGetValue(Index(x, y, z - 1), out var a1) &&
                    vertexOf.TryGetValue(Index(x, y, z), out var a2) && vertexOf.TryGetValue(Index(x, y - 1, z), out var a3))
                    Quad(a0, a1, a2, a3, !here);
                if (y < ny - 1 && here != (field[Index(x, y + 1, z)] < 0f) &&
                    vertexOf.TryGetValue(Index(x - 1, y, z - 1), out var b0) && vertexOf.TryGetValue(Index(x - 1, y, z), out var b1) &&
                    vertexOf.TryGetValue(Index(x, y, z), out var b2) && vertexOf.TryGetValue(Index(x, y, z - 1), out var b3))
                    Quad(b0, b1, b2, b3, !here);
                if (z < nz - 1 && here != (field[Index(x, y, z + 1)] < 0f) &&
                    vertexOf.TryGetValue(Index(x - 1, y - 1, z), out var c0) && vertexOf.TryGetValue(Index(x, y - 1, z), out var c1) &&
                    vertexOf.TryGetValue(Index(x, y, z), out var c2) && vertexOf.TryGetValue(Index(x - 1, y, z), out var c3))
                    Quad(c0, c1, c2, c3, !here);
            }

            var normals = new Vector3[vertices.Count];
            var colours = new Color[vertices.Count];
            var e = cell * 0.5f;
            for (var i = 0; i < vertices.Count; i++)
            {
                var p = vertices[i];
                normals[i] = new Vector3(Distance(p + Vector3.right * e) - Distance(p - Vector3.right * e), Distance(p + Vector3.up * e) - Distance(p - Vector3.up * e),
                    Distance(p + Vector3.forward * e) - Distance(p - Vector3.forward * e)).normalized;
                colours[i] = Paint(p);
            }

            // Face the triangles outward: if most of them face against the field's slope, the winding is backwards.
            var against = 0;
            for (var t = 0; t + 2 < triangles.Count; t += 3)
            {
                var face = Vector3.Cross(vertices[triangles[t + 1]] - vertices[triangles[t]], vertices[triangles[t + 2]] - vertices[triangles[t]]);
                if (Vector3.Dot(face, normals[triangles[t]]) < 0f) against++;
            }

            if (against * 2 > triangles.Count / 3)
                for (var t = 0; t + 2 < triangles.Count; t += 3)
                    (triangles[t + 1], triangles[t + 2]) = (triangles[t + 2], triangles[t + 1]);

            var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
