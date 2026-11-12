using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core;
using SickOfIt.Gameplay;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// first-rewind: Level 1's first minute as the tutorial (Docs/Story.md "First five minutes", docs/world-bible.md "The Meadow"). Level 1 loaded directly
        /// (setup shortcut), played by the bot with its own rewinds off, first on desktop (R held), then with the touch UI on at phone size 390x844 (a finger on the
        /// pulsing button). Checks: an enemy shot crossing within 8 s, 4+ enemies on screen by 25 s, the village moving and the meadow bed playing, the staged volley
        /// hitting inside 45 s with time slowed to ~20%, the rewind button pulsing (64+ px on the phone) and the hero's voiced line, the press starting a rewind and a
        /// relief line after it, and never two captions at once. check events first-rewind &lt;beat&gt;; metrics firstRewind.*
        /// </summary>
        public const string FirstRewindId = "first-rewind";
        const float FirstRewindSeconds = 420f;

        sealed partial class Session
        {
            int firstRewindFailed;

            void FirstRewindCheck(string beat, bool ok, string detail)
            {
                Run.Event("check", $"first-rewind {beat}: {(ok ? "ok" : "FAILED")} ({detail})");
                if (!ok)
                {
                    firstRewindFailed++;
                    Run.Problem(PlaytestSeverity.Bug, $"first-rewind {beat}: {detail}");
                }
            }

            public IEnumerator FirstRewindRun()
            {
                yield return FirstRewindOnce(false);
                if (!Run.Done)
                    yield return FirstRewindOnce(false, true);
                if (Run.Done)
                    yield break;
                Run.Progress(0.5f);
                int w0 = Screen.width, h0 = Screen.height;
                if (CanResizeWindow)
                {
                    Screen.SetResolution(390, 844, false);
                    yield return Run.Until(() => Screen.width == 390 && Screen.height == 844, 6f, "the window to resize to 390x844");
                }
                yield return FirstRewindOnce(true);
                if (!Run.Done)
                    yield return FirstRewindOnce(true, true);
                GameplayHud.TouchUiForced = false;
                if (CanResizeWindow)
                    Screen.SetResolution(w0, h0, false);
                if (Run.Done)
                    yield break;
                Run.Metric("firstRewind.failed", firstRewindFailed);
                Run.Progress(1f);
                End(firstRewindFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            IEnumerator FirstRewindOnce(bool touch, bool noRewind = false)
            {
                string tag = (touch ? "touch 390x844" : "desktop") + (noRewind ? " no rewind" : "");
                GameplayHud.TouchUiForced = touch;
                GameplayHud.ResetTouchHints();
                Run.Event("shortcut", $"level 1 loaded directly ({tag})");
                var load = LoadLevel(1);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => LevelRunning(1) && GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null, 60f, "the level");
                if (Run.Done)
                    yield break;
                bool done = false, started = false;
                yield return PlayLevel(1, 0f, now =>
                {
                    if (bot != null)
                        bot.useRewind = false;
                    if (started)
                        return null;
                    started = true;
                    return FirstRewindWatch(touch, tag, () => done = true, noRewind);
                }, touch ? 0.5f : 0f, touch ? 1f : 0.5f, 200f, until: () => done);
            }

            IEnumerator FirstRewindWatch(bool touch, string tag, System.Action finished, bool noRewind)
            {
                var world = GameplayWorld.Instance;
                var hud = world.Hud;
                float firstShot = -1f, hitAt = -1f, bedAt = -1f;
                int maxOnScreen25 = 0, overlap = 0, hits0 = FirstRewindStage.Hits;
                float walked = 0f;
                var startX = new System.Collections.Generic.Dictionary<int, float>();
                bool slowSeen = false, stageChecked = false, relief = false;
                float t0 = Time.realtimeSinceStartup;
                while (!Run.Done && Time.realtimeSinceStartup - t0 < 150f)
                {
                    yield return null;
                    float now = world.Now;
                    int onScreen = 0;
                    var view = world.View;
                    foreach (var e in world.Live)
                    {
                        if (e == null || !e.IsAlive || e.Def == null)
                            continue;
                        if (e.Team == SickOfIt.Gameplay.Content.Team.Enemy && e.Def.health > 0 && view.Contains(e.Position))
                            onScreen++;
                        if (firstShot < 0f && e.Team == SickOfIt.Gameplay.Content.Team.Enemy && e.Def.health <= 0 && view.Contains(e.Position) && e.DefId != "")
                            firstShot = now;
                        if (e.DefId == "meadowNeighbour")
                        {
                            int k = e.GetInstanceID();
                            if (!startX.TryGetValue(k, out float x0))
                                startX[k] = e.Position.x;
                            else
                                walked = Mathf.Max(walked, Mathf.Abs(e.Position.x - x0));
                        }
                        if (bedAt < 0f && e.DefId == "meadowBed")
                        {
                            bedAt = now;
                            Run.Event("sound", $"ambient bed: the meadowBed marker is up at {now:0.0} s; the bed itself is the level's ambientSound \"{SickOfIt.Core.Audio.AmbienceBed.Current}\" on the Ambience bus");
                        }
                    }
                    if (now <= 25.5f)
                        maxOnScreen25 = Mathf.Max(maxOnScreen25, onScreen);
                    if (hud.SubtitleShown != "" && hud.BannerShown != "")
                        overlap++;

                    if (!slowSeen && FirstRewindStage.Current == FirstRewindStage.Stage.Slow)
                    {
                        slowSeen = true;
                        hitAt = now;
                        yield return null;
                        yield return WaitReal(0.5f);
                        var rect = hud.RewindButtonScreenRect;
                        float px = Mathf.Min(rect.width, rect.height);
                        Run.Event("staged", $"{tag}: hit at {hitAt:0.0} s, time scale {Time.timeScale:0.00}, button {rect.width:0}x{rect.height:0} px, caption \"{hud.SubtitleShown}\"");
                        FirstRewindCheck($"{tag}: the staged volley hits within 45 s", hitAt <= 45f && hitAt >= 20f, $"at {hitAt:0.0} s");
                        FirstRewindCheck($"{tag}: time drops to about 20%", Time.timeScale <= 0.3f, $"Time.timeScale {Time.timeScale:0.00}");
                        FirstRewindCheck($"{tag}: the rewind button pulses", FirstRewindStage.Pulsing, "HUD button grown and pulsing");
                        FirstRewindCheck($"{tag}: one short caption", hud.SubtitleShown.Split(' ').Length <= 6 && hud.SubtitleShown != "", $"\"{hud.SubtitleShown}\"");
                        if (touch)
                            FirstRewindCheck($"{tag}: the pulsing button is 64+ px", px >= 64f, $"{px:0} px");
                        Run.Metric($"firstRewind.{(touch ? "touch" : "desktop")}.buttonPx", px);
                        stageChecked = true;
                        if (noRewind)
                        {
                            // The player does nothing: time must come back by itself within 2 s of real time (never stuck in slow motion).
                            float hitReal = Time.realtimeSinceStartup - 0.5f;
                            yield return Run.Until(() => Time.timeScale > 0.99f, 2f, "time to return to full speed");
                            float back = Time.realtimeSinceStartup - hitReal;
                            FirstRewindCheck($"{tag}: time scale back to 1 within 2 s", Time.timeScale > 0.99f && back <= 2.2f, $"{back:0.00} s, time scale {Time.timeScale:0.00}, freeze active {SickOfIt.Core.Fx.HitFreeze.Active}");
                            FirstRewindCheck($"{tag}: the button still pulses after", FirstRewindStage.Pulsing, $"stage {FirstRewindStage.Current}");
                            Run.Metric($"firstRewind.noRewind.{(touch ? "touch" : "desktop")}.secondsToFullSpeed", back);
                            yield return WaitReal(5f);
                            FirstRewindCheck($"{tag}: still full speed later", Time.timeScale > 0.99f, $"time scale {Time.timeScale:0.00}");
                            stageChecked = true;
                            break;
                        }
                        yield return WaitReal(0.6f);
                        int touchId = -1;
                        var at = rect.center;
                        if (touch) touchId = input.TouchDown(at); else input.SetKey(Key.R, true);
                        yield return Run.Until(() => GameClock.Instance != null && GameClock.Instance.IsRewinding, 4f, "the staged rewind to start");
                        bool rewinding = GameClock.Instance != null && GameClock.Instance.IsRewinding;
                        Run.Event("rewind", $"{tag}: staged rewind {(rewinding ? "started" : "did not start")}");
                        FirstRewindCheck($"{tag}: pressing the button rewinds", rewinding, rewinding ? "rewind event logged" : "no rewind");
                        yield return WaitReal(1.8f);
                        if (touch) input.TouchUp(touchId, at); else input.SetKey(Key.R, false);
                        yield return Run.Until(() => FirstRewindStage.Current == FirstRewindStage.Stage.Done, 6f, "the rewind to end");
                        relief = FirstRewindStage.Relieved > 0;
                        FirstRewindCheck($"{tag}: relief line after the rewind", relief && Time.timeScale > 0.9f, $"time scale {Time.timeScale:0.00}");
                        break;
                    }
                }
                FirstRewindCheck($"{tag}: an enemy shot crosses the screen within 8 s", firstShot >= 0f && firstShot <= 8f, $"first at {firstShot:0.0} s");
                FirstRewindCheck($"{tag}: 4+ enemies on screen by 25 s", maxOnScreen25 >= 4, $"{maxOnScreen25} at most");
                FirstRewindCheck($"{tag}: neighbours move", walked > 1f, $"walked {walked:0.0} units");
                FirstRewindCheck($"{tag}: the meadow bed plays", bedAt >= 0f && bedAt < 5f, $"at {bedAt:0.0} s");
                FirstRewindCheck($"{tag}: never a banner and a caption together", overlap == 0, $"{overlap} frames");
                FirstRewindCheck($"{tag}: staged rewind ran", stageChecked && FirstRewindStage.Hits > hits0, $"hits {FirstRewindStage.Hits - hits0}");
                finished();
            }
        }
    }
}
