using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RushAhead.EditorTools
{
    /// <summary>
    /// Cuts a dense mesh (a scan, a sculpture) down to about a number of triangles for phones: Unity's own mesh LOD
    /// generator simplifies it in steps, and the first step small enough becomes a mesh of its own, keeping only the
    /// vertices it uses (and their normals, tangents, UVs and colours), with a few LOD levels of its own for distance.
    /// </summary>
    static class MeshCutter
    {
        public static Mesh Cut(Mesh source, int maxTriangles, string name, out int before, out int after)
        {
            var work = Object.Instantiate(source);
            MeshLodUtility.GenerateMeshLods(work, -1);
            int Triangles(int level)
            {
                var total = 0;
                for (var s = 0; s < work.subMeshCount; s++) total += (int)work.GetIndexCount(s, level) / 3;
                return total;
            }

            var lod = 0;
            while (lod + 1 < work.lodCount && Triangles(lod) > maxTriangles) lod++;
            before = Triangles(0);
            after = Triangles(lod);

            var positions = work.vertices;
            var normals = work.normals;
            var tangents = work.tangents;
            var uvs = work.uv;
            var colours = work.colors;
            var remap = new Dictionary<int, int>();
            var outPositions = new List<Vector3>();
            var outNormals = new List<Vector3>();
            var outTangents = new List<Vector4>();
            var outUvs = new List<Vector2>();
            var outColours = new List<Color>();
            var parts = new List<int[]>();
            for (var s = 0; s < work.subMeshCount; s++)
            {
                var indices = work.GetIndices(s, lod, true);
                for (var i = 0; i < indices.Length; i++)
                {
                    var old = indices[i];
                    if (!remap.TryGetValue(old, out var kept))
                    {
                        kept = outPositions.Count;
                        remap[old] = kept;
                        outPositions.Add(positions[old]);
                        if (normals.Length > old) outNormals.Add(normals[old]);
                        if (tangents.Length > old) outTangents.Add(tangents[old]);
                        if (uvs.Length > old) outUvs.Add(uvs[old]);
                        if (colours.Length > old) outColours.Add(colours[old]);
                    }

                    indices[i] = kept;
                }

                parts.Add(indices);
            }

            var mesh = new Mesh { name = name, indexFormat = outPositions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(outPositions);
            if (outNormals.Count == outPositions.Count) mesh.SetNormals(outNormals);
            else mesh.RecalculateNormals();
            if (outUvs.Count == outPositions.Count) mesh.SetUVs(0, outUvs);
            if (outColours.Count == outPositions.Count) mesh.SetColors(outColours);
            if (outTangents.Count == outPositions.Count) mesh.SetTangents(outTangents);
            else if (outUvs.Count == outPositions.Count) mesh.RecalculateTangents();
            mesh.subMeshCount = parts.Count;
            for (var s = 0; s < parts.Count; s++) mesh.SetIndices(parts[s], MeshTopology.Triangles, s);
            mesh.RecalculateBounds();
            MeshLodUtility.GenerateMeshLods(mesh, 3);
            Object.DestroyImmediate(work);
            return mesh;
        }
    }
}
