using System;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>This device's settings, saved as one JSON string. Bad or missing saves fall back to the defaults.</summary>
    [Serializable]
    public sealed class GameSettings
    {
        const string Key = "rushahead.settings.v1";

        /// <summary>The stick points where the air goes, not where the raft goes (expert).</summary>
        public bool directNozzleAim;

        /// <summary>The stick on the right, the buttons on the left.</summary>
        public bool leftHanded;

        /// <summary>30 saves battery; 60 is smoother.</summary>
        public int frameRate = 60;

        /// <summary>-1 keeps the platform's default; otherwise a quality level (1 Low, 2 Medium, 3 High).</summary>
        public int quality = -1;

        /// <summary>The live tuning sliders and the debug readout.</summary>
        public bool developerTools;

        public static GameSettings Load()
        {
            try
            {
                var saved = PlayerPrefs.GetString(Key, "");
                if (saved.Length > 0) return Sanitised(JsonUtility.FromJson<GameSettings>(saved));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RushAhead] Ignoring unreadable settings: {e.Message}");
            }

            return new GameSettings();
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        /// <summary>Puts the settings into effect.</summary>
        public void Apply(CrewInputRouter router)
        {
            Application.targetFrameRate = frameRate;
            if (quality >= 0 && quality < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != quality)
                QualitySettings.SetQualityLevel(quality, true);
            router.DirectNozzleAim = directNozzleAim;
            router.Touch.LeftHanded = leftHanded;
        }

        static GameSettings Sanitised(GameSettings s)
        {
            s ??= new GameSettings();
            if (s.frameRate != 30) s.frameRate = 60;
            s.quality = Mathf.Clamp(s.quality, -1, 5);
            return s;
        }
    }
}
