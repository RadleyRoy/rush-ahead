using System.Collections.Generic;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// How a river course looks: a river running along a high ridge, with a stone lip at each side and cliffs dropping
    /// to a wooded gorge far below (built as meshes that follow the river's curve exactly, so the lip is where a raft
    /// goes over), waterfalls spilling off the sides and the end, rocks with foam, floating barrels, an arch over
    /// every checkpoint that lights up as the next one and turns green once passed, a chequered finish, and drifting
    /// foam chevrons wherever the water flows. Purely visual: the physics is the course's.
    /// </summary>
    public sealed class CourseView : MonoBehaviour
    {
        const float RimWidth = 0.8f; // the stone lip between the water and the drop
        const float RimTop = 0.07f;
        const float WaterUnderLip = 0.25f;
        const float LedgeDepth = 2.5f; // the steep top of the cliff, before it leans out
        const float MaxLean = 4f;
        const float FallLip = 1.5f; // how far past the river's end the water runs before it pours off
        const float FloorMargin = 70f;
        const float GateHeight = 7.2f; // high enough for the chase camera to pass under the banner

        static readonly Color NextGate = new(1f, 0.85f, 0.15f);
        static readonly Color PassedGate = new(0.35f, 0.9f, 0.4f);
        static readonly Color LaterGate = new(0.95f, 0.95f, 0.92f);

        Course course;
        ArtPalette palette;
        BankStyle style;
        readonly List<Renderer[]> gateBanners = new();
        readonly List<Chevron> chevrons = new();

        /// <summary>The moving obstacles (wheels, crocodiles, swinging logs, geysers); set their clock to pose them.</summary>
        public HazardViews Obstacles { get; private set; }

        int shownProgress = -1;

        sealed class Chevron
        {
            public Transform Shape;
            public CurrentZoneView Zone;
            public float Distance;
            public float Across;
        }

        sealed class CurrentZoneView
        {
            public float From, To, Speed;
        }

        /// <summary>Leaves the scenery unbatched (a diagnostic: <c>-rushahead-no-batching</c>).</summary>
        public static bool NoBatching = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-rushahead-no-batching") >= 0;

        public static CourseView Build(Course course, ArtPalette palette)
        {
            var view = new GameObject($"Course view: {course.Def.displayName}").AddComponent<CourseView>();
            view.course = course;
            view.palette = palette;
            view.style = BankStyle.For(course.Def.scene, palette);
            view.SetAir(course.Def.scene);
            var random = new System.Random(StableSeed(course.Def.name)); // the same trees on every device

            var scenery = Shapes3D.Group(view.transform, "Scenery");
            view.BuildWater(scenery);
            view.BuildEdge(scenery, left: true, random);
            view.BuildEdge(scenery, left: false, random);
            view.BuildEnds(scenery, random);
            if (palette.HasLand) view.BuildLand(scenery, random);
            else view.BuildFloor(scenery, random);
            foreach (var rock in course.Def.rocks) CourseDressing.Rock(scenery, rock.position, rock.radius, palette, random);
            IslandViews.Build(course, view.style, palette, scenery, random);
            if (view.style.LilyPads) view.BuildLilyPads(scenery, random);
            if (!NoBatching) StaticBatchingUtility.Combine(scenery.gameObject); // hundreds of primitives that never move: draw them in a few batches

            var moving = Shapes3D.Group(view.transform, "Moving");
            for (var i = 0; i < course.Barrels.Count; i++)
            {
                if (course.IsObstacleBody(i)) continue; // drawn by its obstacle's view
                if (!course.IsLog(i)) CourseDressing.Barrel(moving, course.Barrels[i].transform, Course.BarrelRadius, palette);
                else CourseDressing.Log(moving, course.Barrels[i].transform, course.Def.logs[i - course.Def.barrels.Length].length, Course.LogRadius, palette);
            }

            view.BuildGates(moving);
            view.BuildCurrents(moving, random);
            view.BuildWhirlpools(moving);
            view.Obstacles = HazardViews.Build(course, palette, moving);
            view.ShowProgress(0);
            return view;
        }

        /// <summary>Lights the next checkpoint and turns the ones already passed green.</summary>
        Material riverWater;
        float shownFlowBoost = 1f;

        /// <summary>The river running faster (Elimination, after each knockout): its ripples speed up to match.</summary>
        public void SetFlowBoost(float boost)
        {
            if (riverWater == null || Mathf.Approximately(boost, shownFlowBoost)) return;
            shownFlowBoost = boost;
            riverWater.SetFloat("_FlowSpeed", course.Def.flow * boost);
        }

        readonly List<GameObject> lamps = new();
        bool night;

        /// <summary>Night: the gates' lamps come on and their banners glow, so the way stays clear in the dark.</summary>
        public void SetNight(bool on)
        {
            if (on == night) return;
            night = on;
            foreach (var lamp in lamps) lamp.SetActive(on);
            shownProgress = -1;
            ShowProgress(lastProgress);
        }

        int lastProgress;

        static readonly Color KnockoutGate = new(1f, 0.22f, 0.18f);
        readonly HashSet<int> knockouts = new();

        /// <summary>Elimination's knockout gates (as checkpoint counts) hang red banners until they're passed; null or empty for none.</summary>
        public void ShowKnockouts(int[] gates)
        {
            var count = gates?.Length ?? 0;
            if (count == knockouts.Count && (gates == null || System.Array.TrueForAll(gates, g => knockouts.Contains(g - 1)))) return;
            knockouts.Clear();
            if (gates != null)
                foreach (var gate in gates) knockouts.Add(gate - 1);
            shownProgress = -1;
        }

        public void ShowProgress(int gatesPassed)
        {
            lastProgress = gatesPassed;
            if (gatesPassed == shownProgress) return;
            shownProgress = gatesPassed;
            for (var i = 0; i < gateBanners.Count; i++)
            {
                var finish = i == gateBanners.Count - 1;
                if (finish) continue; // the chequered finish keeps its pattern
                var color = i < gatesPassed ? PassedGate : knockouts.Contains(i) ? KnockoutGate : i == gatesPassed ? NextGate : LaterGate;
                var material = color == KnockoutGate ? KnockoutBanner() : night ? palette.Glow(color) : palette.Tinted(palette.sign, color);
                foreach (var banner in gateBanners[i]) banner.sharedMaterial = material;
            }
        }

        Material knockoutDay, knockoutNight;

        /// <summary>The knockout banner: "LAST ONE OUT" on red, lit like the other signs by day and glowing by night.</summary>
        Material KnockoutBanner()
        {
            if (night)
            {
                if (knockoutNight == null)
                {
                    knockoutNight = new Material(palette.foam) { name = "Knockout banner (night)", color = Color.white, mainTexture = KnockoutLettering() };
                    if (knockoutNight.HasProperty("_BaseMap")) knockoutNight.SetTexture("_BaseMap", KnockoutLettering());
                }

                return knockoutNight;
            }

            if (knockoutDay == null)
            {
                knockoutDay = new Material(palette.sign) { name = "Knockout banner", color = Color.white, mainTexture = KnockoutLettering() };
                if (knockoutDay.HasProperty("_BaseMap")) knockoutDay.SetTexture("_BaseMap", KnockoutLettering());
            }

            return knockoutDay;
        }

        static Texture2D knockoutLettering;

        /// <summary>"LAST ONE OUT" in white capitals on the knockout red, drawn once from a little 5x7 pixel font.</summary>
        static Texture2D KnockoutLettering()
        {
            if (knockoutLettering != null) return knockoutLettering;
            const string text = "LAST ONE OUT";
            var font = new Dictionary<char, string>
            {
                ['L'] = "#....#....#....#....#....#....#####",
                ['A'] = ".###.#...##...#######...##...##...#",
                ['S'] = ".#####....#.....###.....#....#####.",
                ['T'] = "#####..#....#....#....#....#....#..",
                ['O'] = ".###.#...##...##...##...##...#.###.",
                ['N'] = "#...###..##.#.##..###...##...##...#",
                ['E'] = "######....#....####.#....#....#####",
                ['U'] = "#...##...##...##...##...##...#.###.",
            };
            const int scale = 4, width = 320, height = 40;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = "Knockout lettering", wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            var pixels = new Color32[width * height];
            Color32 red = KnockoutGate, white = new(255, 255, 255, 255);
            for (var i = 0; i < pixels.Length; i++) pixels[i] = red;
            var x0 = (width - (text.Length * 6 - 1) * scale) / 2;
            var y0 = (height - 7 * scale) / 2;
            for (var k = 0; k < text.Length; k++)
            {
                if (!font.TryGetValue(text[k], out var glyph)) continue;
                for (var row = 0; row < 7; row++)
                for (var col = 0; col < 5; col++)
                {
                    if (glyph[row * 5 + col] != '#') continue;
                    for (var dy = 0; dy < scale; dy++)
                    for (var dx = 0; dx < scale; dx++)
                        pixels[(y0 + (6 - row) * scale + dy) * width + x0 + (k * 6 + col) * scale + dx] = white;
                }
            }

            System.Array.Reverse(pixels); // turned half round: the face the rafts see has its UVs that way up
            texture.SetPixels32(pixels);
            texture.Apply();
            return knockoutLettering = texture;
        }

        void Update()
        {
            CircleDebris(Time.deltaTime);

            var river = course.River;
            foreach (var chevron in chevrons)
            {
                var zone = chevron.Zone;
                chevron.Distance += zone.Speed * Time.deltaTime;
                if (chevron.Distance > zone.To) chevron.Distance -= zone.To - zone.From;
                if (chevron.Distance < zone.From) chevron.Distance += zone.To - zone.From;

                var at = river.At(chevron.Distance);
                var heading = zone.Speed >= 0f ? at.Tangent : -at.Tangent;
                chevron.Shape.SetPositionAndRotation(SimSpace.ToWorld(at.Centre + at.Left * (chevron.Across * at.HalfWidth), 0.05f),
                    Quaternion.LookRotation(SimSpace.Direction(heading)));
            }
        }

        /// <summary>The water: a ribbon over the channel only, running a little under the stone lip on each side.</summary>
        void BuildWater(Transform parent)
        {
            // UVs run across (u, metres from the middle) and down (v, metres down the river), so the ripples can rush
            // downstream at the river's own speed.
            var samples = course.River.Samples;
            var water = new MeshBuilder();
            for (var i = 0; i < samples.Count - 1; i++)
            {
                var a = samples[i];
                var b = samples[i + 1];
                var wa = a.HalfWidth + WaterUnderLip;
                var wb = b.HalfWidth + WaterUnderLip;
                water.Quad(SimSpace.ToWorld(a.Centre + a.Left * wa), SimSpace.ToWorld(b.Centre + b.Left * wb),
                    SimSpace.ToWorld(b.Centre - b.Left * wb), SimSpace.ToWorld(a.Centre - a.Left * wa), Vector3.up,
                    new Vector2(wa, a.Distance), new Vector2(wb, b.Distance), new Vector2(-wb, b.Distance), new Vector2(-wa, a.Distance));
                water.Widths(a.HalfWidth, b.HalfWidth, b.HalfWidth, a.HalfWidth);
            }

            // Out over the lip of the last fall, so the water visibly pours off the end.
            var last = samples[^1];
            var over = last.Centre + last.Tangent * FallLip;
            var width = last.HalfWidth + WaterUnderLip;
            water.Quad(SimSpace.ToWorld(last.Centre + last.Left * width), SimSpace.ToWorld(over + last.Left * width),
                SimSpace.ToWorld(over - last.Left * width), SimSpace.ToWorld(last.Centre - last.Left * width), Vector3.up,
                new Vector2(width, last.Distance), new Vector2(width, last.Distance + FallLip), new Vector2(-width, last.Distance + FallLip),
                new Vector2(-width, last.Distance));
            water.Widths(last.HalfWidth, last.HalfWidth, last.HalfWidth, last.HalfWidth);
            var flowing = new Material(palette.water) { name = "River water" };
            TintWater(flowing, course.Def.scene);
            flowing.SetFloat("_RiverSpace", 1f);
            flowing.SetFloat("_FlowSpeed", course.Def.flow);
            riverWater = flowing;
            flowing.SetFloat("_Holes", 1f); // whirlpools' funnels show through
            Shapes3D.MeshObject("Water", water.Build("Water"), parent, Vector3.zero, flowing);
        }

        /// <summary>
        /// One side of the river: a white line of foam where the water meets a low stone lip, and past the lip a cliff
        /// dropping to the gorge floor far below, steep at the top and leaning out further down. Waterfalls spill over
        /// the lip here and there. On the inside of a bend the cliff can only lean out so far before it folds over.
        /// </summary>
        void BuildEdge(Transform parent, bool left, System.Random random)
        {
            var samples = course.River.Samples;
            var count = samples.Count;
            var edge = new Vector2[count];
            var outward = new Vector2[count];
            var lean = new float[count];
            var rim = new float[count];

            for (var i = 0; i < count; i++)
            {
                var s = samples[i];
                outward[i] = left ? s.Left : -s.Left;
                edge[i] = s.Centre + outward[i] * s.HalfWidth;

                // Bending toward this side puts the centre of the bend here, 1/curvature from the middle.
                var towardThisSide = left ? s.Curvature : -s.Curvature;
                var room = towardThisSide > 1e-4f ? 1f / towardThisSide - s.HalfWidth : 100f;
                rim[i] = Mathf.Clamp(room * 0.3f, 0.2f, RimWidth);
                lean[i] = Mathf.Clamp((room - rim[i]) * 0.4f, 0f, MaxLean);
            }

            for (var pass = 0; pass < 2; pass++) // spread each cut a little either way, so the cliff stays smooth
                for (var i = 0; i < count; i++)
                    for (var j = Mathf.Max(0, i - 4); j <= Mathf.Min(count - 1, i + 4); j++)
                        lean[i] = Mathf.Min(lean[i], lean[j] + 0.4f * Mathf.Abs(i - j));

            var drop = style.Drop;
            var foam = new MeshBuilder();
            var stone = new MeshBuilder();
            var face = new MeshBuilder();
            var deep = new MeshBuilder();
            for (var i = 0; i < count - 1; i++)
            {
                var j = i + 1;
                var outside = SimSpace.Direction((outward[i] + outward[j]) * 0.5f);
                Vector3 At(int k, float across, float height) => SimSpace.ToWorld(edge[k] + outward[k] * across, height);

                foam.Quad(At(i, -0.45f, 0.03f), At(j, -0.45f, 0.03f), At(j, -0.02f, 0.03f), At(i, -0.02f, 0.03f), Vector3.up);
                stone.Quad(At(i, -0.04f, RimTop), At(j, -0.04f, RimTop), At(j, rim[j], RimTop), At(i, rim[i], RimTop), Vector3.up);
                stone.Quad(At(i, -0.04f, -0.1f), At(j, -0.04f, -0.1f), At(j, -0.04f, RimTop), At(i, -0.04f, RimTop), -outside); // the step up from the water
                face.Quad(At(i, rim[i], RimTop), At(j, rim[j], RimTop), At(j, rim[j] + lean[j] * 0.12f, -LedgeDepth), At(i, rim[i] + lean[i] * 0.12f, -LedgeDepth), outside);
                deep.Quad(At(i, rim[i] + lean[i] * 0.12f, -LedgeDepth), At(j, rim[j] + lean[j] * 0.12f, -LedgeDepth),
                    At(j, rim[j] + lean[j], -drop), At(i, rim[i] + lean[i], -drop), outside);
            }

            var side = left ? "Left" : "Right";
            Shapes3D.MeshObject($"{side} foam line", foam.Build($"{side} foam line"), parent, Vector3.zero, palette.foam);
            Shapes3D.MeshObject($"{side} lip", stone.Build($"{side} lip"), parent, Vector3.zero, style.Lip);
            Shapes3D.MeshObject($"{side} cliff", face.Build($"{side} cliff"), parent, Vector3.zero, style.Cliff);
            Shapes3D.MeshObject($"{side} cliff below", deep.Build($"{side} cliff below"), parent, Vector3.zero, style.CliffDeep);

            // Ferns along the outer edge of the lip, here and there.
            if (palette.fernMesh != null)
                for (var i = (int)(4 + random.NextDouble() * 8); i < count - 4; i += (int)(10 + random.NextDouble() * 14))
                    Shapes3D.Model("Fern", palette.fernMesh, palette.fernMaterial, parent, SimSpace.ToWorld(edge[i] + outward[i] * (rim[i] * 0.85f), RimTop),
                        0.9f + (float)random.NextDouble() * 0.7f, (float)random.NextDouble() * 360f);

            // Shrubs on the lip here and there.
            if (palette.shrubMeshes.Length > 0)
                for (var i = (int)(6 + random.NextDouble() * 10); i < count - 6; i += (int)(10 + random.NextDouble() * 16))
                {
                    var pick = random.Next(palette.shrubMeshes.Length);
                    Cull(Shapes3D.Model("Lip shrub", palette.shrubMeshes[pick], palette.shrubMaterials[pick], parent,
                        SimSpace.ToWorld(edge[i] + outward[i] * (rim[i] + 0.25f), RimTop - 0.05f), 0.9f + 0.8f * (float)random.NextDouble(), (float)random.NextDouble() * 360f, 0.1f), 0.01f);
                }

            // Waterfalls spilling over the lip every so often, clear of the very ends.
            for (var i = (int)(8 + random.NextDouble() * 10); i < count - 8; i += (int)(14 + random.NextDouble() * 18))
            {
                var width = 1f + (float)random.NextDouble() * 1.4f;
                Waterfall(parent, edge[i] + outward[i] * rim[i], outward[i], samples[i].Tangent, width, lean[i], drop);
            }
        }

        /// <summary>
        /// White water pouring off a lip: over the edge, arcing a little way out and down the cliff to the gorge floor,
        /// with spray hanging at the bottom.
        /// </summary>
        void Waterfall(Transform parent, Vector2 lip, Vector2 outward, Vector2 along, float width, float lean, float drop)
        {
            var sheet = new MeshBuilder();
            const int steps = 8;
            var half = along * (width * 0.5f);
            Vector3 Point(float t, float side)
            {
                // Out fast at the top, then falling almost straight, just outside the cliff.
                var outBy = 0.3f + 0.8f * Mathf.Sqrt(t) + lean * t;
                return SimSpace.ToWorld(lip + outward * outBy + half * side, RimTop - drop * t * t * (3f - 2f * t) * 0.98f - 0.02f);
            }

            sheet.TwoSided(SimSpace.ToWorld(lip - outward * 0.6f - half, 0.04f), SimSpace.ToWorld(lip - outward * 0.6f + half, 0.04f),
                Point(0f, 1f), Point(0f, -1f), Vector3.up);
            for (var k = 0; k < steps; k++)
            {
                float t0 = k / (float)steps, t1 = (k + 1) / (float)steps;
                sheet.TwoSided(Point(t0, -1f), Point(t0, 1f), Point(t1, 1f), Point(t1, -1f), SimSpace.Direction(outward) + Vector3.up * 0.2f);
            }

            var fall = Shapes3D.MeshObject("Waterfall", sheet.Build("Waterfall"), parent, Vector3.zero, palette.foam);
            fall.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var bottom = lip + outward * (1.4f + lean);
            for (var puff = 0; puff < 3; puff++)
                Shapes3D.Primitive(PrimitiveType.Sphere, parent, SimSpace.ToWorld(bottom + outward * (puff * 1.4f) + along * ((puff - 1) * width * 0.6f), -drop + 0.8f + puff * 0.5f),
                    new Vector3(3.6f + width, 1.6f, 3.6f + width), palette.foam, name: "Spray");
        }

        /// <summary>
        /// The gorge floor far below: grass all round, with woods along the foot of the cliffs where you look down on
        /// them, and never under the river itself.
        /// </summary>
        void BuildFloor(Transform parent, System.Random random)
        {
            var min = Vector2.positiveInfinity;
            var max = Vector2.negativeInfinity;
            foreach (var point in course.River.Outline())
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            min -= Vector2.one * FloorMargin;
            max += Vector2.one * FloorMargin;
            var floor = new MeshBuilder();
            var drop = style.Drop;
            floor.Quad(SimSpace.ToWorld(min, -drop), SimSpace.ToWorld(new Vector2(max.x, min.y), -drop), SimSpace.ToWorld(max, -drop),
                SimSpace.ToWorld(new Vector2(min.x, max.y), -drop), Vector3.up);
            Shapes3D.MeshObject("Gorge floor", floor.Build("Gorge floor"), parent, Vector3.zero, style.Ground);

            var river = course.River;
            for (var distance = -10f; distance < river.Length + 10f; distance += 3.5f)
            {
                var at = river.At(Mathf.Clamp(distance, 0f, river.Length));
                foreach (var side in new[] { -1f, 1f })
                {
                    if (random.NextDouble() > 0.75) continue;
                    var across = at.HalfWidth + RimWidth + MaxLean + 2f + (float)(random.NextDouble() * random.NextDouble()) * 34f;
                    var spot = at.Centre + at.Left * (side * across) + at.Tangent * (distance - Mathf.Clamp(distance, 0f, river.Length));
                    var near = river.Project(spot);
                    if (Mathf.Abs(near.Lateral) < near.HalfWidth + RimWidth + MaxLean + 1.5f) continue; // under another stretch of the river
                    style.Plant(parent, spot, -drop, palette, random);
                }
            }
        }

        /// <summary>
        /// The land below (see <see cref="GorgeTerrain"/>), planted: woods thick near the foot of the cliffs and thinning
        /// up the hills (mostly conifers in the rapids' gorge, broadleaf trees elsewhere), shrubs and mossy boulders on the
        /// scree, and a little mist lying along the valley floor.
        /// </summary>
        void BuildLand(Transform parent, System.Random random)
        {
            var land = GorgeTerrain.Build(course, style, palette, parent, RimWidth + MaxLean);
            Plant(land, course.Def.scene, style, palette, parent, random);

            // Mist lying along the valley floor here and there.
            var mist = new Material(palette.foam) { name = "Mist", color = new Color(0.93f, 0.95f, 1f, 0.07f) };
            var river = course.River;
            for (var i = 0; i < 10; i++)
            {
                var at = river.At((i + 0.5f) / 10f * river.Length);
                var side = random.NextDouble() < 0.5 ? -1f : 1f;
                var spot = at.Centre + at.Left * (side * (at.HalfWidth + RimWidth + MaxLean + 30f + 30f * (float)random.NextDouble()));
                var cloud = Shapes3D.MeshObject("Mist", Shapes3D.Disc(20f + 14f * (float)random.NextDouble(), 24), parent,
                    SimSpace.ToWorld(spot, land.Height(spot) + 2.5f + 4f * (float)random.NextDouble()), mist);
                cloud.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>The water's colour by place: turquoise meltwater on the glacier, green-brown in the canyon.</summary>
        static void TintWater(Material water, CourseScene scene)
        {
            switch (scene)
            {
                case CourseScene.Glacier:
                    water.SetColor("_BaseColor", new Color(0.1f, 0.55f, 0.6f));
                    water.SetColor("_DeepColor", new Color(0.03f, 0.36f, 0.45f));
                    water.SetColor("_RippleColor", new Color(0.78f, 0.96f, 0.98f));
                    break;
                case CourseScene.Canyon:
                    water.SetColor("_BaseColor", new Color(0.27f, 0.42f, 0.31f));
                    water.SetColor("_DeepColor", new Color(0.12f, 0.25f, 0.18f));
                    water.SetColor("_RippleColor", new Color(0.72f, 0.82f, 0.66f));
                    break;
                case CourseScene.Swamp:
                    water.SetColor("_BaseColor", new Color(0.24f, 0.3f, 0.16f));
                    water.SetColor("_DeepColor", new Color(0.1f, 0.15f, 0.08f));
                    water.SetColor("_RippleColor", new Color(0.62f, 0.7f, 0.5f));
                    break;
            }
        }

        /// <summary>
        /// Woods and scree on a piece of land: trees thickest a little way out from the edge and thinning up the hills,
        /// scanned boulders and shrubs near it. Used round the courses and the practice lagoon. Glacier woods are all
        /// pines and sparser; the canyon has only a few low trees, more scrub and more boulders.
        /// </summary>
        public static void Plant(GorgeTerrain.Land land, CourseScene scene, BankStyle style, ArtPalette palette, Transform parent, System.Random random)
        {
            var detail = Looks.Detail;
            var size = land.Terrain.terrainData.size;
            var corner = land.Terrain.transform.position;
            Vector2 Somewhere(float x, float z, float step) =>
                new(corner.x + x + (float)random.NextDouble() * step, corner.z + z + (float)random.NextDouble() * step);

            var conifers = scene switch { CourseScene.Rapids => 0.85, CourseScene.Lake => 0.2, CourseScene.Glacier => 1.0, CourseScene.Canyon => 0.4, CourseScene.Swamp => 0.1, _ => 0.3 };
            var (trees, shrubs, boulders, treeSize) = scene switch
            {
                CourseScene.Glacier => (0.6, 0.4, 1.5, 1f),
                CourseScene.Canyon => (0.12, 2.2, 1.7, 0.6f),
                CourseScene.Swamp => (1.3, 1.6, 0.5, 1.1f),
                _ => (1.0, 1.0, 1.0, 1f),
            };
            var woods = Shapes3D.Group(parent, "Woods");
            const float treeStep = 8.5f;
            for (var z = 0f; z < size.z - treeStep; z += treeStep)
            for (var x = 0f; x < size.x - treeStep; x += treeStep)
            {
                var spot = Somewhere(x, z, treeStep);
                var out_ = land.FromCliff(spot);
                var chance = out_ < 3f ? 0f : out_ < 45f ? 0.7f : out_ < 110f ? 0.3f : 0.1f;
                if (random.NextDouble() >= chance * detail * trees || land.Steepness(spot) > 33f) continue;
                var ground = land.Height(spot) - 0.15f;
                if (!Flora.Available(palette))
                {
                    style.Plant(woods, spot, ground, palette, random);
                    continue;
                }

                var conifer = random.NextDouble() < conifers;
                var height = (conifer ? 9f + 7f * (float)random.NextDouble() : 7f + 5f * (float)random.NextDouble()) * treeSize;
                Flora.Tree(conifer ? Flora.Kind.Conifer : Flora.Kind.Broadleaf, woods, SimSpace.ToWorld(spot, ground), height,
                    (float)random.NextDouble() * 360f, random.Next(Flora.Variants), palette);
            }

            var scree = Shapes3D.Group(parent, "Scree");
            const float rockStep = 6f;
            for (var z = 0f; z < size.z - rockStep; z += rockStep)
            for (var x = 0f; x < size.x - rockStep; x += rockStep)
            {
                var spot = Somewhere(x, z, rockStep);
                var out_ = land.FromCliff(spot);
                if (out_ < 0.5f || out_ > 40f) continue;
                var roll = random.NextDouble();
                var ground = SimSpace.ToWorld(spot, land.Height(spot));
                if (out_ < 14f && roll < 0.25 * detail * boulders && palette.boulderMeshes.Length > 0)
                {
                    var pick = random.Next(palette.boulderMeshes.Length);
                    Cull(Shapes3D.Model("Boulder", palette.boulderMeshes[pick], palette.boulderMaterials[pick], scree, ground,
                        1.2f + 3.4f * (float)(random.NextDouble() * random.NextDouble()), (float)random.NextDouble() * 360f, 0.25f), 0.012f);
                }
                else if (roll < 0.12 * detail * shrubs && palette.shrubMeshes.Length > 0)
                {
                    var pick = random.Next(palette.shrubMeshes.Length);
                    Cull(Shapes3D.Model("Shrub", palette.shrubMeshes[pick], palette.shrubMaterials[pick], scree, ground,
                        1f + 1.6f * (float)random.NextDouble(), (float)random.NextDouble() * 360f, 0.05f), 0.015f);
                }
            }
        }

        /// <summary>Drops a small thing from the drawing once it's a speck on the screen.</summary>
        static void Cull(GameObject thing, float screenHeight)
        {
            var renderers = thing.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            thing.AddComponent<LODGroup>().SetLODs(new[] { new LOD(screenHeight, renderers) });
        }

        /// <summary>
        /// The two ends. At the top, a line of boulders across the river (the sim's wall there) and the end of the
        /// ridge dropping away behind them; at the bottom, a log boom holding the rafts back from the last waterfall,
        /// which pours the whole river off the end into the gorge.
        /// </summary>
        void BuildEnds(Transform parent, System.Random random)
        {
            var river = course.River;
            var drop = style.Drop;
            var top = river.At(0f);
            var width = top.HalfWidth + RimWidth;
            var back = top.Centre - top.Tangent * 1.8f;

            for (var across = -width; across <= width + 0.01f; across += 1.5f)
            {
                var rock = top.Centre + top.Left * across - top.Tangent * (0.6f + (float)random.NextDouble() * 0.4f);
                Shapes3D.Primitive(PrimitiveType.Sphere, parent, SimSpace.ToWorld(rock, 0.2f), new Vector3(2.1f, 1.3f, 1.9f) * (0.8f + (float)random.NextDouble() * 0.4f),
                    palette.rock, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f), "Boulder");
            }

            var ends = new MeshBuilder();
            var upstream = -SimSpace.Direction(top.Tangent);
            ends.Quad(SimSpace.ToWorld(top.Centre + top.Left * (width + 0.1f), RimTop), SimSpace.ToWorld(top.Centre - top.Left * (width + 0.1f), RimTop),
                SimSpace.ToWorld(back - top.Left * (width + 0.1f), RimTop), SimSpace.ToWorld(back + top.Left * (width + 0.1f), RimTop), Vector3.up);
            ends.Quad(SimSpace.ToWorld(back + top.Left * width, RimTop), SimSpace.ToWorld(back - top.Left * width, RimTop),
                SimSpace.ToWorld(back - top.Left * width - top.Tangent * MaxLean, -drop), SimSpace.ToWorld(back + top.Left * width - top.Tangent * MaxLean, -drop), upstream);

            var end = river.At(river.Length);
            var lipAt = end.Centre + end.Tangent * FallLip;
            var endWidth = end.HalfWidth + RimWidth;
            var downstream = SimSpace.Direction(end.Tangent);
            ends.Quad(SimSpace.ToWorld(lipAt + end.Left * endWidth, -0.05f), SimSpace.ToWorld(lipAt - end.Left * endWidth, -0.05f),
                SimSpace.ToWorld(lipAt - end.Left * endWidth + end.Tangent * MaxLean, -drop), SimSpace.ToWorld(lipAt + end.Left * endWidth + end.Tangent * MaxLean, -drop), downstream);
            foreach (var side in new[] { -1f, 1f })
            {
                var corner = end.Centre + end.Left * (side * (end.HalfWidth + 0.02f));
                ends.Quad(SimSpace.ToWorld(corner, RimTop), SimSpace.ToWorld(corner + end.Left * (side * RimWidth), RimTop),
                    SimSpace.ToWorld(corner + end.Left * (side * RimWidth) + end.Tangent * FallLip, RimTop), SimSpace.ToWorld(corner + end.Tangent * FallLip, RimTop), Vector3.up);
            }

            Shapes3D.MeshObject("Ends", ends.Build("Ends"), parent, Vector3.zero, style.Cliff);
            Waterfall(parent, lipAt, end.Tangent, end.Left, end.HalfWidth * 2f, MaxLean, drop);

            // The boom: logs chained end to end across the river, with a post on the lip at each side.
            var boom = river.At(river.Length - Course.BoomBack);
            var bark = palette.Tinted(palette.wood, new Color(0.45f, 0.3f, 0.18f));
            var yaw = Quaternion.LookRotation(SimSpace.Direction(boom.Tangent)) * Quaternion.Euler(0f, 0f, 90f); // a cylinder's axis is its y: lay it across
            const int logs = 4;
            var span = (boom.HalfWidth + 0.3f) * 2f / logs;
            for (var k = 0; k < logs; k++)
            {
                var middle = boom.Centre + boom.Left * (boom.HalfWidth + 0.3f - span * (k + 0.5f));
                Shapes3D.Primitive(PrimitiveType.Cylinder, parent, SimSpace.ToWorld(middle, 0.1f), new Vector3(0.5f, span * 0.5f - 0.05f, 0.5f), bark, yaw, "Boom log");
            }

            foreach (var side in new[] { -1f, 1f })
                Shapes3D.Primitive(PrimitiveType.Cylinder, parent, SimSpace.ToWorld(boom.Centre + boom.Left * (side * (boom.HalfWidth + 0.5f)), 0.6f),
                    new Vector3(0.35f, 0.6f, 0.35f), palette.wood, name: "Boom post");
        }

        /// <summary>Lily pads in the still water along both shores, clear of the middle where the rafts race.</summary>
        void BuildLilyPads(Transform parent, System.Random random)
        {
            var river = course.River;
            for (var distance = 4f; distance < river.Length - 4f; distance += 3.2f)
            {
                var at = river.At(distance);
                foreach (var side in new[] { -1f, 1f })
                {
                    if (random.NextDouble() > 0.45) continue;
                    var across = at.HalfWidth - 0.9f - (float)random.NextDouble() * Mathf.Min(2.5f, at.HalfWidth * 0.25f);
                    CourseDressing.LilyPad(parent, at.Centre + at.Left * (side * across) + at.Tangent * ((float)random.NextDouble() - 0.5f) * 2f, palette, random);
                }
            }
        }

        /// <summary>
        /// Each whirlpool: a patch of darker water and three foam arms spiralling in, turning the way the water goes
        /// (anticlockwise from above for a positive spin), about as fast as the water halfway out.
        /// </summary>
        static readonly int WhirlpoolsId = Shader.PropertyToID("_RushWhirlpools");
        static readonly int WhirlpoolCountId = Shader.PropertyToID("_RushWhirlpoolCount");

        /// <summary>
        /// Each whirlpool is a funnel: the river's water is cut away over it (in the water shader) and a bowl dips in its
        /// place, its water turning with foam arms spiralling into a dark eye, sticks and leaves circling down into it.
        /// Rafts ride down into the bowl (<see cref="WaterSurface"/>).
        /// </summary>
        void BuildWhirlpools(Transform parent)
        {
            var spots = course.Def.whirlpools;
            var holes = new Vector4[6];
            for (var i = 0; i < spots.Length && i < holes.Length; i++)
                holes[i] = new Vector4(spots[i].position.x, spots[i].position.y, spots[i].radius - 0.04f, 0f);
            whirlpoolOwner = this; // a course torn down after this one was built mustn't clear its whirlpools
            Shader.SetGlobalVectorArray(WhirlpoolsId, holes);
            Shader.SetGlobalFloat(WhirlpoolCountId, Mathf.Min(spots.Length, holes.Length));
            WaterSurface.SetFunnels(spots);
            var ramps = new List<JumpRamp>();
            foreach (var obstacle in course.Hazards.Obstacles)
                if (obstacle is JumpRamp ramp) ramps.Add(ramp);
            WaterSurface.SetRamps(ramps);

            var bark = palette.Tinted(palette.wood, new Color(0.42f, 0.31f, 0.2f));
            var random = new System.Random(spots.Length * 31 + 5);
            foreach (var pool in spots)
            {
                var root = Shapes3D.Group(parent, "Whirlpool", SimSpace.ToWorld(pool.position, 0f));
                var depth = WaterSurface.FunnelDepth(pool.radius);
                var water = new Material(palette.water) { name = "Whirlpool water" };
                TintWater(water, course.Def.scene);
                water.SetFloat("_Swirl", 1f);
                water.SetVector("_SwirlCentre", new Vector4(pool.position.x, pool.position.y, 0f, 0f));
                water.SetFloat("_SwirlRadius", pool.radius);
                // A positive spin turns sim x toward sim y (world x toward z); the shader's pattern turns the other way for a positive rate.
                var turn = pool.spin / Mathf.Max(0.5f, pool.radius * 0.5f);
                water.SetFloat("_SwirlSpin", -turn * 1.5f);
                Shapes3D.MeshObject("Funnel", Funnel(pool.radius, depth), root, Vector3.zero, water);

                for (var i = 0; i < 6; i++)
                {
                    var stick = Shapes3D.Group(root, "Debris");
                    var leafy = i % 3 == 2;
                    Shapes3D.Primitive(leafy ? PrimitiveType.Sphere : PrimitiveType.Cylinder, stick, Vector3.zero,
                        leafy ? new Vector3(0.35f, 0.06f, 0.25f) : new Vector3(0.09f, 0.45f + 0.3f * (float)random.NextDouble(), 0.09f),
                        leafy ? palette.leaves : bark, leafy ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f), "Piece");
                    debris.Add(new Debris
                    {
                        Piece = stick, Centre = pool.position, Radius = pool.radius, Depth = depth, Turn = turn,
                        R = pool.radius * (0.2f + 0.75f * (float)random.NextDouble()), Angle = (float)(random.NextDouble() * Mathf.PI * 2.0),
                    });
                }
            }
        }

        /// <summary>A bowl of water dipping to <paramref name="depth"/> at its eye, flat at the rim to meet the river.</summary>
        static Mesh Funnel(float radius, float depth)
        {
            const int rings = 16, segments = 56;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var ring = 0; ring <= rings; ring++)
            {
                var r = radius * (1f - ring / (float)rings * 0.97f);
                var y = WaterSurface.FunnelHeight(r, radius, depth);
                for (var s = 0; s < segments; s++)
                {
                    var a = s * Mathf.PI * 2f / segments;
                    vertices.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                }
            }

            for (var ring = 0; ring < rings; ring++)
            for (var s = 0; s < segments; s++)
            {
                var a = ring * segments + s;
                var b = ring * segments + (s + 1) % segments;
                var c = a + segments;
                var d = b + segments;
                triangles.AddRange(new[] { a, c, b, b, c, d });
            }

            // The eye: a little cap closing the bottom.
            var eye = vertices.Count;
            vertices.Add(new Vector3(0f, -depth, 0f));
            for (var s = 0; s < segments; s++)
                triangles.AddRange(new[] { rings * segments + s, eye, rings * segments + (s + 1) % segments });

            var mesh = new Mesh { name = "Whirlpool funnel" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A stick or leaf circling down into a whirlpool, faster as it nears the eye, then back out at the rim.</summary>
        sealed class Debris
        {
            public Transform Piece;
            public Vector2 Centre;
            public float Radius, Depth, Turn, R, Angle;
        }

        readonly List<Debris> debris = new();

        void CircleDebris(float dt)
        {
            foreach (var bit in debris)
            {
                var spin = bit.Turn * Mathf.Clamp(bit.Radius * 0.5f / Mathf.Max(bit.R, 0.4f), 0.5f, 3f);
                bit.Angle += spin * dt;
                bit.R -= (0.25f + 0.9f * (1f - bit.R / bit.Radius)) * dt;
                if (bit.R < 0.35f) bit.R = bit.Radius * 0.95f;
                var at = bit.Centre + new Vector2(Mathf.Cos(bit.Angle), Mathf.Sin(bit.Angle)) * bit.R;
                var along = new Vector2(-Mathf.Sin(bit.Angle), Mathf.Cos(bit.Angle)) * Mathf.Sign(bit.Turn);
                bit.Piece.SetPositionAndRotation(SimSpace.ToWorld(at, WaterSurface.FunnelHeight(bit.R, bit.Radius, bit.Depth) + 0.04f),
                    Quaternion.LookRotation(SimSpace.Direction(along)));
            }
        }

        static CourseView whirlpoolOwner;

        // The scene's own haze, put back when a course that changed it goes.
        static CourseView airOwner;
        static (Color colour, float start, float end) sceneAir;

        /// <summary>
        /// The air by place: clear, warm desert air in the canyon so its red walls and floor show far below, and crisp
        /// cold air on the glacier. Everywhere else keeps the scene's soft blue haze.
        /// </summary>
        void SetAir(CourseScene scene)
        {
            if (airOwner == null) sceneAir = (RenderSettings.fogColor, RenderSettings.fogStartDistance, RenderSettings.fogEndDistance);
            var (colour, start, end) = scene switch
            {
                CourseScene.Canyon => (new Color(0.86f, 0.78f, 0.7f), 90f, 300f),
                CourseScene.Glacier => (new Color(0.8f, 0.88f, 0.96f), 60f, 220f),
                CourseScene.Swamp => (new Color(0.6f, 0.65f, 0.55f), 28f, 150f), // misty
                _ => sceneAir,
            };
            RenderSettings.fogColor = colour;
            RenderSettings.fogStartDistance = start;
            RenderSettings.fogEndDistance = end;
            airOwner = this;
        }

        void OnDestroy()
        {
            if (airOwner == this)
            {
                airOwner = null;
                (RenderSettings.fogColor, RenderSettings.fogStartDistance, RenderSettings.fogEndDistance) = sceneAir;
            }

            if (whirlpoolOwner != this) return;
            whirlpoolOwner = null;
            WaterSurface.Clear();
            Shader.SetGlobalFloat(WhirlpoolCountId, 0f);
        }

        void BuildGates(Transform parent)
        {
            var start = course.River.At(course.Def.startDistance - 3f);
            Arch(parent, new Gate(start), "Start", palette.Tinted(palette.sign, new Color(0.3f, 0.6f, 1f)));

            for (var i = 0; i < course.Gates.Count; i++)
            {
                var finish = i == course.Gates.Count - 1;
                gateBanners.Add(Arch(parent, course.Gates[i], finish ? "Finish" : $"Checkpoint {i + 1}", palette.sign, chequered: finish));
            }
        }

        /// <summary>Timber posts on the lip each side, a crossbar over the river and a banner hanging from it. Returns the banner's pieces.</summary>
        Renderer[] Arch(Transform parent, Gate gate, string name, Material banner, bool chequered = false)
        {
            var arch = Shapes3D.Group(parent, name);
            var left = gate.LeftEnd - gate.Across * (RimWidth * 0.5f);
            var right = gate.RightEnd + gate.Across * (RimWidth * 0.5f);
            foreach (var post in new[] { left, right })
                Shapes3D.Primitive(PrimitiveType.Cylinder, arch, SimSpace.ToWorld(post, GateHeight * 0.5f), new Vector3(0.4f, GateHeight * 0.5f, 0.4f), palette.wood, name: "Post");

            var middle = (left + right) * 0.5f;
            var span = (right - left).magnitude;
            var yaw = Quaternion.LookRotation(SimSpace.Direction(gate.Downstream)); // local +X runs across the river
            Shapes3D.Primitive(PrimitiveType.Cube, arch, SimSpace.ToWorld(middle, GateHeight), new Vector3(span + 0.6f, 0.28f, 0.28f), palette.wood, yaw, "Crossbar");

            // A lamp hung from the middle of the crossbar, lit at night.
            var lamp = Shapes3D.Group(arch, "Lamp", SimSpace.ToWorld(middle, GateHeight - 0.1f));
            Shapes3D.Primitive(PrimitiveType.Sphere, lamp, Vector3.zero, Vector3.one * 0.45f, palette.Glow(new Color(1f, 0.85f, 0.55f, 0.95f)), name: "Bulb");
            Shapes3D.Primitive(PrimitiveType.Sphere, lamp, Vector3.zero, Vector3.one * 1.3f, palette.Glow(new Color(1f, 0.75f, 0.4f, 0.15f)), name: "Halo");
            if (Looks.Detail >= 0.75f)
            {
                var light = lamp.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.8f, 0.5f);
                light.range = 22f;
                light.intensity = 2.6f;
                light.shadows = LightShadows.None;
            }

            lamp.gameObject.SetActive(night);
            lamps.Add(lamp.gameObject);

            var pieces = new List<Renderer>();
            if (chequered)
            {
                const int squares = 12;
                var white = palette.Tinted(palette.sign, Color.white);
                var black = palette.Tinted(palette.sign, new Color(0.1f, 0.1f, 0.12f));
                var size = span * 0.8f / squares;
                for (var row = 0; row < 2; row++)
                for (var col = 0; col < squares; col++)
                {
                    var offset = gate.Across * ((col - (squares - 1) * 0.5f) * size);
                    var piece = Shapes3D.Primitive(PrimitiveType.Cube, arch, SimSpace.ToWorld(middle - offset, GateHeight - 0.45f - row * size),
                        new Vector3(size, size, 0.1f), (row + col) % 2 == 0 ? white : black, yaw, "Square");
                    pieces.Add(piece.GetComponent<Renderer>());
                }
            }
            else
            {
                var flag = Shapes3D.Primitive(PrimitiveType.Cube, arch, SimSpace.ToWorld(middle, GateHeight - 0.65f), new Vector3(span * 0.55f, 0.9f, 0.1f), banner, yaw, "Banner");
                pieces.Add(flag.GetComponent<Renderer>());
            }

            return pieces.ToArray();
        }

        /// <summary>Foam chevrons drifting with each current at its speed, pointing the way the water goes.</summary>
        void BuildCurrents(Transform parent, System.Random random)
        {
            foreach (var current in course.Def.currents)
            {
                var zone = new CurrentZoneView { From = current.from, To = current.to, Speed = current.speed };
                var count = Mathf.CeilToInt((current.to - current.from) / 1.6f);
                var arrow = palette.Spray(current.speed >= 0f ? new Color(0.8f, 1f, 1f, 0.8f) : new Color(1f, 0.78f, 0.45f, 0.85f), 0f, 0f, 0f, 0f);
                for (var i = 0; i < count; i++)
                {
                    var shape = Shapes3D.Group(parent, "Current");
                    foreach (var side in new[] { -1f, 1f })
                        Shapes3D.Primitive(PrimitiveType.Cube, shape, new Vector3(0.55f * side, 0f, -0.45f), new Vector3(0.22f, 0.02f, 1.5f), arrow,
                            Quaternion.Euler(0f, -38f * side, 0f), "Stroke");
                    chevrons.Add(new Chevron
                    {
                        Shape = shape,
                        Zone = zone,
                        Distance = Mathf.Lerp(current.from, current.to, (i + (float)random.NextDouble()) / count),
                        Across = ((float)random.NextDouble() * 2f - 1f) * 0.6f,
                    });
                }
            }

            Update();
        }

        /// <summary>string.GetHashCode differs between runtimes (Mono on a PC, IL2CPP on a phone); this doesn't.</summary>
        static int StableSeed(string text)
        {
            unchecked
            {
                var hash = (int)2166136261;
                foreach (var c in text) hash = (hash ^ c) * 16777619;
                return hash;
            }
        }

        /// <summary>Collects quads into one mesh, winding each to face the way asked.</summary>
        sealed class MeshBuilder
        {
            readonly List<Vector3> vertices = new();
            readonly List<Vector2> uvs = new();
            readonly List<Vector2> widths = new();
            readonly List<int> triangles = new();

            /// <summary>The river's half width at the last quad's corners, in the second UV channel (the water shader's depth).</summary>
            public void Widths(float a, float b, float c, float d)
            {
                widths[^4] = new Vector2(a, 0f);
                widths[^3] = new Vector2(b, 0f);
                widths[^2] = new Vector2(c, 0f);
                widths[^1] = new Vector2(d, 0f);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                Quad(a, b, c, d, facing);
                uvs[^4] = ua;
                uvs[^3] = ub;
                uvs[^2] = uc;
                uvs[^1] = ud;
            }

            /// <summary>Both faces, for thin things seen from either side (falling water).</summary>
            public void TwoSided(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing)
            {
                Quad(a, b, c, d, facing);
                Quad(a, b, c, d, -facing);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing)
            {
                var first = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                vertices.Add(d);
                for (var k = 0; k < 4; k++)
                {
                    uvs.Add(Vector2.zero);
                    widths.Add(Vector2.zero);
                }
                var flip = Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f; // Unity's front faces wind clockwise
                if (flip) triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
                else triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetUVs(1, widths);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
