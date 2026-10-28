using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace RushAhead.EditorTools
{
    /// <summary>
    /// Installs the app icon (drawn by Design/app_icon.py into Assets/_Project/Art/Icon) for every platform: the
    /// default icon, every iOS size (all from the 1024 master, which has no transparency, as Apple requires), and
    /// Android's adaptive icon (slices behind, raft in front), round icon and legacy icon.
    ///
    /// Batchmode:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod RushAhead.EditorTools.AppIcons.Install
    /// </summary>
    public static class AppIcons
    {
        const string Folder = "Assets/_Project/Art/Icon";

        [MenuItem("Rush Ahead/Install App Icon")]
        public static void Install()
        {
            var master = Prepare($"{Folder}/AppIcon-1024.png", transparent: false);
            var round = Prepare($"{Folder}/AppIcon-Round-1024.png", transparent: true);
            var back = Prepare($"{Folder}/AdaptiveBackground-432.png", transparent: false);
            var front = Prepare($"{Folder}/AdaptiveForeground-432.png", transparent: true);
            if (master == null || round == null || back == null || front == null)
            {
                Debug.LogError($"[RushAhead] App icon files are missing from {Folder}. Run Design/app_icon.py first.");
                return;
            }

            // The default icon, which every platform falls back to.
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { master }, IconKind.Any);

            // Every kind each platform asks for. Android's adaptive icon is the one with two layers.
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                foreach (var kind in PlayerSettings.GetSupportedIconKinds(target))
                {
                    var icons = PlayerSettings.GetPlatformIcons(target, kind);
                    foreach (var icon in icons)
                    {
                        if (icon.maxLayerCount >= 2) icon.SetTextures(back, front);
                        else icon.SetTexture(kind.ToString().Contains("Round") ? round : master);
                    }

                    PlayerSettings.SetPlatformIcons(target, kind, icons);
                }
            }

            AssetDatabase.SaveAssets();
            var android = PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android).Select(k => k.ToString());
            var ios = PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS).Select(k => k.ToString());
            Debug.Log($"[RushAhead] App icon installed. Android: {string.Join(", ", android)}. iOS: {string.Join(", ", ios)}.");
        }

        /// <summary>Icon textures: full quality, no mipmaps, readable so Unity can scale them to every size.</summary>
        static Texture2D Prepare(string path, bool transparent)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return null;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.alphaIsTransparency = transparent;
            importer.alphaSource = transparent ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
