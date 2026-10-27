using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Core.Input;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The tap-jump scenario: Level 1 loaded directly (setup shortcut), no bot. A virtual touchscreen taps the middle of the screen
        /// 200 times (30-120 ms down, one in ten is down and up inside one frame, every twentieth has the frame stall 150 ms while the finger is down);
        /// each tap must make exactly one jump. Then a tap while R rewinds must not jump when the rewind ends.
        /// </summary>
        public const string TapJumpId = "tap-jump";
        const int TapJumpLevel = 1;
        const int TapJumpTaps = 200;
        const float TapJumpSeconds = 420f;

        sealed partial class Session
        {
            int tapJumpPassed, tapJumpFailed;

            void TapJumpCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    tapJumpPassed++;
                    Run.Event("check", $"tap-jump {beat}: ok ({detail})");
                }
                else
                {
                    tapJumpFailed++;
                    Run.Event("check", $"tap-jump {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"tap-jump {beat}: {detail}");
                }
            }

            public IEnumerator TapJumpRun()
            {
                Run.Event("shortcut", $"level {TapJumpLevel} loaded directly");
                var load = LoadLevel(TapJumpLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => LevelRunning(TapJumpLevel) && GameplayWorld.Instance != null && GameplayWorld.Instance.PlayerController != null, 60f, "the level");
                if (Run.Done)
                    yield break;
                var world = GameplayWorld.Instance;
                var hero = world.PlayerController;
                var health = hero.GetComponent<PlayerHealth>();
                var at = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                var random = new System.Random(7);
                Run.Progress(0.05f);

                // 1. 200 taps, one jump each.
                int wrong = 0, oneFrame = 0, stalled = 0;
                for (int i = 0; i < TapJumpTaps && !Run.Done; i++)
                {
                    yield return Run.Until(() => hero.IsGrounded && hero.PendingJumps == 0, 20f, $"the hero on the ground before tap {i + 1}");
                    if (Run.Done)
                        yield break;
                    yield return null;
                    if (health != null)
                    {
                        health.SetHealth(health.MaxHealth);
                        health.SetInvincibilityTimer(2f);
                    }
                    int before = hero.JumpsMade;
                    int id = input.TouchDown(at);
                    if (i % 10 == 9)
                    {
                        // Down and up in the same frame: the old poll saw only the end of it.
                        input.TouchUp(id, at);
                        oneFrame++;
                        yield return null;
                    }
                    else
                    {
                        if (i % 20 == 4)
                        {
                            // A hitch with the finger down: the frame takes 150 ms.
                            var watch = System.Diagnostics.Stopwatch.StartNew();
                            while (watch.ElapsedMilliseconds < 150)
                            { }
                            stalled++;
                        }
                        float hold = Time.realtimeSinceStartup + (30f + (float)random.NextDouble() * 90f) / 1000f;
                        while (Time.realtimeSinceStartup < hold)
                            yield return null;
                        input.TouchUp(id, at);
                        yield return null;
                    }
                    yield return Run.Until(() => hero.JumpsMade > before, 1.0f, $"the jump of tap {i + 1}");
                    if (Run.Done)
                        yield break;
                    // Let the jump finish (a second jump from the same tap would show up here).
                    yield return WaitReal(0.15f);
                    int made = hero.JumpsMade - before;
                    if (made != 1)
                    {
                        wrong++;
                        Run.Event("tap-jump", $"tap {i + 1}: {made} jumps");
                    }
                    if (i % 25 == 0)
                        Run.Progress(0.05f + 0.75f * i / TapJumpTaps);
                }
                Run.Metric("tapJump.taps", TapJumpTaps);
                Run.Metric("tapJump.oneFrameTaps", oneFrame);
                Run.Metric("tapJump.stalledTaps", stalled);
                Run.Metric("tapJump.wrongTaps", wrong);
                TapJumpCheck("exactly one jump per tap", wrong == 0, $"{TapJumpTaps} taps ({oneFrame} inside one frame, {stalled} with a 150 ms stall), {wrong} taps without exactly one jump");

                // 2. A tap while rewinding must not jump when the rewind ends.
                yield return Run.Until(() => hero.IsGrounded, 20f, "the hero on the ground");
                yield return WaitReal(1.5f);
                int jumpsBefore = hero.JumpsMade;
                input.SetKey(UnityEngine.InputSystem.Key.R, true);
                yield return Run.Until(() => GameClock.Instance != null && GameClock.Instance.IsRewinding, 5f, "the rewind to start");
                if (Run.Done)
                    yield break;
                yield return WaitReal(0.3f);
                int rewindId = input.TouchDown(at);
                yield return WaitReal(0.15f);
                input.TouchUp(rewindId, at);
                yield return WaitReal(0.2f);
                TapJumpCheck("a tap while rewinding is not latched", hero.PendingJumps == 0, $"{hero.PendingJumps} presses waiting during the rewind");
                input.SetKey(UnityEngine.InputSystem.Key.R, false);
                yield return Run.Until(() => GameClock.Instance != null && !GameClock.Instance.IsRewinding, 5f, "the rewind to end");
                yield return WaitReal(0.6f);
                TapJumpCheck("no jump when the rewind ends", hero.JumpsMade == jumpsBefore && hero.PendingJumps == 0, $"{hero.JumpsMade - jumpsBefore} jumps after it, {hero.PendingJumps} presses waiting");

                Run.Metric("tapJump.passed", tapJumpPassed);
                Run.Metric("tapJump.failed", tapJumpFailed);
                Run.Progress(1f);
                End(tapJumpFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }
        }
    }
}
