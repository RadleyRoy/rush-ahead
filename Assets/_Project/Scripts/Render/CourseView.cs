using System.Collections.Generic;
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
        readonly List<(Transform spiral, float degreesPerSecond)> whirlpools = new();

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

        public static CourseView Build(Course course, ArtPalette palette)
        {
            var view = new GameObject($"Course view: {course.Def.displayName}").AddComponent<CourseView>();
            view.course = course;
            view.palette = palette;
            view.style = BankStyle.For(course.Def.scene, palette);
            var random = new System.Random(StableSeed(course.Def.name)); // the same trees on every device

            var scenery = Shapes3D.Group(view.transform, "Scenery");
            view.BuildWater(scenery);
            view.BuildEdge(scenery, left: true, random);
            view.BuildEdge(scenery, left: false, random);
            view.BuildEnds(scenery, random);
            view.BuildFloor(scenery, random);
            foreach (var rock in course.Def.rocks) CourseDressing.Rock(scenery, rock.position, rock.radius, palette, random);
            if (view.style.LilyPads) view.BuildLilyPads(scenery, random);
            StaticBatchingUtility.Combine(scenery.gameObject); // hundreds of primitives that never move: draw them in a few batches

            var moving = Shapes3D.Group(view.transform, "Moving");
            for (var i = 0; i < course.Barrels.Count; i++)
            {
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
        public void ShowProgress(int gatesPassed)
        {
            if (gatesPassed == shownProgress) return;
            shownProgress = gatesPassed;
            for (var i = 0; i < gateBanners.Count; i++)
            {
                var finish = i == gateBanners.Count - 1;
                if (finish) continue; // the chequered finish keeps its pattern
                var color = i < gatesPassed ? PassedGate : i == gatesPassed ? NextGate : LaterGate;
                foreach (var banner in gateBanners[i]) banner.sharedMaterial = palette.Tinted(palette.sign, color);
            }
        }

        void Update()
        {
            foreach (var (spiral, rate) in whirlpools) spiral.Rotate(0f, rate * Time.deltaTime, 0f);

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
            var samples = course.River.Samples;
            var water = new MeshBuilder();
            for (var i = 0; i < samples.Count - 1; i++)
            {
                var a = samples[i];
                var b = samples[i + 1];
                var wa = a.HalfWidth + WaterUnderLip;
                var wb = b.HalfWidth + WaterUnderLip;
                water.Quad(SimSpace.ToWorld(a.Centre + a.Left * wa), SimSpace.ToWorld(b.Centre + b.Left * wb),
                    SimSpace.ToWorld(b.Centre - b.Left * wb), SimSpace.ToWorld(a.Centre - a.Left * wa), Vector3.up);
            }

            // Out over the lip of the last fall, so the water visibly pours off the end.
            var last = samples[^1];
            var over = last.Centre + last.Tangent * FallLip;
            var width = last.HalfWidth + WaterUnderLip;
            water.Quad(SimSpace.ToWorld(last.Centre + last.Left * width), SimSpace.ToWorld(over + last.Left * width),
                SimSpace.ToWorld(over - last.Left * width), SimSpace.ToWorld(last.Centre - last.Left * width), Vector3.up);
            Shapes3D.MeshObject("Water", water.Build("Water"), parent, Vector3.zero, palette.water);
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
            Shapes3D.MeshObject($"{side} lip", stone.Build($"{side} lip"), parent, Vector3.zero, style.Cliff);
            Shapes3D.MeshObject($"{side} cliff", face.Build($"{side} cliff"), parent, Vector3.zero, style.Cliff);
            Shapes3D.MeshObject($"{side} cliff below", deep.Build($"{side} cliff below"), parent, Vector3.zero, style.CliffDeep);

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
        void BuildWhirlpools(Transform parent)
        {
            var deep = palette.Tinted(palette.water, new Color(0.05f, 0.38f, 0.62f));
            foreach (var pool in course.Def.whirlpools)
            {
                var root = Shapes3D.Group(parent, "Whirlpool", SimSpace.ToWorld(pool.position, 0f));
                Shapes3D.MeshObject("Deep water", Shapes3D.Disc(pool.radius, 32), root, new Vector3(0f, 0.01f, 0f), deep);
                var spiral = Shapes3D.Group(root, "Spiral");
                const int arms = 3, pieces = 12;
                for (var arm = 0; arm < arms; arm++)
                for (var k = 0; k < pieces; k++)
                {
                    var t = k / (pieces - 1f);
                    var angle = (arm * 360f / arms + t * 300f) * Mathf.Deg2Rad;
                    var radius = pool.radius * (0.12f + 0.8f * t);
                    var at = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    var along = Quaternion.LookRotation(new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)));
                    Shapes3D.Primitive(PrimitiveType.Cube, spiral, at + Vector3.up * 0.04f, new Vector3(0.12f + 0.1f * t, 0.02f, 0.5f + 0.5f * t), palette.foam, along, "Foam");
                }

                // A positive spin turns sim x toward sim y, which in the world is x toward z: a negative turn about y.
                var degreesPerSecond = -pool.spin / Mathf.Max(0.5f, pool.radius * 0.5f) * Mathf.Rad2Deg;
                whirlpools.Add((spiral, degreesPerSecond));
            }
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

        /// <summary>Bamboo posts on the lip each side, a crossbar over the river and a banner hanging from it. Returns the banner's pieces.</summary>
        Renderer[] Arch(Transform parent, Gate gate, string name, Material banner, bool chequered = false)
        {
            var arch = Shapes3D.Group(parent, name);
            var left = gate.LeftEnd - gate.Across * (RimWidth * 0.5f);
            var right = gate.RightEnd + gate.Across * (RimWidth * 0.5f);
            foreach (var post in new[] { left, right })
                Shapes3D.Primitive(PrimitiveType.Cylinder, arch, SimSpace.ToWorld(post, GateHeight * 0.5f), new Vector3(0.4f, GateHeight * 0.5f, 0.4f), palette.bamboo, name: "Post");

            var middle = (left + right) * 0.5f;
            var span = (right - left).magnitude;
            var yaw = Quaternion.LookRotation(SimSpace.Direction(gate.Downstream)); // local +X runs across the river
            Shapes3D.Primitive(PrimitiveType.Cube, arch, SimSpace.ToWorld(middle, GateHeight), new Vector3(span + 0.6f, 0.28f, 0.28f), palette.wood, yaw, "Crossbar");

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
                var count = Mathf.CeilToInt((current.to - current.from) / 2.5f);
                for (var i = 0; i < count; i++)
                {
                    var shape = Shapes3D.Group(parent, "Current");
                    foreach (var side in new[] { -1f, 1f })
                        Shapes3D.Primitive(PrimitiveType.Cube, shape, new Vector3(0.32f * side, 0f, -0.25f), new Vector3(0.1f, 0.02f, 0.9f), palette.foam,
                            Quaternion.Euler(0f, -35f * side, 0f), "Stroke");
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
            readonly List<int> triangles = new();

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
                var flip = Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f; // Unity's front faces wind clockwise
                if (flip) triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
                else triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
