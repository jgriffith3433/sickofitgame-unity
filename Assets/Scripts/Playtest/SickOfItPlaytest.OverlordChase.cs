using System.Collections;
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
        /// overlord-chase: the chase seen in play (docs/world-bible.md "The chase"). Level 3 loaded directly (setup shortcut), the bot
        /// plays to lose; while the death's undo window is open it rewinds (R, or a finger on the HUD button when the touch UI is on:
        /// run it on the 390x844 phone). The Overlord's silhouette must climb out of the top of the screen, big enough to read, then be
        /// gone; and a rewind with no death behind it must not show him. check events overlord-chase &lt;beat&gt;; metrics overlordChase.*
        /// </summary>
        public const string OverlordChaseId = "overlord-chase";
        const int OverlordChaseLevel = 3;
        const float OverlordChaseSeconds = 240f;

        sealed partial class Session
        {
            int chaseFailed;

            void ChaseCheck(string beat, bool ok, string detail)
            {
                Run.Event("check", $"overlord-chase {beat}: {(ok ? "ok" : "FAILED")} ({detail})");
                if (!ok)
                {
                    chaseFailed++;
                    Run.Problem(PlaytestSeverity.Bug, $"overlord-chase {beat}: {detail}");
                }
            }

            public IEnumerator OverlordChaseRun()
            {
                Run.Event("shortcut", $"level {OverlordChaseLevel} loaded directly; the bot plays to lose, then rewinds the death");
                var load = LoadLevel(OverlordChaseLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => LevelRunning(OverlordChaseLevel) && RewindBudget.Instance != null, 60f, "the level");
                if (Run.Done)
                    yield break;

                bool done = false, started = false;
                reckless = true;
                try
                {
                    yield return PlayLevel(OverlordChaseLevel, 0f, now =>
                    {
                        var hero = GameplayWorld.Instance != null ? GameplayWorld.Instance.Player : null;
                        var health = hero != null ? hero.GetComponentInChildren<PlayerHealth>() : null;
                        if (started || health == null || health.DeathTimeLeft < 0.4f)
                            return null;
                        started = true;
                        return ChaseRewindDeath(() => done = true);
                    }, 0f, 1f, 200f, until: () => done);
                }
                finally { reckless = false; }
                if (Run.Done)
                    yield break;
                ChaseCheck("the death happened", started, started ? "the hero died with the undo window open" : "the bot never died");
                Run.Metric("overlordChase.failed", chaseFailed);
                Run.Progress(1f);
                End(chaseFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            IEnumerator ChaseRewindDeath(System.Action finished)
            {
                var budget = RewindBudget.Instance;
                if (budget != null)
                    budget.Add(budget.Max);
                int before = OverlordEscape.Escapes;
                bool touch = GameplayHud.TouchUiOn;
                var hud = GameplayWorld.Instance != null ? GameplayWorld.Instance.Hud : null;
                var rect = hud != null ? hud.RewindButtonScreenRect : default;
                ChaseCheck("calm before", !OverlordEscape.Visible, "no silhouette while he is only dying");
                Run.Event("rewind", $"undo the death ({(touch ? "finger on the button" : "R held")})");
                int finger = -1;
                var at = rect.center;
                if (touch) finger = input.TouchDown(at); else input.SetKey(Key.R, true);

                float peakSize = 0f, firstP = -1f, lastP = -1f, topY = 0f;
                bool seen = false;
                for (float end = Time.realtimeSinceStartup + 4f; Time.realtimeSinceStartup < end && !Run.Done;)
                {
                    yield return null;
                    if (OverlordEscape.Visible)
                    {
                        seen = true;
                        if (firstP < 0f) firstP = OverlordEscape.Progress;
                        lastP = OverlordEscape.Progress;
                        peakSize = Mathf.Max(peakSize, OverlordEscape.SizePx.y);
                        if (lastP >= 1f) break;
                    }
                }
                if (touch) input.TouchUp(finger, at); else input.SetKey(Key.R, false);
                float px = Mathf.Min(rect.width, rect.height);
                Run.Metric("overlordChase.peakHeightPx", peakSize);
                ChaseCheck("he climbs out", seen && OverlordEscape.Escapes == before + 1 && lastP > firstP + 0.3f,
                    $"silhouette shown, climbing {firstP:0.00} to {lastP:0.00}, {OverlordEscape.Escapes - before} escape(s)");
                ChaseCheck("he is readable", peakSize >= Mathf.Min(Screen.height, Screen.width) * 0.3f, $"{peakSize:0} px tall on a {Screen.width}x{Screen.height} screen");
                if (touch)
                    ChaseCheck("tap target", px >= 44f, $"rewind button {px:0} px");
                yield return Run.Until(() => !OverlordEscape.Visible, 4f, "the silhouette to be gone");
                ChaseCheck("he is gone after", !OverlordEscape.Visible, "off the top of the screen, nothing left over");
                finished();
            }
        }
    }
}
