using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core.Progression;
using SickOfIt.Data;
using Object = UnityEngine.Object;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>Yooba's weapon goods (Docs/Story.md, "Weapon goods"): hidden until the weapon is unlocked, Keep Weapons, his accusation.</summary>
        public const string ShopWeaponsId = "shop-weapons";

        sealed partial class Session
        {
            static readonly string[] WeaponGoods = { "fistGoo", "fistReach", "keepWeapons" };

            /// <summary>
            /// Setup shortcuts: no weapon unlock flag, 1,000 diamonds, Keep Weapons not owned. Then with real clicks: the shop hides
            /// every weapon good; the unlock is set (what the first fists pick-up does); the goods appear; Keep Weapons is bought and Yooba
            /// accuses the hero of stealing; a run ends (EndRun) with Keep Weapons and the fists are kept; with the good gone (a save
            /// that never bought it) the same run end clears them.
            /// </summary>
            public IEnumerator ShopWeapons()
            {
                var roguelite = RogueliteManager.Instance;
                var definition = GameDefinition.Instance;
                int failed = 0;
                void Gate(string beat, bool ok, string detail)
                {
                    if (ok) { Run.Event("check", $"shop-weapons {beat}: ok ({detail})"); return; }
                    failed++;
                    Run.Event("check", $"shop-weapons {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"shop-weapons {beat}: {detail}");
                }

                yield return ToMainMenu();
                if (Run.Done) yield break;

                Run.Event("shortcut", "no weapon unlocked, nothing of Yooba's weapon goods owned, 1000 diamonds");
                UnlockFlags.Reset();
                WeaponProgress.Reset();
                foreach (string id in WeaponGoods)
                    PlayerPrefs.DeleteKey("Rogue_Shop_" + id);
                roguelite.SetPoints(1000);
                yield return ClickUntil(definition.mainMenuConfig.labels.shop, () => Object.FindAnyObjectByType<UpgradeShopManager>() != null, 20f);
                if (Run.Done) yield break;
                Run.Scene("Shop (no weapon)");
                var shop = Object.FindAnyObjectByType<UpgradeShopManager>();
                yield return WaitReal(2f);
                if (Run.Done || shop == null) yield break;

                bool anyShown = false;
                foreach (string id in WeaponGoods)
                    anyShown |= shop.ShownIds().Contains(id) || shop.CardButton(id) != null;
                Gate("hiddenWithoutWeapon", !anyShown, $"on the counter: {string.Join(", ", shop.ShownIds())}");
                yield return Audit("Shop (weapon goods hidden)", 0, 2, 1f, progress: false);
                if (Run.Done) yield break;

                // Leave and come back: the unlock is what the first pick-up of the fists writes (PlayerWeapons.Give).
                yield return ClickUntil(definition.upgradeShopConfig.layout.backText, () => Object.FindAnyObjectByType<MainMenuManager>() != null, 20f);
                if (Run.Done) yield break;
                Run.Event("shortcut", "the fists unlocked (weapon:fists), as the first pick-up does");
                UnlockFlags.Set("weapon:fists");
                yield return ClickUntil(definition.mainMenuConfig.labels.shop, () => Object.FindAnyObjectByType<UpgradeShopManager>() != null, 20f);
                if (Run.Done) yield break;
                Run.Scene("Shop (fists unlocked)");
                shop = Object.FindAnyObjectByType<UpgradeShopManager>();
                yield return WaitReal(2f);
                if (Run.Done || shop == null) yield break;
                bool allShown = true;
                foreach (string id in WeaponGoods)
                    allShown &= shop.ShownIds().Contains(id) && shop.CardButton(id) != null;
                Gate("shownWithWeapon", allShown, $"on the counter: {string.Join(", ", shop.ShownIds())}");
                yield return Audit("Shop (weapon goods shown)", 1, 2, 1f, progress: false);
                if (Run.Done) yield break;

                int buy0 = yoobaLines.Count;
                yield return ClickButton(shop.CardButton("keepWeapons"), "Keep Weapons");
                yield return Run.Until(() => yoobaLines.Count > buy0 && (shop.Yooba == null || !shop.Yooba.Speaking), 25f, "Yooba's accusation");
                if (Run.Done) yield break;
                Gate("bought", roguelite.GetUpgradeLevel("keepWeapons") == 1, $"Keep Weapons level {roguelite.GetUpgradeLevel("keepWeapons")}");
                Gate("yoobaAccuses", ShopLineSince(buy0, "ShopBuyWeapon"), FirstSince(buy0));
                yield return WaitReal(1f);

                int buy1 = yoobaLines.Count;
                yield return ClickButton(shop.CardButton("fistGoo"), "Slimed Knuckles");
                yield return Run.Until(() => yoobaLines.Count > buy1 && (shop.Yooba == null || !shop.Yooba.Speaking), 25f, "Yooba's second accusation");
                if (Run.Done) yield break;
                Gate("yoobaAccusesUpgrade", ShopLineSince(buy1, "ShopBuyWeapon"), FirstSince(buy1));
                yield return ClickUntil(definition.upgradeShopConfig.layout.backText, () => Object.FindAnyObjectByType<MainMenuManager>() != null, 20f);
                if (Run.Done) yield break;
                Run.Progress(0.6f);

                // A run ends (setup shortcut: the same call RunFlow makes when a death sends him to a Waystone).
                var held = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("fists", 0) };
                WeaponProgress.Store(held, "fists", true);
                Run.Event("shortcut", "a run ends (RogueliteManager.EndRun) with the fists in his hands and Keep Weapons bought");
                roguelite.EndRun();
                Gate("keptWithKeepWeapons", WeaponProgress.Has("fists") && WeaponProgress.Equipped() == "fists", $"fists held: {WeaponProgress.Has("fists")}, equipped \"{WeaponProgress.Equipped()}\"");

                PlayerPrefs.DeleteKey("Rogue_Shop_keepWeapons");
                WeaponProgress.Store(held, "fists", true);
                Run.Event("shortcut", "Keep Weapons not bought; a run ends with the fists in his hands");
                roguelite.EndRun();
                Gate("lostWithout", !WeaponProgress.Has("fists"), $"fists held: {WeaponProgress.Has("fists")}");
                Gate("unlockStays", UnlockFlags.Has("weapon:fists"), "the shop still knows the fists (the flag outlives the run)");

                foreach (string id in WeaponGoods)
                    PlayerPrefs.DeleteKey("Rogue_Shop_" + id);
                UnlockFlags.Reset();
                WeaponProgress.Reset();
                Run.Progress(1f);
                End(failed == 0 ? PlaytestOutcome.Won : PlaytestOutcome.Done);
            }
        }
    }
}
