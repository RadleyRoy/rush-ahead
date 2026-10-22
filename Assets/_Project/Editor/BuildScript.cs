using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RushAhead.EditorTools
{
    /// <summary>
    /// Command-line builds for local verification and CI.
    ///
    /// Batchmode:
    ///   Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod RushAhead.EditorTools.BuildScript.AndroidDebug
    ///   Unity.exe -batchmode -quit -projectPath . -buildTarget Win64 -executeMethod RushAhead.EditorTools.BuildScript.WindowsDev
    /// </summary>
    public static class BuildScript
    {
        const string AndroidDebugPath = "Builds/Android/RushAhead-debug.apk";
        const string WindowsDevPath = "Builds/Windows/RushAhead.exe";

        [MenuItem("Rush Ahead/Build/Android Debug APK")]
        public static void AndroidDebug()
        {
            EditorUserBuildSettings.buildAppBundle = false; // .apk for sideloading; .aab is for the store
            if (!Build(BuildTarget.Android, BuildTargetGroup.Android, AndroidDebugPath, out var seconds)) return;

            // BuildSummary.totalSize measures intermediate build data, not the packaged file, so read the APK itself.
            var apkMegabytes = new FileInfo(AndroidDebugPath).Length / (1024f * 1024f);
            Debug.Log($"[RushAhead] Android debug build: {AndroidDebugPath} ({apkMegabytes:F1} MB, {seconds:F0}s)");
        }

        /// <summary>A development Windows player: an extra local client on the dev PC, and the target for the scripted demo.</summary>
        [MenuItem("Rush Ahead/Build/Windows Dev Player")]
        public static void WindowsDev()
        {
            if (Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, WindowsDevPath, out var seconds))
                Debug.Log($"[RushAhead] Windows dev build: {WindowsDevPath} ({seconds:F0}s)");
        }

        static bool Build(BuildTarget target, BuildTargetGroup group, string path, out double seconds)
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
                options = BuildOptions.Development,
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
