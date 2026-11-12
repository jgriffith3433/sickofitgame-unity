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
        /// overlord-taunts: the Overlord watches the climb (Docs/Story.md "The Overlord watches the climb"). Levels 2-5 (Floors 1000, 999, 998, 997) are loaded
        /// directly (setup shortcut) and played by the bot; on each he must appear with his animated figure and a voiced (or captioned, until the clips
        /// are made) entry taunt, no line repeating across the four floors, his caption on one line. On Floor 998 the bot plays to lose and rewinds the
        /// death (R, or a finger on the HUD button on touch: run it at 390x844): a rewind taunt within 3 s. On a phone his figure is at least 64 px tall.
        /// check events overlord-taunts &lt;beat&gt;; metrics overlordTaunts.*
        /// </summary>
        public const string OverlordTauntsId = "overlord-taunts";
        const float OverlordTauntsSeconds = 600f;

        sealed partial class Session
        {
            int tauntFailed;

            void TauntCheck(string beat, bool ok, string detail)
            {
                Run.Event("check", $"overlord-taunts {beat}: {(ok ? "ok" : "FAILED")} ({detail})");
                if (!ok)
                {
                    tauntFailed++;
                    Run.Problem(PlaytestSeverity.Bug, $"overlord-taunts {beat}: {detail}");
                }
            }

            public IEnumerator OverlordTauntsRun()
            {
                OverlordTaunts.Said.Clear();
                OverlordTaunts.Counts.Clear();
                var heard = new List<string>();
                for (int level = 2; level <= 5 && !Run.Done; level++)
                {
                    int floor = 1002 - level;
                    string key = "enter" + floor;
                    Run.Event("shortcut", $"level {level} (Floor {floor}) loaded directly");
                    var load = LoadLevel(level);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => LevelRunning(level) && RewindBudget.Instance != null, 60f, "the level");
                    if (Run.Done)
                        yield break;
                    float size = 0f;
                    int lines = 0;
                    bool shown = false;
                    string line = "";
                    yield return PlayLevel(level, 0f, now =>
                    {
                        if (OverlordTaunts.Counts.TryGetValue(key, out int n) && n > 0 && OverlordTaunts.Visible)
                        {
                            var hud = GameplayWorld.Instance != null ? GameplayWorld.Instance.Hud : null;
                            if (!shown) { shown = true; line = OverlordTaunts.LastLine; }
                            size = Mathf.Max(size, OverlordTaunts.SizePx);
                            if (hud != null && hud.TryGetSubtitleScreenRect(out _, out int l)) lines = Mathf.Max(lines, l);
                        }
                        return null;
                    }, 0f, 1f, 30f, until: () => shown && size > 0f);
                    TauntCheck($"floor {floor} appears", shown, shown ? $"{line} on screen, {size:0} px tall" : "no taunt on the floor's entry");
                    if (shown)
                    {
                        TauntCheck($"floor {floor} not a repeat", !heard.Contains(line), line);
                        heard.Add(line);
                        TauntCheck($"floor {floor} caption", lines <= 1, $"{lines} line(s)");
                        if (GameplayHud.TouchUiOn)
                            TauntCheck($"floor {floor} size", size >= 64f, $"{size:0} px tall");
                    }
                    Run.Metric($"overlordTaunts.floor{floor}.heightPx", size);
                }
                if (Run.Done)
                    yield break;

                // Dying and rewinding it: a taunt about the rewind within 3 s.
                Run.Event("shortcut", "level 4 loaded directly; the bot plays to lose, then rewinds the death");
                var load4 = LoadLevel(4);
                if (load4 != null)
                    yield return load4;
                yield return Run.Until(() => LevelRunning(4) && RewindBudget.Instance != null, 60f, "the level");
                if (Run.Done)
                    yield break;
                bool done = false, started = false;
                reckless = true;
                try
                {
                    yield return PlayLevel(4, 0f, now =>
                    {
                        var hero = GameplayWorld.Instance != null ? GameplayWorld.Instance.Player : null;
                        var health = hero != null ? hero.GetComponentInChildren<PlayerHealth>() : null;
                        if (started || health == null || health.DeathTimeLeft < 0.4f)
                            return null;
                        started = true;
                        return TauntRewindDeath(heard, () => done = true);
                    }, 0f, 1f, 200f, until: () => done);
                }
                finally { reckless = false; }
                if (Run.Done)
                    yield break;
                TauntCheck("the death happened", started, started ? "died with the undo window open" : "the bot never died");
                Run.Metric("overlordTaunts.failed", tauntFailed);
                Run.Progress(1f);
                End(tauntFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            IEnumerator TauntRewindDeath(List<string> heard, System.Action finished)
            {
                var budget = RewindBudget.Instance;
                if (budget != null)
                    budget.Add(budget.Max);
                bool touch = GameplayHud.TouchUiOn;
                var hud = GameplayWorld.Instance != null ? GameplayWorld.Instance.Hud : null;
                var rect = hud != null ? hud.RewindButtonScreenRect : default;
                string before = OverlordTaunts.LastLine;
                float t0 = Time.realtimeSinceStartup;
                int finger = -1;
                var at = rect.center;
                if (touch) finger = input.TouchDown(at); else input.SetKey(Key.R, true);
                float heardAt = -1f;
                for (float end = t0 + 4f; Time.realtimeSinceStartup < end && !Run.Done;)
                {
                    yield return null;
                    if (OverlordTaunts.LastKey == "undo" && OverlordTaunts.LastLine != before) { heardAt = Time.realtimeSinceStartup - t0; break; }
                }
                if (touch) input.TouchUp(finger, at); else input.SetKey(Key.R, false);
                TauntCheck("rewind taunt within 3 s", heardAt >= 0f && heardAt <= 3f, heardAt >= 0f ? $"{OverlordTaunts.LastLine} after {heardAt:0.0} s" : "no taunt");
                TauntCheck("rewind taunt not a repeat", !heard.Contains(OverlordTaunts.LastLine), OverlordTaunts.LastLine);
                Run.Metric("overlordTaunts.rewindTauntSeconds", heardAt);
                finished();
            }
        }
    }
}
