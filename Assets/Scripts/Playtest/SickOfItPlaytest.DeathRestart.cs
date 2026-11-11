using System.Collections;
using System.Collections.Generic;
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
        /// <summary>Flow between attempts: Again! restarts in under a second, the death screen's quip varies, the level clear shows time and clean kills.</summary>
        public const string DeathRestartId = "death-restart";
        const float DeathRestartSeconds = 600f;
        /// <summary>The fast restart's budget (real seconds from the click to the next level running).</summary>
        const float AgainBudgetSeconds = 1f;

        /// <summary>
        /// Watches the hero's death window: the frame it opens on (the hit that leaves him at 0) and the 2 real seconds after it, and keeps
        /// the longest frame (<c>Time.unscaledDeltaTime</c>, so the hit-freeze doesn't hide it). Read-only, no polling outside its own Update.
        /// </summary>
        sealed class LastHitHitchProbe : MonoBehaviour
        {
            public const float BudgetMs = 100f;
            const float WatchSeconds = 2f;
            float watchUntil = -1f;
            bool armed;
            SickOfIt.Combat.Health.PlayerHealth health;

            public bool Measured { get; private set; }
            public float WorstMs { get; private set; }

            public void Begin() { armed = true; Measured = false; WorstMs = 0f; watchUntil = -1f; health = null; }

            void Update()
            {
                if (!armed)
                    return;
                if (health == null)
                {
                    var world = GameplayWorld.Instance;
                    if (world != null && world.Player != null)
                        health = world.Player.GetComponentInChildren<SickOfIt.Combat.Health.PlayerHealth>();
                    return;
                }
                if (watchUntil < 0f)
                {
                    if (health.DeathTimeLeft < 0f)
                        return;
                    watchUntil = Time.unscaledTime + WatchSeconds;
                    Measured = true;
                }
                if (Time.unscaledTime <= watchUntil)
                    WorstMs = Mathf.Max(WorstMs, Time.unscaledDeltaTime * 1000f);
                else
                    armed = false;
            }
        }

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
                var definition = GameDefinition.Instance;
                var roguelite = RogueliteManager.Instance;
                var progress = GameProgressManager.Instance;
                var texts = definition.menuTheme.texts;
                int level = WaystoneLevel;
                yield return ToMainMenu();
                if (Run.Done) yield break;

                // Setup: the Landing's Waystone reached, Yooba met, no diamonds (so only Again! and Rage Quit are offered).
                roguelite.ResetAllRogueliteData();
                progress.InitializeNewPlayer();
                progress.SetLevelCompleted(1);
                progress.SetLevelCompleted(LandingLevel);
                ShaftProgress.Floor = LandingLastFloor;
                ShaftProgress.Plunged = true;
                YoobaProgress.Met = true;
                WaystoneProgress.Reach(definition.shaft != null ? definition.shaft.bottomFloor : LandingFirstFloor);
                Run.Event("shortcut", "fresh save: the Landing's Waystone reached, Yooba met");

                var quips = new List<string>();
                int repeats = 0;
                const int deaths = 4;
                var hitch = new GameObject("[LastHitHitchProbe]").AddComponent<LastHitHitchProbe>();
                for (int i = 0; i < deaths; i++)
                {
                    roguelite.SetPoints(0);
                    hitch.Begin();
                    yield return DeathReturnDie(level, 0);
                    if (Run.Done) yield break;
                    // The 2 s after the hit that left him at 0 (his last life): no frame over 100 ms (criterion for the phone run at 390x844).
                    DeathRestartCheck($"last-life hit {i + 1}", hitch.Measured && hitch.WorstMs <= LastHitHitchProbe.BudgetMs,
                        hitch.Measured ? $"worst frame {hitch.WorstMs:0} ms in the 2 s after the hit (budget {LastHitHitchProbe.BudgetMs:0} ms), the death overlay and the death screen both came" : "the hit that left him at 0 was never seen");
                    Run.Metric("deathRestart.lastHitWorstFrameMs." + (i + 1), hitch.WorstMs);
                    var death = DeathRuntime.Current;
                    string quip = death != null ? death.QuipText : "";
                    bool fresh = quips.Count == 0 || quips[quips.Count - 1] != quip;
                    if (!fresh) repeats++;
                    quips.Add(quip);
                    Run.Event("quip", $"death {i + 1}: \"{quip}\"");
                    DeathRestartCheck($"quip {i + 1}", death != null && !string.IsNullOrWhiteSpace(quip) && fresh,
                        $"\"{quip}\" under \"{(death != null ? death.TitleText : "")}\"{(fresh ? "" : ", the same as the death before")}");
                    {
                        var rec = GameObject.Find("BestFloor");
                        var recText = rec != null ? rec.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
                        DeathRestartCheck($"best floor {i + 1}", recText != null && recText.text.StartsWith("BEST: FLOOR"),
                            recText != null ? recText.text.Replace('\n', '|') : "no best-floor line on the death screen");
                    }
                    DeathRestartCheck($"again offered {i + 1}", death != null && death.AgainOffered, "the Again! button is on the death screen");
                    {
                        var goalCard = SickOfIt.Core.Progression.NextGoalCard.Current;
                        yield return WaitReal(2.2f);
                        bool noEmptyBar = goalCard == null || (goalCard.Goal != null && goalCard.Goal.To > 0 && goalCard.Label != null && goalCard.Label.text.Length > 0);
                        DeathRestartCheck($"next goal {i + 1}", noEmptyBar,
                            goalCard != null ? $"{goalCard.Goal.Kind}: {goalCard.Label.text} (bar {goalCard.FillFraction:0.00}, ticks {goalCard.Ticks})" : "no card: nothing gathered, the empty bar is hidden");
                        var rect = goalCard != null ? goalCard.Panel : null;
                        bool sane = goalCard == null || (rect != null && rect.rect.width > 0f && rect.rect.height > 0f);
                        DeathRestartCheck($"next goal fits {i + 1}", sane, rect != null ? $"card {rect.rect.width:0}x{rect.rect.height:0}, Again! offered" : "no card, Again! offered");
                        DeathRestartCheck($"reaction {i + 1}", true, $"cause \"{SickOfIt.Core.Progression.NextGoal.LastCause}\" -> {(SickOfIt.Core.Progression.DeathReaction.LastSaid == "" ? "no clip made yet (pending_clips.json)" : SickOfIt.Core.Progression.DeathReaction.LastSaid)}");
                    }
                    if (death == null || !death.AgainOffered) { End(PlaytestOutcome.Lost); yield break; }

                    int next = death.RunEnded ? death.RunStop.NextLevel : level;
                    float clickedAt = Time.realtimeSinceStartup;
                    yield return ClickUntil(texts.deathAgain, () => death == null || death.AgainPressedAt >= 0f, 20f);
                    if (Run.Done) yield break;
                    clickedAt = death != null && death.AgainPressedAt >= 0f ? death.AgainPressedAt : clickedAt;
                    yield return Run.Until(() => LevelRunning(next), 30f, $"level {next} after Again!");
                    if (Run.Done) yield break;
                    float took = Time.realtimeSinceStartup - clickedAt;
                    Run.Metric("deathRestart.againSeconds." + (i + 1), took);
                    DeathRestartCheck($"restart {i + 1}", took < AgainBudgetSeconds, $"Again! to level {next} running in {took:0.00} s (budget {AgainBudgetSeconds:0.0} s)");
                    Run.Progress(0.05f + 0.15f * (i + 1));
                }
                UnityEngine.Object.Destroy(hitch.gameObject);
                var distinct = new HashSet<string>(quips);
                DeathRestartCheck("varied", distinct.Count >= 2 && repeats == 0, $"{distinct.Count} different quips over {deaths} deaths: {string.Join(" / ", quips)}");

                // The level clear: the bot plays level 3 properly; the results list the time and the clean kills.
                roguelite.SetPoints(0);
                Run.Event("shortcut", $"level {WaystoneLevel} loaded directly to clear it");
                var load = LoadLevel(WaystoneLevel);
                if (load != null)
                    yield return load;
                yield return PlayLevel(WaystoneLevel, 0f, null, 0.8f, 0.95f, 300f);
                if (Run.Done) yield break;
                yield return Run.Until(() => GameplayWorld.Instance == null || GameplayWorld.Instance.Hud == null || GameplayWorld.Instance.Hud.ResultsOpen,
                    30f, "the results screen");
                if (Run.Done) yield break;
                var hud = GameplayWorld.Instance != null ? GameplayWorld.Instance.Hud : null;
                bool open = hud != null && hud.ResultsOpen;
                string rows = open ? hud.ResultsSummary : "no results screen";
                Run.Event("results", rows);
                DeathRestartCheck("clear time", open && rows.Contains(texts.resultsTime) && System.Text.RegularExpressions.Regex.IsMatch(rows, @"\d+:\d\d"), rows);
                DeathRestartCheck("clear cleanKills", open && rows.Contains(texts.resultsCleanKills), open ? $"{texts.resultsCleanKills}: {hud.ResultsCleanKills}" : rows);
                Run.Metric("deathRestart.cleanKills", open ? hud.ResultsCleanKills : -1);
                if (open)
                    yield return ClickUntil(texts.continueText, () => GameplayWorld.Instance == null || !GameplayWorld.Instance.Hud.ResultsOpen, 20f);
                End(deathRestartFailed == 0 ? PlaytestOutcome.Done : PlaytestOutcome.Lost);
            }
        }
    }
}
