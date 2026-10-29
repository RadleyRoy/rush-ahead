using System.Collections.Generic;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// How a river course looks: continuous clay cliffs with grass tops along both banks (built as meshes that follow
    /// the river's curve exactly, so the cliff face is where the collider is), trees and bamboo, rocks with foam,
    /// floating barrels, an arch over every checkpoint that lights up as the next one and turns green once passed,
    /// a chequered finish, and drifting foam chevrons wherever the water flows. Purely visual: the physics is the course's.
    /// </summary>
    public sealed class CourseView : MonoBehaviour
    {
        const float LandWidth = 26f; // grass beyond each bank
        const float PlainCell = 4f;
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

        /// <summary>The flat country beyond the banks, a little below the lowest bank top.</summary>
        float PlainHeight => style.Height - 0.25f;
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
            view.BuildBank(scenery, left: true, random);
            view.BuildBank(scenery, left: false, random);
            view.BuildEnds(scenery, random);
            view.BuildPlain(scenery);
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

        void BuildWater(Transform parent)
        {
            var min = Vector2.positiveInfinity;
            var max = Vector2.negativeInfinity;
            foreach (var point in course.River.Outline())
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            var margin = new Vector2(LandWidth + 60f, LandWidth + 60f);
            CourseDressing.Water(parent, (min + max) * 0.5f, max - min + 2f * margin, palette);
        }

        /// <summary>
        /// One bank as two strips: the cliff face from below the water up to a gently varying height, and the grass on
        /// top running back from the edge. On the inside of a bend the grass is cut short so it never folds over itself.
        /// </summary>
        void BuildBank(Transform parent, bool left, System.Random random)
        {
            var samples = course.River.Samples;
            var count = samples.Count;
            var edge = new Vector2[count];
            var outward = new Vector2[count];
            var height = new float[count];
            var depth = new float[count];
            var seed = (float)random.NextDouble() * 100f;

            for (var i = 0; i < count; i++)
            {
                var s = samples[i];
                outward[i] = left ? s.Left : -s.Left;
                edge[i] = s.Centre + outward[i] * s.HalfWidth;
                height[i] = style.Height + style.HeightRange * Mathf.PerlinNoise(seed, s.Distance * 0.05f);

                // Bending toward this bank puts the centre of the bend on this side, 1/curvature from the middle.
                var towardThisBank = left ? s.Curvature : -s.Curvature;
                depth[i] = towardThisBank > 1e-4f ? Mathf.Clamp(0.8f * (1f / towardThisBank - s.HalfWidth), 1f, LandWidth) : LandWidth;
            }

            for (var pass = 0; pass < 2; pass++) // spread each cut a little either way, so the land's edge stays smooth
                for (var i = 0; i < count; i++)
                    for (var j = Mathf.Max(0, i - 4); j <= Mathf.Min(count - 1, i + 4); j++)
                        depth[i] = Mathf.Min(depth[i], depth[j] + 0.6f * Mathf.Abs(i - j));

            var cliff = new MeshBuilder();
            var grass = new MeshBuilder();
            for (var i = 0; i < count - 1; i++)
            {
                var j = i + 1;
                var water = -SimSpace.Direction((outward[i] + outward[j]) * 0.5f); // the face looks out over the river
                cliff.Quad(SimSpace.ToWorld(edge[i], -0.6f), SimSpace.ToWorld(edge[j], -0.6f),
                    SimSpace.ToWorld(edge[j], height[j]), SimSpace.ToWorld(edge[i], height[i]), water);

                const float lip = 0.2f; // the grass overhangs the cliff a little, like the reference's turf
                grass.Quad(SimSpace.ToWorld(edge[i] - outward[i] * lip, height[i] + 0.1f), SimSpace.ToWorld(edge[j] - outward[j] * lip, height[j] + 0.1f),
                    SimSpace.ToWorld(edge[j] + outward[j] * depth[j], height[j] + 0.1f), SimSpace.ToWorld(edge[i] + outward[i] * depth[i], height[i] + 0.1f), Vector3.up);
                grass.Quad(SimSpace.ToWorld(edge[i] - outward[i] * lip, height[i] - 0.15f), SimSpace.ToWorld(edge[j] - outward[j] * lip, height[j] - 0.15f),
                    SimSpace.ToWorld(edge[j] - outward[j] * lip, height[j] + 0.1f), SimSpace.ToWorld(edge[i] - outward[i] * lip, height[i] + 0.1f), water);
            }

            var side = left ? "Left" : "Right";
            Shapes3D.MeshObject($"{side} cliff", cliff.Build($"{side} cliff"), parent, Vector3.zero, style.Cliff);
            Shapes3D.MeshObject($"{side} grass", grass.Build($"{side} grass"), parent, Vector3.zero, style.Ground);

            // Plants: a fringe along the edge (bamboo, reeds or pines), then sparser woods further back.
            for (var i = 0; i < count; i += 3)
            {
                var ground = height[i] + 0.1f;
                var roll = random.NextDouble();
                if (roll < 0.45 && depth[i] > 3f)
                    style.Plant(parent, edge[i] + outward[i] * (2f + (float)random.NextDouble() * Mathf.Min(5f, depth[i] - 2.5f)), ground, palette, random);
                else if (roll < 0.7)
                    style.Fringe(parent, edge[i] + outward[i] * 1f, ground, palette, random);

                if (i % 6 == 0 && depth[i] > 12f)
                    style.Plant(parent, edge[i] + outward[i] * (8f + (float)random.NextDouble() * (depth[i] - 10f)), ground, palette, random);
            }
        }

        /// <summary>
        /// Flat grassland filling everything that isn't river, so the inside of a bend (where the banks' own grass is
        /// cut short) and the far distance read as countryside rather than open sea. Built from square cells, leaving
        /// out any cell that could reach the water; the banks' raised grass covers the edge.
        /// </summary>
        void BuildPlain(Transform parent)
        {
            var min = Vector2.positiveInfinity;
            var max = Vector2.negativeInfinity;
            foreach (var point in course.River.Outline())
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            min -= Vector2.one * (LandWidth + 40f);
            max += Vector2.one * (LandWidth + 40f);
            var reach = PlainCell * 0.75f; // a cell's centre to its corner, plus a margin
            var plain = new MeshBuilder();
            for (var x = min.x; x < max.x; x += PlainCell)
            for (var y = min.y; y < max.y; y += PlainCell)
            {
                var centre = new Vector2(x + PlainCell * 0.5f, y + PlainCell * 0.5f);
                var at = course.River.Project(centre);
                var pastTheEnds = at.Distance <= 0.01f || at.Distance >= course.River.Length - 0.01f;
                if (!pastTheEnds && Mathf.Abs(at.Lateral) < at.HalfWidth + reach) continue;
                if (pastTheEnds && Mathf.Abs(at.Lateral) < at.HalfWidth + reach && Vector2.Distance(centre, ClosestEnd(centre)) < reach + 2f) continue;

                plain.Quad(SimSpace.ToWorld(new Vector2(x, y), PlainHeight), SimSpace.ToWorld(new Vector2(x + PlainCell, y), PlainHeight),
                    SimSpace.ToWorld(new Vector2(x + PlainCell, y + PlainCell), PlainHeight), SimSpace.ToWorld(new Vector2(x, y + PlainCell), PlainHeight), Vector3.up);
            }

            Shapes3D.MeshObject("Plain", plain.Build("Plain"), parent, Vector3.zero, style.Ground);
        }

        Vector2 ClosestEnd(Vector2 point)
        {
            var samples = course.River.Samples;
            return Vector2.Distance(point, samples[0].Centre) < Vector2.Distance(point, samples[^1].Centre) ? samples[0].Centre : samples[^1].Centre;
        }

        /// <summary>Cliffs across the top and bottom of the river, reaching back over the land on both sides.</summary>
        void BuildEnds(Transform parent, System.Random random)
        {
            var samples = course.River.Samples;
            var first = samples[0];
            var last = samples[^1];
            // CliffBank builds to the right of its direction: across the start from the left bank, across the end from the right.
            CourseDressing.CliffBank(parent, first.LeftBank + first.Left * LandWidth, first.RightBank - first.Left * LandWidth, 12f, palette, random, style);
            CourseDressing.CliffBank(parent, last.RightBank - last.Left * LandWidth, last.LeftBank + last.Left * LandWidth, 12f, palette, random, style);
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

        /// <summary>Bamboo posts on each bank, a crossbar over the river and a banner hanging from it. Returns the banner's pieces.</summary>
        Renderer[] Arch(Transform parent, Gate gate, string name, Material banner, bool chequered = false)
        {
            var arch = Shapes3D.Group(parent, name);
            var left = gate.LeftEnd + gate.Across * 0.6f;
            var right = gate.RightEnd - gate.Across * 0.6f;
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
