using System;
using System.IO;
using RushAhead.Content;
using RushAhead.Render;
using RushAhead.Sim;
using RushAhead.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RushAhead.EditorTools
{
    /// <summary>
    /// Sets up the 3D renderer, the placeholder art, the default blowers, the <see cref="SimConfig"/> and the
    /// feel-test scene. Never overwrites tuning in an existing asset, so tuned values survive running it again.
    ///
    /// Batchmode:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod RushAhead.EditorTools.ContentBootstrap.Run
    /// </summary>
    public static class ContentBootstrap
    {
        const string ContentFolder = "Assets/_Project/Content";
        const string BlowerFolder = ContentFolder + "/Blowers";
        const string SceneFolder = "Assets/_Project/Scenes";
        const string FeelTestScene = SceneFolder + "/FeelTest.unity";
        const string TemplateScene = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Rush Ahead/Create Default Content")]
        public static void Run()
        {
            RenderSetup.UseUniversalRenderer(); // project assets only, so it runs before the scene opens

            EnsureFolder(ContentFolder);
            EnsureFolder(BlowerFolder);
            EnsureFolder(SceneFolder);

            // Open the scene before loading any assets: opening a scene unloads assets nothing references yet,
            // which would leave the references below pointing at destroyed objects (saved as null).
            var scene = OpenFeelTestScene();
            var palette = ArtBootstrap.CreatePalette();
            var sky = ArtBootstrap.Sky();

            var blowers = new[]
            {
                Blower("LeafBlower", "Leaf Blower", new Color(1f, 0.55f, 0.15f), _ => { }), // the baseline: BlowerDef's defaults
                Blower("HairDryer", "Hair Dryer", new Color(1f, 0.45f, 0.72f), b =>
                {
                    b.thrustNewtons = 30f; b.spoolUpMs = 140f; b.spoolDownMs = 120f;
                    b.coneHalfAngleDeg = 9f; b.range = 6.5f; b.falloffExp = 1.1f;
                    b.airCapacity = 140f; b.drainPerSec = 14f;
                    b.burstImpulse = 30f; b.burstCost = 20f;
                }),
                Blower("Bellows", "Bellows", new Color(0.62f, 0.4f, 0.22f), b =>
                {
                    // Burst-only: no continuous airflow, big puffs that recharge fast.
                    b.thrustNewtons = 0f;
                    b.coneHalfAngleDeg = 30f; b.range = 3f;
                    b.rechargePerSec = 30f; b.rechargeDelayMs = 200f;
                    b.burstImpulse = 110f; b.burstCost = 26f;
                }),
                Blower("HandFan", "Hand Fan", new Color(0.9f, 0.25f, 0.28f), b =>
                {
                    // Weak, wide, instant, and never runs out.
                    b.thrustNewtons = 18f; b.spoolUpMs = 90f; b.spoolDownMs = 90f;
                    b.coneHalfAngleDeg = 34f; b.range = 2.6f;
                    b.drainPerSec = 0f;
                    b.burstImpulse = 0f;
                }),
                Blower("ShopVac", "Shop Vac", new Color(1f, 0.85f, 0.2f), b =>
                {
                    b.thrustNewtons = 40f; b.spoolUpMs = 400f; b.spoolDownMs = 200f;
                    b.coneHalfAngleDeg = 22f; b.range = 5f;
                    b.airCapacity = 110f; b.drainPerSec = 26f;
                    b.burstImpulse = 0f;
                    b.special = BlowerSpecial.Vacuum;
                }),
            };

            var config = LoadOrCreate<SimConfig>($"{ContentFolder}/SimConfig.asset");
            EditorUtility.SetDirty(config); // re-saving drops fields the code no longer has (the old fixed seat list)
            WireFeelTestScene(scene, config, blowers, palette, sky);
            AssetDatabase.SaveAssets();
            Debug.Log("[RushAhead] Default content and FeelTest scene ready.");
        }

        /// <summary>Creates a blower with its tuning, or keeps an existing one's tuning. The colour is cosmetic and always applied.</summary>
        static BlowerDef Blower(string file, string displayName, Color color, Action<BlowerDef> configure)
        {
            var path = $"{BlowerFolder}/{file}.asset";
            var blower = AssetDatabase.LoadAssetAtPath<BlowerDef>(path);
            if (blower == null)
            {
                blower = ScriptableObject.CreateInstance<BlowerDef>();
                blower.displayName = displayName;
                configure(blower);
                AssetDatabase.CreateAsset(blower, path);
            }

            blower.color = color;
            EditorUtility.SetDirty(blower);
            return blower;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static Scene OpenFeelTestScene()
        {
            if (!File.Exists(FeelTestScene) && !AssetDatabase.CopyAsset(TemplateScene, FeelTestScene))
                throw new InvalidOperationException($"Could not create {FeelTestScene} from {TemplateScene}.");

            return EditorSceneManager.OpenScene(FeelTestScene, OpenSceneMode.Single);
        }

        static void WireFeelTestScene(Scene scene, SimConfig config, BlowerDef[] blowers, ArtPalette palette, Material sky)
        {
            var bootstrap = Object.FindAnyObjectByType<FeelTestBootstrap>();
            if (bootstrap == null) bootstrap = new GameObject("Feel Test").AddComponent<FeelTestBootstrap>();

            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.FindProperty("palette").objectReferenceValue = palette;
            var list = serialized.FindProperty("blowers");
            list.arraySize = blowers.Length;
            for (var i = 0; i < blowers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = blowers[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // The chase camera positions itself every frame; the scene only needs a perspective camera and a sky.
            var camera = Object.FindAnyObjectByType<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 55f;
            camera.clearFlags = CameraClearFlags.Skybox;
            RenderSetup.LightScene(sky);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(FeelTestScene, true) };
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
