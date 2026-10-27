using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Tests.Editor
{
    /// <summary>The <c>requires</c> gate on shop goods and Grudges, the old-save guarantee and the shop's prices.</summary>
    public class ShopGateTests
    {
        const string FlagsKey = "UnlockFlags";
        const string GrudgeKey = "GameplayUpgrades";

        [SetUp]
        public void Clean()
        {
            PlayerPrefs.DeleteKey(FlagsKey);
            PlayerPrefs.DeleteKey("Rogue_Shop_spicyFireballs");
            PlayerPrefs.DeleteKey(GrudgeKey);
        }

        [TearDown]
        public void CleanUp() => Clean();

        [Test]
        public void NoRequirementsIsAlwaysMet()
        {
            Assert.IsTrue(Requirements.Met(null));
            Assert.IsTrue(Requirements.Met(new string[0]));
        }

        [Test]
        public void FlagAndWeaponWaitForTheirUnlock()
        {
            var needs = new[] { "weapon:fists", "flag:metYooba" };
            Assert.IsFalse(Requirements.Met(needs));
            UnlockFlags.Set("weapon:fists");
            Assert.IsFalse(Requirements.Met(needs), "every condition must hold");
            UnlockFlags.Set("flag-less");
            UnlockFlags.Set("metYooba");
            Assert.IsTrue(Requirements.Met(needs));
            UnlockFlags.Reset();
            Assert.IsFalse(Requirements.Met(needs));
        }

        [Test]
        public void GrudgeRequirementReadsTheRun()
        {
            Assert.IsFalse(Requirements.Met(new[] { "grudge:notToday" }));
            UpgradeProgress.Add("notToday");
            Assert.IsTrue(Requirements.Met(new[] { "grudge:notToday" }));
            Assert.IsFalse(Requirements.Met(new[] { "grudge:notToday:2" }));
        }

        [Test]
        public void MalformedRequirementsAreReportedAndNeverMet()
        {
            foreach (string bad in new[] { "", "weapons", "mood:good", "flag:", "level:x", "shop:a:b" })
            {
                Assert.IsNotNull(Requirements.Check(bad), bad);
                Assert.IsFalse(Requirements.Met(new[] { bad }), bad);
            }
            foreach (string good in new[] { "flag:a", "weapon:fists", "shop:spicyFireballs:2", "grudge:notToday", "level:3" })
                Assert.IsNull(Requirements.Check(good), good);
        }

        [Test]
        public void AGoodAlreadyOwnedIsNeverHidden()
        {
            var good = new UpgradeDefinition { id = "x", requires = { "flag:never" } };
            Assert.IsFalse(good.IsForSale(0));
            Assert.IsTrue(good.IsForSale(1), "a save that already bought it keeps it on the counter");
        }

        [Test]
        public void AnOldSaveHasNoFlagsAndKeepsItsPurchases()
        {
            PlayerPrefs.SetInt("Rogue_Shop_spicyFireballs", 2); // as a build before the gate wrote it
            Assert.AreEqual(0, UnlockFlags.All().Count);
            Assert.AreEqual(2, PlayerPrefs.GetInt("Rogue_Shop_spicyFireballs"));
        }

        [Test]
        public void ThePlayerJsonPricesAreTheDocumentedOnes()
        {
            string path = Path.Combine(Application.dataPath, "GameConfig", "player.json");
            // Yooba's new goods (they carry a card icon) are priced in ShopGoodsTests; these are the older ones.
            var goods = JObject.Parse(File.ReadAllText(path))["availableUpgrades"].Cast<JObject>().Where(g => g["icon"] == null).ToList();
            int Price(JObject g)
            {
                var costs = g["costsPerLevel"].Select(t => (int)t).ToArray();
                int max = (int)g["maxLevel"], sum = 0;
                for (int i = 0; i < max; i++)
                    sum += costs[System.Math.Min(i, costs.Length - 1)];
                return sum;
            }
            int all = goods.Sum(Price);
            int gated = goods.Where(g => g["requires"] != null && g["requires"].HasValues).Sum(Price);
            Assert.AreEqual(2686, all, "Docs/Story.md, \"Yooba's prices\": 2,686 diamonds for the older goods (Yooba's rewind goods, the weapon goods and the Slimed Lining included)");
            Assert.AreEqual(1696, all - gated, "1,696 without the gated goods");
            foreach (var g in goods)
                foreach (var r in g["requires"] ?? new JArray())
                    Assert.IsNull(Requirements.Check((string)r), (string)g["id"]);
        }
    }
}
