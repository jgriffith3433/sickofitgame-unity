using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core.Audio;
using SickOfIt.Core.Progression;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The level-clear celebration (Docs/Story.md "The level-clear screen"; run it on device "phone", 390x844, touch): level 3 cleared twice by
        /// the bot. Clear one is left to play out: five rows tick up one after another (a tick sound each), the rank stamp slams within 3 s with
        /// embers and its sound, Yooba speaks the verdict for the rank, Continue appears after about 2 s and is 48+ px tall, everything sits in the
        /// safe area. Clear two is skipped by a finger tap: the end state is on screen within 0.3 s. The two clears of one rank must not
        /// repeat the same verdict. Every tap is a finger.
        /// </summary>
        public const string LevelClearId = "level-clear";
        const float LevelClearSeconds = 900f;

        sealed partial class Session
        {
            int levelClearFailed;
            string levelClearRank = "";
            string levelClearVerdict = "";

            void LevelClearCheck(string beat, bool ok, string detail)
            {
                if (!ok) levelClearFailed++;
                Run.Event("check", $"level-clear {beat}: {(ok ? "ok" : "FAILED")} ({detail})");
                if (!ok)
                    Run.Problem(PlaytestSeverity.Bug, $"level-clear {beat}: {detail}");
                Run.Metric("levelClear.failed", levelClearFailed);
            }

            public IEnumerator LevelClearRun()
            {
                var definition = GameDefinition.Instance;
                var texts = definition.menuTheme.texts;
                int level = WaystoneLevel;

                // Data: two voiced verdicts for each of the four ranks, in Yooba's voice, with a clip.
                var missing = new List<string>();
                foreach (var r in new[] { "s", "a", "b", "c" })
                    for (int n = 1; n <= 2; n++)
                        if (!Sfx.Has($"vo_yooba_rank_{r}_{n}"))
                            missing.Add($"vo_yooba_rank_{r}_{n}");
                LevelClearCheck("verdicts", missing.Count == 0, missing.Count == 0 ? "8 voiced verdicts (2 per rank) have clips" : "no clip: " + string.Join(", ", missing));
                LevelClearCheck("sounds", Sfx.Has("ui_rank_stamp") && Sfx.Has("ui_tally_tick") && Sfx.Has("ui_new_best"), "stamp, tally tick and new-best sounds have clips");

                yield return ToMainMenu();
                if (Run.Done) yield break;
                RogueliteManager.Instance.ResetAllRogueliteData();
                GameProgressManager.Instance.InitializeNewPlayer();
                GameProgressManager.Instance.SetLevelCompleted(1);
                GameProgressManager.Instance.SetLevelCompleted(LandingLevel);
                ShaftProgress.Floor = LandingLastFloor;
                ShaftProgress.Plunged = true;
                YoobaProgress.Met = true;
                WaystoneProgress.Reach(definition.shaft != null ? definition.shaft.bottomFloor : LandingFirstFloor);

                var played = new List<Sfx.PlayedSound>();
                System.Action<Sfx.PlayedSound> onPlayed = ps => played.Add(ps);
                Sfx.Played += onPlayed;
                for (int pass = 1; pass <= 2; pass++)
                {
                    bool skipPass = pass == 2;
                    played.Clear();
                    Run.Event("shortcut", $"level {level} loaded directly; clear {pass}: {(skipPass ? "a finger tap skips the tally" : "left to play out")}");
                    var load = LoadLevel(level);
                    if (load != null)
                        yield return load;
                    yield return PlayLevel(level, 0f, null, 0.1f + 0.4f * (pass - 1), 0.5f * pass, 300f);
                    if (Run.Done) { Sfx.Played -= onPlayed; yield break; }
                    yield return Run.Until(() => GameplayWorld.Instance == null || GameplayWorld.Instance.Hud == null || GameplayWorld.Instance.Hud.ResultsOpen,
                        30f, "the results screen");
                    if (Run.Done) { Sfx.Played -= onPlayed; yield break; }
                    var hud = GameplayWorld.Instance != null ? GameplayWorld.Instance.Hud : null;
                    if (hud == null || !hud.ResultsOpen)
                    {
                        LevelClearCheck($"results {pass}", false, $"the level ended {lastOutcome} with no results screen");
                        break;
                    }
                    float openedAt = Time.unscaledTime;
                    int firstTick = played.Count;
                    float skippedAt = -1f;
                    if (skipPass)
                    {
                        yield return WaitReal(0.4f);
                        var tap = new Vector2(Screen.width * 0.5f, Screen.height * 0.62f);
                        skippedAt = Time.unscaledTime;
                        int finger = input.TouchDown(tap);
                        yield return WaitReal(0.05f);
                        input.TouchUp(finger, tap);
                        yield return Run.Until(() => hud.ResultsStampSeconds >= 0f, 2f, "the stamp after the tap");
                        float took = Time.unscaledTime - skippedAt;
                        Run.Metric("levelClear.tapToEndSeconds", took);
                        LevelClearCheck("tap skips", hud.ResultsStampSeconds >= 0f && took <= 0.3f, $"a tap {0.4f:0.0} s in put the stamp on screen {took:0.00} s later");
                    }
                    else
                    {
                        yield return Run.Until(() => hud.ResultsStampSeconds >= 0f, 6f, "the rank stamp");
                    }
                    if (Run.Done) { Sfx.Played -= onPlayed; yield break; }

                    // The tally: five rows in order, a tick for each (not in the skipped pass: the tap cut them short).
                    string rows = hud.ResultsSummary;
                    var order = new[] { texts.resultsTime, texts.resultsCleanKills, texts.resultsGrazes, texts.resultsRewinds, texts.resultsDiamonds };
                    int at = -1;
                    bool inOrder = true;
                    foreach (var label in order)
                    {
                        int found = rows.IndexOf(label, System.StringComparison.Ordinal);
                        if (found < at || found < 0) inOrder = false;
                        at = found;
                    }
                    Run.Event("results", rows);
                    LevelClearCheck($"rows {pass}", inOrder, rows);
                    var ticks = new List<float>();
                    for (int i = firstTick; i < played.Count; i++)
                        if (played[i].id == "ui_tally_tick")
                            ticks.Add(played[i].time);
                    if (!skipPass)
                    {
                        float gap = 99f;
                        for (int i = 1; i < Mathf.Min(5, ticks.Count); i++)
                            gap = Mathf.Min(gap, ticks[i] - ticks[i - 1]);
                        LevelClearCheck("ticks", ticks.Count >= 5 && gap >= 0.2f && gap <= 0.45f, $"{ticks.Count} tick sounds, the closest two {gap:0.00} s apart (0.3 s a row)");
                    }
                    Run.Metric("levelClear.stampSeconds." + pass, hud.ResultsStampSeconds);
                    LevelClearCheck($"stamp {pass}", hud.ResultsStampSeconds > 0f && hud.ResultsStampSeconds <= 3f && hud.ResultsEmbers > 0,
                        $"rank {hud.ResultsRank} slammed {hud.ResultsStampSeconds:0.00} s after the screen opened, {hud.ResultsEmbers} embers thrown");
                    yield return WaitReal(0.3f);
                    bool stampSound = played.Exists(p => p.id == Sfx.Events.uiRankStamp || p.id == Sfx.Events.gradeReveal);
                    LevelClearCheck($"stamp sound {pass}", stampSound, "the stamp sound started with the slam");

                    // Yooba's verdict: the one for this rank, heard, in his portrait.
                    string rank = hud.ResultsRank.Length > 0 ? hud.ResultsRank.ToLowerInvariant() : "";
                    string key = rank == "s" || rank == "a" || rank == "b" ? rank : "c";
                    string verdict = hud.ResultsVerdict;
                    yield return Run.Until(() => played.Exists(p => p.id == verdict), 3f, "Yooba's verdict");
                    bool heard = played.Exists(p => p.id == verdict);
                    LevelClearCheck($"verdict {pass}", heard && verdict.StartsWith("vo_yooba_rank_" + key + "_"), $"rank {hud.ResultsRank}: {verdict} {(heard ? "heard" : "NOT heard")}");
                    Run.Event("line", $"yooba (rank {hud.ResultsRank}): {Sfx.SpokenText(verdict)} ({verdict})");
                    if (pass == 1)
                    {
                        levelClearRank = hud.ResultsRank;
                        levelClearVerdict = verdict;
                    }
                    else
                        LevelClearCheck("verdict varies", levelClearRank != hud.ResultsRank || levelClearVerdict != verdict,
                            levelClearRank == hud.ResultsRank ? $"rank {levelClearRank} twice: {levelClearVerdict} then {verdict}" : $"ranks {levelClearRank} then {hud.ResultsRank}: {levelClearVerdict}, {verdict}");
                    if (hud.ResultsNewBestShown)
                        Run.Event("newBest", $"NEW BEST banner shown on clear {pass}");

                    // Diamonds fly into the total once the stamp is down.
                    yield return WaitReal(1.4f);
                    LevelClearCheck($"total {pass}", hud.ResultsTotalShown >= 0, $"the diamond total reads {hud.ResultsTotalShown}, {hud.ResultsDiamondsFlown} diamonds flew in");

                    // The phone: Continue after about 2 s (or the tap), 48+ px tall, the words inside the safe area.
                    yield return Run.Until(() => hud.ResultsContinuePixels > 0f, 3f, "Continue");
                    float appeared = Time.unscaledTime - openedAt;
                    float height = hud.ResultsContinuePixels;
                    Run.Metric("levelClear.continuePx." + pass, height);
                    LevelClearCheck($"continue {pass}", height >= 48f && (skipPass || appeared >= 1.8f && appeared <= 3.5f),
                        $"Continue {height:0} px tall, there {appeared:0.0} s after the screen opened");
                    string outside = hud.ResultsOutsideSafeArea(Screen.safeArea);
                    LevelClearCheck($"safe area {pass}", outside.Length == 0, outside.Length == 0 ? $"every word inside the safe area at {Screen.width}x{Screen.height}" : outside);

                    var button = FindButton(texts.continueText);
                    if (button != null)
                    {
                        var where = ScreenPoint(button);
                        int tapId = input.TouchDown(where);
                        yield return WaitReal(0.05f);
                        input.TouchUp(tapId, where);
                    }
                    yield return Run.Until(() => GameplayWorld.Instance == null || GameplayWorld.Instance.Hud == null || !GameplayWorld.Instance.Hud.ResultsOpen, 20f, "Continue to take effect");
                    if (Run.Done) { Sfx.Played -= onPlayed; yield break; }
                    Run.Progress(0.5f * pass);
                }
                Sfx.Played -= onPlayed;
                End(levelClearFailed == 0 ? PlaytestOutcome.Won : PlaytestOutcome.Done);
            }
        }
    }
}
