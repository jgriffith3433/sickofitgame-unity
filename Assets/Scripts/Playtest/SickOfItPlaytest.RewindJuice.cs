using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Rewind;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>The rewind-juice scenario: a longer rewind with rewards (Level 1, loaded directly: a setup shortcut).</summary>
        public const string RewindJuiceId = "rewind-juice";
        const int RewindJuiceLevel = 1;
        const float RewindJuiceSeconds = 240f;

        sealed partial class Session
        {
            int rewindJuicePassed, rewindJuiceFailed;

            void RewindJuiceCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    rewindJuicePassed++;
                    Run.Event("check", $"rewind-juice {beat}: ok ({detail})");
                }
                else
                {
                    rewindJuiceFailed++;
                    Run.Event("check", $"rewind-juice {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"rewind-juice {beat}: {detail}");
                }
            }

            public IEnumerator RewindJuiceRun()
            {
                var roguelite = RogueliteManager.Instance;
                var definition = GameDefinition.Instance;
                if (roguelite == null || definition == null)
                {
                    Run.Problem(PlaytestSeverity.Bug, "rewind-juice: no RogueliteManager or game definition");
                    End(PlaytestOutcome.Error);
                    yield break;
                }

                // 1. A new game: nothing bought.
                roguelite.ResetAllRogueliteData();
                Run.Event("shortcut", $"level {RewindJuiceLevel} loaded directly with the shop reset");
                var load = LoadLevel(RewindJuiceLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => LevelRunning(RewindJuiceLevel) && RewindBudget.Instance != null, 60f, "the level's rewind juice");
                if (Run.Done)
                    yield break;
                var budget = RewindBudget.Instance;
                var config = definition.rewindBudget;
                Run.Metric("rewindJuice.newGame.juice", budget.Juice);
                Run.Metric("rewindJuice.newGame.max", budget.Max);
                RewindJuiceCheck("a new game starts with about 10 s", budget.Juice >= 9.9f && budget.Max >= 9.9f, $"juice {budget.Juice:0.0} s of {budget.Max:0.0} s (it was 3 s of 5 s)");
                Run.Event("rewind-juice", $"history window {definition.rewindMaxHistorySeconds:0} s every {definition.rewindRecordInterval:0.00} s; clean kill +{config.cleanKillRefund:0.0} s when not hurt for {config.cleanKillSeconds:0} s; " +
                                         $"survive {config.survivalSeconds:0} s after a death-rewind for +{config.survivalBonus:0.0} s");
                RewindJuiceCheck("the history window holds the whole cap", definition.rewindMaxHistorySeconds >= budget.Max, $"history {definition.rewindMaxHistorySeconds:0} s");

                // 2. The goods, bought through the shop's own purchase.
                roguelite.AddPoints(5000);
                foreach (var id in new[] { "juiceBox", "bottomlessJar", "quickUnwind" })
                {
                    UpgradeDefinition good = null;
                    foreach (var upgrade in RogueliteManager.ShopCatalog)
                    {
                        if (upgrade != null && upgrade.id == id)
                            good = upgrade;
                    }
                    int bought = 0;
                    while (good != null && roguelite.TryPurchase(good))
                        bought++;
                    RewindJuiceCheck($"shop sells {id}", good != null && bought == good.maxLevel, $"{bought} of {(good != null ? good.maxLevel : 0)} levels bought");
                }
                float expectedMax = config.maxSeconds + roguelite.GetShopTotal(SickOfIt.Data.Config.ShopEffect.RewindJuice);
                float speedBonus = roguelite.GetShopTotal(SickOfIt.Data.Config.ShopEffect.RewindSpeed);
                var old = GameplayWorld.Instance;
                load = LoadLevel(RewindJuiceLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(RewindJuiceLevel) && RewindBudget.Instance != null, 60f, "the level with the goods");
                if (Run.Done)
                    yield break;
                budget = RewindBudget.Instance;
                Run.Metric("rewindJuice.upgraded.max", budget.Max);
                RewindJuiceCheck("the goods extend the cap much further", budget.Max >= expectedMax - 0.1f && budget.Max >= 24f, $"cap {budget.Max:0.0} s (expected {expectedMax:0.0} s); juice {budget.Juice:0.0} s");
                RewindJuiceCheck("rewind speed upgrade", speedBonus >= 0.74f, $"+{speedBonus * 100f:0}% rewind speed");

                // 3. Play until a clean kill pays.
                int rewards = 0;
                float lastJuice = budget.Juice;
                bool refundSeen = false;
                System.Action<float, string> onReward = (seconds, label) =>
                {
                    rewards++;
                    Run.Event("juice", $"{label} +{seconds:0.0} s, juice now {budget.Juice:0.0} s of {budget.Max:0.0} s");
                    if (label == "CLEAN KILL")
                        refundSeen = true;
                };
                budget.Rewarded += onReward;
                // Make room to see the refund: the cap is full at the start, so spend a little first (a setup shortcut: juice isn't rewound state).
                budget.Configure(budget.Max - 3f, budget.Max, 1f);
                budget.ConfigureRewards(config.survivalSeconds, config.survivalBonus);
                var world = GameplayWorld.Instance;
                yield return PlayLevel(RewindJuiceLevel, 0f, now => null, 0f, 1f, 200f, until: () => refundSeen && world.Hud.RewardsShown > 0);
                budget.Rewarded -= onReward;
                if (Run.Done)
                    yield break;
                var hud = GameplayWorld.Instance != null ? GameplayWorld.Instance.Hud : null;
                RewindJuiceCheck("a clean kill refunds juice", refundSeen, $"{rewards} reward(s) paid; juice {lastJuice:0.0} -> {budget.Juice:0.0} s");
                RewindJuiceCheck("the HUD shows it", hud != null && hud.RewardsShown > 0, hud != null ? $"floated \"{hud.LastRewardShown}\" ({hud.RewardsShown} time(s))" : "no HUD");

                Run.Metric("rewindJuice.passed", rewindJuicePassed);
                Run.Metric("rewindJuice.failed", rewindJuiceFailed);
                Run.Progress(1f);
                End(rewindJuiceFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }
        }
    }
}
