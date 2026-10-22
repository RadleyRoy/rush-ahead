using System;
using System.Globalization;
using System.Text;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>
    /// The animals this device brings to a raft: its player's own, which travels with them when they join someone else,
    /// what each seat wears while it's a spare (an AI crewmate, or this device's other controls), the AI setting, and
    /// the device's own id. Saved between sessions.
    /// </summary>
    public sealed class LocalCrew
    {
        const string Key = "rushahead.crew.v1";
        const string DeviceKey = "rushahead.device.v1";
        const string AiKey = "rushahead.ai.v1";

        static readonly string[] StarterNames =
        {
            "Skipper", "Puddles", "Breezy", "Gusty", "Splash", "Bubbles", "Paddles", "Whirly", "Squall", "Drizzle", "Ripple", "Zephyr",
        };

        public Loadout Profile;
        /// <summary>Per seat, numbered across both Team Race rafts.</summary>
        public readonly Loadout[] Spares = new Loadout[RushAhead.Net.CrewRoster.MaxSeats];

        /// <summary>A random id this device keeps forever, so a host recognises its player coming back after a dropout.</summary>
        public ulong DeviceId { get; private set; }

        /// <summary>AI crewmates in spare seats: -1 off, 0 Clumsy, 1 Keen (the default), 2 Ace.</summary>
        public int AiSetting = 1;

        /// <summary>The saved crew, or a fresh one: a bear with a random starter name, and a different animal in each spare seat.</summary>
        public static LocalCrew Load(CrewCatalog catalog)
        {
            var crew = new LocalCrew { Profile = catalog.DefaultLoadout(0) };
            crew.Profile.Name = StarterNames[UnityEngine.Random.Range(0, StarterNames.Length)];
            for (var seat = 0; seat < crew.Spares.Length; seat++) crew.Spares[seat] = catalog.DefaultLoadout(seat + 1);

            try
            {
                var lines = PlayerPrefs.GetString(Key, "").Split('\n');
                if (lines.Length >= 2) // older saves have fewer spare seats (one raft); the rest keep their defaults
                {
                    crew.Profile = catalog.Clamp(Parse(lines[0]));
                    for (var seat = 0; seat < crew.Spares.Length && seat + 1 < lines.Length; seat++) crew.Spares[seat] = catalog.Clamp(Parse(lines[seat + 1]));
                }
            }
            catch (Exception e) when (e is FormatException or OverflowException)
            {
                // A save from some other build: start fresh rather than fail.
            }

            crew.AiSetting = Mathf.Clamp(PlayerPrefs.GetInt(AiKey, 1), -1, 2);
            if (!ulong.TryParse(PlayerPrefs.GetString(DeviceKey, ""), NumberStyles.None, CultureInfo.InvariantCulture, out var device) || device == 0)
            {
                device = ((ulong)(uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue) << 32) | (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                if (device == 0) device = 1;
                PlayerPrefs.SetString(DeviceKey, device.ToString(CultureInfo.InvariantCulture));
                PlayerPrefs.Save();
            }

            crew.DeviceId = device;
            return crew;
        }

        public void Save()
        {
            var text = new StringBuilder(Format(Profile));
            foreach (var spare in Spares) text.Append('\n').Append(Format(spare));
            PlayerPrefs.SetString(Key, text.ToString());
            PlayerPrefs.SetInt(AiKey, AiSetting);
            PlayerPrefs.Save();
        }

        // "species,fur,blower,name": the name goes last because it may itself contain commas. Names never contain newlines.
        static string Format(Loadout l) => FormattableString.Invariant($"{l.Species},{l.Fur},{l.Blower},{Loadout.CleanName(l.Name)}");

        static Loadout Parse(string line)
        {
            var parts = line.Split(new[] { ',' }, 4);
            if (parts.Length != 4) throw new FormatException(line);
            return new Loadout
            {
                Species = byte.Parse(parts[0], CultureInfo.InvariantCulture),
                Fur = byte.Parse(parts[1], CultureInfo.InvariantCulture),
                Blower = byte.Parse(parts[2], CultureInfo.InvariantCulture),
                Name = parts[3],
            };
        }
    }
}
