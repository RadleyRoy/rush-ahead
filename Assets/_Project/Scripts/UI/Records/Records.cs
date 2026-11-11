using System.Collections.Generic;
using System.Globalization;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>
    /// This device's best on each course, kept apart for Co-op and Team Race (two rafts sharing a river are slower): the
    /// most stars (lives left at the finish) and, separately, the fastest time with its time at each checkpoint so later
    /// runs can be compared as they go. Every round deals a new layout, so the time is a bragging number, not a target.
    /// </summary>
    public static class Records
    {
        /// <summary>The best time, or 0 if there isn't one yet.</summary>
        public static float BestTime(CourseDef course, bool teamRace) => PlayerPrefs.GetFloat(Key("best", course, teamRace), 0f);

        public static int BestStars(CourseDef course, bool teamRace) => PlayerPrefs.GetInt(Key("stars", course, teamRace), 0);

        /// <summary>A course's stars toward opening the next ones: the better of its Co-op and Team Race bests.</summary>
        public static int StarsOn(CourseDef course) => Mathf.Max(BestStars(course, false), BestStars(course, true));

        public static int TotalStars(IEnumerable<CourseDef> courses)
        {
            var total = 0;
            foreach (var course in courses) total += StarsOn(course);
            return total;
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

        /// <summary>Keeps this run's stars if they're the most yet, and its time if it's the fastest. Returns whether the time was.</summary>
        public static bool Submit(CourseDef course, bool teamRace, float seconds, IReadOnlyList<float> splits, int stars)
        {
            if (stars > BestStars(course, teamRace))
            {
                PlayerPrefs.SetInt(Key("stars", course, teamRace), stars);
                PlayerPrefs.Save();
            }

            var best = BestTime(course, teamRace);
            if (best > 0f && seconds >= best) return false;

            PlayerPrefs.SetFloat(Key("best", course, teamRace), seconds);
            var text = new string[splits.Count];
            for (var i = 0; i < text.Length; i++) text[i] = splits[i].ToString("0.###", CultureInfo.InvariantCulture);
            PlayerPrefs.SetString(Key("splits", course, teamRace), string.Join(",", text));
            PlayerPrefs.Save();
            return true;
        }

        const string CoinsKey = "rushahead.coins";
        const string OwnedKey = "rushahead.owned";
        static HashSet<string> owned;

        /// <summary>This player's coins, to spend in the shop.</summary>
        public static int Coins => Mathf.Max(0, PlayerPrefs.GetInt(CoinsKey, 0));

        public static void AddCoins(int coins)
        {
            if (coins == 0) return;
            PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, Coins + coins));
            PlayerPrefs.Save();
        }

        /// <summary>Whether this player owns a shop item. The first of each kind (no hat, the classic tube, the factory paint) is free.</summary>
        public static bool Owns(CosmeticKind kind, int index) =>
            index == 0 || (Cosmetics.Key(kind, index) is { } key && OwnedSet().Contains(key));

        /// <summary>Buys a shop item if it isn't owned yet and there are coins enough. Returns whether it's now owned.</summary>
        public static bool Buy(CosmeticKind kind, int index)
        {
            if (Owns(kind, index)) return true;
            var key = Cosmetics.Key(kind, index);
            var price = Cosmetics.Price(kind, index);
            if (key == null || Coins < price) return false;
            PlayerPrefs.SetInt(CoinsKey, Coins - price);
            OwnedSet().Add(key);
            SaveOwned();
            return true;
        }

        /// <summary>Developer tools: everything in the shop.</summary>
        public static void OwnEverything()
        {
            foreach (CosmeticKind kind in System.Enum.GetValues(typeof(CosmeticKind)))
                for (var i = 1; i < Cosmetics.Of(kind).Count; i++)
                    OwnedSet().Add(Cosmetics.Key(kind, i));
            SaveOwned();
        }

        /// <summary>For tests that put PlayerPrefs back: forget what's been read from them.</summary>
        internal static void Reload()
        {
            owned = null;
            seen = null;
        }

        static void SaveOwned()
        {
            PlayerPrefs.SetString(OwnedKey, string.Join(",", owned));
            PlayerPrefs.Save();
        }

        static HashSet<string> OwnedSet()
        {
            if (owned != null) return owned;
            owned = new HashSet<string>();
            foreach (var key in PlayerPrefs.GetString(OwnedKey, "").Split(','))
                if (key.Length > 0) owned.Add(key);
            return owned;
        }

        const string SeenKey = "guide.seen";
        static HashSet<string> seen;

        /// <summary>Whether this player has met this Field Guide entry on a river yet.</summary>
        public static bool Seen(string id) => SeenSet().Contains(id);

        /// <summary>Notes a Field Guide entry as met; true the first time.</summary>
        public static bool MarkSeen(string id)
        {
            if (string.IsNullOrEmpty(id) || !SeenSet().Add(id)) return false;
            PlayerPrefs.SetString(SeenKey, string.Join(",", seen));
            PlayerPrefs.Save();
            return true;
        }

        public static int SeenCount => SeenSet().Count;

        static HashSet<string> SeenSet()
        {
            if (seen != null) return seen;
            seen = new HashSet<string>();
            foreach (var id in PlayerPrefs.GetString(SeenKey, "").Split(','))
                if (id.Length > 0) seen.Add(id);
            return seen;
        }

        /// <summary>Forgets every best time on this device.</summary>
        public static void Clear(IEnumerable<CourseDef> courses)
        {
            foreach (var course in courses)
                foreach (var team in new[] { false, true })
                {
                    PlayerPrefs.DeleteKey(Key("best", course, team));
                    PlayerPrefs.DeleteKey(Key("stars", course, team));
                    PlayerPrefs.DeleteKey(Key("splits", course, team));
                }

            PlayerPrefs.Save();
        }

        // Co-op keeps the key the best times have always had, so earlier records carry over.
        static string Key(string what, CourseDef course, bool teamRace) => $"rushahead.{what}.{course.name}{(teamRace ? ".team" : "")}";
    }
}
