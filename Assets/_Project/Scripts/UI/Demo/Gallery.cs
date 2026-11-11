using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>
    /// Photographs every Field Guide entry on its own: launch a player build with <c>-rushahead-gallery &lt;folder&gt;</c>.
    /// Each obstacle (or course feature) is built alone on a short straight river with the raft moored just upstream, and
    /// shot from upstream and to one side, the camera held still. By default each is shot at its showcase moment
    /// (<see cref="ShowcaseSeconds"/>) as <c>&lt;Id&gt;.png</c>; <c>-rushahead-gallery-sheet</c> shoots eight moments
    /// over six seconds instead (<c>&lt;Id&gt;-0.png</c>...) for checking how each one moves. <c>-rushahead-gallery-only
    /// Id,Id</c> limits it to some entries. The game quits when done.
    /// </summary>
    public sealed class Gallery : MonoBehaviour
    {
        const string Flag = "-rushahead-gallery";
        const float Along = 72f; // where the exhibit stands, down the straight river
        const float Width = 16f;

        GameController game;
        string folder;
        bool sheet;
        HashSet<string> only;
        double clock;
        Vector3 cameraAt;
        Quaternion cameraTurn;
        bool framed;
        bool holdTurbo;

        public static void StartIfRequested(GameController game)
        {
            var folder = CommandLine.After(Flag);
            if (folder == null) return;
            Directory.CreateDirectory(folder);
            var gallery = game.gameObject.AddComponent<Gallery>();
            gallery.game = game;
            gallery.folder = folder;
            gallery.sheet = CommandLine.Has("-rushahead-gallery-sheet");
            if (CommandLine.After("-rushahead-gallery-only") is { } ids) gallery.only = new HashSet<string>(ids.Split(','));
            game.AllowSearching = false;
        }

        IEnumerator Start()
        {
            while (game.Ui == null || game.Ui.Flow.Screen != AppScreen.MainMenu) yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            AudioListener.volume = 0f;
            GameUi.CameraHeld = true;
            game.Ui.GetComponent<UIDocument>().rootVisualElement.style.display = DisplayStyle.None;
            var cam = Camera.main;
            cam.fieldOfView = 46f;

            foreach (var entry in ObstacleGuide.Entries)
            {
                if (only != null && !only.Contains(entry.Id)) continue;
                var def = Exhibit(entry, out var layout);
                if (def == null)
                {
                    Debug.Log($"[RushAhead.gallery] {entry.Id}: not built yet, skipped");
                    continue;
                }

                clock = 0.0;
                var course = game.ShowExhibit(def, layout, () => clock, RaftAt, 0f);
                course.Hazards.RaceStart = 0.0; // a race on, from the clock's zero: a tree falls a few seconds in
                var exhibitBoss = Dress(entry, course);
                var (target, side) = Aim(entry, course);
                (cameraAt, cameraTurn) = Frame(entry, target, side);
                framed = true;
                yield return new WaitForSecondsRealtime(entry.Id == "Snapjaw" ? 1.7f : 0.6f); // terrain, trees and LODs settle (and Snapjaw rises)
                if (CommandLine.Has("-rushahead-gallery-sizes"))
                    foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                        if (r.name is "Boulder" or "Rock" or "Buoy" or "Barrel" or "Stone" or "Funnel" or "Veil" or "Hump" or "Billow" or "Spray")
                            Debug.Log($"[RushAhead.gallery] {entry.Id} size {r.name} {r.bounds.size} at {r.bounds.center} scale {r.transform.lossyScale.x:0.###} mesh {r.GetComponent<MeshFilter>().sharedMesh.name} {r.GetComponent<MeshFilter>().sharedMesh.bounds.size.x:0.#####} {r.GetComponent<MeshFilter>().sharedMesh.bounds.size.y:0.#####} {r.GetComponent<MeshFilter>().sharedMesh.bounds.size.z:0.#####} static {r.isPartOfStaticBatch} visible {r.isVisible} enabled {r.enabled} active {r.gameObject.activeInHierarchy} mat {r.sharedMaterial.name} {r.sharedMaterial.color} q {r.sharedMaterial.renderQueue}");

                if (sheet)
                {
                    for (var i = 0; i < 8; i++)
                        yield return Shoot(entry, i * 0.75, $"{entry.Id}-{i}");
                }
                else
                {
                    var at = ShowcaseSeconds(entry, course);
                    if (entry.Id is "Turbo" or "Shield" or "Horn") yield return ShowItem(entry.Id);
                    yield return Shoot(entry, at, entry.Id);
                    holdTurbo = false;
                }

                if (exhibitBoss != null) Destroy(exhibitBoss.gameObject);
                Destroy(def);
            }

            Debug.Log("[RushAhead.gallery] done");
            Application.Quit();
        }

        static readonly Vector2 RaftAt = new(Along - 7.5f, -1.5f);

        /// <summary>What the newer river entries need around them: weather, a knockout banner, Old Snapjaw behind the raft.</summary>
        Render.BossView Dress(GuideEntry entry, Course course)
        {
            switch (entry.Id)
            {
                case "Gust":
                    game.ExhibitWeather(Weather.Rain);
                    break;
                case "FogNight":
                    game.ExhibitWeather(Weather.Night);
                    break;
                case "Knockout":
                    game.ExhibitView.ShowKnockouts(new[] { 1 });
                    break;
                case "Snapjaw":
                {
                    var boss = Render.BossView.Build(game.ExhibitPalette, game.ExhibitView.transform);
                    var at = new Vector2(Along - 15.5f, -1f);
                    boss.State = () => (true, at, 0f, BossMood.Lunging, 0);
                    return boss;
                }
            }

            return null;
        }

        /// <summary>The raft using an item: a turbo flaring, its bubble up, or the horn just sounded.</summary>
        IEnumerator ShowItem(string id)
        {
            var boat = game.Boat;
            if (boat == null) yield break;
            switch (id)
            {
                case "Turbo":
                    holdTurbo = true;
                    break;
                case "Shield":
                    boat.Shielded = true;
                    break;
                case "Horn":
                    boat.HornCount++;
                    break;
            }

            yield return new WaitForSecondsRealtime(id == "Horn" ? 0.08f : 0.5f); // the horn's ring is still near the raft
        }

        /// <summary>
        /// Keeps the exhibit raft's turbo lit: a moored raft isn't racing, so each sim step puts its turbo out, and this
        /// lights it again after the step and before the effects read it.
        /// </summary>
        void Update()
        {
            if (holdTurbo && game.Boat != null) game.Boat.TurboSeconds = 2.4f;
        }

        IEnumerator Shoot(GuideEntry entry, double seconds, string name)
        {
            clock = seconds;
            yield return null;
            yield return null; // posed in LateUpdate, drawn the frame after
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "[RushAhead.gallery] shot={0} entry={1} t={2:0.00}", name, entry.Id, seconds));
            yield return null;
            yield return null;
        }

        /// <summary>The moment that shows an entry best: mid-action, from its own timetable where it has one.</summary>
        static double ShowcaseSeconds(GuideEntry entry, Course course)
        {
            // The newer kinds: a moment in their own timetable, whatever period and offset the dealer gave them.
            if (entry.Kind is { } kind && NewObstacles.Covers(kind))
                foreach (var obstacle in course.Hazards.Obstacles)
                    if (obstacle is ISpotted { Spot: var spot } && spot.kind == kind)
                        return Mathf.Repeat(ShowcasePhase(kind) * spot.period - spot.offset, Mathf.Max(0.01f, spot.period));
            return OlderShowcase(entry);
        }

        /// <summary>How far round its timetable each newer kind looks its best: mid-strike, mid-leap, mid-gust.</summary>
        static float ShowcasePhase(ObstacleKind kind) => kind switch
        {
            ObstacleKind.Kraken => 0.64f,
            ObstacleKind.TidalBore => 0.36f,
            ObstacleKind.WreckingBall => 0.04f,
            ObstacleKind.StormCloud => 0.74f,
            ObstacleKind.PaddleSteamer => 0.1f,
            ObstacleKind.Drawbridge => 0.45f,
            ObstacleKind.CargoCrane => 0.6f,
            ObstacleKind.LogFlume => 0.45f,
            ObstacleKind.CalvingGlacier => 0.67f,
            ObstacleKind.DynamiteFloat => 0.6f,
            ObstacleKind.LeapingFrogs => 0.76f,
            ObstacleKind.GiantFan => 0.75f,
            ObstacleKind.ChainedMines => 0.3f,
            _ => 0f,
        };

        /// <summary>A moment when a gust is blowing its hardest (the exhibit's weather is dealt from seed 7).</summary>
        static double GustShowcase()
        {
            var gusts = new Gusts(7);
            for (var t = 6.0; t < 40.0; t += 0.05)
                if (gusts.At(t).strength > 0.9f) return t;
            return 10.0;
        }

        static double OlderShowcase(GuideEntry entry) => entry.Id switch
        {
            "Gust" => GustShowcase(),
            "Bear" or "Sweeper" or "SwingingLog" or "ElectricEels" or "Ferry" or "Waterspout" or "Barrel" => 1.5,
            "BeaverDam" or "Cannon" or "Geyser" or "IceFloes" or "Mudslide" or "Rockslide" => 0.75,
            "Crocodile" or "NetBoat" or "PiranhaShoal" => 2.25,
            "FallingTree" or "Hippo" or "Turtle" => 3.75,
            "Pelican" => 4.5,
            _ => 0.0,
        };


        /// <summary>What to look at, and which side of the river (+1 left, -1 right) the thing stands on (0: the middle).</summary>
        static (Vector2 target, float side) Aim(GuideEntry entry, Course course)
        {
            var river = course.River;
            var markers = course.Hazards.Markers;
            var mine = -1;
            for (var i = 0; i < markers.Count; i++)
                if (markers[i].Id == entry.Id) mine = i;
            if (entry.Kind != null && mine >= 0)
            {
                var m = markers[mine];
                var at = river.At(m.Distance);
                return (at.Centre + at.Left * m.Across, Mathf.Abs(m.Across) > 2f ? Mathf.Sign(m.Across) : 0f);
            }

            return entry.Id switch
            {
                "Edge" => (new Vector2(Along, Width * 0.5f), 1f),
                "Island" => (new Vector2(Along + 2f, 2f), 0f),
                "Knockout" => (new Vector2(Along - 1f, 0f), 0f),
                "FogNight" => (new Vector2(Along - 2f, 0f), 0f),
                _ => (new Vector2(Along, 0f), 0f),
            };
        }

        /// <summary>Holds the camera where the gallery put it (a raft moved to its mooring snaps the chase camera to it).</summary>
        void LateUpdate()
        {
            if (framed) Camera.main.transform.SetPositionAndRotation(cameraAt, cameraTurn);
        }

        /// <summary>The camera upstream of the target and off to the side away from it, looking down at it.</summary>
        static (Vector3, Quaternion) Frame(GuideEntry entry, Vector2 target, float side)
        {
            var away = side != 0f ? -side : -1f;
            if (entry.Id is "Turbo" or "Shield" or "Horn")
            {
                // Close on the raft itself, from behind and to one side.
                var near = new Vector3(RaftAt.x - 6.5f, 3.4f, RaftAt.y + 4.5f);
                return (near, Quaternion.LookRotation(new Vector3(RaftAt.x + 0.5f, 0.7f, RaftAt.y) - near, Vector3.up));
            }

            if (entry.Id == "Snapjaw")
            {
                // From the side: the raft ahead, Snapjaw lunging up behind it.
                var beside = new Vector3(Along - 11f, 4.6f, 10f);
                return (beside, Quaternion.LookRotation(new Vector3(Along - 11.5f, 0.6f, -1f) - beside, Vector3.up));
            }

            if (entry.Kind == ObstacleKind.Waterspout)
            {
                var back = new Vector3(target.x - 26f, 4f, target.y + away * 7f);
                return (back, Quaternion.LookRotation(new Vector3(target.x, 7f, target.y * 0.75f) - back, Vector3.up));
            }

            if (entry.Kind is ObstacleKind.StormCloud or ObstacleKind.WreckingBall or ObstacleKind.CargoCrane or ObstacleKind.GiantFan or ObstacleKind.Kraken)
            {
                var high = new Vector3(target.x - 21f, 8.5f, target.y + away * 8f);
                return (high, Quaternion.LookRotation(new Vector3(target.x + 1f, 3.2f, target.y * 0.6f) - high, Vector3.up));
            }

            var tall = entry.Kind is ObstacleKind.Pelican or ObstacleKind.SwingingLog or ObstacleKind.FallingTree or ObstacleKind.Drawbridge or ObstacleKind.CalvingGlacier or
                ObstacleKind.LogFlume or ObstacleKind.MillRace or ObstacleKind.PaddleSteamer;
            var position = new Vector3(target.x - 15f, tall ? 5.5f : 6.5f, target.y + away * 5f);
            var look = new Vector3(target.x + 1f, tall ? 2.4f : 0.8f, target.y * 0.75f);
            return (position, Quaternion.LookRotation(look - position, Vector3.up));
        }

        /// <summary>A short straight river holding just this entry, or null if the game can't build it yet.</summary>
        static CourseDef Exhibit(GuideEntry entry, out CourseLayout layout)
        {
            var def = ScriptableObject.CreateInstance<CourseDef>();
            def.displayName = entry.Name;
            def.scene = entry.Kind switch
            {
                ObstacleKind.CalvingGlacier or ObstacleKind.IceFloes => CourseScene.Glacier,
                ObstacleKind.DynamiteFloat or ObstacleKind.CargoCrane => CourseScene.Canyon,
                _ => CourseScene.Creek,
            };
            def.river = new[]
            {
                new RiverPoint(0f, 0f, Width), new RiverPoint(40f, 0f, Width), new RiverPoint(80f, 0f, Width), new RiverPoint(120f, 0f, Width),
                new RiverPoint(160f, 0f, Width), new RiverPoint(200f, 0f, Width), // straight along x, so x is the distance down it
            };
            def.startDistance = 8f;
            def.gates = new[] { 150f };
            def.flow = 3f;
            layout = new CourseLayout();
            switch (entry.Id)
            {
                case "Island":
                    foreach (var i in new[] { 1, 2, 3 }) def.river[i].width = 26f;
                    def.islands = new[] { new IslandSpot(Along - 14f, 34f, 2f, 3.5f, 1f) };
                    break;
                case "Crate":
                    layout.crates = new[] { new CrateSpot(Along - 1f, 1.8f), new CrateSpot(Along + 1.5f, -2.6f) };
                    break;
                case "Knockout":
                case "FogNight":
                    def.gates = new[] { Along + 4f, 150f };
                    break;
                case "Snapjaw":
                    def.scene = CourseScene.Swamp;
                    break;
                case "Turbo":
                case "Shield":
                case "Horn":
                case "Gust":
                    break;
                case "Edge":
                    break;
                case "Current":
                    def.currents = new[] { new CurrentZone(Along - 25f, Along + 25f, 3f) };
                    break;
                case "Whirlpool":
                    def.whirlpools = new[] { new WhirlpoolSpot(new Vector2(Along + 4f, 0f), 6f, 2.4f, 1.2f) };
                    break;
                case "BoostLane":
                    layout.boosts = new[] { new BoostSpot(Along - 4f, 0f) };
                    break;
                case "Rock":
                    def.rocks = new[] { new RockSpot(Along, 2f, 1.4f), new RockSpot(Along + 6f, -3f, 1f) };
                    break;
                case "Barrel":
                    def.barrels = new[] { new Vector2(Along, 1f), new Vector2(Along + 3f, -2f), new Vector2(Along + 6f, 3f) };
                    break;
                case "DriftingLog":
                    def.logs = new[] { new LogSpot(new Vector2(Along, 0f), 20f, 6f) };
                    break;
                default:
                    if (entry.Kind == null) break;
                    // A river wide enough for every kind (cannons want 7 m either side of the middle).
                    layout = LayoutGenerator.Single(def, new River(def.river), entry.Kind.Value, Along);
                    if (layout == null || layout.Count == 0)
                    {
                        Destroy(def);
                        return null;
                    }

                    break;
            }

            return def;
        }
    }
}
