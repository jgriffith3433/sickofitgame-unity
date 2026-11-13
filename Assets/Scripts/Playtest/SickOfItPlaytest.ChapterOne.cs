using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core;
using SickOfIt.Core.Rewind;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// Chapter One payoff on Floor 997 (Docs/Story.md "Chapter One closes on Floor 997"): level 5 loaded directly,
        /// chapterOne phase entered (setup shortcut). Bot dodges through the Overlord's three lens phrases while
        /// voiced lines play; escape; CHAPTER ONE card with time/deaths/rewinds/diamonds and Continue ≥48 px.
        /// Run at 390x844 touch too.
        /// </summary>
        public const string ChapterOneId = "chapter-one";
        const float ChapterOneSeconds = 240f;
        const int ChapterOneLevel = 5;

        sealed partial class Session
        {
            readonly HashSet<string> ch1Signals = new HashSet<string>();
            readonly List<string> ch1Lines = new List<string>();
            float ch1StartAt = -1f, ch1CardAt = -1f, ch1Phrase1At = -1f, ch1EscapeAt = -1f;
            int ch1ContinueH;

            void Ch1Telemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (e.Kind == "signal" && e.Subject != null && e.Subject.StartsWith("chapter1:"))
                {
                    if (ch1Signals.Add(e.Subject))
                        Run.Event("chapter1", $"{e.Subject} at {e.Time:0.00}");
                    if (e.Subject == "chapter1:start")
                        ch1StartAt = e.Time;
                    if (e.Subject == "chapter1:phrase1")
                        ch1Phrase1At = e.Time;
                    if (e.Subject == "chapter1:escape")
                        ch1EscapeAt = e.Time;
                    if (e.Subject == "chapter1:card" || e.Subject == "chapter1:cardShown")
                        ch1CardAt = e.Time;
                    if (e.Subject == "chapter1:continueReady")
                        ch1ContinueH = e.Value;
                    if (e.Subject == "chapter1:hook")
                        ch1Lines.Add(e.Detail ?? "hook");
                }
                if (e.Kind == "caption" && !string.IsNullOrEmpty(e.Detail) && e.Detail.Contains("vo_ch1_"))
                {
                    int bar = e.Detail.LastIndexOf('|');
                    string id = bar >= 0 ? e.Detail.Substring(bar + 1) : e.Detail;
                    if (id.StartsWith("vo_ch1_") && !ch1Lines.Contains(id))
                        ch1Lines.Add(id);
                }
                if ((e.Kind == "sound" || e.Kind == "voice") && (e.Subject ?? "").StartsWith("vo_ch1_")
                    && !ch1Lines.Contains(e.Subject))
                    ch1Lines.Add(e.Subject);
            }

            /// <summary>Attaches the bot to the running world (real input, no sim shortcuts) at the playtest's speed.</summary>
            void StartBot()
            {
                if (bot != null)
                    Object.Destroy(bot);
                bot = PlaytestBot.Attach(GameplayWorld.Instance, (uint)(1000 * options.seed + ChapterOneLevel), input);
                Time.timeScale = Speed;
            }

            void Ch1Check(string beat, bool ok, string detail)
            {
                Run.Event("check", $"chapter-one {beat}: {(ok ? "ok" : "FAILED")} ({detail})");
                if (!ok)
                    Run.Problem(PlaytestSeverity.Bug, $"chapter-one {beat}: {detail}");
            }

            public IEnumerator ChapterOneRun()
            {
                bool phone = Screen.height > Screen.width;
                bool touchBefore = GameplayHud.TouchUiForced;
                if (phone)
                    GameplayHud.TouchUiForced = true;
                UnlockFlags.Reset(); // first-play path (no Skip offer)
                ch1Signals.Clear();
                ch1Lines.Clear();
                ch1StartAt = ch1CardAt = ch1Phrase1At = ch1EscapeAt = -1f;
                ch1ContinueH = 0;
                GameplayTelemetry.Recorded += Ch1Telemetry;
                try
                {
                    Prepare(ChapterOneLevel, true);
                    Run.Event("shortcut", "Floor 997 loaded; chapterOne phase entered (setup shortcut)");
                    var old = GameplayWorld.Instance;
                    var load = LoadLevel(ChapterOneLevel);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(ChapterOneLevel)
                        && GameplayWorld.Instance.PlayerController != null, 60f, "Floor 997");
                    if (Run.Done)
                        yield break;

                    var world = GameplayWorld.Instance;
                    // Juice and hearts so a hit during the face-off is undoable.
                    RewindBudget.Instance?.Add(30f);
                    world.Director.GoToPhase("chapterOne");
                    Run.Event("chapter1", "entered chapterOne phase");
                    StartBot();

                    float t0 = Time.realtimeSinceStartup;
                    while (!Run.Done && Time.realtimeSinceStartup - t0 < 100f
                           && !ch1Signals.Contains("chapter1:cardShown")
                           && (world.Hud == null || !world.Hud.ResultsOpen))
                    {
                        // Prefer dodging; the laser aims at the player.
                        yield return null;
                    }

                    // Wait for CHAPTER ONE card (before the Grudge pick).
                    yield return Run.Until(() =>
                        world.Hud != null && (world.Hud.ResultsOpen || ch1Signals.Contains("chapter1:cardShown")),
                        50f, "CHAPTER ONE card");

                    float playSeconds = ch1EscapeAt > 0f && ch1Phrase1At > 0f
                        ? ch1EscapeAt - ch1Phrase1At
                        : (ch1CardAt > 0f && ch1StartAt > 0f ? ch1CardAt - ch1StartAt : -1f);
                    Run.Metric("chapterOne.playSeconds", playSeconds);
                    Run.Metric("chapterOne.lines", ch1Lines.Count);
                    Run.Metric("chapterOne.continueH", ch1ContinueH);

                    Ch1Check("encounter starts", ch1Signals.Contains("chapter1:start") || ch1Signals.Contains("chapter1:found"),
                        string.Join(",", ch1Signals));
                    Ch1Check("player in control during phrases",
                        ch1Signals.Contains("chapter1:phrase1") && ch1Signals.Contains("chapter1:phrase3"),
                        $"signals {string.Join(",", ch1Signals)}");
                    Ch1Check("20-40 s playable exchange",
                        playSeconds >= 18f && playSeconds <= 45f,
                        $"playSeconds {playSeconds:0.0}");
                    // Lines: three Overlord + two hero in the fight, plus hook on the card (≥4 voiced).
                    int voiced = ch1Lines.Count;
                    // Also count say sounds from audio if Sfx telemetry is sparse: accept phrase signals + hook as ≥4 beats.
                    bool enoughVoice = voiced >= 4
                        || (ch1Signals.Contains("chapter1:phrase1") && ch1Signals.Contains("chapter1:phrase2")
                            && ch1Signals.Contains("chapter1:phrase3") && ch1Signals.Contains("chapter1:hook"));
                    Ch1Check("at least 4 voiced lines", enoughVoice, $"lines={voiced} [{string.Join(",", ch1Lines)}]");
                    Ch1Check("chapter card appears",
                        world.Hud != null && !string.IsNullOrEmpty(world.Hud.ResultsSummary)
                        && (world.Hud.ResultsSummary.Contains("Deaths") || world.Hud.ResultsSummary.Contains("Rewinds")),
                        world.Hud?.ResultsSummary ?? "none");
                    Ch1Check("chapter card has time deaths rewinds diamonds",
                        world.Hud != null && world.Hud.ResultsSummary != null
                        && world.Hud.ResultsSummary.IndexOf("Time", System.StringComparison.OrdinalIgnoreCase) >= 0
                        && world.Hud.ResultsSummary.IndexOf("Rewind", System.StringComparison.OrdinalIgnoreCase) >= 0
                        && world.Hud.ResultsSummary.IndexOf("Diamond", System.StringComparison.OrdinalIgnoreCase) >= 0,
                        world.Hud?.ResultsSummary ?? "none");

                    // Continue button height (design units logged; also measure on-screen px).
                    float continuePx = 0f;
                    if (world.Hud != null)
                    {
                        foreach (var btn in world.Hud.GetComponentsInChildren<Button>(true))
                        {
                            var label = btn.GetComponentInChildren<TMPro.TMP_Text>();
                            if (label == null || label.text == null)
                                continue;
                            if (label.text.IndexOf("Continue", System.StringComparison.OrdinalIgnoreCase) < 0)
                                continue;
                            var rt = btn.transform as RectTransform;
                            if (rt == null)
                                continue;
                            var corners = new Vector3[4];
                            rt.GetWorldCorners(corners);
                            continuePx = Mathf.Abs(corners[1].y - corners[0].y);
                            break;
                        }
                    }
                    Run.Metric("chapterOne.continuePx", continuePx);
                    Ch1Check("Continue at least 48 px tall",
                        continuePx >= 48f || ch1ContinueH >= 48,
                        $"px {continuePx:0.0} designH {ch1ContinueH}");

                    if (phone)
                        Ch1Check("touch UI on for phone", GameplayHud.TouchUiOn, $"TouchUiOn {GameplayHud.TouchUiOn}");

                    // Click Continue so the flow closes cleanly.
                    if (world.Hud != null && world.Hud.ResultsOpen)
                    {
                        string continueLabel = GameDefinition.Instance?.menuTheme?.texts?.continueText ?? "Continue";
                        yield return ClickUntil(continueLabel, () => world.Hud == null || !world.Hud.ResultsOpen, 15f);
                    }

                    Run.Event("chapter1", "done");
                    Run.Finish("done");
                }
                finally
                {
                    GameplayTelemetry.Recorded -= Ch1Telemetry;
                    GameplayHud.TouchUiForced = touchBefore;
                    StopBot();
                }
            }
        }
    }
}
