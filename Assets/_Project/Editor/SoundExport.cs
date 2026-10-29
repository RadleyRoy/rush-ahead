using System.IO;
using RushAhead.Render;
using UnityEditor;
using UnityEngine;

namespace RushAhead.EditorTools
{
    /// <summary>
    /// Writes every synthesized sound, and both tunes, as WAV files in Logs/Sounds, to listen to outside the game (or
    /// to use as the starting point for recorded replacements).
    ///
    /// Batchmode:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod RushAhead.EditorTools.SoundExport.Export
    /// </summary>
    public static class SoundExport
    {
        [MenuItem("Rush Ahead/Export Sounds")]
        public static void Export()
        {
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Sounds"));
            Directory.CreateDirectory(folder);
            foreach (var (name, samples) in SoundBank.Render()) Write(Path.Combine(folder, $"{name}.wav"), samples, Synth.Rate);
            Write(Path.Combine(folder, "Music - menu.wav"), Music.Menu(), Music.Rate);
            Write(Path.Combine(folder, "Music - race.wav"), Music.Race(), Music.Rate);
            Debug.Log($"[RushAhead] Sounds written to {folder}");
        }

        /// <summary>A mono 16-bit PCM WAV.</summary>
        static void Write(string path, float[] samples, int rate)
        {
            using var file = new BinaryWriter(File.Create(path));
            var bytes = samples.Length * 2;
            file.Write("RIFF".ToCharArray());
            file.Write(36 + bytes);
            file.Write("WAVEfmt ".ToCharArray());
            file.Write(16);
            file.Write((short)1); // PCM
            file.Write((short)1); // mono
            file.Write(rate);
            file.Write(rate * 2);
            file.Write((short)2);
            file.Write((short)16);
            file.Write("data".ToCharArray());
            file.Write(bytes);
            foreach (var s in samples) file.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * 32767f));
        }
    }
}
