using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using TMPro;
using UnityEngine;
using SickOfIt.Core.Progression;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using Object = UnityEngine.Object;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>The staged main menu and Yooba's tabbed counter (Docs/Story.md "The menu only shows what you have met").</summary>
        public const string StagedMenuId = "staged-menu";

        sealed partial class Session
        {
            /// <summary>This scenario checks the staging itself: no setup shortcut may reveal buttons behind its back.</summary>
            bool strictMenu;

            /// <summary>The first-run button of a brand-new save is Play (it starts the New Game); every other scenario's "New Game" click goes through it.</summary>
            static string NewGameLabel(MainMenuLabels labels) => MenuReveal.IsFirstRun && !string.IsNullOrEmpty(labels.play) ? labels.play : labels.newGame;

            string StagedMenuLabel(string text)
            {
                if (strictMenu || string.IsNullOrEmpty(text) || GameDefinition.Instance == null)
                    return text;
                var labels = GameDefinition.Instance.mainMenuConfig.labels;
                return text == labels.newGame ? NewGameLabel(labels) : text;
            }

            /// <summary>
            /// Scenarios written before the menu was staged click Visit Yooba, Pick a Level and the rest on a fresh save: a named setup
            /// shortcut gives the save what it needs (everything unlocked and seen, the menu built again) the first time one is missing.
            /// </summary>
            void RevealStagedButton(string text)
            {
                if (strictMenu || string.IsNullOrEmpty(text))
                    return;
                var menu = Object.FindAnyObjectByType<MainMenuManager>();
                if (menu == null)
                    return;
                var gd = GameDefinition.Instance;
                var labels = gd.mainMenuConfig.labels;
                var gated = new List<string> { labels.levelSelect, labels.videos, labels.shop };
                if (gd.codex != null)
                    gated.Add(gd.codex.buttonLabel);
                if (gd.replay != null && gd.replay.labels != null)
                    gated.AddRange(new[] { gd.replay.labels.endless, gd.replay.labels.daily, gd.replay.labels.newGamePlus });
                if (!gated.Contains(text) || menu.ButtonLabels().Contains(text))
                    return;
                Run.Event("shortcut", $"\"{text}\" is not on the staged menu yet: every button unlocked (an old save's menu)");
                menu.RevealEverythingForPlaytest();
            }

            /// <summary>
            /// Criteria of "the player always knows what a ware does, and the menu only shows what they have met": a brand-new save's menu has
            /// Play, Settings (and Rage Quit where a page can quit) and nothing else; the first diamonds bring Visit Yooba with a NEW
            /// sticker; the shop's cards lead with their effect in numbers, three tabs and pages of six, the affordable ones marked, one
            /// glowing with Yooba's voiced pick; and a returning player's save has every button back with no stickers, and New Game
            /// takes none away. Setup shortcuts are named; the clicks are real.
            /// </summary>
            public IEnumerator StagedMenu()
            {
                strictMenu = true;
                var roguelite = RogueliteManager.Instance;
                var gd = GameDefinition.Instance;
                var labels = gd.mainMenuConfig.labels;
                int failed = 0;
                void Gate(string beat, bool ok, string detail)
                {
                    if (ok) { Run.Event("check", $"staged-menu {beat}: ok ({detail})"); return; }
                    failed++;
                    Run.Event("check", $"staged-menu {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"staged-menu {beat}: {detail}");
                }

                yield return ToMainMenu();
                if (Run.Done) yield break;
                var menu = Object.FindAnyObjectByType<MainMenuManager>();

                // 1. A brand-new player.
                Run.Event("shortcut", "a brand-new save: nothing played, no diamonds, nothing revealed");
                roguelite.ResetAllRogueliteData();
                roguelite.SetPoints(0);
                YoobaProgress.Reset();
                MenuReveal.Reset();
                PlayerPrefs.DeleteKey("Level_1_Visited");
                PlayerPrefs.Save();
                menu.RebuildForPlaytest();
                yield return WaitReal(1.5f);
                if (Run.Done) yield break;
                var firstLabels = menu.ButtonLabels();
                Run.Event("menu-buttons", "first visit: " + string.Join(" | ", firstLabels));
                Run.Metric("menu.firstVisitButtons", firstLabels.Count);
                Gate("firstVisitAtMostThree", firstLabels.Count <= 3, $"{firstLabels.Count} buttons: {string.Join(", ", firstLabels)}");
                Gate("firstVisitHasPlay", firstLabels.Contains(labels.play) && firstLabels.Contains(labels.settings),
                    $"Play \"{labels.play}\" and Settings on the menu");
                bool nothingElse = !firstLabels.Contains(labels.shop) && !firstLabels.Contains(labels.levelSelect) && !firstLabels.Contains(labels.videos)
                                   && !firstLabels.Contains(labels.newGame) && !firstLabels.Exists(l => l.StartsWith("Continue"));
                Gate("firstVisitOnlyThat", nothingElse, "no shop, level or video pickers, New Game or Continue yet");
                Gate("firstVisitNoStickers", menu.NewStickerIds().Count == 0, $"stickers: {string.Join(", ", menu.NewStickerIds())}");
                yield return Audit("Main menu (first visit)", 0, 0, 0.5f, progress: false);
                if (Run.Done) yield break;

                // 2. The first diamonds (the setup shortcut stands for shooting them out of an imp), then the menu again.
                Run.Event("shortcut", "the first diamonds: 25 added (what imps drop), the main menu opened again");
                roguelite.AddPoints(25);
                menu.RebuildForPlaytest();
                yield return WaitReal(1.5f);
                if (Run.Done) yield break;
                Gate("shopAppears", menu.ButtonLabels().Contains(labels.shop), $"\"{labels.shop}\" on the menu after the first diamonds");
                Gate("shopHasSticker", menu.NewStickerIds().Contains(MenuReveal.Shop), $"NEW stickers: {string.Join(", ", menu.NewStickerIds())}");
                Gate("pickersStillHidden", !menu.ButtonLabels().Contains(labels.levelSelect) && !menu.ButtonLabels().Contains(labels.videos),
                    "Pick a Level and Rewatch Stuff wait for Floor 1000");
                var shopButton = FindButton(labels.shop);
                bool stickerShown = shopButton != null && shopButton.transform.parent.Find(MenuMarks.StickerName) != null;
                Gate("stickerDrawn", stickerShown, "the NEW! burst is on the Visit Yooba button");
                yield return Audit("Main menu (Visit Yooba with NEW)", 0, 0, 0.5f, progress: false);
                if (Run.Done) yield break;

                yield return ClickUntil(labels.shop, () => Object.FindAnyObjectByType<UpgradeShopManager>() != null, 20f);
                if (Run.Done) yield break;
                Gate("stickerGoesWhenPressed", !MenuReveal.IsNew(MenuReveal.Shop), "pressing it marks it seen");
                Run.Scene("Shop (first visit)");
                var shop = Object.FindAnyObjectByType<UpgradeShopManager>();
                yield return WaitReal(1.5f);
                if (Run.Done || shop == null) yield break;

                // 3. The first visit: Yooba points at one affordable ware, which glows and is on screen.
                int lines0 = yoobaLines.Count;
                Gate("recommendedWare", !string.IsNullOrEmpty(shop.RecommendedId) && shop.IsAffordable(shop.RecommendedId),
                    $"recommended \"{shop.RecommendedId}\"");
                Gate("recommendedGlows", shop.RecommendedId != null && shop.IsGlowing(shop.RecommendedId) && shop.PageIds().Contains(shop.RecommendedId),
                    "its card glows and is on the page showing");
                yield return Run.Until(() => shop.OpeningFinished, 30f, "Yooba's opening lines");
                if (Run.Done) yield break;
                bool said = false, clipMade = false;
                for (int i = lines0; i < yoobaLines.Count; i++)
                    if (yoobaLines[i].StartsWith("yooba (ShopRecommend", System.StringComparison.Ordinal))
                    {
                        said = true;
                        clipMade = !yoobaLines[i].Contains(", no clip");
                    }
                Gate("recommendedSaid", said, said ? yoobaLines[yoobaLines.Count - 1] : FirstSince(lines0));
                Run.Event("check", $"staged-menu recommendation clip: {(clipMade ? "voiced" : "subtitle only, vo_yooba_shop_pick is in Tools/Audio/pending_clips.json until its clip is made")}");

                // 4. Cards lead with the effect; three tabs; at most six cards at once; affordable ones marked.
                roguelite.AddPoints(600);
                shop.RefreshUI();
                yield return WaitReal(0.5f);
                foreach (ShopTab tab in System.Enum.GetValues(typeof(ShopTab)))
                {
                    yield return ClickButton(shop.TabButton(tab), $"tab {tab}");
                    yield return WaitReal(0.6f);
                    if (Run.Done) yield break;
                    Gate($"tab{tab}Active", shop.ActiveTab == tab, $"active tab {shop.ActiveTab}");
                    int pages = shop.PageCount;
                    for (int p = 0; p < pages; p++)
                    {
                        var ids = shop.PageIds();
                        Run.Metric($"shop.tab{tab}.page{p}.cards", ids.Count);
                        Gate($"tab{tab}Page{p}AtMostSix", ids.Count <= ShopCardText.CardsPerPage, $"{ids.Count} cards on {tab} page {p + 1}/{pages}");
                        int leading = 0, marked = 0;
                        foreach (var id in ids)
                        {
                            if (!shop.TryReadCard(id, out var effect, out var description, out var status))
                                continue;
                            bool hasNumber = effect.IndexOfAny("0123456789".ToCharArray()) >= 0;
                            if (hasNumber && !string.IsNullOrEmpty(description) && CardEffectAboveName(shop, id))
                                leading++;
                            else
                                Run.Problem(PlaytestSeverity.Bug, $"staged-menu: \"{id}\" card does not lead with its effect in numbers (\"{effect}\" / \"{description}\")");
                            if (shop.IsAffordable(id) && status.ToUpperInvariant().Contains("CAN BUY"))
                                marked++;
                        }
                        Gate($"tab{tab}Page{p}EffectFirst", leading == ids.Count, $"{leading}/{ids.Count} cards lead with an effect that has a number");
                        Run.Event("shop-page", $"{tab} {p + 1}/{pages}: {string.Join(", ", ids)} ({marked} marked as buyable)");
                        yield return Audit($"Shop {tab} page {p + 1}/{pages}", 0, 0, 0.4f, progress: false);
                        if (Run.Done) yield break;
                        if (p + 1 < pages)
                        {
                            yield return ClickButton(shop.NextPageButton, "next page");
                            yield return WaitReal(0.5f);
                        }
                    }
                }
                Gate("affordableMarked", shop.IsAffordable("moreHealth"), "with 600+ diamonds the Goo Plaster is marked buyable");

                // 5. At the phone and desktop sizes: no overlap or cut-off cards on each tab (a WebGL page keeps the runner's device size).
                foreach (ShopTab tab in System.Enum.GetValues(typeof(ShopTab)))
                {
                    yield return ClickButton(shop.TabButton(tab), $"tab {tab}");
                    yield return WaitReal(0.4f);
                    yield return ShopLayoutSizes();
                    if (Run.Done) yield break;
                }

                yield return ClickUntil(gd.upgradeShopConfig.layout.backText, () => Object.FindAnyObjectByType<MainMenuManager>() != null, 20f);
                if (Run.Done) yield break;
                menu = Object.FindAnyObjectByType<MainMenuManager>();

                // 6. A returning player: a save with progress from before the staging (no reveal saved) shows everything it had, no stickers.
                Run.Event("shortcut", "a returning save: Floor 1000 cleared, the first Waystone, diamonds spent; the reveal save absent (as before this change)");
                MenuReveal.Reset();
                PlayerPrefs.SetInt("Level_1_Visited", 1);
                GameProgressManager.Instance.SetLevelCompleted(1);
                GameProgressManager.Instance.SetLevelCompleted(MenuReveal.FloorThousandLevel);
                WaystoneProgress.Reach(1000);
                menu.RebuildForPlaytest();
                yield return WaitReal(1.5f);
                if (Run.Done) yield break;
                var expected = new List<string> { labels.settings, labels.newGame, labels.levelSelect, labels.videos, labels.shop };
                if (gd.replay != null && gd.replay.enabled)
                    expected.AddRange(new[] { gd.replay.labels.endless, gd.replay.labels.daily });
                if (gd.codex != null && gd.codex.enabled && gd.codex.showOnMainMenu)
                    expected.Add(gd.codex.buttonLabel);
                var returning = menu.ButtonLabels();
                Run.Event("menu-buttons", "returning player: " + string.Join(" | ", returning));
                var missing = expected.FindAll(l => !returning.Contains(l));
                Gate("returningSeesEverything", missing.Count == 0 && returning.Exists(l => l.StartsWith("Continue")),
                    missing.Count == 0 ? $"{returning.Count} buttons, Continue among them" : "missing: " + string.Join(", ", missing));
                Gate("returningNoStickers", menu.NewStickerIds().Count == 0, $"stickers: {string.Join(", ", menu.NewStickerIds())}");
                yield return Audit("Main menu (returning player)", 0, 0, 0.5f, progress: false);
                if (Run.Done) yield break;

                // 7. New Game wipes the run, not what he had unlocked.
                Run.Event("shortcut", "New Game's reset (RogueliteManager.ResetAllRogueliteData: levels, Waystone and stats cleared)");
                roguelite.ResetAllRogueliteData();
                menu.RebuildForPlaytest();
                yield return WaitReal(1.5f);
                if (Run.Done) yield break;
                var afterReset = menu.ButtonLabels();
                missing = expected.FindAll(l => l != labels.newGame && !afterReset.Contains(l));
                Gate("newGameKeepsUnlocks", missing.Count == 0, missing.Count == 0 ? "every unlocked button is still there" : "gone: " + string.Join(", ", missing));

                MenuReveal.Reset();
                strictMenu = false;
                Run.Progress(1f);
                End(failed == 0 ? PlaytestOutcome.Won : PlaytestOutcome.Done);
            }

            /// <summary>The card's effect line is drawn above its name line (screen space), so the number is read first.</summary>
            static bool CardEffectAboveName(UpgradeShopManager shop, string id)
            {
                var button = shop.CardButton(id);
                if (button == null)
                    return false;
                var title = FindCardText(button.transform, "Title");
                var description = FindCardText(button.transform, "Description");
                if (title == null || description == null)
                    return false;
                var t = (RectTransform)title.transform.parent;
                var d = (RectTransform)description.transform.parent;
                return t.TransformPoint(t.rect.center).y > d.TransformPoint(d.rect.center).y;
            }
        }
    }
}
