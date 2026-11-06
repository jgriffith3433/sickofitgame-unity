using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Progression;
using SickOfIt.Data;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>Death, restart and level-clear presentation: the quip, the instant Again button, the results' time and clean kills.</summary>
        public const string DeathRestartId = "death-restart";
        const float DeathRestartSeconds = 600f;

        sealed partial class Session
        {
            int deathRestartPassed, deathRestartFailed;

            void DeathRestartCheck(string beat, bool ok, string detail)
            {
                if (ok) deathRestartPassed++; else deathRestartFailed++;
                Run.Event("check", $"death-restart {beat}: {(ok ? "ok" : "FAILED")} ({detail})");
                if (!ok)
                    Run.Problem(PlaytestSeverity.Bug, $"death-restart {beat}: {detail}");
                Run.Metric("deathRestart.passed", deathRestartPassed);
                Run.Metric("deathRestart.failed", deathRestartFailed);
            }

            public IEnumerator DeathRestartRun()
            {
                var texts = GameDefinition.Instance.menuTheme.texts;
                var roguelite = RogueliteManager.Instance;
                const int level = 1;
                yield return ToMainMenu();
                if (Run.Done) yield break;
                roguelite.ResetAllRogueliteData();
                GameProgressManager.Instance.InitializeNewPlayer();

                // 1-2. Die three times on level 1: each death screen has a quip (a different one each time), Again restarts in under a second.
                var quips = new List<string>();
                for (int i = 0; i < 3; i++)
                {
                    var load = LoadLevel(level);
                    if (load != null)
                        yield return load;
                    reckless = true;
                    try { yield return PlayLevel(level, 0f, null, 0f, 1f, 150f, waitOutDeath: true); }
                    finally { reckless = false; }
                    if (Run.Done) yield break;
                    if (lastOutcome != "died")
                    {
                        DeathRestartCheck("died", false, $"the bot couldn't die on level {level} ({lastOutcome})");
                        End(PlaytestOutcome.Lost);
                        yield break;
                    }
                    yield return Run.Until(() => DeathRuntime.Current != null && DeathRuntime.Current.AgainOffered, 30f, "the death screen");
                    if (Run.Done) yield break;
                    yield return WaitReal(0.5f);
                    var death = DeathRuntime.Current;
                    quips.Add(death.Quip);
                    DeathRestartCheck($"quip{i + 1}", !string.IsNullOrEmpty(death.Quip), $"death {i + 1} says \"{death.Quip}\"");
                    DeathRestartCheck($"again{i + 1}", death.AgainOffered, $"the \"{texts.deathAgain}\" button is on the death screen");
                    float pressed = Time.realtimeSinceStartup;
                    Run.Event("death-again", $"clicking \"{texts.deathAgain}\" on death {i + 1}");
                    yield return ClickUntil(texts.deathAgain, () => death == null || death.AgainPressedAt >= 0f, 20f);
                    if (Run.Done) yield break;
                    yield return Run.Until(() => LevelRunning(level), 30f, $"level {level} again");
                    if (Run.Done) yield break;
                    float took = Time.realtimeSinceStartup - death.AgainPressedAt;
                    Run.Metric($"deathRestart.seconds{i + 1}", took);
                    DeathRestartCheck($"fast{i + 1}", death.AgainPressedAt >= 0f && took < 1f, $"Again to a running level took {took:0.00} s (limit 1 s)");
                }
                DeathRestartCheck("varied", quips.Distinct().Count() == quips.Count, $"quips: {string.Join(" | ", quips)}");
                Run.Progress(0.6f);

                // 3. Clear level 1: the results show the time and the clean kills.
                var again = LoadLevel(level);
                if (again != null)
                    yield return again;
                yield return PlayLevel(level, 0f);
                if (Run.Done) yield break;
                StopBot();
                yield return Run.Until(() => GameplayWorld.Instance == null || GameplayWorld.Instance.Hud == null || GameplayWorld.Instance.Hud.ResultsOpen, 30f, "the results screen");
                if (Run.Done) yield break;
                var world = GameplayWorld.Instance;
                bool open = world != null && world.Hud != null && world.Hud.ResultsOpen;
                DeathRestartCheck("resultsOpen", open, $"level {level} cleared ({lastOutcome})");
                if (open)
                {
                    yield return WaitReal(2.5f);
                    var summary = world.Telemetry.Summarize("complete");
                    var words = Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Select(t => t.text).ToList();
                    bool time = words.Any(w => w == texts.resultsTime || w.StartsWith(texts.resultsTime.Split('{')[0]) && w.Contains("par") || w.StartsWith("Time"));
                    bool clean = words.Contains(texts.resultsCleanKills);
                    DeathRestartCheck("resultsTime", time, $"the results list Time {summary.duration:0.0} s");
                    DeathRestartCheck("resultsCleanKills", clean && summary.cleanKills <= summary.kills, $"\"{texts.resultsCleanKills}\" row shown: {summary.cleanKills} of {summary.kills} kills");
                    Run.Event("results", $"time {summary.duration:0.0} s, kills {summary.kills}, clean kills {summary.cleanKills}");
                    Run.Metric("deathRestart.cleanKills", summary.cleanKills);
                    yield return Audit("Results (death-restart)", 0, 1, 3f, progress: false);
                }
                End(deathRestartFailed == 0 ? PlaytestOutcome.Done : PlaytestOutcome.Lost);
            }
        }
    }
}
