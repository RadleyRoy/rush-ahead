using System.Linq;
using NUnit.Framework;
using RushAhead.Content;
using RushAhead.UI;
using UnityEngine;

namespace RushAhead.Tests
{
    /// <summary>Coins from results, buying from the shop, and what a loadout carries for it.</summary>
    public class ShopTests
    {
        const string CoinsKey = "rushahead.coins";
        const string OwnedKey = "rushahead.owned";
        int savedCoins;
        string savedOwned;
        bool hadCoins, hadOwned;

        [SetUp]
        public void SetUp()
        {
            hadCoins = PlayerPrefs.HasKey(CoinsKey);
            hadOwned = PlayerPrefs.HasKey(OwnedKey);
            savedCoins = PlayerPrefs.GetInt(CoinsKey, 0);
            savedOwned = PlayerPrefs.GetString(OwnedKey, "");
            PlayerPrefs.DeleteKey(CoinsKey);
            PlayerPrefs.DeleteKey(OwnedKey);
            Records.Reload();
        }

        [TearDown]
        public void TearDown()
        {
            if (hadCoins) PlayerPrefs.SetInt(CoinsKey, savedCoins);
            else PlayerPrefs.DeleteKey(CoinsKey);
            if (hadOwned) PlayerPrefs.SetString(OwnedKey, savedOwned);
            else PlayerPrefs.DeleteKey(OwnedKey);
            PlayerPrefs.Save();
            Records.Reload();
        }

        [Test]
        public void Coins_ComeFromStarsWinsPlacesBestsAndEscapingSnapjaw()
        {
            Assert.That(CoinRules.Total(CoinRules.For(3, false, 0, false, false)), Is.EqualTo(30), "ten a star");
            Assert.That(CoinRules.Total(CoinRules.For(2, true, 0, false, false)), Is.EqualTo(35), "and fifteen for a Team Race win");
            Assert.That(CoinRules.Total(CoinRules.For(0, false, 1, false, false)), Is.EqualTo(40), "an Elimination win");
            Assert.That(CoinRules.Total(CoinRules.For(0, false, 2, false, false)), Is.EqualTo(20));
            Assert.That(CoinRules.Total(CoinRules.For(0, false, 3, false, false)), Is.EqualTo(10));
            Assert.That(CoinRules.Total(CoinRules.For(0, false, 4, false, false)), Is.Zero, "fourth gets nothing");
            Assert.That(CoinRules.Total(CoinRules.For(1, false, 0, true, true)), Is.EqualTo(10 + 20 + 50), "a new best, and escaping Snapjaw");
            Assert.That(CoinRules.For(0, false, 0, false, false), Is.Empty, "out of lives: nothing");
            Assert.That(CoinRules.For(3, false, 0, true, false).Select(a => a.Why), Is.EqualTo(new[] { "3 stars", "New best" }));
        }

        [Test]
        public void Buying_SpendsCoins_OnlyWhenThereAreEnough_AndOnlyOnce()
        {
            var crown = System.Array.FindIndex(Cosmetics.Hats, h => h.Id == "crown");
            Assert.That(Records.Owns(CosmeticKind.Hat, 0), Is.True, "no hat is free");
            Assert.That(Records.Owns(CosmeticKind.Hat, crown), Is.False);
            Records.AddCoins(Cosmetics.Hats[crown].Price - 1);
            Assert.That(Records.Buy(CosmeticKind.Hat, crown), Is.False, "a coin short");
            Assert.That(Records.Coins, Is.EqualTo(Cosmetics.Hats[crown].Price - 1), "and nothing spent");

            Records.AddCoins(51);
            Assert.That(Records.Buy(CosmeticKind.Hat, crown), Is.True);
            Assert.That(Records.Coins, Is.EqualTo(50));
            Assert.That(Records.Buy(CosmeticKind.Hat, crown), Is.True, "already owned");
            Assert.That(Records.Coins, Is.EqualTo(50), "not charged twice");

            Records.Reload();
            Assert.That(Records.Owns(CosmeticKind.Hat, crown), Is.True, "kept on the device");
            Assert.That(Records.Owns(CosmeticKind.Skin, crown), Is.False, "a crown, not a skin");
            Records.AddCoins(-1000);
            Assert.That(Records.Coins, Is.Zero, "never below none");
        }

        [Test]
        public void TheShop_HasFreeDefaults_UniqueIds_AndPrices()
        {
            foreach (var kind in new[] { CosmeticKind.Hat, CosmeticKind.Skin, CosmeticKind.Paint })
            {
                var items = Cosmetics.Of(kind);
                Assert.That(items[0].Price, Is.Zero, $"the first {kind} is free");
                Assert.That(items.Select(i => i.Id).Distinct().Count(), Is.EqualTo(items.Count), $"{kind} ids are unique");
                for (var i = 1; i < items.Count; i++) Assert.That(items[i].Price, Is.InRange(50, 400), items[i].Name);
            }

            Assert.That(Cosmetics.Hats.Length - 1, Is.EqualTo(12), "twelve hats");
            Assert.That(Cosmetics.Skins.Length - 1, Is.EqualTo(8), "eight raft skins");
            Assert.That(Cosmetics.Paints.Length, Is.EqualTo(10), "ten paints, the factory finish among them");
        }

        [Test]
        public void ALoadout_KeepsItsShopItems_WithinTheCatalog()
        {
            var look = new Loadout { Species = 1, Fur = 2, Blower = 1, Hat = 3, Skin = 2, Paint = 5, Name = "Skipper" }.ClampTo(6, 12, 5);
            Assert.That((look.Hat, look.Skin, look.Paint), Is.EqualTo(((byte)3, (byte)2, (byte)5)));
            var newer = new Loadout { Hat = 200, Skin = 200, Paint = 200, Name = "Skipper" }.ClampTo(6, 12, 5);
            Assert.That((newer.Hat, newer.Skin, newer.Paint), Is.EqualTo(((byte)0, (byte)0, (byte)0)), "a newer device's items become the defaults");
            Assert.That(look.Wearing(CosmeticKind.Skin, 7).Skin, Is.EqualTo(7));
            Assert.That(look.Wearing(CosmeticKind.Skin, 7).Worn(CosmeticKind.Skin), Is.EqualTo(7));
            Assert.That(look.Equals(look.Wearing(CosmeticKind.Hat, 1)), Is.False, "a new hat is a new look");
        }

        [Test]
        public void SavedLooks_ReadBack_AndOldSavesStillLoad()
        {
            var look = new Loadout { Species = 4, Fur = 7, Blower = 2, Hat = 11, Skin = 6, Paint = 9, Name = "Bob, otter" };
            Assert.That(LocalCrew.Parse(LocalCrew.Format(look)), Is.EqualTo(look));
            var old = LocalCrew.Parse("3,5,1,Old, friend");
            Assert.That((old.Species, old.Fur, old.Blower, old.Name), Is.EqualTo(((byte)3, (byte)5, (byte)1, "Old, friend")));
            Assert.That((old.Hat, old.Skin, old.Paint), Is.EqualTo(((byte)0, (byte)0, (byte)0)), "no shop items before the shop");
        }
    }
}
