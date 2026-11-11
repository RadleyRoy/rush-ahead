using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>A timber mill on a stone footing with its big wheel turning in the river and foam streaks drawn toward it.</summary>
    sealed class MillRaceView : ObstacleView
    {
        readonly MillRace mill;
        readonly Transform hub;
        readonly Transform[] streaks = new Transform[10];
        readonly float[] streakAlong = new float[10];

        public MillRaceView(MillRace mill, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Mill race"), palette)
        {
            this.mill = mill;
            var at = mill.At;
            var side = mill.Spot.side;
            var house = at.Centre + at.Left * (side * (at.HalfWidth + 2.8f));
            Built.Footing(Root, house, at.Tangent, 5.2f, 6.4f, palette, 0.4f);
            var walls = Tint(palette.animal, new Color(0.78f, 0.62f, 0.44f));
            var roof = Tint(palette.animal, new Color(0.55f, 0.22f, 0.16f));
            var building = Shapes3D.Group(Root, "Mill");
            building.SetPositionAndRotation(SimSpace.ToWorld(house), Facing(at.Tangent));
            Part(PrimitiveType.Cube, building, new Vector3(0f, 2f, 0f), new Vector3(4.2f, 3.2f, 5.2f), walls, name: "Walls");
            Part(PrimitiveType.Cube, building, new Vector3(0f, 3.6f, 0f), new Vector3(3.2f, 3.2f, 5.6f), roof, Quaternion.Euler(0f, 0f, 45f), "Roof");
            Part(PrimitiveType.Cube, building, new Vector3(-side * 2.12f, 1.5f, 0.8f), new Vector3(0.05f, 1.8f, 1f), Tint(palette.animal, new Color(0.3f, 0.22f, 0.15f)), name: "Door");

            var wheel = Shapes3D.Group(Root, "Wheel");
            wheel.SetPositionAndRotation(SimSpace.ToWorld(mill.Wheel, 0.9f), Facing(at.Tangent));
            hub = Shapes3D.Group(wheel, "Hub");
            var timber = Tint(palette.wood, new Color(0.5f, 0.38f, 0.26f));
            var rim = Shapes3D.Torus(MillRace.WheelRadius, 0.12f, 40, 8);
            foreach (var x in new[] { -0.5f, 0.5f })
                Shapes3D.MeshObject("Rim", rim, hub, new Vector3(x * MillRace.WheelWidth, 0f, 0f), timber).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            for (var p = 0; p < 12; p++)
            {
                var turn = Quaternion.Euler(p * 30f, 0f, 0f);
                Part(PrimitiveType.Cube, hub, turn * new Vector3(0f, MillRace.WheelRadius - 0.35f, 0f), new Vector3(MillRace.WheelWidth, 0.1f, 0.8f), palette.Planks, turn, "Paddle");
                if (p % 3 == 0) Part(PrimitiveType.Cube, hub, Vector3.zero, new Vector3(0.12f, MillRace.WheelRadius * 2f, 0.16f), timber, turn, "Spoke");
            }

            Part(PrimitiveType.Cylinder, wheel, new Vector3(side * 1.4f, 0f, 0f), new Vector3(0.35f, 1.6f, 0.35f), palette.Metal, Quaternion.Euler(0f, 0f, 90f), "Axle");
            Part(PrimitiveType.Sphere, wheel, new Vector3(0f, -0.8f, 0f), new Vector3(3.6f, 0.4f, 5.2f), palette.Spray(new Color(1f, 1f, 1f, 0.75f), 0.6f, 0f, 1.2f), name: "Churn");

            var random = new System.Random(Mathf.RoundToInt(mill.Spot.distance * 9f));
            for (var i = 0; i < streaks.Length; i++)
            {
                streakAlong[i] = ((float)random.NextDouble() * 2f - 1f) * (MillRace.Along - 1f);
                streaks[i] = Part(PrimitiveType.Cube, Root, Vector3.zero, new Vector3(0.12f, 0.03f, 1.8f), palette.foam, name: "Streak").transform;
            }
        }

        public override void Pose(double seconds)
        {
            var t = (float)seconds;
            var at = mill.At;
            var side = mill.Spot.side;
            hub.localRotation = Quaternion.Euler(-side * mill.WheelAngle(seconds), 0f, 0f);
            for (var i = 0; i < streaks.Length; i++)
            {
                var along = streakAlong[i];
                var pull = 1f - Mathf.Abs(along) / MillRace.Along;
                var across = Mathf.Lerp(-side * at.HalfWidth * 0.7f, side * (at.HalfWidth - 1.5f), Mathf.Repeat(t * 0.4f * (0.5f + pull) + i * 0.37f, 1f));
                var point = at.Centre + at.Tangent * along + at.Left * across;
                streaks[i].SetPositionAndRotation(SimSpace.ToWorld(point, 0.05f), Facing(at.Left * side + at.Tangent * 0.3f));
            }
        }
    }

    /// <summary>Two planked leaves on stone piers that lower to meet over the river and lift again, red lights flashing first.</summary>
    sealed class DrawbridgeView : ObstacleView
    {
        readonly Drawbridge bridge;
        readonly Transform[] leaves = new Transform[2];
        readonly Quaternion[] down = new Quaternion[2];
        readonly MeshRenderer[] lights = new MeshRenderer[4];
        readonly Material lit;
        readonly Material dark;

        public DrawbridgeView(Drawbridge bridge, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Drawbridge"), palette)
        {
            this.bridge = bridge;
            var at = bridge.At;
            var stone = palette.cliff;
            var rail = Tint(palette.animal, new Color(0.4f, 0.3f, 0.22f));
            var timber = Tint(palette.animal, new Color(0.66f, 0.5f, 0.34f));
            var red = Tint(palette.sign, new Color(0.9f, 0.15f, 0.12f));
            var white = Tint(palette.sign, new Color(0.95f, 0.95f, 0.92f));
            lit = Tint(palette.sign, new Color(1f, 0.1f, 0.05f));
            dark = Tint(palette.sign, new Color(0.25f, 0.04f, 0.03f));
            var reach = at.HalfWidth + 0.1f;
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var pier = at.Centre + at.Left * (side * (at.HalfWidth + 1f));
                Built.Footing(Root, pier, at.Tangent, 1.8f, Drawbridge.Deck + 1.6f, palette, 2.2f);
                Part(PrimitiveType.Cube, Root, SimSpace.ToWorld(pier, 2.9f), new Vector3(1.4f, 1.4f, 1.4f), stone, Facing(at.Tangent), "Tower");
                for (var l = 0; l < 2; l++)
                    lights[s * 2 + l] = Part(PrimitiveType.Sphere, Root, SimSpace.ToWorld(pier + at.Tangent * ((l == 0 ? -1f : 1f) * 0.45f), 3.85f), Vector3.one * 0.35f, dark, name: "Light")
                        .GetComponent<MeshRenderer>();

                var hinge = at.Centre + at.Left * (side * (at.HalfWidth + 0.1f));
                var leaf = leaves[s] = Shapes3D.Group(Root, "Leaf");
                leaf.position = SimSpace.ToWorld(hinge, 0.95f);
                down[s] = Facing(-side * at.Left);
                Part(PrimitiveType.Cube, leaf, new Vector3(0f, 0f, reach * 0.5f), new Vector3(Drawbridge.Deck, 0.35f, reach), timber, name: "Deck");
                foreach (var x in new[] { -1f, 1f })
                    Part(PrimitiveType.Cube, leaf, new Vector3(x * (Drawbridge.Deck * 0.5f - 0.06f), 0.45f, reach * 0.5f), new Vector3(0.1f, 0.55f, reach), rail, name: "Rail");
                for (var b = 0; b < 4; b++)
                    Part(PrimitiveType.Cube, leaf, new Vector3(0f, 0.19f, reach - 0.25f - b * 0.5f), new Vector3(Drawbridge.Deck * 0.98f, 0.03f, 0.5f), b % 2 == 0 ? red : white, name: "Stripe");
            }
        }

        public override void Pose(double seconds)
        {
            var open = bridge.Open(seconds);
            for (var s = 0; s < 2; s++) leaves[s].rotation = down[s] * Quaternion.Euler(-82f * open, 0f, 0f);
            var warning = bridge.Warning(seconds);
            var flip = Mathf.Repeat((float)seconds * 2.5f, 1f) < 0.5f;
            for (var i = 0; i < lights.Length; i++) lights[i].sharedMaterial = warning && (i % 2 == 0) == flip ? lit : dark;
        }
    }

    /// <summary>A yellow tower crane on the bank by a stack of crates, its trolley running out along the jib to drop one in the river.</summary>
    sealed class CargoCraneView : ObstacleView
    {
        const float MastHeight = 8.6f;
        readonly CargoCrane crane;
        readonly Transform jib;
        readonly Transform trolley;
        readonly Transform cable;
        readonly Transform crate;
        readonly Transform shadow;
        readonly Material shade;
        readonly Transform splash;

        public CargoCraneView(CargoCrane crane, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Cargo crane"), palette)
        {
            this.crane = crane;
            var at = crane.At;
            var yellow = Tint(palette.Metal, new Color(0.96f, 0.74f, 0.12f));
            var black = Tint(palette.Metal, new Color(0.1f, 0.1f, 0.1f));
            Built.Footing(Root, (crane.Base + crane.Stack) * 0.5f, at.Tangent, 4f, 6.5f, palette);
            Part(PrimitiveType.Cube, Root, SimSpace.ToWorld(crane.Base, MastHeight * 0.5f), new Vector3(0.9f, MastHeight, 0.9f), yellow, name: "Mast");
            for (var b = 0; b < 5; b++)
                Part(PrimitiveType.Cube, Root, SimSpace.ToWorld(crane.Base, 0.8f + b * 1.6f), new Vector3(0.95f, 0.12f, 0.95f), black, Quaternion.Euler(0f, 45f, 0f), "Brace");
            jib = Shapes3D.Group(Root, "Jib");
            jib.position = SimSpace.ToWorld(crane.Base, MastHeight);
            var length = at.HalfWidth * 1.45f + 7f;
            Part(PrimitiveType.Cube, jib, new Vector3(0f, 0.3f, length * 0.5f), new Vector3(0.6f, 0.6f, length), yellow, name: "Arm");
            Part(PrimitiveType.Cube, jib, new Vector3(0f, 0.3f, -1.8f), new Vector3(0.6f, 0.6f, 3.6f), yellow, name: "Counter-jib");
            Part(PrimitiveType.Cube, jib, new Vector3(0f, -0.1f, -3.2f), new Vector3(1.4f, 1.2f, 1f), Tint(palette.rock, new Color(0.6f, 0.6f, 0.58f)), name: "Counterweight");
            Part(PrimitiveType.Cube, jib, new Vector3(0f, -0.5f, 0.9f), new Vector3(1.3f, 1.1f, 1.3f), yellow, name: "Cab");
            Part(PrimitiveType.Cube, jib, new Vector3(0f, -0.4f, 1.56f), new Vector3(1.1f, 0.6f, 0.04f), Tint(palette.Metal, new Color(0.2f, 0.3f, 0.38f)), name: "Window");
            trolley = Part(PrimitiveType.Cube, Root, Vector3.zero, new Vector3(0.8f, 0.35f, 0.8f), black, name: "Trolley").transform;
            cable = Part(PrimitiveType.Cylinder, Root, Vector3.zero, Vector3.one, black, name: "Cable").transform;

            var wood = Tint(palette.animal, new Color(0.8f, 0.6f, 0.36f));
            crate = MakeCrate(palette, wood);
            var stack = new[] { new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, 1f), new Vector3(0f, 1.9f, 0f) };
            foreach (var offset in stack)
            {
                var piece = MakeCrate(palette, wood);
                piece.SetPositionAndRotation(SimSpace.ToWorld(crane.Stack, 0.3f + 0.95f) + Facing(at.Tangent) * offset, Facing(at.Tangent));
            }

            shade = new Material(palette.foam) { name = "Crate shadow", color = new Color(0f, 0f, 0f, 0f) };
            shadow = Part(PrimitiveType.Cube, Root, Vector3.zero, new Vector3(CargoCrane.Crate * 2f, 0.02f, CargoCrane.Crate * 2f), shade, name: "Shadow").transform;
            Shapes3D.MeshObject("Target", Shapes3D.Torus(CargoCrane.Crate * 1.45f, 0.08f, 4, 6), shadow, new Vector3(0f, 1f, 0f), Tint(palette.sign, new Color(1f, 0.25f, 0.15f)))
                .transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            splash = Part(PrimitiveType.Sphere, Root, Vector3.zero, Vector3.one, palette.Spray(new Color(1f, 1f, 1f, 0.75f), 0.5f, 0f, 1.5f), name: "Splash").transform;
        }

        Transform MakeCrate(ArtPalette palette, Material wood)
        {
            var size = CargoCrane.Crate * 2f;
            var box = Shapes3D.Group(Root, "Crate");
            Part(PrimitiveType.Cube, box, Vector3.zero, Vector3.one * size, wood, name: "Box");
            Part(PrimitiveType.Cube, box, Vector3.zero, new Vector3(size * 1.02f, size * 0.2f, size * 1.02f), Tint(palette.animal, new Color(0.45f, 0.3f, 0.18f)), name: "Band");
            Part(PrimitiveType.Cube, box, Vector3.zero, new Vector3(size * 0.2f, size * 1.02f, size * 1.02f), Tint(palette.animal, new Color(0.45f, 0.3f, 0.18f)), name: "Band");
            return box;
        }

        public override void Pose(double seconds)
        {
            var t = (float)seconds;
            var hook = crane.Hook(seconds);
            var out_ = hook - crane.Base;
            jib.rotation = Facing(out_.sqrMagnitude > 1e-4f ? out_.normalized : crane.At.Left);
            var trolleyAt = SimSpace.ToWorld(hook, MastHeight - 0.05f);
            trolley.SetPositionAndRotation(trolleyAt, jib.rotation);

            var (position, height, hooked) = crane.CratePose(seconds);
            var shown = height > -3f;
            Built.Show(crate, shown);
            var floating = !hooked && height <= 0.01f;
            var bottom = floating ? height - 0.4f + 0.06f * Mathf.Sin(t * 3f) : height;
            if (shown) crate.SetPositionAndRotation(SimSpace.ToWorld(position, bottom + CargoCrane.Crate), Facing(crane.At.Tangent) * Quaternion.Euler(floating ? 4f * Mathf.Sin(t * 2f) : 0f, 0f, 0f));
            var hookBottom = hooked ? SimSpace.ToWorld(position, height + CargoCrane.Crate * 2f) : trolleyAt + Vector3.down * 1.2f;
            Built.Span(cable, trolleyAt, hookBottom, 0.06f);

            var dark = crane.Shadow(seconds);
            Built.Show(shadow, dark > 0f);
            shadow.SetPositionAndRotation(SimSpace.ToWorld(crane.Target(seconds), 0.03f), Facing(crane.At.Tangent));
            shade.color = new Color(0f, 0f, 0f, 0.5f * dark);
            var phase = crane.CranePhase(seconds);
            var splashing = phase >= CargoCrane.Land - 0.01f && phase < CargoCrane.Land + 0.06f;
            Built.Show(splash, splashing);
            if (splashing)
            {
                splash.position = SimSpace.ToWorld(crane.Target(seconds), 0.3f);
                splash.localScale = Vector3.one * (1.5f + 30f * (phase - CargoCrane.Land + 0.01f));
            }
        }
    }

    /// <summary>A sawmill shack and its wooden chute down the bank, logs shooting out of it into the river.</summary>
    sealed class LogFlumeView : ObstacleView
    {
        readonly LogFlume flume;
        readonly Transform[] logs = new Transform[2];
        readonly Quaternion chute;
        readonly Transform splash;

        public LogFlumeView(LogFlume flume, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Log flume"), palette)
        {
            this.flume = flume;
            var at = flume.At;
            var outward = -flume.Spot.side * at.Left;
            var top = SimSpace.ToWorld(flume.Top, LogFlume.ChuteHeight + 0.1f);
            var bottom = SimSpace.ToWorld(flume.Exit + outward * 0.6f, 0.15f);
            var line = bottom - top;
            chute = Quaternion.LookRotation(line.normalized);
            var trough = Shapes3D.Group(Root, "Chute");
            trough.SetPositionAndRotation((top + bottom) * 0.5f, chute);
            var board = Tint(palette.animal, new Color(0.6f, 0.46f, 0.32f));
            Part(PrimitiveType.Cube, trough, Vector3.zero, new Vector3(1.3f, 0.12f, line.magnitude + 0.4f), board, name: "Floor");
            foreach (var x in new[] { -1f, 1f })
                Part(PrimitiveType.Cube, trough, new Vector3(x * 0.65f, 0.28f, 0f), new Vector3(0.1f, 0.55f, line.magnitude + 0.4f), board, name: "Side");
            Part(PrimitiveType.Cube, trough, new Vector3(0f, 0.1f, 0f), new Vector3(1.15f, 0.06f, line.magnitude + 0.3f), palette.Spray(new Color(0.8f, 0.92f, 0.95f, 0.75f), 0.9f, 0f, -2f), name: "Water");
            for (var i = 0; i < 3; i++)
            {
                var foot = Vector3.Lerp(top, bottom, 0.2f + i * 0.28f);
                Part(PrimitiveType.Cylinder, Root, new Vector3(foot.x, (foot.y - 6f) * 0.5f, foot.z), new Vector3(0.22f, (foot.y + 6f) * 0.5f, 0.22f), palette.wood, name: "Stilt");
            }

            var shack = flume.Top - outward * 2.6f;
            Built.Footing(Root, shack, at.Tangent, 4.6f, 5f, palette, LogFlume.ChuteHeight - 1.2f);
            var mill = Shapes3D.Group(Root, "Sawmill");
            mill.SetPositionAndRotation(SimSpace.ToWorld(shack, LogFlume.ChuteHeight - 1.2f), Facing(at.Tangent));
            Part(PrimitiveType.Cube, mill, new Vector3(0f, 1.5f, 0f), new Vector3(3.6f, 3f, 4.2f), Tint(palette.animal, new Color(0.72f, 0.56f, 0.38f)), name: "Walls");
            Part(PrimitiveType.Cube, mill, new Vector3(0f, 3.2f, 0f), new Vector3(4f, 0.25f, 4.8f), Tint(palette.animal, new Color(0.42f, 0.33f, 0.26f)), Quaternion.Euler(0f, 0f, 8f), "Roof");
            for (var i = 0; i < 3; i++)
                Part(PrimitiveType.Cylinder, mill, new Vector3(-1.4f, 0.3f + i * 0.55f, -2.6f), new Vector3(0.5f, 1.8f, 0.5f), palette.wood, Quaternion.Euler(0f, 0f, 90f), "Log pile");

            var bark = Tint(palette.wood, new Color(0.42f, 0.31f, 0.2f));
            var cut = Tint(palette.wood, new Color(0.85f, 0.68f, 0.45f));
            for (var i = 0; i < logs.Length; i++)
            {
                var log = logs[i] = Shapes3D.Group(Root, "Log");
                Part(PrimitiveType.Cylinder, log, Vector3.zero, new Vector3(LogFlume.LogRadius * 2f, LogFlume.LogLength * 0.5f, LogFlume.LogRadius * 2f), bark, Quaternion.Euler(90f, 0f, 0f), "Trunk");
                foreach (var z in new[] { -1f, 1f })
                    Part(PrimitiveType.Cylinder, log, new Vector3(0f, 0f, z * LogFlume.LogLength * 0.5f), new Vector3(LogFlume.LogRadius * 1.9f, 0.02f, LogFlume.LogRadius * 1.9f), cut,
                        Quaternion.Euler(90f, 0f, 0f), "End");
            }

            splash = Part(PrimitiveType.Sphere, Root, SimSpace.ToWorld(flume.Exit + outward * 1.5f, 0.3f), Vector3.one, palette.Spray(new Color(1f, 1f, 1f, 0.7f), 0.5f, 0f, 1.5f), name: "Splash").transform;
        }

        public override void Pose(double seconds)
        {
            var t = (float)seconds;
            var newest = flume.Newest(seconds);
            var splashing = 0f;
            for (var j = 0; j < logs.Length; j++)
            {
                var k = newest - j;
                var (centre, axis, height, inWater, alive) = flume.Log(k, seconds);
                Built.Show(logs[j], alive);
                if (!alive) continue;
                var roll = Quaternion.Euler(0f, 0f, t * 40f + k * 50f);
                if (inWater)
                {
                    logs[j].SetPositionAndRotation(SimSpace.ToWorld(centre, height + 0.1f + 0.04f * Mathf.Sin(t * 2.5f + k)), Quaternion.LookRotation(SimSpace.Direction(axis)) * roll);
                    var age = (float)((seconds + flume.Spot.offset) / flume.Spot.period - k) - LogFlume.Chute;
                    if (age < 0.12f) splashing = Mathf.Max(splashing, 1f - age / 0.12f);
                }
                else logs[j].SetPositionAndRotation(SimSpace.ToWorld(centre, height), chute);
            }

            Built.Show(splash, splashing > 0f);
            splash.localScale = new Vector3(2.2f, 1.2f, 2.2f) * (0.4f + splashing);
        }
    }

    /// <summary>A pale blue ice cliff on the bank; a block cracks, topples into the river, throws up a splash and a wave, and bobs away melting.</summary>
    sealed class CalvingGlacierView : ObstacleView
    {
        readonly CalvingGlacier glacier;
        readonly Transform block;
        readonly Transform[] cracks = new Transform[3];
        readonly Transform wave;
        readonly Transform plume;
        readonly Vector2 lip;
        readonly Quaternion facing;

        public CalvingGlacierView(CalvingGlacier glacier, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Calving glacier"), palette)
        {
            this.glacier = glacier;
            var at = glacier.At;
            var side = glacier.Spot.side;
            var ice = Tint(palette.animal, new Color(0.84f, 0.94f, 1f));
            var blue = Tint(palette.animal, new Color(0.58f, 0.78f, 0.95f));
            var random = new System.Random(Mathf.RoundToInt(glacier.Spot.distance * 13f));
            Shapes3D.MeshObject("Ice cliff", Bluff(at, side, CalvingGlacier.Along + 2f, 6.5f, 4.5f, random), Root, Vector3.zero, Tint(palette.animal, new Color(0.88f, 0.95f, 1f)));
            for (var i = 0; i < 6; i++)
            {
                var along = ((float)random.NextDouble() * 2f - 1f) * (CalvingGlacier.Along + 1f);
                var size = 1.2f + (float)random.NextDouble() * 1.6f;
                Part(PrimitiveType.Cube, Root, SimSpace.ToWorld(at.Centre + at.Tangent * along + at.Left * (side * (at.HalfWidth + 1.6f + (float)random.NextDouble() * 2f)), 2.6f + (float)random.NextDouble() * 1.2f),
                    new Vector3(size, size * 1.4f, size), i % 2 == 0 ? ice : blue, Quaternion.Euler((float)random.NextDouble() * 20f, (float)random.NextDouble() * 90f, (float)random.NextDouble() * 20f), "Serac");
            }

            lip = at.Centre + at.Left * (side * (at.HalfWidth + 1.2f));
            facing = Facing(-side * at.Left);
            block = Shapes3D.Group(Root, "Block");
            Part(PrimitiveType.Cube, block, new Vector3(0f, 2.3f, 0f), new Vector3(3.4f, 4.6f, 2.4f), ice, name: "Ice");
            Part(PrimitiveType.Cube, block, new Vector3(0f, 2.3f, 0.9f), new Vector3(3f, 4.2f, 0.7f), blue, name: "Face");
            var crackColour = Tint(palette.animal, new Color(0.15f, 0.3f, 0.5f));
            for (var i = 0; i < cracks.Length; i++)
                cracks[i] = Part(PrimitiveType.Cube, block, new Vector3((i - 1f) * 0.9f, 2.4f, 1.27f), new Vector3(0.07f, 3.6f, 0.04f), crackColour, Quaternion.Euler(0f, 0f, (i - 1f) * 14f), "Crack").transform;

            wave = Shapes3D.Group(Root, "Wave");
            Shapes3D.MeshObject("Water", Built.WaveMesh(), wave, Vector3.zero, Built.TwoSided(palette.Spray(new Color(0.42f, 0.7f, 0.74f, 0.92f), 0.9f, 0f, 0f, 0.5f)));
            Part(PrimitiveType.Capsule, wave, new Vector3(0f, 1.5f, -0.65f), new Vector3(0.6f, 1f, 0.75f), palette.Spray(new Color(1f, 1f, 1f, 0.95f), 0.6f, 0f, 1.5f),
                Quaternion.Euler(0f, 0f, 90f), "Crest");
            Part(PrimitiveType.Cube, wave, new Vector3(0f, 0.04f, 1.5f), new Vector3(2f, 0.04f, 3f), palette.foam, name: "Foam");
            plume = Part(PrimitiveType.Sphere, Root, SimSpace.ToWorld(at.Centre + at.Left * (side * (at.HalfWidth - 1f)), 1f), new Vector3(6f, 4f, 6f),
                palette.Spray(new Color(1f, 1f, 1f, 0.75f), 0.5f, 0f, 2f), name: "Splash").transform;
        }

        public override void Pose(double seconds)
        {
            var t = (float)seconds;
            var at = glacier.At;
            var side = glacier.Spot.side;
            var phase = glacier.IcePhase(seconds);
            var into = -side * at.Left;

            Vector3 position;
            var rotation = facing;
            var scale = 1f;
            if (phase < CalvingGlacier.Calve)
            {
                // Growing out of the cliff at the start of the round, shuddering once it cracks.
                var grow = Mathf.Clamp01(phase / 0.1f);
                position = SimSpace.ToWorld(lip - into * (2.2f * (1f - grow)));
                if (phase >= CalvingGlacier.Crack) position += new Vector3(Mathf.Sin(t * 47f), 0f, Mathf.Cos(t * 39f)) * 0.06f;
            }
            else if (phase < CalvingGlacier.Splash)
            {
                var u = (phase - CalvingGlacier.Calve) / (CalvingGlacier.Splash - CalvingGlacier.Calve);
                position = SimSpace.ToWorld(lip + into * (2.2f * u), -2.2f * u * u);
                rotation = facing * Quaternion.Euler(90f * u * u, 0f, 0f);
            }
            else
            {
                var u = (phase - CalvingGlacier.Splash) / (1f - CalvingGlacier.Splash);
                position = SimSpace.ToWorld(lip + into * 2.4f + at.Tangent * (2f * u), -1.4f - 0.15f * Mathf.Sin(t * 2f) - 1.2f * u * u);
                rotation = facing * Quaternion.Euler(90f + 5f * Mathf.Sin(t * 1.6f), 0f, 0f);
                scale = Mathf.Lerp(1f, 0.45f, u);
            }

            block.SetPositionAndRotation(position, rotation);
            block.localScale = Vector3.one * scale;
            foreach (var crack in cracks) Built.Show(crack, phase >= CalvingGlacier.Crack && phase < CalvingGlacier.Splash);

            var (front, strength) = glacier.Wave(seconds);
            Built.Show(wave, strength > 0f);
            if (strength > 0f)
            {
                wave.SetPositionAndRotation(SimSpace.ToWorld(at.Centre + at.Left * front), Facing(-into)); // its face toward where it's going
                wave.localScale = new Vector3(CalvingGlacier.Along, strength, 0.8f);
            }

            var splashing = phase >= CalvingGlacier.Splash && phase < CalvingGlacier.Splash + 0.08f;
            Built.Show(plume, splashing);
            if (splashing) plume.localScale = new Vector3(6f, 4f, 6f) * (0.5f + (phase - CalvingGlacier.Splash) / 0.08f);
        }
    }

    /// <summary>A miner's shack on the bank; a red crate of dynamite sails out, drifts with its fuse fizzing, and goes up in a fireball.</summary>
    sealed class DynamiteFloatView : ObstacleView
    {
        readonly DynamiteFloat dynamite;
        readonly Transform crate;
        readonly Transform fuse;
        readonly Transform spark;
        readonly Transform blast;
        readonly Transform ring;

        public DynamiteFloatView(DynamiteFloat dynamite, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Dynamite float"), palette)
        {
            this.dynamite = dynamite;
            var at = dynamite.At;
            var side = dynamite.Spot.side;
            Built.Footing(Root, dynamite.Shack, at.Tangent, 4f, 4.4f, palette);
            var shack = Shapes3D.Group(Root, "Shack");
            shack.SetPositionAndRotation(SimSpace.ToWorld(dynamite.Shack, 0.3f), Facing(at.Tangent));
            Part(PrimitiveType.Cube, shack, new Vector3(0f, 1.3f, 0f), new Vector3(3f, 2.6f, 3.4f), Tint(palette.animal, new Color(0.64f, 0.5f, 0.36f)), name: "Walls");
            Part(PrimitiveType.Cube, shack, new Vector3(0f, 2.75f, 0f), new Vector3(3.4f, 0.2f, 3.9f), Tint(palette.Metal, new Color(0.5f, 0.45f, 0.4f)), Quaternion.Euler(0f, 0f, -side * 10f), "Roof");
            Part(PrimitiveType.Cube, shack, new Vector3(-side * 1.52f, 1.1f, 0f), new Vector3(0.05f, 1.9f, 1.1f), Tint(palette.animal, new Color(0.15f, 0.1f, 0.08f)), name: "Door");
            var post = SimSpace.ToWorld(dynamite.Shack + at.Tangent * 2.4f - side * at.Left * 1.2f);
            Part(PrimitiveType.Cylinder, Root, post + Vector3.up * 0.8f, new Vector3(0.12f, 0.8f, 0.12f), palette.wood, name: "Sign post");
            Part(PrimitiveType.Cube, Root, post + Vector3.up * 1.7f, new Vector3(1.4f, 0.7f, 0.08f), Tint(palette.sign, new Color(1f, 0.82f, 0.1f)), Facing(-side * at.Left), "Danger sign");

            var red = Tint(palette.animal, new Color(0.8f, 0.16f, 0.1f));
            var stick = Tint(palette.sign, new Color(0.85f, 0.12f, 0.1f));
            crate = Shapes3D.Group(Root, "Crate");
            Part(PrimitiveType.Cube, crate, Vector3.zero, new Vector3(1.1f, 0.7f, 1.1f), red, name: "Box");
            Part(PrimitiveType.Cube, crate, Vector3.zero, new Vector3(1.12f, 0.18f, 1.12f), Tint(palette.eyeWhite, new Color(0.95f, 0.95f, 0.9f)), name: "Band");
            for (var i = 0; i < 4; i++)
                Part(PrimitiveType.Cylinder, crate, new Vector3((i % 2 - 0.5f) * 0.35f, 0.55f, (i / 2 - 0.5f) * 0.35f), new Vector3(0.17f, 0.22f, 0.17f), stick, name: "Stick");
            fuse = Part(PrimitiveType.Cylinder, crate, new Vector3(0f, 0.95f, 0f), new Vector3(0.05f, 0.2f, 0.05f), Tint(palette.Metal, new Color(0.1f, 0.1f, 0.1f)), name: "Fuse").transform;
            spark = Part(PrimitiveType.Sphere, crate, new Vector3(0f, 1.15f, 0f), Vector3.one * 0.25f, palette.Spray(new Color(1f, 0.85f, 0.3f, 0.95f), 0.1f, 0f, 3f, 0.2f), name: "Spark").transform;
            crate.localScale = Vector3.one * 1.35f;
            blast = Part(PrimitiveType.Sphere, Root, Vector3.zero, Vector3.one, palette.Spray(new Color(1f, 0.58f, 0.15f, 0.88f), 0.5f, 0.5f, 2.5f), name: "Fireball").transform;
            ring = Shapes3D.MeshObject("Shock", Shapes3D.Disc(1f, 32), Root, Vector3.zero, palette.foam).transform;
        }

        public override void Pose(double seconds)
        {
            var t = (float)seconds;
            var (position, height, burnt, boom) = dynamite.Crate(seconds);
            Built.Show(crate, boom <= 0f);
            if (boom <= 0f)
            {
                crate.SetPositionAndRotation(SimSpace.ToWorld(position, height + 0.15f + 0.05f * Mathf.Sin(t * 2.6f)),
                    Facing(dynamite.At.Tangent) * Quaternion.Euler(4f * Mathf.Sin(t * 1.9f), t * 25f, 3f * Mathf.Sin(t * 2.3f)));
                fuse.localScale = new Vector3(0.05f, 0.2f * (1f - burnt) + 0.02f, 0.05f);
                fuse.localPosition = new Vector3(0f, 0.75f + 0.2f * (1f - burnt), 0f);
                spark.localPosition = new Vector3(0f, 0.78f + 0.4f * (1f - burnt), 0f);
                var rate = burnt < 0.7f ? 6f : 6f + 40f * (burnt - 0.7f);
                spark.localScale = Vector3.one * (Mathf.Repeat(t * rate, 1f) < 0.6f ? 0.28f + 0.1f * Mathf.Sin(t * 50f) : 0.1f);
            }

            var exploding = boom > 0f && boom < 0.35f;
            Built.Show(blast, exploding);
            Built.Show(ring, exploding);
            if (!exploding) return;
            var u = boom / 0.35f;
            blast.position = SimSpace.ToWorld(position, 0.6f + 1.5f * u);
            blast.localScale = Vector3.one * (DynamiteFloat.Blast * 2f * (0.35f + 0.65f * Mathf.Sqrt(u)));
            ring.position = SimSpace.ToWorld(position, 0.05f);
            ring.localScale = Vector3.one * (DynamiteFloat.Blast * (0.5f + 0.7f * u));
        }
    }

    /// <summary>Three lily pads and two fat green frogs that crouch, spring across the river and land with a splash.</summary>
    sealed class LeapingFrogsView : ObstacleView
    {
        readonly LeapingFrogs frogs;
        readonly Transform[] bodies = new Transform[LeapingFrogs.Frogs];
        readonly Transform[][] legs = new Transform[LeapingFrogs.Frogs][];
        readonly Transform[] splashes = new Transform[LeapingFrogs.Frogs];

        public LeapingFrogsView(LeapingFrogs frogs, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Leaping frogs"), palette)
        {
            this.frogs = frogs;
            var pad = Tint(palette.animal, new Color(0.26f, 0.52f, 0.2f));
            var vein = Tint(palette.animal, new Color(0.36f, 0.62f, 0.28f));
            var petal = Tint(palette.animal, new Color(0.98f, 0.6f, 0.75f));
            for (var i = 0; i < 3; i++)
            {
                var at = SimSpace.ToWorld(frogs.Pad(i), 0.04f);
                Part(PrimitiveType.Cylinder, Root, at, new Vector3(LeapingFrogs.PadRadius * 2f, 0.03f, LeapingFrogs.PadRadius * 2f), pad, name: "Lily pad");
                Part(PrimitiveType.Cube, Root, at + Vector3.up * 0.03f, new Vector3(0.08f, 0.01f, LeapingFrogs.PadRadius * 1.8f), vein, Quaternion.Euler(0f, i * 50f, 0f), "Vein");
                if (i != 1) Part(PrimitiveType.Sphere, Root, at + new Vector3(0.6f, 0.15f, 0.5f), new Vector3(0.45f, 0.3f, 0.45f), petal, name: "Flower");
            }

            var skin = Tint(palette.animal, new Color(0.32f, 0.62f, 0.2f));
            var belly = Tint(palette.animal, new Color(0.85f, 0.85f, 0.5f));
            var spots = Tint(palette.animal, new Color(0.2f, 0.4f, 0.14f));
            for (var f = 0; f < bodies.Length; f++)
            {
                var frog = bodies[f] = Shapes3D.Group(Root, "Frog");
                Part(PrimitiveType.Sphere, frog, new Vector3(0f, 0.55f, 0f), new Vector3(1.4f, 0.95f, 1.6f), skin, name: "Body");
                Part(PrimitiveType.Sphere, frog, new Vector3(0f, 0.42f, 0.2f), new Vector3(1.2f, 0.7f, 1.3f), belly, name: "Belly");
                Part(PrimitiveType.Sphere, frog, new Vector3(0.3f, 0.95f, -0.3f), new Vector3(0.35f, 0.15f, 0.35f), spots, name: "Spot");
                Part(PrimitiveType.Sphere, frog, new Vector3(-0.35f, 0.9f, 0.05f), new Vector3(0.28f, 0.12f, 0.28f), spots, name: "Spot");
                foreach (var x in new[] { -1f, 1f })
                {
                    Part(PrimitiveType.Sphere, frog, new Vector3(x * 0.38f, 1.08f, 0.5f), Vector3.one * 0.48f, skin, name: "Eye bump");
                    Part(PrimitiveType.Sphere, frog, new Vector3(x * 0.38f, 1.14f, 0.62f), Vector3.one * 0.36f, palette.eyeWhite, name: "Eye");
                    Part(PrimitiveType.Sphere, frog, new Vector3(x * 0.38f, 1.16f, 0.76f), Vector3.one * 0.18f, palette.eyeDark, name: "Pupil");
                }

                Part(PrimitiveType.Cube, frog, new Vector3(0f, 0.62f, 0.8f), new Vector3(0.8f, 0.04f, 0.04f), palette.eyeDark, name: "Mouth");
                legs[f] = new Transform[2];
                for (var l = 0; l < 2; l++)
                {
                    var x = l == 0 ? -1f : 1f;
                    var leg = legs[f][l] = Shapes3D.Group(frog, "Leg", new Vector3(x * 0.6f, 0.35f, -0.45f));
                    Part(PrimitiveType.Capsule, leg, new Vector3(0f, 0f, -0.35f), new Vector3(0.38f, 0.45f, 0.38f), skin, Quaternion.Euler(90f, 0f, 0f), "Thigh");
                    Part(PrimitiveType.Cube, leg, new Vector3(0f, -0.25f, -0.75f), new Vector3(0.45f, 0.06f, 0.5f), skin, name: "Foot");
                    Part(PrimitiveType.Capsule, frog, new Vector3(x * 0.45f, 0.2f, 0.55f), new Vector3(0.18f, 0.25f, 0.18f), skin, name: "Arm");
                }

                splashes[f] = Part(PrimitiveType.Sphere, Root, Vector3.zero, Vector3.one, palette.Spray(new Color(1f, 1f, 1f, 0.75f), 0.5f, 0f, 1.5f), name: "Splash").transform;
            }
        }

        public override void Pose(double seconds)
        {
            var t = (float)seconds;
            var phase = frogs.HopPhase(seconds);
            var round = Mathf.FloorToInt((float)((seconds + frogs.Spot.offset) / frogs.Spot.period));
            for (var f = 0; f < bodies.Length; f++)
            {
                var (position, height, leap) = frogs.Frog(f, seconds);
                var next = frogs.Pad(LeapingFrogs.PadOf(f, round + 1));
                var toward = next - position;
                var heading = toward.sqrMagnitude > 0.04f ? toward.normalized : frogs.At.Tangent;
                var crouch = phase < LeapingFrogs.Leap ? Mathf.Clamp01((phase - (LeapingFrogs.Leap - 0.15f)) / 0.15f) : 0f;
                var airborne = leap > 0f;
                bodies[f].SetPositionAndRotation(SimSpace.ToWorld(position, height - 0.3f + (airborne ? 0f : 0.03f * Mathf.Sin(t * 3f + f))),
                    Facing(heading) * Quaternion.Euler(airborne ? Mathf.Lerp(-25f, 25f, leap) : 0f, 0f, 0f));
                bodies[f].localScale = new Vector3(1f + 0.1f * crouch, 1f - 0.25f * crouch + (airborne ? 0.05f : 0f), airborne ? 1.15f : 1f);
                var kick = airborne ? Mathf.Lerp(-60f, -10f, leap) : 25f * crouch;
                foreach (var leg in legs[f]) leg.localRotation = Quaternion.Euler(kick, 0f, 0f);

                var landed = phase < 0.1f;
                Built.Show(splashes[f], landed);
                if (!landed) continue;
                splashes[f].position = SimSpace.ToWorld(frogs.Pad(LeapingFrogs.PadOf(f, round)), 0.3f);
                splashes[f].localScale = new Vector3(LeapingFrogs.Splash * 2f, 1.4f, LeapingFrogs.Splash * 2f) * (0.4f + phase / 0.1f * 0.6f);
            }
        }
    }

    /// <summary>A huge caged fan on the bank, blades spinning up as its windsock lifts, then streaks of wind racing across the water.</summary>
    sealed class GiantFanView : ObstacleView
    {
        readonly GiantFan fan;
        readonly Transform blades;
        readonly Transform sock;
        readonly Quaternion limp;
        readonly Quaternion blowing;
        readonly Transform[] streaks = new Transform[10];
        float angle;

        public GiantFanView(GiantFan fan, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Giant fan"), palette)
        {
            this.fan = fan;
            var at = fan.At;
            var into = -fan.Spot.side * at.Left;
            var frame = Tint(palette.Metal, new Color(0.3f, 0.45f, 0.55f));
            var dark = Tint(palette.Metal, new Color(0.14f, 0.15f, 0.16f));
            var blade = Tint(palette.Metal, new Color(0.85f, 0.86f, 0.88f));
            Built.Footing(Root, fan.Fan, at.Tangent, 4.6f, 7.4f, palette);
            var body = Shapes3D.Group(Root, "Fan");
            body.SetPositionAndRotation(SimSpace.ToWorld(fan.Fan, 3.6f), Facing(into));
            Shapes3D.MeshObject("Cage", Shapes3D.Torus(3f, 0.18f, 48, 8), body, Vector3.zero, frame).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (var i = 0; i < 4; i++)
                Part(PrimitiveType.Cube, body, new Vector3(0f, 0f, 0.5f), new Vector3(6f, 0.06f, 0.06f), frame, Quaternion.Euler(0f, 0f, i * 45f), "Grille");
            Part(PrimitiveType.Cylinder, body, new Vector3(0f, 0f, -1.1f), new Vector3(1.8f, 0.9f, 1.8f), dark, Quaternion.Euler(90f, 0f, 0f), "Motor");
            foreach (var x in new[] { -1f, 1f })
                Part(PrimitiveType.Cube, body, new Vector3(x * 1.6f, -2.5f, -0.6f), new Vector3(0.35f, 3.4f, 0.35f), frame, Quaternion.Euler(0f, 0f, x * 12f), "Leg");
            blades = Shapes3D.Group(body, "Blades", new Vector3(0f, 0f, 0.15f));
            Part(PrimitiveType.Sphere, blades, Vector3.zero, Vector3.one * 0.8f, dark, name: "Hub");
            for (var i = 0; i < 4; i++)
            {
                var arm = Shapes3D.Group(blades, "Blade");
                arm.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
                Part(PrimitiveType.Cube, arm, new Vector3(0f, 1.45f, 0f), new Vector3(0.95f, 2.5f, 0.1f), blade, Quaternion.Euler(0f, 22f, 0f), "Blade");
            }

            var pole = SimSpace.ToWorld(fan.Fan + at.Tangent * 4.2f);
            Part(PrimitiveType.Cylinder, Root, pole + Vector3.up * 2.6f, new Vector3(0.12f, 2.6f, 0.12f), palette.Metal, name: "Pole");
            sock = Shapes3D.Group(Root, "Windsock");
            sock.position = pole + Vector3.up * 5.1f;
            var orange = Tint(palette.sign, new Color(1f, 0.42f, 0.1f));
            var white = Tint(palette.sign, new Color(0.96f, 0.95f, 0.92f));
            for (var i = 0; i < 4; i++)
                Part(PrimitiveType.Cylinder, sock, new Vector3(0f, 0.3f + i * 0.45f, 0f), new Vector3(0.62f - i * 0.1f, 0.23f, 0.62f - i * 0.1f), i % 2 == 0 ? orange : white, name: "Sock");
            limp = Quaternion.FromToRotation(Vector3.up, new Vector3(0f, -1f, 0f) + SimSpace.Direction(into) * 0.15f);
            blowing = Quaternion.FromToRotation(Vector3.up, SimSpace.Direction(into));

            for (var i = 0; i < streaks.Length; i++)
                streaks[i] = Part(PrimitiveType.Cube, Root, Vector3.zero, new Vector3(0.08f, 0.03f, 2.6f), palette.Spray(new Color(1f, 1f, 1f, 0.55f), 0.3f), name: "Gust").transform;
        }

        public override void Pose(double seconds)
        {
            var t = (float)seconds;
            var blow = fan.Blowing(seconds);
            var lift = fan.Sock(seconds);
            angle += Time.deltaTime * (90f + 1500f * blow + 500f * lift);
            blades.localRotation = Quaternion.Euler(0f, 0f, angle);
            sock.rotation = Quaternion.Slerp(limp, blowing, lift) * Quaternion.Euler(6f * lift * Mathf.Sin(t * 13f), 0f, 0f);

            var at = fan.At;
            var into = -fan.Spot.side * at.Left;
            for (var i = 0; i < streaks.Length; i++)
            {
                Built.Show(streaks[i], blow > 0.05f);
                if (blow <= 0.05f) continue;
                var along = ((i * 0.618f) % 1f * 2f - 1f) * (GiantFan.Along - 0.5f);
                var across = Mathf.Lerp(-at.HalfWidth, at.HalfWidth, Mathf.Repeat(t * 1.6f + i * 0.29f, 1f));
                var point = at.Centre + at.Tangent * along + into * across;
                streaks[i].SetPositionAndRotation(SimSpace.ToWorld(point, 0.3f + 0.2f * (i % 3)), Facing(into));
            }
        }
    }
}
