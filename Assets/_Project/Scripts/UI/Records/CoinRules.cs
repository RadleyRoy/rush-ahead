using System.Collections.Generic;

namespace RushAhead.UI
{
    /// <summary>One reason for coins on the results screen, and how many.</summary>
    public readonly struct CoinAward
    {
        public readonly string Why;
        public readonly int Coins;

        public CoinAward(string why, int coins)
        {
            Why = why;
            Coins = coins;
        }

        public override string ToString() => $"{Why} +{Coins}";
    }

    /// <summary>
    /// Coins come from results only, each device paying its own player: ten a star (a life kept at the finish), a bonus
    /// for winning a Team Race, for a place on the Elimination podium, for a new best time, and for escaping Old
    /// Snapjaw. Nothing to pick up on the river.
    /// </summary>
    public static class CoinRules
    {
        public const int PerStar = 10;
        public const int TeamRaceWin = 15;
        public const int NewBest = 20;
        public const int Escaped = 50;

        /// <summary>Elimination: first, second and third.</summary>
        public static readonly int[] ByPlace = { 40, 20, 10 };

        /// <param name="place">An Elimination's place, from 1; 0 for the other modes.</param>
        public static List<CoinAward> For(int stars, bool teamRaceWin, int place, bool newBest, bool escapedBoss)
        {
            var awards = new List<CoinAward>();
            if (stars > 0) awards.Add(new CoinAward(stars == 1 ? "1 star" : $"{stars} stars", stars * PerStar));
            if (teamRaceWin) awards.Add(new CoinAward("Team Race win", TeamRaceWin));
            if (place >= 1 && place <= ByPlace.Length) awards.Add(new CoinAward($"{Ordinal(place)} place", ByPlace[place - 1]));
            if (newBest) awards.Add(new CoinAward("New best", NewBest));
            if (escapedBoss) awards.Add(new CoinAward("Escaped Snapjaw", Escaped));
            return awards;
        }

        public static int Total(IReadOnlyList<CoinAward> awards)
        {
            var total = 0;
            if (awards != null)
                foreach (var award in awards) total += award.Coins;
            return total;
        }

        public static string Ordinal(int n) => n switch
        {
            1 => "1st",
            2 => "2nd",
            3 => "3rd",
            _ => $"{n}th",
        };
    }
}
