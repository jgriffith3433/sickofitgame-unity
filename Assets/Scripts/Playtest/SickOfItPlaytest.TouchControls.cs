using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The touch-controls scenario (a phone's move stick, rewind button and first-run hints). First a desktop run (Level 1, no
        /// touch controls drawn), then the touch UI switched on (<see cref="GameplayHud.TouchUiForced"/>, a named setup shortcut) and Level 2
        /// loaded directly: the stick is on screen outside the playfield, the hints show, a virtual finger drags the stick and the hero walks,
        /// a second finger holds the rewind button (48+ px) while the first still drags, a flick up jumps, and the hints are gone and remembered.
        /// </summary>
        public const string TouchControlsId = "touch-controls";
        const int TouchDesktopLevel = 1;
        const int TouchLevel = 2;
        const float TouchControlsSeconds = 240f;

        sealed partial class Session
        {
            int touchPassed, touchFailed;

            void TouchCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    touchPassed++;
                    Run.Event("check", $"touch-controls {beat}: ok ({detail})");
                }
                else
                {
                    touchFailed++;
                    Run.Event("check", $"touch-controls {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"touch-controls {beat}: {detail}");
                }
            }

            public IEnumerator TouchControlsRun()
            {
                // 1. Desktop: nothing touch is drawn.
                GameplayHud.TouchUiForced = false;
                GameplayHud.ResetTouchHints();
                Run.Event("shortcut", $"level {TouchDesktopLevel} loaded directly (desktop: no touch UI)");
                var load = LoadLevel(TouchDesktopLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => LevelRunning(TouchDesktopLevel) && GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null, 60f, "the level");
                if (Run.Done)
                    yield break;
                yield return WaitReal(2f);
                var hud = GameplayWorld.Instance.Hud;
                TouchCheck("desktop shows no touch controls", !hud.TouchStickShown && !hud.TouchPadShown && !hud.TouchHintsShown,
                    $"stick {hud.TouchStickShown}, pad {hud.TouchPadShown}, hints {hud.TouchHintsShown}");
                Run.Progress(0.1f);

                // 2. A touch device.
                GameplayHud.TouchUiForced = true;
                GameplayHud.ResetTouchHints();
                Run.Event("shortcut", $"touch UI on, level {TouchLevel} loaded directly");
                load = LoadLevel(TouchLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => LevelRunning(TouchLevel) && GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null
                                             && GameplayWorld.Instance.Hud.TouchStickShown, 60f, "the touch stick");
                if (Run.Done)
                    yield break;
                var world = GameplayWorld.Instance;
                hud = world.Hud;
                var hero = world.PlayerController;
                var health = hero.GetComponent<PlayerHealth>();
                yield return WaitReal(1f);
                Time.timeScale = Speed;

                var ring = hud.TouchStickScreenRect;
                var play = world.Camera != null ? world.Camera.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
                bool portrait = Screen.height > Screen.width;
                bool outside = !play.Overlaps(ring);
                Run.Event("touch", $"{(portrait ? "portrait" : "landscape")} {Screen.width}x{Screen.height}: stick ring {ring}, playfield {play}");
                TouchCheck("move stick visible on a touch device", hud.TouchStickShown && ring.width > 40f, $"ring {ring.width:0}x{ring.height:0} px");
                TouchCheck("stick sits outside the playfield (portrait) / on the left (landscape)",
                    portrait ? outside : ring.center.x < Screen.width * 0.5f, $"playfield {play}, ring {ring}");
                var rewindRect = hud.RewindButtonScreenRect;
                TouchCheck("rewind button is at least 48 px", rewindRect.width >= 48f && rewindRect.height >= 48f, $"{rewindRect.width:0}x{rewindRect.height:0} px");
                TouchCheck("first-run hint shows", hud.TouchHintsShown, "move and rewind hints on screen");
                Run.Metric("touch.rewindButtonPx", Mathf.Min(rewindRect.width, rewindRect.height));
                Run.Metric("touch.stickPx", ring.width);
                Run.Progress(0.3f);

                // 3. Drag the stick right: the hero walks.
                yield return Run.Until(() => hero.IsGrounded, 20f, "the hero on the ground");
                if (health != null)
                {
                    health.SetHealth(health.MaxHealth);
                    health.SetInvincibilityTimer(60f);
                }
                float x0 = hero.transform.position.x;
                var center = ring.center;
                float radius = ring.width * 0.5f;
                int stickFinger = input.TouchDown(center);
                yield return null;
                for (int i = 1; i <= 6; i++)
                {
                    input.TouchMove(stickFinger, center + new Vector2(radius * 0.9f * i / 6f, 0f));
                    yield return null;
                }
                yield return WaitReal(1.5f);
                float x1 = hero.transform.position.x;
                Run.Event("touch", $"dragged right: hero x {x0:0.00} -> {x1:0.00}, stick axis {hud.TouchStickAxis:0.00}");
                TouchCheck("dragging the stick moves the hero", x1 - x0 > 0.5f && hud.TouchStickAxis > 0.3f, $"x {x0:0.00} -> {x1:0.00}, axis {hud.TouchStickAxis:0.00}");
                TouchCheck("move hint goes after the first drag", PlayerPrefs.GetInt("TouchStickMoved", 0) == 1, "remembered in PlayerPrefs");
                Run.Progress(0.5f);

                // 4. A second finger holds rewind while the first still drags.
                var rewindAt = rewindRect.center;
                int rewindFinger = input.TouchDown(rewindAt);
                yield return Run.Until(() => GameClock.Instance != null && GameClock.Instance.IsRewinding, 4f, "the rewind to start (touch)");
                bool rewound = GameClock.Instance != null && GameClock.Instance.IsRewinding;
                float axisWhile = hud.TouchStickAxis;
                yield return WaitReal(0.8f);
                input.TouchUp(rewindFinger, rewindAt);
                yield return Run.Until(() => GameClock.Instance != null && !GameClock.Instance.IsRewinding, 5f, "the rewind to end");
                TouchCheck("rewind works while the stick finger is down", rewound && axisWhile > 0.3f, $"rewinding {rewound}, stick axis {axisWhile:0.00}");
                input.TouchUp(stickFinger, center);
                yield return WaitReal(0.4f);
                TouchCheck("rewind hint goes after the first rewind", PlayerPrefs.GetInt("TouchStickRewound", 0) == 1 && !hud.TouchHintsShown, $"hints shown {hud.TouchHintsShown}");
                Run.Progress(0.75f);

                // 5. A flick up jumps.
                yield return Run.Until(() => hero.IsGrounded, 20f, "the hero on the ground");
                yield return WaitReal(0.3f);
                int jumps = hero.JumpsMade;
                int flick = input.TouchDown(center);
                yield return null;
                input.TouchMove(flick, center + new Vector2(0f, radius * 1.1f));
                yield return WaitReal(0.4f);
                input.TouchUp(flick, center);
                TouchCheck("a flick up on the stick jumps", hero.JumpsMade > jumps, $"{hero.JumpsMade - jumps} jumps");

                Run.Metric("touch.passed", touchPassed);
                Run.Metric("touch.failed", touchFailed);
                GameplayHud.TouchUiForced = false;
                Run.Progress(1f);
                End(touchFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }
        }
    }
}
