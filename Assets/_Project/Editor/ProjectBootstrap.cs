using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace RushAhead.EditorTools
{
    /// <summary>
    /// Applies every project-level setting the game depends on, so they live in version-controlled
    /// code rather than in hand-clicked Project Settings. Idempotent: safe to run any number of times.
    ///
    /// Batchmode:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod RushAhead.EditorTools.ProjectBootstrap.Run
    /// </summary>
    public static class ProjectBootstrap
    {
        public const string ProductName = "Rush Ahead";
        public const string CompanyName = "Rush Ahead";

        // Placeholder. The Android application id is permanent once published to Google Play,
        // so confirm the final value before the first store upload.
        public const string BundleId = "com.rushahead.game";

        [MenuItem("Rush Ahead/Apply Project Settings")]
        public static void Run()
        {
            // Text-serialised YAML and visible .meta files so scenes and prefabs diff and merge in git.
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;

            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, BundleId);

            // Google Play requires 64-bit; IL2CPP is required for ARM64.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // Landscape only: two thumbs on a wide screen.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // Top-down water game: no gravity anywhere in 2D physics.
            SetProjectSetting("ProjectSettings/Physics2DSettings.asset", "m_Gravity", so => so.vector2Value = Vector2.zero);

            AssetDatabase.SaveAssets();
            Debug.Log($"[RushAhead] Project settings applied: {ProductName} ({BundleId}), IL2CPP/ARM64, landscape, ForceText, 2D physics gravity off.");
        }

        static void SetProjectSetting(string assetPath, string property, System.Action<SerializedProperty> apply)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (assets.Length == 0)
            {
                Debug.LogError($"[RushAhead] Could not load {assetPath}");
                return;
            }

            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty(property);
            if (prop == null)
            {
                Debug.LogError($"[RushAhead] {assetPath} has no property '{property}'");
                return;
            }

            apply(prop);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
