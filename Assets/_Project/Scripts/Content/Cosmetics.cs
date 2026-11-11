using System.Collections.Generic;

namespace RushAhead.Content
{
    /// <summary>What a player can buy with coins in the shop.</summary>
    public enum CosmeticKind : byte
    {
        Hat,
        Skin,
        Paint,
    }

    /// <summary>One thing in the shop: a hat, a raft skin or a blower paint, its price in coins and its stable id.</summary>
    public readonly struct Cosmetic
    {
        /// <summary>Stable across versions: what's owned is saved by id, so the shop can grow and reorder.</summary>
        public readonly string Id;
        public readonly string Name;
        public readonly int Price;

        public Cosmetic(string id, string name, int price)
        {
            Id = id;
            Name = name;
            Price = price;
        }
    }

    /// <summary>
    /// The shop's stock. Everything is made in code (hats from shapes, skins drawn on the tube, paints as finishes), so
    /// the lists live here rather than in assets. The first of each is free and always owned: no hat, the classic tube,
    /// the factory paint. A loadout holds an index into each list; anything past the end becomes the first.
    /// </summary>
    public static class Cosmetics
    {
        public static readonly Cosmetic[] Hats =
        {
            new("none", "No hat", 0),
            new("cap", "Cap", 50),
            new("beanie", "Beanie", 60),
            new("party", "Party hat", 80),
            new("chef", "Chef's hat", 100),
            new("flowers", "Flower crown", 120),
            new("headphones", "Headphones", 150),
            new("cowboy", "Cowboy hat", 180),
            new("propeller", "Propeller beanie", 200),
            new("pirate", "Pirate hat", 240),
            new("viking", "Viking helmet", 280),
            new("tophat", "Top hat", 320),
            new("crown", "Crown", 400),
        };

        public static readonly Cosmetic[] Skins =
        {
            new("classic", "Classic", 0),
            new("stripes", "Stripes", 60),
            new("dots", "Polka dots", 80),
            new("checker", "Checker", 100),
            new("stars", "Stars", 140),
            new("camo", "Camo", 180),
            new("shark", "Shark teeth", 240),
            new("flames", "Flames", 300),
            new("gold", "Gold", 400),
        };

        public static readonly Cosmetic[] Paints =
        {
            new("factory", "Factory", 0),
            new("red", "Fire red", 50),
            new("blue", "Ocean blue", 50),
            new("lime", "Lime", 50),
            new("bubblegum", "Bubblegum", 70),
            new("midnight", "Midnight", 90),
            new("camo", "Camo", 150),
            new("chrome", "Chrome", 220),
            new("gold", "Gold", 300),
            new("rainbow", "Rainbow", 350),
        };

        public static IReadOnlyList<Cosmetic> Of(CosmeticKind kind) => kind switch
        {
            CosmeticKind.Hat => Hats,
            CosmeticKind.Skin => Skins,
            _ => Paints,
        };

        /// <summary>The key it's saved under once bought: "hat.crown", "skin.flames", "paint.gold".</summary>
        public static string Key(CosmeticKind kind, int index)
        {
            var list = Of(kind);
            return index >= 0 && index < list.Count ? $"{kind.ToString().ToLowerInvariant()}.{list[index].Id}" : null;
        }

        public static int Price(CosmeticKind kind, int index)
        {
            var list = Of(kind);
            return index >= 0 && index < list.Count ? list[index].Price : 0;
        }
    }
}
