using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Flow;
using SickOfIt.Core.Progression;
using SickOfIt.Data;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>Flow between attempts: the death screen's written quip (varied), Again (a restart under 1 s) and the results' time and clean kills.</summary>
        public const string DeathFlowId = "death-flow";
        const float DeathFlowSeconds = 600f;

        sealed partial class Session
        {
            int deathFlowPassed, deathFlowFailed;

            void DeathFlowCheck(string beat, bool ok, string detail)
            {
                if (ok) deathFlowPassed++; else deathFlowFailed++;
                Run.Event("check", $"death-flow {beat}: {(ok ? "ok" : "FAILED")} ({detail})");
                if (!ok)
                    Run.Problem(PlaytestSeverity.Bug, $"death-flow {beat}: {detail}");
                Run.Metric("deathFlow.passed", deathFlowPassed);
                Run.Metric("deathFlow.failed", deathFlowFailed);
            }

            public IEnumerator DeathFlowRun()
            {
                var definition = GameDefinition.Instance;
                var roguelite = RogueliteManager.Instance;
                var progress = GameProgressManager.Instance;
                var texts = definition.menuTheme.texts;
                int level = WaystoneLevel;
                yield return ToMainMenu();
                if (Run.Done) yield break;

                // Setup (named shortcut): the Landing's Waystone reached, Yooba met, one run already ended (so the drop is skippable), no diamonds (no Return offer).
                roguelite.ResetAllRogueliteData();
                progress.InitializeNewPlayer();
                progress.SetLevelCompleted(1);
                progress.SetLevelCompleted(LandingLevel);
                ShaftProgress.Floor = LandingLastFloor;
                ShaftProgress.Plunged = true;
                YoobaProgress.Met = true;
                WaystoneProgress.Reach(definition.shaft != null ? definition.shaft.bottomFloor : LandingFirstFloor);
                WaystoneProgress.RunsEnded = 1;
                Run.Event("shortcut", "fresh save: the Landing's Waystone reached, Yooba met, one run already ended");

                // 1. Three deaths: each shows a quip; they must not repeat back to back and must not all be the same.
                var quips = new List<string>();
                for (int i = 0; i < 3 && !Run.Done; i++)
                {
                    yield return DeathReturnDie(level, 0);
                    if (Run.Done) yield break;
                    var death = DeathRuntime.Current;
                    string quip = death != null ? death.Quip : "";
                    quips.Add(quip);
                    Run.Event("quip", $"death {i + 1}: \"{quip}\"");
                    DeathFlowCheck($"quip{i + 1}", !string.IsNullOrWhiteSpace(quip), $"the death screen says \"{quip}\"");
                    if (i == 2)
                        break;
                    // 2. Again (Enter, real input): the next attempt must be playing within 1 s.
                    float pressed = Time.realtimeSinceStartup;
                    yield return Tap(UnityEngine.InputSystem.Key.Enter);
                    yield return Run.Until(() => LevelRunning(WaystoneNextLevel(death)), 30f, "the level again after Again");
                    float took = Time.realtimeSinceStartup - pressed;
                    Run.Metric($"deathFlow.restartSeconds{i + 1}", took);
                    Run.Event("restart", $"Again to the level playing again: {took:0.00} s (death scene's own load call at {death.AgainSeconds:0.00} s)");
                    DeathFlowCheck($"restart{i + 1}", took < 1f, $"restart took {took:0.00} s (must be under 1 s)");
                    DeathFlowCheck($"againNoShaft{i + 1}", ShaftScreen.Current == null && death.AgainPressed, "no Shaft drop in between");
                }
                if (Run.Done) yield break;
                bool varied = quips.Distinct().Count() > 1 && quips.Zip(quips.Skip(1), (a, b) => a != b).All(x => x);
                DeathFlowCheck("quipsVaried", varied, string.Join(" | ", quips));
                Run.Progress(0.4f);

                // 3. Level clear: time and clean kills on the results screen.
                yield return ToMainMenu();
                if (Run.Done) yield break;
                var load = LoadLevel(level);
                if (load != null)
                    yield return load;
                yield return PlayLevel(level, 0f, null, 0.4f, 0.9f, 400f);
                if (Run.Done) yield break;
                yield return Run.Until(() => GameplayWorld.Instance == null || GameplayWorld.Instance.Hud == null || GameplayWorld.Instance.Hud.ResultsOpen, 60f, "the results screen");
                if (Run.Done) yield break;
                bool open = GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null && GameplayWorld.Instance.Hud.ResultsOpen;
                if (!open)
                {
                    DeathFlowCheck("results", false, $"the level didn't end in a clear ({lastOutcome})");
                    End(PlaytestOutcome.Lost);
                    yield break;
                }
                yield return WaitReal(2.5f); // the rows count up
                var shown = Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None).Where(t => t.isActiveAndEnabled).Select(t => t.text).ToList();
                var world = GameplayWorld.Instance;
                bool hasTime = shown.Any(t => t.StartsWith(texts.resultsTime));
                bool hasClean = shown.Contains(texts.resultsCleanKills);
                int clean = Mathf.RoundToInt(world.GetCounter("cleanKills"));
                Run.Event("results", $"time label {hasTime}, clean kills label {hasClean}, cleanKills counter {clean}");
                DeathFlowCheck("resultsTime", hasTime, "the results screen has a Time row");
                DeathFlowCheck("resultsCleanKills", hasClean, $"the results screen has a \"{texts.resultsCleanKills}\" row (counter {clean})");
                End(deathFlowFailed == 0 ? PlaytestOutcome.Done : PlaytestOutcome.Lost);
            }

            static int WaystoneNextLevel(DeathRuntime death) => death != null && death.RunEnded ? death.RunStop.NextLevel : WaystoneLevel;
        }
    }
}
