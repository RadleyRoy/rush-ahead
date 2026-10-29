using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// Placeholder 3D art: Unity primitives plus a couple of procedural meshes. Everything here is visual only;
    /// the 2D physics never sees it. Phase 8 replaces it with real models.
    /// </summary>
    public static class Shapes3D
    {
        public static Transform Group(Transform parent, string name, Vector3 localPosition = default)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localPosition = localPosition;
            return group;
        }

        public static GameObject Primitive(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale,
                                           Material material, Quaternion? localRotation = null, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name ?? type.ToString();
            Object.Destroy(go.GetComponent<Collider>()); // visuals only: gameplay physics is 2D
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(localPosition, localRotation ?? Quaternion.identity);
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        public static GameObject MeshObject(string name, Mesh mesh, Transform parent, Vector3 localPosition, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>A ring lying flat around the Y axis, like an inflatable tube.</summary>
        public static Mesh Torus(float radius, float tubeRadius, int segments = 40, int sides = 14)
        {
            var vertices = new Vector3[(segments + 1) * (sides + 1)];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            for (var i = 0; i <= segments; i++)
            {
                var u = 2f * Mathf.PI * i / segments;
                var around = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));
                for (var j = 0; j <= sides; j++)
                {
                    var v = 2f * Mathf.PI * j / sides;
                    var normal = around * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v);
                    var index = i * (sides + 1) + j;
                    vertices[index] = around * radius + normal * tubeRadius;
                    normals[index] = normal;
                    uvs[index] = new Vector2(i / (float)segments, j / (float)sides);
                }
            }

            var triangles = new int[segments * sides * 6];
            var t = 0;
            for (var i = 0; i < segments; i++)
            for (var j = 0; j < sides; j++)
            {
                int a = i * (sides + 1) + j, b = a + sides + 1, c = b + 1, d = a + 1;
                // Clockwise seen from outside: Unity's front face.
                triangles[t++] = a; triangles[t++] = d; triangles[t++] = c;
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
            }

            var mesh = new Mesh { name = "Torus", vertices = vertices, normals = normals, uv = uvs, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A flat ring segment between two hull-local angles (0 = bow along +Z, +90 = port along -X), coloured per
        /// vertex from <paramref name="inner"/> to <paramref name="outer"/>. UVs sit at the texture's centre, so a soft
        /// round texture contributes full opacity and the vertex colours alone do the fading.
        /// </summary>
        public static Mesh Sector(float innerRadius, float outerRadius, float fromDeg, float toDeg, Color inner, Color outer, int segments = 24)
        {
            var vertices = new Vector3[(segments + 1) * 2];
            var colors = new Color[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            for (var i = 0; i <= segments; i++)
            {
                var angle = Mathf.Lerp(fromDeg, toDeg, i / (float)segments) * Mathf.Deg2Rad;
                var direction = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                vertices[2 * i] = direction * innerRadius;
                vertices[2 * i + 1] = direction * outerRadius;
                colors[2 * i] = inner;
                colors[2 * i + 1] = outer;
                uvs[2 * i] = uvs[2 * i + 1] = new Vector2(0.5f, 0.5f);
            }

            var triangles = new int[segments * 6];
            for (var i = 0; i < segments; i++)
            {
                int innerNow = 2 * i, outerNow = innerNow + 1, innerNext = innerNow + 2, outerNext = innerNow + 3;
                // Increasing angle runs anticlockwise seen from above; these are wound clockwise (front-facing).
                triangles[i * 6] = innerNow; triangles[i * 6 + 1] = outerNext; triangles[i * 6 + 2] = outerNow;
                triangles[i * 6 + 3] = innerNow; triangles[i * 6 + 4] = innerNext; triangles[i * 6 + 5] = outerNext;
            }

            var mesh = new Mesh { name = "Sector", vertices = vertices, colors = colors, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A flat disc facing up. UVs run radially so a soft round texture fades out at the rim.</summary>
        public static Mesh Disc(float radius, int segments = 40)
        {
            var vertices = new Vector3[segments + 2];
            var uvs = new Vector2[vertices.Length];
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (var i = 0; i <= segments; i++)
            {
                var angle = 2f * Mathf.PI * i / segments;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices[i + 1] = new Vector3(dir.x, 0f, dir.y) * radius;
                uvs[i + 1] = new Vector2(0.5f, 0.5f) + dir * 0.5f;
            }

            var triangles = new int[segments * 3];
            for (var i = 0; i < segments; i++)
            {
                // Clockwise seen from above.
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 2;
                triangles[i * 3 + 2] = i + 1;
            }

            // White vertex colours: particle shaders (used for foam) multiply by vertex colour.
            var colors = new Color32[vertices.Length];
            for (var i = 0; i < colors.Length; i++) colors[i] = new Color32(255, 255, 255, 255);

            var mesh = new Mesh { name = "Disc", vertices = vertices, uv = uvs, colors32 = colors, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A cone standing on the origin, its point <paramref name="height"/> up: pine trees.</summary>
        public static Mesh Cone(float radius, float height, int segments = 12)
        {
            // Each side is its own triangle with its own normals, for the faceted look of the rest of the scenery.
            var vertices = new Vector3[segments * 3];
            var triangles = new int[segments * 3];
            var apex = new Vector3(0f, height, 0f);
            for (var i = 0; i < segments; i++)
            {
                var a = 2f * Mathf.PI * i / segments;
                var b = 2f * Mathf.PI * (i + 1) / segments;
                vertices[i * 3] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                vertices[i * 3 + 1] = apex;
                vertices[i * 3 + 2] = new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * radius;
                triangles[i * 3] = i * 3;
                triangles[i * 3 + 1] = i * 3 + 1;
                triangles[i * 3 + 2] = i * 3 + 2;
            }

            var mesh = new Mesh { name = "Cone", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
