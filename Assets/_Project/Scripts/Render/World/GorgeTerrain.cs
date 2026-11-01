using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// The land round a course: a Unity terrain shaped from the river when the course loads (the same on every device).
    /// The river runs along the top of a ridge; its cliffs fall to a valley floor with scree heaped at their feet, open
    /// near the cliffs and rolling up into wooded hills further off, painted by what the ground is doing there: scree on
    /// the heaps and steep slopes, earth and leaf litter under the trees, grass in the open, moss and rock on the hills.
    /// </summary>
    public static class GorgeTerrain
    {
        const int Resolution = 257; // heightmap samples a side (a power of two plus one)
        const int SplatResolution = 128;
        const float Margin = 160f; // how far the land reaches past the river
        const float Talus = 5f; // how high scree heaps against the cliff foot
        const float TalusWidth = 10f;
        const float HillsFrom = 28f; // metres out from the cliff foot where the land starts to rise
        const float HillHeight = 34f;

        /// <summary>The ground, for planting things on.</summary>
        public sealed class Land
        {
            public Terrain Terrain;
            internal float[,] edge; // metres out from the cliff foot at each heightmap sample (negative under the ridge)
            internal Vector2 min;
            internal Vector2 cell;

            /// <summary>The height of the ground at a point on the water plane.</summary>
            public float Height(Vector2 at) => Terrain.SampleHeight(SimSpace.ToWorld(at)) + Terrain.transform.position.y;

            /// <summary>How far a point is out from the foot of the cliffs (negative under the ridge the river runs on).</summary>
            public float FromCliff(Vector2 at)
            {
                var x = Mathf.Clamp(Mathf.RoundToInt((at.x - min.x) / cell.x), 0, Resolution - 1);
                var y = Mathf.Clamp(Mathf.RoundToInt((at.y - min.y) / cell.y), 0, Resolution - 1);
                return edge[y, x];
            }

            /// <summary>How steep the ground is there, in degrees.</summary>
            public float Steepness(Vector2 at)
            {
                var size = Terrain.terrainData.size;
                return Terrain.terrainData.GetSteepness((at.x - min.x) / size.x, (at.y - min.y) / size.z);
            }

            public bool Contains(Vector2 at) => at.x > min.x && at.y > min.y && at.x < min.x + cell.x * (Resolution - 1) && at.y < min.y + cell.y * (Resolution - 1);
        }

        /// <summary>Ground layers by role, from the palette; a style picks four of them (one pass on phones).</summary>
        public enum Ground { Grass, Forest, Scree, Moss, Mud, Sand }

        /// <param name="foot">How far out from the river's edge the foot of the cliff is.</param>
        public static Land Build(Course course, BankStyle style, ArtPalette palette, Transform parent, float foot)
        {
            var river = course.River;
            var samples = river.Samples;
            var min = Vector2.positiveInfinity;
            var max = Vector2.negativeInfinity;
            foreach (var point in river.Outline())
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            min -= Vector2.one * Margin;
            max += Vector2.one * Margin;
            var span = max - min;
            var cell = span / (Resolution - 1);

            // Nearest river sample for every heightmap point: seed round each sample, then sweep forward and back (a
            // feature transform; exact enough at 1.5 m sample spacing), all in a few milliseconds.
            var nearest = new int[Resolution, Resolution];
            var best = new float[Resolution, Resolution];
            for (var y = 0; y < Resolution; y++)
            for (var x = 0; x < Resolution; x++)
            {
                nearest[y, x] = -1;
                best[y, x] = float.MaxValue;
            }

            Vector2 Point(int x, int y) => min + new Vector2(x * cell.x, y * cell.y);
            void Offer(int x, int y, int k)
            {
                if (k < 0 || x < 0 || y < 0 || x >= Resolution || y >= Resolution) return;
                var d = (Point(x, y) - samples[k].Centre).sqrMagnitude;
                if (d >= best[y, x]) return;
                best[y, x] = d;
                nearest[y, x] = k;
            }

            for (var k = 0; k < samples.Count; k++)
            {
                var c = samples[k].Centre - min;
                int cx = Mathf.RoundToInt(c.x / cell.x), cy = Mathf.RoundToInt(c.y / cell.y);
                for (var dy = -3; dy <= 3; dy++)
                for (var dx = -3; dx <= 3; dx++)
                    Offer(cx + dx, cy + dy, k);
            }

            for (var pass = 0; pass < 2; pass++)
            {
                for (var y = 0; y < Resolution; y++)
                for (var x = 0; x < Resolution; x++)
                {
                    if (x > 0) Offer(x, y, nearest[y, x - 1]);
                    if (y > 0)
                    {
                        Offer(x, y, nearest[y - 1, x]);
                        if (x > 0) Offer(x, y, nearest[y - 1, x - 1]);
                        if (x < Resolution - 1) Offer(x, y, nearest[y - 1, x + 1]);
                    }
                }

                for (var y = Resolution - 1; y >= 0; y--)
                for (var x = Resolution - 1; x >= 0; x--)
                {
                    if (x < Resolution - 1) Offer(x, y, nearest[y, x + 1]);
                    if (y < Resolution - 1)
                    {
                        Offer(x, y, nearest[y + 1, x]);
                        if (x < Resolution - 1) Offer(x, y, nearest[y + 1, x + 1]);
                        if (x > 0) Offer(x, y, nearest[y + 1, x - 1]);
                    }
                }
            }

            // The shape of the land.
            var floor = -style.Drop;
            var bottom = floor - 4f;
            var top = floor + Talus + HillHeight * 1.3f + 6f;
            var edge = new float[Resolution, Resolution];
            var heights = new float[Resolution, Resolution];
            for (var y = 0; y < Resolution; y++)
            for (var x = 0; x < Resolution; x++)
            {
                var k = nearest[y, x];
                var out_ = k < 0 ? Margin : Mathf.Sqrt(best[y, x]) - samples[k].HalfWidth - foot;
                edge[y, x] = out_;
                var p = Point(x, y);
                heights[y, x] = (GroundHeight(out_, p, floor) - bottom) / (top - bottom);
            }

            var data = new TerrainData { heightmapResolution = Resolution, name = "Gorge" };
            data.size = new Vector3(span.x, top - bottom, span.y);
            data.SetHeights(0, 0, heights);

            // Paint it: four layers, so phones draw it in one pass.
            var layers = Layers(course.Def.scene, palette);
            data.terrainLayers = layers.terrain;
            data.alphamapResolution = SplatResolution;
            var weights = new float[SplatResolution, SplatResolution, 4];
            for (var y = 0; y < SplatResolution; y++)
            for (var x = 0; x < SplatResolution; x++)
            {
                var hx = Mathf.RoundToInt(x / (SplatResolution - 1f) * (Resolution - 1));
                var hy = Mathf.RoundToInt(y / (SplatResolution - 1f) * (Resolution - 1));
                var p = Point(hx, hy);
                var slope = data.GetSteepness(hx / (Resolution - 1f), hy / (Resolution - 1f));
                Paint(layers.roles, edge[hy, hx], slope, p, x, y, weights);
            }

            data.SetAlphamaps(0, 0, weights);
            data.baseMapResolution = 128;

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Gorge land";
            Object.Destroy(go.GetComponent<TerrainCollider>()); // the game's physics is 2D
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(min.x, bottom, min.y);
            var terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = palette.terrainMaterial;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 6f;
            terrain.basemapDistance = 140f;
            terrain.drawTreesAndFoliage = false;
            return new Land { Terrain = terrain, edge = edge, min = min, cell = cell };
        }

        /// <summary>The ground's height <paramref name="out_"/> metres out from the cliff foot.</summary>
        static float GroundHeight(float out_, Vector2 p, float floor)
        {
            if (out_ < 0f) return floor + Talus; // under the ridge: hidden by the cliffs, level with the scree against them
            var heap = 1f - out_ / TalusWidth;
            var talus = heap > 0f ? Talus * heap * heap : 0f;
            var ripple = (Mathf.PerlinNoise(p.x * 0.06f + 11f, p.y * 0.06f + 5f) - 0.5f) * 1.4f;
            var rise = Mathf.Clamp01((out_ - HillsFrom) / 95f);
            rise = rise * rise * (3f - 2f * rise);
            var hills = rise * HillHeight * (0.35f + 0.85f * Mathf.PerlinNoise(p.x * 0.011f + 3.1f, p.y * 0.011f + 7.7f));
            var knolls = rise * 4f * (Mathf.PerlinNoise(p.x * 0.045f + 1.3f, p.y * 0.045f + 9.1f) - 0.5f);
            return floor + talus + ripple + hills + knolls;
        }

        static (TerrainLayer[] terrain, Ground[] roles) Layers(CourseScene scene, ArtPalette palette)
        {
            var roles = scene switch
            {
                CourseScene.Lake => new[] { Ground.Grass, Ground.Sand, Ground.Mud, Ground.Moss },
                CourseScene.Rapids => new[] { Ground.Forest, Ground.Scree, Ground.Moss, Ground.Grass },
                _ => new[] { Ground.Grass, Ground.Forest, Ground.Scree, Ground.Mud },
            };
            var layers = new TerrainLayer[roles.Length];
            for (var i = 0; i < roles.Length; i++) layers[i] = palette.TerrainLayerFor(roles[i]);
            return (layers, roles);
        }

        /// <summary>How much of each of the four layers covers one spot.</summary>
        static void Paint(Ground[] roles, float out_, float slope, Vector2 p, int x, int y, float[,,] weights)
        {
            var noise = Mathf.PerlinNoise(p.x * 0.05f + 2.2f, p.y * 0.05f + 8.4f);
            var patch = Mathf.PerlinNoise(p.x * 0.13f + 5.5f, p.y * 0.13f + 1.7f);
            var scree = Mathf.Clamp01(1f - out_ / (TalusWidth + 2f)) + Mathf.Clamp01((slope - 26f) / 12f);
            var woods = Mathf.Clamp01(1f - Mathf.Abs(out_ - 26f) / 22f) * (0.4f + noise);
            var hilltop = Mathf.Clamp01((out_ - HillsFrom - 30f) / 60f);
            var total = 0f;
            for (var i = 0; i < roles.Length; i++)
            {
                var w = roles[i] switch
                {
                    Ground.Scree => scree * 1.4f,
                    Ground.Forest => woods + hilltop * 0.5f * noise,
                    Ground.Grass => 0.55f + 0.5f * (1f - noise) - woods * 0.4f,
                    Ground.Moss => Mathf.Clamp01((slope - 14f) / 20f) * 0.7f + hilltop * patch * 0.6f,
                    Ground.Mud => Mathf.Clamp01(patch - 0.62f) * 2.5f + scree * 0.2f,
                    Ground.Sand => Mathf.Clamp01(1f - out_ / 16f) * 1.2f + Mathf.Clamp01(patch - 0.7f) * 2f,
                    _ => 0f,
                };
                w = Mathf.Max(0f, w);
                weights[y, x, i] = w;
                total += w;
            }

            if (total <= 1e-4f)
            {
                weights[y, x, 0] = 1f;
                return;
            }

            for (var i = 0; i < roles.Length; i++) weights[y, x, i] /= total;
        }
    }
}
