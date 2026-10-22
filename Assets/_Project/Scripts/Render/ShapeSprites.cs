using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// Placeholder art generated at runtime: anti-aliased white shapes, tinted per use with
    /// <see cref="SpriteRenderer.color"/>. Each sprite's pivot is the shape's local origin, so a sprite placed
    /// at a transform lines up exactly with the physics shape it represents. Phase 8 replaces all of it.
    /// </summary>
    public static class ShapeSprites
    {
        const int PixelsPerUnit = 64;
        const int Padding = 2;
        static readonly Dictionary<string, Sprite> Cache = new();

        public static Sprite Polygon(IReadOnlyList<Vector2> points)
        {
            var key = new StringBuilder("poly");
            foreach (var p in points) key.Append($":{p.x:0.###},{p.y:0.###}");
            return Cached(key.ToString(), () => Rasterize(Bounds(points), p => Contains(points, p)));
        }

        public static Sprite Circle(float radius) =>
            Cached($"circle:{radius:0.###}", () => Rasterize(new Rect(-radius, -radius, 2f * radius, 2f * radius), p => p.sqrMagnitude <= radius * radius));

        /// <summary>A unit square centred on its pivot. Scale it for bars and nozzles.</summary>
        public static Sprite Square => Polygon(new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f) });

        /// <summary>An air cone with its apex at the pivot, pointing along +x.</summary>
        public static Sprite Cone(float halfAngleDeg, float range)
        {
            const int arcSegments = 10;
            var points = new List<Vector2> { Vector2.zero };
            for (var i = 0; i <= arcSegments; i++)
            {
                var angle = Mathf.Lerp(-halfAngleDeg, halfAngleDeg, i / (float)arcSegments) * Mathf.Deg2Rad;
                points.Add(range * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            }

            return Polygon(points);
        }

        /// <summary>One water cell with faint edges, for a tiled background that makes motion visible.</summary>
        public static Sprite WaterCell(float cellSize, Color fill, Color line) =>
            Cached($"water:{cellSize}:{fill}:{line}", () =>
            {
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
                var pixels = new Color32[size * size];
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    pixels[y * size + x] = x < 2 || y < 2 ? line : fill;
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / cellSize, 0, SpriteMeshType.FullRect);
            });

        static Sprite Cached(string key, Func<Sprite> create)
        {
            if (!Cache.TryGetValue(key, out var sprite) || sprite == null)
                Cache[key] = sprite = create();
            return sprite;
        }

        static Rect Bounds(IReadOnlyList<Vector2> points)
        {
            Vector2 min = points[0], max = points[0];
            foreach (var p in points)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static Sprite Rasterize(Rect shapeBounds, Func<Vector2, bool> inside)
        {
            var width = Mathf.CeilToInt(shapeBounds.width * PixelsPerUnit) + 2 * Padding;
            var height = Mathf.CeilToInt(shapeBounds.height * PixelsPerUnit) + 2 * Padding;
            var origin = shapeBounds.min - Vector2.one * (Padding / (float)PixelsPerUnit);

            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                // 4x supersampling for soft edges.
                var covered = 0;
                for (var s = 0; s < 4; s++)
                {
                    var sample = origin + new Vector2(x + 0.25f + 0.5f * (s & 1), y + 0.25f + 0.5f * (s >> 1)) / PixelsPerUnit;
                    if (inside(sample)) covered++;
                }

                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(covered * 255 / 4));
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var pivot = new Vector2(-origin.x * PixelsPerUnit / width, -origin.y * PixelsPerUnit / height);
            return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        static bool Contains(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }

            return inside;
        }

        /// <summary>A sprite child with the given tint and sort order.</summary>
        public static SpriteRenderer Spawn(Transform parent, string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
