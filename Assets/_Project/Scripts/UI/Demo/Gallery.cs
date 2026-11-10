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
                var course = game.ShowExhibit(def, layout, () => clock, new Vector2(Along - 7.5f, -1.5f), 0f);
                course.Hazards.RaceStart = 0.0; // a race on, from the clock's zero: a tree falls a few seconds in
                var (target, side) = Aim(entry, course);
                (cameraAt, cameraTurn) = Frame(entry, target, side);
                framed = true;
                yield return new WaitForSecondsRealtime(0.6f); // terrain, trees and LODs settle
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
                    yield return Shoot(entry, ShowcaseSeconds(entry, course), entry.Id);
                }

                Destroy(def);
            }

            Debug.Log("[RushAhead.gallery] done");
            Application.Quit();
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
        static double ShowcaseSeconds(GuideEntry entry, Course course) => entry.Id switch
        {
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
            if (entry.Kind == ObstacleKind.Waterspout)
            {
                var back = new Vector3(target.x - 26f, 4f, target.y + away * 7f);
                return (back, Quaternion.LookRotation(new Vector3(target.x, 7f, target.y * 0.75f) - back, Vector3.up));
            }

            var tall = entry.Kind is ObstacleKind.Pelican or ObstacleKind.SwingingLog or ObstacleKind.FallingTree;
            var position = new Vector3(target.x - 15f, tall ? 5.5f : 6.5f, target.y + away * 5f);
            var look = new Vector3(target.x + 1f, tall ? 2.4f : 0.8f, target.y * 0.75f);
            return (position, Quaternion.LookRotation(look - position, Vector3.up));
        }

        /// <summary>A short straight river holding just this entry, or null if the game can't build it yet.</summary>
        static CourseDef Exhibit(GuideEntry entry, out CourseLayout layout)
        {
            var def = ScriptableObject.CreateInstance<CourseDef>();
            def.displayName = entry.Name;
            def.scene = entry.Kind == ObstacleKind.CalvingGlacier ? CourseScene.Rapids : CourseScene.Creek;
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
