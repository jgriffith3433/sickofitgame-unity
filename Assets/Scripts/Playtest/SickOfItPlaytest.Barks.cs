using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Hawkes.Playtest;
using UnityEngine.InputSystem;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Core.Rewind;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// hero-barks: the hero talks back (Docs/Story.md "The hero talks back", <see cref="HeroBarks"/>). Level 1 (the Meadow: imps, kills, grazes) and Levels 2-4
        /// (Floors 1000-998) are loaded directly (setup shortcut; the graze distance is widened as in the style scenario, the bot's dodging still decides what
        /// grazes) and played by the bot; a session's barks are collected from the telemetry. Then on Floor 998 the bot plays to lose once the cooldown is open
        /// and rewinds the death (R, or a finger on the HUD button on touch: run it at 390x844). Checks: 6+ different barks of 4+ events, each caption on screen
        /// ~1.5 s; no line twice and every pair of barks 15+ s apart; a graze streak of 5 (cooldown open) and the undone death each bark within 1 s; on touch
        /// the caption stays inside the screen and off the hero, the rewind button and the stick's zone. check events hero-barks &lt;beat&gt;; events bark, bark-spoken;
        /// metrics heroBarks.*
        /// </summary>
        public const string HeroBarksId = "hero-barks";
        const float HeroBarksSeconds = 900f;

        sealed partial class Session
        {
            int barksFailed;
            readonly List<(float at, int value)> barkGrazes = new List<(float, int)>();
            readonly List<float> barkSpoken = new List<float>();
            readonly List<string> barkWhere = new List<string>();
            float barkShownSince = -1f;
            string barkShownText = "";
            float barkLongest;
            int barkPhoneBad;
            string barkPhoneDetail = "";
            int barkCaptionsSeen;

            void BarkCheck(string beat, bool ok, string detail, bool warnOnly = false)
            {
                Run.Event("check", $"hero-barks {beat}: {(ok ? "ok" : "FAILED")} ({detail})");
                if (!ok)
                {
                    if (!warnOnly) barksFailed++;
                    Run.Problem(warnOnly ? PlaytestSeverity.Warning : PlaytestSeverity.Bug, $"hero-barks {beat}: {detail}");
                }
            }

            void BarkTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                switch (e.Kind)
                {
                    case "graze":
                        if (e.Value == 5) barkGrazes.Add((Time.unscaledTime, e.Value));
                        break;
                    case "bark":
                        Run.Event("bark", $"{e.Subject}: {e.Detail} \"{SickOfIt.Core.Audio.Sfx.SpokenText(e.Detail)}\" on floor {e.Value}");
                        barkWhere.Add(e.Subject);
                        break;
                    case "barkSpoken":
                        barkSpoken.Add(Time.unscaledTime);
                        Run.Event("bark-spoken", $"{e.Subject}: {e.Detail}");
                        break;
                }
            }

            /// <summary>Per frame while a level plays: how long each caption stays, and on touch where the bubble is.</summary>
            void BarkWatch()
            {
                var world = GameplayWorld.Instance;
                var hud = world != null ? world.Hud : null;
                if (hud == null)
                    return;
                string shown = hud.BarkShown;
                float now = Time.unscaledTime;
                if (shown.Length > 0)
                {
                    if (barkShownSince < 0f || shown != barkShownText)
                    {
                        barkShownSince = now;
                        barkShownText = shown;
                        barkCaptionsSeen++;
                    }
                    barkLongest = Mathf.Max(barkLongest, now - barkShownSince);
                    if (hud.TryGetBarkScreenRects(out var bubble, out var hero))
                    {
                        var screen = new Rect(0f, 0f, Screen.width, Screen.height);
                        var bad = new List<string>();
                        if (bubble.xMin < screen.xMin - 1f || bubble.xMax > screen.xMax + 1f || bubble.yMin < screen.yMin - 1f || bubble.yMax > screen.yMax + 1f)
                            bad.Add("off the screen");
                        if (bubble.Overlaps(hero)) bad.Add("over the hero");
                        var rewind = hud.RewindButtonScreenRect;
                        if (rewind.width > 1f && bubble.Overlaps(rewind)) bad.Add("over the rewind button");
                        if (hud.TouchStickShown && bubble.Overlaps(hud.TouchStickZoneScreenRect)) bad.Add("over the touch stick");
                        if (bad.Count > 0)
                        {
                            barkPhoneBad++;
                            barkPhoneDetail = $"\"{shown}\" {string.Join(", ", bad)} (bubble {bubble}, hero {hero}, screen {Screen.width}x{Screen.height})";
                        }
                    }
                }
                else
                {
                    barkShownSince = -1f;
                }
            }

            IEnumerator BarkLoad(int level)
            {
                var load = LoadLevel(level);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => LevelRunning(level) && RewindBudget.Instance != null, 60f, "the level");
            }

            public IEnumerator HeroBarksRun()
            {
                yield return ToMainMenu();
                if (Run.Done) yield break;
                HeroBarks.ResetSession();
                GameplayTelemetry.Recorded += BarkTelemetry;
                try
                {
                    // Phase 1: the Meadow, then Floors 1000-998, played by the bot.
                    int[] levels = { 1, 2, 3, 4 };
                    foreach (int level in levels)
                    {
                        if (Run.Done) yield break;
                        Run.Event("shortcut", $"level {level} loaded directly");
                        yield return BarkLoad(level);
                        if (Run.Done) yield break;
                        var world = GameplayWorld.Instance;
                        if (world != null && world.Style != null)
                        {
                            world.Style.Config.grazeDistance = 2.5f;
                            world.Style.Config.streakWindow = 8f;
                            Run.Event("shortcut", "grazeDistance 2.5, streakWindow 8 s");
                        }
                        int before = HeroBarks.Log.Count;
                        // Level 4 is left for the death phase after 70 s; the others play until they have spoken a few lines or run 120 s.
                        float cap = level == 1 ? 130f : 75f;
                        bool deathPhase = level == 4;
                        bool started = false, finished = false;
                        float levelStart = Time.unscaledTime;
                        yield return PlayLevel(level, 0f, now =>
                        {
                            BarkWatch();
                            if (!deathPhase || started)
                                return null;
                            // The cooldown must be open and the floor's arrival bark done before he dies, so the undone death can answer.
                            var w = GameplayWorld.Instance;
                            var hero = w != null ? w.Player : null;
                            var health = hero != null ? hero.GetComponentInChildren<PlayerHealth>() : null;
                            reckless = Time.unscaledTime - HeroBarks.LastAt > HeroBarks.Cooldown + 1f && Time.unscaledTime - levelStart > 8f;
                            if (health == null || health.DeathTimeLeft < 0.4f)
                                return null;
                            started = true;
                            reckless = false;
                            return BarkRewindDeath(() => finished = true);
                        }, 0f, 1f, cap + (deathPhase ? 120f : 0f), until: () => deathPhase ? finished : Time.unscaledTime - levelStart > cap);
                        reckless = false;
                        Run.Event("check", $"hero-barks level {level}: {HeroBarks.Log.Count - before} bark(s) here, {HeroBarks.Log.Count} in the session");
                        if (deathPhase)
                            BarkCheck("the death happened", started, started ? "died with the undo window open" : "the bot never died after the cooldown opened");
                    }
                    if (Run.Done) yield break;

                    // Phase 2: eight quiet seconds, once the cooldown is open (idle bark).
                    if (GameplayWorld.Instance != null && GameplayWorld.Instance.Player != null)
                    {
                        // Level 4 ended by its death phase: load Floor 1000 again for a calm place to stand.
                        yield return BarkLoad(2);
                        if (Run.Done) yield break;
                        yield return Run.Until(() => Time.unscaledTime - HeroBarks.LastAt > HeroBarks.Cooldown + 0.5f, 40f, "the cooldown");
                        StopBot();
                        input.ReleaseAll();
                        int idleBefore = HeroBarks.Counts.TryGetValue("idle", out int n0) ? n0 : 0;
                        for (float end = Time.realtimeSinceStartup + 14f; Time.realtimeSinceStartup < end && !Run.Done;)
                        {
                            BarkWatch();
                            yield return null;
                            if ((HeroBarks.Counts.TryGetValue("idle", out int n1) ? n1 : 0) > idleBefore) break;
                        }
                        int idleNow = HeroBarks.Counts.TryGetValue("idle", out int n2) ? n2 : 0;
                        BarkCheck("idle for 8 s", idleNow > idleBefore, idleNow > idleBefore ? HeroBarks.LastLine : "stood still 14 s with no bark (he may have been hurt or the floor moved him)", warnOnly: true);
                        yield return WaitReal(1.8f);
                        BarkWatch();
                    }
                }
                finally
                {
                    GameplayTelemetry.Recorded -= BarkTelemetry;
                    input.ReleaseAll();
                    reckless = false;
                }
                if (Run.Done) yield break;

                // Verdicts.
                var log = HeroBarks.Log;
                var lines = new HashSet<string>();
                var events = new HashSet<string>();
                bool repeat = false, tooClose = false;
                float minGap = float.MaxValue;
                for (int i = 0; i < log.Count; i++)
                {
                    if (!lines.Add(log[i].line)) repeat = true;
                    events.Add(log[i].key);
                    if (i > 0)
                    {
                        float gap = log[i].at - log[i - 1].at;
                        minGap = Mathf.Min(minGap, gap);
                        if (gap < HeroBarks.Cooldown - 0.05f) tooClose = true;
                    }
                }
                BarkCheck("6+ different barks", lines.Count >= 6, $"{lines.Count} different lines from {events.Count} events: {string.Join(", ", events)}");
                BarkCheck("barks from 4+ events", events.Count >= 4, string.Join(", ", events));
                BarkCheck("no line twice", !repeat, $"{log.Count} barks, {lines.Count} different");
                BarkCheck("15 s between barks", !tooClose, log.Count > 1 ? $"closest pair {minGap:0.0} s apart" : "fewer than two barks");
                BarkCheck("every bark has a caption", barkSpoken.Count >= log.Count - 1 && barkCaptionsSeen >= 1, $"{barkSpoken.Count} captions started, {barkCaptionsSeen} seen on the HUD for {log.Count} barks");
                BarkCheck("the caption lasts about 1.5 s", barkCaptionsSeen == 0 || (barkLongest >= 1.2f && barkLongest <= 2.2f), $"longest seen {barkLongest:0.00} s");

                // A graze streak of 5 with the cooldown open must bark within 1 s.
                int open = 0, answered = 0;
                foreach (var (at, value) in barkGrazes)
                {
                    float lastBefore = -999f;
                    foreach (var entry in log)
                        if (entry.at < at - 0.01f && entry.at > lastBefore) lastBefore = entry.at;
                    bool openGate = at - lastBefore >= HeroBarks.Cooldown;
                    bool barked = false;
                    foreach (var entry in log)
                        if (entry.key == "graze5" && entry.at >= at - 0.01f && entry.at - at <= 1f) barked = true;
                    Run.Event("check", $"hero-barks graze streak 5 at {at:0.0}: cooldown {(openGate ? "open" : "closed")}, {(barked ? "barked" : "no bark")}");
                    if (openGate) { open++; if (barked) answered++; }
                }
                BarkCheck("a graze streak of 5 barks within 1 s", barkGrazes.Count == 0 || open == 0 || answered == open,
                    $"{barkGrazes.Count} streak(s) of 5, {open} with the cooldown open, {answered} answered");
                BarkCheck("a graze streak of 5 happened", barkGrazes.Count > 0, $"{barkGrazes.Count} streak(s) of 5 (the bot's dodging decides)", warnOnly: true);

                // On touch: the caption stays inside the screen and off the hero, the rewind button and the stick.
                if (GameplayHud.TouchUiOn)
                    BarkCheck("[phone] the caption stays clear", barkPhoneBad == 0, barkPhoneBad == 0 ? $"{barkCaptionsSeen} captions, none over the hero or the controls" : $"{barkPhoneBad} frame(s): {barkPhoneDetail}");
                Run.Metric("heroBarks.lines", lines.Count);
                Run.Metric("heroBarks.events", events.Count);
                Run.Metric("heroBarks.barks", log.Count);
                Run.Metric("heroBarks.minGapSeconds", log.Count > 1 ? minGap : -1f);
                Run.Metric("heroBarks.phoneOverlapFrames", barkPhoneBad);
                Run.Metric("heroBarks.failed", barksFailed);
                Run.Progress(1f);
                End(barksFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            /// <summary>Holds the rewind (R, or a finger on the HUD button on touch) over a death: the bark must start within 1 s.</summary>
            IEnumerator BarkRewindDeath(System.Action finished)
            {
                var budget = RewindBudget.Instance;
                if (budget != null)
                    budget.Add(budget.Max);
                bool touch = GameplayHud.TouchUiOn;
                var hud = GameplayWorld.Instance != null ? GameplayWorld.Instance.Hud : null;
                var rect = hud != null ? hud.RewindButtonScreenRect : default;
                int logBefore = HeroBarks.Log.Count;
                float t0 = Time.realtimeSinceStartup;
                int finger = -1;
                var at = rect.center;
                if (touch) finger = input.TouchDown(at); else input.SetKey(Key.R, true);
                float heardAt = -1f, voiceAt = -1f;
                for (float end = t0 + 4f; Time.realtimeSinceStartup < end && !Run.Done;)
                {
                    BarkWatch();
                    yield return null;
                    if (heardAt < 0f && HeroBarks.Log.Count > logBefore && HeroBarks.LastKey == "undo")
                        heardAt = Time.realtimeSinceStartup - t0;
                    if (heardAt >= 0f && voiceAt < 0f && HeroBarks.LastShownAt >= HeroBarks.LastAt - 0.01f && HeroBarks.LastShownAt > 0f && HeroBarks.LastKey == "undo")
                        voiceAt = Time.realtimeSinceStartup - t0;
                    if (heardAt >= 0f && voiceAt >= 0f) break;
                }
                if (touch) input.TouchUp(finger, at); else input.SetKey(Key.R, false);
                BarkCheck("the undone death barks within 1 s", heardAt >= 0f && heardAt <= 1f, heardAt >= 0f ? $"{HeroBarks.LastLine} \"{HeroBarks.LastCaption}\" after {heardAt:0.00} s" : "no undo bark");
                Run.Metric("heroBarks.undoBarkSeconds", heardAt);
                Run.Metric("heroBarks.undoCaptionSeconds", voiceAt);
                finished();
            }
        }
    }
}
