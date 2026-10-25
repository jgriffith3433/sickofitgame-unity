using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>Yooba's new goods and the new Grudges (Docs/Story.md, "Yooba's new goods"): Level 1, loaded directly (a setup shortcut).</summary>
        public const string ShopGoodsId = "shop-goods";
        const int ShopGoodsLevel = 1;
        const float ShopGoodsSeconds = 300f;
        static readonly float[] ShopGoodsCurve = { 1f, 2.2f, 3.6f, 5.8f, 8.4f };

        sealed partial class Session
        {
            int shopGoodsPassed, shopGoodsFailed;

            void ShopGoodsCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    shopGoodsPassed++;
                    Run.Event("check", $"shop-goods {beat}: ok ({detail})");
                }
                else
                {
                    shopGoodsFailed++;
                    Run.Event("check", $"shop-goods {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"shop-goods {beat}: {detail}");
                }
            }

            public IEnumerator ShopGoodsRun()
            {
                var roguelite = RogueliteManager.Instance;
                if (roguelite == null || GameDefinition.Instance == null)
                {
                    Run.Problem(PlaytestSeverity.Bug, "shop-goods: no RogueliteManager or game definition");
                    End(PlaytestOutcome.Error);
                    yield break;
                }

                // 1. The counter: the new goods (they carry a card tag), their effects and their prices on the curve.
                var goods = RogueliteManager.ShopCatalog.Where(g => g != null && !string.IsNullOrEmpty(g.icon)).ToList();
                ShopGoodsCheck("at least 20 new goods", goods.Count >= 20, $"{goods.Count} goods with a card tag");
                int offCurve = 0;
                foreach (var g in goods)
                {
                    for (int i = 1; i < g.maxLevel && i < ShopGoodsCurve.Length; i++)
                    {
                        if (Mathf.Abs(g.costsPerLevel[i] - g.costsPerLevel[0] * ShopGoodsCurve[i]) > 5f)
                            offCurve++;
                    }
                    Run.Event("shop-good", $"{g.title} ({g.id}): {g.effect} {g.amountPerLevel} x {g.maxLevel}, {string.Join("/", g.costsPerLevel)} diamonds" +
                                           (g.requires != null && g.requires.Count > 0 ? $", needs {string.Join(" ", g.requires)}" : ""));
                }
                ShopGoodsCheck("prices follow the curve (1, 2.2, 3.6, 5.8, 8.4 of the first level)", offCurve == 0, $"{offCurve} level(s) off it");
                Run.Metric("shopGoods.count", goods.Count);
                Run.Metric("shop.totalPrice", RogueliteManager.TotalPrice(true));
                Run.Metric("shop.basePrice", RogueliteManager.TotalPrice(false));

                // 2. Buy every one through the shop's own purchase, gated goods after the goods they wait on (setup shortcut: the purse).
                roguelite.ResetAllRogueliteData();
                roguelite.AddPoints(50000);
                int bought = 0;
                for (int pass = 0; pass < 4; pass++)
                {
                    foreach (var g in goods)
                    {
                        if (!g.IsForSale(roguelite.GetUpgradeLevel(g.id)))
                            continue;
                        while (roguelite.TryPurchase(g))
                            bought++;
                    }
                }
                int expectedLevels = goods.Sum(g => g.maxLevel);
                ShopGoodsCheck("every level bought", bought == expectedLevels, $"{bought} of {expectedLevels} levels");

                // 3. A floor with them: the totals are in the hero's perks and the charges full.
                Run.Event("shortcut", $"level {ShopGoodsLevel} loaded directly with every new good bought");
                var load = LoadLevel(ShopGoodsLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => LevelRunning(ShopGoodsLevel) && GameplayWorld.Instance != null && GameplayWorld.Instance.Perks != null, 60f, "the level with the goods");
                if (Run.Done)
                    yield break;
                var world = GameplayWorld.Instance;
                var perks = world.Perks;
                foreach (var group in goods.Where(g => HeroPerks.IsPerk(g.effect)).GroupBy(g => g.effect))
                {
                    float expected = group.Sum(g => g.maxLevel * g.amountPerLevel);
                    ShopGoodsCheck($"{group.Key} total", Mathf.Abs(perks.Total(group.Key) - expected) < 0.01f, $"{perks.Total(group.Key):0.##} (expected {expected:0.##})");
                }
                ShopGoodsCheck("magnet reaches farther", world.PickupMagnetBonus >= 1.5f * 3f, $"+{world.PickupMagnetBonus:0.0}");
                ShopGoodsCheck("charges start full", perks.Shield == 5 && perks.Bombs == 2 && perks.Gasp == 2 && perks.Plates == 5, $"bubbles {perks.Shield}, bombs {perks.Bombs}, gasps {perks.Gasp}, plates {perks.Plates}");
                ShopGoodsCheck("enemy shots slower", perks.EnemyShotSpeed < 0.9f, $"x{perks.EnemyShotSpeed:0.00}");
                ShopGoodsCheck("rewind costs less", SickOfIt.Core.Rewind.RewindBudget.Instance != null && SickOfIt.Core.Rewind.RewindBudget.Instance.CostScale < 0.8f,
                    SickOfIt.Core.Rewind.RewindBudget.Instance != null ? $"x{SickOfIt.Core.Rewind.RewindBudget.Instance.CostScale:0.00}" : "no budget");

                // 4. The Grudges: each new one granted through the upgrade system (setup shortcut: the offer's pick), and its perk lands.
                var grudges = world.Content.Upgrades.Where(p => p.Value.perksPerLevel.Count > 0).OrderBy(p => p.Key, System.StringComparer.Ordinal).ToList();
                ShopGoodsCheck("at least 8 new Grudges", grudges.Count >= 8, $"{grudges.Count} Grudges with perks");
                foreach (var pair in grudges)
                {
                    var perk = pair.Value.perksPerLevel[0];
                    float before = perks.Total(perk.effect);
                    world.Upgrades.Grant(pair.Key, "playtest:" + pair.Key);
                    ShopGoodsCheck($"Grudge {pair.Value.title}", UpgradeProgress.LevelOf(pair.Key) == 1 && Mathf.Abs(perks.Total(perk.effect) - before - perk.amount) < 0.01f,
                        $"{perk.effect} {before:0.##} -> {perks.Total(perk.effect):0.##}");
                }

                // 5. Play with it all: the bot fights, and each perk that fires is logged from the telemetry.
                var seen = new Dictionary<string, int>();
                System.Action<GameplayTelemetry, GameplayTelemetry.Event> listen = (t, e) =>
                {
                    if (e.Kind != "perk")
                        return;
                    seen[e.Subject] = (seen.TryGetValue(e.Subject, out int n) ? n : 0) + 1;
                    Run.Event("perk", $"{e.Subject} ({e.Value}{(string.IsNullOrEmpty(e.Detail) ? "" : ", " + e.Detail)}) at {e.Time:0.0} s");
                };
                GameplayTelemetry.Recorded += listen;
                yield return PlayLevel(ShopGoodsLevel, 0f, now => null, 0f, 1f, 240f,
                    until: () => seen.ContainsKey("crit") && (seen.ContainsKey("bubble") || seen.ContainsKey("plate") || seen.ContainsKey("slip")) && seen.ContainsKey("fatDiamond"));
                GameplayTelemetry.Recorded -= listen;
                if (Run.Done)
                    yield break;
                ShopGoodsCheck("a crit seen", seen.ContainsKey("crit"), $"{Count(seen, "crit")} crit volleys");
                ShopGoodsCheck("a hit absorbed", seen.ContainsKey("bubble") || seen.ContainsKey("plate") || seen.ContainsKey("slip"),
                    $"bubble {Count(seen, "bubble")}, plate {Count(seen, "plate")}, slip {Count(seen, "slip")}");
                ShopGoodsCheck("a fat diamond seen", seen.ContainsKey("fatDiamond"), $"{Count(seen, "fatDiamond")} diamonds worth more");
                Run.Event("shop-goods", $"also seen: burp heals {Count(seen, "burpHeal")}, bombs {Count(seen, "bomb")}, gasps {Count(seen, "gasp")}, plates regrown {Count(seen, "plateRegrown")}");

                Run.Metric("shopGoods.passed", shopGoodsPassed);
                Run.Metric("shopGoods.failed", shopGoodsFailed);
                Run.Progress(1f);
                End(shopGoodsFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            static int Count(Dictionary<string, int> seen, string key) => seen.TryGetValue(key, out int n) ? n : 0;
        }
    }
}
