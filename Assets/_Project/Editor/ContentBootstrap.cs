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
        const string SpeciesFolder = ContentFolder + "/Species";
        const string AiFolder = ContentFolder + "/AI";

        /// <summary>Curated fur swatches; the first six are the species' own colours, the rest are for dressing up.</summary>
        static readonly Color[] FurColors =
        {
            new(0.62f, 0.42f, 0.28f), // bear brown
            new(0.98f, 0.6f, 0.25f), // ginger
            new(0.96f, 0.94f, 0.9f), // cream
            new(1f, 0.66f, 0.72f), // pink
            new(0.45f, 0.31f, 0.21f), // otter brown
            new(0.6f, 0.6f, 0.64f), // raccoon grey
            new(0.27f, 0.27f, 0.3f), // charcoal
            new(0.96f, 0.8f, 0.36f), // golden
            new(0.52f, 0.76f, 0.97f), // sky
            new(0.56f, 0.88f, 0.7f), // mint
            new(0.76f, 0.62f, 0.93f), // lilac
            new(0.86f, 0.74f, 0.57f), // sand
        };
        const string SceneFolder = "Assets/_Project/Scenes";
        const string GameScene = SceneFolder + "/Game.unity";
        const string TemplateScene = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Rush Ahead/Create Default Content")]
        public static void Run()
        {
            RenderSetup.UseUniversalRenderer(); // project assets only, so it runs before the scene opens

            EnsureFolder(ContentFolder);
            EnsureFolder(BlowerFolder);
            EnsureFolder(SpeciesFolder);
            EnsureFolder(AiFolder);
            EnsureFolder(SceneFolder);

            // Open the scene before loading any assets: opening a scene unloads assets nothing references yet,
            // which would leave the references below pointing at destroyed objects (saved as null).
            var scene = OpenGameScene();
            var palette = ArtBootstrap.CreatePalette();
            var sky = ArtBootstrap.Sky();

            var blowers = new[]
            {
                Blower("LeafBlower", "Leaf Blower", "The all-rounder: strong push, slow to wind up.", new Color(1f, 0.55f, 0.15f), _ => { }), // BlowerDef's defaults
                Blower("HairDryer", "Hair Dryer", "Quick and precise: a narrow, long jet.", new Color(1f, 0.45f, 0.72f), b =>
                {
                    b.thrustNewtons = 30f; b.spoolUpMs = 140f; b.spoolDownMs = 120f;
                    b.coneHalfAngleDeg = 9f; b.range = 6.5f; b.falloffExp = 1.1f;
                    b.airCapacity = 140f;
                    b.burstImpulse = 30f; b.burstCost = 20f;
                }),
                Blower("Bellows", "Bellows", "No steady air at all: big puffs only (tap to burst).", new Color(0.62f, 0.4f, 0.22f), b =>
                {
                    // Burst-only: no continuous airflow, big puffs that recharge fast.
                    b.thrustNewtons = 0f;
                    b.coneHalfAngleDeg = 30f; b.range = 3f;
                    b.rechargePerSec = 30f; b.rechargeDelayMs = 200f;
                    b.burstImpulse = 110f; b.burstCost = 26f;
                }),
                Blower("HandFan", "Hand Fan", "Weak and wide, but instant: no wind-up at all.", new Color(0.9f, 0.25f, 0.28f), b =>
                {
                    // Weak, wide and instant.
                    b.thrustNewtons = 18f; b.spoolUpMs = 90f; b.spoolDownMs = 90f;
                    b.coneHalfAngleDeg = 34f; b.range = 2.6f;
                    b.burstImpulse = 0f;
                }),
                Blower("ShopVac", "Shop Vac", "Sucks instead of blows: pulls the raft where it points.", new Color(1f, 0.85f, 0.2f), b =>
                {
                    b.thrustNewtons = 40f; b.spoolUpMs = 400f; b.spoolDownMs = 200f;
                    b.coneHalfAngleDeg = 22f; b.range = 5f;
                    b.airCapacity = 110f;
                    b.burstImpulse = 0f;
                    b.special = BlowerSpecial.Vacuum;
                }),
            };

            // The order is part of the wire format: loadouts name species, fur and blowers by index. Only ever append.
            var species = new[]
            {
                Species("Bear", AnimalShape.Bear, 0),
                Species("Cat", AnimalShape.Cat, 1),
                Species("Bunny", AnimalShape.Bunny, 2),
                Species("Pig", AnimalShape.Pig, 3),
                Species("Otter", AnimalShape.Otter, 4),
                Species("Raccoon", AnimalShape.Raccoon, 5),
            };

            var catalog = LoadOrCreate<CrewCatalog>($"{ContentFolder}/CrewCatalog.asset");
            catalog.species = species;
            catalog.blowers = blowers;
            catalog.furColors = FurColors;
            EditorUtility.SetDirty(catalog);

            var courses = CourseLibrary.CreateDefaults();
            var aiProfiles = new[] { Ai(AiSkill.Clumsy), Ai(AiSkill.Keen), Ai(AiSkill.Ace) };

            var config = LoadOrCreate<SimConfig>($"{ContentFolder}/SimConfig.asset");
            EditorUtility.SetDirty(config); // re-saving drops fields the code no longer has (the old fixed seat list)
            WireGameScene(scene, config, catalog, courses, aiProfiles, palette, sky);
            AssetDatabase.SaveAssets();
            Debug.Log("[RushAhead] Default content and Game scene ready.");
        }

        /// <summary>Creates a blower with its tuning, or keeps an existing one's tuning. Name, blurb and colour are cosmetic and always applied.</summary>
        static BlowerDef Blower(string file, string displayName, string blurb, Color color, Action<BlowerDef> configure)
        {
            var path = $"{BlowerFolder}/{file}.asset";
            var blower = AssetDatabase.LoadAssetAtPath<BlowerDef>(path);
            if (blower == null)
            {
                blower = ScriptableObject.CreateInstance<BlowerDef>();
                configure(blower);
                AssetDatabase.CreateAsset(blower, path);
            }

            blower.displayName = displayName;
            blower.blurb = blurb;
            blower.color = color;
            EditorUtility.SetDirty(blower);
            return blower;
        }

        /// <summary>An AI skill from its built-in tuning, created once; after that the asset's tuning is kept.</summary>
        static AiProfile Ai(AiSkill skill)
        {
            var path = $"{AiFolder}/{skill}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<AiProfile>(path);
            if (profile != null) return profile;

            profile = AiProfile.Preset(skill);
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        /// <summary>Species are purely cosmetic, so their fields are always applied.</summary>
        static SpeciesDef Species(string displayName, AnimalShape shape, int defaultFur)
        {
            var species = LoadOrCreate<SpeciesDef>($"{SpeciesFolder}/{displayName}.asset");
            species.displayName = displayName;
            species.shape = shape;
            species.defaultFur = defaultFur;
            EditorUtility.SetDirty(species);
            return species;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static Scene OpenGameScene()
        {
            if (!File.Exists(GameScene) && !AssetDatabase.CopyAsset(TemplateScene, GameScene))
                throw new InvalidOperationException($"Could not create {GameScene} from {TemplateScene}.");

            return EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        }

        static void WireGameScene(Scene scene, SimConfig config, CrewCatalog catalog, CourseDef[] courses, AiProfile[] aiProfiles, ArtPalette palette, Material sky)
        {
            var bootstrap = Object.FindAnyObjectByType<GameController>();
            if (bootstrap == null) bootstrap = new GameObject("Game").AddComponent<GameController>();

            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.FindProperty("palette").objectReferenceValue = palette;
            serialized.FindProperty("catalog").objectReferenceValue = catalog;
            var list = serialized.FindProperty("courses");
            list.arraySize = courses.Length;
            for (var i = 0; i < courses.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = courses[i];
            var ai = serialized.FindProperty("aiProfiles");
            ai.arraySize = aiProfiles.Length;
            for (var i = 0; i < aiProfiles.Length; i++) ai.GetArrayElementAtIndex(i).objectReferenceValue = aiProfiles[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            UiBootstrap.Wire(bootstrap);

            // The chase camera positions itself every frame; the scene only needs a perspective camera and a sky.
            var camera = Object.FindAnyObjectByType<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 55f;
            camera.clearFlags = CameraClearFlags.Skybox;
            RenderSetup.LightScene(sky);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(GameScene, true) };
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
