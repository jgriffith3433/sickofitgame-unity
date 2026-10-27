using System.Collections;
using System.Linq;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core.Progression;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>Yooba's health line (Docs/Story.md, "Yooba's prices", 2026-10-27): ten Goo Plasters for ~430 diamonds take 5 hearts to 25, and the HUD bar stays readable.</summary>
        public const string ShopBalanceId = "shop-balance";

        sealed partial class Session
        {
            /// <summary>
            /// Setup shortcut: a fresh save with exactly the line's price in the purse. Then with real clicks the ten Goo Plasters are bought in Yooba's shop
            /// (every click must be affordable, the purse ends at 0), Level 1 starts with 25 hearts, and the health bar is checked: one smooth bar
            /// (not 25 slivers), wide enough, with its count. Run it on a 1920x1080 and a 390x844 device. Prices of the other goods are reported.
            /// </summary>
            public IEnumerator ShopBalance()
            {
                var roguelite = RogueliteManager.Instance;
                var definition = GameDefinition.Instance;
                int failed = 0;
                void Gate(string beat, bool ok, string detail)
                {
                    if (ok) { Run.Event("check", $"shop-balance {beat}: ok ({detail})"); return; }
                    failed++;
                    Run.Event("check", $"shop-balance {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"shop-balance {beat}: {detail}");
                }

                var plaster = RogueliteManager.ShopCatalog.FirstOrDefault(g => g != null && g.Effect == SickOfIt.Data.Config.ShopEffect.MaxHealth);
                if (plaster == null) { Run.Problem(PlaytestSeverity.Bug, "shop-balance: no health good in the catalog"); End(PlaytestOutcome.Error); yield break; }
                int line = 0;
                for (int i = 0; i < plaster.maxLevel; i++) line += plaster.costsPerLevel[System.Math.Min(i, plaster.costsPerLevel.Length - 1)];
                int hearts = definition.player.baseMaxHealth + plaster.maxLevel * Mathf.RoundToInt(plaster.amountPerLevel);
                Gate("line price", line >= 400 && line <= 460, $"{plaster.title}: {plaster.maxLevel} levels, {string.Join("/", plaster.costsPerLevel)} = {line} diamonds");
                Gate("line cap", hearts == 25, $"{definition.player.baseMaxHealth} hearts + {plaster.maxLevel} x {plaster.amountPerLevel} = {hearts}");
                Run.Metric("shopBalance.healthLinePrice", line);
                Run.Metric("shopBalance.healthLineHearts", hearts);
                foreach (var g in RogueliteManager.ShopCatalog.Where(g => g != null && g != plaster && string.IsNullOrEmpty(g.icon)))
                    Run.Event("shop-good", $"{g.title} ({g.id}): {g.effect} {g.amountPerLevel} x {g.maxLevel}, {string.Join("/", g.costsPerLevel)} diamonds");

                yield return ToMainMenu();
                if (Run.Done) yield break;
                Run.Event("shortcut", $"a fresh save with {line} diamonds");
                roguelite.ResetAllRogueliteData();
                roguelite.SetPoints(line);
                yield return ClickUntil(definition.mainMenuConfig.labels.shop, () => Object.FindAnyObjectByType<UpgradeShopManager>() != null, 20f);
                if (Run.Done) yield break;
                Run.Scene("Shop (Goo Plaster)");
                var shop = Object.FindAnyObjectByType<UpgradeShopManager>();
                yield return WaitReal(2f);
                if (Run.Done || shop == null) yield break;

                for (int level = 0; level < plaster.maxLevel; level++)
                {
                    int before = roguelite.GetUpgradeLevel(plaster.id);
                    yield return ClickButton(shop.CardButton(plaster.id), $"Goo Plaster {level + 1}");
                    yield return WaitReal(0.6f);
                    if (Run.Done) yield break;
                    Gate($"plaster {level + 1}", roguelite.GetUpgradeLevel(plaster.id) == before + 1, $"level {roguelite.GetUpgradeLevel(plaster.id)}, {roguelite.GetPoints()} diamonds left");
                }
                Gate("spent exactly", roguelite.GetPoints() == 0, $"{roguelite.GetPoints()} diamonds left");
                Gate("max health", roguelite.GetMaxHealth() == hearts, $"max health {roguelite.GetMaxHealth()}");
                yield return Audit("Shop (Goo Plaster maxed)", 0, 1, 1f, progress: false);
                if (Run.Done) yield break;

                Run.Event("shortcut", "Level 1 loaded directly");
                var load = LoadLevel(1);
                if (load != null) yield return load;
                yield return Run.Until(() => LevelRunning(1) && GameplayWorld.Instance != null && GameplayWorld.Instance.Player != null, 60f, "Level 1 with the full line");
                if (Run.Done) yield break;
                yield return WaitReal(2f);
                Gate("hero hearts", GameplayWorld.Instance.Player.MaxHealth == hearts, $"{GameplayWorld.Instance.Player.MaxHealth} hearts in the level");

                var bar = Object.FindObjectsByType<MenuBar>(FindObjectsSortMode.None).FirstOrDefault(b => b.transform.parent != null && b.transform.parent.parent != null && b.transform.parent.parent.name == "PlayerHealth");
                if (bar == null) { Gate("health bar", false, "no health bar in the HUD"); }
                else
                {
                    var corners = new Vector3[4];
                    ((RectTransform)bar.transform).GetWorldCorners(corners);
                    float width = Mathf.Abs(corners[2].x - corners[0].x), height = Mathf.Abs(corners[2].y - corners[0].y);
                    float shortSide = Mathf.Min(Screen.width, Screen.height);
                    Run.Event("hud", $"health bar {width:0} x {height:0} px on {Screen.width}x{Screen.height} ({width / Screen.width * 100f:0}% of the width)");
                    Gate("bar smooth", !bar.Segmented, $"{hearts} hearts as {(bar.Segmented ? "slivers" : "one smooth bar")}");
                    Gate("bar readable", width >= shortSide * 0.25f && height >= shortSide * 0.015f, $"{width:0} x {height:0} px against a {shortSide:0} px short side");
                }
                yield return Audit("Level 1 HUD (25 hearts)", 0, 1, 1f, progress: false);
                if (Run.Done) yield break;

                Run.Metric("shopBalance.failed", failed);
                Run.Progress(1f);
                End(failed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }
        }
    }
}
