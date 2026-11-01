using System;
using System.Collections.Generic;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// The boost lanes: rows of glowing chevrons on the water pointing downstream, rippling forward. Says when the raft
    /// being watched rides onto one (<see cref="Entered"/>), for the whoosh and the camera's kick.
    /// </summary>
    public sealed class BoostViews : MonoBehaviour
    {
        const int Chevrons = 5;

        readonly List<(BoostLane lane, Transform[] chevrons)> lanes = new();
        Material glow;
        bool riding;

        /// <summary>Where the raft being watched is.</summary>
        public Func<Vector2> Watched;

        /// <summary>The watched raft just rode onto a lane.</summary>
        public event Action Entered;

        public static BoostViews Build(IReadOnlyList<BoostLane> boosts, ArtPalette palette, Transform parent)
        {
            var view = new GameObject("Boost lanes").AddComponent<BoostViews>();
            view.transform.SetParent(parent, false);
            view.glow = new Material(palette.foam) { name = "Boost glow", color = new Color(0.35f, 0.95f, 1f, 0.85f) };
            var chevron = ChevronMesh();
            foreach (var lane in boosts)
            {
                var row = new Transform[Chevrons];
                var facing = Quaternion.LookRotation(SimSpace.Direction(lane.At.Tangent));
                for (var i = 0; i < Chevrons; i++)
                {
                    var go = Shapes3D.MeshObject("Chevron", chevron, view.transform, Vector3.zero, view.glow);
                    go.transform.rotation = facing;
                    go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    row[i] = go.transform;
                }

                view.lanes.Add((lane, row));
            }

            return view;
        }

        /// <summary>A flat chevron pointing along +z, as wide as a lane.</summary>
        static Mesh ChevronMesh()
        {
            var w = BoostLane.HalfWidth;
            const float depth = 0.55f, thick = 0.45f;
            var vertices = new[]
            {
                new Vector3(-w, 0f, -depth), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, thick), new Vector3(-w, 0f, -depth + thick),
                new Vector3(w, 0f, -depth), new Vector3(w, 0f, -depth + thick),
            };
            var mesh = new Mesh { name = "Chevron", vertices = vertices, triangles = new[] { 0, 3, 2, 0, 2, 1, 1, 2, 5, 1, 5, 4 } };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[] { new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f) };
            mesh.RecalculateBounds();
            return mesh;
        }

        void Update()
        {
            var t = Time.time;
            foreach (var (lane, row) in lanes)
                for (var i = 0; i < row.Length; i++)
                {
                    // Chevrons sweep forward along the lane, again and again.
                    var along = Mathf.Repeat(i / (float)row.Length + t * 0.9f, 1f);
                    var offset = (along - 0.5f) * BoostLane.Length;
                    row[i].position = SimSpace.ToWorld(lane.Middle + lane.At.Tangent * offset, 0.06f);
                    row[i].localScale = Vector3.one * (0.75f + 0.35f * Mathf.Sin(along * Mathf.PI));
                }

            glow.color = new Color(0.35f, 0.95f, 1f, 0.6f + 0.3f * Mathf.Sin(t * 6f));
            if (Watched == null) return;
            var position = Watched();
            var now = false;
            foreach (var (lane, _) in lanes) now |= lane.Riding(position);
            if (now && !riding) Entered?.Invoke();
            riding = now;
        }
    }
}
