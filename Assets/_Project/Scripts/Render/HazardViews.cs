using System;
using System.Collections.Generic;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// How the moving obstacles look: paddle wheels on posts, crocodiles that open their jaws as they lunge, rope bridges
    /// with a log swinging beneath, and geysers that bubble and then spout. Posed every frame from the race clock (the
    /// host's tick, or a joined player's playback tick), exactly where the host's physics has them.
    /// </summary>
    public sealed class HazardViews : MonoBehaviour
    {
        const float BridgeClearance = 0.45f; // the log's lowest point above the water

        Hazards hazards;
        River river;
        ArtPalette palette;
        readonly List<Transform> wheels = new();
        readonly List<(Transform body, Transform jaw)> crocs = new();
        readonly List<(Transform log, Transform[] ropes, Vector3 pivot, float rope)> logs = new();
        readonly List<(Transform bubbles, Transform spout, Renderer ring)> geysers = new();

        /// <summary>The race clock, in seconds.</summary>
        public Func<double> Clock;

        public static HazardViews Build(Course course, ArtPalette palette, Transform parent)
        {
            var view = new GameObject("Obstacles").AddComponent<HazardViews>();
            view.transform.SetParent(parent, false);
            view.hazards = course.Hazards;
            view.river = course.River;
            view.palette = palette;
            var def = course.Def;
            for (var i = 0; i < def.wheels.Length; i++) view.BuildWheel(def.wheels[i]);
            for (var i = 0; i < def.crocodiles.Length; i++) view.BuildCroc();
            for (var i = 0; i < def.swingers.Length; i++) view.BuildSwinger(i);
            for (var i = 0; i < def.geysers.Length; i++) view.BuildGeyser(def.geysers[i]);
            view.Pose(0);
            return view;
        }

        void BuildWheel(Content.WheelSpot spot)
        {
            var wood = palette.Tinted(palette.wood, new Color(0.6f, 0.4f, 0.22f));
            var root = Shapes3D.Group(transform, "Paddle wheel", SimSpace.ToWorld(spot.position));
            Shapes3D.Primitive(PrimitiveType.Cylinder, root, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), palette.Tinted(palette.rock, new Color(0.45f, 0.42f, 0.4f)), name: "Post");
            Shapes3D.MeshObject("Foam", Shapes3D.Disc(spot.radius + 0.4f), root, new Vector3(0f, 0.02f, 0f), palette.foam);
            var spin = Shapes3D.Group(root, "Arms");
            var arms = Mathf.Max(2, spot.arms);
            for (var arm = 0; arm < arms; arm++)
            {
                var angle = Quaternion.Euler(0f, -arm * 360f / arms, 0f);
                Shapes3D.Primitive(PrimitiveType.Cube, spin, angle * new Vector3(spot.radius * 0.5f, 0.35f, 0f), new Vector3(spot.radius, 0.22f, 0.22f), wood, angle, "Arm");
                Shapes3D.Primitive(PrimitiveType.Cube, spin, angle * new Vector3(spot.radius - 0.3f, 0.35f, 0f), new Vector3(0.6f, 0.7f, Hazards.ArmWidth + 0.5f), wood, angle, "Paddle");
            }

            wheels.Add(spin);
        }

        void BuildCroc()
        {
            var skin = palette.Tinted(palette.animal, new Color(0.3f, 0.5f, 0.22f));
            var belly = palette.Tinted(palette.animal, new Color(0.75f, 0.8f, 0.5f));
            var root = Shapes3D.Group(transform, "Crocodile");
            var length = Hazards.CrocLength;
            Shapes3D.Primitive(PrimitiveType.Capsule, root, new Vector3(-0.1f, 0.12f, 0f), new Vector3(0.75f, length * 0.32f, 0.6f), skin, Quaternion.Euler(0f, 0f, 90f), "Body");
            Shapes3D.Primitive(PrimitiveType.Cube, root, new Vector3(-length * 0.5f, 0.1f, 0f), new Vector3(0.9f, 0.18f, 0.28f), skin, name: "Tail");
            for (var ridge = 0; ridge < 5; ridge++)
                Shapes3D.Primitive(PrimitiveType.Cube, root, new Vector3(-0.8f + ridge * 0.35f, 0.42f, 0f), new Vector3(0.14f, 0.12f, 0.14f), skin, Quaternion.Euler(45f, 0f, 0f), "Ridge");
            Shapes3D.Primitive(PrimitiveType.Cube, root, new Vector3(length * 0.42f, 0.08f, 0f), new Vector3(0.75f, 0.14f, 0.42f), belly, name: "Lower jaw");
            var jaw = Shapes3D.Group(root, "Upper jaw", new Vector3(length * 0.2f, 0.2f, 0f));
            Shapes3D.Primitive(PrimitiveType.Cube, jaw, new Vector3(0.45f, 0f, 0f), new Vector3(0.85f, 0.16f, 0.44f), skin, name: "Snout");
            foreach (var side in new[] { -1f, 1f })
            {
                Shapes3D.Primitive(PrimitiveType.Sphere, root, new Vector3(length * 0.14f, 0.42f, 0.16f * side), Vector3.one * 0.16f, palette.eyeWhite, name: "Eye");
                Shapes3D.Primitive(PrimitiveType.Sphere, root, new Vector3(length * 0.17f, 0.44f, 0.16f * side), Vector3.one * 0.08f, palette.eyeDark, name: "Pupil");
            }

            Shapes3D.MeshObject("Foam", Shapes3D.Disc(1.2f), root, new Vector3(0f, 0.02f, 0f), palette.foam);
            crocs.Add((root, jaw));
        }

        void BuildSwinger(int i)
        {
            var def = hazards.Def.swingers[i];
            var at = river.At(def.distance);
            var amplitude = Mathf.Max(0.5f, at.HalfWidth - 0.6f);
            var rope = amplitude / Mathf.Sin(55f * Mathf.Deg2Rad); // swings out to 55 degrees
            var pivotHeight = rope + BridgeClearance;
            var wood = palette.Tinted(palette.wood, new Color(0.55f, 0.38f, 0.22f));
            var yaw = Quaternion.LookRotation(SimSpace.Direction(at.Tangent)); // local x runs across the river

            // The bridge: a post on each side and a plank deck over the river.
            var bridge = Shapes3D.Group(transform, "Bridge");
            foreach (var side in new[] { -1f, 1f })
                Shapes3D.Primitive(PrimitiveType.Cylinder, bridge, SimSpace.ToWorld(at.Centre + at.Left * side * (at.HalfWidth + 0.8f), pivotHeight * 0.5f),
                    new Vector3(0.45f, pivotHeight * 0.5f + 0.2f, 0.45f), wood, name: "Post");
            Shapes3D.Primitive(PrimitiveType.Cube, bridge, SimSpace.ToWorld(at.Centre, pivotHeight + 0.15f), new Vector3(at.HalfWidth * 2f + 2.4f, 0.22f, 1.6f), wood, yaw, "Deck");

            var log = Shapes3D.Group(transform, "Swinging log");
            var bark = palette.Tinted(palette.wood, new Color(0.42f, 0.28f, 0.17f));
            Shapes3D.Primitive(PrimitiveType.Cylinder, log, Vector3.zero, new Vector3(0.6f, def.logLength * 0.5f, 0.6f), bark, Quaternion.Euler(90f, 0f, 0f), "Log");
            var ropes = new Transform[2];
            for (var r = 0; r < 2; r++)
                ropes[r] = Shapes3D.Primitive(PrimitiveType.Cylinder, transform, Vector3.zero, new Vector3(0.06f, 1f, 0.06f), palette.Tinted(palette.wood, new Color(0.8f, 0.7f, 0.5f)), name: "Rope").transform;
            logs.Add((log, ropes, SimSpace.ToWorld(at.Centre, pivotHeight), rope));
        }

        void BuildGeyser(Content.GeyserSpot spot)
        {
            var root = Shapes3D.Group(transform, "Geyser", SimSpace.ToWorld(spot.position));
            var ring = Shapes3D.MeshObject("Warning", Shapes3D.Disc(spot.radius, 28), root, new Vector3(0f, 0.025f, 0f), palette.foam);
            var bubbles = Shapes3D.Group(root, "Bubbles");
            var random = new System.Random(spot.position.GetHashCode());
            for (var b = 0; b < 9; b++)
            {
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var distance = (float)random.NextDouble() * spot.radius * 0.8f;
                Shapes3D.Primitive(PrimitiveType.Sphere, bubbles, new Vector3(Mathf.Cos(angle) * distance, 0.05f, Mathf.Sin(angle) * distance), Vector3.one * 0.3f, palette.foam, name: "Bubble");
            }

            var spout = Shapes3D.Primitive(PrimitiveType.Cylinder, root, new Vector3(0f, 2f, 0f), new Vector3(spot.radius * 1.2f, 2f, spot.radius * 1.2f), palette.air, name: "Spout").transform;
            geysers.Add((bubbles, spout, ring.GetComponent<Renderer>()));
        }

        void LateUpdate()
        {
            if (Clock != null) Pose(Clock());
        }

        void Pose(double seconds)
        {
            var def = hazards.Def;
            for (var i = 0; i < wheels.Count; i++)
                wheels[i].localRotation = Quaternion.Euler(0f, -hazards.WheelAngleDeg(i, seconds), 0f); // anticlockwise in the sim is a negative turn about y

            for (var i = 0; i < crocs.Count; i++)
            {
                var (position, heading, lunging) = hazards.Croc(i, seconds);
                crocs[i].body.SetPositionAndRotation(SimSpace.ToWorld(position), Quaternion.Euler(0f, -heading * Mathf.Rad2Deg, 0f));
                var open = lunging ? 0.5f + 0.5f * Mathf.Sin((float)seconds * 14f) : 0f;
                crocs[i].jaw.localRotation = Quaternion.Euler(0f, 0f, 28f * open);
            }

            for (var i = 0; i < logs.Count; i++)
            {
                var (centre, along, across, _, _) = hazards.Swing(i, seconds);
                var (log, ropes, pivot, rope) = logs[i];
                var drop = Mathf.Sqrt(Mathf.Max(0f, rope * rope - across * across));
                var position = SimSpace.ToWorld(centre, pivot.y - drop);
                log.SetPositionAndRotation(position, Quaternion.LookRotation(SimSpace.Direction(along)));
                for (var r = 0; r < 2; r++)
                {
                    var end = position + SimSpace.Direction(along) * ((r == 0 ? -0.35f : 0.35f) * def.swingers[i].logLength);
                    var top = pivot + SimSpace.Direction(along) * ((r == 0 ? -0.35f : 0.35f) * def.swingers[i].logLength);
                    ropes[r].position = (end + top) * 0.5f;
                    ropes[r].up = (top - end).normalized;
                    ropes[r].localScale = new Vector3(0.06f, Vector3.Distance(top, end) * 0.5f, 0.06f);
                }
            }

            for (var i = 0; i < geysers.Count; i++)
            {
                var warning = hazards.GeyserWarning(i, seconds, out var spouting);
                var (bubbles, spout, ring) = geysers[i];
                var bob = 0.05f + 0.12f * warning * Mathf.Abs(Mathf.Sin((float)seconds * 18f));
                bubbles.localPosition = new Vector3(0f, bob, 0f);
                bubbles.gameObject.SetActive(warning > 0f);
                ring.enabled = warning > 0f;
                var height = spouting ? 4.5f : 0f;
                var scale = spout.localScale;
                spout.localScale = new Vector3(scale.x, Mathf.Lerp(scale.y, height * 0.5f, 0.35f), scale.z);
                spout.localPosition = new Vector3(0f, spout.localScale.y, 0f);
                spout.gameObject.SetActive(spout.localScale.y > 0.05f);
            }
        }
    }
}
