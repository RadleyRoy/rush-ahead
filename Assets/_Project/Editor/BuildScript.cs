using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RushAhead.EditorTools
{
    /// <summary>
    /// Command-line builds for local verification, CI and the store.
    ///
    /// Batchmode:
    ///   Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod RushAhead.EditorTools.BuildScript.AndroidDebug
    ///   Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod RushAhead.EditorTools.BuildScript.AndroidRelease
    ///   Unity.exe -batchmode -quit -projectPath . -buildTarget Win64 -executeMethod RushAhead.EditorTools.BuildScript.WindowsDev
    ///   Unity.exe -batchmode -quit -projectPath . -buildTarget Win64 -executeMethod RushAhead.EditorTools.BuildScript.WindowsRelease
    ///
    /// The store build is signed with the upload key named by four environment variables, which only ever live on the
    /// machine that builds (never in the project): RUSHAHEAD_KEYSTORE (the .keystore file), RUSHAHEAD_KEYSTORE_PASS,
    /// RUSHAHEAD_KEY_ALIAS and RUSHAHEAD_KEY_PASS. Without them it's signed with Unity's debug key and named
    /// NOT-FOR-UPLOAD, which proves the build works but which Google Play won't accept.
    /// </summary>
    public static class BuildScript
    {
        /// <summary>The version players see. Bump it for each release; the version code counts itself.</summary>
        public const string Version = "1.0.0";

        const string AndroidDebugPath = "Builds/Android/RushAhead-debug.apk";
        const string WindowsDevPath = "Builds/Windows/RushAhead.exe";
        const string WindowsReleasePath = "Builds/WindowsRelease/RushAhead.exe";

        [MenuItem("Rush Ahead/Build/Android Debug APK")]
        public static void AndroidDebug()
        {
            EditorUserBuildSettings.buildAppBundle = false; // .apk for sideloading; .aab is for the store
            if (!Build(BuildTarget.Android, BuildTargetGroup.Android, AndroidDebugPath, out var seconds)) return;

            // BuildSummary.totalSize measures intermediate build data, not the packaged file, so read the APK itself.
            var apkMegabytes = new FileInfo(AndroidDebugPath).Length / (1024f * 1024f);
            Debug.Log($"[RushAhead] Android debug build: {AndroidDebugPath} ({apkMegabytes:F1} MB, {seconds:F0}s)");
        }

        /// <summary>
        /// The Google Play build: an Android App Bundle (.aab), not a development build, with native debug symbols for
        /// Play's crash reports. Version name <see cref="Version"/>; version code the number of commits, so every upload
        /// from a newer commit counts up (set RUSHAHEAD_VERSION_CODE to override, say for a rebuild of the same commit).
        /// </summary>
        [MenuItem("Rush Ahead/Build/Android Release Bundle (Google Play)")]
        public static void AndroidRelease()
        {
            var code = VersionCode();
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.Android.bundleVersionCode = code;
            EditorUserBuildSettings.buildAppBundle = true;
#pragma warning disable CS0618 // still the way to ask for a symbols file in a batch build
            EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Public;
#pragma warning restore CS0618

            var keystore = Environment.GetEnvironmentVariable("RUSHAHEAD_KEYSTORE");
            var signed = !string.IsNullOrEmpty(keystore) && File.Exists(keystore);
            var path = $"Builds/Android/RushAhead-{Version}-{code}{(signed ? "" : "-NOT-FOR-UPLOAD")}.aab";
            try
            {
                PlayerSettings.Android.useCustomKeystore = signed;
                if (signed)
                {
                    PlayerSettings.Android.keystoreName = Path.GetFullPath(keystore);
                    PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("RUSHAHEAD_KEYSTORE_PASS") ?? "";
                    PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("RUSHAHEAD_KEY_ALIAS") ?? "upload";
                    PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("RUSHAHEAD_KEY_PASS") ?? "";
                }
                else
                {
                    Debug.LogWarning("[RushAhead] No upload key (RUSHAHEAD_KEYSTORE): signing with Unity's debug key. Google Play won't take this bundle.");
                }

                if (!Build(BuildTarget.Android, BuildTargetGroup.Android, path, out var seconds, BuildOptions.None)) return;
                var megabytes = new FileInfo(path).Length / (1024f * 1024f);
                Debug.Log($"[RushAhead] Android release bundle: {path} (version {Version}, code {code}, {megabytes:F1} MB, " +
                          $"{(signed ? "signed with the upload key" : "debug-signed")}, {seconds:F0}s)");
            }
            finally
            {
                // The key's details are only for this build: never left in the project's settings.
                PlayerSettings.Android.useCustomKeystore = false;
                PlayerSettings.Android.keystoreName = "";
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasName = "";
                PlayerSettings.Android.keyaliasPass = "";
                EditorUserBuildSettings.buildAppBundle = false;
            }
        }

        /// <summary>A Windows player without the development watermark, for store screenshots.</summary>
        [MenuItem("Rush Ahead/Build/Windows Release Player")]
        public static void WindowsRelease()
        {
            if (Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, WindowsReleasePath, out var seconds, BuildOptions.None))
                Debug.Log($"[RushAhead] Windows release build: {WindowsReleasePath} ({seconds:F0}s)");
        }

        /// <summary>The number of commits, from git; RUSHAHEAD_VERSION_CODE wins if set.</summary>
        static int VersionCode()
        {
            if (int.TryParse(Environment.GetEnvironmentVariable("RUSHAHEAD_VERSION_CODE"), out var forced) && forced > 0) return forced;
            try
            {
                using var git = Process.Start(new ProcessStartInfo("git", "rev-list --count HEAD")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                var output = git!.StandardOutput.ReadToEnd().Trim();
                git.WaitForExit();
                if (int.TryParse(output, out var commits) && commits > 0) return commits;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RushAhead] Couldn't count commits for the version code: {e.Message}");
            }

            return 1;
        }

        /// <summary>A development Windows player: an extra local client on the dev PC, and the target for the scripted demo.</summary>
        [MenuItem("Rush Ahead/Build/Windows Dev Player")]
        public static void WindowsDev()
        {
            if (Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, WindowsDevPath, out var seconds))
                Debug.Log($"[RushAhead] Windows dev build: {WindowsDevPath} ({seconds:F0}s)");
        }

        static bool Build(BuildTarget target, BuildTargetGroup group, string path, out double seconds, BuildOptions options = BuildOptions.Development)
        {
            seconds = 0;
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Fail("No enabled scenes in Build Settings. Run RushAhead.EditorTools.ContentBootstrap.Run first.");
                return false;
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = target,
                targetGroup = group,
                options = options,
            });

            var summary = report.summary;
            seconds = summary.totalTime.TotalSeconds;
            if (summary.result == BuildResult.Succeeded) return true;

            Fail($"{target} build {summary.result} with {summary.totalErrors} error(s).");
            return false;
        }

        static void Fail(string message)
        {
            Debug.LogError($"[RushAhead] {message}");
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }
}
