using RushAhead.Content;
using RushAhead.Sim;
using UnityEditor;
using UnityEngine;

namespace RushAhead.EditorTools
{
    /// <summary>
    /// Course authoring in the Scene view: select a course asset and its river is drawn on the water plane (centreline,
    /// banks, racing line, gates, rocks, barrels and currents). Drag a control point to move it, shift-drag to change
    /// the river's width there. Everything else is laid out by distance down the river, so it follows the edits.
    /// </summary>
    [CustomEditor(typeof(CourseDef))]
    public sealed class CourseDefEditor : Editor
    {
        static readonly Color Bank = new(0.55f, 0.35f, 0.2f);
        static readonly Color Line = new(1f, 0.85f, 0.2f);
        static readonly Color GateColor = new(0.3f, 1f, 0.4f);
        static readonly Color Current = new(0.4f, 0.8f, 1f);

        void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;
        void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Select this asset and look at the Scene view from above: drag the white points to bend the river; " +
                                    "hold Shift and drag to change its width. Gates, currents and the start are measured in metres down the river.",
                MessageType.Info);
            var def = (CourseDef)target;
            if (def.river.Length >= 2)
            {
                var river = new River(def.river);
                EditorGUILayout.LabelField("River length", $"{river.Length:0} m");
                var bend = river.TightestBend;
                if (bend >= 0.9f) EditorGUILayout.HelpBox($"A bend is so tight the inside bank folds over ({bend:0.00} of 1). Widen the bend or narrow the river.", MessageType.Warning);
            }

            DrawDefaultInspector();
        }

        void OnSceneGUI(SceneView view)
        {
            var def = (CourseDef)target;
            if (def == null || def.river == null || def.river.Length < 2) return;

            River river;
            try
            {
                river = new River(def.river);
            }
            catch (System.ArgumentException)
            {
                return;
            }

            DrawRiver(def, river);
            EditPoints(def);
        }

        static void DrawRiver(CourseDef def, River river)
        {
            var samples = river.Samples;
            var left = new Vector3[samples.Count];
            var right = new Vector3[samples.Count];
            var centre = new Vector3[samples.Count];
            var line = new Vector3[samples.Count];
            for (var i = 0; i < samples.Count; i++)
            {
                left[i] = World(samples[i].LeftBank);
                right[i] = World(samples[i].RightBank);
                centre[i] = World(samples[i].Centre);
                line[i] = World(samples[i].RacingPoint);
            }

            Handles.color = Bank;
            Handles.DrawAAPolyLine(4f, left);
            Handles.DrawAAPolyLine(4f, right);
            Handles.color = new Color(1f, 1f, 1f, 0.3f);
            Handles.DrawDottedLines(Segments(centre), 4f);
            Handles.color = Line;
            Handles.DrawAAPolyLine(2f, line);

            Handles.color = GateColor;
            for (var i = 0; i < def.gates.Length; i++)
            {
                var gate = new Gate(river.At(def.gates[i]));
                Handles.DrawAAPolyLine(5f, World(gate.LeftEnd), World(gate.RightEnd));
                Handles.Label(World(gate.LeftEnd), i == def.gates.Length - 1 ? "Finish" : $"Checkpoint {i + 1}");
            }

            var start = river.At(def.startDistance);
            Handles.color = Color.cyan;
            Handles.DrawWireDisc(World(start.Centre), Vector3.up, 1.6f);
            Handles.Label(World(start.Centre), "Start");

            Handles.color = new Color(0.6f, 0.6f, 0.6f);
            foreach (var rock in def.rocks) Handles.DrawSolidDisc(World(rock.position), Vector3.up, rock.radius);
            Handles.color = new Color(0.9f, 0.2f, 0.2f);
            foreach (var barrel in def.barrels) Handles.DrawWireDisc(World(barrel), Vector3.up, Course.BarrelRadius);

            Handles.color = Current;
            foreach (var zone in def.currents)
                for (var d = zone.from + 2f; d < zone.to; d += 4f)
                {
                    var at = river.At(d);
                    var tip = at.Centre + at.Tangent * Mathf.Sign(zone.speed) * 1.5f;
                    Handles.DrawAAPolyLine(3f, World(at.Centre - at.Tangent * Mathf.Sign(zone.speed) * 1.5f), World(tip));
                }
        }

        void EditPoints(CourseDef def)
        {
            for (var i = 0; i < def.river.Length; i++)
            {
                var point = def.river[i];
                var at = World(point.position);
                var size = HandleUtility.GetHandleSize(at) * 0.12f;
                Handles.color = Color.white;

                EditorGUI.BeginChangeCheck();
                var moved = Handles.FreeMoveHandle(at, size, Vector3.zero, Handles.SphereHandleCap);
                if (!EditorGUI.EndChangeCheck()) continue;

                Undo.RecordObject(def, "Edit river");
                var delta = new Vector2(moved.x - at.x, moved.z - at.z);
                if (Event.current.shift)
                    point.width = Mathf.Max(4f, point.width + delta.magnitude * Mathf.Sign(Vector2.Dot(delta, Vector2.one)) * 2f);
                else
                    point.position = new Vector2(moved.x, moved.z);
                def.river[i] = point;
                EditorUtility.SetDirty(def);
            }
        }

        static Vector3 World(Vector2 sim) => new(sim.x, 0.05f, sim.y);

        static Vector3[] Segments(Vector3[] polyline)
        {
            var pairs = new Vector3[(polyline.Length - 1) * 2];
            for (var i = 0; i < polyline.Length - 1; i++)
            {
                pairs[2 * i] = polyline[i];
                pairs[2 * i + 1] = polyline[i + 1];
            }

            return pairs;
        }
    }
}
