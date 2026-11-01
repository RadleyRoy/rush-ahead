using System;
using System.Collections.Generic;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// A developer's view of where things really hit (<c>-rushahead-hitzones</c>): thin lines on the water tracing every
    /// collider (solid things red, triggers cyan, rafts white) and, in yellow, each obstacle's scripted reach: the outline
    /// a raft's rim has to touch for its knock to land. For checking that what you see is what hits.
    /// </summary>
    public sealed class HitZoneOverlay : MonoBehaviour
    {
        const float Height = 0.2f;
        const float Width = 0.06f;

        Hazards hazards;
        Func<double> clock;
        readonly List<Vector2> solid = new();
        readonly List<Vector2> triggers = new();
        readonly List<Vector2> rafts = new();
        readonly List<Vector2> zones = new();
        readonly (Mesh mesh, List<Vector2> lines)[] layers = new (Mesh, List<Vector2>)[4];

        public static HitZoneOverlay Attach(Transform parent, Hazards hazards, Func<double> clock, ArtPalette palette)
        {
            var overlay = new GameObject("Hit zones").AddComponent<HitZoneOverlay>();
            overlay.transform.SetParent(parent, false);
            overlay.hazards = hazards;
            overlay.clock = clock;
            var colours = new[] { new Color(1f, 0.15f, 0.2f), new Color(0.1f, 0.95f, 1f), Color.white, new Color(1f, 0.9f, 0.1f) };
            var lists = new[] { overlay.solid, overlay.triggers, overlay.rafts, overlay.zones };
            for (var i = 0; i < 4; i++)
            {
                var mesh = new Mesh { name = "Hit zone lines" };
                mesh.MarkDynamic();
                var go = Shapes3D.MeshObject("Lines", mesh, overlay.transform, Vector3.zero, palette.Tinted(palette.sign, colours[i]));
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                overlay.layers[i] = (mesh, lists[i]);
            }

            return overlay;
        }

        void LateUpdate()
        {
            foreach (var (_, lines) in layers) lines.Clear();
            foreach (var collider in FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
            {
                if (!collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                var raft = collider.attachedRigidbody != null && collider.attachedRigidbody.GetComponent<BoatPhysics>() != null;
                Trace(collider, raft ? rafts : collider.isTrigger ? triggers : solid);
            }

            if (hazards != null && clock != null) hazards.Zones(clock(), zones);
            foreach (var (mesh, lines) in layers) Fill(mesh, lines);
        }

        /// <summary>Adds a collider's outline as lines.</summary>
        static void Trace(Collider2D collider, List<Vector2> lines)
        {
            var t = collider.transform;
            Vector2 World(Vector2 local) => t.TransformPoint(new Vector3(local.x, local.y, 0f));
            switch (collider)
            {
                case CircleCollider2D circle:
                    Obstacle.Circle(lines, World(circle.offset), circle.radius * Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.y)));
                    break;
                case BoxCollider2D box:
                {
                    var h = box.size * 0.5f;
                    var corners = new[] { new Vector2(h.x, h.y), new Vector2(-h.x, h.y), new Vector2(-h.x, -h.y), new Vector2(h.x, -h.y) };
                    for (var i = 0; i < 4; i++)
                    {
                        lines.Add(World(box.offset + corners[i]));
                        lines.Add(World(box.offset + corners[(i + 1) % 4]));
                    }

                    break;
                }
                case CapsuleCollider2D capsule:
                {
                    var horizontal = capsule.direction == CapsuleDirection2D.Horizontal;
                    var radius = (horizontal ? capsule.size.y : capsule.size.x) * 0.5f;
                    var half = Mathf.Max(0f, (horizontal ? capsule.size.x : capsule.size.y) * 0.5f - radius);
                    var axis = horizontal ? Vector2.right : Vector2.up;
                    var side = new Vector2(-axis.y, axis.x) * radius;
                    foreach (var end in new[] { -1f, 1f }) Obstacle.Circle(lines, World(capsule.offset + axis * (half * end)), radius * Mathf.Abs(t.lossyScale.x));
                    foreach (var s in new[] { -1f, 1f })
                    {
                        lines.Add(World(capsule.offset - axis * half + side * s));
                        lines.Add(World(capsule.offset + axis * half + side * s));
                    }

                    break;
                }
                case PolygonCollider2D polygon:
                    for (var p = 0; p < polygon.pathCount; p++)
                    {
                        var path = polygon.GetPath(p);
                        for (var i = 0; i < path.Length; i++)
                        {
                            lines.Add(World(polygon.offset + path[i]));
                            lines.Add(World(polygon.offset + path[(i + 1) % path.Length]));
                        }
                    }

                    break;
                case EdgeCollider2D edge:
                    for (var i = 0; i + 1 < edge.pointCount; i++)
                    {
                        lines.Add(World(edge.offset + edge.points[i]));
                        lines.Add(World(edge.offset + edge.points[i + 1]));
                    }

                    break;
            }
        }

        /// <summary>Each line as a thin flat strip lying on the water.</summary>
        static void Fill(Mesh mesh, List<Vector2> lines)
        {
            var vertices = new List<Vector3>(lines.Count * 2);
            var triangles = new List<int>(lines.Count * 3);
            for (var i = 0; i + 1 < lines.Count; i += 2)
            {
                var a = lines[i];
                var b = lines[i + 1];
                var d = b - a;
                if (d.sqrMagnitude < 1e-8f) continue;
                var side = new Vector2(-d.y, d.x).normalized * (Width * 0.5f);
                var v = vertices.Count;
                vertices.Add(SimSpace.ToWorld(a - side, Height));
                vertices.Add(SimSpace.ToWorld(a + side, Height));
                vertices.Add(SimSpace.ToWorld(b + side, Height));
                vertices.Add(SimSpace.ToWorld(b - side, Height));
                triangles.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3, v, v + 2, v + 1, v, v + 3, v + 2 }); // both faces
            }

            mesh.Clear();
            mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            var up = new Vector3[vertices.Count];
            for (var i = 0; i < up.Length; i++) up[i] = Vector3.up;
            mesh.SetNormals(up);
            mesh.RecalculateBounds();
        }
    }
}
