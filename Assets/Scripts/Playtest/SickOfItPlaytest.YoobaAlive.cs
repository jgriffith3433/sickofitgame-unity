using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core.Progression;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using Object = UnityEngine.Object;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>Yooba's living shop (Docs/Story.md, "Yooba's shop"): the shelf fills as floors are cleared, he reacts, mutters, and pays the Lost Things quest.</summary>
        public const string YoobaAliveId = "yooba-alive";

        sealed partial class Session
        {
            /// <summary>
            /// Setup shortcuts: Yooba's save reset, floors marked cleared (what the results screen writes), the deaths counted and the
            /// three lost things' flags set (what each floor's pickup writes on touch). Everything else is the real shop, clicked with the
            /// virtual mouse: the shelf has one jar and nine gaps, then more jars after more floors; Yooba points out each new ware, asks
            /// for the quest, reacts to the deaths and to a buy, fidgets by himself, mutters when it is quiet, notes each find and pays the
            /// reward once; the ledger chip counts the finds; the Thank-You Jar joins the shelf.
            /// </summary>
            public IEnumerator YoobaAlive()
            {
                var definition = GameDefinition.Instance;
                var settings = definition.yooba;
                var roguelite = RogueliteManager.Instance;
                int failed = 0;
                void Gate(string beat, bool ok, string detail)
                {
                    if (ok) { Run.Event("check", $"yooba-alive {beat}: ok ({detail})"); return; }
                    failed++;
                    Run.Event("check", $"yooba-alive {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"yooba-alive {beat}: {detail}");
                }
                bool Heard(int from, string needle)
                {
                    for (int i = from; i < yoobaLines.Count; i++)
                        if (yoobaLines[i].Contains(needle))
                            return true;
                    return false;
                }

                // Yooba's idle timers shortened so the run sees them (presentation numbers; restored at the end).
                float behaviorSeconds = settings.idleBehaviorSeconds, lineSeconds = settings.idleLineSeconds;
                settings.idleBehaviorSeconds = 1.2f;
                settings.idleLineSeconds = 8f;

                yield return ToMainMenu();
                if (Run.Done) yield break;

                Run.Event("shortcut", "Yooba's save reset, Levels 1-2 cleared (Floor 1000), 500 diamonds");
                YoobaProgress.Reset();
                UnlockFlags.Reset();
                for (int n = 1; n <= definition.TotalLevels; n++)
                    PlayerPrefs.DeleteKey($"Level_{n}_Completed");
                GameProgressManager.Instance.SetLevelCompleted(1);
                GameProgressManager.Instance.SetLevelCompleted(2);
                roguelite.SetPoints(500);

                int newLines = 0;
                foreach (var line in settings.lines)
                    if (line != null && (line.moment >= YoobaMoment.ShopDeaths || !string.IsNullOrEmpty(line.item) || line.minDeaths > 0 || line.requires.Count > 0))
                        newLines++;
                Run.Metric("yooba.newReactiveLines", newLines);
                Gate("twentyNewLines", newLines >= 20, $"{newLines} reactive lines in ui/yooba.json");

                UpgradeShopManager shop = null;
                IEnumerator OpenShop(string scene)
                {
                    yield return ClickUntil(definition.mainMenuConfig.labels.shop, () => Object.FindAnyObjectByType<UpgradeShopManager>() != null, 20f);
                    if (Run.Done) yield break;
                    Run.Scene(scene);
                    shop = Object.FindAnyObjectByType<UpgradeShopManager>();
                    yield return Run.Until(() => shop == null || shop.OpeningFinished, 90f, "Yooba's opening lines");
                }
                IEnumerator CloseShop()
                {
                    yield return ClickUntil(definition.upgradeShopConfig.layout.backText, () => Object.FindAnyObjectByType<MainMenuManager>() != null, 20f);
                }

                // ── 1. A young shop: one jar, the rest gaps ──
                int open0 = yoobaLines.Count;
                yield return OpenShop("Shop (Floor 1000 cleared)");
                if (Run.Done || shop == null) yield break;
                var wares = shop.ShelfWares();
                Run.Event("shelf", $"{wares.Count} of {shop.ShelfSlots} wares: {string.Join(", ", wares)}");
                Gate("shelfStartsSparse", wares.Count == 1 && shop.ShelfSlots >= 9, $"{wares.Count} wares on {shop.ShelfSlots} slots");
                Gate("questNotAskedYet", !UnlockFlags.Has(settings.quest.askedFlag) && shop.QuestChipText == "", $"chip \"{shop.QuestChipText}\"");
                yield return Audit("Shop (shelf, Floor 1000)", 0, 4, 1f, progress: false);
                if (Run.Done) yield break;
                int shelf0 = wares.Count;
                yield return CloseShop();
                if (Run.Done) yield break;

                // ── 2. Floors cleared: more on the shelf, Yooba points them out, asks for the quest ──
                Run.Event("shortcut", "Floors 999, 998, 995 cleared (levels 3, 4, 7), 3 deaths counted");
                foreach (int n in new[] { 3, 4, 7 })
                    GameProgressManager.Instance.SetLevelCompleted(n);
                YoobaProgress.Deaths = 3;
                int open1 = yoobaLines.Count;
                yield return OpenShop("Shop (Floor 995 cleared)");
                if (Run.Done || shop == null) yield break;
                wares = shop.ShelfWares();
                Run.Event("shelf", $"{wares.Count} of {shop.ShelfSlots} wares: {string.Join(", ", wares)}");
                Run.Metric("shelf.wares", wares.Count);
                Gate("shelfFills", wares.Count > shelf0, $"{shelf0} wares, then {wares.Count} after three more floors");
                Gate("yoobaPointsOutWare", Heard(open1, "ShopStocked"), FirstSince(open1));
                Gate("yoobaReactsToDeaths", Heard(open1, "ShopDeaths"), "3 deaths counted");
                Gate("yoobaAsksForQuest", Heard(open1, "QuestOffer") && UnlockFlags.Has(settings.quest.askedFlag), "the quest offered once Floor 999 is cleared");
                Gate("ledgerShown", shop.QuestChipText.Contains("0/3"), $"chip \"{shop.QuestChipText}\"");
                yield return Audit("Shop (shelf, Floor 995)", 1, 4, 1f, progress: false);
                if (Run.Done) yield break;

                // ── 3. A buy, answered for that good; then he fidgets and mutters by himself ──
                int buy0 = yoobaLines.Count;
                int moves0 = shop.Yooba != null ? shop.Yooba.BehaviorsDone : 0;
                yield return ClickButton(shop.CardButton("moreHealth"), "Goo Plaster");
                yield return Run.Until(() => Heard(buy0, "vo_yooba_buy_plaster"), 15f, "Yooba's line for the Goo Plaster");
                Gate("yoobaReactsToBuy", Heard(buy0, "vo_yooba_buy_plaster"), FirstSince(buy0));
                int idle0 = yoobaLines.Count;
                yield return Run.Until(() => Heard(idle0, "ShopIdle") && (shop.Yooba == null || !shop.Yooba.Speaking), 90f, "Yooba muttering to himself");
                Gate("yoobaMutters", Heard(idle0, "ShopIdle"), "a ShopIdle line after the quiet");
                Gate("yoobaFidgets", shop.Yooba != null && shop.Yooba.BehaviorsDone > moves0, $"{(shop.Yooba != null ? shop.Yooba.BehaviorsDone - moves0 : 0)} idle behaviors (blink, look, hop, hum, sniff)");
                yield return CloseShop();
                if (Run.Done) yield break;

                // ── 4. The quest: one find, then all three ──
                Run.Event("shortcut", "the baker's rolling pin found (flag lost.pin, what its pickup on Floor 997 writes)");
                UnlockFlags.Set("lost.pin");
                int find0 = yoobaLines.Count;
                yield return OpenShop("Shop (one lost thing)");
                if (Run.Done || shop == null) yield break;
                Gate("yoobaNotesFind", Heard(find0, "vo_yooba_quest_pin"), FirstSince(find0));
                Gate("ledgerCounts", shop.QuestChipText.Contains("1/3"), $"chip \"{shop.QuestChipText}\"");
                yield return CloseShop();
                if (Run.Done) yield break;

                Run.Event("shortcut", "the mitt and the bell found (flags lost.mitt, lost.bell)");
                UnlockFlags.Set("lost.mitt");
                UnlockFlags.Set("lost.bell");
                int before = roguelite.GetPoints();
                int done0 = yoobaLines.Count;
                yield return OpenShop("Shop (all three found)");
                if (Run.Done || shop == null) yield break;
                Gate("questPays", roguelite.GetPoints() >= before + settings.quest.rewardDiamonds && UnlockFlags.Has(settings.quest.paidFlag),
                    $"diamonds {before} -> {roguelite.GetPoints()} (reward {settings.quest.rewardDiamonds})");
                Gate("yoobaCelebrates", Heard(done0, "QuestDone"), FirstSince(done0));
                Gate("rewardJarOnShelf", shop.ShelfWares().Contains("thankYouJar"), string.Join(", ", shop.ShelfWares()));
                Gate("ledgerDone", shop.QuestChipText == settings.quest.doneText, $"chip \"{shop.QuestChipText}\"");
                yield return Audit("Shop (quest done)", 2, 4, 1f, progress: false);
                if (Run.Done) yield break;
                yield return CloseShop();
                if (Run.Done) yield break;

                int paid = roguelite.GetPoints();
                yield return OpenShop("Shop (quest already paid)");
                if (Run.Done || shop == null) yield break;
                Gate("paidOnce", roguelite.GetPoints() == paid, $"diamonds {paid} -> {roguelite.GetPoints()}");
                yield return CloseShop();

                settings.idleBehaviorSeconds = behaviorSeconds;
                settings.idleLineSeconds = lineSeconds;
                YoobaProgress.Reset();
                UnlockFlags.Reset();
                roguelite.SetPoints(0);
                Run.Progress(1f);
                End(failed == 0 ? PlaytestOutcome.Won : PlaytestOutcome.Done);
            }
        }
    }
}
