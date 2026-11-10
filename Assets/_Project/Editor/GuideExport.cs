using System;
using System.IO;
using System.Linq;
using System.Text;
using RushAhead.Content;
using UnityEditor;
using UnityEngine;

namespace RushAhead.EditorTools
{
    /// <summary>
    /// Turns the gallery's photographs into the Field Guide's pictures and writes its web page. Photograph every entry with
    /// a player build (<c>RushAhead.exe -rushahead-gallery &lt;folder&gt;</c>), then
    /// <c>-executeMethod RushAhead.EditorTools.GuideExport.Run -guideShots &lt;folder&gt;</c>: each &lt;Id&gt;.png becomes
    /// <c>Resources/Guide/&lt;Id&gt;.jpg</c> for the game and <c>docs/guide/&lt;Id&gt;.jpg</c> for the page, and
    /// <c>docs/FIELD_GUIDE.md</c> is written from <see cref="ObstacleGuide"/>. Without <c>-guideShots</c> it only
    /// rewrites the page.
    /// </summary>
    public static class GuideExport
    {
        const string GameFolder = "Assets/_Project/Resources/Guide";
        const string DocsFolder = "docs/guide";
        const string Page = "docs/FIELD_GUIDE.md";
        const int Width = 640, Height = 360;

        public static void Run()
        {
            var shots = Arg("-guideShots");
            if (shots != null) ImportShots(shots);
            WritePage();
            AssetDatabase.Refresh();
        }

        static void ImportShots(string folder)
        {
            ContentBootstrap.EnsureFolder(GameFolder);
            Directory.CreateDirectory(DocsFolder);
            var made = 0;
            foreach (var entry in ObstacleGuide.Entries)
            {
                var path = Path.Combine(folder, entry.Id + ".png");
                if (!File.Exists(path)) continue;
                var shot = new Texture2D(2, 2);
                shot.LoadImage(File.ReadAllBytes(path));
                var jpg = Scaled(shot, Width, Height).EncodeToJPG(84);
                File.WriteAllBytes($"{GameFolder}/{entry.Id}.jpg", jpg);
                File.WriteAllBytes($"{DocsFolder}/{entry.Id}.jpg", jpg);
                UnityEngine.Object.DestroyImmediate(shot);
                made++;
            }

            AssetDatabase.Refresh();
            foreach (var entry in ObstacleGuide.Entries)
            {
                // Pictures for the screens: no mipmaps, full size, compressed for whichever phone.
                if (AssetImporter.GetAtPath($"{GameFolder}/{entry.Id}.jpg") is not TextureImporter importer) continue;
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }

            Debug.Log($"[RushAhead] Field Guide pictures: {made} of {ObstacleGuide.Entries.Count} from {folder}");
        }

        static Texture2D Scaled(Texture2D source, int width, int height)
        {
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, target);
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var result = new Texture2D(width, height, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            return result;
        }

        static void WritePage()
        {
            var courses = CourseLibrary.Shipped().Where(c => c != null).ToArray();
            var text = new StringBuilder();
            text.AppendLine("# Rush Ahead: Field Guide");
            text.AppendLine();
            text.AppendLine("Everything you can meet on the river, what it does and how to get past it. The same pages are in the game");
            text.AppendLine("(Field guide on the main menu, the pause menu and \"What hit me?\" on the results), and a card pops up the first");
            text.AppendLine("time you meet each one. Every round deals a fresh layout from the course's list, so you never quite know");
            text.AppendLine("what's round the next bend.");
            text.AppendLine();
            text.AppendLine("*This page is written by `GuideExport` from `Assets/_Project/Scripts/Content/ObstacleGuide.cs`; edit the text there.*");
            text.AppendLine();
            Section(text, "On the river", ObstacleGuide.Entries.Where(e => e.Kind == null), courses);
            Section(text, "Obstacles", ObstacleGuide.Entries.Where(e => e.Kind != null), courses);
            Directory.CreateDirectory(Path.GetDirectoryName(Page));
            File.WriteAllText(Page, text.ToString().Replace("\r\n", "\n"));
            Debug.Log($"[RushAhead] Field Guide page written: {Page}");
        }

        static void Section(StringBuilder text, string heading, System.Collections.Generic.IEnumerable<GuideEntry> entries, CourseDef[] courses)
        {
            text.AppendLine($"## {heading}");
            text.AppendLine();
            foreach (var entry in entries)
            {
                text.AppendLine($"### {entry.Name}");
                text.AppendLine();
                if (File.Exists($"{DocsFolder}/{entry.Id}.jpg")) text.AppendLine($"![{entry.Name}](guide/{entry.Id}.jpg)").AppendLine();
                text.AppendLine($"**{entry.CounterWord}.** {entry.What}");
                text.AppendLine();
                text.AppendLine($"*How to beat it:* {entry.Beat}");
                text.AppendLine();
                var on = courses.Where(c => ObstacleGuide.Appears(entry, c)).Select(c => c.displayName).ToArray();
                text.AppendLine(on.Length == 0 ? "*Where:* not dealt on any course yet."
                    : on.Length == courses.Length ? "*Where:* every course."
                    : $"*Where:* {string.Join(", ", on)}.");
                text.AppendLine();
            }
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
