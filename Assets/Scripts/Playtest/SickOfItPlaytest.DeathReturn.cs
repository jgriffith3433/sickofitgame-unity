using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Core.Flow;
using SickOfIt.Core.Progression;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>The Return offer on the death screen (Docs/Story.md "Waystones and runs"): pay, decline, and too poor to be offered.</summary>
        public const string DeathReturnId = "death-return";
        const float DeathReturnSeconds = 420f;

        sealed partial class Session
        {
            int deathReturnPassed, deathReturnFailed;

            void DeathReturnCheck(string beat, bool ok, string detail)
            {
                if (ok) deathReturnPassed++; else deathReturnFailed++;
                Run.Event("check", $"death-return {beat}: {(ok ? "ok" : "FAILED")} ({detail})");
                if (!ok)
                    Run.Problem(PlaytestSeverity.Bug, $"death-return {beat}: {detail}");
                Run.Metric("deathReturn.passed", deathReturnPassed);
                Run.Metric("deathReturn.failed", deathReturnFailed);
            }

            /// <summary>Loads the level directly (a setup shortcut) with 'diamonds' and one Grudge, and plays to lose until the death screen is up.</summary>
            IEnumerator DeathReturnDie(int level, int diamonds)
            {
                var roguelite = RogueliteManager.Instance;
                roguelite.SetPoints(diamonds);
                if (GrudgeCount() == 0)
                    foreach (var id in GameContent.Current.Upgrades.Keys) { UpgradeProgress.Add(id); break; }
                Run.Event("shortcut", $"level {level} loaded directly with {diamonds} diamonds and Grudges {GrudgeList()}");
                var load = LoadLevel(level);
                if (load != null)
                    yield return load;
                reckless = true;
                try { yield return PlayLevel(level, 0f, null, 0f, 1f, 150f, waitOutDeath: true); }
                finally { reckless = false; }
                if (Run.Done) yield break;
                if (lastOutcome != "died")
                {
                    Run.Problem(PlaytestSeverity.Bug, $"death-return: the bot couldn't die on level {level} ({lastOutcome})");
                    End(PlaytestOutcome.Done);
                    yield break;
                }
                yield return Run.Until(() => DeathRuntime.Current != null, 30f, "the death screen");
                if (!Run.Done) yield return WaitReal(2f);
            }

            public IEnumerator DeathReturnRun()
            {
                var definition = GameDefinition.Instance;
                var roguelite = RogueliteManager.Instance;
                var progress = GameProgressManager.Instance;
                var texts = definition.menuTheme.texts;
                int cost = RunFlow.ReturnCost();
                int level = WaystoneLevel;
                string offerText = string.Format(texts.returnForDiamonds, cost);
                yield return ToMainMenu();
                if (Run.Done) yield break;

                // Setup: the Landing's Waystone reached, Yooba met (as in the waystone scenario).
                roguelite.ResetAllRogueliteData();
                progress.InitializeNewPlayer();
                progress.SetLevelCompleted(1);
                progress.SetLevelCompleted(LandingLevel);
                ShaftProgress.Floor = LandingLastFloor;
                ShaftProgress.Plunged = true;
                YoobaProgress.Met = true;
                WaystoneProgress.Reach(definition.shaft != null ? definition.shaft.bottomFloor : LandingFirstFloor);
                Run.Event("shortcut", "fresh save: the Landing's Waystone reached, Yooba met");

                // 1. Pay: 25 diamonds, die, the offer is on the screen, click it.
                yield return DeathReturnDie(level, cost + 15);
                if (Run.Done) yield break;
                var death = DeathRuntime.Current;
                int runsBefore = WaystoneProgress.RunsEnded;
                string grudgesBefore = GrudgeList();
                DeathReturnCheck("offered", death != null && death.ReturnOffered, death != null ? $"the \"{offerText}\" button is {(death.ReturnOffered ? "" : "not ")}on the death screen with {roguelite.GetPoints()} diamonds" : "no death screen");
                DeathReturnCheck("runKept", WaystoneProgress.RunsEnded == runsBefore, "the run is not ended while the offer stands");
                if (death == null || !death.ReturnOffered) { End(PlaytestOutcome.Lost); yield break; }
                yield return ClickUntil(offerText, () => death.ReturnPaid, 20f);
                if (Run.Done) yield break;
                yield return Run.Until(() => LevelRunning(level), 60f, $"level {level} again");
                if (Run.Done) yield break;
                var health = GameplayWorld.Instance.Player != null ? GameplayWorld.Instance.Player.GetComponentInChildren<PlayerHealth>() : null;
                DeathReturnCheck("paid", roguelite.GetPoints() == 15, $"{roguelite.GetPoints()} diamonds left (25 before, {cost} paid)");
                DeathReturnCheck("sameLevel", GameplayWorld.Instance.LevelNumber == level, $"level {GameplayWorld.Instance.LevelNumber} again, no Shaft");
                DeathReturnCheck("invincible", health != null && health.IsInvincible, health != null ? $"{health.InvincibilityTimer:0.0} s of safety left" : "no health");
                DeathReturnCheck("notEnded", WaystoneProgress.RunsEnded == runsBefore && GrudgeList() == grudgesBefore, $"runs ended {WaystoneProgress.RunsEnded}, Grudges {grudgesBefore} then {GrudgeList()}");
                Run.Progress(0.4f);

                // 2. Decline: 15 diamonds left, offered, Escape skips: the Shaft's drop, nothing spent, the run ended.
                yield return DeathReturnDie(level, 15);
                if (Run.Done) yield break;
                death = DeathRuntime.Current;
                DeathReturnCheck("declineOffered", death != null && death.ReturnOffered, "offered with 15 diamonds");
                runsBefore = WaystoneProgress.RunsEnded;
                yield return Tap(UnityEngine.InputSystem.Key.Escape);
                yield return Run.Until(() => ShaftScreen.Current != null, 30f, "the Shaft after declining");
                if (Run.Done) yield break;
                DeathReturnCheck("declined", ShaftScreen.Current != null && ShaftScreen.Current.Kind == ShaftScreen.Mode.Drop && roguelite.GetPoints() == 15 && WaystoneProgress.RunsEnded == runsBefore + 1 && GrudgeCount() == 0,
                    $"the Shaft {ShaftScreen.Current?.Kind}, {roguelite.GetPoints()} diamonds, runs ended {runsBefore} then {WaystoneProgress.RunsEnded}, Grudges {GrudgeList()}");
                Run.Progress(0.7f);

                // 3. Too poor: 5 diamonds, no offer, the drop is the only way.
                yield return DeathReturnDie(level, cost - 5);
                if (Run.Done) yield break;
                death = DeathRuntime.Current;
                DeathReturnCheck("poorNotOffered", death != null && !death.ReturnOffered, $"no offer with {roguelite.GetPoints()} diamonds");
                yield return Tap(UnityEngine.InputSystem.Key.Escape);
                yield return Run.Until(() => ShaftScreen.Current != null, 30f, "the Shaft when poor");
                if (Run.Done) yield break;
                DeathReturnCheck("poorDrops", ShaftScreen.Current.Kind == ShaftScreen.Mode.Drop, $"the Shaft {ShaftScreen.Current.Kind}");
                End(deathReturnFailed == 0 ? PlaytestOutcome.Done : PlaytestOutcome.Lost);
            }
        }
    }
}
