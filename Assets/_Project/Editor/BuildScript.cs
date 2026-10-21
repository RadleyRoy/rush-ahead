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
    /// </summary>
    public static class BuildScript
    {
        const string AndroidDebugPath = "Builds/Android/RushAhead-debug.apk";

        [MenuItem("Rush Ahead/Build/Android Debug APK")]
        public static void AndroidDebug()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Fail("No enabled scenes in Build Settings.");
                return;
            }

            EditorUserBuildSettings.buildAppBundle = false; // .apk for sideloading; .aab is for the store
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = AndroidDebugPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development,
            });

            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Fail($"Android build {summary.result} with {summary.totalErrors} error(s).");
                return;
            }

            // summary.totalSize measures intermediate build data, not the packaged file, so read the APK itself.
            var apkMegabytes = new FileInfo(AndroidDebugPath).Length / (1024f * 1024f);
            Debug.Log($"[RushAhead] Android debug build: {AndroidDebugPath} ({apkMegabytes:F1} MB, {summary.totalTime.TotalSeconds:F0}s)");
        }

        static void Fail(string message)
        {
            Debug.LogError($"[RushAhead] {message}");
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }
}
