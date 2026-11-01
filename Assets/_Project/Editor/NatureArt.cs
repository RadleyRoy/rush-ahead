using System.Collections.Generic;
using System.IO;
using System.Linq;
using RushAhead.Render;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RushAhead.EditorTools
{
    /// <summary>
    /// The natural world's art (Phase 15), from the Poly Haven downloads: ground layers and a material for the terrain,
    /// scanned shrubs, mossy rocks, a boulder and a cliff face cut down to phone size with Unity's own mesh LOD tools,
    /// leafy sprays stamped from scanned leaves and needle sprays drawn in code for the trees, and mud for mudslides.
    /// Everything it makes is kept under Art/PolyHaven, so it's only built once.
    /// </summary>
    static class NatureArt
    {
        const string Root = "Assets/_Project/ThirdParty/PolyHaven";
        const string Out = "Assets/_Project/Art/PolyHaven";
        const string Meshes = Out + "/Meshes";
        const string Generated = Out + "/Generated";
        const string Ground = Out + "/Terrain";

        public static bool Available => AssetDatabase.IsValidFolder($"{Root}/shrub_02");

        public static void Apply(ArtPalette palette)
        {
            if (!Available) return;
            foreach (var folder in new[] { Meshes, Generated, Ground }) ContentBootstrap.EnsureFolder(folder);
            PrepareModels();

            palette.shrubMeshes = new[] { Simplified("shrub_02", 2500), Simplified("shrub_04", 2500), Simplified("weed_plant_02", 1500), Simplified("nettle_plant", 1500) };
            palette.shrubMaterials = new[] { Cutout("shrub_02"), Cutout("shrub_04"), Cutout("weed_plant_02"), Cutout("nettle_plant") };

            var boulders = new List<Mesh>();
            var boulderMaterials = new List<Material>();
            foreach (var set in new[] { "rock_moss_set_01", "rock_moss_set_02" })
            {
                var material = PolyHavenArt.Model(set, 0.15f);
                foreach (var (mesh, i) in Models(set).Take(3).Select((m, i) => (m, i)))
                {
                    boulders.Add(Simplified(set, 1500, mesh, $"{set}_{i}"));
                    boulderMaterials.Add(material);
                }
            }

            boulders.Add(Simplified("boulder_01", 3000));
            boulderMaterials.Add(PolyHavenArt.Model("boulder_01", 0.12f));
            palette.boulderMeshes = boulders.ToArray();
            palette.boulderMaterials = boulderMaterials.ToArray();
            palette.cliffMesh = Simplified("namaqualand_cliff_01", 5000);
            palette.cliffMaterial = PolyHavenArt.Model("namaqualand_cliff_01", 0.1f);

            palette.leafCards = Cards("Leaf cards", LeafSpray(), 0.18f);
            palette.needleCards = Cards("Needle cards", NeedleSpray(), 0.12f);
            palette.mud = PolyHavenArt.Triplanar("Mud", "brown_mud_02", 3f, 0.35f, new Color(0.5f, 0.5f, 0.5f));
            palette.terrainLayers = new[]
            {
                Layer("Grass", "aerial_grass_rock", 9f), Layer("Forest floor", "forrest_ground_01", 5f), Layer("Scree", "rocky_terrain_02", 6f),
                Layer("Moss", "mossy_rock", 5f), Layer("Mud", "brown_mud_leaves_01", 5f), Layer("Sand", "coast_sand_01", 6f),
            };
            palette.terrainMaterial = TerrainMaterial();
            EditorUtility.SetDirty(palette);
        }

        /// <summary>The new models import without materials and readable, so their meshes can be cut down here.</summary>
        static void PrepareModels()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer || importer.isReadable) continue;
                importer.isReadable = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.SaveAndReimport();
            }
        }

        static IEnumerable<Mesh> Models(string folder) =>
            AssetDatabase.LoadAllAssetsAtPath($"{Root}/{folder}/{folder}_1k.fbx").OfType<Mesh>().OrderByDescending(m => m.bounds.size.sqrMagnitude);

        /// <summary>A scanned mesh cut down to about <paramref name="maxTriangles"/> (see <see cref="MeshCutter"/>); the full scan stays out of the build.</summary>
        static Mesh Simplified(string folder, int maxTriangles, Mesh source = null, string name = null)
        {
            name ??= folder;
            var path = $"{Meshes}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            source ??= Models(folder).First();

            var mesh = MeshCutter.Cut(source, maxTriangles, name, out var before, out var after);
            mesh.UploadMeshData(true);
            AssetDatabase.CreateAsset(mesh, path);
            Debug.Log($"[RushAhead] {name}: {before} triangles cut to {after} ({mesh.vertexCount} vertices)");
            return mesh;
        }

        /// <summary>A plant's colour with its alpha mask folded in, cut out and seen from both sides.</summary>
        static Material Cutout(string folder)
        {
            var basePath = $"{Generated}/{folder}_base.png";
            if (!File.Exists(basePath))
            {
                var colour = Load($"{Root}/{folder}/{folder}_diff_1k.jpg");
                var alpha = Load($"{Root}/{folder}/{folder}_alpha_1k.jpg");
                var pixels = colour.GetPixels32();
                var mask = alpha.GetPixels32();
                for (var i = 0; i < pixels.Length && i < mask.Length; i++) pixels[i].a = mask[i].r;
                Save(basePath, colour.width, pixels);
                Object.DestroyImmediate(colour);
                Object.DestroyImmediate(alpha);
            }

            var material = PolyHavenArt.LoadOrCreate($"{Out}/{folder}.mat", "Universal Render Pipeline/Lit");
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/{folder}/{folder}_nor_gl_1k.jpg"));
            material.EnableKeyword("_NORMALMAP");
            CutOut(material, 0.2f);
            return material;
        }

        /// <summary>A card material for tree foliage: the generated spray, cut out, both sides.</summary>
        static Material Cards(string name, Texture2D spray, float smoothness)
        {
            var material = PolyHavenArt.LoadOrCreate($"{Out}/{name}.mat", "Universal Render Pipeline/Lit");
            material.SetTexture("_BaseMap", spray);
            material.SetColor("_BaseColor", Color.white);
            CutOut(material, smoothness);
            material.enableInstancing = true;
            return material;
        }

        static void CutOut(Material material, float smoothness)
        {
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.45f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            material.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
        }

        /// <summary>
        /// A leafy spray for broadleaf trees: a twig with leaves along it, each a real leaf cut from the scanned shrub's
        /// leaf sheet (found by flooding out from a point inside it), turned and sized at random.
        /// </summary>
        static Texture2D LeafSpray()
        {
            var path = $"{Generated}/leaf_spray.png";
            if (!File.Exists(path))
            {
                var colour = Load($"{Root}/shrub_02/shrub_02_diff_1k.jpg");
                var alpha = Load($"{Root}/shrub_02/shrub_02_alpha_1k.jpg");
                var source = colour.GetPixels32();
                var mask = alpha.GetPixels32();
                var width = colour.width;
                // Points inside three whole leaves on the sheet (x right, y up; the sheet is 1024 square). Only leaf-shaped
                // finds count: long and narrow, not a strip of the stem the sheet also holds.
                var leaves = new[] { new Vector2Int(585, 423), new Vector2Int(755, 373), new Vector2Int(905, 423) }
                    .Select(seed => Flood(mask, width, seed))
                    .Where(leaf => leaf.bounds.width > 20 && leaf.bounds.width < 220 && leaf.bounds.height > 250 && leaf.bounds.height < 700).ToArray();

                const int size = 512;
                var canvas = Enumerable.Repeat(new Color(0.36f, 0.42f, 0.27f, 0f), size * size).ToArray(); // leaf green where it's clear, so edges don't darken
                var random = new System.Random(5);
                var bark = new Color(0.33f, 0.24f, 0.16f, 1f);
                var twigFrom = new Vector2(size * 0.5f, 14f);
                var twigTo = new Vector2(size * 0.53f, size - 30f);
                Line(canvas, size, twigFrom, twigTo, 3.5f, bark);
                for (var i = 0; i < 30; i++)
                {
                    var t = 0.08f + 0.9f * i / 30f;
                    var root = Vector2.Lerp(twigFrom, twigTo, t);
                    var side = i % 2 == 0 ? 1f : -1f;
                    var angle = (90f - side * (35f + (float)random.NextDouble() * 35f)) * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var length = Mathf.Lerp(190f, 120f, t) * (0.85f + 0.3f * (float)random.NextDouble());
                    var leaf = leaves[random.Next(leaves.Length)];
                    var shade = 0.8f + 0.35f * (float)random.NextDouble();
                    Stamp(canvas, size, source, leaf, width, root, direction, length, shade);
                }

                var pixels = canvas.Select(c => (Color32)c).ToArray();
                Save(path, size, pixels, coverage: true);
                Object.DestroyImmediate(colour);
                Object.DestroyImmediate(alpha);
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>A needle spray for conifers, drawn needle by needle along a twig: long at the base, short at the tip.</summary>
        static Texture2D NeedleSpray()
        {
            var path = $"{Generated}/needle_spray.png";
            if (!File.Exists(path))
            {
                const int size = 512;
                var canvas = Enumerable.Repeat(new Color(0.14f, 0.25f, 0.12f, 0f), size * size).ToArray(); // needle green where it's clear
                var random = new System.Random(11);
                var from = new Vector2(size * 0.5f, 8f);
                var to = new Vector2(size * 0.5f, size - 12f);
                Line(canvas, size, from, to, 3f, new Color(0.3f, 0.22f, 0.15f, 1f));
                for (var i = 0; i < 520; i++)
                {
                    var t = (float)random.NextDouble();
                    var root = Vector2.Lerp(from, to, t) + new Vector2(((float)random.NextDouble() - 0.5f) * 6f, 0f);
                    var side = random.NextDouble() < 0.5 ? -1f : 1f;
                    var angle = (90f - side * (38f + (float)random.NextDouble() * 34f)) * Mathf.Deg2Rad;
                    var length = Mathf.Lerp(215f, 70f, t) * (0.7f + 0.45f * (float)random.NextDouble());
                    var tip = root + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length;
                    var green = new Color(0.1f + 0.08f * (float)random.NextDouble(), 0.2f + 0.12f * (float)random.NextDouble(), 0.09f + 0.06f * (float)random.NextDouble(), 1f);
                    Line(canvas, size, root, tip, 1.3f, green);
                }

                Save(path, size, canvas.Select(c => (Color32)c).ToArray(), coverage: true);
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>The pixels of the leaf under <paramref name="seed"/>: every opaque pixel joined to it, and their bounds.</summary>
        static (RectInt bounds, HashSet<int> pixels) Flood(Color32[] mask, int width, Vector2Int seed)
        {
            var height = mask.Length / width;
            bool Solid(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && mask[y * width + x].r > 128;
            // Find solid ground near the seed, in case it fell on a speck.
            for (var r = 0; r < 30 && !Solid(seed.x, seed.y); r++) seed += new Vector2Int(r % 2 == 0 ? 1 : 0, r % 2 == 0 ? 0 : 1);
            var pixels = new HashSet<int>();
            if (!Solid(seed.x, seed.y)) return (new RectInt(0, 0, 0, 0), pixels);
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(seed);
            pixels.Add(seed.y * width + seed.x);
            int minX = seed.x, maxX = seed.x, minY = seed.y, maxY = seed.y;
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
                foreach (var n in new[] { p + Vector2Int.left, p + Vector2Int.right, p + Vector2Int.up, p + Vector2Int.down })
                    if (Solid(n.x, n.y) && pixels.Add(n.y * width + n.x)) queue.Enqueue(n);
            }

            return (new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1), pixels);
        }

        /// <summary>Draws one leaf from the sheet onto the canvas: its stalk at <paramref name="root"/>, pointing along <paramref name="direction"/>.</summary>
        static void Stamp(Color[] canvas, int size, Color32[] sheet, (RectInt bounds, HashSet<int> pixels) leaf, int sheetWidth, Vector2 root, Vector2 direction,
                          float length, float shade)
        {
            var b = leaf.bounds;
            var width = length * b.width / b.height;
            var across = new Vector2(direction.y, -direction.x);
            var corners = new[] { root + across * (width * 0.5f), root - across * (width * 0.5f), root + direction * length + across * (width * 0.5f), root + direction * length - across * (width * 0.5f) };
            var lo = Vector2Int.Max(Vector2Int.zero, Vector2Int.FloorToInt(corners.Aggregate(Vector2.Min)));
            var hi = Vector2Int.Min(new Vector2Int(size - 1, size - 1), Vector2Int.CeilToInt(corners.Aggregate(Vector2.Max)));
            for (var y = lo.y; y <= hi.y; y++)
            for (var x = lo.x; x <= hi.x; x++)
            {
                var offset = new Vector2(x, y) - root;
                var u = Vector2.Dot(offset, across) / width + 0.5f;
                var v = Vector2.Dot(offset, direction) / length;
                if (u < 0f || u > 1f || v < 0f || v > 1f) continue;
                var sx = Mathf.Clamp(Mathf.RoundToInt(b.x + u * (b.width - 1)), 0, sheetWidth - 1);
                var sy = Mathf.Clamp(Mathf.RoundToInt(b.y + v * (b.height - 1)), 0, sheet.Length / sheetWidth - 1);
                var index = sy * sheetWidth + sx;
                if (!leaf.pixels.Contains(index)) continue;
                Color c = sheet[index];
                c = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
                canvas[y * size + x] = c; // leaves are opaque: the newer one on top
            }
        }

        /// <summary>An anti-aliased line of the given half-thickness, drawn over what's there.</summary>
        static void Line(Color[] canvas, int size, Vector2 a, Vector2 b, float halfWidth, Color colour)
        {
            var lo = Vector2Int.Max(Vector2Int.zero, Vector2Int.FloorToInt(Vector2.Min(a, b) - Vector2.one * (halfWidth + 1f)));
            var hi = Vector2Int.Min(new Vector2Int(size - 1, size - 1), Vector2Int.CeilToInt(Vector2.Max(a, b) + Vector2.one * (halfWidth + 1f)));
            var ab = b - a;
            var lengthSq = Mathf.Max(1e-4f, ab.sqrMagnitude);
            for (var y = lo.y; y <= hi.y; y++)
            for (var x = lo.x; x <= hi.x; x++)
            {
                var p = new Vector2(x, y);
                var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
                var d = Vector2.Distance(p, a + ab * t);
                var cover = Mathf.Clamp01(halfWidth + 0.5f - d);
                if (cover <= 0f) continue;
                var i = y * size + x;
                var under = canvas[i];
                canvas[i] = new Color(Mathf.Lerp(under.r, colour.r, cover), Mathf.Lerp(under.g, colour.g, cover), Mathf.Lerp(under.b, colour.b, cover),
                    Mathf.Max(under.a, cover));
            }
        }

        static Texture2D Load(string path)
        {
            var texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(path));
            return texture;
        }

        /// <summary>Writes a PNG and imports it as a cut-out texture whose mip levels keep their coverage (so far trees don't go thin).</summary>
        static void Save(string path, int width, Color32[] pixels, bool coverage = false)
        {
            var texture = new Texture2D(width, pixels.Length / width, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 1024;
            importer.mipMapsPreserveCoverage = coverage;
            importer.alphaTestReferenceValue = 0.45f;
            importer.wrapMode = TextureWrapMode.Clamp;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 512;
            android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }

        static TerrainLayer Layer(string name, string set, float metresPerTile)
        {
            var path = $"{Ground}/{name}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }

            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/{set}/{set}_diff_1k.jpg");
            layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/{set}/{set}_nor_gl_1k.jpg");
            layer.normalScale = 1f;
            layer.tileSize = new Vector2(metresPerTile, metresPerTile);
            layer.smoothness = 0.05f;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        static Material TerrainMaterial()
        {
            var material = PolyHavenArt.LoadOrCreate($"{Ground}/Terrain.mat", "Universal Render Pipeline/Terrain/Lit");
            material.EnableKeyword("_NORMALMAP");
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
