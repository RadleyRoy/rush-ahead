using System;
using RushAhead.Content;
using RushAhead.Render;
using UnityEditor;
using UnityEngine;

namespace RushAhead.EditorTools
{
    /// <summary>
    /// Bakes the sculpted animals (see <see cref="AnimalSculptures"/>) into mesh assets with a few LOD levels, and makes
    /// the fur material they wear, so phones load finished meshes rather than sculpting them.
    /// </summary>
    static class SculptBootstrap
    {
        const string Out = "Assets/_Project/Art/Sculpted";
        const float Cell = 0.016f; // metres: fine enough for a nose, coarse enough for a phone

        public static void Apply(ArtPalette palette)
        {
            ContentBootstrap.EnsureFolder(Out);
            var shapes = (AnimalShape[])Enum.GetValues(typeof(AnimalShape));
            palette.animalMeshes = new Mesh[shapes.Length];
            foreach (var shape in shapes) palette.animalMeshes[(int)shape] = Bake(shape.ToString(), AnimalSculptures.For(shape), Cell, 6000);
            var creatures = (Creature[])Enum.GetValues(typeof(Creature));
            palette.creatureMeshes = new Mesh[creatures.Length];
            foreach (var creature in creatures)
                palette.creatureMeshes[(int)creature] = Bake(creature.ToString(), CreatureSculptures.For(creature), CreatureSculptures.Cell(creature), Budget(creature));
            palette.fur = PolyHavenArt.LoadOrCreate($"{Out}/Fur.mat", "RushAhead/Fur");
            palette.fur.enableInstancing = true;
            EditorUtility.SetDirty(palette.fur);
            EditorUtility.SetDirty(palette);
        }

        /// <summary>Triangles each creature may have on a phone: the big ones are seen big.</summary>
        static int Budget(Creature creature) => creature switch
        {
            Creature.Hippo or Creature.BearBody or Creature.TurtleShell or Creature.CrocBody => 6000,
            Creature.TurtleFlipper => 800,
            _ => 2500,
        };

        /// <summary>Meshes the sculpture finely, then cuts it to <paramref name="budget"/> triangles (see <see cref="MeshCutter"/>).</summary>
        static Mesh Bake(string name, Sculpture sculpture, float cell, int budget)
        {
            var path = $"{Out}/{name}.asset";
            var fine = sculpture.Mesh(name, cell);
            var mesh = MeshCutter.Cut(fine, budget, name, out _, out _);
            UnityEngine.Object.DestroyImmediate(fine);
            mesh.UploadMeshData(true);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                mesh = existing;
            }
            else AssetDatabase.CreateAsset(mesh, path);

            Debug.Log($"[RushAhead] Sculpted {name}: {mesh.vertexCount} vertices, {mesh.GetIndexCount(0, 0) / 3} triangles");
            return mesh;
        }
    }
}
