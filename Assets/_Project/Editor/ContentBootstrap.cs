using System;
using System.IO;
using RushAhead.Content;
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
    /// Creates the default blowers, the <see cref="SimConfig"/> and the feel-test scene. Never overwrites an
    /// existing asset, so tuned values survive running it again.
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
            EnsureFolder(ContentFolder);
            EnsureFolder(BlowerFolder);
            EnsureFolder(SceneFolder);

            // Open the scene before loading any assets: opening a scene unloads assets nothing references yet,
            // which would leave the references below pointing at destroyed objects (saved as null).
            var scene = OpenFeelTestScene();

            var blowers = new[]
            {
                Blower("LeafBlower", "Leaf Blower", _ => { }), // the baseline: BlowerDef's defaults
                Blower("HairDryer", "Hair Dryer", b =>
                {
                    b.thrustNewtons = 30f; b.spoolUpMs = 140f; b.spoolDownMs = 120f;
                    b.coneHalfAngleDeg = 9f; b.range = 6.5f; b.falloffExp = 1.1f;
                    b.airCapacity = 140f; b.drainPerSec = 14f;
                    b.burstImpulse = 30f; b.burstCost = 20f;
                }),
                Blower("Bellows", "Bellows", b =>
                {
                    // Burst-only: no continuous airflow, big puffs that recharge fast.
                    b.thrustNewtons = 0f;
                    b.coneHalfAngleDeg = 30f; b.range = 3f;
                    b.rechargePerSec = 30f; b.rechargeDelayMs = 200f;
                    b.burstImpulse = 110f; b.burstCost = 26f;
                }),
                Blower("HandFan", "Hand Fan", b =>
                {
                    // Weak, wide, instant, and never runs out.
                    b.thrustNewtons = 18f; b.spoolUpMs = 90f; b.spoolDownMs = 90f;
                    b.coneHalfAngleDeg = 34f; b.range = 2.6f;
                    b.drainPerSec = 0f;
                    b.burstImpulse = 0f;
                }),
                Blower("ShopVac", "Shop Vac", b =>
                {
                    b.thrustNewtons = 40f; b.spoolUpMs = 400f; b.spoolDownMs = 200f;
                    b.coneHalfAngleDeg = 22f; b.range = 5f;
                    b.airCapacity = 110f; b.drainPerSec = 26f;
                    b.burstImpulse = 0f;
                    b.special = BlowerSpecial.Vacuum;
                }),
            };

            var config = LoadOrCreate<SimConfig>($"{ContentFolder}/SimConfig.asset");
            WireFeelTestScene(scene, config, blowers);
            AssetDatabase.SaveAssets();
            Debug.Log("[RushAhead] Default content and FeelTest scene ready.");
        }

        static BlowerDef Blower(string file, string displayName, Action<BlowerDef> configure)
        {
            var path = $"{BlowerFolder}/{file}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<BlowerDef>(path);
            if (existing != null) return existing;

            var blower = ScriptableObject.CreateInstance<BlowerDef>();
            blower.displayName = displayName;
            configure(blower);
            AssetDatabase.CreateAsset(blower, path);
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

        static void WireFeelTestScene(Scene scene, SimConfig config, BlowerDef[] blowers)
        {
            var bootstrap = Object.FindAnyObjectByType<FeelTestBootstrap>();
            if (bootstrap == null) bootstrap = new GameObject("Feel Test").AddComponent<FeelTestBootstrap>();

            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("config").objectReferenceValue = config;
            var list = serialized.FindProperty("blowers");
            list.arraySize = blowers.Length;
            for (var i = 0; i < blowers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = blowers[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var camera = Object.FindAnyObjectByType<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.33f, 0.5f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(FeelTestScene, true) };
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
