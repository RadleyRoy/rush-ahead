using System.Collections.Generic;
using System.Globalization;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>
    /// This device's best run on each course, kept apart for Co-op and Team Race (two rafts sharing a river are slower),
    /// with the time at each checkpoint so later runs can be compared as they go.
    /// </summary>
    public static class Records
    {
        /// <summary>The best time, or 0 if there isn't one yet.</summary>
        public static float BestTime(CourseDef course, bool teamRace) => PlayerPrefs.GetFloat(Key("best", course, teamRace), 0f);

        public static int BestStars(CourseDef course, bool teamRace)
        {
            var best = BestTime(course, teamRace);
            return best > 0f ? course.Stars(best) : 0;
        }

        /// <summary>The best run's time at each checkpoint (the last is the finish), or empty.</summary>
        public static float[] BestSplits(CourseDef course, bool teamRace)
        {
            var saved = PlayerPrefs.GetString(Key("splits", course, teamRace), "");
            if (saved.Length == 0) return System.Array.Empty<float>();
            var parts = saved.Split(',');
            var splits = new float[parts.Length];
            for (var i = 0; i < parts.Length; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out splits[i])) return System.Array.Empty<float>();
            return splits;
        }

        /// <summary>Keeps this run if it's the best yet. Returns whether it was.</summary>
        public static bool Submit(CourseDef course, bool teamRace, float seconds, IReadOnlyList<float> splits)
        {
            var best = BestTime(course, teamRace);
            if (best > 0f && seconds >= best) return false;

            PlayerPrefs.SetFloat(Key("best", course, teamRace), seconds);
            var text = new string[splits.Count];
            for (var i = 0; i < text.Length; i++) text[i] = splits[i].ToString("0.###", CultureInfo.InvariantCulture);
            PlayerPrefs.SetString(Key("splits", course, teamRace), string.Join(",", text));
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Forgets every best time on this device.</summary>
        public static void Clear(IEnumerable<CourseDef> courses)
        {
            foreach (var course in courses)
                foreach (var team in new[] { false, true })
                {
                    PlayerPrefs.DeleteKey(Key("best", course, team));
                    PlayerPrefs.DeleteKey(Key("splits", course, team));
                }

            PlayerPrefs.Save();
        }

        // Co-op keeps the key the best times have always had, so earlier records carry over.
        static string Key(string what, CourseDef course, bool teamRace) => $"rushahead.{what}.{course.name}{(teamRace ? ".team" : "")}";
    }
}
