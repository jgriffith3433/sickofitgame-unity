using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core.Flow;
using SickOfIt.Core.Progression;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using Object = UnityEngine.Object;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// First-five: a brand-new player reaches dodging fast (Docs/Story.md "First five minutes"). Fresh save, New Game,
        /// play what is on screen for 5 minutes; reports time to menu / first move / first hit / first rewind, the longest
        /// locked or non-interactive stretch, and choice screens before play. Run it desktop and at 390x844.
        /// </summary>
        public const string FirstFiveId = "first-five";
        const float FirstFiveSeconds = 420f;
        const float FirstFivePlaySeconds = 300f;
        const float FirstFiveMoveBudgetDesktop = 30f;
        const float FirstFiveMoveBudgetPhone = 45f;
        const float FirstFiveLockBudget = 10f;
        const float FirstFiveStretchBudget = 25f;

        sealed partial class Session
        {
            int firstFiveFailed;
            float firstFivePageOpen;
            float firstFiveFloor1000Start = 0f;
            float firstFiveMoveAt = -1f, firstFiveHitAt = -1f, firstFiveRewindAt = -1f;
            float firstFiveLockStarted = -1f;
            string firstFiveLockName = "";
            float firstFiveLongestLock;
            string firstFiveLongestName = "";
            float firstFiveFightEnded = -1f;
            float firstFiveFloor1000 = -1f;
            bool firstFivePortalSkip, firstFiveShaftSkip;
            bool firstFivePhone;

            float FirstFiveAge() => Time.realtimeSinceStartup - firstFivePageOpen;

            void FirstFiveCheck(string beat, bool ok, string detail)
            {
                if (ok) { Run.Event("check", $"first-five {beat}: ok ({detail})"); return; }
                firstFiveFailed++;
                Run.Event("check", $"first-five {beat}: FAILED ({detail})");
                Run.Problem(PlaytestSeverity.Bug, $"first-five {beat}: {detail}");
            }

            void FirstFiveNoteLock(string name, bool on)
            {
                float now = FirstFiveAge();
                if (on)
                {
                    if (firstFiveLockStarted < 0f)
                    {
                        firstFiveLockStarted = now;
                        firstFiveLockName = name;
                        Run.Event("lock", $"start {name} at {now:0.00}s");
                    }
                }
                else if (firstFiveLockStarted >= 0f)
                {
                    float dur = now - firstFiveLockStarted;
                    if (dur > firstFiveLongestLock)
                    {
                        firstFiveLongestLock = dur;
                        firstFiveLongestName = firstFiveLockName;
                    }
                    Run.Event("lock", $"end {firstFiveLockName} after {dur:0.00}s");
                    firstFiveLockStarted = -1f;
                    firstFiveLockName = "";
                }
            }

            void FirstFiveTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (e.Kind == "damage" && e.Subject == "player" && firstFiveHitAt < 0f)
                {
                    firstFiveHitAt = FirstFiveAge();
                    Run.Event("first-five", $"first hit at {firstFiveHitAt:0.00}s ({e.Detail})");
                }
                if (e.Kind == "rewind" && firstFiveRewindAt < 0f)
                {
                    firstFiveRewindAt = FirstFiveAge();
                    Run.Event("first-five", $"first rewind at {firstFiveRewindAt:0.00}s");
                }
                if (e.Kind == "cutscene")
                {
                    if (e.Subject == "lockInput")
                        FirstFiveNoteLock("portal", true);
                    else if (e.Subject == "unlockInput")
                        FirstFiveNoteLock("portal", false);
                    else if (e.Subject == "skip")
                    {
                        firstFivePortalSkip = true;
                        Run.Event("skip", $"portal skipped at {FirstFiveAge():0.00}s");
                    }
                }
                if ((e.Kind == "phase" && e.Subject != null && e.Subject.StartsWith("portal"))
                    || (e.Kind == "signal" && e.Subject == "portal:locked"))
                {
                    if (firstFiveFightEnded < 0f)
                        firstFiveFightEnded = FirstFiveAge();
                }
            }

            IEnumerator FirstFiveTap(Vector2 spot)
            {
                if (firstFivePhone)
                {
                    int finger = input.TouchDown(spot);
                    yield return null;
                    input.TouchUp(finger, spot);
                }
                else
                {
                    input.MoveMouse(spot);
                    yield return null;
                    input.SetMouseButton(true);
                    yield return null;
                    input.SetMouseButton(false);
                }
            }

            IEnumerator FirstFiveDuring(float now)
            {
                var world = GameplayWorld.Instance;
                if (world == null)
                    yield break;

                if (firstFiveMoveAt < 0f)
                {
                    var pc = world.PlayerController;
                    bool moving = pc != null && (Mathf.Abs(pc.TouchMove) > 0.1f
                        || Keyboard.current != null && (Keyboard.current.aKey.isPressed || Keyboard.current.dKey.isPressed)
                        || Gamepad.current != null && Mathf.Abs(Gamepad.current.leftStick.ReadValue().x) > 0.2f);
                    if (moving)
                    {
                        firstFiveMoveAt = FirstFiveAge();
                        Run.Event("first-five", $"first move at {firstFiveMoveAt:0.00}s");
                    }
                }

                // One tap skips the portal once input is locked for the scene.
                if (!firstFivePortalSkip && world.RewindLocked && (float.IsPositiveInfinity(world.InputLockedUntil) || world.Now < world.InputLockedUntil))
                {
                    yield return FirstFiveTap(new Vector2(Screen.width * 0.5f, Screen.height * 0.55f));
                    Run.Event("skip", $"tap to skip portal at {FirstFiveAge():0.00}s");
                    yield return WaitReal(0.35f);
                }
            }

            public IEnumerator FirstFiveRun()
            {
                firstFiveFailed = 0;
                firstFivePhone = Screen.height > Screen.width;
                float moveBudget = firstFivePhone ? FirstFiveMoveBudgetPhone : FirstFiveMoveBudgetDesktop;
                firstFivePageOpen = PageLoad.MenuAtPage >= 0f
                    ? Time.realtimeSinceStartup - (PageLoad.MenuAtReal - PageLoad.MenuAtPage)
                    : Time.realtimeSinceStartup;
                firstFiveMoveAt = firstFiveHitAt = firstFiveRewindAt = -1f;
                firstFiveLockStarted = -1f;
                firstFiveLockName = "";
                firstFiveLongestLock = 0f;
                firstFiveLongestName = "";
                firstFiveFightEnded = firstFiveFloor1000 = -1f;
                firstFivePortalSkip = firstFiveShaftSkip = false;

                yield return ToMainMenu();
                if (Run.Done) yield break;
                float menuAt = FirstFiveAge();
                Run.Metric("firstFive.menuSeconds", menuAt);
                Run.Event("first-five", $"menu at {menuAt:0.00}s after page open ({Screen.width}x{Screen.height}, {(firstFivePhone ? "phone" : "desktop")})");

                // The deploy takes the hero out of the player build's menu scene (it was most of the phone's first download) and the menu
                // puts him back at runtime: he must be on screen, so that move can't remove him for good.
                string heroProblem = MenuHeroProblem();
                FirstFiveCheck("heroOnMenu", heroProblem == null, heroProblem ?? "the hero stands on the menu");

                var labels = GameDefinition.Instance.mainMenuConfig.labels;
                int choices = FindButton("Hell Loop") != null ? 1 : 0;
                FirstFiveCheck("noDifficultyCard", choices == 0, choices == 0
                    ? "no Pick Your Hell before New Game" : "Pick Your Hell already open");
                Run.Metric("firstFive.choicesBeforePlay", choices);

                yield return ClickUntil(NewGameLabel(labels), () => LevelRunning(2) || Object.FindAnyObjectByType<VideoRuntime>() != null, 30f);
                if (Run.Done) yield break;
                FirstFiveCheck("noDifficultyAfterNewGame", FindButton("Hell Loop") == null && Difficulty.Current == DifficultyLevel.Normal,
                    $"Difficulty.Current={Difficulty.Current}");
                FirstFiveCheck("noUpgradeBeforePlay", Object.FindAnyObjectByType<UpgradeShopManager>() == null,
                    "no upgrade shop before the first dodge");

                yield return ReachLevel(2);
                if (Run.Done) yield break;

                firstFiveFloor1000Start = FirstFiveAge();
                GameplayTelemetry.Recorded += FirstFiveTelemetry;
                float deadline = Time.realtimeSinceStartup + FirstFivePlaySeconds;
                try
                {
                    // Level 1 with the bot; during callback taps to skip the portal.
                    yield return PlayLevel(2, 0f, FirstFiveDuring, 0f, 1f, Mathf.Max(60f, deadline - Time.realtimeSinceStartup), waitOutDeath: true);
                    if (Run.Done) yield break;

                    // Results → Continue → Shaft plunge (first run skips Video2).
                    if (lastOutcome == "completed")
                    {
                        if (firstFiveFightEnded < 0f)
                            firstFiveFightEnded = FirstFiveAge();
                        var texts = GameDefinition.Instance.menuTheme.texts;
                        if (GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null && GameplayWorld.Instance.Hud.ResultsOpen)
                        {
                            FirstFiveNoteLock("results", true);
                            yield return ClickUntil(texts.continueText, () => GameplayWorld.Instance == null || GameplayWorld.Instance.Hud == null || !GameplayWorld.Instance.Hud.ResultsOpen, 20f);
                            FirstFiveNoteLock("results", false);
                            if (Run.Done) yield break;
                        }

                        yield return Run.Until(() => ShaftScreen.Current != null || LevelRunning(2) || Object.FindAnyObjectByType<VideoRuntime>() != null,
                            30f, "the Shaft plunge or Floor 1000");
                        if (Run.Done) yield break;

                        FirstFiveCheck("noVideo2FirstRun", Object.FindAnyObjectByType<VideoRuntime>() == null,
                            Object.FindAnyObjectByType<VideoRuntime>() == null
                                ? "Video2 skipped on the first run (Shaft or Floor 1000 next)"
                                : "Video2 played on the first run");

                        var shaft = ShaftScreen.Current;
                        if (shaft != null && shaft.Kind == ShaftScreen.Mode.Plunge)
                        {
                            FirstFiveNoteLock("shaft-plunge", true);
                            float shaftStart = FirstFiveAge();
                            // Shaft ignores taps for 0.35 s after open; then one tap skips from the first usable frame.
                            yield return WaitReal(0.4f);
                            yield return FirstFiveTap(new Vector2(Screen.width * 0.5f, Screen.height * 0.4f));
                            firstFiveShaftSkip = true;
                            Run.Event("skip", $"tap to skip Shaft plunge at {FirstFiveAge():0.00}s");
                            yield return Run.Until(() => ShaftScreen.Current == null || ShaftScreen.Current.Ready, 12f, "the Shaft plunge to end or offer Continue");
                            FirstFiveNoteLock("shaft-plunge", false);
                            float shaftDur = FirstFiveAge() - shaftStart;
                            Run.Metric("firstFive.shaftSeconds", shaftDur);
                            FirstFiveCheck("shaftShort", shaftDur <= 8.1f, $"Shaft plunge took {shaftDur:0.00}s (budget 8s, skip={firstFiveShaftSkip})");

                            if (ShaftScreen.Current != null && ShaftScreen.Current.Ready)
                            {
                                string cont = !string.IsNullOrEmpty(texts.continueText) ? texts.continueText : "Continue";
                                yield return ClickUntil(cont, () => ShaftScreen.Current == null, 20f);
                                if (Run.Done) yield break;
                            }
                        }

                        yield return Run.Until(() => LevelRunning(2), 60f, "Floor 1000");
                        if (Run.Done) yield break;
                        yield return Run.Until(() =>
                        {
                            var w = GameplayWorld.Instance;
                            return w != null && !w.RewindLocked && (w.InputLockedUntil <= w.Now || w.InputLockedUntil <= 0f);
                        }, 30f, "Floor 1000 playable");
                        if (!Run.Done)
                        {
                            firstFiveFloor1000 = FirstFiveAge();
                            Run.Event("first-five", $"Floor 1000 playable at {firstFiveFloor1000:0.00}s");
                        }

                        // Keep playing toward the 5-minute mark.
                        float left = deadline - Time.realtimeSinceStartup;
                        if (left > 5f && !Run.Done)
                            yield return PlayLevel(2, left, null, 0f, 1f, left, waitOutDeath: true);
                    }
                }
                finally
                {
                    GameplayTelemetry.Recorded -= FirstFiveTelemetry;
                    if (firstFiveLockStarted >= 0f)
                        FirstFiveNoteLock(firstFiveLockName, false);
                }

                Run.Metric("firstFive.firstMoveSeconds", firstFiveMoveAt);
                Run.Metric("firstFive.firstHitSeconds", firstFiveHitAt);
                Run.Metric("firstFive.firstRewindSeconds", firstFiveRewindAt);
                Run.Metric("firstFive.longestLockSeconds", firstFiveLongestLock);
                if (firstFiveFightEnded >= 0f && firstFiveFloor1000 >= 0f)
                    Run.Metric("firstFive.fightToFloor1000Seconds", firstFiveFloor1000 - firstFiveFightEnded);

                FirstFiveCheck("firstMove", firstFiveMoveAt >= 0f && firstFiveMoveAt <= moveBudget,
                    firstFiveMoveAt < 0f ? "no move input seen" : $"first move at {firstFiveMoveAt:0.00}s (budget {moveBudget:0}s)");
                FirstFiveCheck("portalSkip", firstFivePortalSkip || firstFiveFightEnded < 0f,
                    firstFivePortalSkip ? "one tap skipped the portal" : "portal never locked (Level 1 may not have cleared)");
                FirstFiveCheck("shaftSkip", firstFiveShaftSkip || firstFiveFloor1000 < 0f,
                    firstFiveShaftSkip ? "one tap skipped the Shaft plunge" : "Shaft plunge never seen");
                if (firstFiveFightEnded >= 0f && firstFiveFloor1000 >= 0f)
                {
                    float stretch = firstFiveFloor1000 - firstFiveFightEnded;
                    FirstFiveCheck("stretch", stretch <= FirstFiveStretchBudget,
                        $"fight-end to Floor 1000 playable took {stretch:0.00}s (budget {FirstFiveStretchBudget:0}s)");
                }
                FirstFiveCheck("firstRewind20s", firstFiveRewindAt >= 0f && firstFiveRewindAt - firstFiveFloor1000Start <= 20f,
                    firstFiveRewindAt < 0f ? "no rewind seen" : $"first rewind {firstFiveRewindAt - firstFiveFloor1000Start:0.00}s after Floor 1000 began (budget 20s)");
                FirstFiveCheck("longestLock", firstFiveLongestLock <= FirstFiveLockBudget + 0.15f,
                    $"longest locked stretch \"{firstFiveLongestName}\" was {firstFiveLongestLock:0.00}s (budget {FirstFiveLockBudget:0}s)");

                bool video2 = GameProgressManager.Instance != null && GameProgressManager.Instance.IsVideoUnlocked(2);
                FirstFiveCheck("video2Rewatch", video2 || lastOutcome != "completed",
                    video2 ? "Video2 unlocked for Rewatch Stuff" : "Video2 not unlocked after Level 1 clear");

                Run.Progress(1f);
                End(firstFiveFailed == 0 ? PlaytestOutcome.Won : PlaytestOutcome.Done);
            }
        }
    }
}
