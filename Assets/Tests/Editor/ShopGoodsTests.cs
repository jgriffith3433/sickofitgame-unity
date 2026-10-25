using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Tests.Editor
{
    /// <summary>Yooba's new goods and the new Grudges (Docs/Story.md, "Yooba's new goods"): counts, effects, the price curve, the gate.</summary>
    public class ShopGoodsTests
    {
        // One level costs the first level's price times these, rounded to 5 (Goo Plaster's 25, 55, 90, 145, 210 are the curve's source).
        static readonly float[] Curve = { 1f, 2.2f, 3.6f, 5.8f, 8.4f };

        static System.Collections.Generic.List<JObject> NewGoods()
        {
            string path = Path.Combine(Application.dataPath, "GameConfig", "player.json");
            return JObject.Parse(File.ReadAllText(path))["availableUpgrades"].Cast<JObject>().Where(g => g["icon"] != null).ToList();
        }

        [Test]
        public void AtLeastTwentyNewGoodsEachWithAPerkEffect()
        {
            var goods = NewGoods();
            Assert.GreaterOrEqual(goods.Count, 20);
            foreach (var g in goods)
            {
                string id = (string)g["id"];
                Assert.IsTrue(System.Enum.TryParse((string)g["effect"], out ShopEffect effect), id);
                Assert.IsTrue(effect >= ShopEffect.Armor || effect == ShopEffect.Magnet, $"{id}: {effect} has no effect to see");
                Assert.Greater((float)g["amountPerLevel"], 0f, id);
                Assert.IsFalse(string.IsNullOrWhiteSpace((string)g["description"]), id);
                Assert.IsTrue(((string)g["icon"]).Length is >= 1 and <= 2, id + ": the card tag is one or two letters");
            }
            CollectionAssert.AllItemsAreUnique(goods.Select(g => (string)g["id"]));
            CollectionAssert.AllItemsAreUnique(goods.Select(g => (string)g["title"]));
        }

        [Test]
        public void NewGoodsFollowTheRetunedPriceCurve()
        {
            foreach (var g in NewGoods())
            {
                var costs = g["costsPerLevel"].Select(t => (int)t).ToArray();
                int max = (int)g["maxLevel"];
                Assert.AreEqual(max, costs.Length, (string)g["id"]);
                Assert.GreaterOrEqual(costs[0], 20, (string)g["id"]);
                Assert.LessOrEqual(costs[0], 130, (string)g["id"] + ": the first level stays under a floor or two of diamonds");
                for (int i = 0; i < max; i++)
                    Assert.AreEqual(costs[0] * Curve[i], costs[i], 5f, $"{g["id"]} level {i + 1}");
            }
        }

        [Test]
        public void NewGoodsGatesPointAtRealGoods()
        {
            string path = Path.Combine(Application.dataPath, "GameConfig", "player.json");
            var all = JObject.Parse(File.ReadAllText(path))["availableUpgrades"].Cast<JObject>().ToDictionary(g => (string)g["id"]);
            foreach (var g in NewGoods())
                foreach (var r in g["requires"] ?? new JArray())
                {
                    string requirement = (string)r;
                    Assert.IsNull(Requirements.Check(requirement), requirement);
                    var parts = requirement.Split(':');
                    if (parts[0] == "shop")
                    {
                        Assert.IsTrue(all.ContainsKey(parts[1]), requirement);
                        Assert.LessOrEqual(int.Parse(parts[2]), (int)all[parts[1]]["maxLevel"], requirement);
                    }
                }
        }

        [Test]
        public void AtLeastEightNewGrudgesEachWithAPerk()
        {
            string dir = Path.Combine(Application.dataPath, "GameConfig", "content", "upgrades");
            var withPerks = Directory.GetFiles(dir, "*.json").Select(f => JObject.Parse(File.ReadAllText(f))).Where(j => j["perksPerLevel"] != null).ToList();
            Assert.GreaterOrEqual(withPerks.Count, 8);
            foreach (var j in withPerks)
                foreach (var perk in j["perksPerLevel"])
                {
                    Assert.IsTrue(System.Enum.TryParse((string)perk["effect"], out ShopEffect effect), (string)j["title"]);
                    Assert.IsTrue(effect >= ShopEffect.Armor, (string)j["title"]);
                }
        }
    }
}
