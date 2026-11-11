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
        /// loaded directly: the stick is on screen, the hints show, a virtual finger drags the stick and the hero walks,
        /// a second finger holds the rewind button (48+ px) while the first still drags, a flick up jumps, and the hints are gone and remembered.
        /// In the editor / standalone the window is also resized to phone portrait and phone landscape: portrait keeps the stick in the
        /// control strip below the playfield with a faint idle outline; landscape floats the stick under the thumb and fades it while
        /// untouched so bullets behind it stay visible.
        /// </summary>
        public const string TouchControlsId = "touch-controls";
        /// <summary>Stickers on the main menu at every size, then the touch-controls scenario (acceptance: stickers clear, landscape float/fade, move+rewind).</summary>
        public const string MenuTouchLayoutId = "menu-touch-layout";
        const int TouchDesktopLevel = 1;
        const int TouchLevel = 2;
        const float TouchControlsSeconds = 360f;
        const float MenuTouchLayoutSeconds = 480f;

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

            /// <summary>
            /// Acceptance criteria for the menu stickers + phone touch stick: audit the main menu's NOW WITH stickers at
            /// desktop / narrow / phone sizes, then run <see cref="TouchControlsRun"/>.
            /// </summary>
            public IEnumerator MenuTouchLayoutRun()
            {
                yield return ToMainMenu();
                if (Run.Done)
                    yield break;
                yield return Audit("Main Menu", 0, 0, 1.2f, false);
                if (Run.Done)
                    yield break;
                yield return MainMenuSizes();
                if (Run.Done)
                    yield break;
                Run.Progress(0.2f);
                yield return TouchControlsRun();
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
                Run.Progress(0.08f);

                // 2. A touch device at the runner's current size, then (where the window can resize) phone portrait and landscape.
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

                yield return TouchControlsAtSize("current", Screen.width, Screen.height, 0.15f, 0.55f);
                if (Run.Done)
                    yield break;

                if (CanResizeWindow)
                {
                    int w0 = Screen.width, h0 = Screen.height;
                    yield return TouchControlsAtSize("phone portrait", 390, 844, 0.6f, 0.75f);
                    if (Run.Done)
                        yield break;
                    yield return TouchControlsAtSize("phone landscape", 844, 390, 0.8f, 0.95f);
                    if (Run.Done)
                        yield break;
                    Screen.SetResolution(w0, h0, false);
                    yield return Run.Until(() => Screen.width == w0 && Screen.height == h0, 6f, "the window to return to its size");
                }
                else
                {
                    Run.Event("touch", $"sizes kept at {Screen.width}x{Screen.height}: a WebGL page's size is the browser window's, set by the runner's device");
                }

                Run.Metric("touch.passed", touchPassed);
                Run.Metric("touch.failed", touchFailed);
                GameplayHud.TouchUiForced = false;
                Run.Progress(1f);
                End(touchFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            /// <summary>Resize (when asked), then check stick placement, idle fade, move, rewind and jump at this size.</summary>
            IEnumerator TouchControlsAtSize(string sizeName, int width, int height, float progressStart, float progressEnd)
            {
                if (sizeName != "current")
                {
                    Screen.SetResolution(width, height, false);
                    yield return Run.Until(() => Screen.width == width && Screen.height == height, 6f, $"the window to resize to {width}x{height}");
                    if (Run.Done)
                        yield break;
                    yield return WaitReal(0.6f);
                }

                var world = GameplayWorld.Instance;
                if (world == null || world.Hud == null || world.PlayerController == null)
                {
                    TouchCheck($"{sizeName}: level still up", false, "world or hud missing");
                    yield break;
                }
                var hud = world.Hud;
                var hero = world.PlayerController;
                var health = hero.GetComponent<PlayerHealth>();
                yield return Run.Until(() => hud.TouchStickShown, 10f, $"the touch stick at {sizeName}");
                if (Run.Done)
                    yield break;
                Time.timeScale = Speed;

                bool portrait = Screen.height > Screen.width;
                var ring = hud.TouchStickScreenRect;
                var zone = hud.TouchStickZoneScreenRect;
                var play = world.Camera != null ? world.Camera.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
                bool outside = !play.Overlaps(ring);
                Run.Event("touch", $"{sizeName} {(portrait ? "portrait" : "landscape")} {Screen.width}x{Screen.height}: stick ring {ring}, zone {zone}, playfield {play}");
                Run.Scene($"touch {sizeName}");

                TouchCheck($"{sizeName}: move stick visible", hud.TouchStickShown && ring.width > 40f, $"ring {ring.width:0}x{ring.height:0} px");
                if (portrait)
                    TouchCheck($"{sizeName}: stick sits in the control strip below the playfield", outside || ring.center.y < play.yMin + 8f,
                        $"playfield {play}, ring {ring}");
                else
                    TouchCheck($"{sizeName}: stick zone is the lower-left float area",
                        zone.width > 40f && zone.center.x < Screen.width * 0.55f && zone.center.y < Screen.height * 0.55f,
                        $"zone {zone}");

                var rewindRect = hud.RewindButtonScreenRect;
                TouchCheck($"{sizeName}: rewind button is at least 48 px", rewindRect.width >= 48f && rewindRect.height >= 48f,
                    $"{rewindRect.width:0}x{rewindRect.height:0} px");
                Run.Metric($"touch.{sizeName.Replace(' ', '_')}.rewindButtonPx", Mathf.Min(rewindRect.width, rewindRect.height));
                Run.Metric($"touch.{sizeName.Replace(' ', '_')}.stickPx", ring.width);

                // Idle: faint outline (portrait ~30%) or faded out (landscape over bullets).
                yield return WaitReal(0.5f);
                float idleAlpha = hud.TouchStickAlpha;
                if (portrait)
                    TouchCheck($"{sizeName}: idle outline is a faint ~30%", idleAlpha > 0.15f && idleAlpha < 0.45f && !hud.TouchStickHeld,
                        $"alpha {idleAlpha:0.00}");
                else
                    TouchCheck($"{sizeName}: idle stick fades while untouched", idleAlpha < 0.2f && !hud.TouchStickHeld,
                        $"alpha {idleAlpha:0.00}");
                Run.Metric($"touch.{sizeName.Replace(' ', '_')}.idleAlpha", idleAlpha);
                Run.Progress(progressStart);

                // Drag: in landscape the ring must float under the thumb; in both orientations the hero walks.
                if (health != null)
                {
                    health.SetHealth(health.MaxHealth);
                    health.SetInvincibilityTimer(60f);
                }
                yield return Run.Until(() => hero.IsGrounded, 20f, "the hero on the ground");
                if (Run.Done)
                    yield break;
                float x0 = hero.transform.position.x;
                // Start the finger inside the zone (not necessarily on the faded ring).
                var touchAt = new Vector2(
                    Mathf.Clamp(zone.center.x + zone.width * 0.15f, zone.xMin + 8f, zone.xMax - 8f),
                    Mathf.Clamp(zone.center.y + zone.height * 0.1f, zone.yMin + 8f, zone.yMax - 8f));
                int stickFinger = input.TouchDown(touchAt);
                yield return null;
                yield return WaitReal(0.15f);
                if (!portrait)
                {
                    var heldRing = hud.TouchStickScreenRect;
                    float dist = Vector2.Distance(heldRing.center, touchAt);
                    TouchCheck($"{sizeName}: landscape stick appears under the thumb",
                        hud.TouchStickHeld && dist < Mathf.Max(40f, heldRing.width * 0.6f),
                        $"thumb {touchAt}, ring centre {heldRing.center}, dist {dist:0}");
                }
                TouchCheck($"{sizeName}: stick lights up while held", hud.TouchStickHeld && hud.TouchStickAlpha > 0.7f,
                    $"held {hud.TouchStickHeld}, alpha {hud.TouchStickAlpha:0.00}");

                float radius = Mathf.Max(24f, hud.TouchStickScreenRect.width * 0.5f);
                for (int i = 1; i <= 6; i++)
                {
                    input.TouchMove(stickFinger, touchAt + new Vector2(radius * 0.9f * i / 6f, 0f));
                    yield return null;
                }
                yield return WaitReal(1.2f);
                float x1 = hero.transform.position.x;
                Run.Event("touch", $"{sizeName}: dragged right: hero x {x0:0.00} -> {x1:0.00}, stick axis {hud.TouchStickAxis:0.00}");
                TouchCheck($"{sizeName}: dragging the stick moves the hero", x1 - x0 > 0.5f && hud.TouchStickAxis > 0.3f,
                    $"x {x0:0.00} -> {x1:0.00}, axis {hud.TouchStickAxis:0.00}");
                TouchCheck($"{sizeName}: move hint goes after the first drag", PlayerPrefs.GetInt("TouchStickMoved", 0) == 1, "remembered in PlayerPrefs");
                Run.Progress((progressStart + progressEnd) * 0.5f);

                // Rewind while the stick finger is still down.
                rewindRect = hud.RewindButtonScreenRect;
                var rewindAt = rewindRect.center;
                int rewindFinger = input.TouchDown(rewindAt);
                yield return Run.Until(() => GameClock.Instance != null && GameClock.Instance.IsRewinding, 4f, "the rewind to start (touch)");
                bool rewound = GameClock.Instance != null && GameClock.Instance.IsRewinding;
                float axisWhile = hud.TouchStickAxis;
                yield return WaitReal(0.7f);
                input.TouchUp(rewindFinger, rewindAt);
                yield return Run.Until(() => GameClock.Instance != null && !GameClock.Instance.IsRewinding, 5f, "the rewind to end");
                TouchCheck($"{sizeName}: rewind works while the stick finger is down", rewound && axisWhile > 0.3f,
                    $"rewinding {rewound}, stick axis {axisWhile:0.00}");
                input.TouchUp(stickFinger, touchAt);
                yield return WaitReal(0.55f);
                float afterAlpha = hud.TouchStickAlpha;
                if (!portrait)
                    TouchCheck($"{sizeName}: stick fades again after release", afterAlpha < 0.25f && !hud.TouchStickHeld,
                        $"alpha {afterAlpha:0.00}");
                TouchCheck($"{sizeName}: rewind hint goes after the first rewind",
                    PlayerPrefs.GetInt("TouchStickRewound", 0) == 1 && !hud.TouchHintsShown, $"hints shown {hud.TouchHintsShown}");

                // Flick up jumps.
                yield return Run.Until(() => hero.IsGrounded, 20f, "the hero on the ground");
                yield return WaitReal(0.25f);
                int jumps = hero.JumpsMade;
                ring = hud.TouchStickScreenRect;
                var flickAt = portrait ? ring.center : touchAt;
                radius = Mathf.Max(24f, (portrait ? ring.width : zone.width) * 0.35f);
                int flick = input.TouchDown(flickAt);
                yield return null;
                input.TouchMove(flick, flickAt + new Vector2(0f, radius * 1.2f));
                yield return WaitReal(0.4f);
                input.TouchUp(flick, flickAt);
                TouchCheck($"{sizeName}: a flick up on the stick jumps", hero.JumpsMade > jumps, $"{hero.JumpsMade - jumps} jumps");
                Run.Progress(progressEnd);
            }
        }
    }
}
