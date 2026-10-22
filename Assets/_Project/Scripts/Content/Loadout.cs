using System;
using System.Text;

namespace RushAhead.Content
{
    /// <summary>
    /// One player's animal: species, fur colour and blower as <see cref="CrewCatalog"/> indices, plus the name shown above
    /// it. The same few bytes are saved on the device, sent once when they change, and shown in every player's roster;
    /// they never ride in a snapshot, because appearance doesn't change mid-frame.
    /// </summary>
    [Serializable]
    public struct Loadout : IEquatable<Loadout>
    {
        public const int MaxNameChars = 12;
        public const int MaxNameBytes = 24; // UTF-8, so a short name in any script fits

        public byte Species;
        public byte Fur;
        public byte Blower;
        public string Name;

        /// <summary>The same loadout with every index inside the catalog (anything unknown becomes the default) and a clean name.</summary>
        public Loadout ClampTo(int speciesCount, int furCount, int blowerCount) => new()
        {
            Species = Species < speciesCount ? Species : (byte)0,
            Fur = Fur < furCount ? Fur : (byte)0,
            Blower = Blower < blowerCount ? Blower : (byte)0,
            Name = CleanName(Name),
        };

        /// <summary>Printable characters only, trimmed, at most <see cref="MaxNameChars"/> characters and <see cref="MaxNameBytes"/> UTF-8 bytes.</summary>
        public static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            var clean = new StringBuilder(name.Length);
            foreach (var c in name)
                if (!char.IsControl(c)) clean.Append(c);

            var result = clean.ToString().Trim();
            while (result.Length > MaxNameChars || Encoding.UTF8.GetByteCount(result) > MaxNameBytes)
            {
                // Drop whole characters: never leave half of a surrogate pair (an emoji) behind.
                var cut = result.Length - 1;
                if (cut > 0 && char.IsLowSurrogate(result[cut]) && char.IsHighSurrogate(result[cut - 1])) cut--;
                result = result.Substring(0, cut).TrimEnd();
            }

            return result;
        }

        public bool Equals(Loadout other) =>
            Species == other.Species && Fur == other.Fur && Blower == other.Blower && (Name ?? "") == (other.Name ?? "");

        public override bool Equals(object obj) => obj is Loadout other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Species, Fur, Blower, Name ?? "");
        public override string ToString() => $"{Name} (species {Species}, fur {Fur}, blower {Blower})";
    }
}
