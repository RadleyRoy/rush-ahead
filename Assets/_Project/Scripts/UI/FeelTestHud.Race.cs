using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>The race overlay: course and clock, the countdown, checkpoints, the results card, and picking a course.</summary>
    public sealed partial class FeelTestHud
    {
        Texture2D star;
        GUIStyle big, clock, centredSmall;
        Rect resultsCard;

        void DrawRace(float width, float height)
        {
            var run = Game.Run;
            resultsCard = Rect.zero;
            if (!run.Active) return;

            big ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 72, fontStyle = FontStyle.Bold };
            clock ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold };
            centredSmall ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13, wordWrap = true };
            if (star == null) star = StarTexture(64);

            // Course, clock and the next checkpoint, top centre.
            var bar = new Rect(width * 0.5f - 170f, 10f, 340f, 62f);
            GUI.Box(bar, GUIContent.none);
            GUI.Label(new Rect(bar.x, bar.y + 2f, bar.width, 30f), $"{run.Course.displayName}   {FormatTime(run.Seconds)}", clock);
            var gates = run.Course.gates.Length;
            var progress = run.Finished ? "Finished!" : run.GatesPassed >= gates - 1 ? "Head for the finish!" : $"Checkpoint {run.GatesPassed + 1} of {gates - 1} next";
            if (run.TeamRace)
            {
                // Red and Blue side by side: checkpoints passed (or the finishing time).
                var mine = FeelTestBootstrap.TeamName(run.MyRaft);
                var theirs = FeelTestBootstrap.TeamName(1 - run.MyRaft);
                string Standing(bool finished, float seconds, int passed) => finished ? FormatTime(seconds) : $"{passed}/{gates}";
                progress = $"{mine} {Standing(run.Finished, run.Seconds, run.GatesPassed)}   vs   {theirs} {Standing(run.RivalFinished, run.RivalSeconds, run.RivalGatesPassed)}";
            }

            GUI.Label(new Rect(bar.x, bar.y + 32f, bar.width, 24f), progress, centredSmall);

            // 3, 2, 1, GO!
            var countdown = run.Countdown > 0 ? run.Countdown.ToString() : run.SinceStart is >= 0f and < 1f ? "GO!" : null;
            if (countdown != null) Shadowed(new Rect(width * 0.5f - 150f, height * 0.32f - 50f, 300f, 100f), countdown, big);

            if (run.Finished) DrawResults(run, width, height);
        }

        void DrawResults(RaceInfo run, float width, float height)
        {
            var course = run.Course;
            resultsCard = new Rect(width * 0.5f - 190f, height * 0.5f - 130f, 380f, run.TeamRace ? 300f : 270f);
            GUILayout.BeginArea(resultsCard, GUI.skin.box);
            if (run.TeamRace)
            {
                var headline = run.Winner < 0 ? "Finished! Waiting for the other raft..."
                    : run.Winner == run.MyRaft ? $"{FeelTestBootstrap.TeamName(run.Winner)} raft wins! That's you!"
                    : $"{FeelTestBootstrap.TeamName(run.Winner)} raft wins!";
                GUILayout.Label(headline, clock);
                var theirs = run.RivalFinished ? FormatTime(run.RivalSeconds) : "still racing";
                GUILayout.Label($"{FeelTestBootstrap.TeamName(run.MyRaft)} {FormatTime(run.Seconds)}   {FeelTestBootstrap.TeamName(1 - run.MyRaft)} {theirs}", centredSmall);
            }
            else
            {
                GUILayout.Label($"{course.displayName}: finished!", clock);
            }
            GUILayout.Label(FormatTime(run.Seconds), big, GUILayout.Height(70f));

            // Stars, drawn rather than typed: not every phone font has a star in it.
            var row = GUILayoutUtility.GetRect(380f, 44f);
            for (var i = 0; i < 3; i++)
            {
                GUI.color = i < run.Stars ? new Color(1f, 0.8f, 0.15f) : new Color(1f, 1f, 1f, 0.25f);
                GUI.DrawTexture(new Rect(row.center.x - 66f + 46f * i - 20f, row.y, 40f, 40f), star);
            }

            GUI.color = Color.white;
            GUILayout.Label(run.Capsizes == 0 ? "No capsizes: a clean run!" : run.Capsizes == 1 ? "1 capsize" : $"{run.Capsizes} capsizes", centredSmall);
            GUILayout.Label(run.NewBest ? "New best time on this device!" : $"Best {FormatTime(run.Best)}", centredSmall);
            GUILayout.Label($"Par {FormatTime(course.parSeconds)}   2 stars {FormatTime(course.twoStarSeconds)}   3 stars {FormatTime(course.threeStarSeconds)}", centredSmall);

            GUILayout.FlexibleSpace();
            if (Game.CanEditRaft)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Race again", GUILayout.Height(40f))) Later(Game.ResetBoat);
                if (GUILayout.Button("Practice lagoon", GUILayout.Height(40f))) Later(() => Game.ChooseVenue(0));
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("The host starts the next race.", centredSmall);
            }

            GUILayout.EndArea();
        }

        /// <summary>The Crew panel's course picker: the host (or a solo player) chooses; joined players see what's on.</summary>
        void DrawCourses()
        {
            GUILayout.Label("Course", header);
            if (!Game.CanEditRaft)
            {
                GUILayout.Label(Game.Venue == 0 ? "Practice lagoon. The host chooses." : $"{Game.Courses[Game.Venue - 1].displayName}. The host chooses.", small);
                return;
            }

            if (GUILayout.Toggle(Game.Venue == 0, " Practice lagoon (free play)", GUI.skin.button) && Game.Venue != 0) Later(() => Game.ChooseVenue(0));
            for (var i = 0; i < Game.Courses.Count; i++)
            {
                var index = i + 1;
                var course = Game.Courses[i];
                if (GUILayout.Toggle(Game.Venue == index, $" {course.displayName} (par {FormatTime(course.parSeconds)})", GUI.skin.button) && Game.Venue != index)
                    Later(() => Game.ChooseVenue(index));
            }

            if (Game.Run.Active) GUILayout.Label(Game.Run.Course.blurb, small);
        }

        static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            var minutes = (int)(seconds / 60f);
            return $"{minutes}:{seconds - minutes * 60f:00.0}";
        }

        static void Shadowed(Rect rect, string text, GUIStyle style)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.Label(new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height), text, style);
            GUI.color = Color.white;
            GUI.Label(rect, text, style);
        }

        /// <summary>A soft-edged white five-pointed star.</summary>
        static Texture2D StarTexture(int size)
        {
            var points = new Vector2[10];
            for (var i = 0; i < 10; i++)
            {
                var angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                var radius = (i % 2 == 0 ? 0.48f : 0.2f) * size;
                points[i] = new Vector2(size * 0.5f + Mathf.Cos(angle) * radius, size * 0.5f + Mathf.Sin(angle) * radius);
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                // Four samples per pixel smooth the edges.
                var covered = 0;
                for (var sy = 0; sy < 2; sy++)
                for (var sx = 0; sx < 2; sx++)
                    if (Inside(points, new Vector2(x + 0.25f + 0.5f * sx, y + 0.25f + 0.5f * sy))) covered++;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(covered * 255 / 4));
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static bool Inside(Vector2[] polygon, Vector2 point)
        {
            var inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }

            return inside;
        }
    }
}
